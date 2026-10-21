using AwesomeAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using OrcaCore.TestSupport;
using OrcaCore.Hosting;
using Xunit;

namespace OrcaCore.Engine.Ephemeral.Tests.Shutdown;

[Trait("Slice", "Issue2Section3")]
[Trait("Disposition", "Implemented")]
public sealed class HostShutdownTests
{
    private static readonly TimeSpan Guard = TimeSpan.FromSeconds(15);
    private static CancellationToken TestToken => TestContext.Current.CancellationToken;

    [Fact]
    [Trait("AC", "AC-544")]
    [Trait("AC", "AC-553")]
    public async Task EmptyHost_CompletesAndCachesTheCompletedObservation()
    {
        var collection = new ServiceCollection();
        collection.AddOrcaCoreEphemeralEngine(new EphemeralEngineHostOptions { StructuredExecution = new StructuredExecutionHostOptions { MaxConcurrentExecutionPathsPerInstance = 2, StepThrottles = [] }, TransientPools = [] });
        using var services = collection.BuildServiceProvider();
        var lifetime = services.GetRequiredService<IEphemeralWorkflowHostLifetime>();
        var result = await lifetime.StopAsync(Timeout.InfiniteTimeSpan, TestContext.Current.CancellationToken);
        result.Should().BeOfType<EphemeralHostShutdownResult.Completed>();
        (await lifetime.StopAsync(TimeSpan.Zero, TestContext.Current.CancellationToken)).Should().BeSameAs(result);
    }

    [Fact]
    [Trait("AC", "AC-543")]
    public async Task Registration_IsReservedIdempotentAndScopedToTheActualRoot()
    {
        var collection = new ServiceCollection();
        var options = Options();
        collection.AddOrcaCoreEphemeralEngine(options);
        var count = collection.Count;
        collection.AddOrcaCoreEphemeralEngine(options);
        collection.Count.Should().Be(count);
        using var first = collection.BuildServiceProvider();
        using var second = collection.BuildServiceProvider();
        using var scope = first.CreateScope();
        var lifetime = first.GetRequiredService<IEphemeralWorkflowHostLifetime>();
        scope.ServiceProvider.GetRequiredService<IEphemeralWorkflowHostLifetime>().Should().BeSameAs(lifetime);
        second.GetRequiredService<IEphemeralWorkflowHostLifetime>().Should().NotBeSameAs(lifetime);
        (await lifetime.StopAsync(Timeout.InfiniteTimeSpan, TestToken)).Should().BeOfType<EphemeralHostShutdownResult.Completed>();
        (await second.GetRequiredService<IEphemeralWorkflowHostLifetime>().StopAsync(Timeout.InfiniteTimeSpan, TestToken))
            .Should().BeOfType<EphemeralHostShutdownResult.Completed>();

        var conflict = new ServiceCollection();
        conflict.AddSingleton<IEphemeralWorkflowHostLifetime>(lifetime);
        var before = conflict.ToArray();
        var error = Record.Exception(() => conflict.AddOrcaCoreEphemeralEngine(options));
        error.Should().BeOfType<ArgumentException>().Which.ParamName.Should().Be("services");
        conflict.Should().Equal(before);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(-10001)]
    [InlineData(42949672940001)]
    [Trait("AC", "AC-544")]
    public async Task InvalidExactTickBudget_HasPrecedenceOverCancellationAndNoEffect(long ticks)
    {
        using var host = new Host();
        using var cancelled = new CancellationTokenSource();
        cancelled.Cancel();
        var error = await Record.ExceptionAsync(async () => await host.Lifetime.StopAsync(TimeSpan.FromTicks(ticks), cancelled.Token));
        error.Should().BeOfType<ArgumentOutOfRangeException>().Which.ParamName.Should().Be("timeout");
        host.Register(host.Definition()).Should().NotBeNull();
        (await host.Lifetime.StopAsync(Timeout.InfiniteTimeSpan, TestToken).AsTask().WaitAsync(Guard, TestToken)).Should().BeOfType<EphemeralHostShutdownResult.Completed>();
    }

    [Fact]
    [Trait("AC", "AC-544")]
    public async Task PreCancelledPublicObservation_DoesNotCloseAdmission_AndStillWinsAfterCompletion()
    {
        using var host = new Host();
        using var cancelled = new CancellationTokenSource();
        cancelled.Cancel();
        var error = await Record.ExceptionAsync(async () => await host.Lifetime.StopAsync(TimeSpan.Zero, cancelled.Token));
        error.Should().BeAssignableTo<OperationCanceledException>().Which.CancellationToken.Should().Be(cancelled.Token);
        host.Register(host.Definition());
        await host.Lifetime.StopAsync(Timeout.InfiniteTimeSpan, TestToken).AsTask().WaitAsync(Guard, TestToken);
        var again = await Record.ExceptionAsync(async () => await host.Lifetime.StopAsync(TimeSpan.Zero, cancelled.Token));
        again.Should().BeAssignableTo<OperationCanceledException>().Which.CancellationToken.Should().Be(cancelled.Token);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    [Trait("AC", "AC-545")]
    [Trait("AC", "AC-546")]
    public async Task InitializedPendingStart_IsAcceptedDuringClosed_WithoutCancellingItsBody(bool bounded)
    {
        using var host = new Host(bounded);
        var definition = host.Definition();
        var handle = host.Register(definition);
        var pending = host.Start(handle);
        await host.Body.Entered.Task.WaitAsync(Guard, TestToken);
        host.Application.StopApplication();
        host.Body.CancellationObserved.Task.IsCompleted.Should().BeFalse();
        var replay = await Record.ExceptionAsync(async () => await host.Start(handle));
        replay.Should().BeOfType<EphemeralHostStoppingException>();
        Record.Exception(() => host.Registry.Register(definition)).Should().BeOfType<EphemeralHostStoppingException>();
        host.Body.Release.TrySetResult();
        var accepted = await pending.WaitAsync(Guard, TestToken);
        accepted.Should().BeOfType<WorkflowStartResult<WorkflowInstanceHandle>.Accepted>();
        (await accepted.GetHandleOrThrow().GetSnapshotAsync(TestToken)).Status.Should().Be(WorkflowInstanceStatus.Completed);
        host.Registry.GetRequiredHandle(definition.Reference).Should().NotBeNull();
        (await host.Lifetime.StopAsync(Timeout.InfiniteTimeSpan, TestToken).AsTask().WaitAsync(Guard, TestToken)).Should().BeOfType<EphemeralHostShutdownResult.Completed>();
        host.Registry.GetRequiredHandle(definition.Reference).Should().NotBeNull("definition identity lookup is passive after Completed");
    }

    [Fact]
    [Trait("AC", "AC-546")]
    public async Task QueuedPreInitStart_IsRefusedAtClosed_WhileInitializedStartDrains()
    {
        using var host = new Host();
        var inits = 0;
        var definition = Workflow.Ephemeral<State>(DefinitionId.New(), DefinitionVersion.Initial)
            .Init<string>(input => { Interlocked.Increment(ref inits); return new State(input); })
            .Then<Body>().End().Build();
        var handle = host.Register(definition);
        var first = host.Start(handle, "first");
        await host.Body.Entered.Task.WaitAsync(Guard, TestToken);
        var queued = host.Start(handle, "queued");
        host.Application.StopApplication();
        host.Body.Release.TrySetResult();
        (await first.WaitAsync(Guard, TestToken)).Should().BeOfType<WorkflowStartResult<WorkflowInstanceHandle>.Accepted>();
        var refused = await Record.ExceptionAsync(async () => await queued.WaitAsync(Guard, TestToken));
        refused.Should().BeOfType<EphemeralHostStoppingException>();
        inits.Should().Be(1);
        await host.Lifetime.StopAsync(Timeout.InfiniteTimeSpan, TestToken).AsTask().WaitAsync(Guard, TestToken);
    }

    public static TheoryData<bool, bool, string, string, bool> Attempts
    {
        get
        {
            var data = new TheoryData<bool, bool, string, string, bool>();
            foreach (var bounded in new[] { false, true })
            foreach (var cooperative in new[] { false, true })
            foreach (var shape in new[] { "root", "branch", "item" })
            foreach (var capacity in new[] { "none", "throttle", "pool" })
            foreach (var stepTimeout in new[] { false, true })
                data.Add(bounded, cooperative, shape, capacity, stepTimeout);
            return data;
        }
    }

    [Theory]
    [MemberData(nameof(Attempts))]
    [Trait("AC", "AC-546")]
    [Trait("AC", "AC-547")]
    [Trait("AC", "AC-549")]
    [Trait("AC", "AC-554")]
    public async Task Stopping_PreHandleAttempts_AreAbortedAndJoinedPhysically(bool bounded, bool cooperative, string shape, string capacity, bool stepTimeout)
    {
        using var host = new Host(bounded, cooperative, capacity);
        var handle = host.Register(host.Definition(shape, capacity, timeout: stepTimeout));
        var pending = host.Start(handle);
        await host.Body.Entered.Task.WaitAsync(Guard, TestToken);
        var runtime = host.Initialized!;
        var observation = await host.Lifetime.StopAsync(TimeSpan.Zero, TestToken);
        observation.Should().BeOfType<EphemeralHostShutdownResult.Incomplete>().Which.PendingWorkCount.Should().BeGreaterThan(0);
        await host.Body.CancellationObserved.Task.WaitAsync(Guard, TestToken);
        if (!cooperative) pending.IsCompleted.Should().BeFalse("a noncooperative body still owns the start's lane and physical attempt");
        host.Body.Release.TrySetResult();
        var error = await Record.ExceptionAsync(async () => await pending.WaitAsync(Guard, TestToken));
        error.Should().BeOfType<EphemeralHostStoppingException>();
        (await host.Lifetime.StopAsync(Timeout.InfiniteTimeSpan, TestToken).AsTask().WaitAsync(Guard, TestToken)).Should().BeOfType<EphemeralHostShutdownResult.Completed>();
        host.Engine.TryGetFacadeInstance(runtime.InstanceId, out _).Should().BeFalse("an aborted start is never retained or published");
    }

    [Fact]
    [Trait("AC", "AC-544")]
    [Trait("AC", "AC-553")]
    public async Task AdmittedObservationCancellation_IsDetached_AndDeadlineUsesMonotonicClock()
    {
        using var host = new Host();
        var pending = host.Start(host.Register(host.Definition()));
        await host.Body.Entered.Task.WaitAsync(Guard, TestToken);
        using var cancelled = new CancellationTokenSource();
        var observing = host.Lifetime.StopAsync(TimeSpan.FromSeconds(10), cancelled.Token).AsTask();
        cancelled.Cancel();
        var cancellation = (await observing.WaitAsync(Guard, TestToken)).Should().BeOfType<EphemeralHostShutdownResult.Incomplete>().Subject;
        cancellation.Reason.Should().Be(EphemeralHostShutdownIncompleteReason.ObservationCancelled);
        cancellation.PendingWorkCount.Should().BeGreaterThan(0);
        var deadline = host.Lifetime.StopAsync(TimeSpan.FromSeconds(10), TestToken).AsTask();
        host.Clock.Advance(TimeSpan.FromSeconds(10));
        var timeout = (await deadline.WaitAsync(Guard, TestToken)).Should().BeOfType<EphemeralHostShutdownResult.Incomplete>().Subject;
        timeout.Reason.Should().Be(EphemeralHostShutdownIncompleteReason.DeadlineElapsed);
        var originalCount = timeout.PendingWorkCount;
        host.Body.Release.TrySetResult();
        await Record.ExceptionAsync(async () => await pending.WaitAsync(Guard, TestToken));
        await host.Lifetime.StopAsync(Timeout.InfiniteTimeSpan, TestToken).AsTask().WaitAsync(Guard, TestToken);
        timeout.PendingWorkCount.Should().Be(originalCount);
    }

    [Fact]
    [Trait("AC", "AC-544")]
    public async Task HostedPreCancelledToken_StartsShutdownAndReturnsTypedIncomplete()
    {
        using var host = new Host();
        var pending = host.Start(host.Register(host.Definition()));
        await host.Body.Entered.Task.WaitAsync(Guard, TestToken);
        using var cancelled = new CancellationTokenSource(); cancelled.Cancel();
        var hosted = host.Services.GetServices<IHostedService>().Single();
        var error = await Record.ExceptionAsync(() => hosted.StopAsync(cancelled.Token));
        var incomplete = error.Should().BeOfType<EphemeralHostShutdownIncompleteException>().Subject;
        incomplete.Code.Should().Be("WF-EPHEMERAL-SHUTDOWN-INCOMPLETE");
        incomplete.Result.Reason.Should().Be(EphemeralHostShutdownIncompleteReason.ObservationCancelled);
        await host.Body.CancellationObserved.Task.WaitAsync(Guard, TestToken);
        host.Body.Release.TrySetResult();
        await Record.ExceptionAsync(async () => await pending.WaitAsync(Guard, TestToken));
        await hosted.StopAsync(TestToken);
    }

    internal static EphemeralEngineHostOptions Options(bool bounded = false, string capacity = "none") => new()
    {
        StructuredExecution = new StructuredExecutionHostOptions
        {
            MaxConcurrentExecutionPathsPerInstance = 2,
            StepThrottles = capacity == "throttle" ? [StepExecutionThrottle.For<Body>(1)] : []
        },
        TransientPools = capacity == "pool" ? [TransientPoolDefinition.Create(TransientPoolName.Create("shutdown"), 1)] : [],
        TerminalRetention = bounded ? EphemeralTerminalRetentionPolicy.Create(2, TimeSpan.FromMinutes(1), TimeSpan.FromSeconds(1)) : null
    };

    internal sealed class Host : IDisposable
    {
        internal ServiceProvider Services { get; }
        internal Clock Clock { get; } = new(new DateTimeOffset(2026, 10, 19, 12, 0, 0, TimeSpan.Zero));
        internal ApplicationLifetime Application { get; } = new();
        internal Body Body { get; }
        internal EphemeralWorkflowEngine Engine => Services.GetRequiredService<EphemeralWorkflowEngine>();
        internal IWorkflowDefinitionRegistry Registry => Services.GetRequiredService<IWorkflowDefinitionRegistry>();
        internal IEphemeralWorkflowHostLifetime Lifetime => Services.GetRequiredService<IEphemeralWorkflowHostLifetime>();
        internal global::OrcaCore.Engine.Ephemeral.Execution.IWorkflowInstance? Initialized { get; private set; }

        internal Host(bool bounded = false, bool cooperative = false, string capacity = "none", Action<ServiceCollection>? configure = null)
        {
            Body = new Body(cooperative);
            var collection = new ServiceCollection();
            collection.AddSingleton(Body).AddSingleton<TimeProvider>(Clock.TimeProvider).AddSingleton<IHostApplicationLifetime>(Application);
            configure?.Invoke(collection);
            collection.AddOrcaCoreEphemeralEngine(Options(bounded, capacity));
            Services = collection.BuildServiceProvider();
            Engine.InstanceCommitted += id =>
            {
                if (Initialized is null && Engine.TryGetFacadeInstance(id, out var instance)) Initialized = instance;
            };
        }

        internal EphemeralDefinitionHandle<string> Register(EphemeralWorkflowDefinition<string> definition) => Registry.Register(definition).GetHandleOrThrow();
        internal Task<WorkflowStartResult<WorkflowInstanceHandle>> Start(EphemeralDefinitionHandle<string> handle, string key = "shutdown") =>
            handle.StartOrGetAsync("input", StartIdempotencyKey.Create(key), TestToken).AsTask();

        internal EphemeralWorkflowDefinition<string> Definition(string shape = "root", string capacity = "none", bool timeout = false)
        {
            var root = Workflow.Ephemeral<State>(DefinitionId.New(), DefinitionVersion.Initial).Init<string>(input => new State(input));
            if (shape == "branch")
                return root.Parallel<string>(branches => branches.Branch<State>(AuthoredBranchId.Create("child"), _ => new State("branch"), branch =>
                {
                    var step = branch.Then<Body>();
                    if (timeout) step.WithStepTimeout(TimeSpan.FromSeconds(30));
                    if (capacity == "pool") step.WithTransientPool(TransientPoolName.Create("shutdown"));
                    step.Return(state => state.Value.Value);
                })).WhenAll((parent, _) => parent.Value).End().Build();
            if (shape == "item")
                return root.ForEach<string, State, string>(_ => ["item"], ForEachOptions.Create(1, 1), item => new State(item.Item), body =>
                {
                    var step = body.Then<Body>();
                    if (timeout) step.WithStepTimeout(TimeSpan.FromSeconds(30));
                    if (capacity == "pool") step.WithTransientPool(TransientPoolName.Create("shutdown"));
                    step.Return(state => state.Value.Value);
                }).WhenAll((parent, _) => parent.Value).End().Build();
            var rootStep = root.Then<Body>();
            if (timeout) rootStep.WithStepTimeout(TimeSpan.FromSeconds(30));
            if (capacity == "pool") rootStep.WithTransientPool(TransientPoolName.Create("shutdown"));
            return rootStep.End().Build();
        }

        public void Dispose() { Body.Release.TrySetResult(); Services.Dispose(); Application.Dispose(); }
    }

    internal sealed record State(string Value);

    internal sealed class Body(bool cooperative) : IStep<State>
    {
        internal TaskCompletionSource Entered { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        internal TaskCompletionSource Release { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        internal TaskCompletionSource CancellationObserved { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public async ValueTask<StepResult> ExecuteAsync(StepContext<State> context, CancellationToken cancellationToken)
        {
            using var registration = cancellationToken.Register(() => CancellationObserved.TrySetResult());
            Entered.TrySetResult();
            try
            {
                if (cooperative) await Release.Task.WaitAsync(cancellationToken);
                else await Release.Task;
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                CancellationObserved.TrySetResult();
                throw;
            }
            return new StepResult.Completed();
        }
    }

    internal sealed class ApplicationLifetime : IHostApplicationLifetime, IDisposable
    {
        private readonly CancellationTokenSource stopping = new();
        public CancellationToken ApplicationStarted => CancellationToken.None;
        public CancellationToken ApplicationStopping => stopping.Token;
        public CancellationToken ApplicationStopped => CancellationToken.None;
        public void StopApplication() => stopping.Cancel();
        public void Dispose() => stopping.Dispose();
    }
}
