using System.Collections.Concurrent;
using OrcaCore.Abstractions.Durable;
using OrcaCore.Abstractions.Ids;
using OrcaCore.Abstractions.Providers;
using OrcaCore.Engine.Durable.Aggregates;

namespace OrcaCore.Engine.Durable.Execution;

internal sealed class DurableCommandProcessor(IWorkflowEventStore eventStore)
{
    private readonly ConcurrentDictionary<InstanceId, SemaphoreSlim> lanes = [];

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
        CancellationToken cancellationToken)
    {
        var lane = lanes.GetOrAdd(instanceId, _ => new SemaphoreSlim(1, 1));
        await lane.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            return await ProcessCoreAsync(instanceId, decide, cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            lane.Release();
        }
    }

    private async Task<DurableCommandResult> ProcessCoreAsync(
        InstanceId instanceId,
        Func<DurableWorkflowAggregate, DurableDecision> decide,
        CancellationToken cancellationToken)
    {
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
                    Checkpoint = decision.Checkpoint
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
            checkpoint.ContentType,
            [.. checkpoint.Payload]);
    }
}

internal enum DurableCommandOutcome
{
    Committed,
    Conflict,
    Evicted,
    NoOp
}

internal sealed record DurableCommandResult(
    DurableCommandOutcome Outcome,
    string? Message,
    StreamVersion StreamVersion,
    bool Evicted = false);
