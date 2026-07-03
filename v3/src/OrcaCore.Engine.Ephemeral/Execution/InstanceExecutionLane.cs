using System.Collections.Concurrent;
using OrcaCore.Abstractions.Ids;

namespace OrcaCore.Engine.Ephemeral.Execution;

/// <summary>
/// Per-instance serialization primitive for the ephemeral engine (CR-040/CR-041/CR-042):
/// operations submitted for the same <see cref="InstanceId"/> run one at a time, in submission
/// order, while operations for different instances proceed independently. Never exposed
/// publicly (CR-021) - the engine facade is the only caller.
/// </summary>
internal sealed class InstanceExecutionLane
{
    private readonly ConcurrentDictionary<InstanceId, SemaphoreSlim> gates = new();

    /// <summary>
    /// Runs <paramref name="operation"/> exclusively for <paramref name="instanceId"/>: if another
    /// operation for the same instance is in flight, this call waits until it releases the lane.
    /// A throwing operation always releases the lane before the exception propagates, so a failed
    /// mutation cannot permanently block future operations on the same instance (CR-042/CR-043).
    /// </summary>
    public async ValueTask RunAsync(
        InstanceId instanceId,
        Func<ValueTask> operation,
        CancellationToken cancellationToken)
    {
        var gate = gates.GetOrAdd(instanceId, static _ => new SemaphoreSlim(1, 1));

        await gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            await operation().ConfigureAwait(false);
        }
        finally
        {
            gate.Release();
        }
    }
}
