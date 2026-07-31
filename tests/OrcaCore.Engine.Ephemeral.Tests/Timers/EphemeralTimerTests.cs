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

        var fired = await provider.GetRequiredService<EphemeralWorkflowEngine>()
            .FireDueTimersAsync(TestContext.Current.CancellationToken);
        var snapshot = await instance.GetSnapshotAsync(TestContext.Current.CancellationToken);
        var state = await instance.GetStateAsync<TimerState>(
            TestContext.Current.CancellationToken);

        fired.Should().BeEmpty();
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

        var firstFire = await provider.GetRequiredService<EphemeralWorkflowEngine>()
            .FireDueTimersAsync(TestContext.Current.CancellationToken);
        var secondFire = await provider.GetRequiredService<EphemeralWorkflowEngine>()
            .FireDueTimersAsync(TestContext.Current.CancellationToken);
        var snapshot = await instance.GetSnapshotAsync(TestContext.Current.CancellationToken);
        var state = await instance.GetStateAsync<TimerState>(
            TestContext.Current.CancellationToken);

        firstFire.Should().ContainSingle();
        secondFire.Should().BeEmpty();
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
            .End()
            .Build();
        var handle = Register(provider, definition);
        var instance = await StartAsync(handle, "nested-delay");
        clock.Advance(TimeSpan.FromMinutes(5));

        var firstFire = await provider.GetRequiredService<EphemeralWorkflowEngine>()
            .FireDueTimersAsync(TestContext.Current.CancellationToken);
        var secondFire = await provider.GetRequiredService<EphemeralWorkflowEngine>()
            .FireDueTimersAsync(TestContext.Current.CancellationToken);
        var state = await instance.GetStateAsync<TimerState>(
            TestContext.Current.CancellationToken);

        firstFire.Should().ContainSingle();
        secondFire.Should().BeEmpty();
        (await instance.GetSnapshotAsync(TestContext.Current.CancellationToken))
            .Status.Should().Be(WorkflowInstanceStatus.Completed);
        state.Entries.Should().Equal("after-delay");
    }

    [Fact]
    public async Task ConcurrentClaimsAndTerminalCancellation_DoNotDuplicateDueContinuations()
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
        var engine = provider.GetRequiredService<EphemeralWorkflowEngine>();
        var claims = await Task.WhenAll(
            Enumerable.Range(0, 8)
                .Select(_ => engine.FireDueTimersAsync(
                    TestContext.Current.CancellationToken)));
        var fired = claims.SelectMany(batch => batch).ToArray();

        fired.Select(snapshot => snapshot.InstanceId).Should().OnlyHaveUniqueItems();
        fired.Should().HaveCount(instanceCount - (instanceCount / 3));
        for (var index = 0; index < instances.Length; index++)
        {
            var snapshot = await instances[index].GetSnapshotAsync(
                TestContext.Current.CancellationToken);
            var state = await instances[index].GetStateAsync<TimerState>(
                TestContext.Current.CancellationToken);
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

    private static Clock NewClock() =>
        new(new DateTimeOffset(2026, 7, 2, 12, 0, 0, TimeSpan.Zero));

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

    private static EphemeralDefinitionHandle<string> Register(
        ServiceProvider provider,
        EphemeralWorkflowDefinition<string> definition) =>
        provider.GetRequiredService<IWorkflowDefinitionRegistry>()
            .Register(definition)
            .GetHandleOrThrow();

    private static async Task<WorkflowInstanceHandle> StartAsync(
        EphemeralDefinitionHandle<string> handle,
        string idempotencyKey) =>
        (await handle.StartOrGetAsync(
            "start",
            StartIdempotencyKey.Create(idempotencyKey),
            TestContext.Current.CancellationToken)).GetHandleOrThrow();

    private static EphemeralWorkflowDefinition<string> DelayedDefinition(
        TimeSpan delay) =>
        Workflow.Ephemeral<TimerState>(
                DefinitionId.New(),
                DefinitionVersion.Initial)
            .Init<string>(_ => new TimerState())
            .Delay(delay)
            .Then<RecordingStep>()
            .End()
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
}
