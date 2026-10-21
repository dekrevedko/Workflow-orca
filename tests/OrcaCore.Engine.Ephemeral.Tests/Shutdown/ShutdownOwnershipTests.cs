using System.Diagnostics.Metrics;
using AwesomeAssertions;
using Microsoft.Extensions.DependencyInjection;
using OrcaCore.Engine.Ephemeral.Execution;
using OrcaCore.Hosting;
using OrcaCore.TestSupport;
using OrcaCore.Engine.Ephemeral.Tests.Support;
using Xunit;
using Host = OrcaCore.Engine.Ephemeral.Tests.Shutdown.HostShutdownTests.Host;
using State = OrcaCore.Engine.Ephemeral.Tests.Shutdown.HostShutdownTests.State;

namespace OrcaCore.Engine.Ephemeral.Tests.Shutdown;

[Trait("Slice", "Issue2Section3")]
[Trait("Disposition", "Implemented")]
public sealed class ShutdownOwnershipTests
{
    private static readonly TimeSpan Guard = TimeSpan.FromSeconds(15);
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Fact]
    [Trait("AC", "AC-547")]
    public async Task EnqueueNotificationFailure_CannotAbandonAnAlreadyRunningPreInstanceInit()
    {
        var initialized = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        using var release = new ManualResetEventSlim();
        var original = new InvalidOperationException("enqueue observer failed");
        var enqueues = 0;
        using var engine = new EphemeralWorkflowEngine(TimeProvider.System, new EphemeralWorkflowEngineOptions
        {
            LaneWorkItemEnqueued = _ =>
            {
                if (Interlocked.Increment(ref enqueues) != 1) return;
                initialized.Task.Wait(Guard, Token).Should().BeTrue();
                throw original;
            }
        });
        IWorkflowInstance? runtime = null;
        engine.InstanceCommitted += id => { if (engine.TryGetFacadeInstance(id, out var found)) runtime = found; };
        var registry = new EphemeralWorkflowDefinitionRegistry(engine);
        var definition = Workflow.Ephemeral<State>(DefinitionId.New(), DefinitionVersion.Initial).Init<string>(value =>
        {
            initialized.TrySetResult(); release.Wait(Guard).Should().BeTrue(); return new State(value);
        }).End().Build();
        var start = registry.Register(definition).GetHandleOrThrow().StartOrGetAsync("notification", StartIdempotencyKey.Create("notification"), Token).AsTask();
        try { start.IsCompleted.Should().BeFalse("an enqueue observer failure must retain the queued operation until its instance can be fenced"); }
        finally { release.Set(); }
        (await Record.ExceptionAsync(async () => await start.WaitAsync(Guard, Token))).Should().BeSameAs(original);
        runtime.Should().NotBeNull();
        await runtime!.Lifetime.WhenSettled.WaitAsync(Guard, Token);
        runtime.Lifetime.IsRetired.Should().BeTrue();
        (await engine.HostLifetime.StopAsync(Timeout.InfiniteTimeSpan, Token).AsTask().WaitAsync(Guard, Token))
            .Should().BeOfType<EphemeralHostShutdownResult.Completed>();
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    [Trait("AC", "AC-550")]
    public async Task AThrowingOrBlockedStepTimeoutHandler_CannotEscapeTheTimer_AndRemainsJoined(bool throwing)
    {
        using var body = new CallbackBody(throwing);
        using var host = new Host(configure: services => services.AddSingleton(body));
        var definition = Workflow.Ephemeral<State>(DefinitionId.New(), DefinitionVersion.Initial).Init<string>(value => new State(value))
            .Then<CallbackBody>().WithStepTimeout(TimeSpan.FromSeconds(30)).End().Build();
        var start = host.Start(host.Register(definition));
        await body.Entered.Task.WaitAsync(Guard, Token);
        try
        {
            Record.Exception(() => host.Clock.Advance(TimeSpan.FromSeconds(30))).Should().BeNull("a step-token callback cannot escape a system timer");
            await body.CallbackEntered.Task.WaitAsync(Guard, Token);
            var instance = (await start.WaitAsync(Guard, Token)).GetHandleOrThrow();
            (await instance.GetSnapshotAsync(Token)).Status.Should().Be(WorkflowInstanceStatus.Failed);
            body.ReleaseBody.TrySetResult();
            await body.BodyReturned.Task.WaitAsync(Guard, Token);
            (await host.Lifetime.StopAsync(TimeSpan.Zero, Token)).Should().BeOfType<EphemeralHostShutdownResult.Incomplete>();
        }
        finally { body.ReleaseBody.TrySetResult(); body.ReleaseCallback.Set(); }
        (await host.Lifetime.StopAsync(Timeout.InfiniteTimeSpan, Token).AsTask().WaitAsync(Guard, Token))
            .Should().BeOfType<EphemeralHostShutdownResult.Completed>();
    }

    [Fact]
    [Trait("AC", "AC-550")]
    public async Task LogicalTimeout_CannotDisposeTheSourceBeforeAllQueuedCancellationHandlersReturn()
    {
        using var body = new QueuedCallbackBody();
        using var host = new Host(configure: services => services.AddSingleton(body));
        var definition = Workflow.Ephemeral<State>(DefinitionId.New(), DefinitionVersion.Initial).Init<string>(value => new State(value))
            .Then<QueuedCallbackBody>().WithStepTimeout(TimeSpan.FromSeconds(30)).End().Build();
        var start = host.Start(host.Register(definition));
        await body.Entered.Task.WaitAsync(Guard, Token);
        try
        {
            host.Clock.Advance(TimeSpan.FromSeconds(30));
            await body.BlockingCallbackEntered.Task.WaitAsync(Guard, Token);
            var instance = (await start.WaitAsync(Guard, Token)).GetHandleOrThrow();
            (await instance.GetSnapshotAsync(Token)).Status.Should().Be(WorkflowInstanceStatus.Failed);
        }
        finally { body.ReleaseBody.TrySetResult(); body.ReleaseCallback.Set(); }
        (await host.Lifetime.StopAsync(Timeout.InfiniteTimeSpan, Token).AsTask().WaitAsync(Guard, Token))
            .Should().BeOfType<EphemeralHostShutdownResult.Completed>();
        body.EarlierCallbackCount.Should().Be(1, "logical timeout must not discard callbacks queued behind a running handler");
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    [Trait("AC", "AC-546")]
    [Trait("AC", "AC-549")]
    public async Task AdmittedInit_RemainsOwnedBeforeAnInstanceExists_AndOnlyStoppingAbortsIt(bool bounded, bool stopping)
    {
        using var host = new Host(bounded);
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        using var release = new ManualResetEventSlim();
        var definition = Workflow.Ephemeral<State>(DefinitionId.New(), DefinitionVersion.Initial).Init<string>(value =>
        {
            Monitor.IsEntered(host.Engine.VisibilityGate).Should().BeFalse();
            entered.TrySetResult();
            release.Wait(Guard).Should().BeTrue();
            return new State(value);
        }).End().Build();
        var start = host.Start(host.Register(definition));
        try
        {
            await entered.Task.WaitAsync(Guard, Token);
            host.Initialized.Should().BeNull();
            host.Application.StopApplication();
            if (stopping) (await host.Lifetime.StopAsync(TimeSpan.Zero, Token)).Should().BeOfType<EphemeralHostShutdownResult.Incomplete>();
        }
        finally { release.Set(); }
        if (stopping)
            (await Record.ExceptionAsync(async () => await start.WaitAsync(Guard, Token))).Should().BeOfType<EphemeralHostStoppingException>();
        else (await start.WaitAsync(Guard, Token)).Should().BeOfType<WorkflowStartResult<WorkflowInstanceHandle>.Accepted>();
        (await host.Lifetime.StopAsync(Timeout.InfiniteTimeSpan, Token).AsTask().WaitAsync(Guard, Token)).Should().BeOfType<EphemeralHostShutdownResult.Completed>();
    }

    [Theory]
    [InlineData("snapshot")]
    [InlineData("state")]
    [InlineData("output")]
    [InlineData("wait")]
    [Trait("AC", "AC-548")]
    [Trait("AC", "AC-552")]
    public async Task AnAdmittedPinnedOperation_IsJoinedThroughItsReturnOrNormalTerminalError(string operation)
    {
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        using var engine = new EphemeralWorkflowEngine(TimeProvider.System, new EphemeralWorkflowEngineOptions
        {
            OperationPinAcquired = _ => { entered.TrySetResult(); return new ValueTask(release.Task); }
        });
        var registry = new EphemeralWorkflowDefinitionRegistry(engine);
        var definition = Workflow.Ephemeral<State>(DefinitionId.New(), DefinitionVersion.Initial).Init<string>(value => new State(value))
            .Delay(TimeSpan.FromDays(1)).End(state => state.Value.Value, WorkflowOutcomeName.Create("done")).Build();
        var instance = (await registry.Register(definition).GetHandleOrThrow().StartOrGetAsync("pin", StartIdempotencyKey.Create("pin"), Token)).GetHandleOrThrow();
        var observing = operation switch
        {
            "state" => instance.GetStateAsync<State>(Token).AsTask(),
            "output" => (Task)instance.GetOutputAsync(Token).AsTask(),
            "wait" => instance.WaitForOutputAsync(Token).AsTask(),
            _ => instance.GetSnapshotAsync(Token).AsTask()
        };
        await entered.Task.WaitAsync(Guard, Token);
        var stopping = engine.HostLifetime.StopAsync(Timeout.InfiniteTimeSpan, Token).AsTask();
        try
        {
            (await engine.HostLifetime.StopAsync(TimeSpan.Zero, Token)).Should().BeOfType<EphemeralHostShutdownResult.Incomplete>();
            stopping.IsCompleted.Should().BeFalse();
        }
        finally { release.TrySetResult(); }
        var error = await Record.ExceptionAsync(async () => await observing.WaitAsync(Guard, Token));
        if (operation == "wait") error.Should().BeOfType<WorkflowOutputUnavailableException>();
        else error.Should().BeNull();
        (await stopping.WaitAsync(Guard, Token)).Should().BeOfType<EphemeralHostShutdownResult.Completed>();
    }

    [Theory]
    [InlineData(false, "root", "none")]
    [InlineData(true, "root", "none")]
    [InlineData(false, "branch", "throttle")]
    [InlineData(true, "branch", "throttle")]
    [InlineData(false, "item", "pool")]
    [InlineData(true, "item", "pool")]
    [Trait("AC", "AC-547")]
    [Trait("AC", "AC-549")]
    [Trait("AC", "AC-554")]
    public async Task ALogicalTimeout_DoesNotLetShutdownForgetPhysicalWorkOrCapacity(bool bounded, string shape, string capacity)
    {
        using var host = new Host(bounded, capacity: capacity);
        var start = host.Start(host.Register(host.Definition(shape, capacity, timeout: true)));
        await host.Body.Entered.Task.WaitAsync(Guard, Token);
        host.Clock.Advance(TimeSpan.FromSeconds(30));
        var instance = (await start.WaitAsync(Guard, Token)).GetHandleOrThrow();
        (await instance.GetSnapshotAsync(Token)).Status.Should().Be(WorkflowInstanceStatus.Failed);
        (await host.Lifetime.StopAsync(TimeSpan.Zero, Token)).Should().BeOfType<EphemeralHostShutdownResult.Incomplete>();
        host.Body.Release.TrySetResult();
        (await host.Lifetime.StopAsync(Timeout.InfiniteTimeSpan, Token).AsTask().WaitAsync(Guard, Token)).Should().BeOfType<EphemeralHostShutdownResult.Completed>();
        host.Initialized!.ToSnapshot().Status.Should().Be(WorkflowInstanceStatus.Failed);
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    [Trait("AC", "AC-550")]
    public async Task FirstCancellationDispatch_IsJoinedAcrossRepeatedRequests_EvenAfterBodyReturn(bool priorRequest, bool throwing)
    {
        using var body = new CallbackBody(throwing);
        using var host = new Host(configure: services => services.AddSingleton(body));
        var definition = Workflow.Ephemeral<State>(DefinitionId.New(), DefinitionVersion.Initial).Init<string>(value => new State(value))
            .Then<CallbackBody>().End().Build();
        var pending = host.Start(host.Register(definition));
        await body.Entered.Task.WaitAsync(Guard, Token);
        Task? request = priorRequest ? host.Engine.RequestCancellationAsync(host.Initialized!.InstanceId, Token) : null;
        var first = await host.Lifetime.StopAsync(TimeSpan.Zero, Token);
        first.Should().BeOfType<EphemeralHostShutdownResult.Incomplete>().Which.PendingWorkCount.Should().BeGreaterThan(0);
        await body.CallbackEntered.Task.WaitAsync(Guard, Token);
        body.ReleaseBody.TrySetResult();
        await body.BodyReturned.Task.WaitAsync(Guard, Token);
        var second = await host.Lifetime.StopAsync(TimeSpan.Zero, Token);
        second.Should().BeOfType<EphemeralHostShutdownResult.Incomplete>().Which.PendingWorkCount.Should().BeGreaterThan(0,
            "a repeated CancelAsync completing quickly does not prove the original handler has returned");
        using var independent = new Host();
        (await independent.Lifetime.StopAsync(Timeout.InfiniteTimeSpan, Token).AsTask().WaitAsync(Guard, Token))
            .Should().BeOfType<EphemeralHostShutdownResult.Completed>("a blocked handler cannot block a different root");
        var faults = 0;
        using var listener = ShutdownListener(name => { if (name == "shutdown-callback-failed") Interlocked.Increment(ref faults); });
        body.ReleaseCallback.Set();
        var startError = await Record.ExceptionAsync(async () => await pending.WaitAsync(Guard, Token));
        startError.Should().BeOfType<EphemeralHostStoppingException>();
        if (request is not null) await request.WaitAsync(Guard, Token);
        (await host.Lifetime.StopAsync(Timeout.InfiniteTimeSpan, Token).AsTask().WaitAsync(Guard, Token))
            .Should().BeOfType<EphemeralHostShutdownResult.Completed>("a returned throwing handler is settled, rather than permanently faulting accounting");
        body.Callbacks.Should().Be(1);
        if (throwing) Volatile.Read(ref faults).Should().BeGreaterThan(0);
    }

    [Fact]
    [Trait("AC", "AC-553")]
    public async Task CancellationOfOneObserver_DoesNotCancelOtherObserversOrTheSharedJoin()
    {
        using var host = new Host();
        var pending = host.Start(host.Register(host.Definition()));
        await host.Body.Entered.Task.WaitAsync(Guard, Token);
        using var cancelled = new CancellationTokenSource();
        var first = host.Lifetime.StopAsync(Timeout.InfiniteTimeSpan, cancelled.Token).AsTask();
        var others = Enumerable.Range(0, 8).Select(_ => host.Lifetime.StopAsync(Timeout.InfiniteTimeSpan, Token).AsTask()).ToArray();
        cancelled.Cancel();
        var result = (await first.WaitAsync(Guard, Token)).Should().BeOfType<EphemeralHostShutdownResult.Incomplete>().Subject;
        result.Reason.Should().Be(EphemeralHostShutdownIncompleteReason.ObservationCancelled);
        others.Should().OnlyContain(task => !task.IsCompleted);
        host.Body.Release.TrySetResult();
        await Record.ExceptionAsync(async () => await pending.WaitAsync(Guard, Token));
        var completed = await Task.WhenAll(others).WaitAsync(Guard, Token);
        completed.Should().OnlyContain(value => value is EphemeralHostShutdownResult.Completed);
        completed.Should().OnlyContain(value => ReferenceEquals(value, completed[0]));
    }

    [Fact]
    [Trait("AC", "AC-547")]
    public async Task CommitNotificationAndLaneTail_AreOwnedUntilTheirCallbacksReturn()
    {
        var committed = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var tail = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        using var releaseCommit = new ManualResetEventSlim();
        using var releaseTail = new ManualResetEventSlim();
        var lane = new InstanceExecutionLane(onLaneEvicted: _ =>
        {
            tail.TrySetResult();
            releaseTail.Wait(Guard).Should().BeTrue();
        });
        using var engine = new EphemeralWorkflowEngine(TimeProvider.System, new InMemoryInstanceRegistry(), lane);
        var registry = new EphemeralWorkflowDefinitionRegistry(engine);
        var definition = Workflow.Ephemeral<State>(DefinitionId.New(), DefinitionVersion.Initial).Init<string>(input => new State(input)).End().Build();
        var handle = registry.Register(definition).GetHandleOrThrow();
        var first = 0;
        engine.InstanceCommitted += _ =>
        {
            if (Interlocked.Increment(ref first) != 1) return;
            committed.TrySetResult();
            releaseCommit.Wait(Guard).Should().BeTrue();
        };
        var start = handle.StartOrGetAsync("commit", StartIdempotencyKey.Create("commit"), Token).AsTask();
        try
        {
            await committed.Task.WaitAsync(Guard, Token);
            (await engine.HostLifetime.StopAsync(TimeSpan.Zero, Token)).Should().BeOfType<EphemeralHostShutdownResult.Incomplete>();
            releaseCommit.Set();
            await tail.Task.WaitAsync(Guard, Token);
            (await engine.HostLifetime.StopAsync(TimeSpan.Zero, Token)).Should().BeOfType<EphemeralHostShutdownResult.Incomplete>();
        }
        finally { releaseCommit.Set(); releaseTail.Set(); }
        (await Record.ExceptionAsync(async () => await start.WaitAsync(Guard, Token))).Should().BeOfType<EphemeralHostStoppingException>();
        (await engine.HostLifetime.StopAsync(Timeout.InfiniteTimeSpan, Token).AsTask().WaitAsync(Guard, Token))
            .Should().BeOfType<EphemeralHostShutdownResult.Completed>();
        lane.ActiveLaneCount.Should().Be(0);
    }

    [Theory]
    [InlineData(false, "retry")]
    [InlineData(true, "retry")]
    [InlineData(false, "resource")]
    [InlineData(true, "resource")]
    [InlineData(false, "timer")]
    [InlineData(true, "timer")]
    [Trait("AC", "AC-547")]
    public async Task CompletionHandoffs_AreJoinedAfterTheirLogicalOperationEnded(bool bounded, string kind)
    {
        var completion = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var selected = kind switch { "retry" => EphemeralWorkKind.RetryContinuation, "resource" => EphemeralWorkKind.ResourceContinuation, _ => EphemeralWorkKind.TimerDispatch };
        var clock = new Clock(new DateTimeOffset(2026, 10, 19, 0, 0, 0, TimeSpan.Zero));
        var body = new HostShutdownTests.Body(cooperative: true);
        using var services = new ServiceCollection().AddSingleton(body).BuildServiceProvider();
        using var engine = new EphemeralWorkflowEngine(clock.TimeProvider, new EphemeralWorkflowEngineOptions
        {
            TerminalRetention = bounded ? new EphemeralTerminalRetentionSettings(2, TimeSpan.FromMinutes(1), TimeSpan.FromSeconds(1)) : null,
            StepThrottles = new Dictionary<Type, int> { [typeof(HostShutdownTests.Body)] = 1 },
            WorkCompletionPending = (_, actual) =>
            {
                if (actual != selected) return ValueTask.CompletedTask;
                completion.TrySetResult();
                return new ValueTask(release.Task);
            }
        }, services);
        var registry = new EphemeralWorkflowDefinitionRegistry(engine);
        var router = new EphemeralWorkflowEventRouter(engine, registry);
        var contract = WorkflowEventContract.Create(EventName.Create("owned-grant"), EventContractVersion.Initial);
        var definition = kind switch
        {
            "retry" => Workflow.Ephemeral<State>(DefinitionId.New(), DefinitionVersion.Initial).Init<string>(value => new State(value))
                .Then(_ => throw new InvalidOperationException("retry pending")).WithRetry(2, TimeSpan.FromHours(1)).End().Build(),
            "resource" => Workflow.Ephemeral<State>(DefinitionId.New(), DefinitionVersion.Initial).Init<string>(value => new State(value))
                .Wait(contract, state => CorrelationId.Create(state.Value.Value)).Then<HostShutdownTests.Body>().End().Build(),
            _ => Workflow.Ephemeral<State>(DefinitionId.New(), DefinitionVersion.Initial).Init<string>(value => new State(value))
                .Delay(TimeSpan.FromSeconds(10)).End().Build()
        };
        var handle = registry.Register(definition).GetHandleOrThrow();
        Task? holder = null;
        Task<WorkflowStartResult<WorkflowInstanceHandle>>? waiting = null;
        try
        {
            if (kind == "resource")
            {
                var acceptedHolder = (await handle.StartOrGetAsync("holder", StartIdempotencyKey.Create("holder"), Token)).GetHandleOrThrow();
                holder = router.RouteToInstanceAsync(acceptedHolder.InstanceId,
                    EphemeralTestEvent.Create(EventId.Create("holder-event"), contract.EventName, CorrelationId.Create("holder"), clock.Now), Token).AsTask();
                await body.Entered.Task.WaitAsync(Guard, Token);
                waiting = handle.StartOrGetAsync("victim", StartIdempotencyKey.Create("victim"), Token).AsTask();
                var acceptedVictim = (await waiting.WaitAsync(Guard, Token)).GetHandleOrThrow();
                await router.RouteToInstanceAsync(acceptedVictim.InstanceId,
                    EphemeralTestEvent.Create(EventId.Create("victim-event"), contract.EventName, CorrelationId.Create("victim"), clock.Now), Token);
            }
            else await handle.StartOrGetAsync("victim", StartIdempotencyKey.Create("victim"), Token);
            if (kind == "timer") clock.Advance(TimeSpan.FromSeconds(10));
            var stopping = engine.HostLifetime.StopAsync(TimeSpan.Zero, Token);
            (await stopping).Should().BeOfType<EphemeralHostShutdownResult.Incomplete>();
            await completion.Task.WaitAsync(Guard, Token);
            (await engine.HostLifetime.StopAsync(TimeSpan.Zero, Token)).Should().BeOfType<EphemeralHostShutdownResult.Incomplete>();
        }
        finally { release.TrySetResult(); body.Release.TrySetResult(); }
        if (holder is not null) await Record.ExceptionAsync(async () => await holder.WaitAsync(Guard, Token));
        if (waiting is not null) await Record.ExceptionAsync(async () => await waiting.WaitAsync(Guard, Token));
        (await engine.HostLifetime.StopAsync(Timeout.InfiniteTimeSpan, Token).AsTask().WaitAsync(Guard, Token))
            .Should().BeOfType<EphemeralHostShutdownResult.Completed>();
    }

    internal static MeterListener ShutdownListener(Action<string> capture)
    {
        var listener = new MeterListener();
        listener.InstrumentPublished = (instrument, current) => { if (instrument.Name == "orca.lifecycle.events") current.EnableMeasurementEvents(instrument); };
        listener.SetMeasurementEventCallback<long>((_, _, tags, _) =>
        {
            var values = tags.ToArray();
            if (!values.Any(tag => tag.Key == "orca.lifecycle.origin" && Equals(tag.Value, "host-shutdown"))) return;
            values.Select(tag => tag.Key).Should().BeEquivalentTo("event.name", "durable", "orca.lifecycle.origin");
            capture((string)values.Single(tag => tag.Key == "event.name").Value!);
        });
        listener.Start(); return listener;
    }

    private sealed class QueuedCallbackBody : IStep<State>, IDisposable
    {
        internal TaskCompletionSource Entered { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        internal TaskCompletionSource BlockingCallbackEntered { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        internal TaskCompletionSource ReleaseBody { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        internal ManualResetEventSlim ReleaseCallback { get; } = new();
        internal int EarlierCallbackCount;
        public async ValueTask<StepResult> ExecuteAsync(StepContext<State> context, CancellationToken cancellationToken)
        {
            cancellationToken.Register(() => Interlocked.Increment(ref EarlierCallbackCount));
            cancellationToken.Register(() =>
            {
                BlockingCallbackEntered.TrySetResult();
                ReleaseCallback.Wait(Guard).Should().BeTrue();
            });
            Entered.TrySetResult();
            await ReleaseBody.Task;
            return new StepResult.Completed();
        }
        public void Dispose() { ReleaseBody.TrySetResult(); ReleaseCallback.Set(); }
    }

    private sealed class CallbackBody(bool throwing) : IStep<State>, IDisposable
    {
        internal TaskCompletionSource Entered { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        internal TaskCompletionSource CallbackEntered { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        internal TaskCompletionSource ReleaseBody { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        internal TaskCompletionSource BodyReturned { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        internal ManualResetEventSlim ReleaseCallback { get; } = new();
        internal int Callbacks;
        public async ValueTask<StepResult> ExecuteAsync(StepContext<State> context, CancellationToken cancellationToken)
        {
            var registration = cancellationToken.Register(() =>
            {
                Interlocked.Increment(ref Callbacks);
                CallbackEntered.TrySetResult();
                ReleaseCallback.Wait(Guard).Should().BeTrue();
                if (throwing) throw new InvalidOperationException("throwing cancellation handler");
            });
            Entered.TrySetResult();
            await ReleaseBody.Task;
            registration.Unregister();
            BodyReturned.TrySetResult();
            return new StepResult.Completed();
        }
        public void Dispose() { ReleaseBody.TrySetResult(); ReleaseCallback.Set(); }
    }
}
