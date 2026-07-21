using AwesomeAssertions;

namespace OrcaCore.DeveloperSurface.Guards;

[Trait(GuardTraits.Phase, GuardTraits.Phase0)]
[Trait(GuardTraits.Disposition, GuardTraits.Infrastructure)]
public sealed class DeadlineRetryInfrastructureGuards
{
    [Fact]
    public void ScenarioLedger_CoversEveryDeadlineRetryAndCoordinateSchedule()
    {
        var scenarios = FixtureDefinitions.Read<GuardScenario[]>(
            "tests/OrcaCore.DeveloperSurface.Guards/Fixtures/deadline-retry-scenarios.json");
        scenarios.Should().HaveCount(10);
        scenarios.Select(x => x.Id).Should().OnlyHaveUniqueItems();
        scenarios.Should().OnlyContain(x => x.TaskId == "3.9" &&
            !string.IsNullOrWhiteSpace(x.Setup) && !string.IsNullOrWhiteSpace(x.Assertion) &&
            !string.IsNullOrWhiteSpace(x.ExpectedRed) && !string.IsNullOrWhiteSpace(x.TurnsGreenTask));
    }

    [Fact]
    public void Matrix_AnchorsThePersistedAttemptCoordinateAndDeadlineRules()
    {
        var matrix = File.ReadAllText(Path.Combine(FixtureDefinitions.RepositoryRoot(), "docs", "specs",
            "17-selected-mode-capability-matrix.md"));
        foreach (var anchor in new[]
        {
            "SFE-AUTH-DEADLINE-001", "token-ignoring late attempt", "StepOperationId", "AttemptNumber",
            "in-flight dispatch marker before dispatch", "maxAttempts == 1", "create-or-observe at most one logical effect",
            "distinct across loop", "competing drivers"
        }) matrix.Should().Contain(anchor);
    }
}

[Trait(GuardTraits.Phase, GuardTraits.Phase0)]
[Trait(GuardTraits.Disposition, GuardTraits.ExpectedRed)]
public sealed class DeadlineRetryExpectedRedGuards
{
    [Fact]
    public void Product_ContainsFinalDeadlineDiagnosticAndOperationCoordinate()
    {
        var source = Directory.GetFiles(Path.Combine(FixtureDefinitions.RepositoryRoot(), "src"), "*.cs", SearchOption.AllDirectories)
            .SelectMany(File.ReadLines).ToArray();
        source.Should().Contain(line => line.Contains("SFE-AUTH-DEADLINE-001", StringComparison.Ordinal));
        PublicSurfaceCatalog.Assemblies.SelectMany(x => x.GetExportedTypes())
            .Should().Contain(x => x.FullName == "OrcaCore.StepOperationId");
    }
}
