
namespace OrcaCore.Runtime.Durable.Persistence;

public sealed record PersistedRuntimeState(
    WorkflowStatus Status,
    DateTimeOffset CreatedAt,
    DateTimeOffset LastTransitionAt,
    IReadOnlyList<PersistedWaitRecord> ActiveWaits,
    IReadOnlyList<PersistedPendingEvent> PendingEvents,
    IReadOnlyCollection<string> ConsumedEventIds,
    PersistedError? Error,
    PersistedExecutionPath MainPath,
    PersistedParallelFrameGroup? ActiveParallel);
