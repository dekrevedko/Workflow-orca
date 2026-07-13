namespace OrcaCore.EventDrivenPrototype.Engine;

internal sealed class PrototypeExecutionResult
{
    private PrototypeExecutionResult(object businessState, int nextStepIndex, string? waitEventName, string? waitCorrelationId, Exception? error, bool completed, bool yielded)
    {
        BusinessState = businessState;
        NextStepIndex = nextStepIndex;
        WaitEventName = waitEventName;
        WaitCorrelationId = waitCorrelationId;
        Error = error;
        IsCompleted = completed;
        IsYielded = yielded;
    }

    public object BusinessState { get; }
    public int NextStepIndex { get; }
    public string? WaitEventName { get; }
    public string? WaitCorrelationId { get; }
    public Exception? Error { get; }
    public bool IsCompleted { get; }
    public bool IsYielded { get; }

    public static PrototypeExecutionResult CreateCompleted(object businessState, int nextStepIndex)
        => new(businessState, nextStepIndex, null, null, null, true, false);

    public static PrototypeExecutionResult CreateWaiting(object businessState, int nextStepIndex, string eventName, string correlationId)
        => new(businessState, nextStepIndex, eventName, correlationId, null, false, false);

    public static PrototypeExecutionResult CreateFailed(object businessState, int nextStepIndex, Exception error)
        => new(businessState, nextStepIndex, null, null, error, false, false);

    public static PrototypeExecutionResult CreateYielded(object businessState, int nextStepIndex)
        => new(businessState, nextStepIndex, null, null, null, false, true);
}
