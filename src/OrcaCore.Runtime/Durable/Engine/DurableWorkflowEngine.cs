using System.Text.Json;
using System.Threading;
using OrcaCore.Runtime.Durable.Persistence;

namespace OrcaCore.Runtime.Durable.Engine;

public sealed class DurableWorkflowEngine : IAsyncDisposable
{
    private readonly IWorkflowStore _store;
    private readonly DurableWorkflowEngineOptions _options;
    private readonly DurableOutboxPump _outboxPump;
    private readonly DurableInstanceManager _instanceManager;
    private readonly DurableDefinitionRegistry _definitions;
    private readonly DurableEventRouter _eventRouter;
    private readonly CancellationTokenSource _disposeCts = new();
    private readonly DurableInstanceRegistry _registry = new();
    private Task? _outboxPumpTask;
    private Task? _disposeTask;

    private DurableWorkflowEngine(IWorkflowStore store, DurableWorkflowEngineOptions options)
    {
        _store = store;
        _options = options;
        _outboxPump = new DurableOutboxPump(store, options);
        _definitions = new DurableDefinitionRegistry(store);
        _instanceManager = new DurableInstanceManager(store, _registry, _definitions, CorrelationIndex);
        _eventRouter = new DurableEventRouter(store, options, _instanceManager, CorrelationIndex);
    }

    internal CorrelationIndex CorrelationIndex { get; } = new();

    public static DurableWorkflowEngine Create(
        IWorkflowStore store,
        DurableWorkflowEngineOptions? options = null,
        CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        ArgumentNullException.ThrowIfNull(store);
        var engine = new DurableWorkflowEngine(store, options ?? new DurableWorkflowEngineOptions());
        engine.StartOutboxPumpIfConfigured();
        return engine;
    }

    public async Task<DurableWorkflowEngine<TState>> ForDefinitionAsync<TState>(
        DurableWorkflowDefinition<TState> definition,
        CancellationToken cancellationToken = default)
    {
        await RegisterDefinitionAsync(definition, cancellationToken);
        return new DurableWorkflowEngine<TState>(this, definition);
    }

    public DurableInstanceScope Instance(string instanceId) => new(this, instanceId);

    public DurableSelectionScope All() => new(this);

    public DurableSelectionScope Where(System.Linq.Expressions.Expression<Func<DurableInstanceSnapshot, bool>> predicate) =>
        new(this, predicate);

    public async Task RaiseEvent(EventEnvelope envelope, CancellationToken cancellationToken = default)
        => await _eventRouter.RouteAsync(envelope, cancellationToken, LoadFromPersisted);

    public Task<OutboxDispatchResult> DispatchPendingOutboxAsync(
        IOutboxDispatcher dispatcher,
        CancellationToken cancellationToken = default)
        => _outboxPump.DispatchPendingAsync(dispatcher, cancellationToken);

    public ValueTask DisposeAsync()
    {
        var disposeTask = Volatile.Read(ref _disposeTask);
        if (disposeTask is not null)
            return new ValueTask(disposeTask);

        var created = DisposeCoreAsync();
        disposeTask = Interlocked.CompareExchange(ref _disposeTask, created, null);
        return new ValueTask(disposeTask ?? created);
    }

    private async Task DisposeCoreAsync()
    {
        _disposeCts.Cancel();

        if (_outboxPumpTask is not null)
        {
            try
            {
                await _outboxPumpTask;
            }
            catch (OperationCanceledException)
            {
                // expected on dispose
            }
        }

        _instanceManager.Shutdown();

        _disposeCts.Dispose();
    }

    internal async Task<DurableInstanceSnapshot> GetSnapshotAsync(string instanceId, CancellationToken cancellationToken)
    {
        var instance = await EnsureLoadedAsync(instanceId, cancellationToken);
        return _instanceManager.GetRegistration(instanceId).Snapshot(instance);
    }

    internal async Task<TState> GetStateAsync<TState>(string instanceId, CancellationToken cancellationToken)
    {
        var instance = await EnsureLoadedAsync(instanceId, cancellationToken);
        var snapshot = _instanceManager.GetRegistration(instanceId).StateSnapshot(instance);
        if (snapshot is not TState typed)
        {
            throw new InvalidOperationException(
                $"Durable instance '{instanceId}' state cannot be viewed as '{typeof(TState).FullName}'.");
        }

        return typed;
    }

    internal async Task<IReadOnlyList<WaitRecord>> GetActiveWaitsAsync(string instanceId, CancellationToken cancellationToken)
    {
        var instance = await EnsureLoadedAsync(instanceId, cancellationToken);
        return instance.RuntimeState.ActiveWaits
            .Where(w => w.Status == WaitStatus.Active)
            .ToList()
            .AsReadOnly();
    }

    internal async Task RaiseEventToInstanceAsync(string instanceId, EventEnvelope envelope, CancellationToken cancellationToken)
        => await _eventRouter.RaiseEventToInstanceAsync(instanceId, envelope, cancellationToken, LoadFromPersisted);

    internal async Task DeleteInstanceAsync(string instanceId, CancellationToken cancellationToken)
    {
        if (_instanceManager.TryGetLoadedInstance(instanceId, out var loaded))
        {
            await loaded.ExecutionLock.WaitAsync(cancellationToken);
            try
            {
                await _store.DeleteAsync(instanceId, cancellationToken);
                _instanceManager.RemoveAndDispose(instanceId);
            }
            finally
            {
                loaded.ExecutionLock.Release();
            }

            return;
        }

        await _store.DeleteAsync(instanceId, cancellationToken);
        _instanceManager.RemoveAndDispose(instanceId);
    }

    internal Task PurgeInstanceArtifactsAsync(string instanceId, DateTimeOffset olderThan, CancellationToken cancellationToken) =>
        _store.PurgeArtifactsAsync(instanceId, olderThan, cancellationToken);

    internal Task PurgeInstanceArtifactsAsync(string instanceId, DurableArtifactRetentionPolicy policy, CancellationToken cancellationToken) =>
        _store.PurgeArtifactsAsync(instanceId, policy.Resolve(DateTimeOffset.UtcNow), cancellationToken);

    internal async Task DeleteSelectionAsync(
        Func<DurableInstanceSnapshot, bool>? predicate,
        CancellationToken cancellationToken)
    {
        var snapshots = await QuerySnapshotsAsync(cancellationToken);
        if (predicate is not null)
            snapshots = snapshots.Where(predicate).ToArray();

        foreach (var snapshot in snapshots)
            await DeleteInstanceAsync(snapshot.InstanceId, cancellationToken);
    }

    internal async Task PurgeSelectionArtifactsAsync(
        Func<DurableInstanceSnapshot, bool>? predicate,
        DateTimeOffset olderThan,
        CancellationToken cancellationToken)
    {
        var snapshots = await QuerySnapshotsAsync(cancellationToken);
        if (predicate is not null)
            snapshots = snapshots.Where(predicate).ToArray();

        foreach (var snapshot in snapshots)
            await PurgeInstanceArtifactsAsync(snapshot.InstanceId, olderThan, cancellationToken);
    }

    internal async Task PurgeSelectionArtifactsAsync(
        Func<DurableInstanceSnapshot, bool>? predicate,
        DurableArtifactRetentionPolicy policy,
        CancellationToken cancellationToken)
    {
        var snapshots = await QuerySnapshotsAsync(cancellationToken);
        if (predicate is not null)
            snapshots = snapshots.Where(predicate).ToArray();

        var cutoffs = policy.Resolve(DateTimeOffset.UtcNow);
        foreach (var snapshot in snapshots)
            await _store.PurgeArtifactsAsync(snapshot.InstanceId, cutoffs, cancellationToken);
    }

    internal async Task RegisterDefinitionAsync<TState>(
        DurableWorkflowDefinition<TState> definition,
        CancellationToken cancellationToken)
    {
        var waiting = await _definitions.RegisterAsync(
            definition,
            cancellationToken,
            persisted => LoadTypedInstance(persisted, definition));

        foreach (var persisted in waiting)
        {
            if (_instanceManager.IsLoaded(persisted.InstanceId))
                continue;

            var hasResidentWait = persisted.RuntimeState.ActiveWaits.Any(wait =>
                wait.Status == WaitStatus.Active && wait.Mode == WaitMode.Resident);

            if (!hasResidentWait)
                continue;

            var instance = StateMapper.FromPersistedState(persisted, definition.InnerDefinition);
            RegisterInstance(instance, definition, persisted.ConcurrencyToken, rebuildCorrelation: true);
        }
    }

    internal async Task<DurableInstanceSnapshot> StartAsync<TState>(
        DurableWorkflowDefinition<TState> definition,
        TState initialState,
        CancellationToken cancellationToken)
    {
        var instanceId = Guid.NewGuid().ToString("N");
        var instance = new WorkflowInstance<TState>(instanceId, definition.DefinitionId, initialState);
        RegisterInstance(instance, definition, 0, rebuildCorrelation: false);
        var evictAfterRelease = false;
        DurableInstanceSnapshot? snapshot = null;

        await instance.ExecutionLock.WaitAsync(cancellationToken);
        try
        {
            var stagedCorrelation = new StagedCorrelationMutationSink();
            await WorkflowRuntime.ExecuteAsync(
                instance,
                definition.InnerDefinition,
                stagedCorrelation,
                cancellationToken,
                durableMode: true);

            var persisted = StateMapper.ToPersistedState(instance, definition.DefinitionVersion, concurrencyToken: 0, payloadTypeResolver: _options.PayloadTypeResolver);
            var outboxRecords = DurableEventRouter.CreateTransitionOutboxRecords(instanceId, instance.RuntimeState, trigger: "Start");
            var historyRecords = new List<HistoryRecord>
            {
                new("Started", DateTimeOffset.UtcNow, definition.DefinitionId)
            };
            historyRecords.AddRange(DurableEventRouter.CreateTransitionHistoryRecords(instance.RuntimeState, "Start"));
            await _store.CreateAsync(persisted, outboxRecords, historyRecords, cancellationToken);
            stagedCorrelation.ApplyTo(CorrelationIndex);
            instance.ConcurrencyToken = persisted.ConcurrencyToken;
            snapshot = CreateSnapshot(instance, definition.DefinitionVersion);
            evictAfterRelease = _instanceManager.ShouldEvict(instanceId);
        }
        catch
        {
            _instanceManager.Remove(instanceId);
            throw;
        }
        finally
        {
            instance.ExecutionLock.Release();
        }

        if (evictAfterRelease)
            _instanceManager.EvictIfLongWait(instanceId);

        return snapshot!;
    }

    internal async Task<IReadOnlyList<string>> GetInstanceIdsForDefinitionAsync(
        string definitionId,
        string definitionVersion,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var residentIds = _instanceManager.GetResidentInstanceIds(definitionId, definitionVersion);

        var persistedIds = await LoadInstanceIdsFromStoreAsync(definitionId, definitionVersion, cancellationToken);
        return residentIds
            .Concat(persistedIds)
            .Distinct(StringComparer.Ordinal)
            .ToArray();
    }

    private void RegisterInstance<TState>(
        WorkflowInstance<TState> instance,
        DurableWorkflowDefinition<TState> definition,
        int concurrencyToken,
        bool rebuildCorrelation)
    {
        instance.ConcurrencyToken = concurrencyToken;
        Func<IWorkflowInstance, PersistedInstance> persist = raw =>
            StateMapper.ToPersistedState((WorkflowInstance<TState>)raw, definition.DefinitionVersion, raw.ConcurrencyToken, payloadTypeResolver: _options.PayloadTypeResolver);
        Func<IWorkflowInstance, DurableInstanceSnapshot> snapshot = raw => CreateSnapshot((WorkflowInstance<TState>)raw, definition.DefinitionVersion);
        Func<IWorkflowInstance, object> state = raw => CloneStateSnapshot(((WorkflowInstance<TState>)raw).BusinessState)!;
        Action<IWorkflowInstance, PersistedInstance> restore = (raw, persisted) =>
        {
            var restored = StateMapper.FromPersistedState(persisted, definition.InnerDefinition, payloadTypeResolver: _options.PayloadTypeResolver);
            ((WorkflowInstance<TState>)raw).RestoreFrom(restored);
        };
        Func<IWorkflowInstance, EventEnvelope, WaitRecord, ICorrelationMutationSink, CancellationToken, Task<WorkflowExecutionReport>> resume = (raw, envelope, matchedWait, correlationSink, ct) =>
        {
            if (matchedWait.BranchId is not null)
            {
                return WorkflowRuntime.ResumeParallelBranchAsync(
                    (WorkflowInstance<TState>)raw,
                    definition.InnerDefinition,
                    correlationSink,
                    matchedWait.BranchId,
                    envelope,
                    ct,
                    durableMode: true);
            }

            return WorkflowRuntime.ExecuteAsync(
                (WorkflowInstance<TState>)raw,
                definition.InnerDefinition,
                correlationSink,
                ct,
                envelope,
                durableMode: true);
        };
        _registry.Set(instance.InstanceId, new DurableInstanceRegistration(
            instance,
            definition.DefinitionVersion,
            persist,
            snapshot,
            state,
            restore,
            resume));

        if (rebuildCorrelation)
        {
            foreach (var wait in instance.RuntimeState.ActiveWaits.Where(w => w.Status == WaitStatus.Active))
                CorrelationIndex.Add(wait.EventName, wait.CorrelationId, instance.InstanceId);
        }
    }

    private Task<IWorkflowInstance> EnsureLoadedAsync(string instanceId, CancellationToken cancellationToken)
        => _instanceManager.EnsureLoadedAsync(instanceId, cancellationToken, LoadFromPersisted);

    private IWorkflowInstance LoadFromPersisted(PersistedInstance persisted)
    {
        var definition = _definitions.GetRequired(persisted);
        return definition.Load(persisted);
    }

    private IWorkflowInstance LoadTypedInstance<TState>(
        PersistedInstance persisted,
        DurableWorkflowDefinition<TState> definition)
    {
        var instance = StateMapper.FromPersistedState(persisted, definition.InnerDefinition, payloadTypeResolver: _options.PayloadTypeResolver);
        RegisterInstance(instance, definition, persisted.ConcurrencyToken, rebuildCorrelation: true);
        return instance;
    }

    private static TState CloneStateSnapshot<TState>(TState state)
    {
        if (state is ICloneable cloneable)
            return (TState)cloneable.Clone();

        var element = JsonSerializer.SerializeToElement(state, typeof(TState));
        return element.Deserialize<TState>()!;
    }

    private async Task<IReadOnlyList<string>> LoadInstanceIdsFromStoreAsync(
        string definitionId,
        string definitionVersion,
        CancellationToken cancellationToken)
    {
        var matches = await _store.QueryAsync(
            definitionId: definitionId,
            definitionVersion: definitionVersion,
            ct: cancellationToken);

        return matches.Select(x => x.InstanceId).ToArray();
    }

    internal bool IsInstanceLoaded(string instanceId) => _instanceManager.IsLoaded(instanceId);

    private void StartOutboxPumpIfConfigured()
    {
        if (!_options.AutoDispatchOutbox || _options.OutboxDispatcher is null)
            return;

        _outboxPumpTask = _outboxPump.StartIfConfigured(_disposeCts.Token);
    }

    internal async Task<IReadOnlyList<DurableInstanceSnapshot>> QuerySnapshotsAsync(CancellationToken cancellationToken)
    {
        var persisted = await _store.QueryAsync(ct: cancellationToken);
        return persisted
            .Select(CreateSnapshot)
            .OrderBy(x => x.InstanceId, StringComparer.Ordinal)
            .ToArray();
    }

    private static DurableInstanceSnapshot CreateSnapshot<TState>(
        WorkflowInstance<TState> instance,
        string definitionVersion)
    {
        return new DurableInstanceSnapshot(
            instance.InstanceId,
            instance.DefinitionId,
            definitionVersion,
            instance.RuntimeState.Status,
            instance.RuntimeState.CreatedAt,
            instance.RuntimeState.LastTransitionAt,
            instance.RuntimeState.ActiveWaits.Count(w => w.Status == WaitStatus.Active),
            instance.ConcurrencyToken,
            instance.RuntimeState.Error);
    }

    private static DurableInstanceSnapshot CreateSnapshot(PersistedInstance instance)
    {
        return new DurableInstanceSnapshot(
            instance.InstanceId,
            instance.DefinitionId,
            instance.DefinitionVersion ?? string.Empty,
            instance.RuntimeState.Status,
            instance.RuntimeState.CreatedAt,
            instance.RuntimeState.LastTransitionAt,
            instance.RuntimeState.ActiveWaits.Count(w => w.Status == WaitStatus.Active),
            instance.ConcurrencyToken,
            instance.RuntimeState.Error is null
                ? null
                : WorkflowError.FromMetadata(
                    instance.RuntimeState.Error.Message,
                    instance.RuntimeState.Error.ExceptionType,
                    instance.RuntimeState.Error.StepId,
                    instance.RuntimeState.Error.Timestamp));
    }
}

public sealed class DurableWorkflowEngine<TState>
{
    private readonly DurableWorkflowEngine _engine;
    private readonly DurableWorkflowDefinition<TState> _definition;

    internal DurableWorkflowEngine(DurableWorkflowEngine engine, DurableWorkflowDefinition<TState> definition)
    {
        _engine = engine;
        _definition = definition;
    }

    public Task<DurableInstanceSnapshot> Start(TState initialState, CancellationToken cancellationToken = default) =>
        _engine.StartAsync(_definition, initialState, cancellationToken);

    public async Task<FanoutDispatchResult> RaiseEvent(EventEnvelope envelope, CancellationToken cancellationToken = default)
    {
        var ids = await _engine.GetInstanceIdsForDefinitionAsync(_definition.DefinitionId, _definition.DefinitionVersion, cancellationToken);
        var results = new List<FanoutInstanceResult>(ids.Count);

        foreach (var id in ids)
        {
            try
            {
                await _engine.RaiseEventToInstanceAsync(id, envelope, cancellationToken);
                results.Add(new FanoutInstanceResult(id, Succeeded: true, Error: null));
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                results.Add(new FanoutInstanceResult(id, Succeeded: false, Error: ex));
            }
        }

        return new FanoutDispatchResult(
            ids.Count,
            results.Count(x => x.Succeeded),
            results.Count(x => !x.Succeeded),
            results);
    }

    public DurableSelectionScope All()
    {
        return _engine.Where(x =>
            x.DefinitionId == _definition.DefinitionId
            && x.DefinitionVersion == _definition.DefinitionVersion);
    }

    public DurableSelectionScope Where(System.Linq.Expressions.Expression<Func<DurableInstanceSnapshot, bool>> predicate)
    {
        return _engine.Where(
            DefinitionScopedQueryBuilder.ForDefinition(
                predicate,
                (nameof(DurableInstanceSnapshot.DefinitionId), _definition.DefinitionId),
                (nameof(DurableInstanceSnapshot.DefinitionVersion), _definition.DefinitionVersion)));
    }
}
