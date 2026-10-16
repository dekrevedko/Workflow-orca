using OrcaCore.Abstractions.Durable;
using OrcaCore.Abstractions.Ids;
using OrcaCore.Abstractions.Providers;

namespace OrcaCore.Engine.Durable.Aggregates;

using WorkflowStatus = global::OrcaCore.WorkflowInstanceStatus;

/// <summary>
/// Memento carrying the full construction state of a <see cref="DurableWorkflowAggregate"/>.
/// Named init-only properties (collections default to empty) replace the former positional
/// constructor so state fields cannot be silently transposed, and adding a field touches
/// exactly one construction surface.
/// </summary>
internal sealed record DurableAggregateState
{
    internal InstanceId InstanceId { get; init; } = null!;

    internal StreamVersion StreamVersion { get; init; } = StreamVersion.Empty;

    internal InstanceId? ParentInstanceId { get; init; }

    internal InstanceId? RootInstanceId { get; init; }

    internal DefinitionId? DefinitionId { get; init; }

    internal DefinitionVersion? DefinitionVersion { get; init; }

    internal WorkflowStatus? Status { get; init; }

    internal DateTimeOffset? CreatedAt { get; init; }

    internal DateTimeOffset? UpdatedAt { get; init; }

    internal string? LastStepPath { get; init; }

    internal string? ErrorSummary { get; init; }

    internal string? OutcomeName { get; init; }

    internal int ContinueAsNewGeneration { get; init; }

    internal IReadOnlyList<DurableActiveTimer> ActiveTimers { get; init; } = [];

    internal IReadOnlyList<DurableActiveWait> ActiveWaits { get; init; } = [];

    internal IReadOnlyList<ResourcePoolTicket> ActiveResourceTickets { get; init; } = [];

    internal IReadOnlyList<DurablePendingResume> PendingResumes { get; init; } = [];

    /// <summary>
    /// Gets the serialized start input content type; restored only by replaying the start
    /// fact, never from a checkpoint (checkpoints do not carry the start input).
    /// </summary>
    internal string? StartInputContentType { get; init; }

    /// <summary>
    /// Gets the serialized start input; restored only by replaying the start fact.
    /// </summary>
    internal byte[]? StartInputPayload { get; init; }

    /// <summary>
    /// Gets the park reason while the instance status is Parked; restored only by replay.
    /// </summary>
    internal DurableParkReason? ParkReason { get; init; }

    internal int ContinuationFailureCount { get; init; }

    internal StreamVersion? ContinuationFailurePositionStreamVersion { get; init; }

    internal DateTimeOffset? ContinuationRetryNotBefore { get; init; }
}
