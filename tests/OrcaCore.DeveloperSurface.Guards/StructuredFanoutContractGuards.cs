using AwesomeAssertions;

namespace OrcaCore.DeveloperSurface.Guards;

[Trait(GuardTraits.Phase, GuardTraits.Phase0)]
[Trait(GuardTraits.Disposition, GuardTraits.Infrastructure)]
public sealed class StructuredFanoutInfrastructureGuards
{
    [Fact]
    public void ScenarioLedger_CoversExactJoinReplayAndAdmissionSchedules()
    {
        var scenarios = FixtureDefinitions.Read<GuardScenario[]>(
            "tests/OrcaCore.DeveloperSurface.Guards/Fixtures/structured-fanout-scenarios.json");
        scenarios.Select(x => x.Id).Should().BeEquivalentTo(
            "empty-parallel-diagnostic-parity", "success-failure-only-outcomes", "ordered-join-failure",
            "ancestor-terminal-suppresses-merge", "empty-foreach-valid", "foreach-bound-and-snapshot-replay",
            "parent-release-child-queue-merge-reacquire", "foreach-lower-limit-and-admitted-slots",
            "restart-readmits-unfinished-items");
        scenarios.Select(x => x.Id).Should().OnlyHaveUniqueItems();
        scenarios.Should().OnlyContain(x => x.TaskId == "3.6" &&
            !string.IsNullOrWhiteSpace(x.Setup) && !string.IsNullOrWhiteSpace(x.Assertion) &&
            !string.IsNullOrWhiteSpace(x.ExpectedRed) && !string.IsNullOrWhiteSpace(x.TurnsGreenTask));
    }

    [Fact]
    public void Matrix_ContainsEveryStructuredFanoutInvariant()
    {
        var matrix = File.ReadAllText(Path.Combine(FixtureDefinitions.RepositoryRoot(), "docs", "specs",
            "17-selected-mode-capability-matrix.md"));
        foreach (var anchor in new[]
        {
            "SFE-AUTH-BRANCH-004", "SFE-JOIN-FAILED", "An empty snapshot is valid", "authored branch/item-index order",
            "suppresses branch/item merges", "releases that token when it parks", "reacquires one only for the merge/continuation",
            "ceiling of one", "admitted nonterminal item scopes"
        }) matrix.Should().Contain(anchor);
    }
}
