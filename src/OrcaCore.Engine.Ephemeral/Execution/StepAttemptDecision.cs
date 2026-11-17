namespace OrcaCore.Engine.Ephemeral.Execution;

/// <summary>Elects at the runtime observation boundary, before physical ownership is released.</summary>
internal sealed class StepAttemptDecision(TimeProvider clock, DateTimeOffset deadline, Action timedOut)
{
    private readonly object gate = new();
    private readonly TaskCompletionSource<bool> decision = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private int winner; // 0: undecided, 1: result, 2: deadline

    internal async Task<bool> WaitForResultAsync(Task attempt)
    {
        await Task.WhenAny(decision.Task, attempt).ConfigureAwait(false);
        // A lifetime/cancellation wrapper can fail before ObserveAsync is entered.
        // Elect its result under the same gate: a timeout may already be elected
        // while its callback is still running, before decision.Task is published.
        if (!decision.Task.IsCompleted && Elect(resultAvailable: true, out _))
            await attempt.ConfigureAwait(false);
        return await decision.Task.ConfigureAwait(false);
    }

    internal TimeSpan? OnTimer()
    {
        Elect(resultAvailable: false, out var remaining);
        return remaining;
    }

    internal async Task<T> ObserveAsync<T>(Func<Task<T>> operation)
    {
        T result;
        try { result = await operation().ConfigureAwait(false); }
        catch
        {
            if (Elect(resultAvailable: true, out _)) throw;
            // A late failure is observed here, but cannot replace the elected timeout.
            return default!;
        }
        Elect(resultAvailable: true, out _);
        return result;
    }

    private bool Elect(bool resultAvailable, out TimeSpan? remaining)
    {
        remaining = null;
        bool resultWon;
        lock (gate)
        {
            if (winner != 0) return winner == 1;
            var now = clock.GetUtcNow();
            var expired = now >= deadline;
            if (!resultAvailable && !expired)
            {
                remaining = deadline - now;
                return false;
            }
            resultWon = !expired;
            winner = resultWon ? 1 : 2;
        }
        if (!resultWon) timedOut();
        decision.TrySetResult(resultWon);
        return resultWon;
    }
}
