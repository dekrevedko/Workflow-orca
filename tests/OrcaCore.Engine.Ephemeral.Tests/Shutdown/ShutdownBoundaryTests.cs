using AwesomeAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using OrcaCore.Engine.Ephemeral.Execution;
using OrcaCore.Hosting;
using OrcaCore.TestSupport;
using Xunit;
using Host = OrcaCore.Engine.Ephemeral.Tests.Shutdown.HostShutdownTests.Host;
using State = OrcaCore.Engine.Ephemeral.Tests.Shutdown.HostShutdownTests.State;

namespace OrcaCore.Engine.Ephemeral.Tests.Shutdown;

[Trait("Slice", "Issue2Section3")]
[Trait("Disposition", "Implemented")]
public sealed class ShutdownBoundaryTests
{
    private static readonly TimeSpan Guard = TimeSpan.FromSeconds(15);
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    [Trait("AC", "AC-546")]
    public async Task AnAuthoritativeCancellationErrorAfterInit_IsNotReplacedByConcurrentHostStop(bool bounded)
    {
        using var host = new Host(bounded);
        using var unrelated = new CancellationTokenSource(); unrelated.Cancel();
        var original = new OperationCanceledException("original callback cancellation", unrelated.Token);
        var first = 0;
        host.Engine.InstanceCommitted += id =>
        {
            if (Interlocked.Increment(ref first) != 1) return;
            _ = host.Lifetime.StopAsync(TimeSpan.Zero, Token);
            throw original;
        };
        var definition = Workflow.Ephemeral<State>(DefinitionId.New(), DefinitionVersion.Initial).Init<string>(value => new State(value)).End().Build();
        var error = await Record.ExceptionAsync(async () => await host.Start(host.Register(definition)));
        error.Should().BeAssignableTo<OperationCanceledException>().Which.CancellationToken.Should().Be(unrelated.Token);
        (await host.Lifetime.StopAsync(Timeout.InfiniteTimeSpan, Token).AsTask().WaitAsync(Guard, Token)).Should().BeOfType<EphemeralHostShutdownResult.Completed>();
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    [Trait("AC", "AC-543")]
    [Trait("AC", "AC-549")]
    public void FailingHostLifetimeDependency_CannotLeaveAnUnownedRetentionTimer(bool property)
    {
        var clock = new CapturingClock();
        var original = new InvalidOperationException("host application lifetime factory failed");
        var collection = new ServiceCollection();
        collection.AddSingleton<TimeProvider>(clock);
        collection.AddSingleton<IHostApplicationLifetime>(_ => property ? new FailingApplicationLifetime(original) : throw original);
        collection.AddOrcaCoreEphemeralEngine(HostShutdownTests.Options(bounded: true));
        using var provider = collection.BuildServiceProvider();
        Record.Exception(() => provider.GetRequiredService<IEphemeralWorkflowHostLifetime>()).Should().BeSameAs(original);
        clock.Callbacks.Should().BeEmpty("failed DI dependency resolution precedes allocation/arming of owned engine timers");
    }

    [Fact]
    [Trait("AC", "AC-547")]
    public async Task RetentionSweepOwner_IsEstablishedBeforeItsFirstTimerCanBeArmed()
    {
        var clock = new CapturingClock();
        var collection = new ServiceCollection(); collection.AddSingleton<TimeProvider>(clock);
        collection.AddOrcaCoreEphemeralEngine(HostShutdownTests.Options(bounded: true));
        using var provider = collection.BuildServiceProvider();
        var lifetime = provider.GetRequiredService<IEphemeralWorkflowHostLifetime>();
        clock.RetentionOwners.Should().ContainSingle().Which.Should().BeTrue("even a callback entering during engine construction needs the actual host ledger");
        (await lifetime.StopAsync(Timeout.InfiniteTimeSpan, Token).AsTask().WaitAsync(Guard, Token)).Should().BeOfType<EphemeralHostShutdownResult.Completed>();
    }

    [Fact]
    [Trait("AC", "AC-553")]
    public async Task DependencyUsingObserverCleanup_IsOwnedBeforeCompleted_AndCannotEscapeAsAnException()
    {
        using var host = new Host(configure: services => services.AddSingleton<TimeProvider>(new FailingObserverDisposalClock()));
        var start = host.Start(host.Register(host.Definition()));
        await host.Body.Entered.Task.WaitAsync(Guard, Token);
        var observation = host.Lifetime.StopAsync(TimeSpan.FromMinutes(1), Token).AsTask();
        host.Body.Release.TrySetResult();
        await Record.ExceptionAsync(async () => await start.WaitAsync(Guard, Token));
        var error = await Record.ExceptionAsync(async () =>
        {
            var result = (await observation.WaitAsync(Guard, Token)).Should().BeOfType<EphemeralHostShutdownResult.Incomplete>().Subject;
            result.Reason.Should().Be(EphemeralHostShutdownIncompleteReason.ShutdownFaulted);
            result.PendingWorkCount.Should().BeGreaterThan(0);
        });
        error.Should().BeNull("observer cleanup using a clock dependency must settle before a Completed observation, and cleanup failure is a sticky accounting fault");
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    [Trait("AC", "AC-551")]
    [Trait("AC", "AC-554")]
    public async Task RawProviderDisposal_OnlyClosesAndDiagnosesAbortiveTeardown_WithoutJoiningOrCancelling(bool bounded)
    {
        using var host = new Host(bounded);
        var handle = host.Register(host.Definition());
        var pending = host.Start(handle);
        await host.Body.Entered.Task.WaitAsync(Guard, Token);
        var abortive = 0;
        using var listener = ShutdownOwnershipTests.ShutdownListener(name => { if (name == "shutdown-abortive-disposal") Interlocked.Increment(ref abortive); });
        await Task.Run(host.Services.Dispose, Token).WaitAsync(Guard, Token);
        pending.IsCompleted.Should().BeFalse("raw disposal cannot join a blocked physical body");
        host.Body.CancellationObserved.Task.IsCompleted.Should().BeFalse("raw disposal only closes; it is not a hosted/public stop");
        Volatile.Read(ref abortive).Should().BeGreaterThan(0);
        (await Record.ExceptionAsync(async () => await host.Start(handle, "after-disposal"))).Should().BeOfType<ObjectDisposedException>();
        host.Body.Release.TrySetResult();
        await Record.ExceptionAsync(async () => await pending.WaitAsync(Guard, Token));
    }

    [Fact]
    [Trait("AC", "AC-550")]
    [Trait("AC", "AC-553")]
    public async Task AccountingFailure_IsStickyAndRetainsAPositiveFaultObligation()
    {
        var storage = new FailingRegistry();
        using var engine = new EphemeralWorkflowEngine(TimeProvider.System, storage, new InstanceExecutionLane());
        var first = await engine.HostLifetime.StopAsync(Timeout.InfiniteTimeSpan, Token).AsTask().WaitAsync(Guard, Token);
        var incomplete = first.Should().BeOfType<EphemeralHostShutdownResult.Incomplete>().Subject;
        incomplete.Reason.Should().Be(EphemeralHostShutdownIncompleteReason.ShutdownFaulted);
        incomplete.PendingWorkCount.Should().BeGreaterThan(0);
        storage.Failing = false;
        var second = (await engine.HostLifetime.StopAsync(TimeSpan.Zero, Token)).Should().BeOfType<EphemeralHostShutdownResult.Incomplete>().Subject;
        second.Reason.Should().Be(EphemeralHostShutdownIncompleteReason.ShutdownFaulted);
        second.PendingWorkCount.Should().BeGreaterThan(0, "restoring storage does not reconstruct a lost ownership proof");
    }

    [Fact]
    [Trait("AC", "AC-552")]
    public async Task Completed_DoesNotReadTheClock_OrEnterStaleTimerCallbacks()
    {
        var clock = new CapturingClock();
        using var bodyServices = new ServiceCollection().BuildServiceProvider();
        using var engine = new EphemeralWorkflowEngine(clock, new EphemeralWorkflowEngineOptions
        {
            TerminalRetention = new EphemeralTerminalRetentionSettings(4, TimeSpan.FromMinutes(1), TimeSpan.FromSeconds(1)),
            StuckStepThreshold = TimeSpan.FromSeconds(10)
        }, bodyServices);
        clock.AdmissionGate = engine.VisibilityGate;
        var registry = new EphemeralWorkflowDefinitionRegistry(engine);
        var definition = Workflow.Ephemeral<State>(DefinitionId.New(), DefinitionVersion.Initial)
            .Init<string>(value => new State(value)).Then(_ => ValueTask.CompletedTask).WithStepTimeout(TimeSpan.FromSeconds(30)).End().Build();
        await registry.Register(definition).GetHandleOrThrow().StartOrGetAsync("clock", StartIdempotencyKey.Create("clock"), Token);
        clock.Callbacks.Count.Should().BeGreaterThanOrEqualTo(3);
        var result = await engine.HostLifetime.StopAsync(Timeout.InfiniteTimeSpan, Token).AsTask().WaitAsync(Guard, Token);
        result.Should().BeOfType<EphemeralHostShutdownResult.Completed>();
        clock.RejectAccess = true;
        foreach (var callback in clock.Callbacks) Record.Exception(callback).Should().BeNull("sealed trampolines only gate-check and return");
        (await engine.HostLifetime.StopAsync(TimeSpan.FromMilliseconds(4_294_967_294), Token)).Should().BeSameAs(result);
        (await engine.HostLifetime.StopAsync(TimeSpan.Zero, Token)).Should().BeSameAs(result);
    }

    [Fact]
    [Trait("AC", "AC-552")]
    public async Task Completed_RejectsStartsAndTypedIngressBeforeInvokingPayloadCode()
    {
        using var host = new Host();
        var definition = Workflow.Ephemeral<State>(DefinitionId.New(), DefinitionVersion.Initial).Init<Payload>(_ => new State("unused")).End().Build();
        var handle = host.Registry.Register(definition).GetHandleOrThrow();
        var payload = new Payload();
        // Use a separate ordinary definition to obtain a valid retained instance.
        var completed = Workflow.Ephemeral<State>(DefinitionId.New(), DefinitionVersion.Initial).Init<string>(value => new State(value)).End().Build();
        var instance = (await host.Start(host.Register(completed))).GetHandleOrThrow();
        var inbound = WorkflowInboundEvent<Payload>.Create(
            WorkflowEventContract<Payload>.Create(EventName.Create("shutdown-payload"), EventContractVersion.Initial),
            EventId.Create("payload"), CorrelationId.Create("payload"), null, host.Clock.Now,
            new WorkflowEventRoute.Direct(instance.InstanceId), payload);
        await host.Lifetime.StopAsync(Timeout.InfiniteTimeSpan, Token).AsTask().WaitAsync(Guard, Token);
        Payload.Reads = 0;
        Payload.RejectReads = true;
        try
        {
            var start = await Record.ExceptionAsync(async () => await handle.StartOrGetAsync(payload, StartIdempotencyKey.Create("payload"), Token));
            start.Should().BeOfType<EphemeralHostStoppingException>();
            var ingress = await Record.ExceptionAsync(async () => await host.Services.GetRequiredService<EphemeralWorkflowEventRouter>().RouteAsync(inbound, Token));
            ingress.Should().BeOfType<EphemeralHostStoppingException>();
            Payload.Reads.Should().Be(0);
        }
        finally { Payload.RejectReads = false; }
    }

    [Fact]
    [Trait("AC", "AC-544")]
    public async Task DisposedRoot_IsCheckedAfterValidationAndCancellation()
    {
        var services = new ServiceCollection(); services.AddOrcaCoreEphemeralEngine(HostShutdownTests.Options());
        var provider = services.BuildServiceProvider();
        var lifetime = provider.GetRequiredService<IEphemeralWorkflowHostLifetime>();
        await lifetime.StopAsync(Timeout.InfiniteTimeSpan, Token).AsTask().WaitAsync(Guard, Token);
        provider.Dispose();
        (await Record.ExceptionAsync(async () => await lifetime.StopAsync(TimeSpan.Zero, Token))).Should().BeOfType<ObjectDisposedException>();
        using var cancelled = new CancellationTokenSource(); cancelled.Cancel();
        (await Record.ExceptionAsync(async () => await lifetime.StopAsync(TimeSpan.Zero, cancelled.Token)))
            .Should().BeAssignableTo<OperationCanceledException>().Which.CancellationToken.Should().Be(cancelled.Token);
        (await Record.ExceptionAsync(async () => await lifetime.StopAsync(TimeSpan.FromTicks(-1), cancelled.Token)))
            .Should().BeOfType<ArgumentOutOfRangeException>().Which.ParamName.Should().Be("timeout");
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    [Trait("AC", "AC-553")]
    public async Task AdmissionBudgetClockFailures_ReturnStickyIncompleteInsteadOfLosingTheStop(bool timestamp)
    {
        var clock = new FailingBudgetClock(timestamp);
        using var host = new Host(configure: services => services.AddSingleton<TimeProvider>(clock));
        var pending = host.Start(host.Register(host.Definition()));
        await host.Body.Entered.Task.WaitAsync(Guard, Token);
        var observed = await Record.ExceptionAsync(async () =>
        {
            var result = await host.Lifetime.StopAsync(TimeSpan.FromSeconds(1), Token).AsTask().WaitAsync(Guard, Token);
            result.Should().BeOfType<EphemeralHostShutdownResult.Incomplete>().Which.Reason.Should().Be(EphemeralHostShutdownIncompleteReason.ShutdownFaulted);
        });
        host.Body.Release.TrySetResult();
        await Record.ExceptionAsync(async () => await pending.WaitAsync(Guard, Token));
        observed.Should().BeNull("dependency-using observation setup is owned and a failure cannot silently lose shutdown accounting");
    }

    private sealed class Payload
    {
        internal static int Reads;
        internal static bool RejectReads;
        public string Value
        {
            get
            {
                Interlocked.Increment(ref Reads);
                if (RejectReads) throw new InvalidOperationException("payload code cannot run after Completed");
                return "payload";
            }
        }
    }

    private sealed class CapturingClock : TimeProvider
    {
        private readonly Clock inner = new(new DateTimeOffset(2026, 10, 19, 0, 0, 0, TimeSpan.Zero));
        internal List<Action> Callbacks { get; } = [];
        internal List<bool> RetentionOwners { get; } = [];
        internal bool RejectAccess;
        internal object? AdmissionGate;
        private void Check()
        {
            if (RejectAccess) throw new InvalidOperationException("clock dependency was accessed after Completed");
            if (AdmissionGate is not null) Monitor.IsEntered(AdmissionGate).Should().BeFalse("clock methods may contain application code");
        }
        public override long GetTimestamp() { Check(); return inner.TimeProvider.GetTimestamp(); }
        public override long TimestampFrequency { get { Check(); return inner.TimeProvider.TimestampFrequency; } }
        public override DateTimeOffset GetUtcNow() { Check(); return inner.Now; }
        public override ITimer CreateTimer(TimerCallback callback, object? state, TimeSpan dueTime, TimeSpan period)
        {
            Check(); Callbacks.Add(() => callback(state));
            if (callback.Target is EphemeralRetentionCoordinator retention) RetentionOwners.Add(retention.HostLifetime is not null);
            return inner.TimeProvider.CreateTimer(callback, state, dueTime, period);
        }
    }

    private sealed class FailingBudgetClock(bool timestamp) : TimeProvider
    {
        public override long GetTimestamp() => timestamp ? throw new InvalidOperationException("timestamp failed") : base.GetTimestamp();
        public override ITimer CreateTimer(TimerCallback callback, object? state, TimeSpan dueTime, TimeSpan period) => throw new InvalidOperationException("observer timer failed");
    }

    private sealed class FailingApplicationLifetime(Exception error) : IHostApplicationLifetime
    {
        public CancellationToken ApplicationStarted => CancellationToken.None;
        public CancellationToken ApplicationStopping => throw error;
        public CancellationToken ApplicationStopped => CancellationToken.None;
        public void StopApplication() { }
    }

    private sealed class FailingObserverDisposalClock : TimeProvider
    {
        public override ITimer CreateTimer(TimerCallback callback, object? state, TimeSpan dueTime, TimeSpan period) =>
            new FailingTimer(base.CreateTimer(callback, state, dueTime, period));
        private sealed class FailingTimer(ITimer inner) : ITimer
        {
            public bool Change(TimeSpan dueTime, TimeSpan period) => inner.Change(dueTime, period);
            public void Dispose() { inner.Dispose(); throw new InvalidOperationException("observer timer cleanup failed"); }
            public ValueTask DisposeAsync() { Dispose(); return ValueTask.CompletedTask; }
        }
    }

    private sealed class FailingRegistry : IInstanceRegistry
    {
        internal bool Failing = true;
        public void Save<TState>(WorkflowInstance<TState> instance) { }
        public IReadOnlyCollection<object> GetMany(IReadOnlyCollection<InstanceId> ids) => [];
        public bool TryGet(InstanceId instanceId, out object? instance) { instance = null; return false; }
        public IReadOnlyCollection<object> List() => Failing ? throw new InvalidOperationException("accounting inventory failed") : [];
        public bool Remove(InstanceId instanceId) => false;
    }
}
