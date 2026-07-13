using System.Diagnostics;
using OrcaCore.Abstractions.Diagnostics;
using OrcaCore.Abstractions.Durable;
using OrcaCore.Abstractions.Ids;
using OrcaCore.Abstractions.Instances;
using OrcaCore.Abstractions.Primitives;
using OrcaCore.Abstractions.Providers;
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
    private readonly DurableCommandTelemetry telemetry;

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
        telemetry = new DurableCommandTelemetry(runtimeObserver ?? NullWorkflowRuntimeObserver.Instance);
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

    internal IWorkflowEventStore EventStore => eventStore;

    internal DurableCommandRuntime.StepCancellationScope EnterStep(
        InstanceId instanceId,
        CancellationToken cancellationToken) =>
        runtime.EnterStep(instanceId, cancellationToken);

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
            cancellationToken,
            expectedVersion: command.ExpectedStreamVersion);
    }

    internal Task<DurableCommandResult> ProcessAsync(
        DurableStepFailedCommand command,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);
        return RunInLaneAsync(
            command.InstanceId,
            aggregate => aggregate.DecideStepFailed(command),
            cancellationToken,
            expectedVersion: command.ExpectedStreamVersion);
    }

    internal Task<DurableCommandResult> ProcessAsync(
        DurableParkCommand command,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);
        return RunInLaneAsync(
            command.InstanceId,
            aggregate => aggregate.DecidePark(command),
            cancellationToken,
            expectedVersion: command.ExpectedStreamVersion);
    }

    internal Task<DurableCommandResult> ProcessAsync(
        DurableUnparkCommand command,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);
        return RunInLaneAsync(
            command.InstanceId,
            aggregate => aggregate.DecideUnpark(command),
            cancellationToken,
            expectedVersion: command.ExpectedStreamVersion);
    }

    internal Task<DurableCommandResult> ProcessAsync(
        DurableContinuationAttemptFailedCommand command,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);
        return RunInLaneAsync(
            command.InstanceId,
            aggregate => aggregate.DecideContinuationAttemptFailed(command),
            cancellationToken,
            expectedVersion: command.ExpectedStreamVersion);
    }

    internal Task<DurableCommandResult> ProcessAsync(
        DurableContinuationAttemptResetCommand command,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);
        return RunInLaneAsync(
            command.InstanceId,
            aggregate => aggregate.DecideContinuationAttemptReset(command),
            cancellationToken,
            expectedVersion: command.ExpectedStreamVersion);
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
            commandType: nameof(DurableYieldCommand),
            expectedVersion: command.ExpectedStreamVersion);
    }

    public Task<DurableCommandResult> ProcessAsync(
        DurableRunChildCommand command,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);
        return RunInLaneAsync(
            command.InstanceId,
            aggregate => aggregate.DecideRunChild(command),
            cancellationToken,
            expectedVersion: command.ExpectedStreamVersion);
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
            cancellationToken,
            expectedVersion: command.ExpectedStreamVersion);
    }

    internal Task<DurableCommandResult> ProcessAsync(
        DurableWaitRegisteredCommand command,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);
        return RunInLaneAsync(
            command.InstanceId,
            aggregate => aggregate.DecideWaitRegistered(command),
            cancellationToken,
            expectedVersion: command.ExpectedStreamVersion);
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
            cancellationToken,
            expectedVersion: command.ExpectedStreamVersion);
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
            cancellationToken,
            expectedVersion: command.ExpectedStreamVersion);
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
            cancellationToken,
            expectedVersion: command.ExpectedStreamVersion);
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
        runtime.RequestStepCancellation(command.InstanceId);
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
            cancellationToken,
            expectedVersion: command.ExpectedStreamVersion);
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
            cancellationToken,
            expectedVersion: command.ExpectedStreamVersion);
    }

    internal Task<DurableCommandResult> ProcessAsync(
        DurableFailCommand command,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);
        return RunInLaneAsync(
            command.InstanceId,
            aggregate => aggregate.DecideFail(command),
            cancellationToken,
            expectedVersion: command.ExpectedStreamVersion);
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
            cancellationToken,
            expectedVersion: command.ExpectedStreamVersion);
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
        string commandType = "DurableCommand",
        StreamVersion? expectedVersion = null)
    {
        return await RunInLaneAsync(
            instanceId,
            (aggregate, _) => Task.FromResult(decide(aggregate)),
            cancellationToken,
            inboxEventId,
            commandType,
            expectedVersion).ConfigureAwait(false);
    }

    private async Task<DurableCommandResult> RunInLaneAsync(
        InstanceId instanceId,
        Func<DurableWorkflowAggregate, CancellationToken, Task<DurableDecision>> decide,
        CancellationToken cancellationToken,
        EventId? inboxEventId = null,
        string commandType = "DurableCommand",
        StreamVersion? expectedVersion = null)
    {
        return await runtime.RunAsync(
            instanceId,
            token => ProcessCoreAsync(instanceId, decide, token, inboxEventId, commandType, expectedVersion),
            cancellationToken).ConfigureAwait(false);
    }

    private async Task<DurableCommandResult> ProcessCoreAsync(
        InstanceId instanceId,
        Func<DurableWorkflowAggregate, CancellationToken, Task<DurableDecision>> decide,
        CancellationToken cancellationToken,
        EventId? inboxEventId,
        string commandType,
        StreamVersion? expectedVersion = null)
    {
        var stopwatch = Stopwatch.StartNew();
        using var activity = OrcaCoreDurableDiagnostics.ActivitySource.StartActivity("orca.command.process");
        activity?.SetTag(OrcaCoreDiagnostics.CommandTypeKey, commandType);
        activity?.SetTag(OrcaCoreDiagnostics.InstanceIdKey, instanceId.ToString());

        try
        {
            // Stage 1: inbox preflight — a duplicate or discarded delivery completes without
            // touching the aggregate.
            var inboxState = await LoadInboxStateAsync(inboxEventId, cancellationToken).ConfigureAwait(false);
            if (DurableInboxPreflight.TryCreateResult(inboxState) is { } preflightResult)
            {
                return await CompleteWithoutCommitAsync(
                    instanceId,
                    preflightResult,
                    inboxEventId,
                    commandType,
                    stopwatch,
                    activity,
                    IsInboxDuplicate(inboxState),
                    cancellationToken).ConfigureAwait(false);
            }

            // Stage 2: load and guard the optimistic stream version.
            var aggregate = await aggregateLoader.LoadAsync(instanceId, cancellationToken).ConfigureAwait(false);
            if (GuardExpectedVersion(aggregate, expectedVersion) is { } conflictResult)
            {
                return await CompleteWithoutCommitAsync(
                    instanceId,
                    conflictResult,
                    inboxEventId,
                    commandType,
                    stopwatch,
                    activity,
                    inboxDuplicate: false,
                    cancellationToken).ConfigureAwait(false);
            }

            // Stage 3: decide and commit (one atomic durable commit per command).
            var activeWaitsById = aggregate.Snapshot.ActiveWaits.ToDictionary(wait => wait.WaitId);
            var decision = await decide(aggregate, cancellationToken).ConfigureAwait(false);

            var providerCommitAttempted = HasProviderCommit(decision, inboxEventId);
            var providerName = DurableCommandTelemetry.ProviderName(eventStore);
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

            // Stage 4: derive post-commit observed state and emit observations.
            var observed = DeriveObservedState(aggregate, decision, result, activity);
            var eventObservations = result.Outcome is DurableCommandOutcome.Committed
                ? DurableCommandTelemetry.CreateEventObservations(
                    decision.Events,
                    observed.DefinitionId,
                    activeWaitsById,
                    stopwatch.Elapsed)
                : [];
            DurableCommandTelemetry.RecordEventSpans(eventObservations, instanceId);
            DurableCommandTelemetry.RecordStepSpans(eventObservations, instanceId);

            return await telemetry.ObserveCommandCompletedAsync(
                instanceId,
                result,
                decision.Events.Count,
                decision.Checkpoint is not null,
                inboxEventId,
                cancellationToken,
                commandType,
                observed.DefinitionId,
                observed.DefinitionVersion,
                observed.Status,
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

    /// <summary>
    /// Completes a command that never reached the commit stage (inbox preflight hit or
    /// optimistic-version conflict): stops timing, tags the outcome, and observes it with
    /// zero events and no checkpoint.
    /// </summary>
    private async Task<DurableCommandResult> CompleteWithoutCommitAsync(
        InstanceId instanceId,
        DurableCommandResult result,
        EventId? inboxEventId,
        string commandType,
        Stopwatch stopwatch,
        Activity? activity,
        bool inboxDuplicate,
        CancellationToken cancellationToken)
    {
        stopwatch.Stop();
        activity?.SetTag(OrcaCoreDiagnostics.CommandOutcomeKey, result.Outcome.ToString());
        return await telemetry.ObserveCommandCompletedAsync(
            instanceId,
            result,
            eventCount: 0,
            checkpointWritten: false,
            inboxEventId,
            cancellationToken,
            commandType,
            definitionId: null,
            definitionVersion: null,
            status: null,
            stopwatch.Elapsed,
            inboxDuplicate: inboxDuplicate)
            .ConfigureAwait(false);
    }

    /// <summary>
    /// Rejects a command decided against a stale read: another mutator moved the stream past
    /// the version the driver observed. Rejecting here keeps committed transitions
    /// exactly-once (DU-022). Returns null when the stream is at the expected version.
    /// </summary>
    private static DurableCommandResult? GuardExpectedVersion(
        DurableWorkflowAggregate aggregate,
        StreamVersion? expectedVersion)
    {
        if (expectedVersion is not { } requiredVersion || aggregate.StreamVersion == requiredVersion)
        {
            return null;
        }

        return new DurableCommandResult(
            DurableCommandOutcome.Conflict,
            $"Expected stream version {requiredVersion.Value} but found {aggregate.StreamVersion.Value}.",
            aggregate.StreamVersion);
    }

    /// <summary>
    /// Derives the definition/status the observer should see: the committed projection
    /// snapshot when the commit landed, otherwise the aggregate's decision-time view; tags
    /// the command activity with whichever won.
    /// </summary>
    private static ObservedInstanceState DeriveObservedState(
        DurableWorkflowAggregate aggregate,
        DurableDecision decision,
        DurableCommandResult result,
        Activity? activity)
    {
        var aggregateSnapshot = aggregate.Snapshot;
        var committedSnapshot = result.Outcome == DurableCommandOutcome.Committed
            ? aggregate
                .CreateProjectionWrites(decision.Events)
                .Select(write => write.InstanceSnapshot)
                .FirstOrDefault(snapshot => snapshot is not null)
            : null;
        var observed = new ObservedInstanceState(
            committedSnapshot?.DefinitionId ?? aggregateSnapshot.DefinitionId,
            committedSnapshot?.DefinitionVersion ?? aggregateSnapshot.DefinitionVersion,
            committedSnapshot?.Status ?? aggregateSnapshot.Status);

        if (observed.DefinitionId is { } definitionId)
        {
            activity?.SetTag(OrcaCoreDiagnostics.DefinitionIdKey, definitionId.ToString());
        }

        if (observed.DefinitionVersion is { } definitionVersion)
        {
            activity?.SetTag(OrcaCoreDiagnostics.DefinitionVersionKey, definitionVersion.ToString());
        }

        if (observed.Status is { } status)
        {
            activity?.SetTag(OrcaCoreDiagnostics.StatusKey, status.ToString());
        }

        return observed;
    }

    private readonly record struct ObservedInstanceState(
        DefinitionId? DefinitionId,
        DefinitionVersion? DefinitionVersion,
        WorkflowStatus? Status);

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
