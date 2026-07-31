using AwesomeAssertions;
using OrcaCore.Abstractions.Ids;
using OrcaCore.Core.Building;
using Xunit;

namespace OrcaCore.Acceptance.Tests;

public sealed class BuilderAcceptanceTests
{
    [Fact]
    [Trait("AC", "AC-008")]
    public void Build_ReportsInspectableGraphErrorsThroughThePublicStagedContract()
    {
        var completion = Workflow.Ephemeral<BuilderState>(DefinitionId.New(), DefinitionVersion.Initial)
            .Init<string>(_ => new BuilderState())
            .Parallel<int>(_ => { })
            .WhenAll((snapshot, _) => snapshot.Value)
            .End()
            ;

        var validation = completion.TryBuild();

        validation.IsValid.Should().BeFalse();
        validation.Diagnostics.Should().ContainSingle(error => error.Code == "SFE-AUTH-BRANCH-004");
    }

    private sealed record BuilderState;
}
