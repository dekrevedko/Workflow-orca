namespace OrcaCore.Runtime.Engine.Exceptions;

public sealed class NoActiveWaitException(string eventName, string correlationId)
    : WorkflowRoutingException(
        $"No active wait found for this CorrelationId: EventName='{eventName}', CorrelationId='{correlationId}'.")
{
    public string EventName { get; } = eventName;

    public string CorrelationId { get; } = correlationId;
}
