using OrcaCore.Abstractions.Ids;

namespace OrcaCore.Abstractions.Providers;

/// <summary>
/// Provider-owned summary used for atomic projection writes and provider certification.
/// It is not an application workflow snapshot.
/// </summary>
public sealed record WorkflowProjectionSnapshot
{
    public required InstanceId InstanceId { get; init; }

    public InstanceId? ParentInstanceId { get; init; }

    public InstanceId? RootInstanceId { get; init; }

    public required DefinitionId DefinitionId { get; init; }

    public required DefinitionVersion DefinitionVersion { get; init; }

    public required global::OrcaCore.WorkflowInstanceStatus Status { get; init; }

    public long? StreamVersion { get; init; }

    public required DateTimeOffset CreatedAt { get; init; }

    public required DateTimeOffset UpdatedAt { get; init; }

    public DateTimeOffset? CurrentStatusEnteredAt { get; init; }

    public DateTimeOffset? LastActiveAt { get; init; }

    public bool IsStuck { get; init; }

    public bool HasStuckStep { get; init; }

    public string? StuckStepPath { get; init; }

    public DateTimeOffset? StuckDetectedAt { get; init; }

    public string? ErrorSummary { get; init; }

    public string? EndOutcomeName { get; init; }

    public int ContinueAsNewGeneration { get; init; }

    public IReadOnlyList<WorkflowProjectionActiveWaitSnapshot> ActiveWaits { get; init; } = [];
}

/// <summary>Provider-owned active-wait projection.</summary>
public sealed record WorkflowProjectionActiveWaitSnapshot
{
    public required WaitId WaitId { get; init; }

    public required string EventName { get; init; }

    public required CorrelationId CorrelationId { get; init; }

    public required DateTimeOffset RegisteredAt { get; init; }

    public string? BranchId { get; init; }

    public required string Status { get; init; }

    public required string Mode { get; init; }

    public FiberId? FiberId { get; init; }

    public ScopeId? ScopeId { get; init; }

    public long WaitSequence { get; init; }
}
