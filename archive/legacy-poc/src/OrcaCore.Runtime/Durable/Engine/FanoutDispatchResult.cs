namespace OrcaCore.Runtime.Durable.Engine;

public sealed record FanoutDispatchResult(
    int AttemptedCount,
    int SucceededCount,
    int FailedCount,
    IReadOnlyList<FanoutInstanceResult> InstanceResults);
