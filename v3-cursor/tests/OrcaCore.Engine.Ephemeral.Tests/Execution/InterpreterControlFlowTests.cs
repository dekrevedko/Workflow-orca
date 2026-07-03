using AwesomeAssertions;
using OrcaCore.Abstractions.Ids;
using OrcaCore.Abstractions.Instances;
using OrcaCore.Abstractions.Steps;
using OrcaCore.Core.Building;
using OrcaCore.Engine.Ephemeral.Execution;
using OrcaCore.TestSupport;

namespace OrcaCore.Engine.Ephemeral.Tests.Execution;

public sealed class InterpreterControlFlowTests
{
    private static readonly DefinitionId DefinitionId = new(Guid.Parse("22222222-2222-7222-8222-222222222222"));
    private static readonly DefinitionVersion Version = new(1);

    private sealed class OrderState
    {
        public int Total { get; set; }
        public List<string> Executed { get; } = [];
    }

    private sealed class RecordingStep(string tag) : IStep<OrderState>
    {
        public ValueTask<StepResult> ExecuteAsync(StepContext<OrderState> context, CancellationToken cancellationToken)
        {
            context.State.Executed.Add(tag);
            return ValueTask.FromResult<StepResult>(new StepResult.Completed());
        }
    }

    private sealed class IncrementAndRecordStep : IStep<OrderState>
    {
        public ValueTask<StepResult> ExecuteAsync(StepContext<OrderState> context, CancellationToken cancellationToken)
        {
            context.State.Total += 1;
            context.State.Executed.Add("iter");
            return ValueTask.FromResult<StepResult>(new StepResult.Completed());
        }
    }

    private static Interpreter<OrderState> CreateInterpreter(Clock clock) => new(clock.Provider);

    [Fact]
    public async Task Run_IfConditionTrue_ExecutesThenBranchOnly()
    {
        var clock = new Clock(DateTimeOffset.UnixEpoch);
        var definition = WorkflowBuilder<OrderState>.Create<int>(input => new OrderState { Total = input })
            .If(
                state => ((OrderState)state!).Total > 0,
                then => then.Then(new RecordingStep("then")),
                @else => @else.Then(new RecordingStep("else")))
            .End()
            .Build(DefinitionId, Version);

        var instance = await CreateInterpreter(clock).RunAsync(definition, 5, InstanceId.New(), TestContext.Current.CancellationToken);

        instance.Status.Should().Be(WorkflowStatus.Completed);
        instance.State.Executed.Should().Equal("then");
    }

    [Fact]
    public async Task Run_IfConditionFalse_ExecutesElseBranchOnly()
    {
        var clock = new Clock(DateTimeOffset.UnixEpoch);
        var definition = WorkflowBuilder<OrderState>.Create<int>(input => new OrderState { Total = input })
            .If(
                state => ((OrderState)state!).Total > 0,
                then => then.Then(new RecordingStep("then")),
                @else => @else.Then(new RecordingStep("else")))
            .End()
            .Build(DefinitionId, Version);

        var instance = await CreateInterpreter(clock).RunAsync(definition, 0, InstanceId.New(), TestContext.Current.CancellationToken);

        instance.Status.Should().Be(WorkflowStatus.Completed);
        instance.State.Executed.Should().Equal("else");
    }

    [Fact]
    public async Task Run_IfWithoutElse_ContinuesAfterSkippedBranch()
    {
        var clock = new Clock(DateTimeOffset.UnixEpoch);
        var definition = WorkflowBuilder<OrderState>.Create<int>(input => new OrderState { Total = input })
            .If(
                state => ((OrderState)state!).Total > 0,
                then => then.Then(new RecordingStep("then")))
            .Then(new RecordingStep("after"))
            .End()
            .Build(DefinitionId, Version);

        var instance = await CreateInterpreter(clock).RunAsync(definition, 0, InstanceId.New(), TestContext.Current.CancellationToken);

        instance.Status.Should().Be(WorkflowStatus.Completed);
        instance.State.Executed.Should().Equal("after");
    }

    [Fact]
    public async Task Run_WhileConditionTrueThenFalse_ReevaluatesConditionEachIteration()
    {
        var clock = new Clock(DateTimeOffset.UnixEpoch);
        var definition = WorkflowBuilder<OrderState>.Create<int>(input => new OrderState { Total = input })
            .While(
                state => ((OrderState)state!).Total < 3,
                body => body.Then(new IncrementAndRecordStep()))
            .End()
            .Build(DefinitionId, Version);

        var instance = await CreateInterpreter(clock).RunAsync(definition, 0, InstanceId.New(), TestContext.Current.CancellationToken);

        instance.Status.Should().Be(WorkflowStatus.Completed);
        instance.State.Executed.Should().Equal("iter", "iter", "iter");
        instance.State.Total.Should().Be(3);
    }

    [Fact]
    public async Task Run_NestedIfInsideWhile_MaintainsCorrectPosition()
    {
        var clock = new Clock(DateTimeOffset.UnixEpoch);
        var definition = WorkflowBuilder<OrderState>.Create<int>(input => new OrderState { Total = input })
            .While(
                state => ((OrderState)state!).Total < 3,
                body => body
                    .If(
                        state => ((OrderState)state!).Total % 2 == 0,
                        then => then.Then(new RecordingStep("even")),
                        @else => @else.Then(new RecordingStep("odd")))
                    .Then(new IncrementAndRecordStep()))
            .End()
            .Build(DefinitionId, Version);

        var instance = await CreateInterpreter(clock).RunAsync(definition, 0, InstanceId.New(), TestContext.Current.CancellationToken);

        instance.Status.Should().Be(WorkflowStatus.Completed);
        instance.State.Executed.Should().Equal("even", "iter", "odd", "iter", "even", "iter");
        instance.State.Total.Should().Be(3);
    }
}
