using System.Text.RegularExpressions;
using AwesomeAssertions;

namespace OrcaCore.DeveloperSurface.Guards;

[Trait(GuardTraits.Phase, GuardTraits.Phase0)]
[Trait(GuardTraits.Disposition, GuardTraits.Infrastructure)]
public sealed class LeaseDiscoveryAndGovernanceInfrastructureGuards
{
    private const int LeasedRetryFixtureCount = 2;

    [Theory]
    [InlineData("lease-discovery-confirmation-scenarios.json", "3.11c", 10)]
    [InlineData("governance-accounting-scenarios.json", "3.11d", 11)]
    public void ScenarioLedgers_AreCompleteSemanticAndUnique(string fixture, string task, int count)
    {
        var scenarios = FixtureDefinitions.Read<GuardScenario[]>($"tests/OrcaCore.DeveloperSurface.Guards/Fixtures/{fixture}");
        scenarios.Should().HaveCount(count);
        scenarios.Select(x => x.Id).Should().OnlyHaveUniqueItems();
        scenarios.Should().OnlyContain(x => x.TaskId == task &&
            !string.IsNullOrWhiteSpace(x.Setup) && !string.IsNullOrWhiteSpace(x.Assertion) &&
            !string.IsNullOrWhiteSpace(x.ExpectedRed) && !string.IsNullOrWhiteSpace(x.TurnsGreenTask));
    }

    [Fact]
    public void Matrix_AnchorsDiscoveryConfirmationAndAccountingContract()
    {
        var matrix = File.ReadAllText(Path.Combine(FixtureDefinitions.RepositoryRoot(), "docs", "specs",
            "17-selected-mode-capability-matrix.md"));
        foreach (var anchor in new[]
        {
            "IDurableResourceLeaseDiagnostics", "ConfirmationConflict", "AlreadyConfirmed", "NotConfirmable",
            "Released", "TokenNotFound", "WorkflowPendingObligationCommitted", "GovernanceReservationCommitted",
            "WorkflowActivationCommitted", "GovernanceOwnershipConfirmed", "immutable creation metadata", "resize debt",
            "contended conservation/direct transfer", "tombstone"
        }) matrix.Should().Contain(anchor);
        matrix.Should().Contain("authorize").And.Contain("redact");
        matrix.Should().NotContain("RenewLease").And.NotContain("ForceRelease");

        var testRoot = Path.Combine(FixtureDefinitions.RepositoryRoot(), "tests");
        var scenarioSourceRoots = new[]
        {
            "OrcaCore.DeveloperSurface.BehaviorScenarios",
            "OrcaCore.ProviderCertification"
        };
        var schedulerSensitiveSources = scenarioSourceRoots
            .SelectMany(directory => Directory.GetFiles(
                Path.Combine(testRoot, directory),
                "*.cs",
                SearchOption.AllDirectories))
            .Where(path => !IsBuildOutputPath(testRoot, path))
            .Where(path => File.ReadAllText(path).Contains(
                ".WaitAsync(TimeSpan",
                StringComparison.Ordinal))
            .Select(path => Path.GetRelativePath(testRoot, path).Replace('\\', '/'))
            .Order(StringComparer.Ordinal)
            .ToArray();
        schedulerSensitiveSources.Should().BeEmpty(
            "behavior-scenario and provider-certification gates must await workflow-owned signals instead of expiring under wall-clock scheduler pressure");

        var leaseExitSource = File.ReadAllText(Path.Combine(
            testRoot,
            "OrcaCore.DeveloperSurface.BehaviorScenarios",
            "LeaseExitScenarioHost.cs"));
        Regex.Matches(leaseExitSource, @"await[ \t]+gate\.SecondStarted\.Task;")
            .Should().HaveCount(LeasedRetryFixtureCount,
                "both leased retry fixtures must await the second-attempt signal before observing start-task completion");
        leaseExitSource.Should().NotContain(
            "if (!gate.SecondStarted.Task.IsCompleted)",
            "a post-completion snapshot is a scheduler race, not notification-driven evidence");
    }

    private static bool IsBuildOutputPath(string testRoot, string path) =>
        Path.GetRelativePath(testRoot, path)
            .Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
            .Any(segment =>
                string.Equals(segment, "bin", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(segment, "obj", StringComparison.OrdinalIgnoreCase));
}

[Trait(GuardTraits.Phase, GuardTraits.Phase0)]
[Trait(GuardTraits.Disposition, GuardTraits.Infrastructure)]
public sealed class LeaseDiscoveryAndGovernanceProductGuards
{
    [Fact]
    public void Product_ContainsFinalDiagnosticsRecoveryAndGovernanceStore()
    {
        var publicTypes = PublicSurfaceCatalog.Assemblies.SelectMany(x => x.GetExportedTypes()).ToArray();
        publicTypes.Should().Contain(x => x.Name == "IDurableResourceLeaseDiagnostics");
        publicTypes.Should().Contain(x => x.Name == "IDurableResourceLeaseRecovery");
        publicTypes.Should().Contain(x => x.Name == "IDurableResourceGovernanceStore");
    }

    [Fact]
    public void Product_ContainsExactFriendBarrierFacts()
    {
        var source = Directory.GetFiles(Path.Combine(FixtureDefinitions.RepositoryRoot(), "src"), "*.cs", SearchOption.AllDirectories)
            .SelectMany(File.ReadLines).ToArray();
        foreach (var barrier in new[]
        {
            "WorkflowPendingObligationCommitted", "GovernanceReservationCommitted",
            "WorkflowActivationCommitted", "GovernanceOwnershipConfirmed"
        }) source.Should().Contain(line => line.Contains(barrier, StringComparison.Ordinal));
    }
}
