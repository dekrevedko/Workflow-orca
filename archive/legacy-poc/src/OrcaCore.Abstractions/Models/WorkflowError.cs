namespace OrcaCore.Abstractions.Models;

public sealed record WorkflowError
{
    private WorkflowError(
        Exception exception,
        string stepId,
        DateTimeOffset timestamp,
        string exceptionType,
        string message)
    {
        Exception = exception;
        StepId = stepId;
        Timestamp = timestamp;
        ExceptionType = exceptionType;
        Message = message;
    }

    public Exception Exception { get; }

    public string StepId { get; }

    public DateTimeOffset Timestamp { get; }

    public string ExceptionType { get; }

    public string Message { get; }

    public static WorkflowError FromException(
        Exception exception,
        string stepId,
        DateTimeOffset timestamp) =>
        new(
            exception,
            stepId,
            timestamp,
            exception.GetType().FullName ?? exception.GetType().Name,
            exception.Message);

    public static WorkflowError FromMetadata(
        string message,
        string exceptionType,
        string stepId,
        DateTimeOffset timestamp) =>
        new(
            new Exception(message),
            stepId,
            timestamp,
            exceptionType,
            message);
}
