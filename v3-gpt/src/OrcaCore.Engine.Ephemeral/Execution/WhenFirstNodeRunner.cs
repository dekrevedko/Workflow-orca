using OrcaCore.Abstractions.Instances;
using OrcaCore.Core.Definitions;

namespace OrcaCore.Engine.Ephemeral.Execution;

internal sealed class WhenFirstNodeRunner<TState>(
    ISequenceExecutionEngine<TState> sequenceExecution,
    TimeProvider timeProvider)
{
    internal async Task RunAsync<TInput>(
        WhenFirstNode<TState> whenFirstNode,
        SequenceExecutionContext<TState, TInput> parentContext,
        int whenFirstIndex,
        CancellationToken cancellationToken)
    {
        var join = new WhenFirstJoin<TState>(
            whenFirstNode,
            parentContext.RunState.Instance!,
            () => timeProvider.GetUtcNow(),
            continuationToken => sequenceExecution.ContinueSequenceAsync(
                parentContext,
                whenFirstIndex + 1,
                continuationToken));

        foreach (var branch in whenFirstNode.Branches)
        {
            if (join.ShouldStopScheduling)
            {
                return;
            }

            var branchContext = parentContext.CreateNested(
                branch.Sequence,
                branch.BranchId,
                parentContext.ResumeEvent,
                continuationToken => join.BranchCompletedAsync(branch, continuationToken));
            var completed = await sequenceExecution.RunSequenceAsync(
                branchContext,
                startIndex: 0,
                cancellationToken).ConfigureAwait(false);
            if (completed)
            {
                await join.BranchCompletedAsync(branch, cancellationToken).ConfigureAwait(false);
            }

            if (parentContext.RunState.Instance!.Status == WorkflowStatus.Failed)
            {
                return;
            }
        }
    }
}
