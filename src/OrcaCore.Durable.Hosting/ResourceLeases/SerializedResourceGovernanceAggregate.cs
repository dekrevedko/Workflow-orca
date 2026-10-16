using System.Security.Cryptography;
using System.Text.Json;
using OrcaCore.Abstractions.Ids;
using OrcaCore.Abstractions.Primitives;
using OrcaCore.Abstractions.Providers;
using OrcaCore.Provider.Abstractions.ResourceGovernance;
using OrcaCore.Runtime.Protocol.ResourceGovernance;

namespace OrcaCore.Hosting.ResourceLeases;

/// <summary>
/// Serializes every durable resource-pool mutation through the provider-owned
/// expected-version governance stream. The persisted record is a complete
/// detached aggregate state, so acquisition, direct transfer, resize debt,
/// review marks, and management projections cannot drift across stores.
/// </summary>
internal sealed class SerializedResourceGovernanceAggregate(
    IDurableResourceGovernanceStore store,
    DurableResourcePoolOptions options) :
    IResourcePoolStore
{
    private const int MaxConflictRetries = 32;
    private const string StateCommitted = "state-committed";

    private readonly DurableResourcePoolDefinition[] configuredPools =
        ValidateAndCopyOptions(options);
    private int initialized;

    internal async ValueTask InitializeAsync(CancellationToken cancellationToken = default)
    {
        for (var retry = 0; retry < MaxConflictRetries; retry++)
        {
            var stream = await store.LoadAsync(options.PartitionId, cancellationToken).ConfigureAwait(false);
            var state = Replay(stream);
            if (state.Pools.Length != 0)
            {
                ValidateCreationAgreement(state);
                Volatile.Write(ref initialized, 1);
                return;
            }

            if (configuredPools.Length == 0)
            {
                Volatile.Write(ref initialized, 1);
                return;
            }

            var created = state with
            {
                Pools = configuredPools
                    .Select(definition => new PoolDocument(
                        definition.Name.Value,
                        definition.Capacity,
                        definition.Capacity,
                        definition.ReviewAfter.Ticks,
                        0))
                    .ToArray()
            };
            if (await TryAppendAsync(stream.Version, "pools-created", created, cancellationToken)
                    .ConfigureAwait(false))
            {
                Volatile.Write(ref initialized, 1);
                return;
            }
        }

        throw SerializationFailure("startup");
    }

    internal async ValueTask<IReadOnlyList<DurableResourcePoolSnapshot>> ListManagementAsync(
        CancellationToken cancellationToken = default)
    {
        await EnsureInitializedAsync(cancellationToken).ConfigureAwait(false);
        var state = Replay(await store.LoadAsync(options.PartitionId, cancellationToken).ConfigureAwait(false));
        return state.Pools
            .OrderBy(pool => pool.Name, StringComparer.Ordinal)
            .Select(pool => ManagementSnapshot(state, pool))
            .ToArray();
    }

    internal async ValueTask<DurableResourcePoolSnapshot> GetManagementAsync(
        ResourcePoolName pool,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(pool);
        await EnsureInitializedAsync(cancellationToken).ConfigureAwait(false);
        var state = Replay(await store.LoadAsync(options.PartitionId, cancellationToken).ConfigureAwait(false));
        var found = FindPool(state, pool.Value) ??
            throw ResourcePoolNotConfiguredException.For([pool]);
        return ManagementSnapshot(state, found);
    }

    internal async ValueTask<DurableResourcePoolResizeResult> ResizeManagementAsync(
        ResourcePoolName pool,
        int capacity,
        ResourcePoolOperationId operationId,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(pool);
        ArgumentOutOfRangeException.ThrowIfLessThan(capacity, 1);
        ArgumentNullException.ThrowIfNull(operationId);
        await EnsureInitializedAsync(cancellationToken).ConfigureAwait(false);

        for (var retry = 0; retry < MaxConflictRetries; retry++)
        {
            var stream = await store.LoadAsync(options.PartitionId, cancellationToken).ConfigureAwait(false);
            var state = Replay(stream);
            var found = FindPool(state, pool.Value) ??
                throw ResourcePoolNotConfiguredException.For([pool]);
            var recorded = state.ResizeOperations.SingleOrDefault(candidate =>
                string.Equals(candidate.OperationId, operationId.Value, StringComparison.Ordinal));
            if (recorded is not null)
            {
                if (string.Equals(recorded.PoolName, pool.Value, StringComparison.Ordinal) &&
                    recorded.Capacity == capacity)
                {
                    return new DurableResourcePoolResizeResult.Applied(
                        operationId,
                        pool,
                        capacity,
                        ManagementSnapshot(state, found));
                }

                return new DurableResourcePoolResizeResult.Conflict(
                    operationId,
                    ResourcePoolName.Create(recorded.PoolName),
                    recorded.Capacity,
                    pool,
                    capacity);
            }

            var resized = found with { CurrentCapacity = capacity };
            var updated = state with
            {
                Pools = Replace(state.Pools, found, resized),
                ResizeOperations =
                [
                    .. state.ResizeOperations,
                    new ResizeOperationDocument(operationId.Value, pool.Value, capacity)
                ]
            };
            if (await TryAppendAsync(stream.Version, "pool-resized", updated, cancellationToken)
                    .ConfigureAwait(false))
            {
                return new DurableResourcePoolResizeResult.Applied(
                    operationId,
                    pool,
                    capacity,
                    ManagementSnapshot(updated, resized));
            }
        }

        throw SerializationFailure("resize");
    }

    public async Task UpsertPoolAsync(
        ResourcePoolDefinition definition,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(definition);
        ArgumentException.ThrowIfNullOrWhiteSpace(definition.Name);
        ArgumentOutOfRangeException.ThrowIfNegative(definition.Capacity);
        cancellationToken.ThrowIfCancellationRequested();

        for (var retry = 0; retry < MaxConflictRetries; retry++)
        {
            var stream = await store.LoadAsync(options.PartitionId, cancellationToken).ConfigureAwait(false);
            var state = Replay(stream);
            var existing = FindPool(state, definition.Name);
            if (existing is not null)
            {
                if (existing.CreationCapacity != definition.Capacity ||
                    existing.ReviewAfterTicks != definition.LeaseDuration?.Ticks)
                {
                    throw new InvalidOperationException(
                        $"Resource pool '{definition.Name}' creation definition does not match persisted governance state.");
                }

                return;
            }

            var added = new PoolDocument(
                definition.Name,
                definition.Capacity,
                definition.Capacity,
                definition.LeaseDuration?.Ticks,
                0);
            var updated = state with { Pools = [.. state.Pools, added] };
            if (await TryAppendAsync(stream.Version, "pool-created", updated, cancellationToken)
                    .ConfigureAwait(false))
            {
                return;
            }
        }

        throw SerializationFailure("pool creation");
    }

    public async Task<ResourcePoolAcquireResult> AcquireAsync(
        ResourcePoolAcquireRequest request,
        CancellationToken cancellationToken)
    {
        ValidateAcquireRequest(request);
        await EnsureInitializedAsync(cancellationToken).ConfigureAwait(false);

        for (var retry = 0; retry < MaxConflictRetries; retry++)
        {
            var stream = await store.LoadAsync(options.PartitionId, cancellationToken).ConfigureAwait(false);
            var state = Replay(stream);
            if (request.Requirements.Any(requirement => FindPool(state, requirement.PoolName) is null))
            {
                return new ResourcePoolAcquireResult(
                    ResourcePoolAcquireStatus.Rejected,
                    [],
                    null,
                    "One or more resource pools do not exist.");
            }

            var held = TicketsFor(state, request.HolderInstanceId, request.HolderKey);
            if (Satisfies(held, request.Requirements))
            {
                return new ResourcePoolAcquireResult(
                    ResourcePoolAcquireStatus.Granted,
                    held.Select(MaterializeTicket).ToArray(),
                    null,
                    null);
            }

            var queued = FindWaiter(state, request.HolderInstanceId, request.HolderKey);
            if (queued is not null)
            {
                return new ResourcePoolAcquireResult(
                    ResourcePoolAcquireStatus.Queued,
                    [],
                    MaterializeWaiter(queued),
                    null);
            }

            if (state.Waiters.Length == 0 && CanGrant(state, request.Requirements))
            {
                var (grantedState, tickets) = Grant(state, request, request.RequestedAt);
                if (await TryAppendAsync(stream.Version, "reservation-granted", grantedState, cancellationToken)
                        .ConfigureAwait(false))
                {
                    return new ResourcePoolAcquireResult(
                        ResourcePoolAcquireStatus.Granted,
                        tickets.Select(MaterializeTicket).ToArray(),
                        null,
                        null);
                }

                continue;
            }

            var waiter = new WaiterDocument(
                Guid.CreateVersion7().ToString("N"),
                request.HolderInstanceId.ToString(),
                request.HolderKey,
                request.Requirements.Select(requirement =>
                    new RequirementDocument(requirement.PoolName, requirement.Count)).ToArray(),
                request.RequestedAt,
                request.ExpiresAt,
                request.FiberId?.Value,
                request.ScopeId?.Value,
                request.ProtectionToken?.Value);
            var queuedState = state with { Waiters = [.. state.Waiters, waiter] };
            if (await TryAppendAsync(stream.Version, "reservation-queued", queuedState, cancellationToken)
                    .ConfigureAwait(false))
            {
                return new ResourcePoolAcquireResult(
                    ResourcePoolAcquireStatus.Queued,
                    [],
                    MaterializeWaiter(waiter),
                    null);
            }
        }

        throw SerializationFailure("acquisition");
    }

    public async Task<ResourcePoolReleaseResult> ReleaseAsync(
        ResourcePoolReleaseRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentException.ThrowIfNullOrWhiteSpace(request.HolderKey);
        await EnsureInitializedAsync(cancellationToken).ConfigureAwait(false);

        for (var retry = 0; retry < MaxConflictRetries; retry++)
        {
            var stream = await store.LoadAsync(options.PartitionId, cancellationToken).ConfigureAwait(false);
            var state = Replay(stream);
            var (working, released, granted) = ReleaseHolder(
                state,
                request.HolderInstanceId,
                request.HolderKey,
                request.ReleasedAt,
                confirmationId: null);

            var changed = released.Length != 0 ||
                state.Waiters.Length != working.Waiters.Length ||
                granted.Length != 0;
            if (!changed)
            {
                return new ResourcePoolReleaseResult([], []);
            }

            if (await TryAppendAsync(stream.Version, "holder-released", working, cancellationToken)
                    .ConfigureAwait(false))
            {
                return new ResourcePoolReleaseResult(
                    released.Select(MaterializeTicket).ToArray(),
                    granted.Select(MaterializeWaiter).ToArray());
            }
        }

        throw SerializationFailure("release");
    }

    public async Task<Option<ResourcePoolReleaseEvidence>> GetReleaseEvidenceAsync(
        LeaseProtectionToken protectionToken,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(protectionToken);
        await EnsureInitializedAsync(cancellationToken).ConfigureAwait(false);
        var state = Replay(await store.LoadAsync(options.PartitionId, cancellationToken).ConfigureAwait(false));
        var evidence = state.ReleaseEvidence.LastOrDefault(candidate =>
            string.Equals(candidate.ProtectionToken, protectionToken.Value, StringComparison.Ordinal));
        return evidence is null
            ? Option<ResourcePoolReleaseEvidence>.None
            : Option<ResourcePoolReleaseEvidence>.Some(MaterializeEvidence(evidence));
    }

    public async Task<Option<LeaseProtectionToken>> GetConfirmationBindingAsync(
        StopConfirmationId confirmationId,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(confirmationId);
        await EnsureInitializedAsync(cancellationToken).ConfigureAwait(false);
        var state = Replay(await store.LoadAsync(options.PartitionId, cancellationToken).ConfigureAwait(false));
        var binding = state.ConfirmationBindings.SingleOrDefault(candidate =>
            string.Equals(
                candidate.ConfirmationId,
                confirmationId.Value,
                StringComparison.Ordinal));
        return binding is null
            ? Option<LeaseProtectionToken>.None
            : Option<LeaseProtectionToken>.Some(
                LeaseProtectionToken.Parse(binding.ProtectionToken));
    }

    public async Task<ResourcePoolStopConfirmationStatus> ConfirmAndReleaseAsync(
        ResourcePoolStopConfirmationRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(request.HolderInstanceId);
        ArgumentException.ThrowIfNullOrWhiteSpace(request.HolderKey);
        ArgumentNullException.ThrowIfNull(request.ProtectionToken);
        ArgumentNullException.ThrowIfNull(request.ConfirmationId);
        await EnsureInitializedAsync(cancellationToken).ConfigureAwait(false);

        for (var retry = 0; retry < MaxConflictRetries; retry++)
        {
            var stream = await store.LoadAsync(options.PartitionId, cancellationToken).ConfigureAwait(false);
            var state = Replay(stream);
            var boundById = state.ConfirmationBindings.SingleOrDefault(candidate =>
                string.Equals(
                    candidate.ConfirmationId,
                    request.ConfirmationId.Value,
                    StringComparison.Ordinal));
            if (boundById is not null)
            {
                return string.Equals(
                    boundById.ProtectionToken,
                    request.ProtectionToken.Value,
                    StringComparison.Ordinal)
                    ? ResourcePoolStopConfirmationStatus.AlreadyConfirmed
                    : ResourcePoolStopConfirmationStatus.ConfirmationConflict;
            }

            var prior = state.ReleaseEvidence.LastOrDefault(candidate =>
                string.Equals(
                    candidate.ProtectionToken,
                    request.ProtectionToken.Value,
                    StringComparison.Ordinal));
            if (prior?.ConfirmationId is not null)
            {
                return ResourcePoolStopConfirmationStatus.AlreadyConfirmed;
            }

            if (prior is not null)
            {
                return ResourcePoolStopConfirmationStatus.TokenNotFound;
            }

            var held = TicketsFor(state, request.HolderInstanceId, request.HolderKey);
            if (held.Length == 0 ||
                held.Any(ticket => !string.Equals(
                    ticket.ProtectionToken,
                    request.ProtectionToken.Value,
                    StringComparison.Ordinal)))
            {
                return ResourcePoolStopConfirmationStatus.TokenNotFound;
            }

            var (releasedState, released, _) = ReleaseHolder(
                state,
                request.HolderInstanceId,
                request.HolderKey,
                request.ConfirmedAt,
                request.ConfirmationId.Value);
            releasedState = releasedState with
            {
                ConfirmationBindings =
                [
                    .. releasedState.ConfirmationBindings,
                    new ConfirmationBindingDocument(
                        request.ConfirmationId.Value,
                        request.ProtectionToken.Value)
                ]
            };
            if (released.Length != 0 &&
                await TryAppendAsync(
                        stream.Version,
                        "confirmed-holder-released",
                        releasedState,
                        cancellationToken)
                    .ConfigureAwait(false))
            {
                return ResourcePoolStopConfirmationStatus.Released;
            }
        }

        throw SerializationFailure("stop confirmation");
    }

    public async Task PurgeReleaseEvidenceAsync(
        InstanceId instanceId,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(instanceId);
        await EnsureInitializedAsync(cancellationToken).ConfigureAwait(false);
        for (var retry = 0; retry < MaxConflictRetries; retry++)
        {
            var stream = await store.LoadAsync(options.PartitionId, cancellationToken).ConfigureAwait(false);
            var state = Replay(stream);
            var instance = instanceId.ToString();
            if (state.Tickets.Any(ticket =>
                    string.Equals(ticket.InstanceId, instance, StringComparison.OrdinalIgnoreCase)) ||
                state.Waiters.Any(waiter =>
                    string.Equals(waiter.InstanceId, instance, StringComparison.OrdinalIgnoreCase)))
            {
                throw new InvalidOperationException(
                    "Active resource ownership cannot be purged.");
            }

            var removedTokens = state.ReleaseEvidence
                .Where(candidate => string.Equals(
                    candidate.InstanceId,
                    instance,
                    StringComparison.OrdinalIgnoreCase))
                .Select(candidate => candidate.ProtectionToken)
                .Where(value => value is not null)
                .ToHashSet(StringComparer.Ordinal);
            if (removedTokens.Count == 0 &&
                state.ReleaseEvidence.All(candidate => !string.Equals(
                    candidate.InstanceId,
                    instance,
                    StringComparison.OrdinalIgnoreCase)))
            {
                return;
            }

            var updated = state with
            {
                ReleaseEvidence = state.ReleaseEvidence
                    .Where(candidate => !string.Equals(
                        candidate.InstanceId,
                        instance,
                        StringComparison.OrdinalIgnoreCase))
                    .ToArray(),
                ConfirmationBindings = state.ConfirmationBindings
                    .Where(binding => !removedTokens.Contains(binding.ProtectionToken))
                    .ToArray()
            };
            if (await TryAppendAsync(
                    stream.Version,
                    "release-evidence-purged",
                    updated,
                    cancellationToken).ConfigureAwait(false))
            {
                return;
            }
        }

        throw SerializationFailure("release-evidence purge");
    }

    public async Task<Option<ResourcePoolSnapshot>> GetPoolAsync(
        string poolName,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(poolName);
        await EnsureInitializedAsync(cancellationToken).ConfigureAwait(false);
        var state = Replay(await store.LoadAsync(options.PartitionId, cancellationToken).ConfigureAwait(false));
        var pool = FindPool(state, poolName);
        return pool is null
            ? Option<ResourcePoolSnapshot>.None
            : Option<ResourcePoolSnapshot>.Some(ProviderSnapshot(state, pool));
    }

    public async Task<IReadOnlyList<ResourcePoolSnapshot>> ListPoolsAsync(
        CancellationToken cancellationToken)
    {
        await EnsureInitializedAsync(cancellationToken).ConfigureAwait(false);
        var state = Replay(await store.LoadAsync(options.PartitionId, cancellationToken).ConfigureAwait(false));
        return state.Pools
            .OrderBy(pool => pool.Name, StringComparer.Ordinal)
            .Select(pool => ProviderSnapshot(state, pool))
            .ToArray();
    }

    public async Task<ResourcePoolExpiryResult> ExpireTicketsAsync(
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        await EnsureInitializedAsync(cancellationToken).ConfigureAwait(false);
        for (var retry = 0; retry < MaxConflictRetries; retry++)
        {
            var stream = await store.LoadAsync(options.PartitionId, cancellationToken).ConfigureAwait(false);
            var state = Replay(stream);
            var newlyExpired = state.Tickets
                .Where(ticket =>
                    ticket.ReviewDeadline is { } deadline &&
                    deadline <= now &&
                    !ticket.ReviewMarked)
                .ToArray();
            if (newlyExpired.Length == 0)
            {
                return new ResourcePoolExpiryResult([]);
            }

            var ids = newlyExpired.Select(ticket => ticket.TicketId).ToHashSet(StringComparer.Ordinal);
            var updated = state with
            {
                Tickets = state.Tickets
                    .Select(ticket => ids.Contains(ticket.TicketId)
                        ? ticket with { ReviewMarked = true }
                        : ticket)
                    .ToArray(),
                ExpiredTickets =
                [
                    .. state.ExpiredTickets,
                    .. newlyExpired.Select(ticket => new ExpiredTicketDocument(ticket.TicketId, now))
                ]
            };
            if (await TryAppendAsync(stream.Version, "tickets-review-marked", updated, cancellationToken)
                    .ConfigureAwait(false))
            {
                return new ResourcePoolExpiryResult(
                    newlyExpired
                        .Select(ticket => new ResourcePoolExpiredTicket(
                            MaterializeTicket(ticket with { ReviewMarked = true }),
                            now))
                        .ToArray());
            }
        }

        throw SerializationFailure("ticket review");
    }

    private async ValueTask EnsureInitializedAsync(CancellationToken cancellationToken)
    {
        if (Volatile.Read(ref initialized) == 0)
        {
            await InitializeAsync(cancellationToken).ConfigureAwait(false);
        }
    }

    private async ValueTask<bool> TryAppendAsync(
        long expectedVersion,
        string operation,
        GovernanceDocument state,
        CancellationToken cancellationToken)
    {
        var record = CreateRecord(
            expectedVersion + 1,
            new GovernanceEnvelope(StateCommitted, operation, state));
        var append = await store.AppendAsync(
            options.PartitionId,
            expectedVersion,
            [record],
            cancellationToken).ConfigureAwait(false);
        return append is ResourceGovernanceAppendResult.Committed;
    }

    private void ValidateCreationAgreement(GovernanceDocument state)
    {
        if (state.Pools.Length != configuredPools.Length)
        {
            throw new InvalidOperationException(
                "Configured durable resource pools do not match persisted creation definitions.");
        }

        foreach (var configured in configuredPools)
        {
            var persisted = FindPool(state, configured.Name.Value);
            if (persisted is null ||
                configured.Capacity != persisted.CreationCapacity ||
                configured.ReviewAfter.Ticks != persisted.ReviewAfterTicks)
            {
                throw new InvalidOperationException(
                    $"Durable resource pool '{configured.Name}' does not match its persisted creation definition.");
            }
        }
    }

    private static GovernanceDocument Replay(ResourceGovernanceStream stream)
    {
        var state = GovernanceDocument.Empty;
        foreach (var record in stream.Records)
        {
            var envelope = JsonSerializer.Deserialize<GovernanceEnvelope>(record.Payload.Span) ??
                throw new InvalidOperationException("A resource governance record was empty.");
            if (!string.Equals(envelope.Kind, StateCommitted, StringComparison.Ordinal))
            {
                throw new InvalidOperationException(
                    $"Unknown durable resource-governance record '{envelope.Kind}'.");
            }

            state = ValidateDocument(envelope.State);
        }

        return state;
    }

    private static GovernanceDocument ValidateDocument(GovernanceDocument candidate)
    {
        ArgumentNullException.ThrowIfNull(candidate);
        if (candidate.Pools.Select(pool => pool.Name).Distinct(StringComparer.Ordinal).Count() !=
            candidate.Pools.Length)
        {
            throw new InvalidOperationException("A governance state contains duplicate pools.");
        }

        if (candidate.Tickets.Select(ticket => ticket.TicketId).Distinct(StringComparer.Ordinal).Count() !=
            candidate.Tickets.Length ||
            candidate.Waiters.Select(waiter => waiter.WaiterId).Distinct(StringComparer.Ordinal).Count() !=
            candidate.Waiters.Length ||
            candidate.ResizeOperations.Select(operation => operation.OperationId)
                .Distinct(StringComparer.Ordinal).Count() != candidate.ResizeOperations.Length ||
            candidate.ConfirmationBindings.Select(binding => binding.ConfirmationId)
                .Distinct(StringComparer.Ordinal).Count() != candidate.ConfirmationBindings.Length)
        {
            throw new InvalidOperationException("A governance state contains duplicate durable identities.");
        }

        if (candidate.Pools.Any(pool =>
                string.IsNullOrWhiteSpace(pool.Name) ||
                pool.CreationCapacity < 0 ||
                pool.CurrentCapacity < 1 ||
                pool.NextProviderGeneration < 0) ||
            candidate.Tickets.Any(ticket =>
                string.IsNullOrWhiteSpace(ticket.TicketId) ||
                ticket.Units <= 0 ||
                ticket.ProviderGeneration <= 0 ||
                FindPool(candidate, ticket.PoolName) is null) ||
            candidate.Waiters.Any(waiter =>
                waiter.Requirements.Length == 0 ||
                waiter.Requirements.Any(requirement =>
                    requirement.Units <= 0 ||
                    FindPool(candidate, requirement.PoolName) is null)) ||
            candidate.ReleaseEvidence.Any(evidence =>
                string.IsNullOrWhiteSpace(evidence.InstanceId) ||
                string.IsNullOrWhiteSpace(evidence.HolderKey) ||
                evidence.ReleasedTickets.Length == 0) ||
            candidate.ConfirmationBindings.Any(binding =>
                string.IsNullOrWhiteSpace(binding.ConfirmationId) ||
                string.IsNullOrWhiteSpace(binding.ProtectionToken)))
        {
            throw new InvalidOperationException("A governance state contains malformed resource accounting.");
        }

        return candidate;
    }

    private static ResourceGovernanceRecord CreateRecord(long sequence, GovernanceEnvelope value)
    {
        var payload = JsonSerializer.SerializeToUtf8Bytes(value);
        var checksum = Convert.ToHexString(SHA256.HashData(payload)).ToLowerInvariant();
        return ResourceGovernanceRecord.FromPersisted(
            sequence,
            ResourceGovernanceRecord.V1Format,
            payload,
            checksum);
    }

    private static (GovernanceDocument State, TicketDocument[] Tickets) Grant(
        GovernanceDocument state,
        ResourcePoolAcquireRequest request,
        DateTimeOffset acquiredAt)
    {
        var pools = state.Pools.ToArray();
        var granted = new List<TicketDocument>();
        foreach (var requirement in request.Requirements.OrderBy(
                     requirement => requirement.PoolName,
                     StringComparer.Ordinal))
        {
            var poolIndex = Array.FindIndex(
                pools,
                pool => string.Equals(pool.Name, requirement.PoolName, StringComparison.Ordinal));
            var pool = pools[poolIndex];
            var generation = pool.NextProviderGeneration + 1;
            pools[poolIndex] = pool with { NextProviderGeneration = generation };
            granted.Add(new TicketDocument(
                Guid.CreateVersion7().ToString("N"),
                requirement.PoolName,
                requirement.Count,
                request.HolderInstanceId.ToString(),
                request.HolderKey,
                acquiredAt,
                request.ExpiresAt,
                request.FiberId?.Value,
                request.ScopeId?.Value,
                generation,
                pool.ReviewAfterTicks is { } ticks ? acquiredAt + TimeSpan.FromTicks(ticks) : null,
                false,
                request.ProtectionToken?.Value));
        }

        return (
            state with
            {
                Pools = pools,
                Tickets = [.. state.Tickets, .. granted]
            },
            granted.ToArray());
    }

    private static bool CanGrant(
        GovernanceDocument state,
        IEnumerable<ResourcePoolRequirement> requirements)
    {
        foreach (var requirement in requirements)
        {
            var pool = FindPool(state, requirement.PoolName);
            if (pool is null)
            {
                return false;
            }

            var reserved = state.Tickets
                .Where(ticket => string.Equals(ticket.PoolName, requirement.PoolName, StringComparison.Ordinal))
                .Sum(ticket => ticket.Units);
            if (pool.CurrentCapacity - reserved < requirement.Count)
            {
                return false;
            }
        }

        return true;
    }

    private static bool Satisfies(
        IReadOnlyList<TicketDocument> held,
        IReadOnlyList<ResourcePoolRequirement> requirements) =>
        held.Count != 0 && requirements.All(requirement =>
            held.Where(ticket => string.Equals(
                    ticket.PoolName,
                    requirement.PoolName,
                    StringComparison.Ordinal))
                .Sum(ticket => ticket.Units) >= requirement.Count);

    private static TicketDocument[] TicketsFor(
        GovernanceDocument state,
        InstanceId instanceId,
        string holderKey) =>
        state.Tickets.Where(ticket => HolderMatches(ticket, instanceId, holderKey)).ToArray();

    private static WaiterDocument? FindWaiter(
        GovernanceDocument state,
        InstanceId instanceId,
        string holderKey) =>
        state.Waiters.SingleOrDefault(waiter => HolderMatches(waiter, instanceId, holderKey));

    private static bool HolderMatches(
        TicketDocument ticket,
        InstanceId instanceId,
        string holderKey) =>
        string.Equals(ticket.InstanceId, instanceId.ToString(), StringComparison.OrdinalIgnoreCase) &&
        string.Equals(ticket.HolderKey, holderKey, StringComparison.Ordinal);

    private static bool HolderMatches(
        WaiterDocument waiter,
        InstanceId instanceId,
        string holderKey) =>
        string.Equals(waiter.InstanceId, instanceId.ToString(), StringComparison.OrdinalIgnoreCase) &&
        string.Equals(waiter.HolderKey, holderKey, StringComparison.Ordinal);

    private static PoolDocument? FindPool(GovernanceDocument state, string name) =>
        state.Pools.SingleOrDefault(pool => string.Equals(pool.Name, name, StringComparison.Ordinal));

    private static T[] Replace<T>(T[] values, T current, T replacement)
        where T : notnull
    {
        var copy = values.ToArray();
        var index = Array.IndexOf(copy, current);
        if (index < 0)
        {
            throw new InvalidOperationException("A governance state member could not be replaced.");
        }

        copy[index] = replacement;
        return copy;
    }

    private static ResourcePoolTicket MaterializeTicket(TicketDocument ticket) =>
        new(
            Guid.ParseExact(ticket.TicketId, "N"),
            ticket.PoolName,
            ticket.Units,
            InstanceId.Parse(ticket.InstanceId),
            ticket.HolderKey,
            ticket.AcquiredAt,
            ticket.ExpiresAt)
        {
            FiberId = ticket.FiberOccurrence is null ? null : new FiberId(ticket.FiberOccurrence),
            ScopeId = ticket.ScopeOccurrence is null ? null : new ScopeId(ticket.ScopeOccurrence),
            ProviderGeneration = ticket.ProviderGeneration,
            ReviewDeadline = ticket.ReviewDeadline,
            ReviewMarked = ticket.ReviewMarked
        };

    private static ResourcePoolWaiter MaterializeWaiter(WaiterDocument waiter) =>
        new(
            Guid.ParseExact(waiter.WaiterId, "N"),
            InstanceId.Parse(waiter.InstanceId),
            waiter.HolderKey,
            waiter.Requirements
                .Select(requirement => new ResourcePoolRequirement(
                    requirement.PoolName,
                    requirement.Units))
                .ToArray(),
            waiter.RequestedAt,
            waiter.ExpiresAt)
        {
            FiberId = waiter.FiberOccurrence is null ? null : new FiberId(waiter.FiberOccurrence),
            ScopeId = waiter.ScopeOccurrence is null ? null : new ScopeId(waiter.ScopeOccurrence)
        };

    private static ResourcePoolReleaseEvidence MaterializeEvidence(
        ReleaseEvidenceDocument evidence) =>
        new(
            InstanceId.Parse(evidence.InstanceId),
            evidence.HolderKey,
            evidence.ProtectionToken is null
                ? null
                : LeaseProtectionToken.Parse(evidence.ProtectionToken),
            evidence.ReleasedAt,
            evidence.ConfirmationId is null
                ? null
                : StopConfirmationId.Create(evidence.ConfirmationId),
            evidence.ReleasedTickets.Select(MaterializeTicket).ToArray());

    private static ResourcePoolSnapshot ProviderSnapshot(
        GovernanceDocument state,
        PoolDocument pool)
    {
        var held = state.Tickets
            .Where(ticket => string.Equals(ticket.PoolName, pool.Name, StringComparison.Ordinal))
            .Select(MaterializeTicket)
            .ToArray();
        var waiters = state.Waiters
            .Where(waiter => waiter.Requirements.Any(requirement =>
                string.Equals(requirement.PoolName, pool.Name, StringComparison.Ordinal)))
            .Select(MaterializeWaiter)
            .ToArray();
        var byId = held.ToDictionary(ticket => ticket.TicketId.ToString("N"), StringComparer.Ordinal);
        return new ResourcePoolSnapshot(
            pool.Name,
            pool.CurrentCapacity,
            Math.Max(0, pool.CurrentCapacity - held.Sum(ticket => ticket.Count)),
            held,
            waiters)
        {
            ExpiredTickets = state.ExpiredTickets
                .Where(expired => byId.ContainsKey(expired.TicketId))
                .Select(expired => new ResourcePoolExpiredTicket(byId[expired.TicketId], expired.ExpiredAt))
                .ToArray()
        };
    }

    private static DurableResourcePoolSnapshot ManagementSnapshot(
        GovernanceDocument state,
        PoolDocument pool)
    {
        var tickets = state.Tickets
            .Where(ticket => string.Equals(ticket.PoolName, pool.Name, StringComparison.Ordinal))
            .ToArray();
        var reserved = tickets.Sum(ticket => ticket.Units);
        return new DurableResourcePoolSnapshot(
            ResourcePoolName.Create(pool.Name),
            pool.CurrentCapacity,
            reserved,
            Math.Max(0, reserved - pool.CurrentCapacity),
            state.Waiters.Count(waiter => waiter.Requirements.Any(requirement =>
                string.Equals(requirement.PoolName, pool.Name, StringComparison.Ordinal))),
            tickets.Where(ticket => ticket.ReviewMarked)
                .Select(ticket => ticket.ReviewDeadline)
                .Where(deadline => deadline is not null)
                .Min());
    }

    private static void ValidateAcquireRequest(ResourcePoolAcquireRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentException.ThrowIfNullOrWhiteSpace(request.HolderKey);
        ArgumentNullException.ThrowIfNull(request.HolderInstanceId);
        if (request.Requirements.Count == 0)
        {
            throw new ArgumentException("At least one resource requirement is required.", nameof(request));
        }

        foreach (var requirement in request.Requirements)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(requirement.PoolName);
            ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(requirement.Count, 0);
        }
    }

    private static DurableResourcePoolDefinition[] ValidateAndCopyOptions(
        DurableResourcePoolOptions candidate)
    {
        ArgumentNullException.ThrowIfNull(candidate);
        ArgumentNullException.ThrowIfNull(candidate.PartitionId);
        ArgumentNullException.ThrowIfNull(candidate.Pools);
        if (candidate.Pools.Any(pool => pool is null))
        {
            throw new ArgumentException("Durable resource pools cannot contain null.", nameof(candidate));
        }

        var copy = candidate.Pools
            .OrderBy(pool => pool.Name.Value, StringComparer.Ordinal)
            .ToArray();
        if (copy.Select(pool => pool.Name.Value).Distinct(StringComparer.Ordinal).Count() != copy.Length)
        {
            throw new ArgumentException("Durable resource-pool names must be unique.", nameof(candidate));
        }

        return copy;
    }

    private static InvalidOperationException SerializationFailure(string operation) =>
        new($"Durable resource governance {operation} could not serialize after repeated conflicts.");

    private static (
        GovernanceDocument State,
        TicketDocument[] Released,
        WaiterDocument[] Granted) ReleaseHolder(
        GovernanceDocument state,
        InstanceId holderInstanceId,
        string holderKey,
        DateTimeOffset releasedAt,
        string? confirmationId)
    {
        var released = TicketsFor(state, holderInstanceId, holderKey);
        var remainingWaiters = state.Waiters
            .Where(waiter => !HolderMatches(waiter, holderInstanceId, holderKey))
            .OrderBy(waiter => waiter.RequestedAt)
            .ThenBy(waiter => waiter.WaiterId, StringComparer.Ordinal)
            .ToList();
        var working = state with
        {
            Tickets = state.Tickets.Except(released).ToArray(),
            Waiters = remainingWaiters.ToArray()
        };
        if (released.Length != 0)
        {
            var token = released.Select(ticket => ticket.ProtectionToken)
                .Distinct(StringComparer.Ordinal)
                .SingleOrDefault();
            working = working with
            {
                ReleaseEvidence =
                [
                    .. working.ReleaseEvidence.Where(candidate =>
                        !string.Equals(
                            candidate.InstanceId,
                            holderInstanceId.ToString(),
                            StringComparison.OrdinalIgnoreCase) ||
                        !string.Equals(candidate.HolderKey, holderKey, StringComparison.Ordinal)),
                    new ReleaseEvidenceDocument(
                        holderInstanceId.ToString(),
                        holderKey,
                        token,
                        releasedAt,
                        confirmationId,
                        released)
                ]
            };
        }

        var granted = new List<WaiterDocument>();
        foreach (var waiter in remainingWaiters.ToArray())
        {
            var requirements = waiter.Requirements
                .Select(requirement => new ResourcePoolRequirement(requirement.PoolName, requirement.Units))
                .ToArray();
            if (!CanGrant(working, requirements))
            {
                break;
            }

            var waiterRequest = new ResourcePoolAcquireRequest(
                InstanceId.Parse(waiter.InstanceId),
                waiter.HolderKey,
                requirements,
                waiter.RequestedAt,
                waiter.ExpiresAt)
            {
                FiberId = waiter.FiberOccurrence is null ? null : new FiberId(waiter.FiberOccurrence),
                ScopeId = waiter.ScopeOccurrence is null ? null : new ScopeId(waiter.ScopeOccurrence),
                ProtectionToken = waiter.ProtectionToken is null
                    ? null
                    : LeaseProtectionToken.Parse(waiter.ProtectionToken)
            };
            var (grantedState, _) = Grant(working, waiterRequest, releasedAt);
            working = grantedState with
            {
                Waiters = grantedState.Waiters
                    .Where(candidate => !ReferenceEquals(candidate, waiter) &&
                        !string.Equals(candidate.WaiterId, waiter.WaiterId, StringComparison.Ordinal))
                    .ToArray()
            };
            granted.Add(waiter);
        }

        return (working, released, granted.ToArray());
    }

    private sealed record GovernanceEnvelope(
        string Kind,
        string Operation,
        GovernanceDocument State);

    private sealed record GovernanceDocument(
        PoolDocument[] Pools,
        TicketDocument[] Tickets,
        WaiterDocument[] Waiters,
        ExpiredTicketDocument[] ExpiredTickets,
        ResizeOperationDocument[] ResizeOperations,
        ReleaseEvidenceDocument[] ReleaseEvidence,
        ConfirmationBindingDocument[] ConfirmationBindings)
    {
        internal static GovernanceDocument Empty { get; } = new([], [], [], [], [], [], []);
    }

    private sealed record PoolDocument(
        string Name,
        int CreationCapacity,
        int CurrentCapacity,
        long? ReviewAfterTicks,
        long NextProviderGeneration);

    private sealed record TicketDocument(
        string TicketId,
        string PoolName,
        int Units,
        string InstanceId,
        string HolderKey,
        DateTimeOffset AcquiredAt,
        DateTimeOffset? ExpiresAt,
        string? FiberOccurrence,
        string? ScopeOccurrence,
        long ProviderGeneration,
        DateTimeOffset? ReviewDeadline,
        bool ReviewMarked,
        string? ProtectionToken);

    private sealed record WaiterDocument(
        string WaiterId,
        string InstanceId,
        string HolderKey,
        RequirementDocument[] Requirements,
        DateTimeOffset RequestedAt,
        DateTimeOffset? ExpiresAt,
        string? FiberOccurrence,
        string? ScopeOccurrence,
        string? ProtectionToken);

    private sealed record RequirementDocument(string PoolName, int Units);

    private sealed record ExpiredTicketDocument(string TicketId, DateTimeOffset ExpiredAt);

    private sealed record ResizeOperationDocument(
        string OperationId,
        string PoolName,
        int Capacity);

    private sealed record ReleaseEvidenceDocument(
        string InstanceId,
        string HolderKey,
        string? ProtectionToken,
        DateTimeOffset ReleasedAt,
        string? ConfirmationId,
        TicketDocument[] ReleasedTickets);

    private sealed record ConfirmationBindingDocument(
        string ConfirmationId,
        string ProtectionToken);
}
