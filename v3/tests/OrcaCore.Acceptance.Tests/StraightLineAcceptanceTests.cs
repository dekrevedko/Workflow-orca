using OrcaCore.Abstractions.Errors;
using OrcaCore.Abstractions.Ids;
using OrcaCore.Abstractions.Instances;
using OrcaCore.Abstractions.Steps;
using OrcaCore.Core.Building;
using OrcaCore.Engine.Ephemeral;

namespace OrcaCore.Acceptance.Tests;

public class StraightLineAcceptanceTests
{
    private sealed class OrderState
    {
        public int Total { get; set; }
    }

    private sealed class RecordToSinkStep(List<int> sink) : IStep<OrderState>
    {
        public ValueTask<StepResult> ExecuteAsync(StepContext<OrderState> context, CancellationToken cancellationToken)
        {
            sink.Add(context.State.Total);
            context.State.Total += 1;
            return ValueTask.FromResult<StepResult>(new StepResult.Completed());
        }
    }

    private sealed class FailingStep : IStep<OrderState>
    {
        public ValueTask<StepResult> ExecuteAsync(StepContext<OrderState> context, CancellationToken cancellationToken) =>
            ValueTask.FromResult<StepResult>(new StepResult.Failed(new OrcaCoreException("order total rejected")));
    }

    [Trait("AC", "AC-001")]
    [Fact]
    public async Task StraightLine_Completes_StateReflectsSteps()
    {
        var sink = new List<int>();
        var builder = WorkflowBuilder<OrderState>.Create<int>(input => new OrderState { Total = input });
        builder.Then(new RecordToSinkStep(sink));
        builder.Then(new RecordToSinkStep(sink));
        builder.End("Completed");
        var definition = builder.Build(new DefinitionId("straight-line"), new DefinitionVersion(1));

        var engine = new EphemeralWorkflowEngine();
        engine.RegisterDefinition(definition);

        var snapshot = await engine.StartAsync<int, OrderState>(new DefinitionId("straight-line"), 10, TestContext.Current.CancellationToken);

        snapshot.Status.Should().Be(WorkflowStatus.Completed);
        snapshot.EndOutcomeName.Should().Be("Completed");
        sink.Should().Equal(10, 11);
    }

    [Trait("AC", "AC-004")]
    [Fact]
    public async Task FailingStep_FailsInstance_ErrorInspectable()
    {
        var builder = WorkflowBuilder<OrderState>.Create<int>(input => new OrderState { Total = input });
        builder.Then(new FailingStep());
        builder.End();
        var definition = builder.Build(new DefinitionId("failing-line"), new DefinitionVersion(1));

        var engine = new EphemeralWorkflowEngine();
        engine.RegisterDefinition(definition);

        var snapshot = await engine.StartAsync<int, OrderState>(new DefinitionId("failing-line"), 1, TestContext.Current.CancellationToken);

        snapshot.Status.Should().Be(WorkflowStatus.Failed);
        snapshot.ErrorSummary.Should().NotBeNull();
        snapshot.ErrorSummary!.Should().Contain("order total rejected");
    }
}
