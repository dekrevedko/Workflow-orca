using OrcaCore.Abstractions.Ids;

namespace OrcaCore.Abstractions.Durable;

/// <summary>
/// The closed vocabulary of the engine's own orchestration facts (DU-012) — never business
/// domain events. The append-only per-instance stream of these records is the write-side
/// source of truth (DU-010). Every variant carries instance identity, its position in the
/// stream, causation metadata, and an occurrence timestamp, declared once here so the whole
/// catalog structurally carries them (DU-011).
/// </summary>
public abstract record WorkflowEvent
{
    private WorkflowEvent(InstanceId instanceId, StreamVersion streamVersion, CausationId causationId, DateTimeOffset occurredAt)
    {
        InstanceId = instanceId;
        StreamVersion = streamVersion;
        CausationId = causationId;
        OccurredAt = occurredAt;
    }

    public InstanceId InstanceId { get; }

    public StreamVersion StreamVersion { get; }

    public CausationId CausationId { get; }

    public DateTimeOffset OccurredAt { get; }

    /// <summary>The instance started and bound to a definition version (DU-040).</summary>
    public sealed record WorkflowStarted(
        InstanceId InstanceId,
        StreamVersion StreamVersion,
        CausationId CausationId,
        DateTimeOffset OccurredAt,
        DefinitionId DefinitionId,
        DefinitionVersion DefinitionVersion,
        object? Input) : WorkflowEvent(InstanceId, StreamVersion, CausationId, OccurredAt);

    /// <summary>The instance's definition version binding was recorded as a durable fact (DU-040).</summary>
    public sealed record VersionBound(
        InstanceId InstanceId,
        StreamVersion StreamVersion,
        CausationId CausationId,
        DateTimeOffset OccurredAt,
        DefinitionId DefinitionId,
        DefinitionVersion DefinitionVersion) : WorkflowEvent(InstanceId, StreamVersion, CausationId, OccurredAt);

    /// <summary>A business or infrastructure step began executing.</summary>
    public sealed record StepEntered(
        InstanceId InstanceId,
        StreamVersion StreamVersion,
        CausationId CausationId,
        DateTimeOffset OccurredAt,
        string StepName) : WorkflowEvent(InstanceId, StreamVersion, CausationId, OccurredAt);

    /// <summary>A step completed successfully.</summary>
    public sealed record StepSucceeded(
        InstanceId InstanceId,
        StreamVersion StreamVersion,
        CausationId CausationId,
        DateTimeOffset OccurredAt,
        string StepName) : WorkflowEvent(InstanceId, StreamVersion, CausationId, OccurredAt);

    /// <summary>A step failed (CR-014).</summary>
    public sealed record StepFailed(
        InstanceId InstanceId,
        StreamVersion StreamVersion,
        CausationId CausationId,
        DateTimeOffset OccurredAt,
        string StepName,
        string ErrorSummary) : WorkflowEvent(InstanceId, StreamVersion, CausationId, OccurredAt);

    /// <summary>A resident wait was registered (EV-021, EV-040).</summary>
    public sealed record WaitRegistered(
        InstanceId InstanceId,
        StreamVersion StreamVersion,
        CausationId CausationId,
        DateTimeOffset OccurredAt,
        WaitId WaitId,
        string EventName,
        CorrelationId CorrelationId) : WorkflowEvent(InstanceId, StreamVersion, CausationId, OccurredAt);

    /// <summary>A wait matched an inbound event and resumed the instance (EV-022/EV-023).</summary>
    public sealed record WaitMatched(
        InstanceId InstanceId,
        StreamVersion StreamVersion,
        CausationId CausationId,
        DateTimeOffset OccurredAt,
        WaitId WaitId,
        EventId EventId) : WorkflowEvent(InstanceId, StreamVersion, CausationId, OccurredAt);

    /// <summary>An inbound event arrived before a matching wait and was buffered (EV-030).</summary>
    public sealed record EventBuffered(
        InstanceId InstanceId,
        StreamVersion StreamVersion,
        CausationId CausationId,
        DateTimeOffset OccurredAt,
        EventId EventId,
        string EventName) : WorkflowEvent(InstanceId, StreamVersion, CausationId, OccurredAt);

    /// <summary>A buffered event was consumed by a matching wait (EV-030/EV-032).</summary>
    public sealed record EventConsumed(
        InstanceId InstanceId,
        StreamVersion StreamVersion,
        CausationId CausationId,
        DateTimeOffset OccurredAt,
        EventId EventId,
        string EventName) : WorkflowEvent(InstanceId, StreamVersion, CausationId, OccurredAt);

    /// <summary>A duplicate delivery of an already-consumed <see cref="EventId"/> was discarded (EV-031).</summary>
    public sealed record DuplicateEventDiscarded(
        InstanceId InstanceId,
        StreamVersion StreamVersion,
        CausationId CausationId,
        DateTimeOffset OccurredAt,
        EventId EventId) : WorkflowEvent(InstanceId, StreamVersion, CausationId, OccurredAt);

    /// <summary>The instance completed, with an optional named outcome (CR-008).</summary>
    public sealed record WorkflowCompleted(
        InstanceId InstanceId,
        StreamVersion StreamVersion,
        CausationId CausationId,
        DateTimeOffset OccurredAt,
        string? EndOutcomeName) : WorkflowEvent(InstanceId, StreamVersion, CausationId, OccurredAt);

    /// <summary>The instance failed (CR-014).</summary>
    public sealed record WorkflowFailed(
        InstanceId InstanceId,
        StreamVersion StreamVersion,
        CausationId CausationId,
        DateTimeOffset OccurredAt,
        string ErrorSummary) : WorkflowEvent(InstanceId, StreamVersion, CausationId, OccurredAt);

    /// <summary>The instance was deleted per retention policy.</summary>
    public sealed record InstanceDeleted(
        InstanceId InstanceId,
        StreamVersion StreamVersion,
        CausationId CausationId,
        DateTimeOffset OccurredAt) : WorkflowEvent(InstanceId, StreamVersion, CausationId, OccurredAt);
}
