using AwesomeAssertions;
using OrcaCore.Engine.Durable.Definitions;
using OrcaCore.Engine.Durable.Execution;
using OrcaCore.Providers.InMemory;
using Xunit;

namespace OrcaCore.Engine.Durable.Tests.Facade;

public sealed class DurableWorkflowFacadeTests
{
    [Fact]
    public async Task Registry_StartsReopensAndReturnsCommittedTypedOutput()
    {
        var services = CreateFacade();
        var definition = Workflow.Durable<State>(DefinitionId.New(), DefinitionVersion.Initial)
            .Init<Input>(input => new State(input.Value))
            .End(
                state => new Output(state.Value.Value + 1),
                WorkflowOutcomeName.Create("completed"))
            .Build();

        var handle = services.Registry.Register(definition).GetHandleOrThrow();
        var start = await handle.StartOrGetAsync(
            new Input(41),
            StartIdempotencyKey.Create("durable-facade-start"),
            TestContext.Current.CancellationToken);

        start.Should().BeOfType<WorkflowStartResult<WorkflowInstanceHandle<Output>>.Accepted>()
            .Which.WasExisting.Should().BeFalse();
        (await start.WaitForOutputAsync(TestContext.Current.CancellationToken))
            .Should().Be(new Output(42));

        var instance = start.GetHandleOrThrow();
        var snapshot = await instance.GetSnapshotAsync(TestContext.Current.CancellationToken);
        snapshot.Status.Should().Be(WorkflowInstanceStatus.Completed);
        snapshot.Outcome.Should().Be(WorkflowOutcomeName.Create("completed"));
        (await instance.GetStateAsync<State>(TestContext.Current.CancellationToken))
            .Should().Be(new State(41));

        var reopened = await handle.GetInstanceAsync(
            instance.InstanceId,
            TestContext.Current.CancellationToken);
        (await reopened.GetOutputAsync(TestContext.Current.CancellationToken))
            .Should().BeOfType<WorkflowOutputResult<Output>.Available>()
            .Which.Output.Should().Be(new Output(42));

        var replay = await handle.StartOrGetAsync(
            new Input(41),
            StartIdempotencyKey.Create("durable-facade-start"),
            TestContext.Current.CancellationToken);
        replay.Should().BeOfType<WorkflowStartResult<WorkflowInstanceHandle<Output>>.Accepted>()
            .Which.WasExisting.Should().BeTrue();
        replay.GetHandleOrThrow().InstanceId.Should().Be(instance.InstanceId);
    }

    [Fact]
    public async Task StartConflictAndModeMismatchAreClosedResults()
    {
        var services = CreateFacade();
        var definition = Workflow.Durable<State>(DefinitionId.New(), DefinitionVersion.Initial)
            .Init<Input>(input => new State(input.Value))
            .End(WorkflowOutcomeName.Create("completed"))
            .Build();
        var handle = services.Registry.Register(definition).GetHandleOrThrow();
        _ = await handle.StartOrGetAsync(
            new Input(1),
            StartIdempotencyKey.Create("durable-conflict"),
            TestContext.Current.CancellationToken);

        var conflict = await handle.StartOrGetAsync(
            new Input(2),
            StartIdempotencyKey.Create("durable-conflict"),
            TestContext.Current.CancellationToken);
        conflict.Should().BeOfType<WorkflowStartResult<WorkflowInstanceHandle>.Conflict>();
        var exception = () => conflict.GetHandleOrThrow();
        exception.Should().Throw<WorkflowStartIdempotencyConflictException>();

        var ephemeral = Workflow.Ephemeral<State>(DefinitionId.New(), DefinitionVersion.Initial)
            .Init<Input>(input => new State(input.Value))
            .End(WorkflowOutcomeName.Create("completed"))
            .Build();
        services.Registry.Register(ephemeral)
            .Should().BeOfType<WorkflowRegistrationResult<EphemeralDefinitionHandle<Input>>.HostIncompatible>()
            .Which.Error.Should().BeOfType<DefinitionHostCompatibilityFailure.EngineModeMismatch>();
    }

    [Fact]
    public async Task ActiveWaitProjection_PreservesAuthoredLocationAndDeadlineAcrossHostReplacement()
    {
        var store = new InMemoryWorkflowProvider();
        var definition = Workflow.Durable<State>(DefinitionId.New(), DefinitionVersion.Initial)
            .Init<Input>(input => new State(input.Value))
            .Wait(
                EventName.Create("approval"),
                _ => CorrelationId.Create("durable-active-wait"),
                TimeSpan.FromHours(1))
            .End()
            .Build();
        var firstHost = CreateFacade(store);
        var started = await firstHost.Registry.Register(definition)
            .GetHandleOrThrow()
            .StartOrGetAsync(
                new Input(1),
                StartIdempotencyKey.Create("durable-active-wait"),
                TestContext.Current.CancellationToken);
        var first = await started.GetHandleOrThrow()
            .GetSnapshotAsync(TestContext.Current.CancellationToken);

        var replacementHost = CreateFacade(store);
        var reopened = await replacementHost.Registry.Register(definition)
            .GetHandleOrThrow()
            .GetInstanceAsync(first.InstanceId, TestContext.Current.CancellationToken);
        var recovered = await reopened.GetSnapshotAsync(TestContext.Current.CancellationToken);

        var originalWait = first.ActiveWaits.Should().ContainSingle().Which;
        var recoveredWait = recovered.ActiveWaits.Should().ContainSingle().Which;
        originalWait.AuthoredLocation.Value.Should().Be("workflow:$/n:00000001");
        originalWait.Deadline.Should().Be(originalWait.RegisteredAt.AddHours(1));
        recoveredWait.Should().BeEquivalentTo(originalWait);
    }

    private static FacadeServices CreateFacade(InMemoryWorkflowProvider? suppliedStore = null)
    {
        var store = suppliedStore ?? new InMemoryWorkflowProvider();
        var notifications = new DurableFacadeNotificationHub();
        var processor = new DurableCommandProcessor(store, runtimeObserver: notifications);
        var runtime = new DurableWorkflowRuntime(
            processor,
            new DurableDefinitionRegistry(),
            TimeProvider.System,
            projectionStore: store);
        var registry = new DurableWorkflowDefinitionRegistry(
            runtime,
            store,
            store,
            processor,
            notifications,
            TimeProvider.System);
        return new FacadeServices(registry, store);
    }

    private sealed record FacadeServices(
        DurableWorkflowDefinitionRegistry Registry,
        InMemoryWorkflowProvider Store);

    private sealed record Input(int Value);
    private sealed record State(int Value);
    private sealed record Output(int Value);
}
