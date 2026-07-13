namespace OrcaCore.Engine.Ephemeral.Execution;

internal sealed record WorkflowErrorDetails(
    string ErrorType,
    string Message,
    string StepPath,
    DateTimeOffset OccurredAt)
{
    internal string Summary => $"{ErrorType}: {Message} at {StepPath}";
}
