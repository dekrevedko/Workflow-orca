using AwesomeAssertions;
using OrcaCore.Abstractions.Errors;
using OrcaCore.Abstractions.Ids;
using OrcaCore.Abstractions.Instances;
using OrcaCore.Abstractions.Steps;
using OrcaCore.Core.Building;
using OrcaCore.Engine.Ephemeral;
using OrcaCore.TestSupport;
using Xunit;

namespace OrcaCore.Engine.Ephemeral.Tests.Execution;

public sealed class InterpreterTests
{
    [Fact]
    public async Task Run_InitStepEnd_CompletesAndMutatesState()
    {
        var state = new TestState();
        var engine = new EphemeralWorkflowEngine();
        var definition = Definition(new WorkflowBuilder<TestState>()
            .Init<string>(_ => state)
            .Then(() => new MutatingStep("first"))
            .End());

        engine.RegisterDefinition(definition);

        var snapshot = await engine.StartAsync<string, TestState>(
            definition.DefinitionId,
            "start",
            TestContext.Current.CancellationToken);

        snapshot.Status.Should().Be(WorkflowStatus.Completed);
        state.Values.Should().Equal(["first"]);
    }

    [Fact]
    public async Task Run_TwoSteps_ExecuteInOrder_SharedState()
    {
        var state = new TestState();
        var engine = new EphemeralWorkflowEngine();
        var definition = Definition(new WorkflowBuilder<TestState>()
            .Init<string>(_ => state)
            .Then(() => new MutatingStep("first"))
            .Then(() => new MutatingStep("second"))
            .End());

        engine.RegisterDefinition(definition);

        await engine.StartAsync<string, TestState>(
            definition.DefinitionId,
            "start",
            TestContext.Current.CancellationToken);

        state.Values.Should().Equal(["first", "second"]);
    }

    [Fact]
    public async Task Run_StepReturnsFailed_InstanceFailed_LaterStepsSkipped()
    {
        var state = new TestState();
        var engine = new EphemeralWorkflowEngine();
        var definition = Definition(new WorkflowBuilder<TestState>()
            .Init<string>(_ => state)
            .Then(() => new FailingResultStep())
            .Then(() => new MutatingStep("skipped"))
            .End());

        engine.RegisterDefinition(definition);

        var snapshot = await engine.StartAsync<string, TestState>(
            definition.DefinitionId,
            "start",
            TestContext.Current.CancellationToken);

        snapshot.Status.Should().Be(WorkflowStatus.Failed);
        snapshot.ErrorSummary.Should().Contain(nameof(WorkflowDefinitionException));
        snapshot.ErrorSummary.Should().Contain("step failed");
        state.Values.Should().BeEmpty();
    }

    [Fact]
    public async Task Run_StepThrows_InstanceFailed_ErrorDetailsCaptured()
    {
        var clock = new Clock(new DateTimeOffset(2026, 7, 2, 10, 15, 0, TimeSpan.Zero));
        var state = new TestState();
        var engine = new EphemeralWorkflowEngine(clock.TimeProvider);
        var definition = Definition(new WorkflowBuilder<TestState>()
            .Init<string>(_ => state)
            .Then(() => new ThrowingStep())
            .End());

        engine.RegisterDefinition(definition);

        var snapshot = await engine.StartAsync<string, TestState>(
            definition.DefinitionId,
            "start",
            TestContext.Current.CancellationToken);

        snapshot.Status.Should().Be(WorkflowStatus.Failed);
        snapshot.ErrorSummary.Should().Contain(nameof(InvalidOperationException));
        snapshot.ErrorSummary.Should().Contain("boom");
        snapshot.ErrorSummary.Should().Contain("root/1");
        snapshot.UpdatedAt.Should().Be(clock.Now);
    }

    [Fact]
    public async Task Run_EndWithOutcome_RecordsOutcomeName()
    {
        var engine = new EphemeralWorkflowEngine();
        var definition = Definition(new WorkflowBuilder<TestState>()
            .Init<string>(_ => new TestState())
            .End("Approved"));

        engine.RegisterDefinition(definition);

        var snapshot = await engine.StartAsync<string, TestState>(
            definition.DefinitionId,
            "start",
            TestContext.Current.CancellationToken);

        snapshot.EndOutcomeName.Should().Be("Approved");
    }

    [Fact]
    public async Task Run_UnsupportedResult_ThrowsNamingOwnerTask()
    {
        var engine = new EphemeralWorkflowEngine();
        var definition = Definition(new WorkflowBuilder<TestState>()
            .Init<string>(_ => new TestState())
            .Then(() => new YieldResultStep())
            .End());

        engine.RegisterDefinition(definition);

        var act = async () => await engine.StartAsync<string, TestState>(
            definition.DefinitionId,
            "start",
            TestContext.Current.CancellationToken);

        await act.Should().ThrowAsync<NotSupportedException>()
            .WithMessage("*T1-15*");
    }

    private static OrcaCore.Core.Definitions.WorkflowDefinition<TestState> Definition(
        WorkflowBuilder<TestState> builder)
    {
        return builder.Build(DefinitionId.New(), DefinitionVersion.Initial);
    }

    private sealed class TestState
    {
        public List<string> Values { get; } = [];
    }

    private sealed class MutatingStep(string value) : IStep<TestState>
    {
        public ValueTask<StepResult> ExecuteAsync(
            StepContext<TestState> context,
            CancellationToken cancellationToken)
        {
            context.State.Values.Add(value);
            return ValueTask.FromResult<StepResult>(new StepResult.Completed());
        }
    }

    private sealed class FailingResultStep : IStep<TestState>
    {
        public ValueTask<StepResult> ExecuteAsync(
            StepContext<TestState> context,
            CancellationToken cancellationToken)
        {
            return ValueTask.FromResult<StepResult>(
                new StepResult.Failed(new WorkflowDefinitionException("step failed")));
        }
    }

    private sealed class ThrowingStep : IStep<TestState>
    {
        public ValueTask<StepResult> ExecuteAsync(
            StepContext<TestState> context,
            CancellationToken cancellationToken)
        {
            throw new InvalidOperationException("boom");
        }
    }

    private sealed class YieldResultStep : IStep<TestState>
    {
        public ValueTask<StepResult> ExecuteAsync(
            StepContext<TestState> context,
            CancellationToken cancellationToken)
        {
            return ValueTask.FromResult<StepResult>(new StepResult.Yield());
        }
    }
}
