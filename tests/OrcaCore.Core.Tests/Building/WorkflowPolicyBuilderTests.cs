using System.Reflection;
using AwesomeAssertions;
using Xunit;

namespace OrcaCore.Core.Tests.Building;

public sealed class WorkflowPolicyBuilderTests
{
    [Fact]
    public void StepPolicy_RetryAndTimeoutDecorateThePrecedingStep()
    {
        var definitionId = DefinitionId.New();
        var configured = Workflow.Ephemeral<PolicyState>(
                definitionId,
                DefinitionVersion.Initial)
            .Init<string>(_ => new PolicyState())
            .Then<NoOpStep>()
            .WithRetry(maxAttempts: 3)
            .WithStepTimeout(TimeSpan.FromSeconds(30))
            .End()
            .Build();
        var baseline = Workflow.Ephemeral<PolicyState>(
                definitionId,
                DefinitionVersion.Initial)
            .Init<string>(_ => new PolicyState())
            .Then<NoOpStep>()
            .End()
            .Build();

        configured.DefinitionFingerprint.Should().NotBe(
            baseline.DefinitionFingerprint);
    }

    [Fact]
    public void DefinitionLevelRetryPolicy_IsNotExposed()
    {
        typeof(EphemeralWorkflowInitBuilder<PolicyState>)
            .GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
            .Select(method => method.Name)
            .Should().NotContain("WithDefinitionRetry");
        typeof(DurableWorkflowInitBuilder<PolicyState>)
            .GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
            .Select(method => method.Name)
            .Should().NotContain("WithDefinitionRetry");
    }

    [Fact]
    public void InvalidRetryPolicy_IsRejectedEagerly()
    {
        var builder = Workflow.Ephemeral<PolicyState>(
                DefinitionId.New(),
                DefinitionVersion.Initial)
            .Init<string>(_ => new PolicyState())
            .Then<NoOpStep>();

        Action invalid = () => builder.WithRetry(maxAttempts: 0);

        invalid.Should().Throw<ArgumentOutOfRangeException>()
            .WithParameterName("maxAttempts");
    }

    private sealed class PolicyState;

    private sealed class NoOpStep : IStep<PolicyState>
    {
        public ValueTask<StepResult> ExecuteAsync(
            StepContext<PolicyState> context,
            CancellationToken cancellationToken) =>
            ValueTask.FromResult<StepResult>(new StepResult.Completed());
    }
}
