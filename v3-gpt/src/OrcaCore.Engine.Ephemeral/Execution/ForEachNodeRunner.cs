using OrcaCore.Abstractions.Instances;
using OrcaCore.Core.Definitions;

namespace OrcaCore.Engine.Ephemeral.Execution;

internal sealed class ForEachNodeRunner<TState>(TimeProvider timeProvider)
{
    internal async Task RunAsync<TInput>(
        ForEachNode<TState> forEachNode,
        SequenceExecutionContext<TState, TInput> parentContext,
        int forEachIndex,
        ISequenceExecutionEngine<TState> sequenceExecution,
        CancellationToken cancellationToken)
    {
        IReadOnlyList<ForEachWorkItemSnapshot> workItems;
        try
        {
            workItems = forEachNode.MaterializeWorkItems(parentContext.RunState.Instance!.State);
        }
        catch (Exception exception) when (exception is not OperationCanceledException and not NotSupportedException)
        {
            sequenceExecution.Fail(parentContext.RunState.Instance!, exception, forEachNode.NodeId);
            return;
        }

        var group = parentContext.RunState.Instance!.RecordForEachGroup(
            forEachNode.NodeId,
            workItems,
            forEachNode.MaxConcurrency,
            timeProvider.GetUtcNow());
        if (workItems.Count == 0)
        {
            await sequenceExecution.ContinueSequenceAsync(
                parentContext,
                forEachIndex + 1,
                cancellationToken).ConfigureAwait(false);
            return;
        }

        var scheduler = new ForEachWorkScheduler<TState, TInput>(
            forEachNode,
            parentContext,
            group,
            workItems,
            forEachIndex,
            sequenceExecution,
            timeProvider);
        await scheduler.DispatchAvailableAsync(cancellationToken).ConfigureAwait(false);
    }
}
