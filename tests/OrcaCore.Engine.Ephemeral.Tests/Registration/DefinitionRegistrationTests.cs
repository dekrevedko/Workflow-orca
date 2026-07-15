using AwesomeAssertions;
using OrcaCore.Abstractions.Errors;
using OrcaCore.Abstractions.Ids;
using OrcaCore.Core.Building;
using OrcaCore.Core.Definitions;
using Xunit;

namespace OrcaCore.Engine.Ephemeral.Tests.Registration;

public sealed class DefinitionRegistrationTests
{
    [Fact]
    public async Task SameVersionFingerprintDrift_IsRejected_AndOriginalDefinitionRemainsActive()
    {
        var engine = new EphemeralWorkflowEngine();
        var definitionId = DefinitionId.New();
        var first = Definition(definitionId, "first");
        var drifted = Definition(definitionId, "second");
        engine.RegisterDefinition(first);

        var register = () => engine.RegisterDefinition(drifted);

        register.Should().Throw<WorkflowDefinitionException>()
            .Which.Message.Should().Contain("fingerprint");
        var completed = await engine.AwaitCompletionAsync<string, RegistrationState>(
            definitionId,
            "input",
            TestContext.Current.CancellationToken);
        completed.EndOutcomeName.Should().Be("first");
    }

    private static WorkflowDefinition<RegistrationState> Definition(
        DefinitionId definitionId,
        string outcome)
    {
        return Workflow.Ephemeral<RegistrationState>(definitionId, DefinitionVersion.Initial)
            .Init<string>(input => new RegistrationState(input))
            .End(outcome)
            .Build();
    }

    private sealed record RegistrationState(string Value);
}
