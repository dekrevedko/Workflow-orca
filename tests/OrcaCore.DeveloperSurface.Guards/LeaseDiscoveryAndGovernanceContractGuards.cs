using AwesomeAssertions;

namespace OrcaCore.DeveloperSurface.Guards;

[Trait(GuardTraits.Phase, GuardTraits.Phase0)]
[Trait(GuardTraits.Disposition, GuardTraits.Infrastructure)]
public sealed class LeaseDiscoveryAndGovernanceInfrastructureGuards
{
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
    }
}

[Trait(GuardTraits.Phase, GuardTraits.Phase0)]
[Trait(GuardTraits.Disposition, GuardTraits.ExpectedRed)]
public sealed class LeaseDiscoveryAndGovernanceExpectedRedGuards
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
