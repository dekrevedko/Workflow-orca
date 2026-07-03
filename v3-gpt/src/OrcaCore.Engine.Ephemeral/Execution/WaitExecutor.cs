using OrcaCore.Abstractions.Ids;
using OrcaCore.Core.Definitions;

namespace OrcaCore.Engine.Ephemeral.Execution;

internal sealed class WaitExecutor<TState>(
    SuspensionScheduler<TState> suspensionScheduler,
    WorkflowFailureHandler<TState> failureHandler)
{
    internal async Task ExecuteAsync<TInput>(
        WaitNode<TState> waitNode,
        string nodeId,
        WorkflowInstance<TState> instance,
        SequenceExecutionContext<TState, TInput> context,
        int waitIndex,
        ISequenceExecutionEngine<TState> sequenceExecution,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(waitNode);
        ArgumentNullException.ThrowIfNull(instance);
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(sequenceExecution);

        CorrelationId correlationId;
        try
        {
            correlationId = waitNode.CorrelationSelector(instance.State);
        }
        catch (Exception exception)
            when (exception is not OperationCanceledException and not NotSupportedException)
        {
            failureHandler.Fail(instance, exception, nodeId);
            return;
        }

        await suspensionScheduler.RegisterWaitAsync(
            instance,
            waitNode.EventName,
            correlationId,
            waitNode.Timeout,
            context,
            nextIndex: waitIndex + 1,
            sequenceExecution,
            cancellationToken).ConfigureAwait(false);
    }
}
