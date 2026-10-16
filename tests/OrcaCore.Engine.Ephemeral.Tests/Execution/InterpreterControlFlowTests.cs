using AwesomeAssertions;
using OrcaCore.Abstractions.Errors;
using OrcaCore.Abstractions.Ids;
using OrcaCore.Abstractions.Instances;
using OrcaCore.Core.Building;
using OrcaCore.Engine.Ephemeral;
using Xunit;

namespace OrcaCore.Engine.Ephemeral.Tests.Execution;

public sealed class InterpreterControlFlowTests
{
    [Fact]
    public async Task Start_InitFailure_IsReportedAsOneDefinitionDiagnostic()
    {
        var definition = global::OrcaCore.Workflow.Ephemeral<TestState>(DefinitionId.New(), DefinitionVersion.Initial)
            .Init<string>(_ => throw new InvalidOperationException("bad input"))
            .End()
            .Build();
        var engine = new EphemeralWorkflowEngine();
        engine.RegisterDefinition(definition);

        var act = () => engine.StartAsync<string, TestState>(
            definition.DefinitionId,
            "start",
            TestContext.Current.CancellationToken);

        var failure = await act.Should().ThrowAsync<WorkflowDefinitionException>()
            .WithMessage("*Init failed*");
        failure.Which.Diagnostics.Should().ContainSingle()
            .Which.Message.Should().Contain("bad input");
    }

    [Fact]
    public async Task Run_IfConditionTrue_ExecutesThenBranchOnly()
    {
        var state = new TestState { Flag = true };
        var engine = new EphemeralWorkflowEngine();
        var definition = Definition(global::OrcaCore.Workflow.Ephemeral<TestState>(DefinitionId.New(), DefinitionVersion.Initial)
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
        var definition = Definition(global::OrcaCore.Workflow.Ephemeral<TestState>(DefinitionId.New(), DefinitionVersion.Initial)
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
        var definition = Definition(global::OrcaCore.Workflow.Ephemeral<TestState>(DefinitionId.New(), DefinitionVersion.Initial)
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
    public async Task Run_IfWithEmptyThenBranch_ContinuesAfterBranch()
    {
        var state = new TestState { Flag = true };
        var engine = new EphemeralWorkflowEngine();
        var definition = Definition(global::OrcaCore.Workflow.Ephemeral<TestState>(DefinitionId.New(), DefinitionVersion.Initial)
            .Init<string>(_ => state)
            .If(current => current.Flag, _ => { })
            .Then(() => new AppendStep("after"))
            .End());

        engine.RegisterDefinition(definition);

        var snapshot = await engine.StartAsync<string, TestState>(
            definition.DefinitionId,
            "start",
            TestContext.Current.CancellationToken);

        snapshot.Status.Should().Be(WorkflowStatus.Completed);
        state.Values.Should().Equal(["after"]);
    }

    [Fact]
    public async Task Run_WhileConditionTrueThenFalse_ReevaluatesConditionEachIteration()
    {
        var state = new TestState();
        var engine = new EphemeralWorkflowEngine();
        var definition = Definition(global::OrcaCore.Workflow.Ephemeral<TestState>(DefinitionId.New(), DefinitionVersion.Initial)
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
    public async Task Run_WhileInitiallyFalse_SkipsBodyAndContinuesAfterLoop()
    {
        var state = new TestState();
        var engine = new EphemeralWorkflowEngine();
        var definition = Definition(global::OrcaCore.Workflow.Ephemeral<TestState>(DefinitionId.New(), DefinitionVersion.Initial)
            .Init<string>(_ => state)
            .While(_ => false, body => body.Then(() => new IncrementIterationStep()))
            .Then(() => new AppendStep("after"))
            .End());

        engine.RegisterDefinition(definition);

        var snapshot = await engine.StartAsync<string, TestState>(
            definition.DefinitionId,
            "start",
            TestContext.Current.CancellationToken);

        snapshot.Status.Should().Be(WorkflowStatus.Completed);
        state.Iterations.Should().Be(0);
        state.Values.Should().Equal(["after"]);
    }

    [Fact]
    public async Task Run_NestedIfInsideWhile_MaintainsCorrectPosition()
    {
        var state = new TestState();
        var engine = new EphemeralWorkflowEngine();
        var definition = Definition(global::OrcaCore.Workflow.Ephemeral<TestState>(DefinitionId.New(), DefinitionVersion.Initial)
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

    [Fact]
    public async Task Run_ParallelWithOneFailingBranch_FailsWorkflowAndSkipsContinuation()
    {
        var state = new TestState();
        var engine = new EphemeralWorkflowEngine();
        var definition = global::OrcaCore.Workflow.Ephemeral<TestState>(DefinitionId.New(), DefinitionVersion.Initial)
            .Init<string>(_ => state)
            .Parallel<string>(
                branches => branches
                    .Branch<BranchState>(
                        "a",
                        _ => new BranchState("a"),
                        branch => branch.Return(current => current.Value.Value))
                    .Branch<BranchState>(
                        "b",
                        _ => new BranchState("b"),
                        branch => branch
                            .Then<FailingBranchStep>()
                            .Return(current => current.Value.Value)),
                (parent, _) => parent.Value)
            .Then(() => new AppendStep("after"))
            .End()
            .Build();

        engine.RegisterDefinition(definition);

        var snapshot = await engine.StartAsync<string, TestState>(
            definition.DefinitionId,
            "start",
            TestContext.Current.CancellationToken);

        snapshot.Status.Should().Be(WorkflowStatus.Failed);
        snapshot.ErrorSummary.Should().Contain("boom");
        state.Values.Should().NotContain("after");
    }

    [Fact]
    public async Task Run_ParallelFailureAfterAnotherBranchWaits_WaitsForSiblingThenFailsAndReleasesRuntimeWork()
    {
        var state = new TestState();
        var engine = new EphemeralWorkflowEngine();
        var definition = global::OrcaCore.Workflow.Ephemeral<TestState>(DefinitionId.New(), DefinitionVersion.Initial)
            .Init<string>(_ => state)
            .Parallel<string>(
                branches => branches
                    .Branch<BranchState>(
                        "waiting",
                        _ => new BranchState("waiting"),
                        branch => branch
                            .Wait("Ready", _ => CorrelationId.Create("waiting"))
                            .Return(current => current.Value.Value))
                    .Branch<BranchState>(
                        "failing",
                        _ => new BranchState("failing"),
                        branch => branch
                            .Then<FailingBranchStep>()
                            .Return(current => current.Value.Value)),
                (parent, _) => parent.Value)
            .Then(() => new AppendStep("after"))
            .End()
            .Build();
        engine.RegisterDefinition(definition);

        var waiting = await engine.StartAsync<string, TestState>(
            definition.DefinitionId,
            "start",
            TestContext.Current.CancellationToken);
        var snapshot = await engine.RaiseEventAsync<TestState>(
            waiting.InstanceId,
            new EventEnvelope
            {
                EventId = EventId.Create(Guid.CreateVersion7().ToString()),
                EventName = "Ready",
                CorrelationId = CorrelationId.Create("waiting"),
                OccurredAt = DateTimeOffset.UtcNow
            },
            TestContext.Current.CancellationToken);

        waiting.Status.Should().Be(WorkflowStatus.Waiting);
        waiting.ActiveWaits.Should().ContainSingle();
        snapshot.Status.Should().Be(WorkflowStatus.Failed);
        snapshot.ActiveWaits.Should().BeEmpty();
        snapshot.ErrorSummary.Should().Contain("boom");
        state.Values.Should().NotContain("after");
    }

    private static OrcaCore.Core.Definitions.WorkflowDefinition<TestState> Definition(
        EphemeralWorkflowBuilder<TestState> builder)
    {
        return builder.Build();
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

    private sealed class FailingStep : IStep<TestState>
    {
        public ValueTask<StepResult> ExecuteAsync(
            StepContext<TestState> context,
            CancellationToken cancellationToken)
        {
            return ValueTask.FromResult<StepResult>(
                new StepResult.Failed(new WorkflowLifecycleException("boom")));
        }
    }

    private sealed record BranchState(string Value);

    private sealed class FailingBranchStep : IStep<BranchState>
    {
        public ValueTask<StepResult> ExecuteAsync(
            StepContext<BranchState> context,
            CancellationToken cancellationToken)
        {
            return ValueTask.FromResult<StepResult>(
                new StepResult.Failed(new WorkflowLifecycleException("boom")));
        }
    }
}
