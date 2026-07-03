using AwesomeAssertions;
using OrcaCore.Abstractions.Errors;
using OrcaCore.Abstractions.Ids;
using OrcaCore.Abstractions.Instances;
using OrcaCore.Abstractions.Steps;
using OrcaCore.Core.Building;
using OrcaCore.Engine.Ephemeral;
using OrcaCore.TestSupport;

namespace OrcaCore.Acceptance.Tests;

public sealed class StraightLineAcceptanceTests
{
    private sealed class OrderState
    {
        public int Total { get; set; }
    }

    /// <summary>
    /// Records the mutated state into a test-owned sink at execution time. `GetState` (a
    /// public, internal-free typed accessor) arrives in T1-13; until then this is how an
    /// acceptance test observes that business-state mutations actually happened.
    /// </summary>
    private sealed class ChargeStep(List<int> sink) : IStep<OrderState>
    {
        public ValueTask<StepResult> ExecuteAsync(StepContext<OrderState> context, CancellationToken cancellationToken)
        {
            context.State.Total += 5;
            sink.Add(context.State.Total);
            return ValueTask.FromResult<StepResult>(new StepResult.Completed());
        }
    }

    private sealed class DecliningPaymentStep : IStep<OrderState>
    {
        public ValueTask<StepResult> ExecuteAsync(StepContext<OrderState> context, CancellationToken cancellationToken) =>
            ValueTask.FromResult<StepResult>(new StepResult.Failed(new OrcaCoreException("card declined")));
    }

    [Trait("AC", "AC-001")]
    [Fact]
    public async Task StraightLine_Completes_StateReflectsSteps()
    {
        var sink = new List<int>();
        var definitionId = DefinitionId.New();
        var definition = WorkflowBuilder<OrderState>.Create<int>(input => new OrderState { Total = input })
            .Then(new ChargeStep(sink))
            .End("Approved")
            .Build(definitionId, new DefinitionVersion(1));

        var engine = new EphemeralWorkflowEngine(new Clock(DateTimeOffset.UnixEpoch).Provider);
        engine.RegisterDefinition(definition);

        var snapshot = await engine.StartAsync<int, OrderState>(definitionId, 10, CancellationToken.None);

        snapshot.Status.Should().Be(WorkflowStatus.Completed);
        snapshot.EndOutcomeName.Should().Be("Approved");
        sink.Should().Equal(15);
    }

    [Trait("AC", "AC-004")]
    [Fact]
    public async Task FailingStep_FailsInstance_ErrorInspectable()
    {
        var definitionId = DefinitionId.New();
        var definition = WorkflowBuilder<OrderState>.Create<int>(input => new OrderState { Total = input })
            .Then(new DecliningPaymentStep())
            .End()
            .Build(definitionId, new DefinitionVersion(1));

        var engine = new EphemeralWorkflowEngine(new Clock(DateTimeOffset.UnixEpoch).Provider);
        engine.RegisterDefinition(definition);

        var snapshot = await engine.StartAsync<int, OrderState>(definitionId, 10, CancellationToken.None);

        snapshot.Status.Should().Be(WorkflowStatus.Failed);
        snapshot.ErrorSummary.Should().NotBeNullOrWhiteSpace();
        snapshot.ErrorSummary.Should().Contain("card declined");
    }
}
