using OrcaCore.Abstractions.Ids;
using OrcaCore.Abstractions.Primitives;
using OrcaCore.Abstractions.Providers;
using OrcaCore.Provider.Abstractions.ResourceGovernance;

namespace OrcaCore.Providers.InMemory;

/// <summary>
/// Provides an in-memory durable resource-pool store.
/// </summary>
public sealed class InMemoryResourcePoolStore :
    IResourcePoolStore,
    IResourceLeaseGovernanceStore
{
    private readonly Lock gate = new();
    private readonly Dictionary<string, PoolState> pools = new(StringComparer.Ordinal);
    private readonly List<ResourcePoolTicket> tickets = [];
    private readonly List<ResourcePoolWaiter> waiters = [];
    private readonly List<ResourcePoolExpiredTicket> expiredTickets = [];
    private readonly List<ResourcePoolAuditRecord> auditRecords = [];
    private readonly Dictionary<string, LeaseProtectionToken> protectionTokens =
        new(StringComparer.Ordinal);
    private readonly Dictionary<string, string> confirmationBindings =
        new(StringComparer.Ordinal);
    private readonly List<ResourcePoolReleaseEvidence> releaseEvidence = [];

    /// <inheritdoc />
    public Task UpsertPoolAsync(ResourcePoolDefinition definition, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(definition);
        ValidatePool(definition);
        cancellationToken.ThrowIfCancellationRequested();

        lock (gate)
        {
            if (pools.TryGetValue(definition.Name, out var existing))
            {
                if (existing.CreationDefinition != definition)
                {
                    throw new InvalidOperationException(
                        $"Resource pool '{definition.Name}' creation definition does not match persisted governance state.");
                }

                return Task.CompletedTask;
            }

            pools.Add(definition.Name, new PoolState(definition, definition.Capacity, 0));
        }

        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task<ResourcePoolAcquireResult> AcquireAsync(
        ResourcePoolAcquireRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        ValidateAcquireRequest(request);
        cancellationToken.ThrowIfCancellationRequested();

        lock (gate)
        {
            RememberProtectionToken(request);
            if (!AllPoolsExist(request.Requirements))
            {
                return Task.FromResult(new ResourcePoolAcquireResult(
                    ResourcePoolAcquireStatus.Rejected,
                    [],
                    null,
                    "One or more resource pools do not exist."));
            }

            // Idempotent re-acquire: a holder whose queued waiter was already granted on a
            // release re-attempts the acquisition (at-least-once, DR-014) and must observe
            // Granted with its existing tickets, never queue behind its own allocation.
            var heldByHolder = tickets
                .Where(ticket => ticket.HolderInstanceId.Equals(request.HolderInstanceId) &&
                    string.Equals(ticket.HolderKey, request.HolderKey, StringComparison.Ordinal))
                .ToArray();
            if (Satisfies(heldByHolder, request.Requirements))
            {
                return Task.FromResult(new ResourcePoolAcquireResult(
                    ResourcePoolAcquireStatus.Granted,
                    heldByHolder,
                    null,
                    null));
            }

            if (CanGrant(request.Requirements))
            {
                var granted = Grant(request, request.RequestedAt);
                return Task.FromResult(new ResourcePoolAcquireResult(
                    ResourcePoolAcquireStatus.Granted,
                    granted,
                    null,
                    null));
            }

            var waiter = FindWaiter(request.HolderInstanceId, request.HolderKey)
                ?? Enqueue(request);
            return Task.FromResult(new ResourcePoolAcquireResult(
                ResourcePoolAcquireStatus.Queued,
                [],
                waiter,
                null));
        }
    }

    /// <inheritdoc />
    public Task<ResourcePoolReleaseResult> ReleaseAsync(
        ResourcePoolReleaseRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentException.ThrowIfNullOrWhiteSpace(request.HolderKey);
        cancellationToken.ThrowIfCancellationRequested();

        lock (gate)
        {
            return Task.FromResult(ReleaseCore(request, confirmationId: null));
        }
    }

    /// <inheritdoc />
    public Task<Option<ResourcePoolReleaseEvidence>> GetReleaseEvidenceAsync(
        LeaseProtectionToken protectionToken,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(protectionToken);
        cancellationToken.ThrowIfCancellationRequested();
        lock (gate)
        {
            var found = releaseEvidence.LastOrDefault(candidate =>
                candidate.ProtectionToken?.Equals(protectionToken) == true);
            return Task.FromResult(found is null
                ? Option<ResourcePoolReleaseEvidence>.None
                : Option<ResourcePoolReleaseEvidence>.Some(CopyEvidence(found)));
        }
    }

    /// <inheritdoc />
    public Task<Option<LeaseProtectionToken>> GetConfirmationBindingAsync(
        StopConfirmationId confirmationId,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(confirmationId);
        cancellationToken.ThrowIfCancellationRequested();
        lock (gate)
        {
            return Task.FromResult(confirmationBindings.TryGetValue(
                confirmationId.Value,
                out var protectionToken)
                ? Option<LeaseProtectionToken>.Some(LeaseProtectionToken.Parse(protectionToken))
                : Option<LeaseProtectionToken>.None);
        }
    }

    /// <inheritdoc />
    public Task<ResourcePoolStopConfirmationStatus> ConfirmAndReleaseAsync(
        ResourcePoolStopConfirmationRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(request.HolderInstanceId);
        ArgumentException.ThrowIfNullOrWhiteSpace(request.HolderKey);
        ArgumentNullException.ThrowIfNull(request.ProtectionToken);
        ArgumentNullException.ThrowIfNull(request.ConfirmationId);
        cancellationToken.ThrowIfCancellationRequested();

        lock (gate)
        {
            if (confirmationBindings.TryGetValue(
                    request.ConfirmationId.Value,
                    out var boundToken))
            {
                return Task.FromResult(string.Equals(
                    boundToken,
                    request.ProtectionToken.Value,
                    StringComparison.Ordinal)
                    ? ResourcePoolStopConfirmationStatus.AlreadyConfirmed
                    : ResourcePoolStopConfirmationStatus.ConfirmationConflict);
            }

            var prior = releaseEvidence.LastOrDefault(candidate =>
                candidate.ProtectionToken?.Equals(request.ProtectionToken) == true);
            if (prior?.ConfirmationId is not null)
            {
                return Task.FromResult(ResourcePoolStopConfirmationStatus.AlreadyConfirmed);
            }

            if (prior is not null)
            {
                return Task.FromResult(ResourcePoolStopConfirmationStatus.TokenNotFound);
            }

            var held = tickets.Any(ticket =>
                ticket.HolderInstanceId.Equals(request.HolderInstanceId) &&
                string.Equals(ticket.HolderKey, request.HolderKey, StringComparison.Ordinal));
            if (!held ||
                !protectionTokens.TryGetValue(
                    HolderIdentity(request.HolderInstanceId, request.HolderKey),
                    out var expectedToken) ||
                !expectedToken.Equals(request.ProtectionToken))
            {
                return Task.FromResult(ResourcePoolStopConfirmationStatus.TokenNotFound);
            }

            confirmationBindings.Add(
                request.ConfirmationId.Value,
                request.ProtectionToken.Value);
            _ = ReleaseCore(
                new ResourcePoolReleaseRequest(
                    request.HolderInstanceId,
                    request.HolderKey,
                    request.ConfirmedAt),
                request.ConfirmationId);
            return Task.FromResult(ResourcePoolStopConfirmationStatus.Released);
        }
    }

    /// <inheritdoc />
    public Task PurgeReleaseEvidenceAsync(
        InstanceId instanceId,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(instanceId);
        cancellationToken.ThrowIfCancellationRequested();
        lock (gate)
        {
            if (tickets.Any(ticket => ticket.HolderInstanceId.Equals(instanceId)))
            {
                throw new InvalidOperationException(
                    "Active resource ownership cannot be purged.");
            }

            var removedTokens = releaseEvidence
                .Where(candidate => candidate.HolderInstanceId.Equals(instanceId))
                .Select(candidate => candidate.ProtectionToken?.Value)
                .Where(value => value is not null)
                .ToHashSet(StringComparer.Ordinal);
            releaseEvidence.RemoveAll(candidate =>
                candidate.HolderInstanceId.Equals(instanceId));
            foreach (var confirmationId in confirmationBindings
                         .Where(binding => removedTokens.Contains(binding.Value))
                         .Select(binding => binding.Key)
                         .ToArray())
            {
                confirmationBindings.Remove(confirmationId);
            }

            foreach (var identity in protectionTokens.Keys
                         .Where(identity => identity.StartsWith(
                             $"{instanceId}:",
                             StringComparison.Ordinal))
                         .ToArray())
            {
                protectionTokens.Remove(identity);
            }
        }

        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task<Option<ResourcePoolSnapshot>> GetPoolAsync(string poolName, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(poolName);
        cancellationToken.ThrowIfCancellationRequested();

        lock (gate)
        {
            if (!pools.TryGetValue(poolName, out var state))
            {
                return Task.FromResult(Option<ResourcePoolSnapshot>.None);
            }

            return Task.FromResult(Option<ResourcePoolSnapshot>.Some(Snapshot(poolName, state)));
        }
    }

    /// <inheritdoc />
    public Task<IReadOnlyList<ResourcePoolSnapshot>> ListPoolsAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        lock (gate)
        {
            return Task.FromResult<IReadOnlyList<ResourcePoolSnapshot>>(
                pools
                    .OrderBy(pool => pool.Key, StringComparer.Ordinal)
                    .Select(pool => Snapshot(pool.Key, pool.Value))
                    .ToArray());
        }
    }

    /// <inheritdoc />
    public Task ResizePoolAsync(string poolName, int capacity, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(poolName);
        ArgumentOutOfRangeException.ThrowIfLessThan(capacity, 1);
        cancellationToken.ThrowIfCancellationRequested();

        lock (gate)
        {
            if (!pools.TryGetValue(poolName, out var state))
            {
                throw ResourcePoolNotConfiguredException.For([ResourcePoolName.Create(poolName)]);
            }

            pools[poolName] = state with { CurrentCapacity = capacity };
        }

        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task<ResourcePoolExpiryResult> ExpireTicketsAsync(
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        lock (gate)
        {
            var newlyExpired = tickets
                .Where(ticket => ticket.ReviewDeadline <= now &&
                    expiredTickets.All(expired => expired.Ticket.TicketId != ticket.TicketId))
                .Select(ticket => new ResourcePoolExpiredTicket(ticket, now))
                .ToArray();
            foreach (var expired in newlyExpired)
            {
                var index = tickets.FindIndex(ticket => ticket.TicketId == expired.Ticket.TicketId);
                tickets[index] = tickets[index] with { ReviewMarked = true };
            }

            expiredTickets.AddRange(newlyExpired);
            return Task.FromResult(new ResourcePoolExpiryResult(newlyExpired));
        }
    }

    private ResourcePoolSnapshot Snapshot(string poolName, PoolState state)
    {
        var held = tickets
            .Where(ticket => string.Equals(ticket.PoolName, poolName, StringComparison.Ordinal))
            .ToArray();
        var queued = waiters
            .Where(waiter => waiter.Requirements.Any(requirement =>
                string.Equals(requirement.PoolName, poolName, StringComparison.Ordinal)))
            .ToArray();
        return new ResourcePoolSnapshot(
            poolName,
            state.CurrentCapacity,
            Math.Max(0, state.CurrentCapacity - held.Sum(ticket => ticket.Count)),
            held,
            queued)
        {
            ExpiredTickets = expiredTickets
                .Where(expired => string.Equals(expired.Ticket.PoolName, poolName, StringComparison.Ordinal))
                .ToArray(),
            AuditRecords = auditRecords
                .Where(audit => string.Equals(audit.Ticket?.PoolName, poolName, StringComparison.Ordinal))
                .ToArray()
        };
    }

    private ResourcePoolReleaseResult ReleaseCore(
        ResourcePoolReleaseRequest request,
        StopConfirmationId? confirmationId)
    {
        waiters.RemoveAll(waiter =>
            waiter.HolderInstanceId.Equals(request.HolderInstanceId) &&
            string.Equals(waiter.HolderKey, request.HolderKey, StringComparison.Ordinal));
        var released = tickets
            .Where(ticket => ticket.HolderInstanceId.Equals(request.HolderInstanceId) &&
                string.Equals(ticket.HolderKey, request.HolderKey, StringComparison.Ordinal))
            .ToArray();
        tickets.RemoveAll(ticket => released.Contains(ticket));
        if (released.Length != 0)
        {
            protectionTokens.TryGetValue(
                HolderIdentity(request.HolderInstanceId, request.HolderKey),
                out var protectionToken);
            releaseEvidence.RemoveAll(candidate =>
                candidate.HolderInstanceId.Equals(request.HolderInstanceId) &&
                string.Equals(candidate.HolderKey, request.HolderKey, StringComparison.Ordinal));
            releaseEvidence.Add(new ResourcePoolReleaseEvidence(
                request.HolderInstanceId,
                request.HolderKey,
                protectionToken,
                request.ReleasedAt,
                confirmationId,
                released.ToArray()));
        }

        var grantedWaiters = GrantQueuedWaiters(request.ReleasedAt);
        return new ResourcePoolReleaseResult(released, grantedWaiters);
    }

    private void RememberProtectionToken(ResourcePoolAcquireRequest request)
    {
        if (request.ProtectionToken is null)
        {
            return;
        }

        var identity = HolderIdentity(request.HolderInstanceId, request.HolderKey);
        if (protectionTokens.TryGetValue(identity, out var existing) &&
            !existing.Equals(request.ProtectionToken))
        {
            throw new InvalidOperationException(
                "One resource holder cannot be rebound to a different protection token.");
        }

        protectionTokens[identity] = request.ProtectionToken;
    }

    private static string HolderIdentity(InstanceId instanceId, string holderKey) =>
        $"{instanceId}:{holderKey}";

    private static ResourcePoolReleaseEvidence CopyEvidence(ResourcePoolReleaseEvidence evidence) =>
        evidence with { ReleasedTickets = evidence.ReleasedTickets.ToArray() };

    private bool AllPoolsExist(IEnumerable<ResourcePoolRequirement> requirements)
    {
        return requirements.All(requirement => pools.ContainsKey(requirement.PoolName));
    }

    private static bool Satisfies(
        IReadOnlyList<ResourcePoolTicket> heldByHolder,
        IReadOnlyList<ResourcePoolRequirement> requirements)
    {
        return heldByHolder.Count > 0 && requirements.All(requirement =>
            heldByHolder
                .Where(ticket => string.Equals(ticket.PoolName, requirement.PoolName, StringComparison.Ordinal))
                .Sum(ticket => ticket.Count) >= requirement.Count);
    }

    private bool CanGrant(IEnumerable<ResourcePoolRequirement> requirements)
    {
        foreach (var requirement in requirements)
        {
            var held = tickets
                .Where(ticket => string.Equals(ticket.PoolName, requirement.PoolName, StringComparison.Ordinal))
                .Sum(ticket => ticket.Count);
            if (pools[requirement.PoolName].CurrentCapacity - held < requirement.Count)
            {
                return false;
            }
        }

        return true;
    }

    private IReadOnlyList<ResourcePoolTicket> Grant(ResourcePoolAcquireRequest request, DateTimeOffset acquiredAt)
    {
        var granted = request.Requirements
            .Select(requirement =>
            {
                var pool = pools[requirement.PoolName];
                var generation = pool.NextProviderGeneration + 1;
                pools[requirement.PoolName] = pool with { NextProviderGeneration = generation };
                return new ResourcePoolTicket(
                Guid.CreateVersion7(),
                requirement.PoolName,
                requirement.Count,
                request.HolderInstanceId,
                request.HolderKey,
                acquiredAt,
                request.ExpiresAt)
                {
                    FiberId = request.FiberId,
                    ScopeId = request.ScopeId,
                    ProviderGeneration = generation,
                    ReviewDeadline = pool.CreationDefinition.LeaseDuration is { } reviewAfter
                        ? acquiredAt + reviewAfter
                        : null
                };
            })
            .ToArray();
        tickets.AddRange(granted);
        return granted;
    }

    private IReadOnlyList<ResourcePoolWaiter> GrantQueuedWaiters(DateTimeOffset grantedAt)
    {
        var granted = new List<ResourcePoolWaiter>();
        foreach (var waiter in waiters.OrderBy(waiter => waiter.RequestedAt).ThenBy(waiter => waiter.WaiterId).ToArray())
        {
            if (!CanGrant(waiter.Requirements))
            {
                continue;
            }

            Grant(
                new ResourcePoolAcquireRequest(
                    waiter.HolderInstanceId,
                    waiter.HolderKey,
                    waiter.Requirements,
                    waiter.RequestedAt,
                    waiter.ExpiresAt)
                {
                    FiberId = waiter.FiberId,
                    ScopeId = waiter.ScopeId
                },
                grantedAt);
            waiters.Remove(waiter);
            granted.Add(waiter);
        }

        return granted;
    }

    private ResourcePoolWaiter Enqueue(ResourcePoolAcquireRequest request)
    {
        var waiter = new ResourcePoolWaiter(
            Guid.CreateVersion7(),
            request.HolderInstanceId,
            request.HolderKey,
            request.Requirements.ToArray(),
            request.RequestedAt,
            request.ExpiresAt)
        {
            FiberId = request.FiberId,
            ScopeId = request.ScopeId
        };
        waiters.Add(waiter);
        return waiter;
    }

    private ResourcePoolWaiter? FindWaiter(InstanceId holderInstanceId, string holderKey)
    {
        return waiters.FirstOrDefault(waiter =>
            waiter.HolderInstanceId.Equals(holderInstanceId) &&
            string.Equals(waiter.HolderKey, holderKey, StringComparison.Ordinal));
    }

    private static void ValidatePool(ResourcePoolDefinition definition)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(definition.Name);
        ArgumentOutOfRangeException.ThrowIfNegative(definition.Capacity);
    }

    private static void ValidateAcquireRequest(ResourcePoolAcquireRequest request)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(request.HolderKey);
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

    private sealed record PoolState(
        ResourcePoolDefinition CreationDefinition,
        int CurrentCapacity,
        long NextProviderGeneration);
}
