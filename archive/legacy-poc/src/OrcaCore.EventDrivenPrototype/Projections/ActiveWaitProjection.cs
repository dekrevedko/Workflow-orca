using OrcaCore.Abstractions.Enums;

namespace OrcaCore.EventDrivenPrototype.Projections;

public sealed record ActiveWaitProjection(
    string InstanceId,
    string DefinitionId,
    string DefinitionVersion,
    string WaitId,
    string EventName,
    string CorrelationId,
    WaitMode Mode,
    DateTimeOffset RegisteredAt);
