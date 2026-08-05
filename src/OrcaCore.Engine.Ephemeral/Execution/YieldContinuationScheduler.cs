using OrcaCore.Abstractions.Ids;
namespace OrcaCore.Engine.Ephemeral.Execution;

internal sealed class YieldContinuationScheduler(
    InstanceExecutionLane executionLane)
{
    internal void Schedule<TState, TInput>(
        WorkflowInstance<TState> instance,
        ISequenceExecutionEngine<TState> engine,
        SequenceExecutionContext<TState, TInput> context,
        int resumeIndex)
    {
        ArgumentNullException.ThrowIfNull(instance);
        ArgumentNullException.ThrowIfNull(engine);
        ArgumentNullException.ThrowIfNull(context);

        instance.ScheduleYield(continuationToken => engine.ContinueSequenceAsync(
            context,
            resumeIndex,
            continuationToken));
    }

    internal async Task<EphemeralWorkflowInstanceSnapshot> DrainAsync(
        IWorkflowInstance instance,
        InstanceId instanceId,
        Action<EphemeralWorkflowInstanceSnapshot> onSnapshotCommitted,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(instance);
        ArgumentNullException.ThrowIfNull(onSnapshotCommitted);

        while (true)
        {
            var result = await executionLane.RunAsync(
                    instanceId,
                    async laneCancellationToken =>
                    {
                        if (!instance.TryTakeYieldContinuation(out var continuation))
                        {
                            return new YieldDrainResult(instance.ToSnapshot(), false);
                        }

                        using var linkedCancellation = instance.CreateLinkedExecutionToken(laneCancellationToken);
                        await continuation!(linkedCancellation.Token).ConfigureAwait(false);
                        var committedSnapshot = instance.ToSnapshot();
                        onSnapshotCommitted(committedSnapshot);
                        return new YieldDrainResult(committedSnapshot, true);
                    },
                    cancellationToken).ConfigureAwait(false);

            if (!result.DrainedContinuation)
            {
                return result.Snapshot;
            }
        }
    }

    internal async Task<EphemeralWorkflowInstanceSnapshot> ResumeAsync<TState>(
        WorkflowInstance<TState> instance,
        InstanceId instanceId,
        Func<CancellationToken, Task> continuation,
        Action<EphemeralWorkflowInstanceSnapshot> onSnapshotCommitted,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(instance);
        ArgumentNullException.ThrowIfNull(continuation);
        ArgumentNullException.ThrowIfNull(onSnapshotCommitted);

        return await executionLane.RunAsync(
                instanceId,
                async laneCancellationToken =>
                {
                    using var linkedCancellation = instance.CreateLinkedExecutionToken(laneCancellationToken);
                    await continuation(linkedCancellation.Token).ConfigureAwait(false);
                    var snapshot = instance.ToSnapshot();
                    onSnapshotCommitted(snapshot);
                    return snapshot;
                },
                cancellationToken).ConfigureAwait(false);
    }

    private sealed record YieldDrainResult(
        EphemeralWorkflowInstanceSnapshot Snapshot,
        bool DrainedContinuation);
}
