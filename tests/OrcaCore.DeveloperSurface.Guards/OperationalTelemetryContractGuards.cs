using System.Collections.Concurrent;
using System.Diagnostics;
using AwesomeAssertions;
using Microsoft.Extensions.DependencyInjection;
using OrcaCore.Abstractions.Diagnostics;
using OrcaCore.Providers.InMemory;
using OrcaCore.Providers.PostgreSql;
using OrcaCore.Providers.SqlServer;

namespace OrcaCore.DeveloperSurface.Guards;

[Trait(GuardTraits.Phase, GuardTraits.Phase0)]
[Trait(GuardTraits.Disposition, GuardTraits.Infrastructure)]
public sealed class OperationalTelemetryContractGuards
{
    private static readonly (string Name, int EventId)[] RequiredStructuredLogs =
    [
        ("CommandCompleted", 1001),
        ("OutboxPumpCompleted", 1101),
        ("OutboxPermanentFailure", 1102),
        ("OutboxException", 1103),
        ("StepTransition", 1201),
        ("WaitTransition", 1301),
        ("TimerTransition", 1302),
        ("ProviderCommitCompleted", 1401),
        ("ProviderCommitConflict", 1402),
        ("ProviderCommitFailed", 1403),
        ("StuckInstancesObserved", 1501),
        ("ResourcePoolTransition", 1601),
        ("ResourcePoolObserved", 1602),
        ("LifecycleTransition", 1701),
        ("SweepCompleted", 1801),
        ("ProcessingCycleFailed", 1901)
    ];

    [Fact]
    public void DiagnosticSourceCatalog_ListsEveryRuntimeAndSelectedProviderOwner()
    {
        OrcaCoreDiagnostics.ProviderSourceNames.Should().Equal(
            OrcaCoreDiagnostics.InMemoryProviderSourceName,
            OrcaCoreDiagnostics.PostgreSqlProviderSourceName,
            OrcaCoreDiagnostics.SqlServerProviderSourceName);
        OrcaCoreDiagnostics.MeterNames.Should().Equal(
            OrcaCoreDiagnostics.SourceName,
            OrcaCoreDiagnostics.DurableSourceName,
            OrcaCoreDiagnostics.EphemeralSourceName,
            OrcaCoreDiagnostics.InMemoryProviderSourceName,
            OrcaCoreDiagnostics.PostgreSqlProviderSourceName,
            OrcaCoreDiagnostics.SqlServerProviderSourceName);
        OrcaCoreDiagnostics.ActivitySourceNames.Should().Equal(OrcaCoreDiagnostics.MeterNames);

        var diagnosticsSource = File.ReadAllText(Path.Combine(
            FixtureDefinitions.RepositoryRoot(),
            "src",
            "OrcaCore.Abstractions",
            "Diagnostics",
            "OrcaCoreDiagnostics.cs"));
        diagnosticsSource.Should().Contain("ActivitySource ActivitySource { get; } = new(SourceName)")
            .And.Contain("Meter Meter { get; } = new(SourceName)");
    }

    [Fact]
    public void ProviderSourcesAndEngineListenerEvidence_AreCompleteAndReflectionFree()
    {
        var observed = new ConcurrentDictionary<string, byte>(StringComparer.Ordinal);
        using var listener = new ActivityListener
        {
            ShouldListenTo = source =>
            {
                if (OrcaCoreDiagnostics.ProviderSourceNames.Contains(source.Name, StringComparer.Ordinal))
                {
                    observed.TryAdd(source.Name, 0);
                }

                return false;
            }
        };
        ActivitySource.AddActivityListener(listener);

        _ = new ServiceCollection().AddOrcaCoreInMemoryDurableProvider();
        _ = new ServiceCollection().AddOrcaCorePostgreSqlDurableProvider(
            new PostgreSqlDurableProviderOptions("Host=localhost;Database=orcacore", "orcacore"));
        _ = new ServiceCollection().AddOrcaCoreSqlServerDurableProvider(
            new SqlServerDurableProviderOptions(
                "Server=localhost;Database=orcacore;Integrated Security=true;TrustServerCertificate=true",
                "orcacore"));

        observed.Keys.OrderBy(name => name, StringComparer.Ordinal)
            .Should().Equal(OrcaCoreDiagnostics.ProviderSourceNames.OrderBy(name => name, StringComparer.Ordinal));

        var root = FixtureDefinitions.RepositoryRoot();
        var listenerEvidence = File.ReadAllText(Path.Combine(
            root,
            "tests",
            "OrcaCore.Engine.Durable.Tests",
            "Diagnostics",
            "DurableOperationalTelemetryTests.cs"));
        listenerEvidence.Should().Contain("ActiveListener_ObservesExactlyTheCompleteCanonicalDurableMetricCatalog")
            .And.Contain(".Should().Equal(RequiredInstrumentNames)")
            .And.Contain("new DurableStepThrottleCoordinator()")
            .And.Contain("ActiveListener_ObservesAuthoritativeGroupedStatisticsWithExactTags")
            .And.Contain("OrcaCoreDurableDiagnostics.RefreshOperatorStatistics(statistics, [pool])")
            .And.Contain("RuntimeObservation_UsesAggregateVersionAndRealStepIdentity");
        AssertNoReflectionBridge(listenerEvidence);

        var ephemeralListenerEvidence = File.ReadAllText(Path.Combine(
            root,
            "tests",
            "OrcaCore.Engine.Ephemeral.Tests",
            "EphemeralOperatorStatisticsTelemetryTests.cs"));
        ephemeralListenerEvidence.Should().Contain("Meter_PublishesExactlyTheCanonicalEphemeralInstrumentCatalog")
            .And.Contain(".Should().Equal(RequiredInstrumentNames.OrderBy")
            .And.Contain("OnlyContain(name => name.StartsWith(\"orca.\", StringComparison.Ordinal))");
        AssertNoReflectionBridge(ephemeralListenerEvidence);
    }

    [Fact]
    public void Product_EmitsTheExactBclSpanAndStructuredLogFamiliesWithoutAnSdkDependency()
    {
        var root = FixtureDefinitions.RepositoryRoot();
        var durableSource = ReadSource(Path.Combine(root, "src", "OrcaCore.Engine.Durable"));
        var hostingRoot = Path.Combine(root, "src", "OrcaCore.Durable.Hosting");
        var hostingSource = ReadSource(hostingRoot);
        foreach (var spanOwner in new[]
        {
            nameof(OrcaCoreDiagnostics.CommandProcessActivity),
            nameof(OrcaCoreDiagnostics.ProviderCommitActivity),
            nameof(OrcaCoreDiagnostics.StepExecuteActivity),
            nameof(OrcaCoreDiagnostics.OutboxDispatchActivity),
            nameof(OrcaCoreDiagnostics.OutboxPumpCycleActivity),
            nameof(OrcaCoreDiagnostics.EventApplyActivity)
        })
        {
            durableSource.Should().Contain($"OrcaCoreDiagnostics.{spanOwner}");
        }

        File.ReadAllText(Path.Combine(
                root,
                "src",
                "OrcaCore.Engine.Durable",
                "Execution",
                "DurableCommandProcessor.cs"))
            .Should().Contain("await telemetry.ObserveProviderCommitFailedAsync(");
        var runtimeObserverSource = File.ReadAllText(Path.Combine(
            root,
            "src",
            "OrcaCore.Engine.Durable",
            "Execution",
            "IWorkflowRuntimeObserver.cs"));
        string.Concat(runtimeObserverSource.Where(character => !char.IsWhiteSpace(character)))
            .Should().Contain(
                "ValueTaskOnProviderCommitFailedAsync(" +
                "WorkflowProviderCommitFailureObservationobservation," +
                "CancellationTokencancellationToken);");

        var driverSource = File.ReadAllText(Path.Combine(
            root,
            "src",
            "OrcaCore.Engine.Durable",
            "Driver",
            "DurableFiberDriverExecutor.cs"));
        CountOrdinalOccurrences(driverSource, "StepOperationId = stepOperationId").Should().Be(3,
            "success, branch-failure, and root-failure commands must preserve the executed operation identity");
        CountOrdinalOccurrences(driverSource, "StepAttemptNumber = stepAttemptNumber").Should().Be(3,
            "success, branch-failure, and root-failure commands must preserve the executed attempt ordinal");

        foreach (var (name, eventId) in RequiredStructuredLogs)
        {
            hostingSource.Should().Contain($"private const int {name}EventId = {eventId};");
            hostingSource.Should().Contain($"partial void {name}(");
        }

        var hostingReadme = File.ReadAllText(Path.Combine(hostingRoot, "README.md"));
        foreach (var rangeStart in RequiredStructuredLogs.Select(item => item.EventId / 100 * 100).Distinct())
        {
            hostingReadme.Should().Contain($"{rangeStart}-{rangeStart + 99}");
        }

        foreach (var logKeyOwner in new[]
        {
            nameof(OrcaCoreDiagnostics.CommandTypeKey),
            nameof(OrcaCoreDiagnostics.OutboxRecordIdKey),
            nameof(OrcaCoreDiagnostics.OutboxAttemptKey),
            nameof(OrcaCoreDiagnostics.StepPathKey),
            nameof(OrcaCoreDiagnostics.StepOperationIdKey),
            nameof(OrcaCoreDiagnostics.StepAttemptKey),
            nameof(OrcaCoreDiagnostics.WaitEventNameKey),
            nameof(OrcaCoreDiagnostics.CorrelationIdKey),
            nameof(OrcaCoreDiagnostics.TimerIdKey),
            nameof(OrcaCoreDiagnostics.TimerFireAtKey),
            nameof(OrcaCoreDiagnostics.ProviderNameKey),
            nameof(OrcaCoreDiagnostics.ExpectedStreamVersionKey),
            nameof(OrcaCoreDiagnostics.StreamVersionKey),
            nameof(OrcaCoreDiagnostics.ResourcePoolNameKey),
            nameof(OrcaCoreDiagnostics.ResourceOwnerKey),
            nameof(OrcaCoreDiagnostics.LeaseObligationIdKey),
            nameof(OrcaCoreDiagnostics.ResourceTicketIdKey),
            nameof(OrcaCoreDiagnostics.ResourceOwnerGenerationKey),
            nameof(OrcaCoreDiagnostics.ResourceActionKey),
            nameof(OrcaCoreDiagnostics.LifecycleEventNameKey)
        })
        {
            hostingSource.Should().Contain($"OrcaCoreDiagnostics.{logKeyOwner}");
        }
        Directory.GetFiles(Path.Combine(root, "src"), "*.csproj", SearchOption.AllDirectories)
            .Select(File.ReadAllText)
            .Should().OnlyContain(text => !text.Contains("OpenTelemetry", StringComparison.Ordinal));

        var providerCertificationSource = File.ReadAllText(Path.Combine(
            root,
            "tests",
            "OrcaCore.ProviderCertification",
            "EventStoreCertificationTests.cs"));
        providerCertificationSource.Should().Contain("DispatchAttempt.Should().Be(1)")
            .And.Contain("DispatchAttempt.Should().Be(2)");

        File.ReadAllText(Path.Combine(
                root,
                "tests",
                "OrcaCore.Integration.Tests",
                "Observability",
                "ObservabilityIntegrationTests.cs"))
            .Should().NotContain("OrcaCore.Hosting.csproj");
        var telemetryGuard = File.ReadAllText(Path.Combine(
            root,
            "tests",
            "OrcaCore.DeveloperSurface.Guards",
            "OperationalTelemetryContractGuards.cs"));
        AssertNoReflectionBridge(telemetryGuard);
    }

    private static void AssertNoReflectionBridge(string source)
    {
        foreach (var token in new[]
        {
            string.Concat("System", ".Reflection"),
            string.Concat("Binding", "Flags"),
            string.Concat("Assembly", ".Load("),
            string.Concat(".Get", "Method("),
            string.Concat(".Get", "Property("),
            string.Concat(".Get", "Field("),
            string.Concat(".Get", "Constructor("),
            string.Concat(".Invoke", "(")
        })
        {
            source.Should().NotContain(token);
        }
    }

    private static string ReadSource(string path) =>
        string.Join('\n', Directory.GetFiles(path, "*.cs", SearchOption.AllDirectories).Select(File.ReadAllText));

    private static int CountOrdinalOccurrences(string source, string value)
    {
        var count = 0;
        var startIndex = 0;
        while ((startIndex = source.IndexOf(value, startIndex, StringComparison.Ordinal)) >= 0)
        {
            count++;
            startIndex += value.Length;
        }

        return count;
    }
}
