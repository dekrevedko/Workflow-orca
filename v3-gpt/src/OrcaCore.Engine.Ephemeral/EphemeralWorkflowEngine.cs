using System.Collections.Concurrent;
using OrcaCore.Abstractions.Events;
using OrcaCore.Abstractions.Errors;
using OrcaCore.Abstractions.Ids;
using OrcaCore.Abstractions.Instances;
using OrcaCore.Abstractions.Steps;
using OrcaCore.Core.Definitions;
using OrcaCore.Core.Lifecycle;
using OrcaCore.Engine.Ephemeral.Execution;
using OrcaCore.Engine.Ephemeral.Governance;
using OrcaCore.Engine.Ephemeral.Timers;

namespace OrcaCore.Engine.Ephemeral;

/// <summary>
/// Executes registered workflow definitions in the current process without durable recovery.
/// </summary>
public sealed class EphemeralWorkflowEngine
{
    private readonly ConcurrentDictionary<DefinitionId, object> definitions = [];
    private readonly ConcurrentDictionary<InstanceId, object> sagaRuntimeStates = [];
    private readonly InstanceExecutionLane executionLane;
    private readonly ResourceGovernanceCoordinator governance;
    private readonly IInstanceRegistry instanceRegistry;
    private readonly EphemeralWorkflowEngineOptions options;
    private readonly EphemeralRoutingIndex routingIndex = new();
    private readonly EphemeralTimerService timerService;
    private readonly TimeProvider timeProvider;
    private readonly YieldContinuationScheduler yieldContinuationScheduler;
    private readonly InterpreterFactory interpreterFactory;

    /// <summary>
    /// Initializes an engine using system time and an in-memory instance registry.
    /// </summary>
    public EphemeralWorkflowEngine()
        : this(TimeProvider.System)
    {
    }

    /// <summary>
    /// Initializes an engine using the supplied time provider and an in-memory instance registry.
    /// </summary>
    public EphemeralWorkflowEngine(TimeProvider timeProvider)
        : this(timeProvider, new EphemeralWorkflowEngineOptions())
    {
    }

    /// <summary>
    /// Initializes an engine using the supplied time provider, options, and an in-memory instance registry.
    /// </summary>
    public EphemeralWorkflowEngine(
        TimeProvider timeProvider,
        EphemeralWorkflowEngineOptions options)
        : this(timeProvider, options, new InMemoryInstanceRegistry(), CreateExecutionLane(options))
    {
    }

    internal EphemeralWorkflowEngine(
        TimeProvider timeProvider,
        IInstanceRegistry instanceRegistry,
        InstanceExecutionLane executionLane)
        : this(timeProvider, new EphemeralWorkflowEngineOptions(), instanceRegistry, executionLane)
    {
    }

    private EphemeralWorkflowEngine(
        TimeProvider timeProvider,
        EphemeralWorkflowEngineOptions options,
        IInstanceRegistry instanceRegistry,
        InstanceExecutionLane executionLane)
    {
        ArgumentNullException.ThrowIfNull(timeProvider);
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(instanceRegistry);
        ArgumentNullException.ThrowIfNull(executionLane);

        this.timeProvider = timeProvider;
        this.options = options;
        this.instanceRegistry = instanceRegistry;
        this.executionLane = executionLane;
        governance = new ResourceGovernanceCoordinator(options);
        timerService = new EphemeralTimerService(timeProvider);
        yieldContinuationScheduler = new YieldContinuationScheduler(governance, executionLane);
        interpreterFactory = new InterpreterFactory(
            timeProvider,
            timerService,
            governance,
            yieldContinuationScheduler,
            options.StuckStepThreshold);
        Management = new EphemeralManagement(this, instanceRegistry);
    }

    private static InstanceExecutionLane CreateExecutionLane(EphemeralWorkflowEngineOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        return new InstanceExecutionLane(options.LaneWorkItemEnqueued);
    }

    /// <summary>
    /// Gets the management query entry point for ephemeral instances.
    /// </summary>
    public EphemeralManagement Management { get; }

    /// <summary>
    /// Registers a workflow definition version for later starts.
    /// </summary>
    public void RegisterDefinition<TState>(WorkflowDefinition<TState> definition)
    {
        ArgumentNullException.ThrowIfNull(definition);

        definitions[definition.DefinitionId] = definition;
    }

    /// <summary>
    /// Starts a registered workflow and runs it inline to suspension or terminal status.
    /// </summary>
    public async Task<WorkflowInstanceSnapshot> StartAsync<TInput, TState>(
        DefinitionId definitionId,
        TInput input,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (!definitions.TryGetValue(definitionId, out var registeredDefinition))
        {
            throw new WorkflowDefinitionException(
                $"No workflow definition is registered for definition id '{definitionId}'.");
        }

        if (registeredDefinition is not WorkflowDefinition<TState> definition)
        {
            throw new WorkflowDefinitionException(
                $"Workflow definition '{definitionId}' was not registered for state type '{typeof(TState).Name}'.");
        }

        var instanceId = InstanceId.New();
        WorkflowInstance<TState>? instance = null;
        WorkflowInstanceSnapshot snapshot;
        await using (await governance.EnterAdvancementAsync(cancellationToken).ConfigureAwait(false))
        {
            snapshot = await executionLane.RunAsync(
                instanceId,
                async laneCancellationToken =>
                {
                    var interpreter = interpreterFactory.Create<TState>();
                    instance = await interpreter.RunAsync(
                        definition,
                        input,
                        instanceId,
                        laneCancellationToken).ConfigureAwait(false);
                    instanceRegistry.Save(instance);

                    return instance.ToSnapshot();
                },
                cancellationToken).ConfigureAwait(false);
        }

        snapshot = instance is null
            ? snapshot
            : await yieldContinuationScheduler
                .DrainAsync(instance, instanceId, IndexSnapshot, cancellationToken)
                .ConfigureAwait(false);
        IndexSnapshot(snapshot);
        return snapshot;
    }

    internal DateTimeOffset GetUtcNow()
    {
        return timeProvider.GetUtcNow();
    }

    /// <summary>
    /// Starts a short-running workflow and returns its terminal snapshot.
    /// </summary>
    public async Task<WorkflowInstanceSnapshot> AwaitCompletionAsync<TInput, TState>(
        DefinitionId definitionId,
        TInput input,
        CancellationToken cancellationToken)
    {
        var snapshot = await StartAsync<TInput, TState>(
            definitionId,
            input,
            cancellationToken).ConfigureAwait(false);

        if (!LifecycleMachine.TerminalStatuses.Contains(snapshot.Status))
        {
            throw new WorkflowLifecycleException(
                $"Workflow instance '{snapshot.InstanceId}' did not reach a terminal status synchronously.");
        }

        return snapshot;
    }

    /// <summary>
    /// Starts an ephemeral saga in-process only. This reduced-guarantee mode has no durable recovery,
    /// no durable compensation audit, and no post-restart operator remediation.
    /// </summary>
    public async Task<WorkflowInstanceSnapshot> StartSagaAsync<TInput, TState>(
        SagaDefinition<TState> definition,
        TInput input,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(definition);
        cancellationToken.ThrowIfCancellationRequested();

        var instanceId = InstanceId.New();
        WorkflowInstanceSnapshot snapshot;
        await using (await governance.EnterAdvancementAsync(cancellationToken).ConfigureAwait(false))
        {
            snapshot = await executionLane.RunAsync(
                instanceId,
                async laneCancellationToken =>
                {
                    var state = definition.CreateState(input);
                    var instance = new WorkflowInstance<TState>(
                        instanceId,
                        definition.DefinitionId,
                        definition.DefinitionVersion,
                        state,
                        timeProvider.GetUtcNow());
                    var runtime = new EphemeralSagaRuntimeState<TState>(definition, instance);
                    sagaRuntimeStates[instanceId] = runtime;
                    instanceRegistry.Save(instance);

                    foreach (var action in definition.ForwardActions)
                    {
                        var failure = await ExecuteSagaStepAsync(
                            instance,
                            action.StepFactory,
                            action.ActionKey,
                            laneCancellationToken).ConfigureAwait(false);
                        if (failure is not null)
                        {
                            instance.RecordLifecycleEvent(
                                "SagaForwardActionFailed",
                                action.ActionKey,
                                instance.Status,
                                timeProvider.GetUtcNow());
                            return await CompensateSagaRuntimeAsync(
                                runtime,
                                failure,
                                laneCancellationToken).ConfigureAwait(false);
                        }

                        runtime.CompletedActions.Add(action);
                    }

                    instance.Complete(null, timeProvider.GetUtcNow());
                    return instance.ToSnapshot();
                },
                cancellationToken).ConfigureAwait(false);
        }

        return snapshot;
    }

    /// <summary>
    /// Requests compensation for an ephemeral saga instance in-process only. This reduced-guarantee mode has
    /// no durable recovery, no durable compensation audit, and no post-restart operator remediation.
    /// </summary>
    public async Task<WorkflowInstanceSnapshot> RequestSagaCompensationAsync<TState>(
        InstanceId instanceId,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (!sagaRuntimeStates.TryGetValue(instanceId, out var registeredRuntime))
        {
            throw new WorkflowRoutingException(
                $"No ephemeral saga instance exists for instance id '{instanceId}'.");
        }

        if (registeredRuntime is not EphemeralSagaRuntimeState<TState> runtime)
        {
            throw new WorkflowDefinitionException(
                $"Saga instance '{instanceId}' is not using state type '{typeof(TState).Name}'.");
        }

        await using (await governance.EnterAdvancementAsync(cancellationToken).ConfigureAwait(false))
        {
            return await executionLane.RunAsync(
                instanceId,
                laneCancellationToken => CompensateSagaRuntimeAsync(runtime, null, laneCancellationToken),
                cancellationToken).ConfigureAwait(false);
        }
    }

    /// <summary>
    /// Fires all transient timers whose due time has passed in this process.
    /// </summary>
    public async Task<IReadOnlyList<WorkflowInstanceSnapshot>> FireDueTimersAsync(
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var dueTimers = timerService.ClaimDueTimers();
        if (dueTimers.Count == 0)
        {
            return [];
        }

        var snapshots = new List<WorkflowInstanceSnapshot>(dueTimers.Count);
        foreach (var timer in dueTimers)
        {
            WorkflowInstanceSnapshot snapshot;
            instanceRegistry.TryGet(timer.InstanceId, out var registeredInstance);
            await using (await governance.EnterAdvancementAsync(cancellationToken).ConfigureAwait(false))
            {
                snapshot = await executionLane.RunAsync(
                    timer.InstanceId,
                    async laneCancellationToken =>
                    {
                        if (registeredInstance is IWorkflowInstance instanceForToken)
                        {
                            using var linkedCancellation = instanceForToken.CreateLinkedExecutionToken(laneCancellationToken);
                            return await timer.FireAsync(linkedCancellation.Token).ConfigureAwait(false);
                        }

                        return await timer.FireAsync(laneCancellationToken).ConfigureAwait(false);
                    },
                    cancellationToken).ConfigureAwait(false);
            }

            if (registeredInstance is IWorkflowInstance instance)
            {
                snapshot = await yieldContinuationScheduler
                    .DrainAsync(instance, timer.InstanceId, IndexSnapshot, cancellationToken)
                    .ConfigureAwait(false);
            }

            IndexSnapshot(snapshot);
            snapshots.Add(snapshot);
        }

        return snapshots;
    }

    /// <summary>
    /// Delivers an event directly to one known instance and resumes it when an active wait matches.
    /// </summary>
    public async Task<WorkflowInstanceSnapshot> RaiseEventAsync<TState>(
        InstanceId instanceId,
        EventEnvelope envelope,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(envelope);
        cancellationToken.ThrowIfCancellationRequested();

        if (!instanceRegistry.TryGet(instanceId, out var registeredInstance))
        {
            throw new WorkflowRoutingException(
                $"No workflow instance exists for instance id '{instanceId}'.");
        }

        if (registeredInstance is not WorkflowInstance<TState> instance)
        {
            throw new WorkflowDefinitionException(
                $"Workflow instance '{instanceId}' is not using state type '{typeof(TState).Name}'.");
        }

        WorkflowInstanceSnapshot snapshot;
        await using (await governance.EnterAdvancementAsync(cancellationToken).ConfigureAwait(false))
        {
            snapshot = await executionLane.RunAsync(
                instanceId,
                async laneCancellationToken =>
                {
                    using var linkedCancellation = instance.CreateLinkedExecutionToken(laneCancellationToken);
                    return await instance.RaiseEventAsync(envelope, linkedCancellation.Token).ConfigureAwait(false);
                },
                cancellationToken).ConfigureAwait(false);
        }

        snapshot = await yieldContinuationScheduler
            .DrainAsync(instance, instanceId, IndexSnapshot, cancellationToken)
            .ConfigureAwait(false);
        IndexSnapshot(snapshot);
        return snapshot;
    }

    internal async Task<WorkflowInstanceSnapshot> CancelInstanceAsync(
        InstanceId instanceId,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (!instanceRegistry.TryGet(instanceId, out var registeredInstance) ||
            registeredInstance is not IWorkflowInstance instance)
        {
            throw new WorkflowRoutingException(
                $"No workflow instance exists for instance id '{instanceId}'.");
        }

        instance.SignalCancellation();
        WorkflowInstanceSnapshot snapshot;
        await using (await governance.EnterAdvancementAsync(cancellationToken).ConfigureAwait(false))
        {
            snapshot = await executionLane.RunAsync(
                instanceId,
                _ => Task.FromResult(instance.Cancel(timeProvider.GetUtcNow())),
                cancellationToken).ConfigureAwait(false);
        }

        IndexSnapshot(snapshot);
        return snapshot;
    }

    internal async Task<WorkflowInstanceSnapshot> TerminateInstanceAsync(
        InstanceId instanceId,
        CancellationToken cancellationToken)
    {
        return await RunTerminalCommandAsync(
            instanceId,
            instance => instance.Terminate(timeProvider.GetUtcNow()),
            cancellationToken).ConfigureAwait(false);
    }

    private async Task<WorkflowInstanceSnapshot> RunTerminalCommandAsync(
        InstanceId instanceId,
        Func<IWorkflowInstance, WorkflowInstanceSnapshot> command,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (!instanceRegistry.TryGet(instanceId, out var registeredInstance) ||
            registeredInstance is not IWorkflowInstance instance)
        {
            throw new WorkflowRoutingException(
                $"No workflow instance exists for instance id '{instanceId}'.");
        }

        await using (await governance.EnterAdvancementAsync(cancellationToken).ConfigureAwait(false))
        {
            var snapshot = await executionLane.RunAsync(
                instanceId,
                _ => Task.FromResult(command(instance)),
                cancellationToken).ConfigureAwait(false);
            IndexSnapshot(snapshot);
            return snapshot;
        }
    }

    private async Task<WorkflowInstanceSnapshot> CompensateSagaRuntimeAsync<TState>(
        EphemeralSagaRuntimeState<TState> runtime,
        Exception? triggeringFailure,
        CancellationToken cancellationToken)
    {
        if (runtime.CompensationRequested)
        {
            return runtime.Instance.ToSnapshot();
        }

        runtime.CompensationRequested = true;
        if (runtime.CompletedActions.Count == 0)
        {
            if (triggeringFailure is not null)
            {
                runtime.Instance.Fail(ToWorkflowError(triggeringFailure, "saga", timeProvider.GetUtcNow()));
            }

            return runtime.Instance.ToSnapshot();
        }

        foreach (var action in runtime.CompletedActions.AsEnumerable().Reverse())
        {
            if (action.Compensation is null)
            {
                continue;
            }

            var failure = await ExecuteSagaStepAsync(
                runtime.Instance,
                action.Compensation.StepFactory,
                $"{action.ActionKey}/compensation",
                cancellationToken).ConfigureAwait(false);
            if (failure is not null)
            {
                runtime.Instance.FailCompensation(ToWorkflowError(
                    failure,
                    $"{action.ActionKey}/compensation",
                    timeProvider.GetUtcNow()));
                return runtime.Instance.ToSnapshot();
            }
        }

        runtime.Instance.Compensate(timeProvider.GetUtcNow());
        return runtime.Instance.ToSnapshot();
    }

    private async Task<Exception?> ExecuteSagaStepAsync<TState>(
        WorkflowInstance<TState> instance,
        Func<IStep<TState>> stepFactory,
        string stepPath,
        CancellationToken cancellationToken)
    {
        try
        {
            var step = stepFactory();
            var result = await step.ExecuteAsync(
                new StepContext<TState>(instance.State, null, timeProvider),
                cancellationToken).ConfigureAwait(false);
            switch (result)
            {
                case StepResult.Completed:
                    instance.RecordLifecycleEvent("SagaStepCompleted", stepPath, instance.Status, timeProvider.GetUtcNow());
                    return null;
                case StepResult.Failed failed:
                    return failed.Error;
                default:
                    return new NotSupportedException(
                        $"Ephemeral saga step result '{result.GetType().Name}' is not supported.");
            }
        }
        catch (Exception exception) when (exception is not OperationCanceledException and not NotSupportedException)
        {
            return exception;
        }
    }

    private static WorkflowErrorDetails ToWorkflowError(Exception exception, string stepPath, DateTimeOffset occurredAt)
    {
        return new WorkflowErrorDetails(
            exception.GetType().Name,
            exception.Message,
            stepPath,
            occurredAt);
    }

    /// <summary>
    /// Resolves one active wait by event name and correlation, then delivers the event to it.
    /// </summary>
    public async Task<WorkflowInstanceSnapshot> RaiseEventByCorrelationAsync<TState>(
        EventEnvelope envelope,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(envelope);
        cancellationToken.ThrowIfCancellationRequested();

        var candidateIds = routingIndex.FindCandidates(envelope);
        if (candidateIds.Count == 0)
        {
            throw new WorkflowRoutingException(
                $"No active wait exists for event '{envelope.EventName}' and correlation '{envelope.CorrelationId}'.");
        }

        var matches = instanceRegistry.GetMany(candidateIds)
            .OfType<WorkflowInstance<TState>>()
            .Where(instance => instance.HasActiveWait(envelope.EventName, envelope.CorrelationId))
            .ToArray();

        if (matches.Length == 0)
        {
            throw new WorkflowRoutingException(
                $"No active wait exists for event '{envelope.EventName}' and correlation '{envelope.CorrelationId}'.");
        }

        if (matches.Length > 1)
        {
            throw new WorkflowRoutingException(
                $"Correlation-targeted delivery for event '{envelope.EventName}' and correlation " +
                $"'{envelope.CorrelationId}' is ambiguous; use instance-targeted delivery or definition fanout.");
        }

        return await RaiseEventAsync<TState>(
            matches[0].InstanceId,
            envelope,
            cancellationToken).ConfigureAwait(false);
    }

    private void IndexSnapshot(WorkflowInstanceSnapshot snapshot)
    {
        routingIndex.IndexSnapshot(snapshot);
    }

    /// <summary>
    /// Delivers an event to all active waits belonging to one workflow definition.
    /// </summary>
    public async Task<IReadOnlyList<WorkflowInstanceSnapshot>> RaiseEventByDefinitionAsync<TState>(
        DefinitionId definitionId,
        EventEnvelope envelope,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(envelope);
        cancellationToken.ThrowIfCancellationRequested();

        var matches = instanceRegistry.List()
            .OfType<WorkflowInstance<TState>>()
            .Where(instance =>
                instance.DefinitionId == definitionId &&
                instance.HasActiveWait(envelope.EventName, envelope.CorrelationId))
            .ToArray();
        var snapshots = new List<WorkflowInstanceSnapshot>(matches.Length);

        foreach (var instance in matches)
        {
            var snapshot = await RaiseEventAsync<TState>(
                instance.InstanceId,
                envelope,
                cancellationToken).ConfigureAwait(false);
            snapshots.Add(snapshot);
        }

        return snapshots;
    }

    private sealed class EphemeralSagaRuntimeState<TState>(
        SagaDefinition<TState> definition,
        WorkflowInstance<TState> instance)
    {
        internal SagaDefinition<TState> Definition { get; } = definition;

        internal WorkflowInstance<TState> Instance { get; } = instance;

        internal List<SagaForwardAction<TState>> CompletedActions { get; } = [];

        internal bool CompensationRequested { get; set; }
    }
}
