using OrcaCore.Abstractions.Ids;

namespace OrcaCore.Abstractions.Providers;

/// <summary>
/// Defines a durable resource pool.
/// </summary>
public sealed record ResourcePoolDefinition(
    string Name,
    int Capacity,
    TimeSpan? LeaseDuration);

/// <summary>
/// Describes one pool requirement in a multi-pool acquisition request.
/// </summary>
public sealed record ResourcePoolRequirement(string PoolName, int Count);

/// <summary>
/// Describes one atomic durable-pool acquisition request.
/// </summary>
public sealed record ResourcePoolAcquireRequest(
    InstanceId HolderInstanceId,
    string HolderKey,
    IReadOnlyList<ResourcePoolRequirement> Requirements,
    DateTimeOffset RequestedAt,
    DateTimeOffset? ExpiresAt);

/// <summary>
/// Describes a release of all tickets held by one guarded holder.
/// </summary>
public sealed record ResourcePoolReleaseRequest(
    InstanceId HolderInstanceId,
    string HolderKey,
    DateTimeOffset ReleasedAt);

/// <summary>
/// Describes a durable resource-pool ticket.
/// </summary>
public sealed record ResourcePoolTicket(
    Guid TicketId,
    string PoolName,
    int Count,
    InstanceId HolderInstanceId,
    string HolderKey,
    DateTimeOffset AcquiredAt,
    DateTimeOffset? ExpiresAt);

/// <summary>
/// Describes a queued acquisition waiter.
/// </summary>
public sealed record ResourcePoolWaiter(
    Guid WaiterId,
    InstanceId HolderInstanceId,
    string HolderKey,
    IReadOnlyList<ResourcePoolRequirement> Requirements,
    DateTimeOffset RequestedAt,
    DateTimeOffset? ExpiresAt);

/// <summary>
/// Describes the outcome category for a pool acquisition.
/// </summary>
public enum ResourcePoolAcquireStatus
{
    /// <summary>
    /// All requested tickets were granted.
    /// </summary>
    Granted,

    /// <summary>
    /// The request was queued without holding any tickets.
    /// </summary>
    Queued,

    /// <summary>
    /// One or more referenced pools do not exist.
    /// </summary>
    Rejected
}

/// <summary>
/// Describes the result of an acquisition attempt.
/// </summary>
public sealed record ResourcePoolAcquireResult(
    ResourcePoolAcquireStatus Status,
    IReadOnlyList<ResourcePoolTicket> Tickets,
    ResourcePoolWaiter? QueuedWaiter,
    string? Error);

/// <summary>
/// Describes the result of a release and any grants made to queued waiters.
/// </summary>
public sealed record ResourcePoolReleaseResult(
    IReadOnlyList<ResourcePoolTicket> ReleasedTickets,
    IReadOnlyList<ResourcePoolWaiter> GrantedWaiters);

/// <summary>
/// Describes an audibly expired ticket.
/// </summary>
public sealed record ResourcePoolExpiredTicket(
    ResourcePoolTicket Ticket,
    DateTimeOffset ExpiredAt);

/// <summary>
/// Describes an audited operator pool action.
/// </summary>
public sealed record ResourcePoolAuditRecord(
    Guid AuditId,
    string Action,
    string Reason,
    DateTimeOffset OccurredAt,
    ResourcePoolTicket? Ticket);

/// <summary>
/// Describes the result of an expiry scan.
/// </summary>
public sealed record ResourcePoolExpiryResult(
    IReadOnlyList<ResourcePoolExpiredTicket> ExpiredTickets);

/// <summary>
/// Describes the result of an operator force-release.
/// </summary>
public sealed record ResourcePoolForceReleaseResult(
    ResourcePoolTicket? ReleasedTicket,
    IReadOnlyList<ResourcePoolWaiter> GrantedWaiters,
    ResourcePoolAuditRecord? AuditRecord);

/// <summary>
/// Describes current pool capacity, held tickets, and queued waiters.
/// </summary>
public sealed record ResourcePoolSnapshot(
    string Name,
    int Capacity,
    int AvailableCapacity,
    IReadOnlyList<ResourcePoolTicket> HeldTickets,
    IReadOnlyList<ResourcePoolWaiter> QueuedWaiters)
{
    /// <summary>
    /// Gets audibly expired tickets for this pool.
    /// </summary>
    public IReadOnlyList<ResourcePoolExpiredTicket> ExpiredTickets { get; init; } = [];

    /// <summary>
    /// Gets audited operator actions for this pool.
    /// </summary>
    public IReadOnlyList<ResourcePoolAuditRecord> AuditRecords { get; init; } = [];
}
