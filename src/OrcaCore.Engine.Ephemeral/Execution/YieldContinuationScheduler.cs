using OrcaCore.Abstractions.Ids;
namespace OrcaCore.Engine.Ephemeral.Execution;

internal sealed class YieldContinuationScheduler(
    InstanceExecutionLane executionLane)
{
    internal async Task<EphemeralWorkflowInstanceSnapshot> ReadSnapshotOnLaneAsync(
        IWorkflowInstance instance,
        InstanceId instanceId,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(instance);
        using var ownership = instance.Lifetime.Own();
        return await executionLane.RunAsync(
            instanceId,
            _ => Task.FromResult(instance.ToSnapshot()),
            cancellationToken).ConfigureAwait(false);
    }

    internal async Task<EphemeralWorkflowInstanceSnapshot> ResumeAsync<TState>(
        WorkflowInstance<TState> instance,
        InstanceId instanceId,
        Func<CancellationToken, Task> continuation,
        Action<EphemeralWorkflowInstanceSnapshot> onSnapshotCommitted,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(instance);
        ArgumentNullException.ThrowIfNull(continuation);
        ArgumentNullException.ThrowIfNull(onSnapshotCommitted);
        using var ownership = instance.Lifetime.Own();

        return await executionLane.RunAsync(
                instanceId,
                async laneCancellationToken =>
                {
                    if (instance.Lifetime.IsTerminal) return instance.ToSnapshot();
                    using var linkedCancellation = instance.CreateLinkedExecutionToken(laneCancellationToken);
                    await continuation(linkedCancellation.Token).ConfigureAwait(false);
                    var snapshot = instance.ToSnapshot();
                    onSnapshotCommitted(snapshot);
                    return snapshot;
                },
                cancellationToken).ConfigureAwait(false);
    }

}
