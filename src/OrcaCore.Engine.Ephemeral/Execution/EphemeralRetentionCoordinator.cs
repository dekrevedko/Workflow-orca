using OrcaCore.Abstractions.Ids;
using OrcaCore.Engine.Ephemeral.Diagnostics;

namespace OrcaCore.Engine.Ephemeral.Execution;

/// <summary>One host-wide retirement order and admission boundary; never invokes cleanup under Gate.</summary>
internal sealed class EphemeralRetentionCoordinator : IDisposable
{
    private readonly TimeProvider clock;
    private readonly EphemeralTerminalRetentionSettings? policy;
    private readonly Dictionary<InstanceId, Entry> entries = [];
    private readonly ITimer? timer;
    private long ordinal;
    private int sweeping;
    private bool disposed;

    internal object Gate { get; }
    internal global::OrcaCore.Hosting.EphemeralHostLifetime? HostLifetime { get; }
    internal int TrackedCount { get { lock (Gate) return entries.Count; } }
    internal bool HasTimer => timer is not null;

    internal EphemeralRetentionCoordinator(TimeProvider clock, EphemeralTerminalRetentionSettings? policy, global::OrcaCore.Hosting.EphemeralHostLifetime? hostLifetime = null)
    {
        this.clock = clock;
        this.policy = policy;
        HostLifetime = hostLifetime;
        Gate = hostLifetime?.Gate ?? new object();
        if (policy is not null)
        {
            // Arm after assignment and rearm explicitly: System timers round sub-ms periods
            // to zero (one shot). Preserve the configured interval without losing idle sweeps.
            timer = clock.CreateTimer(_ => Sweep(), null, Timeout.InfiniteTimeSpan, Timeout.InfiniteTimeSpan);
            timer.Change(policy.SweepInterval, Timeout.InfiniteTimeSpan);
        }
    }

    internal void Track(EphemeralInstanceLifetime lifetime)
    {
        if (policy is null) return;
        lock (Gate)
        {
            if (!disposed) entries.Add(lifetime.InstanceId, new Entry(lifetime));
        }
    }

    internal void Forget(InstanceId id) { lock (Gate) entries.Remove(id); }

    internal void Changed(EphemeralInstanceLifetime lifetime)
    {
        if (policy is null) return;
        lock (Gate) { if (disposed) return; }
        var timestamp = clock.GetTimestamp();
        lock (Gate)
        {
            if (disposed) return;
            if (entries.TryGetValue(lifetime.InstanceId, out var entry) &&
                entry.Ordinal == 0 && lifetime.IsPhysicallySettled)
            {
                entry.Timestamp = timestamp;
                entry.Ordinal = checked(++ordinal);
            }
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
        List<Action> cleanup = [];
        lock (Gate) { if (disposed || policy is null) return; }
        var now = clock.GetTimestamp();
        var frequency = clock.TimestampFrequency;
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(frequency);
        lock (Gate)
        {
            if (disposed || policy is null) return;
            var eligible = entries.Values.Where(e => e.Ordinal != 0 && e.Lifetime.IsEligible)
                .OrderBy(e => e.Ordinal).ToArray();
            // Compare monotonic durations without invoking a virtual clock member
            // under the admission gate or overflowing a timestamp/age product.
            var expired = eligible.Where(e => (decimal)unchecked(now - e.Timestamp) / frequency >=
                (decimal)policy.MaxAge.Ticks / TimeSpan.TicksPerSecond).ToHashSet();
            var remaining = eligible.Length - expired.Count;
            foreach (var entry in eligible)
            {
                if (!expired.Contains(entry) && remaining <= policy.MaxRetainedInstances) continue;
                if (!expired.Contains(entry)) remaining--;
                var release = entry.Lifetime.SelectRetirement();
                if (release is null) continue;
                entries.Remove(entry.Lifetime.InstanceId);
                cleanup.Add(release);
            }
        }
        // Physical release and observations are synchronous, outside every admission lock.
        Exception? failure = null;
        foreach (var release in cleanup)
        {
            try { release(); }
            catch (Exception error) { failure ??= error; }
        }
        if (failure is not null) System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(failure).Throw();
    }

    public void Dispose()
    {
        lock (Gate) { disposed = true; entries.Clear(); }
        timer?.Dispose();
    }

    private sealed class Entry(EphemeralInstanceLifetime lifetime)
    {
        internal EphemeralInstanceLifetime Lifetime { get; } = lifetime;
        internal long Timestamp { get; set; }
        internal long Ordinal { get; set; }
    }
}
