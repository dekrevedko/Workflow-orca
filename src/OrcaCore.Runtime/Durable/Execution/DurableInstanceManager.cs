using OrcaCore.Runtime.Durable.Persistence;

namespace OrcaCore.Runtime.Durable.Execution;

internal sealed class DurableInstanceManager(
    IWorkflowStore store,
    DurableInstanceRegistry registry,
    DurableDefinitionRegistry definitions,
    CorrelationIndex correlationIndex)
{
    private readonly System.Collections.Concurrent.ConcurrentDictionary<string, Task<DurableInstanceRegistration>> _pendingLoads =
        new(StringComparer.Ordinal);

    public void Shutdown()
    {
        foreach (var registration in registry.Drain())
            registration.Instance.ExecutionLock.Dispose();
    }

    public bool IsLoaded(string instanceId) => registry.Contains(instanceId);

    public DurableInstanceRegistration GetRegistration(string instanceId) => registry.GetRequired(instanceId);

    public IReadOnlyList<string> GetResidentInstanceIds(string definitionId, string definitionVersion) =>
        registry.Values
            .Where(registration => string.Equals(registration.Instance.DefinitionId, definitionId, StringComparison.Ordinal))
            .Where(registration => string.Equals(registration.DefinitionVersion, definitionVersion, StringComparison.Ordinal))
            .Select(registration => registration.Instance.InstanceId)
            .ToArray();

    public async Task<IWorkflowInstance> EnsureLoadedAsync(
        string instanceId,
        CancellationToken cancellationToken,
        Func<PersistedInstance, IWorkflowInstance> loadFromPersisted)
        => (await EnsureLoadedRegistrationAsync(instanceId, cancellationToken, loadFromPersisted)).Instance;

    public async Task<DurableInstanceRegistration> EnsureLoadedRegistrationAsync(
        string instanceId,
        CancellationToken cancellationToken,
        Func<PersistedInstance, IWorkflowInstance> loadFromPersisted)
    {
        if (registry.TryGet(instanceId, out var existing))
            return existing;

        var loadTask = _pendingLoads.GetOrAdd(
            instanceId,
            _ => LoadRegistrationAsync(instanceId, loadFromPersisted));

        try
        {
            return await loadTask.WaitAsync(cancellationToken);
        }
        finally
        {
            if (loadTask.IsCompleted)
                _pendingLoads.TryRemove(new KeyValuePair<string, Task<DurableInstanceRegistration>>(instanceId, loadTask));
        }
    }

    public async Task RecoverAfterCommitFailureAsync(
        string instanceId,
        CancellationToken cancellationToken,
        Func<PersistedInstance, IWorkflowInstance> loadFromPersisted)
    {
        if (registry.TryGet(instanceId, out var loaded))
        {
            foreach (var wait in loaded.Instance.RuntimeState.ActiveWaits.Where(wait => wait.Status == WaitStatus.Active))
                correlationIndex.Remove(wait.EventName, wait.CorrelationId, instanceId);
        }

        Remove(instanceId);

        var persisted = await store.LoadAsync(instanceId, cancellationToken);
        if (persisted is null)
            return;

        if (persisted.RuntimeState.Status != WorkflowStatus.Waiting
            || !persisted.RuntimeState.ActiveWaits.Any(wait => wait.Status == WaitStatus.Active && wait.Mode == WaitMode.Resident))
            return;

        if (!definitions.TryResolve(persisted, out _))
            return;

        _ = loadFromPersisted(persisted);
    }

    public bool TryRestoreLoadedInstanceFromSnapshot(string instanceId, PersistedInstance? snapshot)
    {
        if (snapshot is null)
            return false;

        if (!registry.TryGet(instanceId, out var registration))
            return false;

        if (!snapshot.RuntimeState.ActiveWaits.Any(wait => wait.Status == WaitStatus.Active && wait.Mode == WaitMode.Resident))
            return false;

        registration.Restore(registration.Instance, snapshot);
        return true;
    }

    public void EvictIfLongWait(string instanceId)
    {
        if (!registry.TryGet(instanceId, out var registration))
            return;

        if (!ShouldEvict(instanceId))
            return;

        foreach (var wait in registration.Instance.RuntimeState.ActiveWaits.Where(wait => wait.Status == WaitStatus.Active))
            correlationIndex.Remove(wait.EventName, wait.CorrelationId, instanceId);

        registry.TryRemove(instanceId, out _);
    }

    public bool ShouldEvict(string instanceId)
    {
        if (!registry.TryGet(instanceId, out var registration))
            return false;

        return registration.Instance.RuntimeState.Status == WorkflowStatus.Waiting
            && registration.Instance.RuntimeState.ActiveWaits.Any(wait =>
                wait.Status == WaitStatus.Active && wait.Mode == WaitMode.Cold);
    }

    public void RemoveAndDispose(string instanceId)
    {
        if (!registry.TryRemove(instanceId, out var registration))
            return;

        foreach (var wait in registration.Instance.RuntimeState.ActiveWaits.Where(wait => wait.Status == WaitStatus.Active))
            correlationIndex.Remove(wait.EventName, wait.CorrelationId, instanceId);
    }

    public void Remove(string instanceId) => registry.TryRemove(instanceId, out _);

    public bool TryGetLoadedInstance(string instanceId, out IWorkflowInstance instance)
    {
        if (registry.TryGet(instanceId, out var registration))
        {
            instance = registration.Instance;
            return true;
        }

        instance = default!;
        return false;
    }

    private async Task<DurableInstanceRegistration> LoadRegistrationAsync(
        string instanceId,
        Func<PersistedInstance, IWorkflowInstance> loadFromPersisted)
    {
        if (registry.TryGet(instanceId, out var existing))
            return existing;

        var persisted = await store.LoadAsync(instanceId, CancellationToken.None)
            ?? throw new KeyNotFoundException($"Durable instance '{instanceId}' not found.");

        definitions.GetRequired(persisted);
        _ = loadFromPersisted(persisted);
        return registry.GetRequired(instanceId);
    }
}
