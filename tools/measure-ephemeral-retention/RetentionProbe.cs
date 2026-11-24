using System.Diagnostics;
using System.Text.Json;
using OrcaCore.Abstractions.Ids;
using OrcaCore.Hosting;

namespace OrcaCore.Engine.Ephemeral.Execution;

// Compiled into the engine only in Measure.ps1's disposable checkout.
internal static class RetentionProbe
{
    internal static bool Seeding;
    [ThreadStatic] private static long holdTicks;
    [ThreadStatic] private static long waitTicks;
    [ThreadStatic] private static int selections;
    private static int visits, eligibility, sortKeys, ageChecks, retirementChecks, selected;
    private static int indexAdds, indexRemoves, successfulRemoves, bulkClears, denseChecks, indexComparisons, ageRanges, expiredKeys;
    private static readonly TimeSpan Deadline = TimeSpan.FromMinutes(2);

    internal static Selection EnterSelection(long waiting) => new(waiting);
    internal readonly struct Selection(long waiting) : IDisposable
    {
        private readonly long entered = Stopwatch.GetTimestamp();
        public void Dispose()
        {
            holdTicks += Stopwatch.GetTimestamp() - entered;
            waitTicks += entered - waiting;
            selections++;
        }
    }

    // Row counters run only in the single-threaded counts process.
    internal static bool Visit() { visits++; return true; }
    internal static bool CheckEligibility() { eligibility++; return true; }
    internal static long SortKey(long value) { sortKeys++; return value; }
    internal static bool CheckAge() { ageChecks++; return true; }
    internal static void ConsiderRetirement() => retirementChecks++;
    internal static void Selected() => selected++;
    internal static void IndexAdd() => indexAdds++;
    internal static void IndexRemove() => indexRemoves++;
    internal static bool Removed(bool success) { if (success) successfulRemoves++; return success; }
    internal static void BulkClear() => bulkClears++;
    internal static void CheckDenseEntry() => denseChecks++;
    internal static void AgeRange() => ageRanges++;
    internal static long ExpiredKey(long ordinal) { expiredKeys++; return ordinal; }
    internal sealed class CountingComparer<T> : IComparer<T>
    {
        public int Compare(T? left, T? right)
        {
            indexComparisons++;
            return Comparer<T>.Default.Compare(left, right);
        }
    }

    private sealed class Clock : TimeProvider
    {
        internal long Ticks = 1;
        public override long GetTimestamp() => Ticks;
        public override long TimestampFrequency => TimeSpan.TicksPerSecond;
        // The workload advances timestamps explicitly and drives selection through ownership release.
        public override ITimer CreateTimer(TimerCallback callback, object? state, TimeSpan dueTime, TimeSpan period) =>
            new InertTimer();

        private sealed class InertTimer : ITimer
        {
            public bool Change(TimeSpan dueTime, TimeSpan period) => true;
            public void Dispose() { }
            public ValueTask DisposeAsync() => ValueTask.CompletedTask;
        }
    }

    private sealed record Mix(string Name, int ActivePercent, int EligiblePercent);
    private static readonly Mix[] Populations = [new("active", 90, 5), new("terminal", 10, 80), new("pinned", 10, 10)];
    private readonly record struct Sample(long Cycle, long Acquisition, long Release, long Hold, long Wait, long Admission, bool Unpin);

#if INDEXED
    private static bool Indexed => true;
#else
    private static bool Indexed => false;
#endif

    private static void Initialize(EphemeralInstanceLifetime lifetime, EphemeralRetentionCoordinator retention,
        EphemeralHostLifetime host, Action remove)
    {
#if INDEXED
        lifetime.Initialize(remove, null, retention);
#else
        lifetime.Initialize(remove, null, retention.Gate, retention.Changed, host);
        retention.Track(lifetime);
#endif
    }

    private static void Main(string[] args)
    {
        if (args.Length != 2 || args[0] is not ("counts" or "timing"))
            throw new ArgumentException("Expected counts or timing followed by the source commit.");
        using var timeout = new CancellationTokenSource(Deadline);
        var timer = Stopwatch.StartNew();
        VerifyBoundaries();
        if (args[0] == "counts")
        {
            foreach (var control in Populations)
            {
                RunPopulation(100, control, true, args[0], timeout.Token, deferSeed: false);
                if (!Indexed) RunPopulation(100, control, true, args[0], timeout.Token, deferSeed: true);
            }
        }
        foreach (var size in new[] { 1_000, 10_000, 100_000 })
        {
            foreach (var mix in Populations)
                RunPopulation(size, mix, true, args[0], timeout.Token, deferSeed: !Indexed);
            RunPopulation(size, new Mix("disabled", 10, 80), false, args[0], timeout.Token, deferSeed: !Indexed);
        }
        RunPopulation(100_000, Populations[1], true, args[0], timeout.Token, deferSeed: !Indexed, partialExpiry: true);
        Console.WriteLine(JsonSerializer.Serialize(new { mode = args[0], revision = args[1], indexed = Indexed,
            completed = true, elapsedMs = timer.Elapsed.TotalMilliseconds }));
    }

    private static void RunPopulation(int size, Mix mix, bool enabled, string mode, CancellationToken token,
        bool deferSeed, bool partialExpiry = false)
    {
        token.ThrowIfCancellationRequested();
        var clock = new Clock();
        using var host = new EphemeralHostLifetime(clock, new object(), () => Task.CompletedTask);
        var policy = enabled ? new EphemeralTerminalRetentionSettings(size, TimeSpan.FromDays(1), TimeSpan.FromDays(1)) : null;
        using var retention = new EphemeralRetentionCoordinator(clock, policy, host);
        var active = new List<EphemeralInstanceLifetime>();
        var eligible = new List<EphemeralInstanceLifetime>();
        var pins = new List<EphemeralInstanceLifetime.WorkLease>();
        var retired = 0;
        var seed = Stopwatch.GetTimestamp();
        Seeding = deferSeed;
        try
        {
            for (var i = 0; i < size; i++)
            {
                var lifetime = new EphemeralInstanceLifetime(InstanceId.Parse(Guid.CreateVersion7().ToString("D")));
                Initialize(lifetime, retention, host, () => retired++);
                Check(lifetime.TryAccept(CancellationToken.None));
                if (i % 100 < mix.ActivePercent) active.Add(lifetime);
                else
                {
                    if (i % 100 < mix.ActivePercent + mix.EligiblePercent)
                    {
                        // The last eligible member in this separate cohort settles one tick later.
                        if (partialExpiry && eligible.Count + 1 == size * mix.EligiblePercent / 100) clock.Ticks++;
                        eligible.Add(lifetime);
                    }
                    else pins.Add(Pin(lifetime));
                    lifetime.SignalTerminal();
                    Check(lifetime.FinalSettlement.IsCompletedSuccessfully);
                }
            }
        }
        finally { Seeding = false; }
        var seedMs = Stopwatch.GetElapsedTime(seed).TotalMilliseconds;
        Check(retention.TrackedCount == (enabled ? size : 0) && retired == 0);
        try
        {
            if (partialExpiry)
            {
                clock.Ticks = 1 + TimeSpan.TicksPerDay;
                Expiry("expire-all-but-one", eligible.Count - 1);
                Check(!eligible[^1].IsRetired && eligible.Take(eligible.Count - 1).All(lifetime => lifetime.IsRetired));
                clock.Ticks++;
                Expiry("expire-last", eligible.Count);
                Check(eligible[^1].IsRetired && active.All(lifetime => !lifetime.IsRetired));
            }
            else if (mode == "counts")
            {
                Count("stable-work", false, false);
                Count("stable-unpin", true, false);
                clock.Ticks += TimeSpan.TicksPerDay;
                Count("expire-eligible", false, true);
                Check(eligible.All(lifetime => lifetime.IsRetired == enabled));
                Check(active.All(lifetime => !lifetime.IsRetired));
            }
            else
            {
                for (var i = 0; i < 12; i++) Release(active[i], eligible[i], i % 2 != 0, host, enabled);
                Measure(1, 48);
                Measure(4, 24);
                clock.Ticks += TimeSpan.TicksPerDay;
                Expiry("expire-eligible", enabled ? eligible.Count : 0);
            }
        }
        finally
        {
            // The measurement stops before cleanup of the pinned population. No age/capacity selection runs after disposal.
            retention.Dispose();
            foreach (var pin in pins) pin.Dispose();
        }

        void Expiry(string phase, int expectedTotal)
        {
            var before = retired;
            ResetCounters();
            var expiry = Release(active[0], eligible[0], false, host, enabled);
            Check(retired == expectedTotal);
            if (mode == "counts") Check(selected == retired - before);
            Console.WriteLine(JsonSerializer.Serialize(new { mode, phase, size, mix = mix.Name, selected = retired - before,
                timestampFrequency = Stopwatch.Frequency, selectionTicks = expiry.Hold, cycleTicks = expiry.Cycle,
                indexAdds, indexRemoves, successfulRemoves, bulkClears, denseChecks, ageRanges, expiredKeys, indexComparisons }));
        }

        void Count(string phase, bool unpin, bool expired)
        {
            ResetCounters();
            Release(active[0], eligible[0], unpin, host, enabled);
#if !INDEXED
            Check(visits == (enabled ? size : 0) && eligibility == (enabled ? size - active.Count : 0));
            Check(sortKeys == (enabled ? eligible.Count : 0) && ageChecks == sortKeys && retirementChecks == sortKeys);
#else
            var victims = enabled && expired ? eligible.Count : 0;
            Check(ageRanges <= 2 && (expiredKeys == victims || (bulkClears == 1 && expiredKeys == 0)));
            Check(indexAdds == (enabled && unpin ? 1 : 0));
            Check(indexRemoves == indexAdds + victims);
            if (!expired)
            {
                // Two balanced tree removals plus two additions on the first-pin/last-unpin pair.
                var ceiling = 8 * (int)Math.Ceiling(Math.Log2(eligible.Count + 1)) + 8;
                Check(indexComparisons <= (enabled && unpin ? ceiling : 0));
            }
#endif
            Check(selected == (enabled && expired ? eligible.Count : 0) && retired == selected);
            Console.WriteLine(JsonSerializer.Serialize(new { mode, size, mix = mix.Name, phase, deferSeed, active = active.Count,
                eligible = eligible.Count, pinned = pins.Count, seedMs, selections, visits, eligibility,
                sortKeys, ageChecks, retirementChecks, selected, indexAdds, indexRemoves, indexComparisons,
                successfulRemoves, bulkClears, denseChecks, ageRanges, expiredKeys }));
        }

        void Measure(int workers, int iterations)
        {
            using var ready = new CountdownEvent(workers);
            using var start = new ManualResetEventSlim();
            var tasks = Enumerable.Range(0, workers).Select(index => Task.Factory.StartNew(() =>
            {
                var samples = new Sample[iterations];
                ready.Signal();
                start.Wait(token);
                for (var i = 0; i < iterations; i++)
                {
                    token.ThrowIfCancellationRequested();
                    samples[i] = Release(active[index], eligible[index], (i + index) % 2 != 0, host, enabled);
                }
                return samples;
            }, token, TaskCreationOptions.LongRunning, TaskScheduler.Default)).ToArray();
            ready.Wait(token);
            var began = Stopwatch.GetTimestamp();
            start.Set();
            Task.WaitAll(tasks, token);
            var elapsed = Stopwatch.GetElapsedTime(began);
            var samples = tasks.SelectMany(task => task.Result).ToArray();
            Check(retired == 0);
            Console.WriteLine(JsonSerializer.Serialize(new { mode, size, mix = mix.Name, workers, seedMs,
                active = active.Count, eligible = eligible.Count, pinned = pins.Count,
                elapsedMs = elapsed.TotalMilliseconds, releasesPerSecond = samples.Length / elapsed.TotalSeconds,
                timestampFrequency = Stopwatch.Frequency,
                cycleTicks = samples.Select(s => s.Cycle), acquisitionTicks = samples.Select(s => s.Acquisition),
                releaseTicks = samples.Select(s => s.Release), holdTicks = samples.Select(s => s.Hold),
                selectionWaitTicks = samples.Select(s => s.Wait), admissionTicks = samples.Select(s => s.Admission),
                unpin = samples.Select(s => s.Unpin) }));
        }
    }

    private static void ResetCounters()
    {
        visits = eligibility = sortKeys = ageChecks = retirementChecks = selected = 0;
        indexAdds = indexRemoves = successfulRemoves = bulkClears = denseChecks = indexComparisons = ageRanges = expiredKeys = 0;
    }

    private static Sample Release(EphemeralInstanceLifetime active, EphemeralInstanceLifetime terminal,
        bool unpin, EphemeralHostLifetime host, bool enabled)
    {
        var cycle = Stopwatch.GetTimestamp();
        holdTicks = waitTicks = selections = 0;
        var began = Stopwatch.GetTimestamp();
        var admission = host.AdmitStart();
        var admissionTicks = Stopwatch.GetTimestamp() - began;
        admission.Dispose();
        began = Stopwatch.GetTimestamp();
        var lease = unpin ? Pin(terminal) : active.Own();
        var acquisitionTicks = Stopwatch.GetTimestamp() - began;
        began = Stopwatch.GetTimestamp();
        lease.Dispose();
        var releaseTicks = Stopwatch.GetTimestamp() - began;
        var cycleTicks = Stopwatch.GetTimestamp() - cycle;
        if (!Indexed) Check(selections == (enabled ? 1 : 0));
        return new(cycleTicks, acquisitionTicks, releaseTicks, holdTicks, waitTicks, admissionTicks, unpin);
    }

    private static EphemeralInstanceLifetime.WorkLease Pin(EphemeralInstanceLifetime lifetime)
        => lifetime.TryPin(out var lease) ? lease! : throw new InvalidOperationException("Cannot pin a retained lifetime.");

    private static void VerifyBoundaries()
    {
        var clock = new Clock();
        using var host = new EphemeralHostLifetime(clock, new object(), () => Task.CompletedTask);
        using var retention = new EphemeralRetentionCoordinator(clock,
            new(1, TimeSpan.FromTicks(10), TimeSpan.FromTicks(1)), host);
        var removals = 0;
        var lifetime = new EphemeralInstanceLifetime(InstanceId.Parse(Guid.CreateVersion7().ToString("D")));
        Initialize(lifetime, retention, host, () => { Check(!Monitor.IsEntered(retention.Gate)); removals++; });
        var publication = lifetime.Own();
        var heldPin = Pin(lifetime);
        lifetime.SignalTerminal();
        clock.Ticks += 20;
        lock (retention.Gate) Check(lifetime.TryAccept(CancellationToken.None));
        Check(!lifetime.FinalSettlement.IsCompleted);
        clock.Ticks += 20;
        publication.Dispose();
        Check(lifetime.FinalSettlement.IsCompletedSuccessfully && !lifetime.IsEligible);
        clock.Ticks += 9;
        heldPin.Dispose();
        Check(lifetime.IsEligible && removals == 0);
        heldPin = Pin(lifetime);
        clock.Ticks += 1;
        heldPin.Dispose();
        Check(lifetime.IsRetired && removals == 1 && !lifetime.TryPin(out _));
    }

    private static void Check(bool condition)
    {
        if (!condition) throw new InvalidOperationException("Measurement invariant failed.");
    }
}
