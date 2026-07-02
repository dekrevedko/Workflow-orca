using System.Collections.Concurrent;
using OrcaCore.Abstractions.Durable;
using OrcaCore.Abstractions.Ids;
using OrcaCore.Abstractions.Primitives;
using OrcaCore.Abstractions.Providers;
using OrcaCore.Engine.Durable.Aggregates;

namespace OrcaCore.Engine.Durable.Execution;

internal sealed class DurableCommandProcessor(IWorkflowEventStore eventStore)
{
    private readonly ConcurrentDictionary<InstanceId, SemaphoreSlim> lanes = [];
    private readonly IWorkflowInboxStore? inboxStore = eventStore as IWorkflowInboxStore;

    internal Task<DurableCommandResult> ProcessAsync(
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
        Func<DurableWorkflowAggregate, DurableDecision> decide,
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
        var decision = decide(aggregate);

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
                    InboxOperations = CreateInboxOperations(inboxEventId, decision)
                },
                cancellationToken)
            .ConfigureAwait(false);

        return appendResult.Match(
            success => new DurableCommandResult(
                DurableCommandOutcome.Committed,
                null,
                success.NewVersion,
                decision.EvictAfterCommit),
            error => new DurableCommandResult(
                DurableCommandOutcome.Conflict,
                error.Message,
                aggregate.StreamVersion));
    }

    private static DurableAggregateCheckpoint ToAggregateCheckpoint(CheckpointWrite checkpoint)
    {
        return new DurableAggregateCheckpoint(
            checkpoint.InstanceId,
            checkpoint.StreamVersion,
            checkpoint.DefinitionId,
            checkpoint.DefinitionVersion,
            checkpoint.Status,
            checkpoint.LastStepPath,
            checkpoint.ErrorSummary,
            checkpoint.OutcomeName,
            [],
            [],
            checkpoint.ContentType,
            [.. checkpoint.Payload]);
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
}

internal enum DurableCommandOutcome
{
    Committed,
    Conflict,
    Evicted,
    Poisoned,
    NoOp
}

internal sealed record DurableCommandResult(
    DurableCommandOutcome Outcome,
    string? Message,
    StreamVersion StreamVersion,
    bool Evicted = false);
