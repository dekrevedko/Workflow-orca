using OrcaCore.Abstractions.Ids;
using OrcaCore.Core.Concurrency;

namespace OrcaCore.Engine.Durable.Execution;

internal sealed class DurableInstanceCommandLane
{
    private readonly InstanceLane lane = new();

    internal int ActiveLaneCount => lane.ActiveLaneCount;

    internal async Task<T> RunAsync<T>(
        InstanceId instanceId,
        Func<CancellationToken, Task<T>> operation,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(operation);

        return await lane.RunAsync(instanceId, operation, cancellationToken).ConfigureAwait(false);
    }
}
