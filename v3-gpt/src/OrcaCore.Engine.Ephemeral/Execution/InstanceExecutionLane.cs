using OrcaCore.Abstractions.Ids;
using OrcaCore.Core.Concurrency;

namespace OrcaCore.Engine.Ephemeral.Execution;

internal sealed class InstanceExecutionLane
{
    private readonly InstanceLane lane;

    internal InstanceExecutionLane(Action<InstanceId>? onWorkItemEnqueued = null)
    {
        lane = new InstanceLane(onWorkItemEnqueued);
    }

    internal int ActiveLaneCount => lane.ActiveLaneCount;

    internal async Task RunAsync(
        InstanceId instanceId,
        Func<CancellationToken, Task> operation,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(operation);

        await lane.RunAsync(instanceId, operation, cancellationToken).ConfigureAwait(false);
    }

    internal async Task<T> RunAsync<T>(
        InstanceId instanceId,
        Func<CancellationToken, Task<T>> operation,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(operation);

        return await lane.RunAsync(instanceId, operation, cancellationToken).ConfigureAwait(false);
    }
}
