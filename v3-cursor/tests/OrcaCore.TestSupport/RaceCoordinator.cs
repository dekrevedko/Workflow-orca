namespace OrcaCore.TestSupport;

/// <summary>
/// Forces two async callers to reach a gate before either proceeds.
/// </summary>
public sealed class RaceCoordinator
{
    private readonly TimeSpan _timeout;
    private readonly TaskCompletionSource _firstArrived = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly TaskCompletionSource _secondArrived = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly TaskCompletionSource _release = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private int _arrived;

    public RaceCoordinator(TimeSpan? gateTimeout = null) =>
        _timeout = gateTimeout ?? TimeSpan.FromSeconds(5);

    public async Task EnterGateAsync(CancellationToken cancellationToken = default)
    {
        var arrival = Interlocked.Increment(ref _arrived);
        var arrivedSignal = arrival == 1 ? _firstArrived : _secondArrived;
        arrivedSignal.TrySetResult();

        using var timeout = new CancellationTokenSource(_timeout);
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeout.Token);

        if (arrival == 1)
        {
            await _secondArrived.Task.WaitAsync(linked.Token)
                .ConfigureAwait(false);
        }
        else
        {
            await _firstArrived.Task.WaitAsync(linked.Token)
                .ConfigureAwait(false);
        }

        await _release.Task.WaitAsync(linked.Token).ConfigureAwait(false);
    }

    public void ReleaseAll() => _release.TrySetResult();
}
