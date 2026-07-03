namespace OrcaCore.Abstractions.Errors;

/// <summary>
/// Event routing failure such as no matching instance or ambiguous correlation resolution
/// (EV-012). The message states what the caller should do next.
/// </summary>
public class WorkflowRoutingException(string message) : OrcaCoreException(message);
