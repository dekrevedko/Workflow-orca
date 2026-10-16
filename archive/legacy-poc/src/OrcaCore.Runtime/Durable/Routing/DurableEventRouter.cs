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
            if (EventMatcher.FindMatch(runtime.ActiveWaits, envelope) is not { } match)
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
                outboxRecords.AddRange(CreateTransitionOutboxRecords(
                    instanceId,
                    streamVersion: instance.ConcurrencyToken + 1,
                    runtime,
                    envelope.EventName,
                    options.PayloadEnvelopeSerializer));
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
        var committed = await store.CommitAsync(
            new WorkflowCommit(
                persisted,
                inboxRecords,
                processedInboxEventIds,
                outboxRecords,
                [],
                historyRecords),
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
        SerializedPayloadEnvelope? payloadEnvelope = null;
        var payloadType = envelope.PayloadType;
        if (payloadType is not null)
        {
            var serialized = options.PayloadEnvelopeSerializer.Serialize(envelope.Payload, payloadType);
            if (serialized.IsFailure)
                throw serialized.Error!;

            payloadEnvelope = serialized.Value;
        }

        return new InboxRecord(
            envelope.EventId,
            instanceId,
            new PersistedEventEnvelope(
                envelope.EventName,
                envelope.CorrelationId,
                payloadEnvelope,
                envelope.EventId),
            DateTimeOffset.UtcNow,
            processed);
    }

    internal static List<OutboxRecord> CreateTransitionOutboxRecords(
        string instanceId,
        int streamVersion,
        RuntimeState runtime,
        string trigger,
        IPayloadEnvelopeSerializer payloadEnvelopeSerializer)
    {
        if (runtime.Status is not WorkflowStatus.Waiting and not WorkflowStatus.Completed and not WorkflowStatus.Failed)
            return [];

        var payloadResult = payloadEnvelopeSerializer.Serialize(
            new TransitionStatusPayload(
                trigger,
                runtime.Status.ToString(),
                runtime.ActiveWaits.Count(x => x.Status == WaitStatus.Active)),
            typeof(TransitionStatusPayload));
        if (payloadResult.IsFailure)
            throw payloadResult.Error!;

        var createdAt = DateTimeOffset.UtcNow;
        var messageType = $"Workflow{runtime.Status}";
        return
        [
            new OutboxRecord(
                OutboxRecord.CreateDeterministicId(instanceId, streamVersion, 0),
                OutboxRecord.CreateDeterministicId(instanceId, streamVersion, 0),
                instanceId,
                ParentInstanceId: null,
                RootInstanceId: instanceId,
                GroupId: null,
                StreamId: instanceId,
                StreamVersion: streamVersion,
                Sequence: 0,
                MessageType: messageType,
                Channel: "workflow-status",
                Destination: messageType,
                PayloadEnvelope: payloadResult.Value!,
                CorrelationId: null,
                CausationEventId: null,
                ResumeTokenId: null,
                Status: OutboxStatus.Pending,
                AttemptCount: 0,
                CreatedAt: createdAt,
                LastAttemptAt: null,
                NextAttemptAt: null,
                LeaseOwner: null,
                LeaseExpiresAt: null,
                LastError: null)
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

internal sealed record TransitionStatusPayload(
    string Trigger,
    string Status,
    int ActiveWaitCount);
