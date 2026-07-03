using OrcaCore.Abstractions.Ids;
using OrcaCore.Abstractions.Instances;
using OrcaCore.Abstractions.Steps;
using OrcaCore.Core.Building;
using OrcaCore.Engine.Ephemeral.Execution;
using OrcaCore.TestSupport.Time;

namespace OrcaCore.Engine.Ephemeral.Tests.Execution;

public class InterpreterControlFlowTests
{
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

    private static WorkflowInstance<OrderState> NewInstance(Clock clock) =>
        new(
            InstanceId.New(),
            new DefinitionId("order-workflow"),
            new DefinitionVersion(1),
            new OrderState(),
            clock.Now);

    [Fact]
    public async Task Run_IfConditionTrue_ExecutesThenBranchOnly()
    {
        var builder = WorkflowBuilder<OrderState>.Create<int>(input => new OrderState { Total = input });
        builder.If(
            state => ((OrderState)state!).Total > 0,
            then => then.Then(new RecordingStep("then")),
            @else => @else.Then(new RecordingStep("else")));
        builder.End();
        var definition = builder.Build(new DefinitionId("order-workflow"), new DefinitionVersion(1));

        var clock = new Clock(DateTimeOffset.UnixEpoch);
        var instance = NewInstance(clock);
        instance.State = new OrderState { Total = 5 };

        var interpreter = new Interpreter<OrderState>();
        await interpreter.RunAsync(instance, definition, clock.TimeProvider, TestContext.Current.CancellationToken);

        instance.Status.Should().Be(WorkflowStatus.Completed);
        instance.State.Executed.Should().Equal("then");
    }

    [Fact]
    public async Task Run_IfConditionFalse_ExecutesElseBranchOnly()
    {
        var builder = WorkflowBuilder<OrderState>.Create<int>(input => new OrderState { Total = input });
        builder.If(
            state => ((OrderState)state!).Total > 0,
            then => then.Then(new RecordingStep("then")),
            @else => @else.Then(new RecordingStep("else")));
        builder.End();
        var definition = builder.Build(new DefinitionId("order-workflow"), new DefinitionVersion(1));

        var clock = new Clock(DateTimeOffset.UnixEpoch);
        var instance = NewInstance(clock);
        instance.State = new OrderState { Total = 0 };

        var interpreter = new Interpreter<OrderState>();
        await interpreter.RunAsync(instance, definition, clock.TimeProvider, TestContext.Current.CancellationToken);

        instance.Status.Should().Be(WorkflowStatus.Completed);
        instance.State.Executed.Should().Equal("else");
    }

    [Fact]
    public async Task Run_IfWithoutElse_ContinuesAfterSkippedBranch()
    {
        var builder = WorkflowBuilder<OrderState>.Create<int>(input => new OrderState { Total = input });
        builder.If(
            state => ((OrderState)state!).Total > 0,
            then => then.Then(new RecordingStep("then")));
        builder.Then(new RecordingStep("after"));
        builder.End();
        var definition = builder.Build(new DefinitionId("order-workflow"), new DefinitionVersion(1));

        var clock = new Clock(DateTimeOffset.UnixEpoch);
        var instance = NewInstance(clock);
        instance.State = new OrderState { Total = 0 };

        var interpreter = new Interpreter<OrderState>();
        await interpreter.RunAsync(instance, definition, clock.TimeProvider, TestContext.Current.CancellationToken);

        instance.Status.Should().Be(WorkflowStatus.Completed);
        instance.State.Executed.Should().Equal("after");
    }

    [Fact]
    public async Task Run_WhileConditionTrueThenFalse_ReevaluatesConditionEachIteration()
    {
        var builder = WorkflowBuilder<OrderState>.Create<int>(input => new OrderState { Total = input });
        builder.While(
            state => ((OrderState)state!).Total < 3,
            body => body.Then(new IncrementAndRecordStep()));
        builder.End();
        var definition = builder.Build(new DefinitionId("order-workflow"), new DefinitionVersion(1));

        var clock = new Clock(DateTimeOffset.UnixEpoch);
        var instance = NewInstance(clock);
        instance.State = new OrderState { Total = 0 };

        var interpreter = new Interpreter<OrderState>();
        await interpreter.RunAsync(instance, definition, clock.TimeProvider, TestContext.Current.CancellationToken);

        instance.Status.Should().Be(WorkflowStatus.Completed);
        instance.State.Executed.Should().Equal("iter", "iter", "iter");
        instance.State.Total.Should().Be(3);
    }

    [Fact]
    public async Task Run_NestedIfInsideWhile_MaintainsCorrectPosition()
    {
        var builder = WorkflowBuilder<OrderState>.Create<int>(input => new OrderState { Total = input });
        builder.While(
            state => ((OrderState)state!).Total < 3,
            body => body.If(
                state => ((OrderState)state!).Total % 2 == 0,
                then => then.Then(new RecordingStep("even")),
                @else => @else.Then(new RecordingStep("odd")))
                .Then(new IncrementAndRecordStep()));
        builder.End();
        var definition = builder.Build(new DefinitionId("order-workflow"), new DefinitionVersion(1));

        var clock = new Clock(DateTimeOffset.UnixEpoch);
        var instance = NewInstance(clock);
        instance.State = new OrderState { Total = 0 };

        var interpreter = new Interpreter<OrderState>();
        await interpreter.RunAsync(instance, definition, clock.TimeProvider, TestContext.Current.CancellationToken);

        instance.Status.Should().Be(WorkflowStatus.Completed);
        instance.State.Executed.Should().Equal("even", "iter", "odd", "iter", "even", "iter");
        instance.State.Total.Should().Be(3);
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
}
