using OrcaCore.Abstractions.Ids;
using OrcaCore.Abstractions.Instances;

namespace OrcaCore.Engine.Ephemeral.Execution;

internal interface IWorkflowInstance
{
    InstanceId InstanceId { get; }

    Type StateType { get; }

    object StateObject { get; }

    WorkflowInstanceSnapshot Cancel(DateTimeOffset updatedAt);

    WorkflowInstanceSnapshot Terminate(DateTimeOffset updatedAt);

    bool TryTakeYieldContinuation(out Func<CancellationToken, Task>? continuation);

    WorkflowInstanceSnapshot ToSnapshot();
}
