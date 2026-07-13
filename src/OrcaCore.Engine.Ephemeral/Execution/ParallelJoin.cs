namespace OrcaCore.Engine.Ephemeral.Execution;

internal sealed class ParallelJoin(
    int branchCount,
    Func<CancellationToken, Task> continueAsync)
{
    private int remaining = branchCount;
    private int continued;

    internal async Task BranchCompletedAsync(CancellationToken cancellationToken)
    {
        if (Interlocked.Decrement(ref remaining) == 0 &&
            Interlocked.Exchange(ref continued, 1) == 0)
        {
            await continueAsync(cancellationToken).ConfigureAwait(false);
        }
    }
}
