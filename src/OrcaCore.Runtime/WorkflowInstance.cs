namespace OrcaCore.Runtime;

internal sealed class WorkflowInstance<TState>(string instanceId, string definitionId, TState businessState)
    : IWorkflowInstance
{
    public string InstanceId { get; } = instanceId;
    public string DefinitionId { get; } = definitionId;
    public TState BusinessState { get; } = businessState;
    public RuntimeState RuntimeState { get; } = new();
    public SemaphoreSlim ExecutionLock { get; } = new(1, 1);
}
