using AwesomeAssertions;

namespace OrcaCore.DeveloperSurface.Guards;

[Trait(GuardTraits.Phase, GuardTraits.Phase0)]
[Trait(GuardTraits.Disposition, GuardTraits.Infrastructure)]
public sealed class DagContractInfrastructureGuards
{
    [Fact]
    public void DagConsumer_IsTypedCastFreeAndContainsNoInventedChildProtocol()
    {
        var source = File.ReadAllText(Path.Combine(FixtureDefinitions.RepositoryRoot(), "tests",
            "OrcaCore.DeveloperSurface.Guards", "PackageFixtures", "DagHosting", "Program.cs"));
        source.Should().Contain("WorkflowDag.Define<RunInput>").And.Contain("MapInput")
            .And.Contain("OutputOf(produce)").And.Contain("GetHandleOrThrow")
            .And.Contain("WaitForTerminalAsync");
        source.Should().NotContain("RunChild").And.NotContain("RunChildren")
            .And.NotContain("RootInstanceId").And.NotContain("ParentInstanceId").And.NotContain("Outbox");
    }

    [Fact]
    public void ScenarioLedger_CoversCompleteDagBuildAndOperationContract()
    {
        var scenarios = FixtureDefinitions.Read<GuardScenario[]>(
            "tests/OrcaCore.DeveloperSurface.Guards/Fixtures/dag-contract-scenarios.json");
        scenarios.Should().HaveCount(9);
        scenarios.Select(x => x.Id).Should().OnlyHaveUniqueItems();
        scenarios.Should().OnlyContain(x => x.TaskId == "3.10" &&
            !string.IsNullOrWhiteSpace(x.Setup) && !string.IsNullOrWhiteSpace(x.Assertion) &&
            !string.IsNullOrWhiteSpace(x.ExpectedRed) && !string.IsNullOrWhiteSpace(x.TurnsGreenTask));
    }

    [Fact]
    public void Matrix_AnchorsDagMappingOperationsAdmissionAndFriendBoundary()
    {
        var matrix = File.ReadAllText(Path.Combine(FixtureDefinitions.RepositoryRoot(), "docs", "specs",
            "17-selected-mode-capability-matrix.md"));
        foreach (var anchor in new[]
        {
            "DAG-AUTH-MAP-001", "DAG-AUTH-MAP-002", "DAG_INPUT_MAPPING_INVALID", "OutputOf",
            "before mapped-input", "WaitForTerminalAsync", "ChildInstanceId", "MaxConcurrentNodes",
            "InternalsVisibleTo(\"OrcaCore.Dag.Hosting\")", "`RunChild`/`RunChildren` are deferred public APIs"
        }) matrix.Should().Contain(anchor);
    }
}

[Trait(GuardTraits.Phase, GuardTraits.Phase0)]
[Trait(GuardTraits.Disposition, GuardTraits.ExpectedRed)]
public sealed class DagContractExpectedRedGuards
{
    [Fact]
    public void ExactDagAssembliesAndTypesExist()
    {
        var root = Path.Combine(FixtureDefinitions.RepositoryRoot(), "src");
        File.Exists(Path.Combine(root, "OrcaCore.Dag", "OrcaCore.Dag.csproj")).Should().BeTrue();
        File.Exists(Path.Combine(root, "OrcaCore.Dag.Hosting", "OrcaCore.Dag.Hosting.csproj")).Should().BeTrue();
    }
}
