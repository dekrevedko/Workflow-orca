using System.Threading.Channels;

namespace OrcaCore.Runtime.Storage;

internal sealed class InstanceCommandLane : IAsyncDisposable
{
    private readonly Channel<QueuedCommand> _channel = Channel.CreateUnbounded<QueuedCommand>(
        new UnboundedChannelOptions
        {
            SingleReader = true,
            SingleWriter = false,
            AllowSynchronousContinuations = false
        });
    private readonly CancellationTokenSource _disposeCts = new();
    private readonly Task _worker;

    public InstanceCommandLane()
    {
        _worker = Task.Run(ProcessAsync);
    }

    public Task EnqueueAsync(Func<CancellationToken, Task> handler, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (_disposeCts.IsCancellationRequested)
        {
            throw new ObjectDisposedException(nameof(InstanceCommandLane));
        }

        var tcs = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var command = new QueuedCommand(handler, tcs);

        if (!_channel.Writer.TryWrite(command))
        {
            throw new ObjectDisposedException(nameof(InstanceCommandLane));
        }

        return tcs.Task.WaitAsync(cancellationToken);
    }

    public async ValueTask DisposeAsync()
    {
        _channel.Writer.TryComplete();
        _disposeCts.Cancel();

        try
        {
            await _worker;
        }
        catch (OperationCanceledException)
        {
            // expected on shutdown
        }

        _disposeCts.Dispose();
    }

    private async Task ProcessAsync()
    {
        await foreach (var command in _channel.Reader.ReadAllAsync(_disposeCts.Token))
        {
            try
            {
                await command.Handler(_disposeCts.Token);
                command.Completion.SetResult();
            }
            catch (OperationCanceledException ex) when (_disposeCts.IsCancellationRequested)
            {
                command.Completion.SetException(ex);
            }
            catch (Exception ex)
            {
                command.Completion.SetException(ex);
            }
        }
    }
    private sealed record QueuedCommand(
        Func<CancellationToken, Task> Handler,
        TaskCompletionSource Completion);
}
