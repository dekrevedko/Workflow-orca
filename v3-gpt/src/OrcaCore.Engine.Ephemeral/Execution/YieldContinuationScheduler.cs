using OrcaCore.Abstractions.Ids;
using OrcaCore.Abstractions.Instances;
using OrcaCore.Engine.Ephemeral.Governance;

namespace OrcaCore.Engine.Ephemeral.Execution;

internal sealed class YieldContinuationScheduler(
    ResourceGovernanceCoordinator governance,
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

    internal async Task<WorkflowInstanceSnapshot> DrainAsync(
        IWorkflowInstance instance,
        InstanceId instanceId,
        Action<WorkflowInstanceSnapshot> onSnapshotCommitted,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(instance);
        ArgumentNullException.ThrowIfNull(onSnapshotCommitted);

        var snapshot = instance.ToSnapshot();
        while (instance.TryTakeYieldContinuation(out var continuation))
        {
            await using (await governance.EnterAdvancementAsync(cancellationToken).ConfigureAwait(false))
            {
                snapshot = await executionLane.RunAsync(
                    instanceId,
                    async laneCancellationToken =>
                    {
                        using var linkedCancellation = instance.CreateLinkedExecutionToken(laneCancellationToken);
                        await continuation!(linkedCancellation.Token).ConfigureAwait(false);
                        return instance.ToSnapshot();
                    },
                    cancellationToken).ConfigureAwait(false);
            }

            onSnapshotCommitted(snapshot);
        }

        return snapshot;
    }
}
