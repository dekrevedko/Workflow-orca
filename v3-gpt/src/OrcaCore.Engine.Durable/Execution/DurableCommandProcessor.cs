using System.Diagnostics;
using OrcaCore.Abstractions.Diagnostics;
using OrcaCore.Abstractions.Durable;
using OrcaCore.Abstractions.Ids;
using OrcaCore.Abstractions.Instances;
using OrcaCore.Abstractions.Primitives;
using OrcaCore.Abstractions.Providers;
using OrcaCore.Abstractions.Serialization;
using OrcaCore.Engine.Durable.Aggregates;
using OrcaCore.Engine.Durable.Diagnostics;

namespace OrcaCore.Engine.Durable.Execution;

public sealed class DurableCommandProcessor
{
    private readonly DurableCommandRuntime runtime;
    private readonly IWorkflowEventStore eventStore;
    private readonly DurableAggregateLoader aggregateLoader;
    private readonly DurableCommitPipeline commitPipeline;
    private readonly IResourcePoolStore? resourcePoolStore;
    private readonly IWorkflowInboxStore? inboxStore;
    private readonly IWorkflowStartIdempotencyStore? startIdempotencyStore;
    private readonly IWorkflowRuntimeObserver runtimeObserver;

    /// <summary>
    /// Initializes a command processor with its own durable command runtime.
    /// </summary>
    /// <param name="eventStore">The durable event store used for command commits.</param>
    /// <param name="resourcePoolStore">The optional durable resource-pool store used by resource commands.</param>
    public DurableCommandProcessor(
        IWorkflowEventStore eventStore,
        IResourcePoolStore? resourcePoolStore = null,
        IWorkflowRuntimeObserver? runtimeObserver = null)
        : this(new DurableCommandRuntime(eventStore, resourcePoolStore), runtimeObserver)
    {
    }

    /// <summary>
    /// Initializes a command processor that uses a shared durable command runtime.
    /// </summary>
    /// <param name="runtime">The shared durable command runtime for the host process.</param>
    public DurableCommandProcessor(
        DurableCommandRuntime runtime,
        IWorkflowRuntimeObserver? runtimeObserver = null)
    {
        ArgumentNullException.ThrowIfNull(runtime);

        this.runtime = runtime;
        this.runtimeObserver = runtimeObserver ?? NullWorkflowRuntimeObserver.Instance;
        eventStore = runtime.EventStore;
        aggregateLoader = new DurableAggregateLoader(eventStore);
        resourcePoolStore = runtime.ResourcePoolStore;
        commitPipeline = new DurableCommitPipeline(
            eventStore,
            new DurableCommitMaterializer(),
            new DurableResourcePoolCommitEffects(resourcePoolStore));
        inboxStore = eventStore as IWorkflowInboxStore;
        startIdempotencyStore = eventStore as IWorkflowStartIdempotencyStore;
    }

    internal int ActiveLaneCount => runtime.ActiveLaneCount;

    internal async Task<Option<StartedWorkflowIdempotencyRecord>> GetStartedAsync(
        string idempotencyKey,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(idempotencyKey);
        return startIdempotencyStore is null
            ? Option<StartedWorkflowIdempotencyRecord>.None
            : await startIdempotencyStore.GetStartedAsync(idempotencyKey, cancellationToken).ConfigureAwait(false);
    }

    public Task<DurableCommandResult> ProcessAsync(
        StartWorkflowCommand command,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);
        return RunInLaneAsync(
            command.InstanceId,
            aggregate => aggregate.DecideStart(command),
            cancellationToken,
            commandType: nameof(StartWorkflowCommand));
    }

    internal Task<DurableCommandResult> ProcessAsync(
        DurableStepCompletedCommand command,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);
        return RunInLaneAsync(
            command.InstanceId,
            aggregate => aggregate.DecideStepCompleted(command),
            cancellationToken);
    }

    internal Task<DurableCommandResult> ProcessAsync(
        DurableStepFailedCommand command,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);
        return RunInLaneAsync(
            command.InstanceId,
            aggregate => aggregate.DecideStepFailed(command),
            cancellationToken);
    }

    public Task<DurableCommandResult> ProcessAsync(
        DurableYieldCommand command,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);
        return RunInLaneAsync(
            command.InstanceId,
            aggregate => aggregate.DecideYield(command),
            cancellationToken,
            commandType: nameof(DurableYieldCommand));
    }

    public Task<DurableCommandResult> ProcessAsync(
        DurableRunChildCommand command,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);
        return RunInLaneAsync(
            command.InstanceId,
            aggregate => aggregate.DecideRunChild(command),
            cancellationToken);
    }

    public Task<DurableCommandResult> ProcessAsync(
        DurableChildCompletedCommand command,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);
        return RunInLaneAsync(
            command.InstanceId,
            aggregate => aggregate.DecideChildCompleted(command),
            cancellationToken);
    }

    public Task<DurableCommandResult> ProcessAsync(
        DurableRunChildrenCommand command,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);
        return RunInLaneAsync(
            command.InstanceId,
            aggregate => aggregate.DecideRunChildren(command),
            cancellationToken);
    }

    internal Task<DurableCommandResult> ProcessAsync(
        DurableWaitRegisteredCommand command,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);
        return RunInLaneAsync(
            command.InstanceId,
            aggregate => aggregate.DecideWaitRegistered(command),
            cancellationToken);
    }

    internal Task<DurableCommandResult> ProcessAsync(
        DurableWaitMatchedCommand command,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);
        return RunInLaneAsync(
            command.InstanceId,
            aggregate => aggregate.DecideWaitMatched(command),
            cancellationToken);
    }

    public Task<DurableCommandResult> ProcessAsync(
        ScheduleTimerCommand command,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);
        return RunInLaneAsync(
            command.InstanceId,
            aggregate => aggregate.DecideTimerScheduled(command),
            cancellationToken);
    }

    public Task<DurableCommandResult> ProcessAsync(
        FireTimerCommand command,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);
        return RunInLaneAsync(
            command.InstanceId,
            aggregate => aggregate.DecideTimerFired(command),
            cancellationToken);
    }

    public Task<DurableCommandResult> ProcessAsync(
        AcquireResourcePoolCommand command,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);
        return RunInLaneAsync(
            command.InstanceId,
            async (aggregate, token) =>
            {
                var acquireResult = await RequiredResourcePoolStore()
                    .AcquireAsync(
                        new ResourcePoolAcquireRequest(
                            command.InstanceId,
                            command.HolderKey,
                            command.Requirements,
                            command.RequestedAt,
                            command.ExpiresAt),
                        token)
                    .ConfigureAwait(false);
                return aggregate.DecideResourcePoolAcquire(command, acquireResult);
            },
            cancellationToken);
    }

    public Task<DurableCommandResult> ProcessAsync(
        RunExternalJobCommand command,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);
        return RunInLaneAsync(
            command.InstanceId,
            async (aggregate, token) =>
            {
                ResourcePoolAcquireResult? acquireResult = null;
                if (command.Requirements.Count > 0)
                {
                    acquireResult = await RequiredResourcePoolStore()
                        .AcquireAsync(
                            new ResourcePoolAcquireRequest(
                                command.InstanceId,
                                command.ExternalJobId,
                                command.Requirements,
                                command.RequestedAt,
                                command.TimeoutAt),
                            token)
                        .ConfigureAwait(false);
                }

                return aggregate.DecideRunExternalJob(command, acquireResult);
            },
            cancellationToken);
    }

    public Task<DurableCommandResult> ProcessAsync(
        CompleteExternalJobCommand command,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);
        return RunInLaneAsync(
            command.InstanceId,
            aggregate => aggregate.DecideExternalJobCompleted(command),
            cancellationToken,
            command.CompletionEventId);
    }

    public Task<DurableCommandResult> ProcessAsync(
        TimeoutExternalJobCommand command,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);
        return RunInLaneAsync(
            command.InstanceId,
            aggregate => aggregate.DecideExternalJobTimedOut(command),
            cancellationToken);
    }

    public Task<DurableCommandResult> ProcessAsync(
        CancelWorkflowCommand command,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);
        return RunInLaneAsync(
            command.InstanceId,
            aggregate => aggregate.DecideCancel(command),
            cancellationToken);
    }

    public Task<DurableCommandResult> ProcessAsync(
        ConsumeParentResumeTokenCommand command,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);
        return RunInLaneAsync(
            command.InstanceId,
            aggregate => aggregate.DecideConsumeParentResumeToken(command),
            cancellationToken);
    }

    public Task<DurableCommandResult> ProcessAsync(
        CompensateChildGroupCommand command,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);
        return RunInLaneAsync(
            command.InstanceId,
            aggregate => aggregate.DecideCompensateChildGroup(command),
            cancellationToken);
    }

    public Task<DurableCommandResult> ProcessAsync(
        RecordSagaForwardActionCompletedCommand command,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);
        return RunInLaneAsync(
            command.InstanceId,
            aggregate => aggregate.DecideRecordSagaForwardActionCompleted(command),
            cancellationToken);
    }

    public Task<DurableCommandResult> ProcessAsync(
        RequestSagaCompensationCommand command,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);
        return RunInLaneAsync(
            command.InstanceId,
            aggregate => aggregate.DecideRequestSagaCompensation(command),
            cancellationToken);
    }

    public Task<DurableCommandResult> ProcessAsync(
        SagaForwardActionTimedOutCommand command,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);
        return RunInLaneAsync(
            command.InstanceId,
            aggregate => aggregate.DecideSagaForwardActionTimedOut(command),
            cancellationToken);
    }

    public Task<DurableCommandResult> ProcessAsync(
        CompleteSagaCompensationCommand command,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);
        return RunInLaneAsync(
            command.InstanceId,
            aggregate => aggregate.DecideCompleteSagaCompensation(command),
            cancellationToken);
    }

    public Task<DurableCommandResult> ProcessAsync(
        FailSagaCompensationCommand command,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);
        return RunInLaneAsync(
            command.InstanceId,
            aggregate => aggregate.DecideFailSagaCompensation(command),
            cancellationToken);
    }

    public Task<DurableCommandResult> ProcessAsync(
        RecordSagaManualRecoveryCommand command,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);
        return RunInLaneAsync(
            command.InstanceId,
            aggregate => aggregate.DecideRecordSagaManualRecovery(command),
            cancellationToken);
    }

    internal Task<DurableCommandResult> ProcessAsync(
        DurablePauseCommand command,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);
        return RunInLaneAsync(
            command.InstanceId,
            aggregate => aggregate.DecidePause(command),
            cancellationToken);
    }

    internal Task<DurableCommandResult> ProcessAsync(
        DurableResumeCommand command,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);
        return RunInLaneAsync(
            command.InstanceId,
            aggregate => aggregate.DecideResume(command),
            cancellationToken);
    }

    internal Task<DurableCommandResult> ProcessAsync(
        DeliverEventCommand command,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);
        return RunInLaneAsync(
            command.InstanceId,
            aggregate => aggregate.DecideDeliverEvent(command),
            cancellationToken,
            command.Envelope.EventId);
    }

    internal Task<DurableCommandResult> ProcessAsync(
        DurableCompleteCommand command,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);
        return RunInLaneAsync(
            command.InstanceId,
            aggregate => aggregate.DecideComplete(command),
            cancellationToken);
    }

    internal Task<DurableCommandResult> ProcessAsync(
        DurableFailCommand command,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);
        return RunInLaneAsync(
            command.InstanceId,
            aggregate => aggregate.DecideFail(command),
            cancellationToken);
    }

    public Task<DurableCommandResult> ProcessAsync(
        TerminateWorkflowCommand command,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);
        return RunInLaneAsync(
            command.InstanceId,
            aggregate => aggregate.DecideTerminate(command),
            cancellationToken);
    }

    public Task<DurableCommandResult> ProcessAsync(
        ContinueAsNewCommand command,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);
        return RunInLaneAsync(
            command.InstanceId,
            aggregate => aggregate.DecideContinueAsNew(command),
            cancellationToken);
    }

    internal Task<DurableCommandResult> EvictIdleAsync(
        InstanceId instanceId,
        CancellationToken cancellationToken)
    {
        return RunInLaneAsync(
            instanceId,
            aggregate => aggregate.Snapshot.Status is null
                ? DurableDecision.Empty
                : new DurableDecision([], null, true),
            cancellationToken);
    }

    private async Task<DurableCommandResult> RunInLaneAsync(
        InstanceId instanceId,
        Func<DurableWorkflowAggregate, DurableDecision> decide,
        CancellationToken cancellationToken,
        EventId? inboxEventId = null,
        string commandType = "DurableCommand")
    {
        return await RunInLaneAsync(
            instanceId,
            (aggregate, _) => Task.FromResult(decide(aggregate)),
            cancellationToken,
            inboxEventId,
            commandType).ConfigureAwait(false);
    }

    private async Task<DurableCommandResult> RunInLaneAsync(
        InstanceId instanceId,
        Func<DurableWorkflowAggregate, CancellationToken, Task<DurableDecision>> decide,
        CancellationToken cancellationToken,
        EventId? inboxEventId = null,
        string commandType = "DurableCommand")
    {
        return await runtime.RunAsync(
            instanceId,
            token => ProcessCoreAsync(instanceId, decide, token, inboxEventId, commandType),
            cancellationToken).ConfigureAwait(false);
    }

    private async Task<DurableCommandResult> ProcessCoreAsync(
        InstanceId instanceId,
        Func<DurableWorkflowAggregate, CancellationToken, Task<DurableDecision>> decide,
        CancellationToken cancellationToken,
        EventId? inboxEventId,
        string commandType)
    {
        var stopwatch = Stopwatch.StartNew();
        using var activity = OrcaCoreDurableDiagnostics.ActivitySource.StartActivity("orca.command.process");
        activity?.SetTag(OrcaCoreDiagnostics.CommandTypeKey, commandType);
        activity?.SetTag(OrcaCoreDiagnostics.InstanceIdKey, instanceId.ToString());

        try
        {
            var inboxState = await LoadInboxStateAsync(inboxEventId, cancellationToken).ConfigureAwait(false);
            if (DurableInboxPreflight.TryCreateResult(inboxState) is { } preflightResult)
            {
                stopwatch.Stop();
                activity?.SetTag(OrcaCoreDiagnostics.CommandOutcomeKey, preflightResult.Outcome.ToString());
                return await ObserveCommandCompletedAsync(
                    instanceId,
                    preflightResult,
                    eventCount: 0,
                    checkpointWritten: false,
                    inboxEventId,
                    cancellationToken,
                    commandType,
                    definitionId: null,
                    definitionVersion: null,
                    status: null,
                    stopwatch.Elapsed,
                    inboxDuplicate: IsInboxDuplicate(inboxState))
                    .ConfigureAwait(false);
            }

            var aggregate = await aggregateLoader.LoadAsync(instanceId, cancellationToken).ConfigureAwait(false);
            var preDecisionSnapshot = aggregate.Snapshot;
            var activeWaitsById = preDecisionSnapshot.ActiveWaits.ToDictionary(wait => wait.WaitId);
            var decision = await decide(aggregate, cancellationToken).ConfigureAwait(false);

            var providerCommitAttempted = HasProviderCommit(decision, inboxEventId);
            var providerName = ProviderName(eventStore);
            var providerCommitStopwatch = Stopwatch.StartNew();
            var result = await CommitWithTelemetryAsync(
                    instanceId,
                    aggregate,
                    decision,
                    inboxEventId,
                    providerCommitAttempted,
                    providerName,
                    cancellationToken)
                .ConfigureAwait(false);
            providerCommitStopwatch.Stop();
            stopwatch.Stop();
            activity?.SetTag(OrcaCoreDiagnostics.CommandOutcomeKey, result.Outcome.ToString());
            activity?.SetTag("stream.version", result.StreamVersion.Value);
            var aggregateSnapshot = aggregate.Snapshot;
            var committedSnapshot = result.Outcome == DurableCommandOutcome.Committed
                ? aggregate
                    .CreateProjectionWrites(decision.Events)
                    .Select(write => write.InstanceSnapshot)
                    .FirstOrDefault(snapshot => snapshot is not null)
                : null;
            var observedDefinitionId = committedSnapshot?.DefinitionId ?? aggregateSnapshot.DefinitionId;
            var observedDefinitionVersion = committedSnapshot?.DefinitionVersion ?? aggregateSnapshot.DefinitionVersion;
            var observedStatus = committedSnapshot?.Status ?? aggregateSnapshot.Status;

            if (observedDefinitionId is { } definitionId)
            {
                activity?.SetTag(OrcaCoreDiagnostics.DefinitionIdKey, definitionId.ToString());
            }

            if (observedDefinitionVersion is { } definitionVersion)
            {
                activity?.SetTag(OrcaCoreDiagnostics.DefinitionVersionKey, definitionVersion.ToString());
            }

            if (observedStatus is { } status)
            {
                activity?.SetTag(OrcaCoreDiagnostics.StatusKey, status.ToString());
            }

            var eventObservations = result.Outcome is DurableCommandOutcome.Committed
                ? CreateEventObservations(
                    decision.Events,
                    observedDefinitionId,
                    activeWaitsById,
                    stopwatch.Elapsed)
                : [];
            RecordEventSpans(eventObservations, instanceId);
            RecordStepSpans(eventObservations, instanceId);

            return await ObserveCommandCompletedAsync(
                instanceId,
                result,
                decision.Events.Count,
                decision.Checkpoint is not null,
                inboxEventId,
                cancellationToken,
                commandType,
                observedDefinitionId,
                observedDefinitionVersion,
                observedStatus,
                stopwatch.Elapsed,
                eventObservations,
                providerCommitAttempted,
                providerName,
                providerCommitStopwatch.Elapsed,
                inboxDuplicate: false)
                .ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
            activity?.AddException(ex);
            throw;
        }
    }

    private async Task<DurableCommandResult> CommitWithTelemetryAsync(
        InstanceId instanceId,
        DurableWorkflowAggregate aggregate,
        DurableDecision decision,
        EventId? inboxEventId,
        bool providerCommitAttempted,
        string providerName,
        CancellationToken cancellationToken)
    {
        if (!providerCommitAttempted)
        {
            return await commitPipeline
                .CommitAsync(instanceId, aggregate, decision, inboxEventId, cancellationToken)
                .ConfigureAwait(false);
        }

        using var activity = OrcaCoreDurableDiagnostics.ActivitySource.StartActivity("orca.provider.commit");
        activity?.SetTag(OrcaCoreDiagnostics.ProviderNameKey, providerName);
        activity?.SetTag(OrcaCoreDiagnostics.ProviderOperationKey, "append");
        activity?.SetTag(OrcaCoreDiagnostics.InstanceIdKey, instanceId.ToString());

        try
        {
            var result = await commitPipeline
                .CommitAsync(instanceId, aggregate, decision, inboxEventId, cancellationToken)
                .ConfigureAwait(false);
            activity?.SetTag(OrcaCoreDiagnostics.CommandOutcomeKey, result.Outcome.ToString());
            activity?.SetTag(OrcaCoreDiagnostics.StreamVersionKey, result.StreamVersion.Value);
            if (result.Outcome is DurableCommandOutcome.Conflict)
            {
                activity?.SetStatus(ActivityStatusCode.Error, result.Message);
            }

            return result;
        }
        catch (Exception ex)
        {
            activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
            activity?.AddException(ex);
            throw;
        }
    }

    private async Task<DurableCommandResult> ObserveCommandCompletedAsync(
        InstanceId instanceId,
        DurableCommandResult result,
        int eventCount,
        bool checkpointWritten,
        EventId? inboxEventId,
        CancellationToken cancellationToken,
        string commandType,
        DefinitionId? definitionId,
        DefinitionVersion? definitionVersion,
        WorkflowStatus? status,
        TimeSpan duration,
        IReadOnlyList<WorkflowRuntimeEventObservation>? events = null,
        bool providerCommitAttempted = false,
        string providerName = "unknown",
        TimeSpan providerCommitDuration = default,
        bool inboxDuplicate = false)
    {
        try
        {
            await runtimeObserver
                .OnCommandCompletedAsync(
                    new WorkflowRuntimeObservation(
                        ToObservationKind(result.Outcome),
                        instanceId,
                        result.Outcome,
                        result.StreamVersion,
                        eventCount,
                        checkpointWritten,
                        result.Evicted,
                        inboxEventId,
                        result.Message,
                        commandType,
                        definitionId,
                        definitionVersion,
                        status,
                        duration)
                    {
                        Events = events ?? [],
                        InboxDuplicate = inboxDuplicate,
                        ProviderCommitAttempted = providerCommitAttempted,
                        ProviderName = providerName,
                        ProviderCommitDuration = providerCommitDuration
                    },
                    cancellationToken)
                .ConfigureAwait(false);
        }
        catch (Exception)
        {
            // Runtime observations are diagnostics; observer failures must not change command results.
        }

        return result;
    }

    private static bool HasProviderCommit(DurableDecision decision, EventId? inboxEventId)
    {
        return decision.Events.Count > 0 || decision.Checkpoint is not null || inboxEventId is not null;
    }

    private static bool IsInboxDuplicate(Option<InboxRecordState> inboxState)
    {
        return inboxState.HasValue &&
            inboxState.Value is InboxRecordState.Applied
                or InboxRecordState.DuplicateIgnored
                or InboxRecordState.DiscardedOnResume;
    }

    private static IReadOnlyList<WorkflowRuntimeEventObservation> CreateEventObservations(
        IReadOnlyList<WorkflowEvent> events,
        DefinitionId? definitionId,
        IReadOnlyDictionary<WaitId, DurableActiveWait> activeWaitsById,
        TimeSpan stepDuration)
    {
        return events
            .Select(workflowEvent =>
            {
                var eventDefinitionId = workflowEvent is WorkflowStartedEvent started
                    ? started.DefinitionId
                    : definitionId;
                var eventType = WorkflowEventCodec.ToEventType(workflowEvent);
                return workflowEvent switch
                {
                    WorkflowStepCompletedEvent stepCompleted => new WorkflowRuntimeEventObservation(
                        eventType,
                        eventDefinitionId,
                        StepPath: stepCompleted.StepPath,
                        LifecycleEventName: "StepCompleted",
                        StepDuration: stepDuration),
                    WorkflowStepFailedEvent stepFailed => new WorkflowRuntimeEventObservation(
                        eventType,
                        eventDefinitionId,
                        StepPath: stepFailed.StepPath,
                        ErrorKind: nameof(WorkflowStepFailedEvent),
                        LifecycleEventName: "StepFailed",
                        StepDuration: stepDuration),
                    WorkflowWaitMatchedEvent waitMatched => new WorkflowRuntimeEventObservation(
                        eventType,
                        eventDefinitionId,
                        LifecycleEventName: "InstanceResumed",
                        WaitEventName: activeWaitsById.TryGetValue(waitMatched.WaitId, out var wait)
                            ? wait.EventName
                            : null,
                        WaitDuration: activeWaitsById.TryGetValue(waitMatched.WaitId, out wait)
                            ? PositiveDuration(waitMatched.OccurredAt - wait.RegisteredAt)
                            : null),
                    _ => new WorkflowRuntimeEventObservation(
                        eventType,
                        eventDefinitionId,
                        LifecycleEventName: ToLifecycleEventName(workflowEvent))
                };
            })
            .ToArray();
    }

    private static void RecordEventSpans(
        IReadOnlyList<WorkflowRuntimeEventObservation> events,
        InstanceId instanceId)
    {
        foreach (var workflowEvent in events)
        {
            using var activity = OrcaCoreDurableDiagnostics.ActivitySource.StartActivity("orca.event.apply");
            activity?.SetTag(OrcaCoreDiagnostics.EventTypeKey, workflowEvent.EventType);
            activity?.SetTag(OrcaCoreDiagnostics.InstanceIdKey, instanceId.ToString());
            if (workflowEvent.DefinitionId is { } definitionId)
            {
                activity?.SetTag(OrcaCoreDiagnostics.DefinitionIdKey, definitionId.ToString());
            }
        }
    }

    private static void RecordStepSpans(
        IReadOnlyList<WorkflowRuntimeEventObservation> events,
        InstanceId instanceId)
    {
        foreach (var workflowEvent in events.Where(workflowEvent => workflowEvent.StepPath is not null))
        {
            using var activity = OrcaCoreDurableDiagnostics.ActivitySource.StartActivity("orca.step.execute");
            activity?.SetTag(OrcaCoreDiagnostics.StepPathKey, workflowEvent.StepPath);
            activity?.SetTag(OrcaCoreDiagnostics.InstanceIdKey, instanceId.ToString());
            if (workflowEvent.DefinitionId is { } definitionId)
            {
                activity?.SetTag(OrcaCoreDiagnostics.DefinitionIdKey, definitionId.ToString());
            }

            if (workflowEvent.ErrorKind is { } errorKind)
            {
                activity?.SetTag(OrcaCoreDiagnostics.ErrorKindKey, errorKind);
                activity?.SetStatus(ActivityStatusCode.Error, errorKind);
            }
        }
    }

    private static string? ToLifecycleEventName(WorkflowEvent workflowEvent)
    {
        return workflowEvent switch
        {
            WorkflowStartedEvent => "InstanceStarted",
            WorkflowContinuedAsNewEvent => "InstanceContinuedAsNew",
            WorkflowWaitRegisteredEvent => "InstanceSuspended",
            WorkflowTimerScheduledEvent => "InstanceSuspended",
            WorkflowTimerFiredEvent => "InstanceResumed",
            WorkflowPausedEvent => "InstancePaused",
            WorkflowResumedEvent => "InstanceResumed",
            WorkflowCompletedEvent => "InstanceCompleted",
            WorkflowTerminalEvent { Status: WorkflowStatus.Failed } => "InstanceFailed",
            WorkflowTerminalEvent { Status: WorkflowStatus.Cancelled } => "InstanceCancelled",
            WorkflowTerminalEvent { Status: WorkflowStatus.Terminated } => "InstanceTerminated",
            WorkflowTerminalEvent { Status: WorkflowStatus.Compensated } => "InstanceCompensated",
            WorkflowTerminalEvent { Status: WorkflowStatus.CompensationFailed } => "InstanceCompensationFailed",
            _ => null
        };
    }

    private static TimeSpan PositiveDuration(TimeSpan duration)
    {
        return duration < TimeSpan.Zero ? TimeSpan.Zero : duration;
    }

    private static string ProviderName(object provider)
    {
        var name = provider.GetType().Name;
        return name
            .Replace("WorkflowProvider", string.Empty, StringComparison.Ordinal)
            .Replace("WorkflowStore", string.Empty, StringComparison.Ordinal)
            .Replace("EventStore", string.Empty, StringComparison.Ordinal);
    }

    private static WorkflowRuntimeObservationKind ToObservationKind(DurableCommandOutcome outcome)
    {
        return outcome switch
        {
            DurableCommandOutcome.Committed => WorkflowRuntimeObservationKind.CommandCommitted,
            DurableCommandOutcome.Conflict => WorkflowRuntimeObservationKind.CommandConflict,
            DurableCommandOutcome.Evicted => WorkflowRuntimeObservationKind.CommandEvicted,
            DurableCommandOutcome.Poisoned => WorkflowRuntimeObservationKind.CommandPoisoned,
            DurableCommandOutcome.NoOp => WorkflowRuntimeObservationKind.CommandNoOp,
            _ => throw new UnreachableException()
        };
    }

    private async Task<Option<InboxRecordState>> LoadInboxStateAsync(
        EventId? eventId,
        CancellationToken cancellationToken)
    {
        if (eventId is not { } inboxEventId)
        {
            return Option<InboxRecordState>.None;
        }

        return await RequiredInboxStore()
            .GetAsync(inboxEventId, cancellationToken)
            .ConfigureAwait(false);
    }

    private IWorkflowInboxStore RequiredInboxStore()
    {
        return inboxStore ?? throw new InvalidOperationException(
            "Durable event delivery requires an inbox-capable provider.");
    }

    private IResourcePoolStore RequiredResourcePoolStore()
    {
        return resourcePoolStore ?? throw new InvalidOperationException(
            "Durable resource-pool acquisition requires a resource-pool-capable provider.");
    }

}

public enum DurableCommandOutcome
{
    Committed,
    Conflict,
    Evicted,
    Poisoned,
    NoOp
}

public sealed record DurableCommandResult(
    DurableCommandOutcome Outcome,
    string? Message,
    StreamVersion StreamVersion,
    bool Evicted = false);
