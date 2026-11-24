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
            life.Initialize(cleanup, null, coordinator);
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
        }, null, coordinator);
        life.SignalTerminal(); life.TryAccept(CancellationToken.None).Should().BeTrue();
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

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    public void DelayedSettlementSample_UsesCommitOrderForCapacityAndSampleOrderForAge(int capacity)
    {
        var clock = new ManualClock { Ticks = 1 };
        using var coordinator = new EphemeralRetentionCoordinator(clock, new(capacity, TimeSpan.FromTicks(10), TimeSpan.FromTicks(1)));
        clock.GateToCheck = coordinator.Gate;
        var earlierSample = Accepted(coordinator);
        var laterSample = Accepted(coordinator);
        using var earlierWork = earlierSample.Own();
        using var laterWork = laterSample.Own();
        earlierSample.SignalTerminal();
        laterSample.SignalTerminal();

        clock.AfterNextTimestamp = () =>
        {
            clock.Ticks = 5;
            laterWork.Dispose(); // Its settlement ordinal is assigned while the older sample is still returning.
        };
        earlierWork.Dispose();
        laterSample.FinalSettlement.IsCompletedSuccessfully.Should().BeTrue();
        earlierSample.IsRetired.Should().BeFalse();
        laterSample.IsRetired.Should().Be(capacity == 1);

        clock.Ticks = 11;
        clock.Pulse();
        earlierSample.IsRetired.Should().BeTrue();
        laterSample.IsRetired.Should().Be(capacity == 1);
        clock.Ticks = 15;
        clock.Pulse();
        laterSample.IsRetired.Should().BeTrue();
        clock.ReadInsideGate.Should().BeFalse();
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void BulkExpiryPreservesYoungerPinnedAndUnsettledInstances(bool includeYounger)
    {
        var clock = new ManualClock();
        using var coordinator = new EphemeralRetentionCoordinator(clock, new(4, TimeSpan.FromTicks(10), TimeSpan.FromTicks(1)));
        var first = Accepted(coordinator);
        var second = Accepted(coordinator);
        var pinned = Accepted(coordinator);
        var running = Accepted(coordinator);
        pinned.TryPin(out var pin).Should().BeTrue();
        using var work = running.Own();
        foreach (var life in new[] { first, second, pinned, running }) life.SignalTerminal();
        clock.Ticks = 5;
        var younger = includeYounger ? Accepted(coordinator) : null;
        younger?.SignalTerminal();

        clock.Ticks = 10;
        clock.Pulse();
        first.IsRetired.Should().BeTrue();
        second.IsRetired.Should().BeTrue();
        pinned.IsRetired.Should().BeFalse();
        running.IsRetired.Should().BeFalse();
        if (younger is not null) younger.IsRetired.Should().BeFalse();
        coordinator.TrackedCount.Should().Be(includeYounger ? 3 : 2);

        pin!.Dispose();
        pinned.IsRetired.Should().BeTrue();
        work.Dispose();
        running.IsRetired.Should().BeFalse();
        clock.Ticks = 15;
        clock.Pulse();
        if (younger is not null) younger.IsRetired.Should().BeTrue();
        running.IsRetired.Should().BeFalse();
        clock.Ticks = 20;
        clock.Pulse();
        running.IsRetired.Should().BeTrue();
        coordinator.TrackedCount.Should().Be(0);
    }

    [Fact]
    public void SweepSampledBeforeWrap_DoesNotExpireAnInstanceSettledAfterTheSample()
    {
        var clock = new ManualClock { Ticks = long.MaxValue - 3 };
        using var coordinator = new EphemeralRetentionCoordinator(clock, new(2, TimeSpan.FromTicks(4), TimeSpan.FromTicks(1)));
        clock.GateToCheck = coordinator.Gate;
        var older = Accepted(coordinator);
        var newer = Accepted(coordinator);
        older.SignalTerminal();
        clock.AfterNextTimestamp = () =>
        {
            clock.Ticks = long.MinValue + 1;
            newer.SignalTerminal();
        };
        clock.Pulse();
        newer.FinalSettlement.IsCompletedSuccessfully.Should().BeTrue();
        older.IsRetired.Should().BeTrue();
        newer.IsRetired.Should().BeFalse();
        clock.Ticks += 4;
        clock.Pulse();
        newer.IsRetired.Should().BeTrue();
        clock.ReadInsideGate.Should().BeFalse();
    }

    [Theory]
    [InlineData(long.MaxValue - 1L, 3L, TimeSpan.TicksPerSecond + 1L, 3L)]
    [InlineData(-2L, 3L, TimeSpan.TicksPerSecond + 1L, 3L)]
    [InlineData(long.MaxValue - 1L, 9_007_199_254_740_993L, TimeSpan.TicksPerSecond, 9_007_199_254_740_992L)]
    public void ExpiryAcrossTimestampWrap_UsesTheExactFirstDueClockTick(long start, long frequency, long ageTicks, long lastNotDue)
    {
        var clock = new ManualClock { Ticks = start, Frequency = frequency };
        using var coordinator = new EphemeralRetentionCoordinator(clock, new(1, TimeSpan.FromTicks(ageTicks), TimeSpan.FromTicks(1)));
        var life = Accepted(coordinator);
        var overdue = Accepted(coordinator);
        overdue.TryPin(out var pin).Should().BeTrue();
        overdue.SignalTerminal();
        life.SignalTerminal();
        clock.Ticks = unchecked(start + lastNotDue);
        clock.Pulse();
        life.IsRetired.Should().BeFalse();
        clock.Ticks++;
        clock.Pulse();
        life.IsRetired.Should().BeTrue();
        clock.Ticks += 2;
        pin!.Dispose();
        overdue.IsRetired.Should().BeTrue("an overdue stamp can lie in the high segment of a wrapped expiry interval");
    }

    [Fact]
    public void AgeBeyondSignedElapsedRange_StillAllowsCapacityRetirement()
    {
        var clock = new ManualClock { Frequency = long.MaxValue };
        using var coordinator = new EphemeralRetentionCoordinator(clock, new(1, TimeSpan.FromSeconds(2), TimeSpan.FromTicks(1)));
        var older = Accepted(coordinator);
        older.SignalTerminal();
        clock.Ticks = long.MaxValue;
        clock.Pulse();
        older.IsRetired.Should().BeFalse();
        var newer = Accepted(coordinator);
        newer.SignalTerminal();
        older.IsRetired.Should().BeTrue();
        newer.IsRetired.Should().BeFalse();
    }

    [Fact]
    public async Task AcceptingASynchronouslyCompletedStartReadsTheClockOutsideTheAdmissionGate()
    {
        var clock = new ManualClock();
        using var engine = new EphemeralWorkflowEngine(clock, new EphemeralWorkflowEngineOptions
        {
            TerminalRetention = new(1, TimeSpan.FromTicks(10), TimeSpan.FromTicks(1))
        });
        var definitionId = DefinitionId.New();
        new EphemeralWorkflowDefinitionRegistry(engine).Register(Workflow.Ephemeral<string>(definitionId, DefinitionVersion.Initial)
            .Init<string>(input => input).End(state => state.Value).Build()).GetHandleOrThrow();
        var token = TestContext.Current.CancellationToken;
        var snapshot = await engine.StartDetachedCoreAsync<string, string>(definitionId, DefinitionVersion.Initial, "complete", token);
        snapshot.Status.Should().Be(WorkflowInstanceStatus.Completed);
        var sampled = false;
        clock.GateToCheck = engine.VisibilityGate;
        clock.AfterNextTimestamp = () => sampled = true;
        engine.TryAcceptStart(snapshot.InstanceId, token).Should().BeTrue();
        sampled.Should().BeTrue();
        clock.ReadInsideGate.Should().BeFalse();
    }

    [Theory]
    [InlineData(2, 9, false)]
    [InlineData(2, 10, true)]
    [InlineData(1, 9, true)]
    public void LastUnpinRestoresTheOriginalAgeAndOrdinal(int capacity, int unpinTime, bool retiresOnUnpin)
    {
        var clock = new ManualClock();
        using var coordinator = new EphemeralRetentionCoordinator(clock, new(capacity, TimeSpan.FromTicks(10), TimeSpan.FromTicks(1)));
        var oldest = Accepted(coordinator);
        oldest.TryPin(out var publicationPin).Should().BeTrue();
        oldest.SignalTerminal();
        clock.Ticks = 2;
        publicationPin!.Dispose();
        oldest.TryPin(out var first).Should().BeTrue();
        oldest.TryPin(out var last).Should().BeTrue();
        first!.Dispose();

        clock.Ticks = 5;
        var newer = Accepted(coordinator);
        newer.SignalTerminal();
        oldest.IsRetired.Should().BeFalse();
        newer.IsRetired.Should().BeFalse();
        clock.Ticks = unpinTime;
        last!.Dispose();
        oldest.IsRetired.Should().Be(retiresOnUnpin);
        clock.Ticks = 10;
        clock.Pulse();
        oldest.IsRetired.Should().BeTrue();
        newer.IsRetired.Should().BeFalse();
    }

    [Fact]
    public void ManualRetirementFreesCapacityBeforeItsCleanupRuns()
    {
        var clock = new ManualClock();
        using var coordinator = new EphemeralRetentionCoordinator(clock, new(2, TimeSpan.FromDays(1), TimeSpan.FromDays(1)));
        var oldest = Accepted(coordinator);
        oldest.SignalTerminal();
        var cleaned = false;
        var newer = Accepted(coordinator, () =>
        {
            Monitor.IsEntered(coordinator.Gate).Should().BeFalse();
            cleaned = true;
        });
        newer.SignalTerminal();
        var cleanup = newer.SelectRetirement();
        cleanup.Should().NotBeNull();
        try
        {
            newer.TryPin(out _).Should().BeFalse();
            var third = Accepted(coordinator);
            third.SignalTerminal();
            oldest.IsRetired.Should().BeFalse();
            third.IsRetired.Should().BeFalse();
            cleaned.Should().BeFalse();
        }
        finally { cleanup!(); }
        cleaned.Should().BeTrue();
    }

    private static EphemeralInstanceLifetime Accepted(EphemeralRetentionCoordinator coordinator, Action? cleanup = null)
    {
        var life = new EphemeralInstanceLifetime(InstanceId.Parse(Guid.CreateVersion7().ToString()));
        life.Initialize(cleanup ?? (() => { }), null, coordinator);
        life.TryAccept(CancellationToken.None).Should().BeTrue();
        return life;
    }

    private sealed class ManualClock : TimeProvider
    {
        internal long Ticks;
        internal ManualTimer? Timer;
        internal long Frequency = TimeSpan.TicksPerSecond;
        internal Action? AfterNextTimestamp;
        internal object? GateToCheck;
        internal bool ReadInsideGate;
        private void CheckGate() => ReadInsideGate |= GateToCheck is { } gate && Monitor.IsEntered(gate);
        public override long GetTimestamp()
        {
            CheckGate();
            var sample = Ticks;
            var after = AfterNextTimestamp;
            AfterNextTimestamp = null;
            after?.Invoke();
            return sample;
        }
        public override long TimestampFrequency { get { CheckGate(); return Frequency; } }
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
