namespace OrcaCore.Abstractions.Errors;

/// <summary>
/// Illegal lifecycle trigger or transition rejected by the explicit status table (CR-030).
/// </summary>
public class WorkflowLifecycleException(string message) : OrcaCoreException(message);
