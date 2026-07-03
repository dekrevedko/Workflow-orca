namespace OrcaCore.Abstractions.Errors;

/// <summary>
/// Optimistic concurrency or serialized-mutation conflict on an instance. The caller should
/// retry with the latest version or resolve the conflicting command.
/// </summary>
public class WorkflowConcurrencyException(string message) : OrcaCoreException(message);
