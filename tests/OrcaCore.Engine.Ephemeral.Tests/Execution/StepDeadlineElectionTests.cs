using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Time.Testing;
using OrcaCore.Hosting;
using OrcaCore.Engine.Ephemeral.Execution;
using Xunit;

namespace OrcaCore.Engine.Ephemeral.Tests.Execution;

public sealed class StepDeadlineElectionTests
{
    private static readonly TimeSpan Guard = TimeSpan.FromSeconds(10);

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ElectedTimeoutSurvivesWrapperFailureBeforeDecisionPublication(bool cancelled)
    {
        var clock = new FakeTimeProvider();
        var elected = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        using var release = new ManualResetEventSlim();
        var decision = new StepAttemptDecision(clock, clock.GetUtcNow(), () =>
        {
            elected.TrySetResult();
            Assert.True(release.Wait(Guard), "The test must release timeout publication.");
        });
        var timer = Task.Run(() => decision.OnTimer(), TestContext.Current.CancellationToken);
        Task<bool>? pending = null;
        try
        {
            await elected.Task.WaitAsync(Guard, TestContext.Current.CancellationToken);
            var attempt = cancelled ? Task.FromCanceled(new CancellationToken(true))
                : Task.FromException(new InvalidOperationException("Wrapper failure after timeout election."));
            pending = decision.WaitForResultAsync(attempt);
            Assert.False(pending.IsCompleted);
        }
        finally { release.Set(); }
        await timer.WaitAsync(Guard, TestContext.Current.CancellationToken);
        Assert.False(await pending!.WaitAsync(Guard, TestContext.Current.CancellationToken));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task WrapperFailureBeforeObservationDoesNotNeedATimerCallback(bool cancelled)
    {
        var clock = new FakeTimeProvider();
        var decision = new StepAttemptDecision(clock, clock.GetUtcNow().AddMinutes(1), () => Assert.Fail("No timeout is due."));
        var failure = new InvalidOperationException("The ownership wrapper failed before invoking user code.");
        var attempt = cancelled ? Task.FromCanceled(new CancellationToken(true)) : Task.FromException(failure);
        var pending = decision.WaitForResultAsync(attempt).WaitAsync(TimeSpan.FromSeconds(1), TestContext.Current.CancellationToken);
        if (cancelled) await Assert.ThrowsAnyAsync<OperationCanceledException>(() => pending);
        else Assert.Same(failure, await Assert.ThrowsAsync<InvalidOperationException>(() => pending));
    }

    [Theory]
    [InlineData("root", 10_000_000, false)]
    [InlineData("branch", 10_000_000, false)]
    [InlineData("item", 10_000_000, false)]
    [InlineData("root", 20_000_000, true)]
    [InlineData("branch", 20_000_000, true)]
    [InlineData("item", 20_000_000, true)]
    [InlineData("root", 9_999_999, false)]
    public async Task StepResultIsElectedAgainstItsDeadline(string shape, long elapsedTicks, bool deferTimer)
    {
        var clock = new FakeTimeProvider();
        var services = new ServiceCollection();
        var deferred = new DeferredTimerClock(clock);
        services.AddSingleton<TimeProvider>(deferTimer ? deferred : clock);
        services.AddOrcaCoreEphemeralEngine(new EphemeralEngineHostOptions
        {
            StructuredExecution = new StructuredExecutionHostOptions { MaxConcurrentExecutionPathsPerInstance = 1, StepThrottles = [] },
            TransientPools = []
        });
        using var provider = services.BuildServiceProvider();
        ValueTask Body(StepContext<State> context)
        {
            clock.Advance(TimeSpan.FromTicks(elapsedTicks));
            context.ReplaceState(new State(99));
            return ValueTask.CompletedTask;
        }
        var root = Workflow.Ephemeral<State>(DefinitionId.New(), DefinitionVersion.Initial).Init<int>(v => new State(v));
        var definition = shape switch
        {
            "branch" => root.Parallel<int>(branches => branches.Branch<State>(AuthoredBranchId.Create("only"),
                    parent => parent.Value, body => body.Then(Body).WithStepTimeout(TimeSpan.FromSeconds(1)).Return(s => s.Value.Value)))
                .WhenAll((parent, _) => parent.Value).End().Build(),
            "item" => root.ForEach<int, State, int>(_ => new[] { 1 }, ForEachOptions.Create(1),
                    item => new State(item.Item), body => body.Then(Body).WithStepTimeout(TimeSpan.FromSeconds(1)).Return(s => s.Value.Value))
                .WhenAll((parent, _) => parent.Value).End().Build(),
            _ => root.Then(Body).WithStepTimeout(TimeSpan.FromSeconds(1)).End().Build()
        };
        var registered = provider.GetRequiredService<IWorkflowDefinitionRegistry>().Register(definition).GetHandleOrThrow();
        var handle = (await registered.StartOrGetAsync(17, StartIdempotencyKey.Create("deadline"), TestContext.Current.CancellationToken)).GetHandleOrThrow();
        Assert.Equal(elapsedTicks < 10_000_000 ? WorkflowInstanceStatus.Completed : WorkflowInstanceStatus.Failed,
            (await handle.GetSnapshotAsync(TestContext.Current.CancellationToken)).Status);
        if (elapsedTicks >= 10_000_000)
            Assert.Equal(17, (await handle.GetStateAsync<State>(TestContext.Current.CancellationToken)).Value);
        if (deferTimer) Assert.Equal(0, deferred.Callbacks);
    }

    [Fact]
    public async Task ResultElectedBeforeDeadlineSurvivesDelayedPhysicalSettlement()
    {
        var clock = new FakeTimeProvider();
        var completing = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        using var engine = new EphemeralWorkflowEngine(clock, new EphemeralWorkflowEngineOptions
        {
            WorkCompletionPending = (_, kind) =>
            {
                if (kind != EphemeralWorkKind.PhysicalAttempt) return ValueTask.CompletedTask;
                completing.TrySetResult();
                return new ValueTask(release.Task);
            }
        });
        var registry = new EphemeralWorkflowDefinitionRegistry(engine);
        var definition = Workflow.Ephemeral<State>(DefinitionId.New(), DefinitionVersion.Initial)
            .Init<int>(v => new State(v)).Then(context =>
            {
                context.ReplaceState(new State(99));
                return ValueTask.CompletedTask;
            }).WithStepTimeout(TimeSpan.FromSeconds(1)).End().Build();
        var pending = registry.Register(definition).GetHandleOrThrow()
            .StartOrGetAsync(17, StartIdempotencyKey.Create("early-result"), TestContext.Current.CancellationToken).AsTask();
        try
        {
            await completing.Task.WaitAsync(Guard, TestContext.Current.CancellationToken);
            clock.Advance(TimeSpan.FromSeconds(2));
        }
        finally { release.TrySetResult(); }
        var handle = (await pending.WaitAsync(Guard, TestContext.Current.CancellationToken)).GetHandleOrThrow();
        Assert.Equal(WorkflowInstanceStatus.Completed, (await handle.GetSnapshotAsync(TestContext.Current.CancellationToken)).Status);
        Assert.Equal(99, (await handle.GetStateAsync<State>(TestContext.Current.CancellationToken)).Value);
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(true, false)]
    [InlineData(false, true)]
    [InlineData(true, true)]
    public async Task TimeoutKeepsPhysicalOwnershipUntilLateReturnOrFailure(bool throws, bool earlyTimer)
    {
        var clock = new FakeTimeProvider();
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        using var engine = new EphemeralWorkflowEngine(earlyTimer ? new EarlyTimerClock(clock) : clock);
        var registry = new EphemeralWorkflowDefinitionRegistry(engine);
        var definition = Workflow.Ephemeral<State>(DefinitionId.New(), DefinitionVersion.Initial)
            .Init<int>(v => new State(v)).Then(async context =>
            {
                entered.TrySetResult();
                await release.Task;
                context.ReplaceState(new State(99));
                if (throws) throw new InvalidOperationException("late failure");
            }).WithStepTimeout(TimeSpan.FromSeconds(1)).End().Build();
        var pending = registry.Register(definition).GetHandleOrThrow()
            .StartOrGetAsync(17, StartIdempotencyKey.Create("late-result"), TestContext.Current.CancellationToken).AsTask();
        WorkflowInstanceHandle? handle = null;
        try
        {
            await entered.Task.WaitAsync(Guard, TestContext.Current.CancellationToken);
            clock.Advance(TimeSpan.FromMilliseconds(500));
            Assert.False(pending.IsCompleted);
            clock.Advance(TimeSpan.FromMilliseconds(500));
            handle = (await pending.WaitAsync(Guard, TestContext.Current.CancellationToken)).GetHandleOrThrow();
            Assert.Equal(WorkflowInstanceStatus.Failed, (await handle.GetSnapshotAsync(TestContext.Current.CancellationToken)).Status);
            Assert.False(handle.WaitForSettlementAsync(TestContext.Current.CancellationToken).IsCompleted);
        }
        finally { release.TrySetResult(); }
        await handle!.WaitForSettlementAsync(TestContext.Current.CancellationToken).AsTask().WaitAsync(Guard, TestContext.Current.CancellationToken);
        Assert.Equal(17, (await handle.GetStateAsync<State>(TestContext.Current.CancellationToken)).Value);
        Assert.Equal(WorkflowInstanceStatus.Failed, (await handle.GetSnapshotAsync(TestContext.Current.CancellationToken)).Status);
    }

    private sealed class DeferredTimerClock(FakeTimeProvider clock) : TimeProvider
    {
        internal int Callbacks { get; private set; }
        public override DateTimeOffset GetUtcNow() => clock.GetUtcNow();
        public override ITimer CreateTimer(TimerCallback callback, object? state, TimeSpan dueTime, TimeSpan period) =>
            new DeferredTimer(clock.CreateTimer(value => { Callbacks++; callback(value); }, state,
                Timeout.InfiniteTimeSpan, Timeout.InfiniteTimeSpan));

        private sealed class DeferredTimer(ITimer inner) : ITimer
        {
            public bool Change(TimeSpan dueTime, TimeSpan period) =>
                inner.Change(Timeout.InfiniteTimeSpan, Timeout.InfiniteTimeSpan);
            public void Dispose() => inner.Dispose();
            public ValueTask DisposeAsync() => inner.DisposeAsync();
        }
    }

    private sealed class EarlyTimerClock(FakeTimeProvider clock) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => clock.GetUtcNow();
        public override ITimer CreateTimer(TimerCallback callback, object? state, TimeSpan dueTime, TimeSpan period) =>
            new FirstEarlyTimer(clock.CreateTimer(callback, state,
                dueTime > TimeSpan.Zero ? dueTime / 2 : dueTime, period));

        private sealed class FirstEarlyTimer(ITimer inner) : ITimer
        {
            private bool first = true;
            public bool Change(TimeSpan dueTime, TimeSpan period)
            {
                var adjusted = first && dueTime > TimeSpan.Zero ? dueTime / 2 : dueTime;
                first = false;
                return inner.Change(adjusted, period);
            }
            public void Dispose() => inner.Dispose();
            public ValueTask DisposeAsync() => inner.DisposeAsync();
        }
    }

    public sealed record State(int Value);
}
