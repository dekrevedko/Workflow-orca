using System.Security.Cryptography;
using System.Text.Json;
using OrcaCore.Abstractions.Durable;
using OrcaCore.Abstractions.Ids;
using OrcaCore.Engine.Durable.Execution;
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
    DurableResourcePoolOptions options,
    IWorkflowEventStore? workflows = null) :
    IResourcePoolStore, IScopedReservationCloser
{
    private const int MaxConflictRetries = 32;
    private const string StateCommitted = "state-committed";
    private static readonly JsonSerializerOptions RecoveryJson = new()
    {
        RespectRequiredConstructorParameters = true,
        RespectNullableAnnotations = true
    };

    private readonly DurableResourcePoolDefinition[] configuredPools =
        ValidateAndCopyOptions(options);
    private int initialized;

    internal async ValueTask InitializeAsync(CancellationToken cancellationToken = default)
    {
        for (var retry = 0; retry < MaxConflictRetries; retry++)
        {
            var (version, state) = await LoadStateAsync(cancellationToken).ConfigureAwait(false);
            if (version != 0)
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
            if (await TryAppendAsync(version, "pools-created", created, cancellationToken)
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
        var (_, state) = await LoadStateAsync(cancellationToken).ConfigureAwait(false);
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
        var (_, state) = await LoadStateAsync(cancellationToken).ConfigureAwait(false);
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
            var (version, state) = await LoadStateAsync(cancellationToken).ConfigureAwait(false);
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
            if (await TryAppendAsync(version, "pool-resized", updated, cancellationToken)
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
            var (version, state) = await LoadStateAsync(cancellationToken).ConfigureAwait(false);
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
            if (await TryAppendAsync(version, "pool-created", updated, cancellationToken)
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
        request = ValidateAndCopyAcquireRequest(request);
        await EnsureInitializedAsync(cancellationToken).ConfigureAwait(false);

        for (var retry = 0; retry < MaxConflictRetries; retry++)
        {
            var (version, state) = await LoadStateAsync(cancellationToken).ConfigureAwait(false);
            if (request.Requirements.Any(requirement => FindPool(state, requirement.PoolName) is null))
            {
                return new ResourcePoolAcquireResult(
                    ResourcePoolAcquireStatus.Rejected,
                    [],
                    null,
                    "One or more resource pools do not exist.");
            }

            if (request.ProtectionToken is { } token &&
                state.ReleaseEvidence.Any(evidence => evidence.ProtectionToken == token.Value))
                return new ResourcePoolAcquireResult(ResourcePoolAcquireStatus.Rejected, [], null, "Lease occurrence is closed.");

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

            var admissions = await ReadAdmissionsAsync([(request.HolderInstanceId, request.HolderKey, request.ProtectionToken?.Value)],
                cancellationToken).ConfigureAwait(false);
            if (request.ProtectionToken is { } requestedToken && !admissions.Allows(requestedToken.Value))
                return new ResourcePoolAcquireResult(ResourcePoolAcquireStatus.Rejected, [], null, "Lease occurrence is no longer pending.");

            if (state.Waiters.Length == 0 && CanGrant(state, request.Requirements))
            {
                var (grantedState, tickets) = Grant(state, request, request.RequestedAt);
                if (await TryAppendAsync(version, "reservation-granted", grantedState, cancellationToken, admissions.Versions)
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
            if (await TryAppendAsync(version, "reservation-queued", queuedState, cancellationToken, admissions.Versions)
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

    public Task<ResourcePoolReleaseResult> ReleaseAsync(ResourcePoolReleaseRequest request, CancellationToken cancellationToken) =>
        ReleaseAsync(request, null, cancellationToken);

    Task IScopedReservationCloser.CloseAsync(ResourcePoolReleaseRequest request, LeaseProtectionToken token,
        CancellationToken cancellationToken) => ReleaseAsync(request, token.Value, cancellationToken);

    private async Task<ResourcePoolReleaseResult> ReleaseAsync(
        ResourcePoolReleaseRequest request, string? closingToken, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentException.ThrowIfNullOrWhiteSpace(request.HolderKey);
        await EnsureInitializedAsync(cancellationToken).ConfigureAwait(false);

        for (var retry = 0; retry < MaxConflictRetries; retry++)
        {
            var (version, state) = await LoadStateAsync(cancellationToken).ConfigureAwait(false);
            ReleaseEvidenceDocument? prior = null;
            if (closingToken is not null)
            {
                prior = state.ReleaseEvidence.SingleOrDefault(evidence => evidence.ProtectionToken == closingToken);
                if (prior is not null)
                {
                    if (prior.InstanceId != request.HolderInstanceId.ToString() || prior.HolderKey != request.HolderKey)
                        throw new InvalidOperationException("Lease closure conflicts with retained occurrence identity.");
                    // Older writers could admit the same token again after release.
                    // A committed closure also drains any remaining reservation of that occurrence.
                    if (FindWaiter(state, request.HolderInstanceId, request.HolderKey) is null &&
                        TicketsFor(state, request.HolderInstanceId, request.HolderKey).Length == 0)
                        return new ResourcePoolReleaseResult([], []);
                }

                var waiter = FindWaiter(state, request.HolderInstanceId, request.HolderKey);
                if ((waiter is not null && waiter.ProtectionToken != closingToken) ||
                    TicketsFor(state, request.HolderInstanceId, request.HolderKey).Any(t => t.ProtectionToken != closingToken))
                    throw new InvalidOperationException("Lease closure conflicts with current reservation identity.");
            }

            var admissions = await ReadWaiterAdmissionsAsync(state, cancellationToken).ConfigureAwait(false);
            var (working, released, granted) = ReleaseHolder(
                state,
                request.HolderInstanceId,
                request.HolderKey,
                request.ReleasedAt,
                confirmationId: null, admissions, closingToken);

            // Keep an earlier confirmation and its exact receipt if repairing an old duplicate.
            if (prior is not null) working = working with { ReleaseEvidence = state.ReleaseEvidence };

            var changed = closingToken is not null || released.Length != 0 ||
                state.Waiters.Length != working.Waiters.Length ||
                granted.Length != 0;
            if (!changed)
            {
                return new ResourcePoolReleaseResult([], []);
            }

            if (await TryAppendAsync(version, "holder-released", working, cancellationToken, admissions.ForPromotions(granted))
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
        var (_, state) = await LoadStateAsync(cancellationToken).ConfigureAwait(false);
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
        var (_, state) = await LoadStateAsync(cancellationToken).ConfigureAwait(false);
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
            var (version, state) = await LoadStateAsync(cancellationToken).ConfigureAwait(false);
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

            var admissions = await ReadWaiterAdmissionsAsync(state, cancellationToken).ConfigureAwait(false);
            var (releasedState, released, granted) = ReleaseHolder(
                state,
                request.HolderInstanceId,
                request.HolderKey,
                request.ConfirmedAt,
                request.ConfirmationId.Value, admissions);
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
                        version,
                        "confirmed-holder-released",
                        releasedState,
                        cancellationToken, admissions.ForPromotions(granted))
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
            var (version, state) = await LoadStateAsync(cancellationToken).ConfigureAwait(false);
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
                    version,
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
        var (_, state) = await LoadStateAsync(cancellationToken).ConfigureAwait(false);
        var pool = FindPool(state, poolName);
        return pool is null
            ? Option<ResourcePoolSnapshot>.None
            : Option<ResourcePoolSnapshot>.Some(ProviderSnapshot(state, pool));
    }

    public async Task<IReadOnlyList<ResourcePoolSnapshot>> ListPoolsAsync(
        CancellationToken cancellationToken)
    {
        await EnsureInitializedAsync(cancellationToken).ConfigureAwait(false);
        var (_, state) = await LoadStateAsync(cancellationToken).ConfigureAwait(false);
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
            var (version, state) = await LoadStateAsync(cancellationToken).ConfigureAwait(false);
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
            if (await TryAppendAsync(version, "tickets-review-marked", updated, cancellationToken)
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

    private sealed record AdmissionRead(HashSet<string> Tokens, Dictionary<InstanceId, long> Versions, bool CheckOwner)
    {
        internal Dictionary<InstanceId, long> ForPromotions(WaiterDocument[] granted) =>
            granted.Where(w => w.ProtectionToken is not null).Select(w => InstanceId.Parse(w.InstanceId))
                .Distinct().Where(Versions.ContainsKey).ToDictionary(id => id, id => Versions[id]);

        internal bool Allows(string token) => !CheckOwner || Tokens.Contains(token);
    }

    private Task<AdmissionRead> ReadWaiterAdmissionsAsync(GovernanceDocument state, CancellationToken cancellationToken) =>
        ReadAdmissionsAsync(state.Waiters.Where(w => w.ProtectionToken is not null)
            .Select(w => (InstanceId.Parse(w.InstanceId), w.HolderKey, w.ProtectionToken)), cancellationToken);

    private async Task<AdmissionRead> ReadAdmissionsAsync(IEnumerable<(InstanceId Instance, string Holder, string? Token)> candidates,
        CancellationToken cancellationToken)
    {
        var result = new AdmissionRead([], [], workflows is not null);
        if (workflows is null) return result;
        foreach (var group in candidates.Where(c => c.Token is not null).GroupBy(c => c.Instance))
        {
            var current = await workflows.LoadCheckpointAsync(group.Key, cancellationToken).ConfigureAwait(false);
            if (!current.HasValue || current.Value.ContentType != DurableExecutionEnvelopeV2.ContentType ||
                (DurableScopedReservationRecovery.IsTerminal(current.Value) || current.Value.Status == WorkflowInstanceStatus.CancellationRequested)) continue;
            var envelope = DurableExecutionEnvelopeV2.Deserialize(current.Value.Payload);
            foreach (var candidate in group)
            {
                if (!envelope.OwnedObligations.Any(o => o.ProtectionToken == candidate.Token && o.HolderKey == candidate.Holder &&
                        o.Kind == DurableOwnedObligationKind.Resource && o.LeasePhase is ("Queued" or "PendingCommit") &&
                        envelope.Fibers.Any(f => f.FiberId == o.FiberId &&
                            f.Phase is not (DurableFiberPhase.Completed or DurableFiberPhase.Failed or DurableFiberPhase.Cancelled)))) continue;
                result.Tokens.Add(candidate.Token!);
                result.Versions[group.Key] = current.Value.StreamVersion.Value;
            }
        }
        return result;
    }

    private async ValueTask<bool> TryAppendAsync(
        long expectedVersion,
        string operation,
        GovernanceDocument state,
        CancellationToken cancellationToken,
        IReadOnlyDictionary<InstanceId, long>? checkpoints = null)
    {
        var record = CreateRecord(
            expectedVersion + 1,
            new GovernanceEnvelope(StateCommitted, operation, state));
        if (checkpoints is { Count: > 0 })
        {
            if (store is not ICheckpointGuardedResourceGovernanceStore guarded)
                throw new NotSupportedException("Scoped resource admission requires checkpoint-guarded governance writes.");
            return await guarded.AppendIfCurrentAsync(options.PartitionId, expectedVersion, [record], checkpoints,
                cancellationToken).ConfigureAwait(false) is ResourceGovernanceAppendResult.Committed;
        }
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

    private async ValueTask<(long Version, GovernanceDocument State)> LoadStateAsync(CancellationToken cancellationToken)
    {
        var head = await store.LoadLatestAsync(options.PartitionId, cancellationToken).ConfigureAwait(false);
        if (!options.PartitionId.Equals(head.PartitionId))
        {
            throw new InvalidOperationException($"Governance store returned partition '{head.PartitionId}' for '{options.PartitionId}'.");
        }

        if (head.Record is null) return (0, GovernanceDocument.Empty);

        GovernanceEnvelope envelope;
        try
        {
            envelope = JsonSerializer.Deserialize<GovernanceEnvelope>(head.Record.Payload.Span, RecoveryJson)
                ?? throw new JsonException("Empty governance envelope.");
        }
        catch (JsonException error)
        {
            throw new InvalidOperationException(
                $"Unreadable resource governance record for partition '{head.PartitionId}' at version {head.Version}.", error);
        }

        if (!string.Equals(envelope.Kind, StateCommitted, StringComparison.Ordinal))
            throw new InvalidOperationException($"Unknown durable resource-governance record '{envelope.Kind}'.");

        return (head.Version, ValidateDocument(envelope.State));
    }

    private static GovernanceDocument ValidateDocument(GovernanceDocument candidate)
    {
        ArgumentNullException.ThrowIfNull(candidate);
        if (candidate.Pools.Length == 0 ||
            candidate.Pools.Contains(null) || candidate.Tickets.Contains(null) || candidate.Waiters.Contains(null) ||
            candidate.ExpiredTickets.Contains(null) || candidate.ResizeOperations.Contains(null) ||
            candidate.ReleaseEvidence.Contains(null) || candidate.ConfirmationBindings.Contains(null))
        {
            throw new InvalidOperationException("A committed governance state contains no pools or has a null entry.");
        }

        var pools = new Dictionary<string, PoolDocument>(StringComparer.Ordinal);
        foreach (var pool in candidate.Pools)
        {
            if (string.IsNullOrWhiteSpace(pool.Name) || pool.CreationCapacity < 0 ||
                pool.CurrentCapacity < 1 || pool.NextProviderGeneration < 0 || !pools.TryAdd(pool.Name, pool))
            {
                throw new InvalidOperationException("A governance state contains an invalid or duplicate pool.");
            }
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

        static bool ValidGuid(string value, string format) =>
            Guid.TryParseExact(value, format, out var id) && id != Guid.Empty;

        bool InvalidTicket(TicketDocument ticket) =>
            !ValidGuid(ticket.TicketId, "N") || !ValidGuid(ticket.InstanceId, "D") ||
            string.IsNullOrWhiteSpace(ticket.HolderKey) || ticket.Units <= 0 || ticket.ProviderGeneration <= 0 ||
            !pools.TryGetValue(ticket.PoolName, out var pool) || ticket.ProviderGeneration > pool.NextProviderGeneration;

        if (candidate.Tickets.Any(InvalidTicket) ||
            candidate.Waiters.Any(waiter =>
                !ValidGuid(waiter.WaiterId, "N") ||
                !ValidGuid(waiter.InstanceId, "D") || string.IsNullOrWhiteSpace(waiter.HolderKey) ||
                waiter.Requirements.Length == 0 ||
                waiter.Requirements.Any(requirement =>
                    requirement is null || requirement.Units <= 0 ||
                    !pools.ContainsKey(requirement.PoolName)) ||
                (waiter.Requirements.Length > 1 && waiter.Requirements
                    .Select(requirement => requirement.PoolName)
                    .Distinct(StringComparer.Ordinal).Count() != waiter.Requirements.Length)) ||
            candidate.ExpiredTickets.Any(ticket => !ValidGuid(ticket.TicketId, "N")) ||
            candidate.ResizeOperations.Any(operation =>
                string.IsNullOrWhiteSpace(operation.OperationId) || operation.Capacity < 1 ||
                !pools.ContainsKey(operation.PoolName)) ||
            candidate.ReleaseEvidence.Any(evidence =>
                !ValidGuid(evidence.InstanceId, "D") ||
                string.IsNullOrWhiteSpace(evidence.HolderKey) ||
                (evidence.ProtectionToken is { } evidenceToken && !LeaseProtectionToken.TryParse(evidenceToken, out _)) ||
                (evidence.ReleasedTickets.Length == 0 &&
                 (evidence.ProtectionToken is null || evidence.ConfirmationId is not null)) ||
                evidence.ReleasedTickets.Any(ticket => ticket is null || InvalidTicket(ticket) ||
                    !string.Equals(ticket.InstanceId, evidence.InstanceId, StringComparison.OrdinalIgnoreCase) ||
                    !string.Equals(ticket.HolderKey, evidence.HolderKey, StringComparison.Ordinal) ||
                    !string.Equals(ticket.ProtectionToken, evidence.ProtectionToken, StringComparison.Ordinal))) ||
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

    private static ResourcePoolAcquireRequest ValidateAndCopyAcquireRequest(ResourcePoolAcquireRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentException.ThrowIfNullOrWhiteSpace(request.HolderKey);
        ArgumentNullException.ThrowIfNull(request.HolderInstanceId);
        ArgumentNullException.ThrowIfNull(request.Requirements);
        var requirements = request.Requirements.ToArray();
        if (requirements.Length == 0)
        {
            throw new ArgumentException("At least one resource requirement is required.", nameof(request));
        }

        var names = new HashSet<string>(StringComparer.Ordinal);
        foreach (var requirement in requirements)
        {
            ArgumentNullException.ThrowIfNull(requirement);
            ArgumentException.ThrowIfNullOrWhiteSpace(requirement.PoolName);
            ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(requirement.Count, 0);
            if (!names.Add(requirement.PoolName))
            {
                throw new ArgumentException($"Resource pool '{requirement.PoolName}' occurs more than once.", nameof(request));
            }
        }

        return request with { Requirements = requirements };
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
        string? confirmationId, AdmissionRead admissions, string? closingToken = null)
    {
        var released = TicketsFor(state, holderInstanceId, holderKey);
        // Waiters enter the array under the partition's append fence, in committed request order.
        var remainingWaiters = state.Waiters
            .Where(waiter => !HolderMatches(waiter, holderInstanceId, holderKey))
            .ToList();
        var working = state with
        {
            Tickets = state.Tickets.Except(released).ToArray(),
            Waiters = remainingWaiters.ToArray()
        };
        if (released.Length != 0 || closingToken is not null)
        {
            var token = closingToken ?? released.Select(ticket => ticket.ProtectionToken)
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
            if (waiter.ProtectionToken is { } token && !admissions.Allows(token)) continue;
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
