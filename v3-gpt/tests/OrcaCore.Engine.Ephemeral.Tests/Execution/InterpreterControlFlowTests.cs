using AwesomeAssertions;
using OrcaCore.Abstractions.Ids;
using OrcaCore.Abstractions.Instances;
using OrcaCore.Abstractions.Steps;
using OrcaCore.Core.Building;
using OrcaCore.Engine.Ephemeral;
using Xunit;

namespace OrcaCore.Engine.Ephemeral.Tests.Execution;

public sealed class InterpreterControlFlowTests
{
    [Fact]
    public async Task Run_IfConditionTrue_ExecutesThenBranchOnly()
    {
        var state = new TestState { Flag = true };
        var engine = new EphemeralWorkflowEngine();
        var definition = Definition(new WorkflowBuilder<TestState>()
            .Init<string>(_ => state)
            .If(
                current => current.Flag,
                then => then.Then(() => new AppendStep("then")),
                otherwise => otherwise.Then(() => new AppendStep("else")))
            .Then(() => new AppendStep("after"))
            .End());

        engine.RegisterDefinition(definition);

        var snapshot = await engine.StartAsync<string, TestState>(
            definition.DefinitionId,
            "start",
            TestContext.Current.CancellationToken);

        snapshot.Status.Should().Be(WorkflowStatus.Completed);
        state.Values.Should().Equal(["then", "after"]);
    }

    [Fact]
    public async Task Run_IfConditionFalse_ExecutesElseBranchOnly()
    {
        var state = new TestState { Flag = false };
        var engine = new EphemeralWorkflowEngine();
        var definition = Definition(new WorkflowBuilder<TestState>()
            .Init<string>(_ => state)
            .If(
                current => current.Flag,
                then => then.Then(() => new AppendStep("then")),
                otherwise => otherwise.Then(() => new AppendStep("else")))
            .Then(() => new AppendStep("after"))
            .End());

        engine.RegisterDefinition(definition);

        await engine.StartAsync<string, TestState>(
            definition.DefinitionId,
            "start",
            TestContext.Current.CancellationToken);

        state.Values.Should().Equal(["else", "after"]);
    }

    [Fact]
    public async Task Run_IfWithoutElse_ContinuesAfterSkippedBranch()
    {
        var state = new TestState { Flag = false };
        var engine = new EphemeralWorkflowEngine();
        var definition = Definition(new WorkflowBuilder<TestState>()
            .Init<string>(_ => state)
            .If(current => current.Flag, then => then.Then(() => new AppendStep("then")))
            .Then(() => new AppendStep("after"))
            .End());

        engine.RegisterDefinition(definition);

        await engine.StartAsync<string, TestState>(
            definition.DefinitionId,
            "start",
            TestContext.Current.CancellationToken);

        state.Values.Should().Equal(["after"]);
    }

    [Fact]
    public async Task Run_WhileConditionTrueThenFalse_ReevaluatesConditionEachIteration()
    {
        var state = new TestState();
        var engine = new EphemeralWorkflowEngine();
        var definition = Definition(new WorkflowBuilder<TestState>()
            .Init<string>(_ => state)
            .While(
                current =>
                {
                    current.ConditionChecks++;
                    return current.Iterations < 3;
                },
                body => body.Then(() => new IncrementIterationStep()))
            .Then(() => new AppendStep("after"))
            .End());

        engine.RegisterDefinition(definition);

        await engine.StartAsync<string, TestState>(
            definition.DefinitionId,
            "start",
            TestContext.Current.CancellationToken);

        state.Iterations.Should().Be(3);
        state.ConditionChecks.Should().Be(4);
        state.Values.Should().Equal(["after"]);
    }

    [Fact]
    public async Task Run_NestedIfInsideWhile_MaintainsCorrectPosition()
    {
        var state = new TestState();
        var engine = new EphemeralWorkflowEngine();
        var definition = Definition(new WorkflowBuilder<TestState>()
            .Init<string>(_ => state)
            .While(
                current => current.Iterations < 2,
                body => body
                    .If(
                        current => current.Iterations == 0,
                        then => then.Then(() => new AppendStep("first")),
                        otherwise => otherwise.Then(() => new AppendStep("later")))
                    .Then(() => new IncrementIterationStep()))
            .Then(() => new AppendStep("after"))
            .End());

        engine.RegisterDefinition(definition);

        await engine.StartAsync<string, TestState>(
            definition.DefinitionId,
            "start",
            TestContext.Current.CancellationToken);

        state.Values.Should().Equal(["first", "later", "after"]);
        state.Iterations.Should().Be(2);
    }

    private static OrcaCore.Core.Definitions.WorkflowDefinition<TestState> Definition(
        WorkflowBuilder<TestState> builder)
    {
        return builder.Build(DefinitionId.New(), DefinitionVersion.Initial);
    }

    private sealed class TestState
    {
        public bool Flag { get; init; }

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
