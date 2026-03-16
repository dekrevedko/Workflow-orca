namespace OrcaCore.Abstractions;

public abstract record StepResult
{
    private StepResult() { }

    public sealed record Completed() : StepResult;

    public sealed record Failed(Exception Error) : StepResult;

    public sealed record WaitForEvent(string EventName, string CorrelationId) : StepResult;

    public sealed record Yield() : StepResult;
}
