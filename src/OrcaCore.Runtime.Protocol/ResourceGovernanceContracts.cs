namespace OrcaCore.Runtime.Protocol.ResourceGovernance;

/// <summary>Reports current durable resource-pool accounting.</summary>
public sealed record DurableResourcePoolSnapshot(
    ResourcePoolName Name,
    int ConfiguredCapacity,
    int ReservedUnits,
    int ResizeDebt,
    int QueuedRequestCount,
    DateTimeOffset? OldestReviewDeadline);

/// <summary>Closed result of an idempotent durable pool resize.</summary>
public abstract record DurableResourcePoolResizeResult
{
    private protected DurableResourcePoolResizeResult()
    {
    }

    public sealed record Applied(
        ResourcePoolOperationId OperationId,
        ResourcePoolName Pool,
        int Capacity,
        DurableResourcePoolSnapshot Snapshot)
        : DurableResourcePoolResizeResult;

    public sealed record Conflict(
        ResourcePoolOperationId OperationId,
        ResourcePoolName RecordedPool,
        int RecordedCapacity,
        ResourcePoolName AttemptedPool,
        int AttemptedCapacity)
        : DurableResourcePoolResizeResult;
}

/// <summary>Closed result of a trusted protected-work stop confirmation.</summary>
public enum ProtectedWorkStopConfirmationStatus
{
    Released,
    AlreadyConfirmed,
    NotConfirmable,
    TokenNotFound,
    ConfirmationConflict
}

/// <summary>Persistent lifecycle visible through advanced lease diagnostics.</summary>
public enum DurableResourceLeaseObligationStatus
{
    Queued,
    PendingCommit,
    Held,
    ReviewMarked,
    AmbiguousHeld,
    Quarantined,
    Released,
    CancelledBeforeGrant,
    LeaseLost
}

/// <summary>Detached diagnostics for one exact provider ticket.</summary>
public sealed record DurableResourceLeaseTicketSnapshot(
    string TicketId,
    ResourcePoolName Pool,
    int Units,
    long ProviderGeneration,
    DateTimeOffset ReviewDeadline,
    bool ReviewMarked);

/// <summary>Detached diagnostics for one exact durable lease obligation.</summary>
public sealed class DurableResourceLeaseObligationSnapshot
{
    public DurableResourceLeaseObligationSnapshot(
        string obligationId,
        InstanceId instanceId,
        DefinitionId definitionId,
        DefinitionVersion definitionVersion,
        int generation,
        string fiberOccurrence,
        string scopeOccurrence,
        AuthoredLocation authoredLocation,
        LeaseProtectionToken protectionToken,
        DurableResourceLeaseObligationStatus status,
        IReadOnlyList<DurableResourceLeaseTicketSnapshot> tickets,
        DateTimeOffset? quarantinedAt,
        StopConfirmationId? acceptedConfirmationId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(obligationId);
        ArgumentNullException.ThrowIfNull(instanceId);
        ArgumentNullException.ThrowIfNull(definitionId);
        ArgumentNullException.ThrowIfNull(definitionVersion);
        ArgumentOutOfRangeException.ThrowIfNegative(generation);
        ArgumentException.ThrowIfNullOrWhiteSpace(fiberOccurrence);
        ArgumentException.ThrowIfNullOrWhiteSpace(scopeOccurrence);
        ArgumentNullException.ThrowIfNull(authoredLocation);
        ArgumentNullException.ThrowIfNull(protectionToken);
        ArgumentNullException.ThrowIfNull(tickets);
        if (tickets.Any(ticket => ticket is null))
        {
            throw new ArgumentException(
                "Lease diagnostic tickets cannot contain null.",
                nameof(tickets));
        }

        ObligationId = obligationId;
        InstanceId = instanceId;
        DefinitionId = definitionId;
        DefinitionVersion = definitionVersion;
        Generation = generation;
        FiberOccurrence = fiberOccurrence;
        ScopeOccurrence = scopeOccurrence;
        AuthoredLocation = authoredLocation;
        ProtectionToken = protectionToken;
        Status = status;
        Tickets = Array.AsReadOnly(tickets.ToArray());
        QuarantinedAt = quarantinedAt;
        AcceptedConfirmationId = acceptedConfirmationId;
    }

    public string ObligationId { get; }
    public InstanceId InstanceId { get; }
    public DefinitionId DefinitionId { get; }
    public DefinitionVersion DefinitionVersion { get; }
    public int Generation { get; }
    public string FiberOccurrence { get; }
    public string ScopeOccurrence { get; }
    public AuthoredLocation AuthoredLocation { get; }
    public LeaseProtectionToken ProtectionToken { get; }
    public DurableResourceLeaseObligationStatus Status { get; }
    public IReadOnlyList<DurableResourceLeaseTicketSnapshot> Tickets { get; }
    public DateTimeOffset? QuarantinedAt { get; }
    public StopConfirmationId? AcceptedConfirmationId { get; }
}
