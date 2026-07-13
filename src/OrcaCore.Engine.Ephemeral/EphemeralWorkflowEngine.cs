using System.Collections.Concurrent;
using OrcaCore.Abstractions.Events;
using OrcaCore.Abstractions.Errors;
using OrcaCore.Abstractions.Ids;
using OrcaCore.Abstractions.Instances;
using OrcaCore.Abstractions.Steps;
using OrcaCore.Core.Definitions;
using OrcaCore.Core.Lifecycle;
using OrcaCore.Engine.Ephemeral.Diagnostics;
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
        ArgumentNullException.ThrowIfNull(options.StateSnapshotter);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(options.MaxPendingEventsPerInstance);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(options.MaxConsumedEventIdsPerInstance);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(options.MaxLifecycleEventsPerInstance);

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
            options);
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

    internal IEphemeralStateSnapshotter StateSnapshotter => options.StateSnapshotter;

    /// <summary>
    /// Registers a workflow definition version for later starts.
    /// </summary>
    public void RegisterDefinition<TState>(WorkflowDefinition<TState> definition)
    {
        ArgumentNullException.ThrowIfNull(definition);
        if (definition.RequiresDurableEngine)
        {
            throw new WorkflowDefinitionException(
                $"Workflow definition '{definition.DefinitionId}' contains durable-only nodes " +
                "(RunChild/RunChildren) and cannot be registered on the ephemeral engine.");
        }

        if (definition.Policies.Retry is not null)
        {
            throw new WorkflowDefinitionException(
                "Definition-level retry is not supported by the ephemeral engine because replaying the whole " +
                "definition could duplicate completed side effects. Apply retry to individual steps instead.");
        }

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
        using var activity = OrcaCoreEphemeralDiagnostics.StartOperation("start");
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
                        initializedInstance =>
                        {
                            instanceRegistry.Save(initializedInstance);
                            CommitSnapshot(initializedInstance.ToSnapshot());
                        },
                        laneCancellationToken).ConfigureAwait(false);

                    return CommitSnapshot(instance.ToSnapshot());
                },
                cancellationToken).ConfigureAwait(false);
        }

        snapshot = instance is null
            ? snapshot
            : await yieldContinuationScheduler
                .DrainAsync(instance, instanceId, committed => CommitSnapshot(committed), cancellationToken)
                .ConfigureAwait(false);
        activity?.SetTag("workflow.instance_id", snapshot.InstanceId.ToString());
        activity?.SetTag("workflow.status", snapshot.Status.ToString());
        OrcaCoreEphemeralDiagnostics.RecordWorkflowStarted(snapshot.Status);
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
                        timeProvider.GetUtcNow(),
                        options.MaxPendingEventsPerInstance,
                        options.MaxConsumedEventIdsPerInstance,
                        options.MaxLifecycleEventsPerInstance);
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
                            return CommitSnapshot(await CompensateSagaRuntimeAsync(
                                runtime,
                                failure,
                                action.ScopeId,
                                laneCancellationToken).ConfigureAwait(false));
                        }

                        runtime.CompletedActions.Add(action);
                    }

                    instance.Complete(null, timeProvider.GetUtcNow());
                    return CommitSnapshot(instance.ToSnapshot());
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
        return await RequestSagaCompensationCoreAsync<TState>(
            instanceId,
            scopeId: null,
            cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Requests compensation for one declared saga scope in-process only.
    /// </summary>
    public async Task<WorkflowInstanceSnapshot> RequestSagaCompensationAsync<TState>(
        InstanceId instanceId,
        string scopeId,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(scopeId);
        return await RequestSagaCompensationCoreAsync<TState>(
            instanceId,
            (string?)scopeId,
            cancellationToken).ConfigureAwait(false);
    }

    private async Task<WorkflowInstanceSnapshot> RequestSagaCompensationCoreAsync<TState>(
        InstanceId instanceId,
        string? scopeId,
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

        if (scopeId is not null && !runtime.Definition.ForwardActions.Any(action =>
            string.Equals(action.ScopeId, scopeId, StringComparison.Ordinal)))
        {
            throw new WorkflowDefinitionException(
                $"Saga scope '{scopeId}' is not declared by instance '{instanceId}'.");
        }

        await using (await governance.EnterAdvancementAsync(cancellationToken).ConfigureAwait(false))
        {
            return await executionLane.RunAsync(
                instanceId,
                async laneCancellationToken => CommitSnapshot(await CompensateSagaRuntimeAsync(
                    runtime,
                    null,
                    scopeId,
                    laneCancellationToken).ConfigureAwait(false)),
                cancellationToken).ConfigureAwait(false);
        }
    }

    /// <summary>
    /// Fires all transient timers whose due time has passed in this process.
    /// </summary>
    public async Task<IReadOnlyList<WorkflowInstanceSnapshot>> FireDueTimersAsync(
        CancellationToken cancellationToken)
    {
        using var activity = OrcaCoreEphemeralDiagnostics.StartOperation("fire_due_timers");
        cancellationToken.ThrowIfCancellationRequested();

        var dueTimers = timerService.ClaimDueTimers();
        if (dueTimers.Count == 0)
        {
            return [];
        }

        var snapshots = new List<WorkflowInstanceSnapshot>(dueTimers.Count);
        for (var index = 0; index < dueTimers.Count; index++)
        {
            var timer = dueTimers[index];
            try
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
                                return CommitSnapshot(await timer.FireAsync(linkedCancellation.Token).ConfigureAwait(false));
                            }

                            return CommitSnapshot(await timer.FireAsync(laneCancellationToken).ConfigureAwait(false));
                        },
                        cancellationToken).ConfigureAwait(false);
                }

                if (registeredInstance is IWorkflowInstance instance)
                {
                    snapshot = await yieldContinuationScheduler
                        .DrainAsync(instance, timer.InstanceId, committed => CommitSnapshot(committed), cancellationToken)
                        .ConfigureAwait(false);
                }

                snapshots.Add(snapshot);
                OrcaCoreEphemeralDiagnostics.RecordTimerFired(snapshot.Status);
                timerService.Complete(timer);
            }
            catch
            {
                for (var releaseIndex = index; releaseIndex < dueTimers.Count; releaseIndex++)
                {
                    timerService.Release(dueTimers[releaseIndex]);
                }

                throw;
            }
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
        using var activity = OrcaCoreEphemeralDiagnostics.StartOperation("raise_event");
        ArgumentNullException.ThrowIfNull(envelope);
        ValidateEventEnvelope(envelope);
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
                    return CommitSnapshot(await instance
                        .RaiseEventAsync(envelope, timeProvider.GetUtcNow(), linkedCancellation.Token)
                        .ConfigureAwait(false));
                },
                cancellationToken).ConfigureAwait(false);
        }

        snapshot = await yieldContinuationScheduler
            .DrainAsync(instance, instanceId, committed => CommitSnapshot(committed), cancellationToken)
            .ConfigureAwait(false);
        activity?.SetTag("workflow.instance_id", snapshot.InstanceId.ToString());
        activity?.SetTag("workflow.status", snapshot.Status.ToString());
        OrcaCoreEphemeralDiagnostics.RecordEventDelivered(snapshot.Status);
        return snapshot;
    }

    internal async Task<WorkflowInstanceSnapshot> CancelInstanceAsync(
        InstanceId instanceId,
        CancellationToken cancellationToken)
    {
        return await RunInterruptingTerminalCommandAsync(
            instanceId,
            instance => instance.Cancel(timeProvider.GetUtcNow()),
            cancellationToken).ConfigureAwait(false);
    }

    internal async Task<WorkflowInstanceSnapshot> TerminateInstanceAsync(
        InstanceId instanceId,
        CancellationToken cancellationToken)
    {
        return await RunInterruptingTerminalCommandAsync(
            instanceId,
            instance => instance.Terminate(timeProvider.GetUtcNow()),
            cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Removes one terminal instance and its saga runtime state from process memory.
    /// Returns false when the instance is unknown; throws when it is still active.
    /// </summary>
    internal bool EvictInstance(InstanceId instanceId)
    {
        if (!instanceRegistry.TryGet(instanceId, out var registeredInstance) ||
            registeredInstance is not IWorkflowInstance instance)
        {
            return false;
        }

        var snapshot = instance.ToSnapshot();
        if (!LifecycleMachine.TerminalStatuses.Contains(snapshot.Status))
        {
            throw new WorkflowLifecycleException(
                $"Workflow instance '{instanceId}' is '{snapshot.Status}' and cannot be evicted before " +
                "reaching a terminal status.");
        }

        var removed = instanceRegistry.Remove(instanceId);
        sagaRuntimeStates.TryRemove(instanceId, out _);
        IndexSnapshot(snapshot);
        return removed;
    }

    /// <summary>
    /// Removes every terminal instance and its saga runtime state from process memory.
    /// </summary>
    internal int EvictTerminalInstances()
    {
        var purged = 0;
        foreach (var registered in instanceRegistry.List())
        {
            if (registered is not IWorkflowInstance instance)
            {
                continue;
            }

            var snapshot = instance.ToSnapshot();
            if (!LifecycleMachine.TerminalStatuses.Contains(snapshot.Status))
            {
                continue;
            }

            if (instanceRegistry.Remove(instance.InstanceId))
            {
                purged++;
            }

            sagaRuntimeStates.TryRemove(instance.InstanceId, out _);
            IndexSnapshot(snapshot);
        }

        return purged;
    }

    private async Task<WorkflowInstanceSnapshot> RunInterruptingTerminalCommandAsync(
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

        // Signalling interruption is the command's commit point. Once user code has
        // observed it, finish the terminal transition even if the caller disconnects.
        instance.SignalCancellation();
        await using (await governance.EnterAdvancementAsync(CancellationToken.None).ConfigureAwait(false))
        {
            var snapshot = await executionLane.RunAsync(
                instanceId,
                _ => Task.FromResult(CommitSnapshot(command(instance))),
                CancellationToken.None).ConfigureAwait(false);
            OrcaCoreEphemeralDiagnostics.RecordTerminalCommand(snapshot.Status);
            return snapshot;
        }
    }

    private async Task<WorkflowInstanceSnapshot> CompensateSagaRuntimeAsync<TState>(
        EphemeralSagaRuntimeState<TState> runtime,
        Exception? triggeringFailure,
        string? scopeId,
        CancellationToken cancellationToken)
    {
        if (runtime.CompensationState == SagaCompensationState.Completed)
        {
            return runtime.Instance.ToSnapshot();
        }

        runtime.CompensationState = SagaCompensationState.InProgress;
        try
        {
            if (runtime.CompletedActions.Count == 0)
            {
                if (triggeringFailure is not null)
                {
                    runtime.Instance.Fail(ToWorkflowError(triggeringFailure, "saga", timeProvider.GetUtcNow()));
                }

                runtime.CompensationState = SagaCompensationState.Completed;
                return runtime.Instance.ToSnapshot();
            }

            foreach (var action in runtime.CompletedActions
                .Where(action => scopeId is null || string.Equals(action.ScopeId, scopeId, StringComparison.Ordinal))
                .Reverse())
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
                    runtime.CompensationState = SagaCompensationState.Completed;
                    return runtime.Instance.ToSnapshot();
                }
            }

            runtime.Instance.Compensate(timeProvider.GetUtcNow());
            runtime.CompensationState = SagaCompensationState.Completed;
            return runtime.Instance.ToSnapshot();
        }
        catch
        {
            runtime.CompensationState = SagaCompensationState.NotRequested;
            throw;
        }
    }

    private async Task<Exception?> ExecuteSagaStepAsync<TState>(
        WorkflowInstance<TState> instance,
        Func<IStep<TState>> stepFactory,
        string stepPath,
        CancellationToken cancellationToken)
    {
        await using var governanceLease = await governance
            .EnterStepAsync(null, cancellationToken)
            .ConfigureAwait(false);
        using var stateAccess = await instance.EnterStateAccessAsync(cancellationToken).ConfigureAwait(false);
        using var executionCancellation = instance.CreateLinkedExecutionToken(cancellationToken);
        instance.StartStep(stepPath, timeProvider.GetUtcNow(), null);
        try
        {
            var step = stepFactory();
            var result = await step.ExecuteAsync(
                new StepContext<TState>(instance.State, null, timeProvider),
                executionCancellation.Token).ConfigureAwait(false);
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
        finally
        {
            instance.CompleteStep(stepPath, timeProvider.GetUtcNow());
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
        ValidateEventEnvelope(envelope);
        cancellationToken.ThrowIfCancellationRequested();

        var candidateIds = routingIndex.FindCandidates(envelope);
        if (candidateIds.Count == 0)
        {
            throw new WorkflowRoutingException(
                $"No active wait exists for event '{envelope.EventName}' and correlation '{envelope.CorrelationId}'.");
        }

        var matches = instanceRegistry.GetMany(candidateIds)
            .OfType<WorkflowInstance<TState>>()
            .Where(instance => instance.HasActiveWait(envelope))
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

    private WorkflowInstanceSnapshot CommitSnapshot(WorkflowInstanceSnapshot snapshot)
    {
        if (instanceRegistry.TryGet(snapshot.InstanceId, out var registered) &&
            registered is IWorkflowInstance instance)
        {
            try
            {
                instance.PublishState(options.StateSnapshotter);
            }
            catch (WorkflowDefinitionException)
            {
                // State queryability is optional. A state type unsupported by the
                // configured snapshotter must not change workflow execution semantics.
            }
        }

        IndexSnapshot(snapshot);
        return snapshot;
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
        ValidateEventEnvelope(envelope);
        cancellationToken.ThrowIfCancellationRequested();

        var matches = instanceRegistry.List()
            .OfType<WorkflowInstance<TState>>()
            .Where(instance =>
                instance.DefinitionId == definitionId &&
                instance.HasActiveWait(envelope))
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

    private static void ValidateEventEnvelope(EventEnvelope envelope)
    {
        if (envelope.EventId.Value == Guid.Empty)
        {
            throw new ArgumentException("EventId must not be empty.", nameof(envelope));
        }

        if (string.IsNullOrWhiteSpace(envelope.EventName))
        {
            throw new ArgumentException("EventName must not be empty.", nameof(envelope));
        }

        if (string.IsNullOrWhiteSpace(envelope.CorrelationId.Value))
        {
            throw new ArgumentException("CorrelationId must not be empty.", nameof(envelope));
        }

        if (envelope.OccurredAt == default)
        {
            throw new ArgumentException("OccurredAt must not be the default value.", nameof(envelope));
        }

        if (envelope.BranchId is not null && string.IsNullOrWhiteSpace(envelope.BranchId))
        {
            throw new ArgumentException("BranchId must not be whitespace when supplied.", nameof(envelope));
        }
    }

    private sealed class EphemeralSagaRuntimeState<TState>(
        SagaDefinition<TState> definition,
        WorkflowInstance<TState> instance)
    {
        internal SagaDefinition<TState> Definition { get; } = definition;

        internal WorkflowInstance<TState> Instance { get; } = instance;

        internal List<SagaForwardAction<TState>> CompletedActions { get; } = [];

        internal SagaCompensationState CompensationState { get; set; }
    }

    private enum SagaCompensationState
    {
        NotRequested,
        InProgress,
        Completed
    }
}
