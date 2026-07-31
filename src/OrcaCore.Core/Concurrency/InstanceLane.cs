using System.Collections.Concurrent;
using System.Threading.Channels;
using OrcaCore.Abstractions.Ids;

namespace OrcaCore.Core.Concurrency;

public sealed class InstanceLane
{
    private const int LaneCapacity = 1024;

    private readonly ConcurrentDictionary<InstanceId, Lane> lanes = [];
    private readonly Action<InstanceId>? onLaneEvicted;
    private readonly Action<InstanceId>? onWorkItemEnqueued;

    public InstanceLane(
        Action<InstanceId>? onWorkItemEnqueued = null,
        Action<InstanceId>? onLaneEvicted = null)
    {
        this.onWorkItemEnqueued = onWorkItemEnqueued;
        this.onLaneEvicted = onLaneEvicted;
    }

    public int ActiveLaneCount => lanes.Count;

    public async Task RunAsync(
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

    public async Task<T> RunAsync<T>(
        InstanceId instanceId,
        Func<CancellationToken, Task<T>> operation,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(operation);

        var workItem = new LaneWorkItem<T>(operation, cancellationToken);
        while (true)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var lane = lanes.GetOrAdd(instanceId, static (id, owner) => new Lane(id, owner), this);
            if (await lane.TryEnqueueAsync(workItem, cancellationToken).ConfigureAwait(false))
            {
                onWorkItemEnqueued?.Invoke(instanceId);
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
        private readonly InstanceLane owner;
        private int accepting = 1;

        internal Lane(InstanceId instanceId, InstanceLane owner)
        {
            this.instanceId = instanceId;
            this.owner = owner;
            channel = Channel.CreateBounded<ILaneWorkItem>(new BoundedChannelOptions(LaneCapacity)
            {
                AllowSynchronousContinuations = false,
                FullMode = BoundedChannelFullMode.Wait,
                SingleReader = true,
                SingleWriter = false
            });
            _ = ProcessAsync();
        }

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
                owner.TryRemoveLane(instanceId, this);
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
