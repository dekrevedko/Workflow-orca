
namespace OrcaCore.Runtime.Querying;

public sealed class InstanceScope
{
    private readonly InMemoryInstanceStore _store;
    private readonly string _instanceId;

    internal InstanceScope(InMemoryInstanceStore store, string instanceId)
    {
        _store = store;
        _instanceId = instanceId;
    }

    public WorkflowInstanceSnapshot Get()
    {
        var instance = _store.GetUntyped(_instanceId);

        return new WorkflowInstanceSnapshot(
            _instanceId,
            instance.DefinitionId,
            instance.RuntimeState.Status,
            instance.RuntimeState.CreatedAt,
            instance.RuntimeState.LastTransitionAt,
            instance.RuntimeState.Error);
    }

    public TState GetState<TState>()
    {
        var instance = _store.Get<TState>(_instanceId);
        // Return a deep copy to prevent external mutation of internal state (DD-121)
        var json = System.Text.Json.JsonSerializer.Serialize(instance.BusinessState);
        return System.Text.Json.JsonSerializer.Deserialize<TState>(json)!;
    }

    public async Task RaiseEvent(EventEnvelope envelope, CancellationToken cancellationToken = default)
    {
        var instance = _store.GetUntyped(_instanceId);
        var runtime = instance.RuntimeState;

        await instance.ExecutionLock.WaitAsync(cancellationToken);
        try
        {
            // Dedup: skip if this EventId was already consumed or is already buffered
            if (runtime.ConsumedEventIds.Contains(envelope.EventId)
                || runtime.PendingEvents.Any(p => p.Envelope.EventId == envelope.EventId))
                return;

            var match = EventMatcher.FindMatch(runtime.ActiveWaits, envelope);
            if (match is null)
            {
                // Terminal instances reject further triggers explicitly.
                if (runtime.Status is WorkflowStatus.Completed or WorkflowStatus.Failed)
                    throw new InvalidOperationException(
                        $"Cannot raise events for an instance in terminal state '{runtime.Status}'.");

                runtime.PendingEvents.Add(new PendingEvent(envelope, DateTimeOffset.UtcNow, Consumed: false));
                return;
            }

            runtime.ConsumedEventIds.Add(envelope.EventId);

            // Mark the wait as Matched and remove from correlation index
            var index = runtime.ActiveWaits.IndexOf(match);
            runtime.ActiveWaits[index] = match with { Status = WaitStatus.Matched };
            _store.CorrelationIndex.Remove(match.EventName, match.CorrelationId, _instanceId);

            InstanceLifecycle.TransitionTo(runtime, WorkflowStatus.Running);

            // Continue execution with the resumed event
            var resumeDelegate = _store.GetResumeDelegate(_instanceId);
            await resumeDelegate(instance, envelope, match, cancellationToken);
        }
        finally
        {
            instance.ExecutionLock.Release();
        }
    }

    public IReadOnlyList<WaitRecord> GetActiveWaits()
    {
        var instance = _store.GetUntyped(_instanceId);
        var runtime = instance.RuntimeState;
        return runtime.ActiveWaits
            .Where(w => w.Status == WaitStatus.Active)
            .ToList()
            .AsReadOnly();
    }
}
