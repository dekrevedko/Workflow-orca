namespace OrcaCore;

/// <summary>Provides a stable application-owned dispatch failure.</summary>
public sealed class WorkflowEventDispatchFailure
{
    private WorkflowEventDispatchFailure(string code, string? detail)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(code);
        Code = code;
        Detail = detail;
    }

    public string Code { get; }

    public string? Detail { get; }

    public static WorkflowEventDispatchFailure Create(string code, string? detail = null) =>
        new(code, detail);
}

/// <summary>Describes the closed result of dispatching one outbound workflow event.</summary>
public abstract record WorkflowEventDispatchResult
{
    private protected WorkflowEventDispatchResult()
    {
    }

    public sealed record Succeeded : WorkflowEventDispatchResult;

    public sealed record RetryableFailure(WorkflowEventDispatchFailure Failure) : WorkflowEventDispatchResult
    {
        public WorkflowEventDispatchFailure Failure { get; } =
            Failure ?? throw new ArgumentNullException(nameof(Failure));
    }

    public sealed record PermanentFailure(WorkflowEventDispatchFailure Failure) : WorkflowEventDispatchResult
    {
        public WorkflowEventDispatchFailure Failure { get; } =
            Failure ?? throw new ArgumentNullException(nameof(Failure));
    }
}
