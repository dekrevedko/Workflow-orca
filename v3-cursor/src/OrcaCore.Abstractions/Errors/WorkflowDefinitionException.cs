namespace OrcaCore.Abstractions.Errors;

/// <summary>
/// Workflow definition build or validation failure. The caller must fix the definition before
/// starting instances.
/// </summary>
public class WorkflowDefinitionException(string message) : OrcaCoreException(message);
