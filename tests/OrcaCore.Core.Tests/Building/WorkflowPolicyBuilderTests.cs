using AwesomeAssertions;
using OrcaCore.Abstractions.Ids;
using OrcaCore.Abstractions.Steps;
using OrcaCore.Core.Building;
using OrcaCore.Core.Definitions;
using Xunit;

namespace OrcaCore.Core.Tests.Building;

public sealed class WorkflowPolicyBuilderTests
{
    [Fact]
    public void StepPolicy_WithRetryAndTimeout_AttachesMetadataToNextStep()
    {
        var timeout = TimeSpan.FromSeconds(30);

        var definition = new WorkflowBuilder<PolicyState>()
            .Init<string>(_ => new PolicyState())
            .WithRetry(maxAttempts: 3)
            .WithTimeout(timeout)
            .Then<NoOpStep>()
            .End()
            .Build(DefinitionId.New(), DefinitionVersion.Initial);

        var step = definition.RootSequence.Children.OfType<BusinessStepNode<PolicyState>>().Single();
        step.Policies.Retry.Should().NotBeNull();
        step.Policies.Retry!.MaxAttempts.Should().Be(3);
        step.Policies.Timeout.Should().NotBeNull();
        step.Policies.Timeout!.Duration.Should().Be(timeout);
    }

    [Fact]
    public void DefinitionPolicy_AppliesToRootMetadata()
    {
        var definition = new WorkflowBuilder<PolicyState>()
            .WithDefinitionRetry(maxAttempts: 2)
            .Init<string>(_ => new PolicyState())
            .End()
            .Build(DefinitionId.New(), DefinitionVersion.Initial);

        definition.Policies.Retry.Should().NotBeNull();
        definition.Policies.Retry!.MaxAttempts.Should().Be(2);
    }

    [Fact]
    public void InvalidRetryPolicy_ReportsValidationError()
    {
        var validation = new WorkflowBuilder<PolicyState>()
            .Init<string>(_ => new PolicyState())
            .WithRetry(maxAttempts: 0)
            .Then<NoOpStep>()
            .End()
            .BuildValidated(DefinitionId.New(), DefinitionVersion.Initial);

        validation.IsValid.Should().BeFalse();
        validation.Errors.Should().ContainSingle(error => error.Code == BuilderValidationCodes.InvalidRetryPolicy);
    }

    private sealed class PolicyState;

    private sealed class NoOpStep : IStep<PolicyState>
    {
        public ValueTask<StepResult> ExecuteAsync(
            StepContext<PolicyState> context,
            CancellationToken cancellationToken)
        {
            return ValueTask.FromResult<StepResult>(new StepResult.Completed());
        }
    }
}
