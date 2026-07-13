using OrcaCore.Abstractions.Enums;
using OrcaCore.Abstractions.Models;

namespace OrcaCore.EventDrivenPrototype.Persistence;

internal sealed record PrototypeCheckpointState(
    string InstanceId,
    string DefinitionId,
    string DefinitionVersion,
    Type BusinessStateType,
    object BusinessState,
    WorkflowStatus Status,
    int NextStepIndex,
    IReadOnlyList<WaitRecord> ActiveWaits,
    IReadOnlyList<PendingEvent> PendingEvents,
    IReadOnlySet<string> ConsumedEventIds,
    int StreamVersion);
