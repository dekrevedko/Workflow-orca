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

        var leaseExitSource = NormalizeLineEndings(File.ReadAllText(Path.Combine(
            testRoot,
            "OrcaCore.DeveloperSurface.BehaviorScenarios",
            "LeaseExitScenarioHost.cs")));
        Regex.Matches(
                leaseExitSource,
                @"await[ \t]+AwaitSignalBeforeWorkflowTerminalAsync\([ \t\r\n]+gate\.SecondStarted\.Task,[ \t\r\n]+instance,",
                RegexOptions.CultureInvariant)
            .Should().HaveCount(LeasedRetryFixtureCount,
                "both leased retry fixtures must compare the second-attempt signal with the real instance terminal state");
        leaseExitSource.Should().Contain(
            "var snapshot = await instance.GetSnapshotAsync(CancellationToken.None);",
            "second-attempt synchronization must observe the durable instance rather than the start-operation task");
        leaseExitSource.Should().MatchRegex(
            @"if \(!signal\.IsCompleted &&\r?\n[ \t]+snapshot\.Status is \(WorkflowInstanceStatus\.Completed or",
            "a signal that wins during snapshot retrieval must not be misclassified as terminal-first");
        leaseExitSource.Should().MatchRegex(
            @"await gate\.Release\.Task;\r?\n[ \t]+}\r?\n[ \t]+else",
            "the first protected body must remain cancellation-ignoring so late-return fencing stays covered");
        leaseExitSource.Should().NotContain(
            "cancellationToken.ThrowIfCancellationRequested();",
            "the leased retry scenario must not narrow coverage to cancellation-cooperative bodies");
        leaseExitSource.Should().Contain(
            "private static readonly TimeSpan TerminalObservationTimeout = TimeSpan.FromSeconds(15);",
            "terminal observation must retain its exact finite fifteen-second deadline");
        leaseExitSource.Should().Contain(
            "TerminalObservationTimeout,\n            TimeProvider.System",
            "terminal observation must have an explicit bounded deadline");
        leaseExitSource.Should().Contain(
            "Task.Delay(TerminalObservationInterval, TimeProvider.System, deadline.Token)",
            "terminal observation must delay between snapshots rather than spin");
        leaseExitSource.Should().NotMatchRegex(
            @"AwaitSignalBeforeWorkflowCompletionAsync\([ \t\r\n]+gate\.SecondStarted\.Task,[ \t\r\n]+running,",
            "the start operation may complete before the scheduler publishes the second-attempt signal");
        leaseExitSource.Should().NotContain(
            "await gate.SecondStarted.Task;",
            "a bare second-attempt await can hang forever when the workflow completes without publishing the signal");
        leaseExitSource.Should().NotContain(
            "if (!gate.SecondStarted.Task.IsCompleted)",
            "a post-completion snapshot is a scheduler race, not notification-driven evidence");
    }

    private static string NormalizeLineEndings(string value) =>
        value.Replace("\r\n", "\n", StringComparison.Ordinal)
            .Replace("\r", "\n", StringComparison.Ordinal);

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
