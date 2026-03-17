namespace OrcaCore.Runtime.Durable.Engine;

public sealed record FanoutInstanceResult(
    string InstanceId,
    bool Succeeded,
    Exception? Error);
