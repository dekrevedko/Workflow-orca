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
        ephemeral.Should().Contain("WorkflowEventContract.Create").And.Contain("AddWorkflow(definition)")
            .And.Contain("GetRequiredHandle(reference)").And.Contain("start.WaitForOutputAsync(token)");
        durable.Should().Contain("WorkflowEventContract<Approval>.Create").And.Contain(".Wait(")
            .And.Contain(".Publish(").And.Contain("IWorkflowEventIngress").And.Contain("start.WaitForOutputAsync(token)");
        ingress.Should().Contain("WorkflowEventRoute.Direct").And.Contain("WorkflowEventRoute.Correlation")
            .And.Contain("WorkflowEventRoute.DefinitionFanout").And.Contain("WorkflowEventRoute.StartOrDeliver")
            .And.Contain("AcceptAsync").And.Contain("WorkflowEventAcceptanceResult.Accepted")
            .And.Contain("WorkflowEventAcceptanceRejection.FanoutLimitExceeded");
        scheduler.Should().Contain("WorkflowEventContract<JobTerminal>.Create").And.Contain(".Wait(")
            .And.Contain("GetPayload(EventContracts.JobTerminal)").And.Contain(".Then<ValidateTerminalJobStep>()")
            .And.Contain("payload.JobUid").And.Contain("context.Execution.OperationId.Value")
            .And.Contain("lease.ProtectionToken.Value").And.Contain("payload.IsTerminal");
        foreach (var source in new[] { ephemeral, durable, ingress, scheduler })
            source.Should().NotContain("IWorkflowEventClient").And.NotContain("DeliverToInstanceAsync")
                .And.NotContain("DeliverByCorrelationAsync").And.NotContain("CommandProcessor")
                .And.NotContain("WaitLong").And.NotContain("RunExternalJob").And.NotContain("DefinitionId.New()");
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
[Trait(GuardTraits.Disposition, GuardTraits.Infrastructure)]
public sealed class ApplicationJourneyProductGuards
{
    [Fact]
    public void TypedJourneyPackages_AreAvailableForCleanConsumerBuilds()
    {
        var root = FixtureDefinitions.RepositoryRoot();
        var feed = Path.Combine(root, "artifacts", "phase0-packages");
        var requiredPackages = Directory.GetFiles(Path.Combine(root, "tests", "OrcaCore.DeveloperSurface.Guards", "PackageFixtures"),
                "*.csproj", SearchOption.AllDirectories)
            .SelectMany(File.ReadLines)
            .SelectMany(line => System.Text.RegularExpressions.Regex.Matches(line, "PackageReference Include=\"(?<id>OrcaCore[^\"]*)\"")
                .Select(match => match.Groups["id"].Value))
            .Distinct(StringComparer.Ordinal)
            .OrderBy(id => id, StringComparer.Ordinal)
            .ToArray();

        requiredPackages.Should().NotBeEmpty();
        foreach (var packageId in requiredPackages)
            File.Exists(Path.Combine(feed, $"{packageId}.0.0.0-phase0.nupkg")).Should().BeTrue(
                $"the clean consumer fixtures require the exact {packageId} package");
    }
}
