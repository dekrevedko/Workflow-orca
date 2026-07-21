using AwesomeAssertions;

namespace OrcaCore.DeveloperSurface.Guards;

[Trait(GuardTraits.Phase, GuardTraits.Phase0)]
[Trait(GuardTraits.Disposition, GuardTraits.Infrastructure)]
public sealed class FacadeHostingInfrastructureGuards
{
    [Fact]
    public void ScenarioLedger_CoversRegistryHandlesEventsHostingAndAbsences()
    {
        var scenarios = FixtureDefinitions.Read<GuardScenario[]>(
            "tests/OrcaCore.DeveloperSurface.Guards/Fixtures/facade-hosting-scenarios.json");
        scenarios.Select(x => x.Id).Should().BeEquivalentTo(
            "four-typed-registration-handles", "compatibility-order-and-copy", "get-handle-or-throw-parity",
            "typed-instance-output-wait", "reduced-snapshot-management", "two-event-routes-four-overloads",
            "six-hosting-entry-owners", "role-exclusivity-and-dependencies",
            "programmatic-options-copy-validation", "exact-type-throttle", "transient-decorator-binding");
        scenarios.Select(x => x.Id).Should().OnlyHaveUniqueItems();
        scenarios.Should().OnlyContain(x => x.TaskId == "3.7" &&
            !string.IsNullOrWhiteSpace(x.Setup) && !string.IsNullOrWhiteSpace(x.Assertion) &&
            !string.IsNullOrWhiteSpace(x.ExpectedRed) && !string.IsNullOrWhiteSpace(x.TurnsGreenTask));
    }

    [Fact]
    public void Matrix_AnchorsExactRegistryEventAndSixHostingEntries()
    {
        var matrix = File.ReadAllText(Path.Combine(FixtureDefinitions.RepositoryRoot(), "docs", "specs",
            "17-selected-mode-capability-matrix.md"));
        foreach (var anchor in new[]
        {
            "IWorkflowDefinitionRegistry", "WorkflowRegistrationResult", "WorkflowStartResult",
            "WaitForOutputAsync", "DeliverToInstanceAsync", "DeliverByCorrelationAsync",
            "AddOrcaCoreEphemeralEngine", "AddOrcaCoreDurableEngine", "AddOrcaCoreDurableEventIngress",
            "AddOrcaCoreInMemoryDurableProvider", "AddOrcaCorePostgreSqlDurableProvider", "AddOrcaCoreDag",
            "exact named step type", "no configuration-binder"
        }) matrix.Should().Contain(anchor);
    }
}

[Trait(GuardTraits.Phase, GuardTraits.Phase0)]
[Trait(GuardTraits.Disposition, GuardTraits.ExpectedRed)]
public sealed class FacadeHostingExpectedRedGuards
{
    [Fact]
    public void Product_ExportsTheCommonRegistryAndExactEventClient()
    {
        var exported = PublicSurfaceCatalog.Assemblies.SelectMany(x => x.GetExportedTypes()).ToArray();
        exported.Should().Contain(x => x.FullName == "OrcaCore.IWorkflowDefinitionRegistry");
        exported.Should().Contain(x => x.FullName == "OrcaCore.IWorkflowEventClient");
    }
}
