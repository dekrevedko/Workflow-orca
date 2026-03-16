using System.Collections.Concurrent;
using OrcaCore.Abstractions;

namespace OrcaCore.Runtime;

internal sealed class InMemoryInstanceStore
{
    private readonly ConcurrentDictionary<string, IWorkflowInstance> _instances = new();
    private readonly ConcurrentDictionary<string, object> _definitions = new();
    private readonly ConcurrentDictionary<string, Func<IWorkflowInstance, EventEnvelope, WaitRecord, CancellationToken, Task>> _resumeDelegates = new();

    public CorrelationIndex CorrelationIndex { get; } = new();

    public void RegisterDefinition<TState>(WorkflowDefinition<TState> definition)
    {
        _definitions[definition.DefinitionId] = definition;
    }

    public WorkflowDefinition<TState> GetDefinition<TState>(string definitionId)
    {
        if (!_definitions.TryGetValue(definitionId, out var raw))
            throw new KeyNotFoundException(
                $"Definition '{definitionId}' not found.");

        return (WorkflowDefinition<TState>)raw;
    }

    public void Add<TState>(WorkflowInstance<TState> instance)
    {
        if (!_instances.TryAdd(instance.InstanceId, instance))
            throw new InvalidOperationException(
                $"Instance '{instance.InstanceId}' already exists.");

        // Register a typed resume delegate so untyped callers can resume execution
        var definition = GetDefinition<TState>(instance.DefinitionId);
        _resumeDelegates[instance.InstanceId] = (raw, envelope, matchedWait, ct) =>
            ResumeRouter.ResumeAsync(
                (WorkflowInstance<TState>)raw, definition, CorrelationIndex,
                envelope, matchedWait, ct);
    }

    public WorkflowInstance<TState> Get<TState>(string instanceId)
    {
        if (!_instances.TryGetValue(instanceId, out var raw))
            throw new KeyNotFoundException(
                $"Instance '{instanceId}' not found.");

        return (WorkflowInstance<TState>)raw;
    }

    public IWorkflowInstance GetUntyped(string instanceId)
    {
        if (!_instances.TryGetValue(instanceId, out var instance))
            throw new KeyNotFoundException(
                $"Instance '{instanceId}' not found.");

        return instance;
    }

    public IReadOnlyList<string> GetInstanceIdsByDefinition(string definitionId)
    {
        return _instances.Values
            .Where(i => i.DefinitionId == definitionId)
            .Select(i => i.InstanceId)
            .ToList()
            .AsReadOnly();
    }

    public IEnumerable<WorkflowInstanceSnapshot> GetAllSnapshots()
    {
        return _instances.Values.Select(i =>
            new WorkflowInstanceSnapshot(
                i.InstanceId,
                i.DefinitionId,
                i.RuntimeState.Status,
                i.RuntimeState.CreatedAt,
                i.RuntimeState.LastTransitionAt,
                i.RuntimeState.Error));
    }

    public void DisposeAllLocks()
    {
        foreach (var instance in _instances.Values)
            instance.ExecutionLock.Dispose();
    }

    public Func<IWorkflowInstance, EventEnvelope, WaitRecord, CancellationToken, Task> GetResumeDelegate(string instanceId)
    {
        if (!_resumeDelegates.TryGetValue(instanceId, out var del))
            throw new KeyNotFoundException(
                $"Resume delegate for instance '{instanceId}' not found.");

        return del;
    }
}
