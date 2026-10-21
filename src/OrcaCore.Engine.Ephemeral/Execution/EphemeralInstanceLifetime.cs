using OrcaCore.Abstractions.Ids;
using OrcaCore.Hosting;
using OrcaCore.Engine.Ephemeral.Diagnostics;

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
    private object gate = new();
    private readonly CancellationTokenSource terminalCancellation = new();
    private readonly Dictionary<object, Action> cleanupOwners = new(ReferenceEqualityComparer.Instance);
    private TaskCompletionSource settled = NewCompletion();
    private Action? removeInstance;
    private Func<InstanceId, EphemeralWorkKind, ValueTask>? completionPending;
    private Action<EphemeralInstanceLifetime>? changed;
    private int pins;
    private int owned = 1; // The provisional start owns publication until acceptance or definitive abort.
    private bool startPending = true;
    private bool accepted;
    private bool aborting;
    private bool terminal;
    private bool retired;
    private EphemeralHostLifetime? host;
    private EphemeralHostLifetime.Lease? provisionalOwner;
    private EphemeralHostLifetime.Lease? activeOwner;

    internal InstanceId InstanceId => instanceId;
    internal bool IsAccepted { get { lock (gate) return accepted; } }
    internal bool IsPhysicallySettled { get { lock (gate) return accepted && terminal && owned == 0 && !retired; } }
    internal bool IsEligible { get { lock (gate) return IsPhysicallySettled && pins == 0; } }
    internal bool IsRetired { get { lock (gate) return retired; } }
    internal bool IsTerminal { get { lock (gate) return terminal; } }
    internal CancellationToken TerminalCancellation => terminalCancellation.Token;
    internal Task WhenSettled { get { lock (gate) return settled.Task; } }

    internal void Initialize(Action remove, Func<InstanceId, EphemeralWorkKind, ValueTask>? observer, object visibilityGate, Action<EphemeralInstanceLifetime> onChanged, EphemeralHostLifetime? hostLifetime = null)
    {
        gate = visibilityGate; // Set before the first registry publication.
        lock (gate)
        {
            changed = onChanged;
            if (removeInstance is not null) throw new InvalidOperationException("Lifetime is already initialized.");
            removeInstance = remove;
            completionPending = observer;
            host = hostLifetime;
            provisionalOwner = host?.Own();
            activeOwner = host?.Own();
        }
    }

    internal bool TryOwn(out WorkLease? lease)
    {
        lock (gate)
        {
            if (retired) { lease = null; return false; }
            if (owned == 0) settled = NewCompletion();
            checked { owned++; }
            lease = new WorkLease(this, hostOwner: host?.Own());
            return true;
        }
    }

    internal bool TryPin(out WorkLease? lease)
    {
        lock (gate)
        {
            if (retired) { lease = null; return false; }
            checked { pins++; }
            lease = new WorkLease(this, pin: true, hostOwner: host?.Own());
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
        EphemeralHostLifetime.Lease? publication;
        lock (gate)
        {
            if (!startPending) return accepted;
            if (aborting || callerToken.IsCancellationRequested) return false;
            accepted = true;
            startPending = false;
            owned--;
            completed = owned == 0 ? settled : null;
            publication = provisionalOwner;
            provisionalOwner = null;
        }
        try
        {
            if (completed is not null) changed?.Invoke(this);
            completed?.TrySetResult();
        }
        finally { publication?.Dispose(); }
        return true;
    }

    internal void SignalTerminal()
    {
        EphemeralHostLifetime.Lease? active;
        lock (gate)
        {
            if (terminal) return;
            terminal = true;
            active = activeOwner;
            activeOwner = null;
        }
        try { _ = DispatchCancellation(terminalCancellation); }
        finally { active?.Dispose(); }
    }

    internal void Abort()
    {
        EphemeralHostLifetime.Lease? publication;
        lock (gate)
        {
            if (!startPending) return;
            if (!terminal) throw new InvalidOperationException("Abort requires the owning-lane terminal fence.");
            startPending = false;
            publication = provisionalOwner;
            provisionalOwner = null;
        }
        try { Release(); }
        finally { publication?.Dispose(); }
    }

    internal void RequireExecution() => host?.RequireExecution();
    internal bool IsStoppingCancellation(OperationCanceledException error, CancellationToken executionToken) =>
        host?.IsStoppingCancellation(error, executionToken) == true;

    internal bool TryEnterCallback(out IDisposable? ownership)
    {
        lock (gate)
        {
            if (retired) { ownership = null; return false; }
            EphemeralHostLifetime.Lease? callback = null;
            if (host is not null && !host.TryEnterCallback(out callback)) { ownership = null; return false; }
            TryOwn(out var work);
            ownership = new CallbackLease(work!, callback);
            return true;
        }
    }

    // Own the first dispatch, including its handlers and linked propagation. Never
    // call Cancel inline on a lane, a system timer, or the stopping caller.
    internal async Task DispatchCancellation(CancellationTokenSource source)
    {
        using var ownership = Own();
        try { await source.CancelAsync().ConfigureAwait(false); }
        catch (Exception) { OrcaCoreEphemeralDiagnostics.RecordShutdown("shutdown-callback-failed"); }
    }

    internal CancellationScope CreateCancellationScope(CancellationToken parent) => new(this, parent);

    // Logical timeout may finish before either physical work or CancelAsync's
    // queued handlers. Disposing their source at that boundary can silently
    // discard handlers, so each participant retains it through its own return.
    internal sealed class CancellationScope : IDisposable
    {
        private readonly EphemeralInstanceLifetime lifetime;
        private readonly object scopeGate = new();
        private readonly CancellationTokenSource source = new();
        private readonly CancellationTokenRegistration parentRegistration;
        private int references = 1;
        private int disposed;

        internal CancellationScope(EphemeralInstanceLifetime lifetime, CancellationToken parent)
        {
            this.lifetime = lifetime;
            parentRegistration = parent.UnsafeRegister(static state =>
            {
                var scope = (CancellationScope)state!;
                using var entered = scope.TryOwn();
                if (entered is not null) _ = scope.CancelAsync();
            }, this);
        }

        internal CancellationToken Token => source.Token;

        internal IDisposable? TryOwn()
        {
            lock (scopeGate)
            {
                if (references == 0) return null;
                checked { references++; }
                return new ScopeLease(this);
            }
        }

        internal async Task<T> RunAsync<T>(Func<Task<T>> operation)
        {
            using var ownership = TryOwn() ?? throw new ObjectDisposedException(nameof(CancellationScope));
            return await operation().ConfigureAwait(false);
        }

        internal async Task CancelAsync()
        {
            using var lifetimeOwnership = lifetime.Own();
            using var ownership = TryOwn() ?? throw new ObjectDisposedException(nameof(CancellationScope));
            await lifetime.DispatchCancellation(source).ConfigureAwait(false);
        }

        public void Dispose()
        {
            if (Interlocked.Exchange(ref disposed, 1) == 0) Release();
        }

        private void Release()
        {
            bool final;
            lock (scopeGate) final = --references == 0;
            if (final)
            {
                parentRegistration.Unregister();
                source.Dispose();
            }
        }

        private sealed class ScopeLease(CancellationScope scope) : IDisposable
        {
            private CancellationScope? owner = scope;
            public void Dispose() => Interlocked.Exchange(ref owner, null)?.Release();
        }
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
        var cleanup = SelectRetirement();
        if (cleanup is null) return false;
        cleanup();
        return true;
    }

    // Select under the shared visibility gate; execute cleanup only after leaving it.
    internal Action? SelectRetirement()
    {
        lock (gate)
        {
            if (retired || startPending || !terminal || owned != 0 || pins != 0) return null;
            retired = true;
            var cleanup = TakeCleanup();
            var completed = settled;
            return () => FinishCleanup(cleanup, completed);
        }
    }

    private void Release(bool pin = false)
    {
        Action[]? cleanup = null;
        TaskCompletionSource? completed;
        Action<EphemeralInstanceLifetime>? notify;
        lock (gate)
        {
            if (pin)
            {
                if (pins <= 0) throw new InvalidOperationException("Unbalanced operation pin.");
                pins--;
            }
            else
            {
                if (owned <= 0) throw new InvalidOperationException("Unbalanced instance ownership.");
                owned--;
            }
            completed = owned == 0 ? settled : null;
            notify = changed;
            if (owned == 0 && pins == 0 && !startPending && !accepted && terminal && !retired)
            {
                retired = true;
                cleanup = TakeCleanup();
            }
        }
        if (cleanup is not null) FinishCleanup(cleanup, completed!);
        else
        {
            notify?.Invoke(this);
            completed?.TrySetResult();
        }
    }

    private Action[] TakeCleanup()
    {
        var actions = cleanupOwners.Values.Concat(removeInstance is null ? [] : new[] { removeInstance }).ToArray();
        cleanupOwners.Clear();
        removeInstance = null;
        completionPending = null;
        changed = null;
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

    private sealed class CallbackLease(IDisposable work, IDisposable? callback) : IDisposable
    {
        public void Dispose()
        {
            try { work.Dispose(); }
            finally { callback?.Dispose(); }
        }
    }

    internal sealed class WorkLease(EphemeralInstanceLifetime owner, bool pin = false, EphemeralHostLifetime.Lease? hostOwner = null) : IDisposable
    {
        private EphemeralInstanceLifetime? lifetime = owner;
        public void Dispose()
        {
            var current = Interlocked.Exchange(ref lifetime, null);
            if (current is null) return;
            try { current.Release(pin); }
            catch (Exception) { current.host?.Fault(); throw; }
            finally { hostOwner?.Dispose(); }
        }
    }
}
