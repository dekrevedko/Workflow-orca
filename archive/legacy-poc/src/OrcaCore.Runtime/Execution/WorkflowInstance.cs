namespace OrcaCore.Runtime.Execution;

internal sealed class WorkflowInstance<TState>(
    string instanceId,
    string definitionId,
    TState businessState,
    RuntimeState? runtimeState = null)
    : IWorkflowInstance
{
    public string InstanceId { get; } = instanceId;
    public string DefinitionId { get; } = definitionId;
    public TState BusinessState { get; private set; } = businessState;
    public RuntimeState RuntimeState { get; private set; } = runtimeState ?? new();
    public int ConcurrencyToken { get; set; }
    public SemaphoreSlim ExecutionLock { get; } = new(1, 1);

    public void RestoreFrom(WorkflowInstance<TState> restored)
    {
        BusinessState = restored.BusinessState;
        RuntimeState = restored.RuntimeState;
        ConcurrencyToken = restored.ConcurrencyToken;
    }
}
