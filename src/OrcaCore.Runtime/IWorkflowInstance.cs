namespace OrcaCore.Runtime;

internal interface IWorkflowInstance
{
    string InstanceId { get; }
    string DefinitionId { get; }
    RuntimeState RuntimeState { get; }
    SemaphoreSlim ExecutionLock { get; }
}
