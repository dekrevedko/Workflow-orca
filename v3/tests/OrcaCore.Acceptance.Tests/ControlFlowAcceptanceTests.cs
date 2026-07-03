using OrcaCore.Abstractions.Ids;
using OrcaCore.Abstractions.Instances;
using OrcaCore.Abstractions.Steps;
using OrcaCore.Core.Building;
using OrcaCore.Engine.Ephemeral;

namespace OrcaCore.Acceptance.Tests;

public class ControlFlowAcceptanceTests
{
    private sealed class OrderState
    {
        public int Total { get; set; }
    }

    private sealed class RecordToSinkStep(List<string> sink, string tag) : IStep<OrderState>
    {
        public ValueTask<StepResult> ExecuteAsync(StepContext<OrderState> context, CancellationToken cancellationToken)
        {
            sink.Add(tag);
            return ValueTask.FromResult<StepResult>(new StepResult.Completed());
        }
    }

    private sealed class IncrementAndRecordStep(List<int> sink) : IStep<OrderState>
    {
        public ValueTask<StepResult> ExecuteAsync(StepContext<OrderState> context, CancellationToken cancellationToken)
        {
            context.State.Total += 1;
            sink.Add(context.State.Total);
            return ValueTask.FromResult<StepResult>(new StepResult.Completed());
        }
    }

    [Trait("AC", "AC-002")]
    [Fact]
    public async Task If_ExecutesExactlyOneBranch_ThenContinues()
    {
        var sink = new List<string>();
        var builder = WorkflowBuilder<OrderState>.Create<int>(input => new OrderState { Total = input });
        builder.If(
            state => ((OrderState)state!).Total > 0,
            then => then.Then(new RecordToSinkStep(sink, "then")),
            @else => @else.Then(new RecordToSinkStep(sink, "else")));
        builder.Then(new RecordToSinkStep(sink, "after"));
        builder.End("Completed");
        var definition = builder.Build(new DefinitionId("if-workflow"), new DefinitionVersion(1));

        var engine = new EphemeralWorkflowEngine();
        engine.RegisterDefinition(definition);

        var snapshot = await engine.StartAsync<int, OrderState>(new DefinitionId("if-workflow"), 10, TestContext.Current.CancellationToken);

        snapshot.Status.Should().Be(WorkflowStatus.Completed);
        sink.Should().Equal("then", "after");
    }

    [Trait("AC", "AC-003")]
    [Fact]
    public async Task While_RunsThreeIterations_CompletesAfterFourthCheck()
    {
        var sink = new List<int>();
        var builder = WorkflowBuilder<OrderState>.Create<int>(input => new OrderState { Total = input });
        builder.While(
            state => ((OrderState)state!).Total < 3,
            body => body.Then(new IncrementAndRecordStep(sink)));
        builder.End("Completed");
        var definition = builder.Build(new DefinitionId("while-workflow"), new DefinitionVersion(1));

        var engine = new EphemeralWorkflowEngine();
        engine.RegisterDefinition(definition);

        var snapshot = await engine.StartAsync<int, OrderState>(new DefinitionId("while-workflow"), 0, TestContext.Current.CancellationToken);

        snapshot.Status.Should().Be(WorkflowStatus.Completed);
        sink.Should().Equal(1, 2, 3);
    }
}
