namespace OrcaCore.Abstractions;

public sealed record WorkflowError(
    Exception Exception,
    string StepId,
    DateTimeOffset Timestamp);
