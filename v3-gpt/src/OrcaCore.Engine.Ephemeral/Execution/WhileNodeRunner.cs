using OrcaCore.Core.Definitions;

namespace OrcaCore.Engine.Ephemeral.Execution;

internal sealed class WhileNodeRunner<TState>(ConditionEvaluator<TState> conditionEvaluator)
{
    internal async Task RunAsync<TInput>(
        WhileNode<TState> whileNode,
        SequenceExecutionContext<TState, TInput> parentContext,
        int whileIndex,
        ISequenceExecutionEngine<TState> sequenceExecution,
        CancellationToken cancellationToken)
    {
        while (true)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (!conditionEvaluator.TryEvaluate(
                    parentContext.RunState.Instance!,
                    parentContext.RunState,
                    whileNode.Condition,
                    whileNode.NodeId,
                    parentContext.DeferFailures,
                    out var whileResult))
            {
                return;
            }

            if (!whileResult)
            {
                await sequenceExecution.ContinueSequenceAsync(
                    parentContext,
                    whileIndex + 1,
                    cancellationToken).ConfigureAwait(false);
                return;
            }

            var bodyContext = parentContext.CreateNested(
                whileNode.Body,
                parentContext.BranchId,
                parentContext.ResumeEvent,
                continuationToken => RunAsync(
                    whileNode,
                    parentContext,
                    whileIndex,
                    sequenceExecution,
                    continuationToken));
            var completedBody = await sequenceExecution.RunSequenceAsync(
                bodyContext,
                startIndex: 0,
                cancellationToken,
                parentContext.DeferFailures).ConfigureAwait(false);
            if (!completedBody)
            {
                return;
            }
        }
    }
}
