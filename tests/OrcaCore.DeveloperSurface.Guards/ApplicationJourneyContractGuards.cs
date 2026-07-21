using AwesomeAssertions;

namespace OrcaCore.DeveloperSurface.Guards;

[Trait(GuardTraits.Phase, GuardTraits.Phase0)]
[Trait(GuardTraits.Disposition, GuardTraits.Infrastructure)]
public sealed class ApplicationJourneyInfrastructureGuards
{
    [Fact]
    public void PackagePrograms_AreApplicationOnlyTypedJourneys()
    {
        var root = Path.Combine(FixtureDefinitions.RepositoryRoot(), "tests", "OrcaCore.DeveloperSurface.Guards", "PackageFixtures");
        var ephemeral = File.ReadAllText(Path.Combine(root, "MinimalEphemeral", "Program.cs"));
        var durable = File.ReadAllText(Path.Combine(root, "PostgreSqlDurable", "Program.cs"));
        var ingress = File.ReadAllText(Path.Combine(root, "CallbackIngress", "Program.cs"));
        var scheduler = File.ReadAllText(Path.Combine(root, "KubernetesCompanion", "Program.cs"));
        ephemeral.Should().Contain("start.WaitForOutputAsync(token)").And.Contain("GetInstanceAsync");
        durable.Should().Contain(".Wait(").And.Contain("DeliverToInstanceAsync").And.Contain("start.WaitForOutputAsync(token)");
        ingress.Should().Contain("DeliverToInstanceAsync").And.Contain("DeliverByCorrelationAsync")
            .And.Contain("EventDeliveryStatus.NoActiveWait");
        scheduler.Should().Contain(".Wait(").And.Contain(".Then<ValidateTerminalJobStep>()")
            .And.Contain("payload.JobUid").And.Contain("context.Execution.OperationId.Value")
            .And.Contain("lease.ProtectionToken.Value").And.Contain("payload.IsTerminal");
        foreach (var source in new[] { ephemeral, durable, ingress, scheduler })
            source.Should().NotContain("CommandProcessor").And.NotContain("WaitLong").And.NotContain("RunExternalJob");
    }

    [Fact]
    public void ScenarioLedger_CoversEventAndSplitHostSchedules()
    {
        var scenarios = FixtureDefinitions.Read<GuardScenario[]>(
            "tests/OrcaCore.DeveloperSurface.Guards/Fixtures/application-journey-scenarios.json");
        scenarios.Should().HaveCount(8);
        scenarios.Select(x => x.Id).Should().OnlyHaveUniqueItems();
        scenarios.Should().OnlyContain(x => x.TaskId == "3.8" &&
            !string.IsNullOrWhiteSpace(x.Setup) && !string.IsNullOrWhiteSpace(x.Assertion) &&
            !string.IsNullOrWhiteSpace(x.ExpectedRed) && !string.IsNullOrWhiteSpace(x.TurnsGreenTask));
    }
}

[Trait(GuardTraits.Phase, GuardTraits.Phase0)]
[Trait(GuardTraits.Disposition, GuardTraits.ExpectedRed)]
public sealed class ApplicationJourneyExpectedRedGuards
{
    [Fact]
    public void TypedJourneyPackages_AreAvailableForCleanConsumerBuilds()
    {
        Directory.GetFiles(Path.Combine(FixtureDefinitions.RepositoryRoot(), "artifacts", "phase0-packages"), "*.nupkg")
            .Should().Contain(path => Path.GetFileName(path) == "OrcaCore.0.0.0-phase0.nupkg");
    }
}
