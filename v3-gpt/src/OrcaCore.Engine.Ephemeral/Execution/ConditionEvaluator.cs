namespace OrcaCore.Engine.Ephemeral.Execution;

internal sealed class ConditionEvaluator<TState>(WorkflowFailureHandler<TState> failureHandler)
{
    internal bool TryEvaluate(
        WorkflowInstance<TState> instance,
        InterpreterRunState<TState> runState,
        Func<TState, bool> condition,
        string nodePath,
        bool deferFailures,
        out bool result)
    {
        try
        {
            result = condition(instance.State);
            return true;
        }
        catch (Exception exception) when (exception is not OperationCanceledException and not NotSupportedException)
        {
            if (deferFailures)
            {
                runState.DeferredFailure = exception;
            }
            else
            {
                failureHandler.Fail(instance, exception, nodePath);
            }

            result = false;
            return false;
        }
    }
}
