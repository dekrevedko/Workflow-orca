using AwesomeAssertions;
using Microsoft.Extensions.DependencyInjection;
using OrcaCore.Hosting;
using OrcaCore.TestSupport;
using Xunit;

namespace OrcaCore.Engine.Ephemeral.Tests.Timers;

public sealed class EphemeralTimerTests
{
    [Fact]
    public async Task Delay_BeforeDueTime_RemainsWaiting()
    {
        var clock = NewClock();
        using var provider = CreateProvider(clock);
        var handle = Register(provider, DelayedDefinition(TimeSpan.FromMinutes(5)));
        var instance = await StartAsync(handle, "before-due");
        clock.Advance(TimeSpan.FromMinutes(4));

        var snapshot = await instance.GetSnapshotAsync(TestContext.Current.CancellationToken);
        var state = await instance.GetStateAsync<TimerState>(
            TestContext.Current.CancellationToken);

        snapshot.Status.Should().Be(WorkflowInstanceStatus.Waiting);
        state.Entries.Should().BeEmpty();
    }

    [Fact]
    public async Task Delay_AfterDueTime_ContinuesExactlyOnce()
    {
        var clock = NewClock();
        using var provider = CreateProvider(clock);
        var handle = Register(provider, DelayedDefinition(TimeSpan.FromMinutes(5)));
        var instance = await StartAsync(handle, "after-due");
        clock.Advance(TimeSpan.FromMinutes(5));

        var (snapshot, state) = await WaitForStateAsync(
            instance,
            WorkflowInstanceStatus.Completed,
            candidate => candidate.Entries.Count == 1);
        clock.Advance(TimeSpan.FromMinutes(5));

        snapshot.Status.Should().Be(WorkflowInstanceStatus.Completed);
        state.Entries.Should().Equal("after-delay");
    }

    [Fact]
    public async Task NestedSelectedDelay_AfterDueTime_ContinuesExactlyOnce()
    {
        var clock = NewClock();
        using var provider = CreateProvider(clock);
        var definition = Workflow.Ephemeral<TimerState>(
                DefinitionId.New(),
                DefinitionVersion.Initial)
            .Init<string>(_ => new TimerState())
            .If(
                _ => true,
                nested => nested
                    .Delay(TimeSpan.FromMinutes(5))
                    .Then<RecordingStep>())
            .End(snapshot => snapshot.Value.Entries.Count)
            .Build();
        var handle = Register(provider, definition);
        var instance = await StartAsync(handle, "nested-delay");
        clock.Advance(TimeSpan.FromMinutes(5));

        var (snapshot, state) = await WaitForStateAsync(
            instance,
            WorkflowInstanceStatus.Completed,
            candidate => candidate.Entries.Count == 1);
        clock.Advance(TimeSpan.FromMinutes(5));

        snapshot.Status.Should().Be(WorkflowInstanceStatus.Completed);
        state.Entries.Should().Equal("after-delay");
    }

    [Fact]
    public async Task AutomaticTimerDispatch_WhenOneContinuationThrows_RetriesItAndPreservesLaterTimers()
    {
        var clock = NewClock();
        var gate = new ThrowOnceGate();
        using var provider = CreateProvider(clock);
        var engine = provider.GetRequiredService<EphemeralWorkflowEngine>();
        var retryingHandle = Register(provider, RetryingDelayedDefinition(TimeSpan.FromMinutes(5), gate));
        var laterHandle = Register(provider, DelayedDefinition(TimeSpan.FromMinutes(5)));
        var retrying = await StartAsync(retryingHandle, "retrying-timer");
        var later = await StartAsync(laterHandle, "later-timer");
        clock.Advance(TimeSpan.FromMinutes(5));
        await gate.FirstAttempt.WaitAsync(TestContext.Current.CancellationToken);
        await WaitUntilAsync(
            () => !engine.IsTimerDispatching,
            "the failed automatic dispatch must release and rearm its due timers");
        clock.Advance(TimeSpan.FromMilliseconds(100));
        await WaitUntilAsync(
            () => gate.SecondAttempt.IsCompleted,
            "the rearmed timer must enter its second continuation attempt");
        (await retrying.WaitForOutputAsync(TestContext.Current.CancellationToken)).Should().Be(1);
        (await later.WaitForOutputAsync(TestContext.Current.CancellationToken)).Should().Be(1);

        var retryingState = await retrying.GetStateAsync<TimerState>(TestContext.Current.CancellationToken);
        var laterState = await later.GetStateAsync<TimerState>(TestContext.Current.CancellationToken);

        gate.Attempts.Should().Be(2);
        (await retrying.GetSnapshotAsync(TestContext.Current.CancellationToken))
            .Status.Should().Be(WorkflowInstanceStatus.Completed);
        (await later.GetSnapshotAsync(TestContext.Current.CancellationToken))
            .Status.Should().Be(WorkflowInstanceStatus.Completed);
        retryingState.Entries.Should().Equal("after-delay");
        laterState.Entries.Should().Equal("after-delay");
    }

    [Fact]
    public async Task AutomaticTimerDispatch_WithTerminalCancellation_DoesNotDuplicateDueContinuations()
    {
        const int instanceCount = 90;
        var clock = NewClock();
        using var provider = CreateProvider(clock);
        var handle = Register(provider, DelayedDefinition(TimeSpan.FromMinutes(5)));
        var instances = new WorkflowInstanceHandle[instanceCount];
        for (var index = 0; index < instances.Length; index++)
        {
            instances[index] = await StartAsync(handle, $"timer-race-{index}");
        }

        foreach (var instance in instances.Where((_, index) => index % 3 == 0))
        {
            (await instance.TerminateAsync(TestContext.Current.CancellationToken))
                .Should().Be(WorkflowTerminationStatus.Terminated);
        }

        clock.Advance(TimeSpan.FromMinutes(5));
        for (var index = 0; index < instances.Length; index++)
        {
            var expected = index % 3 == 0
                ? WorkflowInstanceStatus.Terminated
                : WorkflowInstanceStatus.Completed;
            var (snapshot, state) = await WaitForStateAsync(
                instances[index],
                expected,
                candidate => expected == WorkflowInstanceStatus.Terminated || candidate.Entries.Count == 1);
            if (index % 3 == 0)
            {
                snapshot.Status.Should().Be(WorkflowInstanceStatus.Terminated);
                state.Entries.Should().BeEmpty();
            }
            else
            {
                snapshot.Status.Should().Be(WorkflowInstanceStatus.Completed);
                state.Entries.Should().Equal("after-delay");
            }
        }
    }

    private static async Task<(WorkflowInstanceSnapshot Snapshot, TimerState State)> WaitForStateAsync(
        WorkflowInstanceHandle instance,
        WorkflowInstanceStatus expected,
        Func<TimerState, bool> statePredicate)
    {
        for (var attempt = 0; attempt < 10_000; attempt++)
        {
            TestContext.Current.CancellationToken.ThrowIfCancellationRequested();
            var snapshot = await instance.GetSnapshotAsync(TestContext.Current.CancellationToken);
            var state = await instance.GetStateAsync<TimerState>(TestContext.Current.CancellationToken);
            if (snapshot.Status == expected && statePredicate(state))
            {
                return (snapshot, state);
            }

            await Task.Yield();
        }

        throw new InvalidOperationException(
            $"Instance '{instance.InstanceId}' did not publish '{expected}' with the expected state.");
    }

    private static Clock NewClock() =>
        new(new DateTimeOffset(2026, 7, 2, 12, 0, 0, TimeSpan.Zero));

    private static async Task WaitUntilAsync(Func<bool> condition, string because)
    {
        for (var attempt = 0; attempt < 10_000 && !condition(); attempt++)
        {
            await Task.Factory.StartNew(
                static () => { },
                TestContext.Current.CancellationToken,
                TaskCreationOptions.DenyChildAttach,
                TaskScheduler.Default);
        }

        condition().Should().BeTrue(because);
    }

    private static ServiceProvider CreateProvider(Clock clock)
    {
        var services = new ServiceCollection();
        services.AddSingleton<TimeProvider>(clock.TimeProvider);
        services.AddTransient<RecordingStep>();
        services.AddOrcaCoreEphemeralEngine(new EphemeralEngineHostOptions
        {
            StructuredExecution = new StructuredExecutionHostOptions
            {
                MaxConcurrentExecutionPathsPerInstance = 4,
                StepThrottles = []
            },
            TransientPools = []
        });
        return services.BuildServiceProvider();
    }

    private static EphemeralDefinitionHandle<string, int> Register(
        ServiceProvider provider,
        EphemeralWorkflowDefinition<string, int> definition) =>
        provider.GetRequiredService<IWorkflowDefinitionRegistry>()
            .Register(definition)
            .GetHandleOrThrow();

    private static async Task<WorkflowInstanceHandle<int>> StartAsync(
        EphemeralDefinitionHandle<string, int> handle,
        string idempotencyKey) =>
        (await handle.StartOrGetAsync(
            "start",
            StartIdempotencyKey.Create(idempotencyKey),
            TestContext.Current.CancellationToken)).GetHandleOrThrow();

    private static EphemeralWorkflowDefinition<string, int> DelayedDefinition(
        TimeSpan delay) =>
        Workflow.Ephemeral<TimerState>(
                DefinitionId.New(),
                DefinitionVersion.Initial)
            .Init<string>(_ => new TimerState())
            .Delay(delay)
            .Then<RecordingStep>()
            .End(snapshot => snapshot.Value.Entries.Count)
            .Build();

    private static EphemeralWorkflowDefinition<string, int> RetryingDelayedDefinition(
        TimeSpan delay,
        ThrowOnceGate gate) =>
        Workflow.Ephemeral<TimerState>(
                DefinitionId.New(),
                DefinitionVersion.Initial)
            .Init<string>(_ => new TimerState())
            .Delay(delay)
            .If(
                _ => gate.Evaluate(),
                nested => nested.Then<RecordingStep>())
            .End(snapshot => snapshot.Value.Entries.Count)
            .Build();

    private sealed class TimerState
    {
        public List<string> Entries { get; set; } = [];
    }

    private sealed class RecordingStep : IStep<TimerState>
    {
        public ValueTask<StepResult> ExecuteAsync(
            StepContext<TimerState> context,
            CancellationToken cancellationToken)
        {
            context.State.Entries.Add("after-delay");
            return ValueTask.FromResult<StepResult>(new StepResult.Completed());
        }
    }

    private sealed class ThrowOnceGate
    {
        private int attempts;
        private readonly TaskCompletionSource firstAttempt =
            new(TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly TaskCompletionSource secondAttempt =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        internal int Attempts => Volatile.Read(ref attempts);
        internal Task FirstAttempt => firstAttempt.Task;
        internal Task SecondAttempt => secondAttempt.Task;

        internal bool Evaluate()
        {
            if (Interlocked.Increment(ref attempts) == 1)
            {
                firstAttempt.TrySetResult();
                throw new NotSupportedException("first timer continuation fails");
            }

            secondAttempt.TrySetResult();
            return true;
        }
    }
}
