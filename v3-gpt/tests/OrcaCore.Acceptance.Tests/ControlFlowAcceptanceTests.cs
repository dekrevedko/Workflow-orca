using AwesomeAssertions;
using OrcaCore.Abstractions.Ids;
using OrcaCore.Abstractions.Instances;
using OrcaCore.Abstractions.Steps;
using OrcaCore.Core.Building;
using OrcaCore.Engine.Ephemeral;
using Xunit;

namespace OrcaCore.Acceptance.Tests;

public sealed class ControlFlowAcceptanceTests
{
    [Fact]
    [Trait("AC", "AC-002")]
    public async Task If_ExecutesExactlyOneBranch_ThenContinues()
    {
        var state = new TestState { Approved = true };
        var engine = new EphemeralWorkflowEngine();
        var definition = new WorkflowBuilder<TestState>()
            .Init<string>(_ => state)
            .If(
                current => current.Approved,
                then => then.Then(() => new AppendStep("approved")),
                otherwise => otherwise.Then(() => new AppendStep("rejected")))
            .Then(() => new AppendStep("continued"))
            .End()
            .Build(DefinitionId.New(), DefinitionVersion.Initial);

        engine.RegisterDefinition(definition);

        var snapshot = await engine.StartAsync<string, TestState>(
            definition.DefinitionId,
            "start",
            TestContext.Current.CancellationToken);

        snapshot.Status.Should().Be(WorkflowStatus.Completed);
        state.Values.Should().Equal(["approved", "continued"]);
    }

    [Fact]
    [Trait("AC", "AC-003")]
    public async Task While_RunsThreeIterations_CompletesAfterFourthCheck()
    {
        var state = new TestState();
        var engine = new EphemeralWorkflowEngine();
        var definition = new WorkflowBuilder<TestState>()
            .Init<string>(_ => state)
            .While(
                current =>
                {
                    current.ConditionChecks++;
                    return current.Iterations < 3;
                },
                body => body.Then(() => new IncrementIterationStep()))
            .End()
            .Build(DefinitionId.New(), DefinitionVersion.Initial);

        engine.RegisterDefinition(definition);

        var snapshot = await engine.StartAsync<string, TestState>(
            definition.DefinitionId,
            "start",
            TestContext.Current.CancellationToken);

        snapshot.Status.Should().Be(WorkflowStatus.Completed);
        state.Iterations.Should().Be(3);
        state.ConditionChecks.Should().Be(4);
    }

    private sealed class TestState
    {
        public bool Approved { get; init; }

        public int Iterations { get; set; }

        public int ConditionChecks { get; set; }

        public List<string> Values { get; } = [];
    }

    private sealed class AppendStep(string value) : IStep<TestState>
    {
        public ValueTask<StepResult> ExecuteAsync(
            StepContext<TestState> context,
            CancellationToken cancellationToken)
        {
            context.State.Values.Add(value);
            return ValueTask.FromResult<StepResult>(new StepResult.Completed());
        }
    }

    private sealed class IncrementIterationStep : IStep<TestState>
    {
        public ValueTask<StepResult> ExecuteAsync(
            StepContext<TestState> context,
            CancellationToken cancellationToken)
        {
            context.State.Iterations++;
            return ValueTask.FromResult<StepResult>(new StepResult.Completed());
        }
    }
}
