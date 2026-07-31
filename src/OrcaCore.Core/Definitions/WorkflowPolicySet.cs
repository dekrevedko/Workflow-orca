namespace OrcaCore.Core.Definitions;

public sealed record WorkflowPolicySet(
    RetryPolicy? Retry = null,
    TimeoutPolicy? Timeout = null,
    bool Cancellation = false,
    string? PoolKey = null)
{
    public static WorkflowPolicySet Empty { get; } = new();

    internal WorkflowPolicySet WithRetry(int maxAttempts)
    {
        return WithRetry(maxAttempts, TimeSpan.Zero);
    }

    internal WorkflowPolicySet WithRetry(int maxAttempts, TimeSpan backoff)
    {
        return this with { Retry = new RetryPolicy(maxAttempts, backoff) };
    }

    internal WorkflowPolicySet WithTimeout(TimeSpan duration)
    {
        return this with { Timeout = new TimeoutPolicy(duration) };
    }

    internal WorkflowPolicySet WithCancellation()
    {
        return this with { Cancellation = true };
    }

    internal WorkflowPolicySet WithPoolKey(string poolKey)
    {
        return this with { PoolKey = poolKey };
    }
}

public sealed record RetryPolicy(int MaxAttempts, TimeSpan Backoff);

public sealed record TimeoutPolicy(TimeSpan Duration);
