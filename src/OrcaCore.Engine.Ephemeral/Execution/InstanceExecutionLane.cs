using System.Collections.Concurrent;
using System.Runtime.ExceptionServices;
using System.Threading.Channels;
using OrcaCore.Abstractions.Ids;
using OrcaCore.Hosting;

namespace OrcaCore.Engine.Ephemeral.Execution;

internal sealed class InstanceExecutionLane
{
    private const int LaneCapacity = 1024;

    private readonly ConcurrentDictionary<InstanceId, Lane> lanes = [];
    private readonly object creationGate = new();
    internal EphemeralHostLifetime? HostLifetime { get; set; }
    private readonly Action<InstanceId>? onLaneEvicted;
    private readonly Action<InstanceId>? onWorkItemEnqueued;

    internal InstanceExecutionLane(
        Action<InstanceId>? onWorkItemEnqueued = null,
        Action<InstanceId>? onLaneEvicted = null)
    {
        this.onWorkItemEnqueued = onWorkItemEnqueued;
        this.onLaneEvicted = onLaneEvicted;
    }

    internal int ActiveLaneCount => lanes.Count;

    internal async Task RunAsync(
        InstanceId instanceId,
        Func<CancellationToken, Task> operation,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(operation);

        await RunAsync<object?>(
            instanceId,
            async token =>
            {
                await operation(token).ConfigureAwait(false);
                return null;
            },
            cancellationToken).ConfigureAwait(false);
    }

    internal async Task<T> RunAsync<T>(
        InstanceId instanceId,
        Func<CancellationToken, Task<T>> operation,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(operation);

        using var admission = HostLifetime?.Own();
        var workItem = new LaneWorkItem<T>(operation, cancellationToken);
        while (true)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Lane lane;
            lock (creationGate)
            {
                if (!lanes.TryGetValue(instanceId, out lane!))
                {
                    lane = new Lane(instanceId, this);
                    lanes[instanceId] = lane;
                    lane.Start();
                }
            }
            if (await lane.TryEnqueueAsync(workItem, cancellationToken).ConfigureAwait(false))
            {
                try { onWorkItemEnqueued?.Invoke(instanceId); }
                catch (Exception error)
                {
                    var original = ExceptionDispatchInfo.Capture(error);
                    // Enqueue already transferred ownership to the lane. Observe
                    // that exact item before reporting the notification failure,
                    // so its caller can fence an instance initialized in flight.
                    try { await workItem.Completion.Task.ConfigureAwait(false); }
                    catch (Exception) { /* The earlier enqueue failure remains authoritative. */ }
                    original.Throw();
                }
                return await workItem.Completion.Task.ConfigureAwait(false);
            }

            TryRemoveLane(instanceId, lane);
        }
    }

    private bool TryRemoveLane(InstanceId instanceId, Lane lane)
    {
        var removed = lanes.TryRemove(new KeyValuePair<InstanceId, Lane>(instanceId, lane));
        if (removed)
        {
            onLaneEvicted?.Invoke(instanceId);
        }

        return removed;
    }

    private sealed class Lane
    {
        private readonly Channel<ILaneWorkItem> channel;
        private readonly InstanceId instanceId;
        private readonly InstanceExecutionLane owner;
        private int accepting = 1;
        private readonly EphemeralHostLifetime.Lease? ownership;

        internal Lane(InstanceId instanceId, InstanceExecutionLane owner)
        {
            this.instanceId = instanceId;
            this.owner = owner;
            ownership = owner.HostLifetime?.Own();
            channel = Channel.CreateBounded<ILaneWorkItem>(new BoundedChannelOptions(LaneCapacity)
            {
                AllowSynchronousContinuations = false,
                FullMode = BoundedChannelFullMode.Wait,
                SingleReader = true,
                SingleWriter = false
            });
        }

        internal void Start() => _ = ProcessAsync();

        internal async Task<bool> TryEnqueueAsync(
            ILaneWorkItem workItem,
            CancellationToken cancellationToken)
        {
            if (Volatile.Read(ref accepting) == 0)
            {
                return false;
            }

            try
            {
                await channel.Writer.WriteAsync(workItem, cancellationToken).ConfigureAwait(false);
                return true;
            }
            catch (ChannelClosedException)
            {
                return false;
            }
        }

        private async Task ProcessAsync()
        {
            try
            {
                while (await channel.Reader.WaitToReadAsync().ConfigureAwait(false))
                {
                    while (channel.Reader.TryRead(out var workItem))
                    {
                        await workItem.ExecuteAsync().ConfigureAwait(false);
                    }

                    if (await TryCloseWhenIdleAsync().ConfigureAwait(false))
                    {
                        return;
                    }
                }
            }
            finally
            {
                try { owner.TryRemoveLane(instanceId, this); }
                catch (Exception) { owner.HostLifetime?.Fault(); }
                finally { ownership?.Dispose(); }
            }
        }

        private async Task<bool> TryCloseWhenIdleAsync()
        {
            if (Interlocked.CompareExchange(ref accepting, 0, 1) != 1)
            {
                return false;
            }

            channel.Writer.TryComplete();
            while (channel.Reader.TryRead(out var workItem))
            {
                await workItem.ExecuteAsync().ConfigureAwait(false);
            }

            return true;
        }
    }

    private interface ILaneWorkItem
    {
        ValueTask ExecuteAsync();
    }

    private sealed class LaneWorkItem<T>(
        Func<CancellationToken, Task<T>> operation,
        CancellationToken cancellationToken) : ILaneWorkItem
    {
        internal TaskCompletionSource<T> Completion { get; } =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        public async ValueTask ExecuteAsync()
        {
            try
            {
                cancellationToken.ThrowIfCancellationRequested();
                var result = await operation(cancellationToken).ConfigureAwait(false);
                Completion.TrySetResult(result);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                Completion.TrySetCanceled(cancellationToken);
            }
            catch (Exception exception)
            {
                Completion.TrySetException(exception);
            }
        }
    }
}
