namespace OrcaCore.Abstractions.Errors;

/// <summary>
/// Definition version mismatch or unsupported version encountered during execution or
/// rehydration (DU-041).
/// </summary>
public class WorkflowVersionException(string message) : OrcaCoreException(message);
