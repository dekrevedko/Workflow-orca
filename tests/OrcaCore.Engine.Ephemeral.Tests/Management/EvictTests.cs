using AwesomeAssertions;
using OrcaCore.Abstractions.Errors;
using OrcaCore.Abstractions.Ids;
using OrcaCore.Abstractions.Instances;
using OrcaCore.Abstractions.Steps;
using OrcaCore.Core.Building;
using OrcaCore.Core.Definitions;
using Xunit;

namespace OrcaCore.Engine.Ephemeral.Tests.Management;

public sealed class EvictTests
{
    [Fact]
    public async Task Evict_TerminalInstance_RemovesItFromQueriesAndRouting()
    {
        var engine = new EphemeralWorkflowEngine();
        var definition = CompletedDefinition();
        engine.RegisterDefinition(definition);
        var snapshot = await engine.StartAsync<string, TestState>(
            definition.DefinitionId,
            "done",
            TestContext.Current.CancellationToken);

        var evicted = engine.Management.Evict(snapshot.InstanceId);

        evicted.Should().BeTrue();
        engine.Management.All().List().Should().BeEmpty();
        engine.Management.Evict(snapshot.InstanceId).Should().BeFalse("evict is idempotent for unknown instances");
    }

    [Fact]
    public async Task Evict_ActiveInstance_ThrowsAndKeepsInstance()
    {
        var engine = new EphemeralWorkflowEngine();
        var definition = WaitingDefinition();
        engine.RegisterDefinition(definition);
        var snapshot = await engine.StartAsync<string, TestState>(
            definition.DefinitionId,
            "wait",
            TestContext.Current.CancellationToken);

        var act = () => engine.Management.Evict(snapshot.InstanceId);

        act.Should().Throw<WorkflowLifecycleException>()
            .WithMessage("*cannot be evicted before reaching a terminal status*");
        engine.Management.All().List().Should().ContainSingle();
    }

    [Fact]
    public async Task EvictTerminal_RemovesOnlyTerminalInstances()
    {
        var engine = new EphemeralWorkflowEngine();
        var completed = CompletedDefinition();
        var waiting = WaitingDefinition();
        engine.RegisterDefinition(completed);
        engine.RegisterDefinition(waiting);
        await engine.StartAsync<string, TestState>(completed.DefinitionId, "a", TestContext.Current.CancellationToken);
        await engine.StartAsync<string, TestState>(completed.DefinitionId, "b", TestContext.Current.CancellationToken);
        await engine.StartAsync<string, TestState>(waiting.DefinitionId, "w", TestContext.Current.CancellationToken);

        var evicted = engine.Management.EvictTerminal();

        evicted.Should().Be(2);
        engine.Management.All().List().Should().ContainSingle()
            .Which.Status.Should().Be(WorkflowStatus.Waiting);
    }

    [Fact]
    public async Task EvictTerminal_RemovesCompletedSagaRuntimeState()
    {
        var engine = new EphemeralWorkflowEngine();
        var definition = new SagaBuilder<TestState>()
            .Init<string>(input => new TestState { Name = input })
            .Then(() => new NoOpStep())
            .End()
            .Build(DefinitionId.New(), DefinitionVersion.Initial);
        var snapshot = await engine.StartSagaAsync<string, TestState>(
            definition,
            "saga",
            TestContext.Current.CancellationToken);

        engine.Management.EvictTerminal().Should().Be(1);

        var act = () => engine.RequestSagaCompensationAsync<TestState>(
            snapshot.InstanceId,
            TestContext.Current.CancellationToken);
        await act.Should().ThrowAsync<WorkflowRoutingException>(
            "evicting a saga instance must also release its saga runtime state");
    }

    private static WorkflowDefinition<TestState> CompletedDefinition()
    {
        return global::OrcaCore.Workflow.Ephemeral<TestState>(DefinitionId.New(), DefinitionVersion.Initial)
            .Init<string>(input => new TestState { Name = input })
            .Then(() => new NoOpStep())
            .End()
            .Build();
    }

    private static WorkflowDefinition<TestState> WaitingDefinition()
    {
        return global::OrcaCore.Workflow.Ephemeral<TestState>(DefinitionId.New(), DefinitionVersion.Initial)
            .Init<string>(input => new TestState { Name = input })
            .Wait("Ready", state => CorrelationId.Create(state.Name))
            .Then(() => new NoOpStep())
            .End()
            .Build();
    }

    private sealed class TestState
    {
        public string Name { get; set; } = string.Empty;
    }

    private sealed class NoOpStep : IStep<TestState>
    {
        public ValueTask<StepResult> ExecuteAsync(
            StepContext<TestState> context,
            CancellationToken cancellationToken)
        {
            return ValueTask.FromResult<StepResult>(new StepResult.Completed());
        }
    }
}
