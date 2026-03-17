namespace OrcaCore.Runtime.Durable.Persistence;

public sealed class ConcurrencyException(string message) : WorkflowStoreException(message);
