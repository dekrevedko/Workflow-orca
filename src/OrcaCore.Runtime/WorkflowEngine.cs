using OrcaCore.Abstractions;

namespace OrcaCore.Runtime;

public sealed class WorkflowEngine : IAsyncDisposable
{
    internal InMemoryInstanceStore Store { get; } = new();

    public WorkflowEngine<TState> ForDefinition<TState>(WorkflowDefinition<TState> definition)
    {
        Store.RegisterDefinition(definition);
        return new WorkflowEngine<TState>(this, definition, Store);
    }

    public WorkflowEngine<TState> ForDefinition<TState>(string definitionId)
    {
        var definition = Store.GetDefinition<TState>(definitionId);
        return new WorkflowEngine<TState>(this, definition, Store);
    }

    public InstanceScope Instance(string instanceId)
    {
        return new InstanceScope(Store, instanceId);
    }

    public SelectionScope All() => new(Store);

    public SelectionScope Where(System.Linq.Expressions.Expression<Func<WorkflowInstanceSnapshot, bool>> predicate) => new(Store, predicate);

    /// <summary>
    /// Correlation-targeted routing: resolves instance by (EventName, CorrelationId).
    /// Must resolve to exactly one instance.
    /// </summary>
    public async Task RaiseEvent(EventEnvelope envelope, CancellationToken cancellationToken = default)
    {
        var instanceId = Store.CorrelationIndex.ResolveExactlyOne(envelope.EventName, envelope.CorrelationId);
        await Instance(instanceId).RaiseEvent(envelope, cancellationToken);
    }

    public ValueTask DisposeAsync()
    {
        Store.DisposeAllLocks();
        return ValueTask.CompletedTask;
    }
}

public sealed class WorkflowEngine<TState>
{
    private readonly WorkflowEngine _engine;
    private readonly WorkflowDefinition<TState> _definition;
    private readonly InMemoryInstanceStore _store;

    internal WorkflowEngine(WorkflowEngine engine, WorkflowDefinition<TState> definition, InMemoryInstanceStore store)
    {
        _engine = engine;
        _definition = definition;
        _store = store;
    }

    public async Task<WorkflowInstanceSnapshot> Start(TState initialState, CancellationToken cancellationToken = default)
    {
        var instanceId = Guid.NewGuid().ToString("N");
        var instance = new WorkflowInstance<TState>(instanceId, _definition.DefinitionId, initialState);
        _store.Add(instance);

        await instance.ExecutionLock.WaitAsync(cancellationToken);
        try
        {
            await WorkflowRuntime.ExecuteAsync(instance, _definition, _store.CorrelationIndex, cancellationToken);
        }
        finally
        {
            instance.ExecutionLock.Release();
        }

        return new WorkflowInstanceSnapshot(
            instance.InstanceId,
            instance.DefinitionId,
            instance.RuntimeState.Status,
            instance.RuntimeState.CreatedAt,
            instance.RuntimeState.LastTransitionAt,
            instance.RuntimeState.Error);
    }

    /// <summary>
    /// Definition-targeted fanout: delivers event to all instances of this definition.
    /// </summary>
    public async Task RaiseEvent(EventEnvelope envelope, CancellationToken cancellationToken = default)
    {
        var instanceIds = _store.GetInstanceIdsByDefinition(_definition.DefinitionId);
        foreach (var instanceId in instanceIds)
        {
            // Each instance gets its own EventId-scoped delivery to avoid dedup conflicts
            await _engine.Instance(instanceId).RaiseEvent(envelope, cancellationToken);
        }
    }
}
