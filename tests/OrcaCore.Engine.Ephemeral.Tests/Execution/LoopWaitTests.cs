using AwesomeAssertions;
using Microsoft.Extensions.DependencyInjection;
using OrcaCore.Hosting;
using Xunit;

namespace OrcaCore.Engine.Ephemeral.Tests.Execution;

public sealed class LoopWaitTests
{
    private static readonly EventName Tick = EventName.Create("tick");
    private static readonly EventName Approval = EventName.Create("approval");

    [Fact]
    public async Task Run_WhileRegistersWaitEachIteration_CreatesFreshWaitIds()
    {
        using var provider = CreateProvider();
        var handle = Register(provider, Definition());
        var instance = await StartAsync(handle, "fresh-waits");
        var first = await instance.GetSnapshotAsync(TestContext.Current.CancellationToken);

        var firstWaitId = first.ActiveWaits.Should().ContainSingle().Which.WaitId;
        var delivery = await DeliverAsync(
            provider,
            instance.InstanceId,
            Tick,
            IterationCorrelation(0),
            "fresh-waits-first");
        var second = await instance.GetSnapshotAsync(TestContext.Current.CancellationToken);

        delivery.Status.Should().Be(EphemeralEventRouteStatus.Accepted);
        second.Status.Should().Be(WorkflowInstanceStatus.Waiting);
        second.ActiveWaits.Should().ContainSingle()
            .Which.WaitId.Should().NotBe(firstWaitId);
    }

    [Fact]
    public async Task RaiseEventAsync_EventForPreviousIteration_DoesNotResumeLaterIteration()
    {
        using var provider = CreateProvider();
        var handle = Register(provider, Definition());
        var instance = await StartAsync(handle, "stale-iteration");
        _ = await DeliverAsync(
            provider,
            instance.InstanceId,
            Tick,
            IterationCorrelation(0),
            "stale-iteration-first");

        var stale = await DeliverAsync(
            provider,
            instance.InstanceId,
            Tick,
            IterationCorrelation(0),
            "stale-iteration-old");
        var snapshot = await instance.GetSnapshotAsync(TestContext.Current.CancellationToken);
        var state = await instance.GetStateAsync<TestState>(TestContext.Current.CancellationToken);

        stale.Status.Should().Be(EphemeralEventRouteStatus.NoActiveWait);
        snapshot.Status.Should().Be(WorkflowInstanceStatus.Waiting);
        snapshot.ActiveWaits.Should().ContainSingle()
            .Which.CorrelationId.Should().Be(IterationCorrelation(1));
        state.Iteration.Should().Be(1);
    }

    [Fact]
    public async Task RaiseEventAsync_CurrentIterationEvent_ResumesCurrentWait()
    {
        using var provider = CreateProvider();
        var handle = Register(provider, Definition());
        var instance = await StartAsync(handle, "current-iteration");
        _ = await DeliverAsync(
            provider,
            instance.InstanceId,
            Tick,
            IterationCorrelation(0),
            "current-iteration-first");

        var current = await DeliverAsync(
            provider,
            instance.InstanceId,
            Tick,
            IterationCorrelation(1),
            "current-iteration-second");
        var snapshot = await instance.GetSnapshotAsync(TestContext.Current.CancellationToken);
        var state = await instance.GetStateAsync<TestState>(TestContext.Current.CancellationToken);

        current.Status.Should().Be(EphemeralEventRouteStatus.Accepted);
        snapshot.Status.Should().Be(WorkflowInstanceStatus.Completed);
        state.Iteration.Should().Be(2);
    }

    [Fact]
    public async Task PreviousIterationEvent_RemainsStaleForLaterWait()
    {
        using var provider = CreateProvider();
        var handle = Register(provider, Definition());
        var instance = await StartAsync(handle, "stale-then-current");
        _ = await DeliverAsync(
            provider,
            instance.InstanceId,
            Tick,
            IterationCorrelation(0),
            "stale-then-current-first");
        var stale = await DeliverAsync(
            provider,
            instance.InstanceId,
            Tick,
            IterationCorrelation(0),
            "stale-then-current-old");

        var current = await DeliverAsync(
            provider,
            instance.InstanceId,
            Tick,
            IterationCorrelation(1),
            "stale-then-current-second");
        var snapshot = await instance.GetSnapshotAsync(TestContext.Current.CancellationToken);
        var state = await instance.GetStateAsync<TestState>(TestContext.Current.CancellationToken);

        stale.Status.Should().Be(EphemeralEventRouteStatus.NoActiveWait);
        current.Status.Should().Be(EphemeralEventRouteStatus.Accepted);
        snapshot.Status.Should().Be(WorkflowInstanceStatus.Completed);
        snapshot.Failure.Should().BeNull();
        state.Iteration.Should().Be(2);
    }

    [Fact]
    public async Task RaiseEventAsync_WaitInsideWhile_DoesNotReExecuteStepsBeforeWaitOnResume()
    {
        using var provider = CreateProvider();
        var handle = Register(provider, NoDoubleExecutionDefinition());
        var instance = await StartAsync(handle, "no-double-execution");
        var first = await instance.GetSnapshotAsync(TestContext.Current.CancellationToken);
        var firstState = await instance.GetStateAsync<TestState>(TestContext.Current.CancellationToken);

        firstState.PreWaitCount.Should().Be(1);
        firstState.PostWaitCount.Should().Be(0);
        var firstWaitId = first.ActiveWaits.Should().ContainSingle().Which.WaitId;

        _ = await DeliverAsync(
            provider,
            instance.InstanceId,
            Approval,
            CorrelationId.Create("corr-1"),
            "no-double-first");
        var second = await instance.GetSnapshotAsync(TestContext.Current.CancellationToken);
        var secondState = await instance.GetStateAsync<TestState>(TestContext.Current.CancellationToken);

        second.Status.Should().Be(WorkflowInstanceStatus.Waiting);
        second.ActiveWaits.Should().ContainSingle()
            .Which.WaitId.Should().NotBe(firstWaitId);
        secondState.PreWaitCount.Should().Be(2);
        secondState.PostWaitCount.Should().Be(1);

        _ = await DeliverAsync(
            provider,
            instance.InstanceId,
            Approval,
            CorrelationId.Create("corr-1"),
            "no-double-second");
        var completed = await instance.GetSnapshotAsync(TestContext.Current.CancellationToken);
        var completedState = await instance.GetStateAsync<TestState>(TestContext.Current.CancellationToken);

        completed.Status.Should().Be(WorkflowInstanceStatus.Completed);
        completed.ActiveWaits.Should().BeEmpty();
        completedState.PreWaitCount.Should().Be(2);
        completedState.PostWaitCount.Should().Be(2);
    }

    private static ServiceProvider CreateProvider()
    {
        var services = new ServiceCollection();
        services.AddTransient<CaptureAndIncrementStep>();
        services.AddTransient<CountPreWaitStep>();
        services.AddTransient<CountPostWaitStep>();
        services.AddOrcaCoreEphemeralEngine(new EphemeralEngineHostOptions
        {
            StructuredExecution = new StructuredExecutionHostOptions
            {
                MaxConcurrentExecutionPathsPerInstance = 2,
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
        string key) =>
        (await handle.StartOrGetAsync(
            "start",
            StartIdempotencyKey.Create(key),
            TestContext.Current.CancellationToken)).GetHandleOrThrow();

    private static ValueTask<EphemeralEventRouteResult> DeliverAsync(
        ServiceProvider provider,
        InstanceId instanceId,
        EventName eventName,
        CorrelationId correlationId,
        string eventId) =>
        provider.GetRequiredService<EphemeralWorkflowEventRouter>().RouteToInstanceAsync(
            instanceId,
            EphemeralTestEvent.Create(
                EventId.Create(eventId),
                eventName,
                correlationId,
                DateTimeOffset.UtcNow),
            TestContext.Current.CancellationToken);

    private static EphemeralWorkflowDefinition<string> Definition() =>
        Workflow.Ephemeral<TestState>(DefinitionId.New(), DefinitionVersion.Initial)
            .Init<string>(_ => new TestState())
            .While(
                current => current.Value.Iteration < 2,
                body => body
                    .Wait(WorkflowEventContract.Create(Tick, EventContractVersion.Initial), current => IterationCorrelation(current.Value.Iteration))
                    .Then<CaptureAndIncrementStep>())
            .End()
            .Build();

    private static EphemeralWorkflowDefinition<string> NoDoubleExecutionDefinition() =>
        Workflow.Ephemeral<TestState>(DefinitionId.New(), DefinitionVersion.Initial)
            .Init<string>(_ => new TestState())
            .While(
                current => current.Value.PostWaitCount < 2,
                body => body
                    .Then<CountPreWaitStep>()
                    .Wait(WorkflowEventContract.Create(Approval, EventContractVersion.Initial), _ => CorrelationId.Create("corr-1"))
                    .Then<CountPostWaitStep>())
            .End()
            .Build();

    private static CorrelationId IterationCorrelation(int iteration) =>
        CorrelationId.Create($"iteration-{iteration}");

    private sealed class TestState
    {
        public int Iteration { get; set; }

        public int PreWaitCount { get; set; }

        public int PostWaitCount { get; set; }
    }

    private sealed class CaptureAndIncrementStep : IStep<TestState>
    {
        public ValueTask<StepResult> ExecuteAsync(
            StepContext<TestState> context,
            CancellationToken cancellationToken)
        {
            context.State.Iteration++;
            return ValueTask.FromResult<StepResult>(new StepResult.Completed());
        }
    }

    private sealed class CountPreWaitStep : IStep<TestState>
    {
        public ValueTask<StepResult> ExecuteAsync(
            StepContext<TestState> context,
            CancellationToken cancellationToken)
        {
            context.State.PreWaitCount++;
            return ValueTask.FromResult<StepResult>(new StepResult.Completed());
        }
    }

    private sealed class CountPostWaitStep : IStep<TestState>
    {
        public ValueTask<StepResult> ExecuteAsync(
            StepContext<TestState> context,
            CancellationToken cancellationToken)
        {
            context.State.PostWaitCount++;
            return ValueTask.FromResult<StepResult>(new StepResult.Completed());
        }
    }
}
