using OrcaCore.Abstractions.Enums;
using OrcaCore.Abstractions.Models;
using OrcaCore.Abstractions.Primitives;
using OrcaCore.EventDrivenPrototype.Persistence;

namespace OrcaCore.EventDrivenPrototype.Engine;

internal static class PrototypeEventRouting
{
    internal enum RaiseToInstanceKind
    {
        DuplicateConsumed,
        DuplicatePendingBuffer,
        BufferUnmatchedEvent,
        MatchingWaitResume
    }

    internal readonly record struct RaiseToInstanceDisposition(RaiseToInstanceKind Kind, WaitRecord? MatchingWait);

    internal static RaiseToInstanceDisposition ClassifyRaiseToInstance(PrototypeCheckpointState checkpoint, EventEnvelope envelope)
    {
        if (checkpoint.ConsumedEventIds.Contains(envelope.EventId))
            return new RaiseToInstanceDisposition(RaiseToInstanceKind.DuplicateConsumed, null);

        var matchingWait = checkpoint.ActiveWaits.FirstOrDefault(x =>
            x.Status == WaitStatus.Active &&
            x.EventName == envelope.EventName &&
            x.CorrelationId == envelope.CorrelationId);

        if (matchingWait is not null)
            return new RaiseToInstanceDisposition(RaiseToInstanceKind.MatchingWaitResume, matchingWait);

        if (checkpoint.PendingEvents.Any(x => x.Envelope.EventId == envelope.EventId))
            return new RaiseToInstanceDisposition(RaiseToInstanceKind.DuplicatePendingBuffer, null);

        return new RaiseToInstanceDisposition(RaiseToInstanceKind.BufferUnmatchedEvent, null);
    }

    internal static Result<RegisteredPrototypeDefinition> TryGetDefinition(
        IReadOnlyDictionary<(string DefinitionId, string DefinitionVersion), RegisteredPrototypeDefinition> definitions,
        string definitionId,
        string definitionVersion)
    {
        if (definitions.TryGetValue((definitionId, definitionVersion), out var definition))
            return Result<RegisteredPrototypeDefinition>.Success(definition);

        return Result<RegisteredPrototypeDefinition>.Failure(
            new InvalidOperationException($"Definition '{definitionId}' version '{definitionVersion}' is not registered."));
    }

    internal static Result<PrototypeCheckpointState> TryRequireCheckpoint(PrototypeCheckpointState? checkpoint, string instanceId)
    {
        if (checkpoint is null)
        {
            return Result<PrototypeCheckpointState>.Failure(
                new InvalidOperationException($"Instance '{instanceId}' was not found."));
        }

        return Result<PrototypeCheckpointState>.Success(checkpoint);
    }

    internal static PrototypeCheckpointState RequireCheckpoint(PrototypeCheckpointState? checkpoint, string instanceId) =>
        TryRequireCheckpoint(checkpoint, instanceId).Match(static ex => throw ex, static c => c);
}
