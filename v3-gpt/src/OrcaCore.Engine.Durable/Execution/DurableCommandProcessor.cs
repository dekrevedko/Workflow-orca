using System.Collections.Concurrent;
using System.Text.Json;
using OrcaCore.Abstractions.Durable;
using OrcaCore.Abstractions.Ids;
using OrcaCore.Abstractions.Instances;
using OrcaCore.Abstractions.Primitives;
using OrcaCore.Abstractions.Providers;
using OrcaCore.Engine.Durable.Aggregates;

namespace OrcaCore.Engine.Durable.Execution;

public sealed class DurableCommandProcessor(IWorkflowEventStore eventStore, IResourcePoolStore? resourcePoolStore = null)
{
    private readonly ConcurrentDictionary<InstanceId, SemaphoreSlim> lanes = [];
    private readonly IWorkflowInboxStore? inboxStore = eventStore as IWorkflowInboxStore;

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
        var lane = lanes.GetOrAdd(instanceId, _ => new SemaphoreSlim(1, 1));
        await lane.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            return await ProcessCoreAsync(instanceId, decide, cancellationToken, inboxEventId).ConfigureAwait(false);
        }
        finally
        {
            lane.Release();
        }
    }

    private async Task<DurableCommandResult> ProcessCoreAsync(
        InstanceId instanceId,
        Func<DurableWorkflowAggregate, CancellationToken, Task<DurableDecision>> decide,
        CancellationToken cancellationToken,
        EventId? inboxEventId)
    {
        var inboxState = await LoadInboxStateAsync(inboxEventId, cancellationToken).ConfigureAwait(false);
        if (inboxState.HasValue &&
            inboxState.Value is
                InboxRecordState.Applied or
                InboxRecordState.DuplicateIgnored or
                InboxRecordState.DiscardedOnResume)
        {
            return new DurableCommandResult(
                DurableCommandOutcome.NoOp,
                "Inbound event was already applied.",
                StreamVersion.Empty);
        }

        if (inboxState.HasValue && inboxState.Value == InboxRecordState.Poisoned)
        {
            return new DurableCommandResult(
                DurableCommandOutcome.Poisoned,
                "Inbound event was previously recorded as poisoned.",
                StreamVersion.Empty);
        }

        var checkpointOption = await eventStore
            .LoadCheckpointAsync(instanceId, cancellationToken)
            .ConfigureAwait(false);
        var checkpointVersion = checkpointOption.HasValue
            ? checkpointOption.Value.StreamVersion
            : StreamVersion.Empty;
        var tail = await eventStore
            .LoadTailAsync(new WorkflowStreamId(instanceId), checkpointVersion, cancellationToken)
            .ConfigureAwait(false);
        var aggregate = DurableWorkflowAggregate.Rehydrate(
            checkpointOption.HasValue ? ToAggregateCheckpoint(checkpointOption.Value) : null,
            tail);
        var decision = await decide(aggregate, cancellationToken).ConfigureAwait(false);

        if (decision.Events.Count == 0 && decision.Checkpoint is null)
        {
            if (inboxEventId is { } poisonedEventId)
            {
                return await CommitInboxOnlyAsync(
                    instanceId,
                    aggregate.StreamVersion,
                    poisonedEventId,
                    InboxRecordState.Poisoned,
                    DurableCommandOutcome.Poisoned,
                    "No active wait matched the inbound event.",
                    cancellationToken).ConfigureAwait(false);
            }

            if (decision.EvictAfterCommit)
            {
                return new DurableCommandResult(
                    DurableCommandOutcome.Evicted,
                    "Instance is evictable from hot memory.",
                    aggregate.StreamVersion,
                    true);
            }

            return new DurableCommandResult(
                DurableCommandOutcome.NoOp,
                "Command produced no durable events.",
                aggregate.StreamVersion);
        }

        var appendResult = await eventStore
            .AppendAsync(
                new ProviderCommitBatch
                {
                    StreamId = new WorkflowStreamId(instanceId),
                    ExpectedVersion = aggregate.StreamVersion,
                    Events = decision.Events,
                    Checkpoint = decision.Checkpoint,
                    InboxOperations = CreateInboxOperations(inboxEventId, decision),
                    OutboxRecords = CreateOutboxRecords(decision.Events),
                    ProjectionOperations = aggregate.CreateProjectionWrites(decision.Events)
                },
                cancellationToken)
            .ConfigureAwait(false);

        if (appendResult.IsFailure)
        {
            return new DurableCommandResult(
                DurableCommandOutcome.Conflict,
                appendResult.Error.Message,
                aggregate.StreamVersion);
        }

        await ReleaseResourcePoolTicketsAsync(decision.Events, cancellationToken).ConfigureAwait(false);
        return new DurableCommandResult(
            DurableCommandOutcome.Committed,
            null,
            appendResult.Value.NewVersion,
            decision.EvictAfterCommit);
    }

    private static DurableAggregateCheckpoint ToAggregateCheckpoint(CheckpointWrite checkpoint)
    {
        return new DurableAggregateCheckpoint(
            checkpoint.InstanceId,
            checkpoint.StreamVersion,
            checkpoint.ParentInstanceId,
            checkpoint.RootInstanceId,
            checkpoint.DefinitionId,
            checkpoint.DefinitionVersion,
            checkpoint.Status,
            null,
            null,
            checkpoint.LastStepPath,
            checkpoint.ErrorSummary,
            checkpoint.OutcomeName,
            checkpoint.ContinueAsNewGeneration,
            [],
            [],
            [],
            [],
            [],
            [],
            checkpoint.ContentType,
            [.. checkpoint.Payload]);
    }

    private static IReadOnlyList<OutboxWrite> CreateOutboxRecords(IReadOnlyList<WorkflowEvent> events)
    {
        return CreateLifecycleOutboxRecords(events)
            .Concat(CreateChildStartOutboxRecords(events))
            .Concat(CreateResidualOutboxRecords(events))
            .Concat(CreateExternalJobOutboxRecords(events))
            .ToArray();
    }

    private static IReadOnlyList<InboxWrite> CreateInboxOperations(
        EventId? inboxEventId,
        DurableDecision decision)
    {
        return inboxEventId is { } eventId
            ? [new InboxWrite(eventId, InboundDeliveryState(decision)), .. decision.InboxOperations]
            : decision.InboxOperations;
    }

    private static InboxRecordState InboundDeliveryState(DurableDecision decision)
    {
        return decision.Events.Any(workflowEvent => workflowEvent is WorkflowDeliveryBufferedEvent)
            ? InboxRecordState.Received
            : InboxRecordState.Applied;
    }

    private static IReadOnlyList<OutboxWrite> CreateLifecycleOutboxRecords(IReadOnlyList<WorkflowEvent> events)
    {
        return events
            .SelectMany(ToLifecycleEvents)
            .Select(lifecycleEvent => new OutboxWrite(
                OutboxRecordId.New(),
                "lifecycle-event",
                JsonSerializer.SerializeToUtf8Bytes(lifecycleEvent)))
            .ToArray();
    }

    private static IReadOnlyList<OutboxWrite> CreateChildStartOutboxRecords(IReadOnlyList<WorkflowEvent> events)
    {
        var singleChildren = events
            .OfType<WorkflowChildScheduledEvent>()
            .Select(child => new OutboxWrite(
                OutboxRecordId.New(),
                "child-start",
                JsonSerializer.SerializeToUtf8Bytes(new StartWorkflowCommand
                {
                    CommandId = new CommandId(child.EventId.Value),
                    InstanceId = child.ChildInstanceId,
                    RequestedAt = child.OccurredAt,
                    ParentInstanceId = child.InstanceId,
                    RootInstanceId = child.RootInstanceId ?? child.InstanceId,
                    DefinitionId = child.ChildDefinitionId,
                    DefinitionVersion = child.ChildDefinitionVersion
                })))
            .ToArray();
        var childGroups = events
            .OfType<WorkflowChildrenScheduledEvent>()
            .SelectMany(group => group.Children.Take(group.InitialDispatchCount).Select(child => new OutboxWrite(
                OutboxRecordId.New(),
                "child-start",
                JsonSerializer.SerializeToUtf8Bytes(new StartWorkflowCommand
                {
                    CommandId = new CommandId(child.ChildInstanceId.Value),
                    InstanceId = child.ChildInstanceId,
                    RequestedAt = group.OccurredAt,
                    ParentInstanceId = group.InstanceId,
                    RootInstanceId = group.RootInstanceId ?? group.InstanceId,
                    DefinitionId = group.ChildDefinitionId,
                    DefinitionVersion = group.ChildDefinitionVersion
                }))))
            .ToArray();
        var childCompensations = events
            .OfType<WorkflowChildCompensationScheduledEvent>()
            .SelectMany(group => group.Compensations.Select(compensation => new OutboxWrite(
                OutboxRecordId.New(),
                "child-compensation-start",
                JsonSerializer.SerializeToUtf8Bytes(new StartWorkflowCommand
                {
                    CommandId = new CommandId(compensation.CompensationInstanceId.Value),
                    InstanceId = compensation.CompensationInstanceId,
                    RequestedAt = group.OccurredAt,
                    ParentInstanceId = group.InstanceId,
                    RootInstanceId = group.RootInstanceId ?? group.InstanceId,
                    DefinitionId = group.CompensationDefinitionId,
                    DefinitionVersion = group.CompensationDefinitionVersion
                }))))
            .ToArray();

        return [.. singleChildren, .. childGroups, .. childCompensations];
    }

    private static IReadOnlyList<OutboxWrite> CreateResidualOutboxRecords(IReadOnlyList<WorkflowEvent> events)
    {
        return events
            .OfType<WorkflowChildResidualIntentRecordedEvent>()
            .Select(residual => new OutboxWrite(
                OutboxRecordId.New(),
                "external-message",
                JsonSerializer.SerializeToUtf8Bytes(residual)))
            .ToArray();
    }

    private static IReadOnlyList<OutboxWrite> CreateExternalJobOutboxRecords(IReadOnlyList<WorkflowEvent> events)
    {
        var starts = events
            .OfType<WorkflowExternalJobStartedEvent>()
            .Select(started => new OutboxWrite(
                OutboxRecordId.New(),
                "external-job-start",
                JsonSerializer.SerializeToUtf8Bytes(started)))
            .ToArray();
        var stops = events
            .OfType<WorkflowExternalJobStopRequestedEvent>()
            .Select(stop => new OutboxWrite(
                OutboxRecordId.New(),
                "external-job-stop",
                JsonSerializer.SerializeToUtf8Bytes(stop)))
            .ToArray();

        return [.. starts, .. stops];
    }

    private static IEnumerable<LifecycleEventSnapshot> ToLifecycleEvents(WorkflowEvent workflowEvent)
    {
        return workflowEvent switch
        {
            WorkflowStartedEvent started =>
            [
                DurableLifecycleEvent(started, "InstanceStarted", null, WorkflowStatus.Running)
            ],
            WorkflowStepCompletedEvent stepCompleted =>
            [
                DurableLifecycleEvent(stepCompleted, "StepCompleted", stepCompleted.StepPath, WorkflowStatus.Running)
            ],
            WorkflowStepFailedEvent stepFailed =>
            [
                DurableLifecycleEvent(stepFailed, "StepFailed", stepFailed.StepPath, WorkflowStatus.Failed)
            ],
            WorkflowWaitRegisteredEvent waitRegistered =>
            [
                DurableLifecycleEvent(waitRegistered, "InstanceSuspended", null, WorkflowStatus.Waiting)
            ],
            WorkflowWaitMatchedEvent waitMatched =>
            [
                DurableLifecycleEvent(waitMatched, "InstanceResumed", null, WorkflowStatus.Running)
            ],
            WorkflowTimerScheduledEvent timerScheduled =>
            [
                DurableLifecycleEvent(timerScheduled, "InstanceSuspended", null, WorkflowStatus.Waiting)
            ],
            WorkflowTimerFiredEvent timerFired =>
            [
                DurableLifecycleEvent(timerFired, "InstanceResumed", null, WorkflowStatus.Running)
            ],
            WorkflowPausedEvent paused =>
            [
                DurableLifecycleEvent(paused, "InstancePaused", null, WorkflowStatus.Paused)
            ],
            WorkflowResumedEvent resumed =>
            [
                DurableLifecycleEvent(resumed, "InstanceResumed", null, WorkflowStatus.Running)
            ],
            WorkflowCompletedEvent completed =>
            [
                DurableLifecycleEvent(completed, "InstanceCompleted", null, WorkflowStatus.Completed)
            ],
            WorkflowTerminalEvent { Status: WorkflowStatus.Failed } terminal =>
            [
                DurableLifecycleEvent(terminal, "InstanceFailed", null, WorkflowStatus.Failed)
            ],
            WorkflowTerminalEvent { Status: WorkflowStatus.Cancelled } terminal =>
            [
                DurableLifecycleEvent(terminal, "InstanceCancelled", null, WorkflowStatus.Cancelled)
            ],
            WorkflowTerminalEvent { Status: WorkflowStatus.Terminated } terminal =>
            [
                DurableLifecycleEvent(terminal, "InstanceTerminated", null, WorkflowStatus.Terminated)
            ],
            WorkflowTerminalEvent { Status: WorkflowStatus.Compensated } terminal =>
            [
                DurableLifecycleEvent(terminal, "InstanceCompensated", null, WorkflowStatus.Compensated)
            ],
            WorkflowTerminalEvent { Status: WorkflowStatus.CompensationFailed } terminal =>
            [
                DurableLifecycleEvent(terminal, "InstanceCompensationFailed", null, WorkflowStatus.CompensationFailed)
            ],
            _ => []
        };
    }

    private static LifecycleEventSnapshot DurableLifecycleEvent(
        WorkflowEvent workflowEvent,
        string eventName,
        string? stepPath,
        WorkflowStatus status)
    {
        return new LifecycleEventSnapshot
        {
            InstanceId = workflowEvent.InstanceId,
            EventName = eventName,
            StepPath = stepPath,
            Status = status,
            OccurredAt = workflowEvent.OccurredAt,
            Durable = true
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

    private async Task<DurableCommandResult> CommitInboxOnlyAsync(
        InstanceId instanceId,
        StreamVersion expectedVersion,
        EventId eventId,
        InboxRecordState state,
        DurableCommandOutcome successOutcome,
        string successMessage,
        CancellationToken cancellationToken)
    {
        var appendResult = await eventStore
            .AppendAsync(
                new ProviderCommitBatch
                {
                    StreamId = new WorkflowStreamId(instanceId),
                    ExpectedVersion = expectedVersion,
                    InboxOperations = [new InboxWrite(eventId, state)]
                },
                cancellationToken)
            .ConfigureAwait(false);

        return appendResult.Match(
            success => new DurableCommandResult(successOutcome, successMessage, success.NewVersion),
            error => new DurableCommandResult(DurableCommandOutcome.Conflict, error.Message, expectedVersion));
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

    private async Task ReleaseResourcePoolTicketsAsync(
        IReadOnlyList<WorkflowEvent> events,
        CancellationToken cancellationToken)
    {
        foreach (var released in events.OfType<WorkflowResourcePoolReleasedEvent>())
        {
            await RequiredResourcePoolStore()
                .ReleaseAsync(
                    new ResourcePoolReleaseRequest(released.InstanceId, released.HolderKey, released.OccurredAt),
                    cancellationToken)
                .ConfigureAwait(false);
        }
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
