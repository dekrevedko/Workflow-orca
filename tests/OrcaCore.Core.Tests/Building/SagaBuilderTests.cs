using AwesomeAssertions;
using OrcaCore.Abstractions.Ids;
using OrcaCore.Core.Building;
using Xunit;

namespace OrcaCore.Core.Tests.Building;

public sealed class SagaBuilderTests
{
    [Fact]
    [Trait("AC", "AC-401")]
    public void SagaBuilder_ForwardStepWithCompensation_BuildsSeparateSagaDefinition()
    {
        var definition = new SagaBuilder<TestState>()
            .Init<string>(_ => new TestState())
            .Then<ReserveInventoryStep>()
            .CompensateBy<ReleaseInventoryStep>()
            .End()
            .Build(DefinitionId.New(), DefinitionVersion.Initial);

        definition.ForwardActions.Should().ContainSingle()
            .Which.Compensation.Should().NotBeNull();
        definition.ForwardActions.Single().StepType.Should().Be(typeof(ReserveInventoryStep));
        definition.ForwardActions.Single().Compensation!.StepType.Should().Be(typeof(ReleaseInventoryStep));
    }

    [Fact]
    [Trait("AC", "AC-402")]
    public void SagaBuilder_CompensationScope_TracksEligibleForwardActions()
    {
        var definition = new SagaBuilder<TestState>()
            .Init<string>(_ => new TestState())
            .CompensationScope("checkout", scope => scope
                .Then<ReserveInventoryStep>()
                .CompensateBy<ReleaseInventoryStep>()
                .Then<AuthorizePaymentStep>()
                .CompensateBy<RefundPaymentStep>())
            .End()
            .Build(DefinitionId.New(), DefinitionVersion.Initial);

        var scope = definition.CompensationScopes.Should().ContainSingle().Subject;
        scope.ScopeId.Should().Be("checkout");
        scope.ForwardActionKeys.Should().Equal(
            definition.ForwardActions.Select(action => action.ActionKey));
    }

    private sealed record TestState;

    private sealed class ReserveInventoryStep : CompletedStep;

    private sealed class ReleaseInventoryStep : CompletedStep;

    private sealed class AuthorizePaymentStep : CompletedStep;

    private sealed class RefundPaymentStep : CompletedStep;

    private abstract class CompletedStep : IStep<TestState>
    {
        public ValueTask<StepResult> ExecuteAsync(
            StepContext<TestState> context,
            CancellationToken cancellationToken)
        {
            return ValueTask.FromResult<StepResult>(new StepResult.Completed());
        }
    }
}
