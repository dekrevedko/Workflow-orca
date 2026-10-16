namespace OrcaCore.Runtime.Durable.Persistence;

public sealed record ProjectionWorkItem(
    string ProjectionName,
    string WorkKind,
    DispatchPayload Payload);
