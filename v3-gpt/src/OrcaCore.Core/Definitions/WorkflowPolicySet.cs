namespace OrcaCore.Core.Definitions;

internal sealed record WorkflowPolicySet(
    RetryPolicy? Retry = null,
    TimeoutPolicy? Timeout = null,
    bool Cancellation = false,
    string? PoolKey = null)
{
    internal static WorkflowPolicySet Empty { get; } = new();

    internal WorkflowPolicySet WithRetry(int maxAttempts)
    {
        return this with { Retry = new RetryPolicy(maxAttempts) };
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

internal sealed record RetryPolicy(int MaxAttempts);

internal sealed record TimeoutPolicy(TimeSpan Duration);
