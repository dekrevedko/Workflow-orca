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

    WorkflowInstanceSnapshot MarkStuckIfNoProgress(DateTimeOffset now, TimeSpan threshold);

    bool TryTakeYieldContinuation(out Func<CancellationToken, Task>? continuation);

    CancellationTokenSource CreateLinkedExecutionToken(CancellationToken cancellationToken);

    void SignalCancellation();

    WorkflowInstanceSnapshot ToSnapshot();
}
