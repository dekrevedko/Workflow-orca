using AwesomeAssertions;
using OrcaCore.Abstractions.Ids;
using OrcaCore.Core.Building;
using OrcaCore.Core.Compilation;
using Xunit;

namespace OrcaCore.Acceptance.Tests;

public sealed class BuilderAcceptanceTests
{
    [Fact]
    [Trait("AC", "AC-008")]
    public void Build_AccumulatesAllValidationErrors()
    {
        var validation = Workflow.Ephemeral<BuilderState>(DefinitionId.New(), DefinitionVersion.Initial)
            .WithCompilerOptions(new DefinitionCompilerOptions
            {
                MaxInternalInstructionsPerQuantum = 0,
                MaxScopeDepth = 0,
                MaxActiveFibers = 0
            })
            .End()
            .TryBuild();

        validation.IsValid.Should().BeFalse();
        validation.Errors.Select(error => error.Code).Should().Equal(
            [
                DefinitionCompilerCodes.MaxInternalInstructionsNotPositive,
                DefinitionCompilerCodes.MaxScopeDepthNotPositive,
                DefinitionCompilerCodes.MaxActiveFibersNotPositive,
                DefinitionCompilerCodes.MissingRootInit
            ]);
    }

    private sealed record BuilderState;
}
