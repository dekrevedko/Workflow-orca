using OrcaCore.Abstractions.Ids;
using OrcaCore.Abstractions.Instances;

namespace OrcaCore.Engine.Ephemeral.Execution;

internal interface IWorkflowInstance
{
    InstanceId InstanceId { get; }

    Type StateType { get; }

    object StateObject { get; }

    object CopyState();

    Type? OutputType => null;

    byte[]? CopyOutputPayload() => null;

    bool HasPublishedState { get; }

    void PublishState();

    LegacyWorkflowInstanceSnapshot Cancel(DateTimeOffset updatedAt);

    LegacyWorkflowInstanceSnapshot Terminate(DateTimeOffset updatedAt);

    LegacyWorkflowInstanceSnapshot MarkStuckIfNoProgress(DateTimeOffset now, TimeSpan threshold);

    bool TryTakeYieldContinuation(out Func<CancellationToken, Task>? continuation);

    CancellationTokenSource CreateLinkedExecutionToken(CancellationToken cancellationToken);

    bool IsCancellationRequested { get; }

    global::OrcaCore.WorkflowCancellationRequestStatus TryRequestCancellation(DateTimeOffset requestedAt);

    void SignalCancellation();

    LegacyWorkflowInstanceSnapshot ToSnapshot();

    LegacyWorkflowInstanceSnapshot GetPublishedSnapshot();
}
