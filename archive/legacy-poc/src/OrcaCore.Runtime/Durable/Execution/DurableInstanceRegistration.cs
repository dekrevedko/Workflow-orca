using OrcaCore.Runtime.Durable.Persistence;

namespace OrcaCore.Runtime.Durable.Execution;

internal sealed class DurableInstanceRegistration(
    IWorkflowInstance instance,
    string definitionVersion,
    Func<IWorkflowInstance, PersistedInstance> persist,
    Func<IWorkflowInstance, DurableInstanceSnapshot> snapshot,
    Func<IWorkflowInstance, object> stateSnapshot,
    Action<IWorkflowInstance, PersistedInstance> restore,
    Func<IWorkflowInstance, EventEnvelope, WaitRecord, ICorrelationMutationSink, CancellationToken, Task<WorkflowExecutionReport>> resume)
{
    public IWorkflowInstance Instance { get; } = instance;

    public string DefinitionVersion { get; } = definitionVersion;

    public Func<IWorkflowInstance, PersistedInstance> Persist { get; } = persist;

    public Func<IWorkflowInstance, DurableInstanceSnapshot> Snapshot { get; } = snapshot;

    public Func<IWorkflowInstance, object> StateSnapshot { get; } = stateSnapshot;

    public Action<IWorkflowInstance, PersistedInstance> Restore { get; } = restore;

    public Func<IWorkflowInstance, EventEnvelope, WaitRecord, ICorrelationMutationSink, CancellationToken, Task<WorkflowExecutionReport>> Resume { get; } = resume;
}
