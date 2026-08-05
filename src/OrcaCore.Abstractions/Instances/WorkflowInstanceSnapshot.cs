using OrcaCore.Abstractions.Ids;

namespace OrcaCore.Abstractions.Instances;

/// <summary>
/// Provides an immutable metadata-only view of a workflow instance.
/// </summary>
public sealed record WorkflowInstanceSnapshot
{
    /// <summary>
    /// Gets the logical workflow instance identity.
    /// </summary>
    public required InstanceId InstanceId { get; init; }

    /// <summary>
    /// Gets the parent workflow instance when this snapshot describes child work.
    /// </summary>
    public InstanceId? ParentInstanceId { get; init; }

    /// <summary>
    /// Gets the root workflow instance for the workflow tree.
    /// </summary>
    public InstanceId? RootInstanceId { get; init; }

    /// <summary>
    /// Gets the workflow definition identity.
    /// </summary>
    public required DefinitionId DefinitionId { get; init; }

    /// <summary>
    /// Gets the workflow definition version bound at start.
    /// </summary>
    public required DefinitionVersion DefinitionVersion { get; init; }

    /// <summary>
    /// Gets the current lifecycle status.
    /// </summary>
    public required global::OrcaCore.WorkflowInstanceStatus Status { get; init; }

    /// <summary>
    /// Gets the monotonic committed stream version for optimistic concurrency, when the
    /// engine mode tracks one (durable instances; null on ephemeral snapshots).
    /// </summary>
    public long? StreamVersion { get; init; }

    /// <summary>
    /// Gets when the instance was created.
    /// </summary>
    public required DateTimeOffset CreatedAt { get; init; }

    /// <summary>
    /// Gets when the instance metadata last changed.
    /// </summary>
    public required DateTimeOffset UpdatedAt { get; init; }

    /// <summary>
    /// Gets when the current lifecycle status was entered.
    /// </summary>
    public DateTimeOffset? CurrentStatusEnteredAt { get; init; }

    /// <summary>
    /// Gets when the instance last made observable execution progress.
    /// </summary>
    public DateTimeOffset? LastActiveAt { get; init; }

    /// <summary>
    /// Gets whether the instance has any current stuck signal.
    /// </summary>
    public bool IsStuck { get; init; }

    /// <summary>
    /// Gets whether any step on the instance has exceeded its stuck threshold.
    /// </summary>
    public bool HasStuckStep { get; init; }

    /// <summary>
    /// Gets the path of the step that most recently exceeded its stuck threshold.
    /// </summary>
    public string? StuckStepPath { get; init; }

    /// <summary>
    /// Gets when stuck work was detected.
    /// </summary>
    public DateTimeOffset? StuckDetectedAt { get; init; }

    /// <summary>
    /// Gets the failure summary when status is failed.
    /// </summary>
    public string? ErrorSummary { get; init; }

    /// <summary>
    /// Gets the named end outcome when the definition ended with one.
    /// </summary>
    public string? EndOutcomeName { get; init; }

    /// <summary>
    /// Gets the current continue-as-new generation for this logical instance.
    /// </summary>
    public int ContinueAsNewGeneration { get; init; }

    /// <summary>
    /// Gets when durable metadata was archived, when archived.
    /// </summary>
    public DateTimeOffset? ArchivedAt { get; init; }

    /// <summary>
    /// Gets immutable snapshots of currently active waits.
    /// </summary>
    public IReadOnlyList<ActiveWaitSnapshot> ActiveWaits { get; init; } = [];

    /// <summary>
    /// Gets the step currently executing on this instance, when the in-process engine can observe it.
    /// </summary>
    public ActiveStepSnapshot? ActiveStep { get; init; }

    /// <summary>
    /// Gets immutable snapshots of completed composition branch outcomes.
    /// </summary>
    public IReadOnlyList<CompositionBranchOutcomeSnapshot> CompositionOutcomes { get; init; } = [];

    /// <summary>
    /// Gets immutable snapshots of in-instance ForEach groups.
    /// </summary>
    public IReadOnlyList<ForEachGroupSnapshot> ForEachGroups { get; init; } = [];

    /// <summary>
    /// Gets immutable snapshots of lifecycle events observed for the instance.
    /// </summary>
    public IReadOnlyList<LifecycleEventSnapshot> LifecycleEvents { get; init; } = [];

    /// <summary>
    /// Gets durable saga audit snapshots for compensation scopes on this instance.
    /// </summary>
    public IReadOnlyList<SagaAuditScopeSnapshot> SagaAudits { get; init; } = [];
}
