using System.Collections.Concurrent;
using OrcaCore.Abstractions.Ids;

namespace OrcaCore.Engine.Ephemeral.Execution;

internal sealed class InstanceExecutionLane
{
    private readonly ConcurrentDictionary<InstanceId, SemaphoreSlim> lanes = [];

    internal async Task RunAsync(
        InstanceId instanceId,
        Func<CancellationToken, Task> operation,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(operation);

        var lane = lanes.GetOrAdd(instanceId, _ => new SemaphoreSlim(1, 1));
        await lane.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            await operation(cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            lane.Release();
        }
    }

    internal async Task<T> RunAsync<T>(
        InstanceId instanceId,
        Func<CancellationToken, Task<T>> operation,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(operation);

        var lane = lanes.GetOrAdd(instanceId, _ => new SemaphoreSlim(1, 1));
        await lane.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            return await operation(cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            lane.Release();
        }
    }
}
