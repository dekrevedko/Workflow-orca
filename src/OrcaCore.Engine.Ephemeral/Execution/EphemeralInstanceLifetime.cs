using OrcaCore.Abstractions.Ids;

namespace OrcaCore.Engine.Ephemeral.Execution;

internal enum EphemeralWorkKind
{
    PhysicalAttempt,
    CapacityRelease,
    RetryContinuation,
    ResourceContinuation,
    TimerDispatch
}

/// <summary>Owns provisional publication and physical work independently of logical status or capacity.</summary>
internal sealed class EphemeralInstanceLifetime(InstanceId instanceId)
{
    private readonly object gate = new();
    private readonly CancellationTokenSource terminalCancellation = new();
    private readonly Dictionary<object, Action> cleanupOwners = new(ReferenceEqualityComparer.Instance);
    private TaskCompletionSource settled = NewCompletion();
    private Action? removeInstance;
    private Func<InstanceId, EphemeralWorkKind, ValueTask>? completionPending;
    private int owned = 1; // The provisional start owns publication until acceptance or definitive abort.
    private bool startPending = true;
    private bool accepted;
    private bool aborting;
    private bool terminal;
    private bool retired;

    internal bool IsRetired { get { lock (gate) return retired; } }
    internal bool IsTerminal { get { lock (gate) return terminal; } }
    internal CancellationToken TerminalCancellation => terminalCancellation.Token;
    internal Task WhenSettled { get { lock (gate) return settled.Task; } }

    internal void Initialize(Action remove, Func<InstanceId, EphemeralWorkKind, ValueTask>? observer)
    {
        lock (gate)
        {
            if (removeInstance is not null) throw new InvalidOperationException("Lifetime is already initialized.");
            removeInstance = remove;
            completionPending = observer;
        }
    }

    internal bool TryOwn(out WorkLease? lease)
    {
        lock (gate)
        {
            if (retired) { lease = null; return false; }
            if (owned == 0) settled = NewCompletion();
            checked { owned++; }
            lease = new WorkLease(this);
            return true;
        }
    }

    internal WorkLease Own() => TryOwn(out var lease)
        ? lease! : throw new InvalidOperationException("The instance has been released.");

    internal async Task<T> RunAsync<T>(EphemeralWorkKind kind, Func<Task<T>> operation)
    {
        var lease = Own();
        try { return await operation().ConfigureAwait(false); }
        finally { await FinishAsync(lease, kind).ConfigureAwait(false); }
    }

    internal async ValueTask FinishAsync(WorkLease lease, EphemeralWorkKind kind)
    {
        try
        {
            // Internal observation seam: outside all lifetime/storage locks, while ownership is still held.
            if (completionPending is { } observe)
                await observe(instanceId, kind).ConfigureAwait(false);
        }
        finally { lease.Dispose(); }
    }

    internal async Task RunAsync(EphemeralWorkKind kind, Func<Task> operation) =>
        _ = await RunAsync(kind, async () =>
        {
            await operation().ConfigureAwait(false);
            return true;
        }).ConfigureAwait(false);

    internal void RegisterCleanup(object owner, Action cleanup)
    {
        lock (gate)
        {
            if (retired) throw new InvalidOperationException("The instance has been released.");
            cleanupOwners.TryAdd(owner, cleanup);
        }
    }

    internal bool TryAccept(CancellationToken callerToken)
    {
        TaskCompletionSource? completed;
        lock (gate)
        {
            if (!startPending) return accepted;
            if (aborting || callerToken.IsCancellationRequested) return false;
            accepted = true;
            startPending = false;
            owned--;
            completed = owned == 0 ? settled : null;
        }
        completed?.TrySetResult();
        return true;
    }

    internal void SignalTerminal()
    {
        lock (gate)
        {
            if (terminal) return;
            terminal = true;
        }
        // Cancellation may run continuations; it must never happen under the lifetime gate.
        terminalCancellation.Cancel();
    }

    internal void Abort()
    {
        lock (gate)
        {
            if (!startPending) return;
            if (!terminal) throw new InvalidOperationException("Abort requires the owning-lane terminal fence.");
            startPending = false;
        }
        Release();
    }

    internal bool TryBeginAbort()
    {
        lock (gate)
        {
            if (accepted || retired) return false;
            aborting = true;
            return true;
        }
    }

    internal bool TryRetire()
    {
        Action[] cleanup;
        TaskCompletionSource completed;
        lock (gate)
        {
            if (retired || startPending || !terminal || owned != 0) return false;
            retired = true;
            cleanup = TakeCleanup();
            completed = settled;
        }
        FinishCleanup(cleanup, completed);
        return true;
    }

    private void Release()
    {
        Action[]? cleanup = null;
        TaskCompletionSource? completed;
        lock (gate)
        {
            if (owned <= 0) throw new InvalidOperationException("Unbalanced instance ownership.");
            owned--;
            completed = owned == 0 ? settled : null;
            if (owned == 0 && !startPending && !accepted && terminal && !retired)
            {
                retired = true;
                cleanup = TakeCleanup();
            }
        }
        if (cleanup is not null) FinishCleanup(cleanup, completed!);
        else completed?.TrySetResult();
    }

    private Action[] TakeCleanup()
    {
        var actions = cleanupOwners.Values.Concat(removeInstance is null ? [] : new[] { removeInstance }).ToArray();
        cleanupOwners.Clear();
        removeInstance = null;
        completionPending = null;
        return actions;
    }

    private void FinishCleanup(Action[] actions, TaskCompletionSource completed)
    {
        Exception? failure = null;
        foreach (var action in actions)
        {
            try { action(); }
            catch (Exception error) { failure ??= error; }
        }
        terminalCancellation.Dispose();
        if (failure is null) completed.TrySetResult();
        else
        {
            completed.TrySetException(failure);
            System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(failure).Throw();
        }
    }

    private static TaskCompletionSource NewCompletion() => new(TaskCreationOptions.RunContinuationsAsynchronously);

    internal sealed class WorkLease(EphemeralInstanceLifetime owner) : IDisposable
    {
        private EphemeralInstanceLifetime? lifetime = owner;
        public void Dispose() => Interlocked.Exchange(ref lifetime, null)?.Release();
    }
}
