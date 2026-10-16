namespace OrcaCore.Engine.Durable.Driver;

internal static class DurablePolicyWinnerSelector
{
    internal static async ValueTask<bool> TimeoutWonAsync(
        Task executionTask,
        Task timeoutTask,
        Func<ValueTask>? afterTimeoutSelected = null)
    {
        ArgumentNullException.ThrowIfNull(executionTask);
        ArgumentNullException.ThrowIfNull(timeoutTask);

        var winner = await Task.WhenAny(executionTask, timeoutTask).ConfigureAwait(false);
        if (!ReferenceEquals(winner, timeoutTask))
        {
            return false;
        }

        if (afterTimeoutSelected is not null)
        {
            await afterTimeoutSelected().ConfigureAwait(false);
        }

        return true;
    }
}
