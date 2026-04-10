using OrcaCore.Abstractions.Enums;
using OrcaCore.Abstractions.Models;
using OrcaCore.EventDrivenPrototype.Definitions;
using OrcaCore.EventDrivenPrototype.Persistence;
using OrcaCore.EventDrivenPrototype.Projections;

namespace OrcaCore.EventDrivenPrototype.Engine;

public sealed class EventDrivenWorkflowEngine(InMemoryPrototypeStore store)
{
    private readonly Dictionary<(string DefinitionId, string DefinitionVersion), RegisteredPrototypeDefinition> _definitions = [];
    private readonly System.Collections.Concurrent.ConcurrentDictionary<string, PrototypeInstanceCommandLane> _commandLanes = new();

    public void Register<TState>(EventDrivenWorkflowDefinition<TState> definition)
    {
        _definitions[(definition.DefinitionId, definition.DefinitionVersion)] = RegisteredPrototypeDefinition.Create(definition);
    }

    public async Task<string> StartAsync<TState>(string definitionId, string definitionVersion, TState initialState, string? correlationIdOverride = null, CancellationToken cancellationToken = default)
    {
        var registered = GetDefinition(definitionId, definitionVersion);
        var instanceId = Guid.NewGuid().ToString("N");
        var execution = await registered.RunToSuspensionAsync(initialState, null, cancellationToken);
        var checkpoint = BuildCheckpoint(instanceId, registered, execution, correlationIdOverride, [], []);
        var newEvents = BuildLifecycleEvents(0, execution, correlationIdOverride);

        await store.CommitAsync(instanceId, checkpoint, newEvents, [], cancellationToken);
        _commandLanes.TryAdd(instanceId, new PrototypeInstanceCommandLane());

        if (checkpoint.Status == WorkflowStatus.Waiting && checkpoint.PendingEvents.Count > 0)
        {
            await TryConsumeBufferedEventsAsync(instanceId, checkpoint, registered, cancellationToken);
        }

        return instanceId;
    }

    public Task<InstanceSummaryProjection?> GetSummaryAsync(string instanceId, CancellationToken cancellationToken = default)
        => store.GetSummaryAsync(instanceId, cancellationToken);

    public async Task<TState> GetStateAsync<TState>(string instanceId, CancellationToken cancellationToken = default)
    {
        var checkpoint = await LoadCheckpointRequiredAsync(instanceId, cancellationToken);
        return PrototypeCloner.Clone((TState)checkpoint.BusinessState);
    }

    public Task<IReadOnlyList<ActiveWaitProjection>> GetActiveWaitsAsync(string instanceId, CancellationToken cancellationToken = default)
        => store.GetActiveWaitsAsync(instanceId, cancellationToken);

    public async Task RaiseEventToInstanceAsync(string instanceId, EventEnvelope envelope, CancellationToken cancellationToken = default)
    {
        var lane = _commandLanes.GetOrAdd(instanceId, static _ => new PrototypeInstanceCommandLane());
        await lane.EnqueueAsync(async ct =>
        {
            var checkpoint = await LoadCheckpointRequiredAsync(instanceId, ct);

            if (checkpoint.ConsumedEventIds.Contains(envelope.EventId))
            {
                await CommitDuplicateIgnoredAsync(checkpoint, envelope, ct);
                return;
            }

            var matchingWait = checkpoint.ActiveWaits.FirstOrDefault(x =>
                x.Status == WaitStatus.Active &&
                x.EventName == envelope.EventName &&
                x.CorrelationId == envelope.CorrelationId);

            if (matchingWait is null)
            {
                var pendingEvents = checkpoint.PendingEvents.ToList();

                if (pendingEvents.Any(x => x.Envelope.EventId == envelope.EventId))
                {
                    await CommitDuplicateIgnoredAsync(checkpoint, envelope, ct);
                    return;
                }

                pendingEvents.Add(new PendingEvent(envelope, DateTimeOffset.UtcNow, false));

                var bufferedCheckpoint = checkpoint with
                {
                    PendingEvents = pendingEvents,
                    StreamVersion = checkpoint.StreamVersion + 1
                };

                await store.CommitAsync(
                    instanceId,
                    bufferedCheckpoint,
                    [CreateEventRecord(checkpoint.StreamVersion + 1, PrototypeEventTypes.EventBuffered, envelope.EventId, envelope.EventName, envelope.CorrelationId)],
                    [new PrototypeInboxRecord(envelope.EventId, envelope.EventName, envelope.CorrelationId, DateTimeOffset.UtcNow, false)],
                    ct);

                return;
            }

            var registered = GetDefinition(checkpoint.DefinitionId, checkpoint.DefinitionVersion);
            var execution = await registered.RunToSuspensionAsync(checkpoint, envelope, ct);
            var remainingWaits = checkpoint.ActiveWaits.Where(x => x.WaitId != matchingWait.WaitId).ToArray();
            var pending = checkpoint.PendingEvents.ToList();

            var nextCheckpoint = BuildCheckpoint(instanceId, registered, execution, null, remainingWaits, pending, checkpoint);
            var events = new List<PrototypeStreamRecord>
            {
                CreateEventRecord(checkpoint.StreamVersion + 1, PrototypeEventTypes.WaitMatched, envelope.EventId, envelope.EventName, envelope.CorrelationId)
            };
            events.AddRange(BuildExecutionEvents(checkpoint.StreamVersion + 1, execution));

            var consumedIds = checkpoint.ConsumedEventIds.ToHashSet(StringComparer.Ordinal);
            consumedIds.Add(envelope.EventId);
            nextCheckpoint = nextCheckpoint with { ConsumedEventIds = consumedIds };

            await store.CommitAsync(
                instanceId,
                nextCheckpoint,
                events,
                [new PrototypeInboxRecord(envelope.EventId, envelope.EventName, envelope.CorrelationId, DateTimeOffset.UtcNow, true)],
                ct);

            if (nextCheckpoint.Status == WorkflowStatus.Waiting && nextCheckpoint.PendingEvents.Count > 0)
            {
                await TryConsumeBufferedEventsAsync(instanceId, nextCheckpoint, registered, ct);
            }
        }, cancellationToken);
    }

    public async Task RaiseEventByCorrelationAsync(EventEnvelope envelope, CancellationToken cancellationToken = default)
    {
        var instanceId = await store.ResolveByCorrelationAsync(envelope.EventName, envelope.CorrelationId, cancellationToken);

        if (instanceId is null)
        {
            throw new InvalidOperationException($"No active wait for event '{envelope.EventName}' and correlation '{envelope.CorrelationId}'.");
        }

        await RaiseEventToInstanceAsync(instanceId, envelope, cancellationToken);
    }

    private async Task TryConsumeBufferedEventsAsync(
        string instanceId,
        PrototypeCheckpointState checkpoint,
        RegisteredPrototypeDefinition registered,
        CancellationToken cancellationToken)
    {
        var activeWait = checkpoint.ActiveWaits.FirstOrDefault(x => x.Status == WaitStatus.Active);

        if (activeWait is null)
        {
            return;
        }

        var pending = checkpoint.PendingEvents.FirstOrDefault(x =>
            !x.Consumed &&
            x.Envelope.EventName == activeWait.EventName &&
            x.Envelope.CorrelationId == activeWait.CorrelationId &&
            !checkpoint.ConsumedEventIds.Contains(x.Envelope.EventId));

        if (pending is null)
        {
            return;
        }

        var execution = await registered.RunToSuspensionAsync(checkpoint, pending.Envelope, cancellationToken);
        var remainingPending = checkpoint.PendingEvents
            .Where(x => x.Envelope.EventId != pending.Envelope.EventId)
            .ToArray();
        var remainingWaits = checkpoint.ActiveWaits.Where(x => x.WaitId != activeWait.WaitId).ToArray();

        var nextCheckpoint = BuildCheckpoint(instanceId, registered, execution, null, remainingWaits, remainingPending, checkpoint);
        var consumedIds = checkpoint.ConsumedEventIds.ToHashSet(StringComparer.Ordinal);
        consumedIds.Add(pending.Envelope.EventId);
        nextCheckpoint = nextCheckpoint with { ConsumedEventIds = consumedIds };

        var events = new List<PrototypeStreamRecord>
        {
            CreateEventRecord(checkpoint.StreamVersion + 1, PrototypeEventTypes.BufferedEventConsumed, pending.Envelope.EventId, pending.Envelope.EventName, pending.Envelope.CorrelationId)
        };
        events.AddRange(BuildExecutionEvents(checkpoint.StreamVersion + 1, execution));

        await store.CommitAsync(
            instanceId,
            nextCheckpoint,
            events,
            [new PrototypeInboxRecord(pending.Envelope.EventId, pending.Envelope.EventName, pending.Envelope.CorrelationId, pending.ReceivedAt, true)],
            cancellationToken);
    }

    private async Task CommitDuplicateIgnoredAsync(PrototypeCheckpointState checkpoint, EventEnvelope envelope, CancellationToken cancellationToken)
    {
        var nextCheckpoint = checkpoint with { StreamVersion = checkpoint.StreamVersion + 1 };

        await store.CommitAsync(
            checkpoint.InstanceId,
            nextCheckpoint,
            [CreateEventRecord(nextCheckpoint.StreamVersion, PrototypeEventTypes.DuplicateEventIgnored, envelope.EventId, envelope.EventName, envelope.CorrelationId)],
            [new PrototypeInboxRecord(envelope.EventId, envelope.EventName, envelope.CorrelationId, DateTimeOffset.UtcNow, true)],
            cancellationToken);
    }

    private static PrototypeCheckpointState BuildCheckpoint(
        string instanceId,
        RegisteredPrototypeDefinition registered,
        PrototypeExecutionResult execution,
        string? correlationIdOverride,
        IReadOnlyList<WaitRecord> carriedWaits,
        IReadOnlyList<PendingEvent> pendingEvents,
        PrototypeCheckpointState? previous = null)
    {
        var waits = carriedWaits.ToList();
        var status = WorkflowStatus.Running;

        if (execution.WaitEventName is not null && execution.WaitCorrelationId is not null)
        {
            status = WorkflowStatus.Waiting;

            var correlationId = correlationIdOverride ?? execution.WaitCorrelationId;

            waits.Add(new WaitRecord(
                Guid.NewGuid().ToString("N"),
                execution.WaitEventName,
                correlationId,
                null,
                DateTimeOffset.UtcNow,
                WaitStatus.Active,
                WaitMode.Resident));
        }
        else if (execution.Error is not null)
        {
            status = WorkflowStatus.Failed;
        }
        else if (execution.IsCompleted)
        {
            status = WorkflowStatus.Completed;
        }

        return new PrototypeCheckpointState(
            instanceId,
            registered.DefinitionId,
            registered.DefinitionVersion,
            registered.StateType,
            PrototypeCloner.CloneObject(execution.BusinessState, registered.StateType),
            status,
            execution.NextStepIndex,
            waits,
            pendingEvents.ToArray(),
            previous?.ConsumedEventIds.ToHashSet(StringComparer.Ordinal) ?? new HashSet<string>(StringComparer.Ordinal),
            (previous?.StreamVersion ?? 0) + (execution.WaitEventName is not null || execution.Error is not null || execution.IsCompleted ? 2 : 1));
    }

    private static List<PrototypeStreamRecord> BuildLifecycleEvents(int currentVersion, PrototypeExecutionResult execution, string? correlationIdOverride)
    {
        var events = new List<PrototypeStreamRecord>
        {
            CreateEventRecord(++currentVersion, PrototypeEventTypes.WorkflowStarted, null, null, null)
        };

        events.AddRange(BuildExecutionEvents(currentVersion, execution, correlationIdOverride));
        return events;
    }

    private static IEnumerable<PrototypeStreamRecord> BuildExecutionEvents(int currentVersion, PrototypeExecutionResult execution, string? correlationIdOverride = null)
    {
        if (execution.Error is not null)
        {
            yield return CreateEventRecord(++currentVersion, PrototypeEventTypes.WorkflowFailed, null, null, null);
            yield break;
        }

        if (execution.WaitEventName is not null && execution.WaitCorrelationId is not null)
        {
            yield return CreateEventRecord(++currentVersion, PrototypeEventTypes.StepCompleted, null, null, null);
            yield return CreateEventRecord(++currentVersion, PrototypeEventTypes.WaitRegistered, null, execution.WaitEventName, correlationIdOverride ?? execution.WaitCorrelationId);
            yield break;
        }

        yield return CreateEventRecord(++currentVersion, PrototypeEventTypes.StepCompleted, null, null, null);

        if (execution.IsCompleted)
        {
            yield return CreateEventRecord(++currentVersion, PrototypeEventTypes.WorkflowCompleted, null, null, null);
        }
    }

    private static PrototypeStreamRecord CreateEventRecord(int version, string eventType, string? eventId, string? eventName, string? correlationId)
    {
        var data = new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["EventId"] = eventId,
            ["EventName"] = eventName,
            ["CorrelationId"] = correlationId
        };

        return new PrototypeStreamRecord(version, eventType, DateTimeOffset.UtcNow, data);
    }

    private RegisteredPrototypeDefinition GetDefinition(string definitionId, string definitionVersion)
    {
        if (_definitions.TryGetValue((definitionId, definitionVersion), out var definition))
        {
            return definition;
        }

        throw new InvalidOperationException($"Definition '{definitionId}' version '{definitionVersion}' is not registered.");
    }

    private async Task<PrototypeCheckpointState> LoadCheckpointRequiredAsync(string instanceId, CancellationToken cancellationToken)
    {
        var checkpoint = await store.LoadCheckpointAsync(instanceId, cancellationToken);

        if (checkpoint is null)
        {
            throw new InvalidOperationException($"Instance '{instanceId}' was not found.");
        }

        return checkpoint;
    }
}
