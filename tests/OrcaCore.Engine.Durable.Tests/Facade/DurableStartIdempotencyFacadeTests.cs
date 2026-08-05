using AwesomeAssertions;
using OrcaCore.Engine.Durable.Definitions;
using OrcaCore.Engine.Durable.Execution;
using OrcaCore.Providers.InMemory;
using Xunit;

namespace OrcaCore.Engine.Durable.Tests.Facade;

public sealed class DurableStartIdempotencyFacadeTests
{
    [Fact]
    public async Task ReplacementHost_SameKeyWithChangedInput_ReturnsConflictWithoutAnIncompatibleHandle()
    {
        var store = new InMemoryWorkflowProvider();
        var definition = Workflow.Durable<State>(DefinitionId.New(), DefinitionVersion.Initial)
            .Init<Input>(input => new State(input.Value))
            .End(WorkflowOutcomeName.Create("completed"))
            .Build();
        var key = StartIdempotencyKey.Create("replacement-host-input-conflict");

        var firstHandle = CreateRegistry(store).Register(definition).GetHandleOrThrow();
        var firstStart = await firstHandle.StartOrGetAsync(
            new Input(1),
            key,
            TestContext.Current.CancellationToken);
        var firstInstance = firstStart.GetHandleOrThrow().InstanceId;

        var replacementHandle = CreateRegistry(store).Register(definition).GetHandleOrThrow();
        var conflict = await replacementHandle.StartOrGetAsync(
            new Input(2),
            key,
            TestContext.Current.CancellationToken);

        conflict.Should().BeOfType<WorkflowStartResult<WorkflowInstanceHandle>.Conflict>();
        var getIncompatibleHandle = () => conflict.GetHandleOrThrow();
        getIncompatibleHandle.Should().Throw<WorkflowStartIdempotencyConflictException>();

        var compatibleReplay = await replacementHandle.StartOrGetAsync(
            new Input(1),
            key,
            TestContext.Current.CancellationToken);
        compatibleReplay.Should().BeOfType<WorkflowStartResult<WorkflowInstanceHandle>.Accepted>()
            .Which.WasExisting.Should().BeTrue();
        compatibleReplay.GetHandleOrThrow().InstanceId.Should().Be(firstInstance);
    }

    [Fact]
    public async Task ReplacementHost_SameKeyWithChangedDefinitionFingerprint_ReturnsConflict()
    {
        var store = new InMemoryWorkflowProvider();
        var definitionId = DefinitionId.New();
        var original = Workflow.Durable<State>(definitionId, DefinitionVersion.Initial)
            .Init<Input>(input => new State(input.Value))
            .End(WorkflowOutcomeName.Create("original-outcome"))
            .Build();
        var changed = Workflow.Durable<State>(definitionId, DefinitionVersion.Initial)
            .Init<Input>(input => new State(input.Value))
            .End(WorkflowOutcomeName.Create("changed-outcome"))
            .Build();
        original.DefinitionFingerprint.Should().NotBe(changed.DefinitionFingerprint);
        var key = StartIdempotencyKey.Create("replacement-host-definition-conflict");

        var firstHandle = CreateRegistry(store).Register(original).GetHandleOrThrow();
        _ = await firstHandle.StartOrGetAsync(
            new Input(1),
            key,
            TestContext.Current.CancellationToken);

        var replacementHandle = CreateRegistry(store).Register(changed).GetHandleOrThrow();
        var conflict = await replacementHandle.StartOrGetAsync(
            new Input(1),
            key,
            TestContext.Current.CancellationToken);

        conflict.Should().BeOfType<WorkflowStartResult<WorkflowInstanceHandle>.Conflict>();
        var getIncompatibleHandle = () => conflict.GetHandleOrThrow();
        getIncompatibleHandle.Should().Throw<WorkflowStartIdempotencyConflictException>();
    }

    private static DurableWorkflowDefinitionRegistry CreateRegistry(InMemoryWorkflowProvider store)
    {
        var notifications = new DurableFacadeNotificationHub();
        var processor = new DurableCommandProcessor(store, runtimeObserver: notifications);
        var runtime = new DurableWorkflowRuntime(
            processor,
            new DurableDefinitionRegistry(),
            TimeProvider.System,
            projectionStore: store);
        return new DurableWorkflowDefinitionRegistry(
            runtime,
            store,
            store,
            processor,
            notifications,
            TimeProvider.System);
    }

    private sealed record Input(int Value);
    private sealed record State(int Value);
}
