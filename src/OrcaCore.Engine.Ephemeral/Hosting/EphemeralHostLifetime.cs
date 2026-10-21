using OrcaCore.Engine.Ephemeral.Diagnostics;

namespace OrcaCore.Hosting;

/// <summary>One admission boundary and conserved work ledger per engine root.</summary>
internal sealed class EphemeralHostLifetime(TimeProvider clock, object gate, Func<Task> stop) : IEphemeralWorkflowHostLifetime, IDisposable
{
    private static readonly TimeSpan MaximumTimeout = TimeSpan.FromMilliseconds(4_294_967_294);
    private static readonly object GateCancellation = new();
    private readonly EphemeralHostShutdownResult.Completed completed = new();
    private TaskCompletionSource changed = NewSignal();
    private readonly HashSet<ObservationTimer> observationTimers = [];
    private CancellationToken applicationStopping;
    private CancellationTokenRegistration closingRegistration;
    private long pending;
    private bool closed;
    private bool stopping;
    private bool done;
    private bool faulted;
    private bool disposed;

    internal object Gate => gate;
    internal bool IsStopping { get { lock (gate) return stopping; } }

    internal void Attach(CancellationToken stoppingToken)
    {
        applicationStopping = stoppingToken;
        // Consult the token at admission even if an earlier callback blocks ours.
        closingRegistration = applicationStopping.UnsafeRegister(static state => ((EphemeralHostLifetime)state!).Close(), this);
    }

    internal void Close() { lock (gate) closed = true; }

    internal void RequireOpen()
    {
        lock (gate)
        {
            ObjectDisposedException.ThrowIf(disposed, this);
            if (closed || applicationStopping.IsCancellationRequested) throw new EphemeralHostStoppingException();
        }
    }

    internal void RequireRunning()
    {
        lock (gate)
        {
            ObjectDisposedException.ThrowIf(disposed, this);
            if (stopping) throw new EphemeralHostStoppingException();
        }
    }

    internal void RequireExecution()
    {
        lock (gate)
        {
            ObjectDisposedException.ThrowIf(disposed, this);
            if (stopping)
            {
                var cancellation = new OperationCanceledException("The ephemeral workflow host is stopping.", new CancellationToken(canceled: true));
                cancellation.Data[GateCancellation] = true;
                throw cancellation;
            }
        }
    }

    internal bool IsStoppingCancellation(OperationCanceledException error, CancellationToken executionToken) =>
        IsStopping && (error.Data.Contains(GateCancellation) ||
            executionToken.IsCancellationRequested && error.CancellationToken == executionToken);

    internal Lease AdmitStart() { lock (gate) { RequireOpen(); return Own(); } }

    // A successor is acquired while its predecessor still owns the handoff.
    // Stable zero is final and cannot be resurrected by a late callback.
    internal Lease Own()
    {
        lock (gate)
        {
            if (done) throw new EphemeralHostStoppingException();
            checked { pending++; }
            return new Lease(this);
        }
    }

    internal bool TryEnterCallback(out Lease? lease)
    {
        lock (gate)
        {
            if (stopping || disposed) { lease = null; return false; }
            lease = Own();
            return true;
        }
    }

    internal void Fault()
    {
        lock (gate)
        {
            if (faulted) return;
            faulted = true;
            checked { pending++; } // Lost proof remains a permanent obligation.
            Pulse();
        }
        OrcaCoreEphemeralDiagnostics.RecordShutdown("shutdown-fault");
    }

    public ValueTask<EphemeralHostShutdownResult> StopAsync(TimeSpan timeout, CancellationToken cancellationToken = default)
    {
        Validate(timeout);
        cancellationToken.ThrowIfCancellationRequested();
        Lease admission;
        lock (gate)
        {
            ObjectDisposedException.ThrowIf(disposed, this);
            if (done) return ValueTask.FromResult<EphemeralHostShutdownResult>(completed);
            admission = Own();
        }
        return ObserveAsync(timeout, cancellationToken, admission);
    }

    internal ValueTask<EphemeralHostShutdownResult> StopHostedAsync(CancellationToken cancellationToken)
    {
        Lease admission;
        lock (gate)
        {
            if (done) return ValueTask.FromResult<EphemeralHostShutdownResult>(completed);
            admission = Own();
        }
        return ObserveAsync(Timeout.InfiniteTimeSpan, cancellationToken, admission);
    }

    private async ValueTask<EphemeralHostShutdownResult> ObserveAsync(TimeSpan timeout, CancellationToken cancellationToken, Lease admission)
    {
        long started = 0;
        using (admission)
        {
            try { if (timeout != Timeout.InfiniteTimeSpan) started = clock.GetTimestamp(); }
            catch (Exception) { Fault(); }
            lock (gate)
            {
                if (!stopping)
                {
                    closed = stopping = true;
                    var worker = Own();
                    _ = Task.Run(async () =>
                    {
                        try { await stop().ConfigureAwait(false); }
                        catch (Exception) { Fault(); }
                        finally { worker.Dispose(); }
                    });
                }
            }
        }

        var interrupted = NewSignal();
        using var cancellation = cancellationToken.UnsafeRegister(static state => ((TaskCompletionSource)state!).TrySetResult(), interrupted);
        ObservationTimer? deadline = null;
        try
        {
            if (timeout != Timeout.InfiniteTimeSpan)
            {
                using var setup = OwnUnlessDone();
                if (setup is not null)
                {
                    try
                    {
                        var remaining = timeout - clock.GetElapsedTime(started);
                        if (remaining > TimeSpan.Zero)
                        {
                            deadline = new ObservationTimer(clock.CreateTimer(static state => ((TaskCompletionSource)state!).TrySetResult(), interrupted, remaining, Timeout.InfiniteTimeSpan));
                            lock (gate) observationTimers.Add(deadline);
                        }
                        else interrupted.TrySetResult();
                    }
                    catch (Exception) { Fault(); }
                }
            }

            while (true)
            {
                Task next;
                Lease? reporting = null;
                EphemeralHostShutdownIncompleteReason? reason = null;
                long count;
                lock (gate)
                {
                    count = pending;
                    if (faulted) reason = EphemeralHostShutdownIncompleteReason.ShutdownFaulted;
                    else if (done) return completed;
                    else if (cancellationToken.IsCancellationRequested) reason = EphemeralHostShutdownIncompleteReason.ObservationCancelled;
                    else if (interrupted.Task.IsCompleted) reason = EphemeralHostShutdownIncompleteReason.DeadlineElapsed;
                    if (reason is not null) reporting = Own();
                    next = changed.Task;
                }
                if (reason is { } why)
                {
                    using var notification = reporting;
                    OrcaCoreEphemeralDiagnostics.RecordShutdown("shutdown-incomplete");
                    return new EphemeralHostShutdownResult.Incomplete(why, count);
                }
                await Task.WhenAny(next, interrupted.Task).ConfigureAwait(false);
            }
        }
        finally { if (deadline is not null) ReleaseObservationTimer(deadline); }
    }

    private Lease? OwnUnlessDone() { lock (gate) return done ? null : Own(); }

    private static void Validate(TimeSpan timeout)
    {
        if (timeout != Timeout.InfiniteTimeSpan && (timeout < TimeSpan.Zero || timeout > MaximumTimeout))
            throw new ArgumentOutOfRangeException(nameof(timeout));
    }

    private void Release()
    {
        ITimer[] timers = [];
        Lease? cleanup = null;
        lock (gate)
        {
            if (pending <= 0) throw new InvalidOperationException("Unbalanced host ownership.");
            pending--;
            if (stopping && pending == 0 && !faulted)
            {
                timers = observationTimers.Select(timer => timer.Take()).OfType<ITimer>().ToArray();
                observationTimers.Clear();
                if (timers.Length == 0) done = true;
                else cleanup = Own();
            }
            Pulse();
        }
        if (cleanup is not null) QueueTimerCleanup(timers, cleanup);
    }

    private void ReleaseObservationTimer(ObservationTimer observation)
    {
        ITimer? timer;
        Lease cleanup;
        lock (gate)
        {
            observationTimers.Remove(observation);
            timer = observation.Take();
            if (timer is null) return; // Stable-zero cleanup already owns it.
            cleanup = Own();
        }
        QueueTimerCleanup([timer], cleanup);
    }

    private void QueueTimerCleanup(ITimer[] timers, Lease cleanup) => _ = Task.Run(() =>
    {
        try
        {
            foreach (var timer in timers)
            {
                try { timer.Dispose(); }
                catch (Exception) { Fault(); }
            }
        }
        finally { cleanup.Dispose(); }
    });

    private void Pulse()
    {
        var prior = changed;
        changed = NewSignal();
        prior.TrySetResult();
    }

    public void Dispose()
    {
        bool abortive;
        lock (gate) { if (disposed) return; disposed = closed = true; abortive = !done; }
        closingRegistration.Unregister();
        if (abortive) OrcaCoreEphemeralDiagnostics.RecordShutdown("shutdown-abortive-disposal");
    }

    private static TaskCompletionSource NewSignal() => new(TaskCreationOptions.RunContinuationsAsynchronously);

    private sealed class ObservationTimer(ITimer ownedTimer)
    {
        private ITimer? timer = ownedTimer;
        internal ITimer? Take() => Interlocked.Exchange(ref timer, null);
    }

    internal sealed class Lease(EphemeralHostLifetime lifetime) : IDisposable
    {
        private EphemeralHostLifetime? owner = lifetime;
        public void Dispose() => Interlocked.Exchange(ref owner, null)?.Release();
    }
}
