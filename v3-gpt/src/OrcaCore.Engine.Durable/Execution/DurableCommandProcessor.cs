using System.Diagnostics;
using OrcaCore.Abstractions.Durable;
using OrcaCore.Abstractions.Ids;
using OrcaCore.Abstractions.Instances;
using OrcaCore.Abstractions.Primitives;
using OrcaCore.Abstractions.Providers;
using OrcaCore.Engine.Durable.Aggregates;

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
            cancellationToken);
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
        EventId? inboxEventId = null)
    {
        return await RunInLaneAsync(
            instanceId,
            (aggregate, _) => Task.FromResult(decide(aggregate)),
            cancellationToken,
            inboxEventId).ConfigureAwait(false);
    }

    private async Task<DurableCommandResult> RunInLaneAsync(
        InstanceId instanceId,
        Func<DurableWorkflowAggregate, CancellationToken, Task<DurableDecision>> decide,
        CancellationToken cancellationToken,
        EventId? inboxEventId = null)
    {
        return await runtime.RunAsync(
            instanceId,
            token => ProcessCoreAsync(instanceId, decide, token, inboxEventId),
            cancellationToken).ConfigureAwait(false);
    }

    private async Task<DurableCommandResult> ProcessCoreAsync(
        InstanceId instanceId,
        Func<DurableWorkflowAggregate, CancellationToken, Task<DurableDecision>> decide,
        CancellationToken cancellationToken,
        EventId? inboxEventId)
    {
        var inboxState = await LoadInboxStateAsync(inboxEventId, cancellationToken).ConfigureAwait(false);
        if (DurableInboxPreflight.TryCreateResult(inboxState) is { } preflightResult)
        {
            return await ObserveCommandCompletedAsync(
                instanceId,
                preflightResult,
                eventCount: 0,
                checkpointWritten: false,
                inboxEventId,
                cancellationToken)
                .ConfigureAwait(false);
        }

        var aggregate = await aggregateLoader.LoadAsync(instanceId, cancellationToken).ConfigureAwait(false);
        var decision = await decide(aggregate, cancellationToken).ConfigureAwait(false);

        var result = await commitPipeline
            .CommitAsync(instanceId, aggregate, decision, inboxEventId, cancellationToken)
            .ConfigureAwait(false);
        return await ObserveCommandCompletedAsync(
            instanceId,
            result,
            decision.Events.Count,
            decision.Checkpoint is not null,
            inboxEventId,
            cancellationToken)
            .ConfigureAwait(false);
    }

    private async Task<DurableCommandResult> ObserveCommandCompletedAsync(
        InstanceId instanceId,
        DurableCommandResult result,
        int eventCount,
        bool checkpointWritten,
        EventId? inboxEventId,
        CancellationToken cancellationToken)
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
                        result.Message),
                    cancellationToken)
                .ConfigureAwait(false);
        }
        catch (Exception)
        {
            // Runtime observations are diagnostics; observer failures must not change command results.
        }

        return result;
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
