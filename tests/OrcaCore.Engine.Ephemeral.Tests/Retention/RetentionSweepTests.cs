using AwesomeAssertions;
using System.Diagnostics.Metrics;
using OrcaCore.Engine.Ephemeral.Execution;
using Xunit;

namespace OrcaCore.Engine.Ephemeral.Tests.Retention;

[Trait("AC", "AC-536")]
public sealed class RetentionSweepTests
{
    [Fact]
    public void ThrowingCleanup_IsDiagnosedWithoutEscapingTimer_AndLaterIdleSweepsContinue()
    {
        var clock = new ManualClock();
        using var coordinator = new EphemeralRetentionCoordinator(clock, new(3, TimeSpan.FromTicks(10), TimeSpan.FromTicks(1)));
        var failures = 0L;
        using var listener = new MeterListener();
        listener.InstrumentPublished = (instrument, current) =>
        {
            if (instrument.Name == "orca.lifecycle.events") current.EnableMeasurementEvents(instrument);
        };
        listener.SetMeasurementEventCallback<long>((_, value, tags, _) =>
        {
            var values = tags.ToArray();
            if (!values.Any(tag => tag.Key == "orca.lifecycle.origin" && Equals(tag.Value, "retention-sweep"))) return;
            values.Select(tag => tag.Key).Should().BeEquivalentTo("event.name", "durable", "orca.lifecycle.origin");
            values.Single(tag => tag.Key == "event.name").Value.Should().Be("cleanup-failed");
            Interlocked.Add(ref failures, value);
            throw new InvalidOperationException("the diagnostic listener also fails");
        });
        listener.Start();
        var removed = 0;
        void Add(Action cleanup)
        {
            var life = new EphemeralInstanceLifetime(InstanceId.Parse(Guid.NewGuid().ToString()));
            life.Initialize(cleanup, null, coordinator.Gate, coordinator.Changed);
            coordinator.Track(life);
            life.SignalTerminal();
            life.TryAccept(CancellationToken.None).Should().BeTrue();
        }
        Add(() => throw new InvalidOperationException("injected retirement cleanup failure"));
        Add(() => Interlocked.Increment(ref removed));
        clock.Ticks = 10;
        Record.Exception(clock.Pulse).Should().BeNull("a system timer must never receive a cleanup or diagnostic exception");
        removed.Should().Be(1, "one failing unit must not prevent cleanup of the other selected units");
        failures.Should().Be(1);
        coordinator.TrackedCount.Should().Be(0);
        clock.Timer!.Changes.Should().Be(2);
        Add(() => Interlocked.Increment(ref removed));
        clock.Ticks = 20;
        Record.Exception(clock.Pulse).Should().BeNull();
        removed.Should().Be(2, "the next idle sweep must still run");
        failures.Should().Be(1);
        clock.Timer.Changes.Should().Be(3);
    }

    [Fact]
    public async Task ConcurrentSweep_DoesNotOverlapCleanup_AndDisposalStopsRearming()
    {
        var clock = new ManualClock();
        using var coordinator = new EphemeralRetentionCoordinator(clock, new(1, TimeSpan.FromTicks(10), TimeSpan.FromTicks(1)));
        using var release = new ManualResetEventSlim();
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var removals = 0;
        var life = new EphemeralInstanceLifetime(InstanceId.Parse(Guid.NewGuid().ToString()));
        life.Initialize(() =>
        {
            Monitor.IsEntered(coordinator.Gate).Should().BeFalse();
            Interlocked.Increment(ref removals);
            entered.TrySetResult();
            release.Wait(TimeSpan.FromSeconds(10)).Should().BeTrue();
        }, null, coordinator.Gate, coordinator.Changed);
        coordinator.Track(life); life.SignalTerminal(); life.TryAccept(CancellationToken.None).Should().BeTrue();
        clock.Ticks = 10;
        var first = Task.Run(clock.Pulse, TestContext.Current.CancellationToken);
        try
        {
            await entered.Task.WaitAsync(TimeSpan.FromSeconds(10), TestContext.Current.CancellationToken);
            await Task.Run(clock.Pulse, TestContext.Current.CancellationToken).WaitAsync(TimeSpan.FromSeconds(10), TestContext.Current.CancellationToken);
            removals.Should().Be(1);
            coordinator.Dispose();
        }
        finally { release.Set(); }
        await first.WaitAsync(TimeSpan.FromSeconds(10), TestContext.Current.CancellationToken);
        clock.Timer!.Changes.Should().Be(1, "an in-flight disposed sweep cannot rearm the timer");
        coordinator.TrackedCount.Should().Be(0);
    }

    [Fact]
    public void OneTickInterval_RearmsWithoutAZeroPeriod_AndDisabledPolicyCreatesNoTimer()
    {
        var clock = new ManualClock();
        using (var disabled = new EphemeralRetentionCoordinator(clock, null)) clock.Timer.Should().BeNull();
        using var coordinator = new EphemeralRetentionCoordinator(clock, new(1, TimeSpan.FromSeconds(1), TimeSpan.FromTicks(1)));
        clock.Pulse(); clock.Pulse();
        clock.Timer!.Changes.Should().Be(3);
        clock.Timer.Due.Should().Be(TimeSpan.FromTicks(1));
        clock.Timer.Period.Should().Be(Timeout.InfiniteTimeSpan);
    }

    private sealed class ManualClock : TimeProvider
    {
        internal long Ticks;
        internal ManualTimer? Timer;
        public override long GetTimestamp() => Ticks;
        public override long TimestampFrequency => TimeSpan.TicksPerSecond;
        internal void Pulse() => Timer!.Callback();
        public override ITimer CreateTimer(TimerCallback callback, object? state, TimeSpan dueTime, TimeSpan period) =>
            Timer = new ManualTimer(() => callback(state));
    }
    private sealed class ManualTimer(Action callback) : ITimer
    {
        internal int Changes;
        internal TimeSpan Due, Period;
        private bool disposed;
        internal void Callback() { if (!disposed) callback(); }
        public bool Change(TimeSpan dueTime, TimeSpan period)
        {
            if (disposed) throw new ObjectDisposedException(nameof(ManualTimer));
            Changes++; Due = dueTime; Period = period; return true;
        }
        public void Dispose() => disposed = true;
        public ValueTask DisposeAsync() { Dispose(); return ValueTask.CompletedTask; }
    }
}
