using AwesomeAssertions;

namespace OrcaCore.DeveloperSurface.Guards;

[Trait(GuardTraits.Phase, GuardTraits.Phase0)]
[Trait(GuardTraits.Disposition, GuardTraits.Infrastructure)]
public sealed class LeaseAuthoringAndExitInfrastructureGuards
{
    [Theory]
    [InlineData("lease-authoring-admission-scenarios.json", "3.11a", 10)]
    [InlineData("lease-retry-exit-scenarios.json", "3.11b", 8)]
    public void LeaseScenarioLedgers_AreCompleteSemanticAndUnique(string fixture, string task, int count)
    {
        var scenarios = FixtureDefinitions.Read<GuardScenario[]>($"tests/OrcaCore.DeveloperSurface.Guards/Fixtures/{fixture}");
        scenarios.Should().HaveCount(count);
        scenarios.Select(x => x.Id).Should().OnlyHaveUniqueItems();
        scenarios.Should().OnlyContain(x => x.TaskId == task &&
            !string.IsNullOrWhiteSpace(x.Setup) && !string.IsNullOrWhiteSpace(x.Assertion) &&
            !string.IsNullOrWhiteSpace(x.ExpectedRed) && !string.IsNullOrWhiteSpace(x.TurnsGreenTask));
    }

    [Fact]
    public void Matrix_AnchorsScopedAdmissionRetryExitAndQuarantineRules()
    {
        var matrix = File.ReadAllText(Path.Combine(FixtureDefinitions.RepositoryRoot(), "docs", "specs",
            "17-selected-mode-capability-matrix.md"));
        foreach (var anchor in new[]
        {
            "AcquireResources", "ResourcePoolNotConfiguredException", "CancelledBeforeGrant",
            "only the requesting fiber parks", "SFE-RUN-001", "SFE-RUN-002", "PendingCommit", "ReviewMarked",
            "AmbiguousHeld", "Quarantined", "no overlapping in-process", "before branch/item failure"
        }) matrix.Should().Contain(anchor);
        matrix.Should().NotContain("AcquireLease(");
    }
}

[Trait(GuardTraits.Phase, GuardTraits.Phase0)]
[Trait(GuardTraits.Disposition, GuardTraits.Infrastructure)]
public sealed class LeaseAuthoringAndExitProductGuards
{
    [Fact]
    public void Product_ContainsScopedAcquireResourcesAndFinalLifecycle()
    {
        var source = Directory.GetFiles(Path.Combine(FixtureDefinitions.RepositoryRoot(), "src"), "*.cs", SearchOption.AllDirectories)
            .SelectMany(File.ReadLines).ToArray();
        source.Should().Contain(line => line.Contains("SFE-RUN-002", StringComparison.Ordinal));
        source.Should().Contain(line => line.Contains("AmbiguousHeld", StringComparison.Ordinal));
        PublicSurfaceCatalog.Assemblies.SelectMany(x => x.GetExportedTypes())
            .Should().Contain(x => x.FullName == "OrcaCore.ResourceLeaseRequest");
    }
}
