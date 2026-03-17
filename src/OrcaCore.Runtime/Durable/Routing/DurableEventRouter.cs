using System.Text.Json;
using OrcaCore.Runtime.Durable.Persistence;

namespace OrcaCore.Runtime.Durable.Routing;

internal sealed class DurableEventRouter(
    IWorkflowStore store,
    DurableWorkflowEngineOptions options,
    DurableInstanceManager instanceManager,
    CorrelationIndex correlationIndex)
{
    public async Task RouteAsync(
        EventEnvelope envelope,
        CancellationToken cancellationToken,
        Func<PersistedInstance, IWorkflowInstance> loadFromPersisted)
    {
        var instanceId = await ResolveInstanceIdAsync(envelope, cancellationToken);
        await RaiseEventToInstanceAsync(instanceId, envelope, cancellationToken, loadFromPersisted);
    }

    public async Task RaiseEventToInstanceAsync(
        string instanceId,
        EventEnvelope envelope,
        CancellationToken cancellationToken,
        Func<PersistedInstance, IWorkflowInstance> loadFromPersisted)
    {
        var registration = await instanceManager.EnsureLoadedRegistrationAsync(instanceId, cancellationToken, loadFromPersisted);
        var instance = registration.Instance;
        var runtime = instance.RuntimeState;
        var evictAfterRelease = false;
        var inboxRecords = new List<InboxRecord>();
        var processedInboxEventIds = new List<string>();
        var outboxRecords = new List<OutboxRecord>();
        var historyRecords = new List<HistoryRecord>();
        PersistedInstance? preMutationSnapshot = null;

        await instance.ExecutionLock.WaitAsync(cancellationToken);
        try
        {
            if (await IsDuplicateEventAsync(instanceId, runtime, envelope.EventId, cancellationToken))
                return;

            preMutationSnapshot = registration.Persist(instance);
            var match = EventMatcher.FindMatch(runtime.ActiveWaits, envelope);
            if (match is null)
            {
                if (runtime.Status is WorkflowStatus.Completed or WorkflowStatus.Failed)
                {
                    throw new InvalidOperationException(
                        $"Cannot raise events for an instance in terminal state '{runtime.Status}'.");
                }

                runtime.PendingEvents.Add(new PendingEvent(envelope, DateTimeOffset.UtcNow, Consumed: false));
                inboxRecords.Add(CreateInboxRecord(instanceId, envelope, processed: false));
                historyRecords.Add(new HistoryRecord("EventBuffered", DateTimeOffset.UtcNow, envelope.EventName));
                try
                {
                    await CommitInstanceAsync(registration, instanceId, inboxRecords, processedInboxEventIds, outboxRecords, historyRecords, cancellationToken);
                }
                catch
                {
                    if (!instanceManager.TryRestoreLoadedInstanceFromSnapshot(instanceId, preMutationSnapshot))
                        await instanceManager.RecoverAfterCommitFailureAsync(instanceId, cancellationToken, loadFromPersisted);
                    throw;
                }
                evictAfterRelease = instanceManager.ShouldEvict(instanceId);
            }
            else
            {
                runtime.ConsumedEventIds.Add(envelope.EventId);

                var index = runtime.ActiveWaits.IndexOf(match);
                runtime.ActiveWaits[index] = match with { Status = WaitStatus.Matched };
                var stagedCorrelation = new StagedCorrelationMutationSink();
                stagedCorrelation.Remove(match.EventName, match.CorrelationId, instanceId);

                InstanceLifecycle.TransitionTo(runtime, WorkflowStatus.Running);

                var resume = registration.Resume;
                var report = await resume(instance, envelope, match, stagedCorrelation, cancellationToken);
                inboxRecords.Add(CreateInboxRecord(instanceId, envelope, processed: true));
                processedInboxEventIds.AddRange(report.ConsumedBufferedEventIds);
                historyRecords.Add(new HistoryRecord("EventResumed", DateTimeOffset.UtcNow, envelope.EventName));
                outboxRecords.AddRange(CreateTransitionOutboxRecords(instanceId, runtime, envelope.EventName));
                historyRecords.AddRange(CreateTransitionHistoryRecords(runtime, envelope.EventName));
                try
                {
                    await CommitInstanceAsync(registration, instanceId, inboxRecords, processedInboxEventIds, outboxRecords, historyRecords, cancellationToken);
                    stagedCorrelation.ApplyTo(correlationIndex);
                }
                catch
                {
                    if (!instanceManager.TryRestoreLoadedInstanceFromSnapshot(instanceId, preMutationSnapshot))
                        await instanceManager.RecoverAfterCommitFailureAsync(instanceId, cancellationToken, loadFromPersisted);
                    throw;
                }
                evictAfterRelease = instanceManager.ShouldEvict(instanceId);
            }
        }
        finally
        {
            instance.ExecutionLock.Release();
        }

        if (evictAfterRelease)
            instanceManager.EvictIfLongWait(instanceId);
    }

    private async Task CommitInstanceAsync(
        DurableInstanceRegistration registration,
        string instanceId,
        IReadOnlyList<InboxRecord> inboxRecords,
        IReadOnlyList<string> processedInboxEventIds,
        IReadOnlyList<OutboxRecord> outboxRecords,
        IReadOnlyList<HistoryRecord> historyRecords,
        CancellationToken cancellationToken)
    {
        var instance = registration.Instance;
        var persisted = registration.Persist(instance);
        var committed = await store.CommitTransitionAsync(
            persisted,
            inboxRecords,
            processedInboxEventIds,
            outboxRecords,
            historyRecords,
            cancellationToken);
        instance.ConcurrencyToken = committed.ConcurrencyToken;
    }

    private async Task<string> ResolveInstanceIdAsync(EventEnvelope envelope, CancellationToken cancellationToken)
    {
        try
        {
            return correlationIndex.ResolveExactlyOne(envelope.EventName, envelope.CorrelationId);
        }
        catch (NoActiveWaitException)
        {
            var matches = await store.LookupByCorrelationAsync(
                envelope.EventName,
                envelope.CorrelationId,
                cancellationToken);

            if (matches.MatchType == CorrelationMatchType.NoMatch)
                throw new NoActiveWaitException(envelope.EventName, envelope.CorrelationId);

            if (matches.MatchType == CorrelationMatchType.Ambiguous)
                throw new AmbiguousCorrelationException(envelope.EventName, envelope.CorrelationId, matches.Matches.Count);

            return matches.Matches[0].InstanceId;
        }
    }

    private async Task<bool> IsDuplicateEventAsync(
        string instanceId,
        RuntimeState runtime,
        string eventId,
        CancellationToken cancellationToken)
    {
        if (runtime.ConsumedEventIds.Contains(eventId)
            || runtime.PendingEvents.Any(p => p.Envelope.EventId == eventId))
            return true;

        var inbox = await store.GetInboxAsync(instanceId, cancellationToken);
        return inbox.Any(record => string.Equals(record.EventId, eventId, StringComparison.Ordinal));
    }

    private InboxRecord CreateInboxRecord(string instanceId, EventEnvelope envelope, bool processed)
    {
        JsonElement? payload = null;
        string? payloadTypeKey = null;
        if (envelope.Payload is not null)
        {
            if (!options.PayloadTypeResolver.TryGetTypeKey(envelope.Payload.GetType(), out payloadTypeKey))
            {
                throw new DurablePayloadSerializationException(
                    $"Payload type '{envelope.Payload.GetType().FullName}' is not registered for durable serialization.");
            }

            payload = JsonSerializer.SerializeToElement(envelope.Payload, envelope.Payload.GetType());
        }

        return new InboxRecord(
            envelope.EventId,
            instanceId,
            new PersistedEventEnvelope(
                envelope.EventName,
                envelope.CorrelationId,
                payload,
                payloadTypeKey,
                envelope.EventId),
            DateTimeOffset.UtcNow,
            processed);
    }

    internal static List<OutboxRecord> CreateTransitionOutboxRecords(
        string instanceId,
        RuntimeState runtime,
        string trigger)
    {
        if (runtime.Status is not WorkflowStatus.Waiting and not WorkflowStatus.Completed and not WorkflowStatus.Failed)
            return [];

        var payload = JsonSerializer.SerializeToElement(new
        {
            Trigger = trigger,
            Status = runtime.Status.ToString(),
            ActiveWaitCount = runtime.ActiveWaits.Count(x => x.Status == WaitStatus.Active)
        });

        return
        [
            new OutboxRecord(
                Guid.NewGuid().ToString("N"),
                instanceId,
                $"Workflow{runtime.Status}",
                payload,
                DateTimeOffset.UtcNow,
                Dispatched: false)
        ];
    }

    internal static List<HistoryRecord> CreateTransitionHistoryRecords(RuntimeState runtime, string trigger)
    {
        if (runtime.Status is not WorkflowStatus.Waiting and not WorkflowStatus.Completed and not WorkflowStatus.Failed)
            return [];

        return
        [
            new HistoryRecord(
                runtime.Status.ToString(),
                DateTimeOffset.UtcNow,
                trigger)
        ];
    }
}
