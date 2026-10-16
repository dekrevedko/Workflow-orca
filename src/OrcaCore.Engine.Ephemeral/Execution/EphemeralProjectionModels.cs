using OrcaCore.Abstractions.Ids;

namespace OrcaCore.Engine.Ephemeral.Execution;

// Runtime-only state used by the in-process scheduler. These records deliberately live in the
// engine assembly: the application contract exposes only OrcaCore.WorkflowInstanceSnapshot.
internal sealed record EphemeralActiveStepSnapshot
{
    internal required string StepPath { get; init; }
    internal required DateTimeOffset StartedAt { get; init; }
    internal TimeSpan? ExpectedTimeout { get; init; }
}

internal sealed record EphemeralActiveWaitSnapshot
{
    internal required WaitId WaitId { get; init; }
    internal required WorkflowEventContract EventContract { get; init; }
    internal required CorrelationId CorrelationId { get; init; }
    internal required DateTimeOffset RegisteredAt { get; init; }
    internal required string AuthoredPath { get; init; }
    internal DateTimeOffset? Deadline { get; init; }
    internal string? BranchId { get; init; }
    internal required string Status { get; init; }
    internal required string Mode { get; init; }
    internal FiberId? FiberId { get; init; }
    internal ScopeId? ScopeId { get; init; }
    internal long WaitSequence { get; init; }
}

internal sealed record EphemeralCompositionBranchOutcomeSnapshot
{
    internal required string CompositionId { get; init; }
    internal required string BranchId { get; init; }
    internal required string Status { get; init; }
    internal required DateTimeOffset RecordedAt { get; init; }
}

internal enum EphemeralForEachWorkItemStatus
{
    Pending,
    Active,
    Completed,
    Failed,
    Cancelled
}

internal sealed record EphemeralForEachWorkItemSnapshot
{
    internal required int Index { get; init; }
    internal required IReadOnlyList<object?> Items { get; init; }
    internal required EphemeralForEachWorkItemStatus Status { get; init; }
    internal DateTimeOffset? StartedAt { get; init; }
    internal DateTimeOffset? CompletedAt { get; init; }
    internal string? ErrorSummary { get; init; }
}

internal sealed record EphemeralForEachGroupSnapshot
{
    internal required string GroupId { get; init; }
    internal required int TotalItems { get; init; }
    internal required int WorkItemCount { get; init; }
    internal int? MaxConcurrency { get; init; }
    internal required int ActiveCount { get; init; }
    internal required int CompletedCount { get; init; }
    internal required int FailedCount { get; init; }
    internal required int CancelledCount { get; init; }
    internal required IReadOnlyList<EphemeralForEachWorkItemSnapshot> WorkItems { get; init; }
}

internal sealed record EphemeralLifecycleEventSnapshot
{
    internal required InstanceId InstanceId { get; init; }
    internal required string EventName { get; init; }
    internal string? StepPath { get; init; }
    internal WorkflowInstanceStatus? Status { get; init; }
    internal required DateTimeOffset OccurredAt { get; init; }
    internal required bool Durable { get; init; }
}

internal sealed record EphemeralWorkflowInstanceSnapshot
{
    internal required InstanceId InstanceId { get; init; }
    internal required DefinitionId DefinitionId { get; init; }
    internal required DefinitionVersion DefinitionVersion { get; init; }
    internal required WorkflowInstanceStatus Status { get; init; }
    internal required DateTimeOffset CreatedAt { get; init; }
    internal required DateTimeOffset UpdatedAt { get; init; }
    internal DateTimeOffset? CurrentStatusEnteredAt { get; init; }
    internal DateTimeOffset? LastActiveAt { get; init; }
    internal bool IsStuck { get; init; }
    internal bool HasStuckStep { get; init; }
    internal string? StuckStepPath { get; init; }
    internal DateTimeOffset? StuckDetectedAt { get; init; }
    internal string? ErrorSummary { get; init; }
    internal string? EndOutcomeName { get; init; }
    internal IReadOnlyList<EphemeralActiveWaitSnapshot> ActiveWaits { get; init; } = [];
    internal EphemeralActiveStepSnapshot? ActiveStep { get; init; }
    internal IReadOnlyList<EphemeralCompositionBranchOutcomeSnapshot> CompositionOutcomes { get; init; } = [];
    internal IReadOnlyList<EphemeralForEachGroupSnapshot> ForEachGroups { get; init; } = [];
    internal IReadOnlyList<EphemeralLifecycleEventSnapshot> LifecycleEvents { get; init; } = [];
}
