using OrcaCore.Abstractions.Instances;
using OrcaCore.Core.Definitions;

namespace OrcaCore.Engine.Ephemeral.Execution;

internal sealed class ParallelNodeRunner<TState>
{
    internal async Task RunAsync<TInput>(
        ParallelNode<TState> parallelNode,
        SequenceExecutionContext<TState, TInput> parentContext,
        int parallelIndex,
        ISequenceExecutionEngine<TState> sequenceExecution,
        CancellationToken cancellationToken)
    {
        var join = new ParallelJoin(
            parallelNode.Branches.Count,
            continuationToken => sequenceExecution.ContinueSequenceAsync(
                parentContext,
                parallelIndex + 1,
                continuationToken));

        foreach (var branch in parallelNode.Branches)
        {
            var branchContext = parentContext.CreateNested(
                branch.Sequence,
                branch.BranchId,
                parentContext.ResumeEvent,
                continuationToken => join.BranchCompletedAsync(continuationToken));
            var completed = await sequenceExecution.RunSequenceAsync(
                branchContext,
                startIndex: 0,
                cancellationToken).ConfigureAwait(false);
            if (completed)
            {
                await join.BranchCompletedAsync(cancellationToken).ConfigureAwait(false);
            }

            if (parentContext.RunState.Instance!.Status == WorkflowStatus.Failed)
            {
                return;
            }
        }
    }
}
