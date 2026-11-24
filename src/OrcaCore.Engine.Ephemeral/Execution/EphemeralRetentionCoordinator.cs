using OrcaCore.Abstractions.Ids;
using OrcaCore.Engine.Ephemeral.Diagnostics;

namespace OrcaCore.Engine.Ephemeral.Execution;

/// <summary>One host-wide retirement order and admission boundary; never invokes cleanup under Gate.</summary>
internal sealed class EphemeralRetentionCoordinator : IDisposable
{
    private readonly TimeProvider clock;
    private readonly EphemeralTerminalRetentionSettings? policy;
    private EphemeralRetentionIndex? index;
    private readonly ITimer? timer;
    private int sweeping;
    private bool disposed;

    internal object Gate { get; }
    internal global::OrcaCore.Hosting.EphemeralHostLifetime? HostLifetime { get; }
    internal int TrackedCount { get { lock (Gate) return index?.TrackedCount ?? 0; } }
    internal bool HasTimer => timer is not null;

    internal EphemeralRetentionCoordinator(TimeProvider clock, EphemeralTerminalRetentionSettings? policy, global::OrcaCore.Hosting.EphemeralHostLifetime? hostLifetime = null)
    {
        this.clock = clock;
        this.policy = policy;
        HostLifetime = hostLifetime;
        Gate = hostLifetime?.Gate ?? new object();
        if (policy is not null)
        {
            index = new EphemeralRetentionIndex();
            // Arm after assignment and rearm explicitly: System timers round sub-ms periods
            // to zero (one shot). Preserve the configured interval without losing idle sweeps.
            timer = clock.CreateTimer(_ => Sweep(), null, Timeout.InfiniteTimeSpan, Timeout.InfiniteTimeSpan);
            timer.Change(policy.SweepInterval, Timeout.InfiniteTimeSpan);
        }
    }

    // Registration, pin transitions and retirement elections are already under Gate in the lifetime.
    internal bool Track(EphemeralInstanceLifetime lifetime)
    {
        if (index is null) return false;
        index.Track(lifetime);
        return true;
    }

    internal void SetPinned(InstanceId id, bool pinned) => index?.SetPinned(id, pinned);

    internal void Forget(InstanceId id) => index?.Forget(id);

    internal void Changed(EphemeralInstanceLifetime lifetime)
    {
        bool needsStamp;
        lock (Gate)
        {
            if (index is null) return;
            needsStamp = index.NeedsStamp(lifetime);
        }
        if (needsStamp)
        {
            var timestamp = clock.GetTimestamp();
            lock (Gate) index?.Stamp(lifetime, timestamp);
        }
        Enforce();
    }

    private void Sweep()
    {
        global::OrcaCore.Hosting.EphemeralHostLifetime.Lease? ownership = null;
        if (HostLifetime is { } host && !host.TryEnterCallback(out ownership)) return;
        using var callback = ownership;
        if (Interlocked.CompareExchange(ref sweeping, 1, 0) != 0) return;
        try { Enforce(); }
        catch (Exception)
        {
            HostLifetime?.Fault();
            try { OrcaCoreEphemeralDiagnostics.RecordRetentionSweepFailure(); }
            catch (Exception) { /* Even diagnostic initialization cannot escape a system timer callback. */ }
        }
        finally
        {
            Volatile.Write(ref sweeping, 0);
            bool rearm;
            lock (Gate) rearm = !disposed;
            if (rearm)
            {
                try { timer?.Change(policy!.SweepInterval, Timeout.InfiniteTimeSpan); }
                catch (ObjectDisposedException) { /* Disposal won the rearm race. */ }
                catch (Exception) { HostLifetime?.Fault(); }
            }
        }
    }

    private void Enforce()
    {
        lock (Gate) { if (disposed || policy is null) return; }
        var now = clock.GetTimestamp();
        var frequency = clock.TimestampFrequency;
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(frequency);
        // Round up to the first whole timestamp tick that is at least MaxAge.
        var minimumAge = ((Int128)policy.MaxAge.Ticks * frequency + TimeSpan.TicksPerSecond - 1) / TimeSpan.TicksPerSecond;
        List<(long Ordinal, Action Release)>? cleanup;
        lock (Gate)
        {
            cleanup = index?.Select(now, minimumAge, policy.MaxRetainedInstances);
        }
        if (cleanup is null) return;
        // Preserve settlement order for cleanup and the first failure. Only victims need ordering.
        cleanup.Sort(static (left, right) => left.Ordinal.CompareTo(right.Ordinal));
        Exception? failure = null;
        foreach (var (_, release) in cleanup)
        {
            try { release(); }
            catch (Exception error) { failure ??= error; }
        }
        if (failure is not null) System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(failure).Throw();
    }

    public void Dispose()
    {
        lock (Gate) { disposed = true; index = null; }
        timer?.Dispose();
    }
}
