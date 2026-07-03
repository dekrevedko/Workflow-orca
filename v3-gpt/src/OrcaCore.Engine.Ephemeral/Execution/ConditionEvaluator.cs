namespace OrcaCore.Engine.Ephemeral.Execution;

internal sealed class ConditionEvaluator<TState>(WorkflowFailureHandler<TState> failureHandler)
{
    internal bool TryEvaluate(
        WorkflowInstance<TState> instance,
        Func<TState, bool> condition,
        string nodePath,
        out bool result)
    {
        try
        {
            result = condition(instance.State);
            return true;
        }
        catch (Exception exception) when (exception is not OperationCanceledException and not NotSupportedException)
        {
            failureHandler.Fail(instance, exception, nodePath);
            result = false;
            return false;
        }
    }
}
