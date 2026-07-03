using System.Collections.Concurrent;
using System.Threading.Channels;
using OrcaCore.Abstractions.Ids;

namespace OrcaCore.Engine.Ephemeral.Execution;

internal sealed class InstanceExecutionLane
{
    private const int LaneCapacity = 1024;

    private readonly ConcurrentDictionary<InstanceId, InstanceLane> lanes = [];

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

        var workItem = new LaneWorkItem<T>(operation, cancellationToken);
        while (true)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var lane = lanes.GetOrAdd(instanceId, static (id, owner) => new InstanceLane(id, owner), this);
            if (await lane.TryEnqueueAsync(workItem, cancellationToken).ConfigureAwait(false))
            {
                return await workItem.Completion.Task.ConfigureAwait(false);
            }

            lanes.TryRemove(new KeyValuePair<InstanceId, InstanceLane>(instanceId, lane));
        }
    }

    private sealed class InstanceLane
    {
        private readonly Channel<ILaneWorkItem> channel;
        private readonly InstanceId instanceId;
        private readonly InstanceExecutionLane owner;
        private int accepting = 1;

        internal InstanceLane(InstanceId instanceId, InstanceExecutionLane owner)
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
                owner.lanes.TryRemove(new KeyValuePair<InstanceId, InstanceLane>(instanceId, this));
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
