namespace OrcaCore.Runtime.Execution;

internal interface IWorkflowInstance
{
    string InstanceId { get; }
    string DefinitionId { get; }
    RuntimeState RuntimeState { get; }
    int ConcurrencyToken { get; set; }
    SemaphoreSlim ExecutionLock { get; }
}
