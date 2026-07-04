using OrcaCore.Abstractions.Ids;
using OrcaCore.Abstractions.Primitives;
using OrcaCore.Abstractions.Providers;

namespace OrcaCore.Providers.InMemory;

/// <summary>
/// Provides an in-memory durable resource-pool store.
/// </summary>
public sealed class InMemoryResourcePoolStore : IResourcePoolStore
{
    private readonly Lock gate = new();
    private readonly Dictionary<string, ResourcePoolDefinition> pools = new(StringComparer.Ordinal);
    private readonly List<ResourcePoolTicket> tickets = [];
    private readonly List<ResourcePoolWaiter> waiters = [];
    private readonly List<ResourcePoolExpiredTicket> expiredTickets = [];
    private readonly List<ResourcePoolAuditRecord> auditRecords = [];

    /// <inheritdoc />
    public Task UpsertPoolAsync(ResourcePoolDefinition definition, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(definition);
        ValidatePool(definition);
        cancellationToken.ThrowIfCancellationRequested();

        lock (gate)
        {
            pools[definition.Name] = definition;
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
            if (!AllPoolsExist(request.Requirements))
            {
                return Task.FromResult(new ResourcePoolAcquireResult(
                    ResourcePoolAcquireStatus.Rejected,
                    [],
                    null,
                    "One or more resource pools do not exist."));
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
            var released = tickets
                .Where(ticket => ticket.HolderInstanceId == request.HolderInstanceId &&
                    string.Equals(ticket.HolderKey, request.HolderKey, StringComparison.Ordinal))
                .ToArray();
            tickets.RemoveAll(ticket => released.Contains(ticket));

            var grantedWaiters = GrantQueuedWaiters(request.ReleasedAt);
            return Task.FromResult(new ResourcePoolReleaseResult(released, grantedWaiters));
        }
    }

    /// <inheritdoc />
    public Task<Option<ResourcePoolSnapshot>> GetPoolAsync(string poolName, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(poolName);
        cancellationToken.ThrowIfCancellationRequested();

        lock (gate)
        {
            if (!pools.TryGetValue(poolName, out var definition))
            {
                return Task.FromResult(Option<ResourcePoolSnapshot>.None);
            }

            return Task.FromResult(Option<ResourcePoolSnapshot>.Some(Snapshot(definition)));
        }
    }

    /// <inheritdoc />
    public Task ResizePoolAsync(string poolName, int capacity, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(poolName);
        ArgumentOutOfRangeException.ThrowIfNegative(capacity);
        cancellationToken.ThrowIfCancellationRequested();

        lock (gate)
        {
            if (pools.TryGetValue(poolName, out var definition))
            {
                pools[poolName] = definition with { Capacity = capacity };
            }
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
                .Where(ticket => ticket.ExpiresAt <= now &&
                    expiredTickets.All(expired => expired.Ticket.TicketId != ticket.TicketId))
                .Select(ticket => new ResourcePoolExpiredTicket(ticket, now))
                .ToArray();
            expiredTickets.AddRange(newlyExpired);
            return Task.FromResult(new ResourcePoolExpiryResult(newlyExpired));
        }
    }

    /// <inheritdoc />
    public Task<ResourcePoolForceReleaseResult> ForceReleaseTicketAsync(
        Guid ticketId,
        string reason,
        DateTimeOffset releasedAt,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(reason);
        cancellationToken.ThrowIfCancellationRequested();

        lock (gate)
        {
            var ticket = tickets.FirstOrDefault(candidate => candidate.TicketId == ticketId);
            if (ticket is null)
            {
                return Task.FromResult(new ResourcePoolForceReleaseResult(null, [], null));
            }

            tickets.Remove(ticket);
            expiredTickets.RemoveAll(expired => expired.Ticket.TicketId == ticketId);
            var audit = new ResourcePoolAuditRecord(
                Guid.CreateVersion7(),
                "ForceRelease",
                reason,
                releasedAt,
                ticket);
            auditRecords.Add(audit);
            var granted = GrantQueuedWaiters(releasedAt);
            return Task.FromResult(new ResourcePoolForceReleaseResult(ticket, granted, audit));
        }
    }

    private ResourcePoolSnapshot Snapshot(ResourcePoolDefinition definition)
    {
        var held = tickets
            .Where(ticket => string.Equals(ticket.PoolName, definition.Name, StringComparison.Ordinal))
            .ToArray();
        var queued = waiters
            .Where(waiter => waiter.Requirements.Any(requirement =>
                string.Equals(requirement.PoolName, definition.Name, StringComparison.Ordinal)))
            .ToArray();
        return new ResourcePoolSnapshot(
            definition.Name,
            definition.Capacity,
            Math.Max(0, definition.Capacity - held.Sum(ticket => ticket.Count)),
            held,
            queued)
        {
            ExpiredTickets = expiredTickets
                .Where(expired => string.Equals(expired.Ticket.PoolName, definition.Name, StringComparison.Ordinal))
                .ToArray(),
            AuditRecords = auditRecords
                .Where(audit => string.Equals(audit.Ticket?.PoolName, definition.Name, StringComparison.Ordinal))
                .ToArray()
        };
    }

    private bool AllPoolsExist(IEnumerable<ResourcePoolRequirement> requirements)
    {
        return requirements.All(requirement => pools.ContainsKey(requirement.PoolName));
    }

    private bool CanGrant(IEnumerable<ResourcePoolRequirement> requirements)
    {
        foreach (var requirement in requirements)
        {
            var held = tickets
                .Where(ticket => string.Equals(ticket.PoolName, requirement.PoolName, StringComparison.Ordinal))
                .Sum(ticket => ticket.Count);
            if (pools[requirement.PoolName].Capacity - held < requirement.Count)
            {
                return false;
            }
        }

        return true;
    }

    private IReadOnlyList<ResourcePoolTicket> Grant(ResourcePoolAcquireRequest request, DateTimeOffset acquiredAt)
    {
        var granted = request.Requirements
            .Select(requirement => new ResourcePoolTicket(
                Guid.CreateVersion7(),
                requirement.PoolName,
                requirement.Count,
                request.HolderInstanceId,
                request.HolderKey,
                acquiredAt,
                request.ExpiresAt))
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
                    waiter.ExpiresAt),
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
            request.ExpiresAt);
        waiters.Add(waiter);
        return waiter;
    }

    private ResourcePoolWaiter? FindWaiter(InstanceId holderInstanceId, string holderKey)
    {
        return waiters.FirstOrDefault(waiter =>
            waiter.HolderInstanceId == holderInstanceId &&
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
}
