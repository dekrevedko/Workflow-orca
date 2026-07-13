using AwesomeAssertions;
using OrcaCore.Abstractions.Ids;
using OrcaCore.Core.Building;
using Xunit;

namespace OrcaCore.Acceptance.Tests;

public sealed class BuilderAcceptanceTests
{
    [Fact]
    [Trait("AC", "AC-008")]
    public void Build_AccumulatesAllValidationErrors()
    {
        var validation = new WorkflowBuilder<BuilderState>()
            .If(null!, then => then.Parallel(("empty", _ => { })))
            .End()
            .BuildValidated(DefinitionId.New(), DefinitionVersion.Initial);

        validation.IsValid.Should().BeFalse();
        validation.Errors.Select(error => error.Code).Should().Equal(
            [
                BuilderValidationCodes.MissingInit,
                BuilderValidationCodes.NullDelegate,
                BuilderValidationCodes.EmptyBranch
            ]);
    }

    private sealed record BuilderState;
}
