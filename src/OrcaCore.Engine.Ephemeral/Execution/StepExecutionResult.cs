using OrcaCore.Abstractions.Ids;

namespace OrcaCore.Engine.Ephemeral.Execution;

internal enum StepExecutionStatus
{
    Continue,
    Stop,
    Wait,
    Yield,
    Failed
}

internal sealed record StepExecutionResult(
    StepExecutionStatus Status,
    string? EventName,
    CorrelationId CorrelationId,
    Exception? Error)
{
    internal static StepExecutionResult Continue()
    {
        return new StepExecutionResult(StepExecutionStatus.Continue, null, default, null);
    }

    internal static StepExecutionResult Stop()
    {
        return new StepExecutionResult(StepExecutionStatus.Stop, null, default, null);
    }

    internal static StepExecutionResult Wait(string eventName, CorrelationId correlationId)
    {
        return new StepExecutionResult(StepExecutionStatus.Wait, eventName, correlationId, null);
    }

    internal static StepExecutionResult Yield()
    {
        return new StepExecutionResult(StepExecutionStatus.Yield, null, default, null);
    }

    internal static StepExecutionResult Failed(Exception exception)
    {
        return new StepExecutionResult(StepExecutionStatus.Failed, null, default, exception);
    }
}
