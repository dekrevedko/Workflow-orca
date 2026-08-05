using System.Diagnostics.Metrics;
using System.Reflection;
using AwesomeAssertions;
using OrcaCore.Abstractions.Diagnostics;

namespace OrcaCore.DeveloperSurface.Guards;

[Trait(GuardTraits.Phase, GuardTraits.Phase0)]
[Trait(GuardTraits.Disposition, GuardTraits.Infrastructure)]
public sealed class OperationalTelemetryContractGuards
{
    private const string QuarantinedUnitsInstrumentName = "orcacore.resource.quarantined.units";
    private const string OldestQuarantinedObligationAgeInstrumentName =
        "orcacore.resource.quarantined.oldest_age";
    private const string FencedBodiesRunningInstrumentName =
        "orcacore.execution.fenced_bodies.running";

    [Fact]
    public void DurableEngine_PublishesTheThreeExactBclOperationalInstruments()
    {
        var published = new HashSet<string>(StringComparer.Ordinal);
        using var listener = new MeterListener
        {
            InstrumentPublished = (instrument, current) =>
            {
                if (instrument.Meter.Name == OrcaCoreDiagnostics.DurableSourceName)
                {
                    published.Add(instrument.Name);
                    current.EnableMeasurementEvents(instrument);
                }
            }
        };
        listener.Start();
        _ = Assembly.Load("OrcaCore.Engine.Durable")
            .GetType("OrcaCore.Engine.Durable.Diagnostics.OrcaCoreDurableDiagnostics", throwOnError: true)!
            .GetProperty("Meter", BindingFlags.Public | BindingFlags.Static)!
            .GetValue(null);

        published.Should().Contain(new[]
        {
            QuarantinedUnitsInstrumentName,
            OldestQuarantinedObligationAgeInstrumentName,
            FencedBodiesRunningInstrumentName
        });
    }

    [Fact]
    public void DashboardGuidance_MapsEveryInstrumentAndKeepsAdapterSecurityHostOwned()
    {
        var guidance = File.ReadAllText(Path.Combine(
            FixtureDefinitions.RepositoryRoot(),
            "docs",
            "observability",
            "resource-governance-dashboard.md"));
        guidance.Should().Contain(OrcaCoreDiagnostics.DurableSourceName);
        guidance.Should().Contain(QuarantinedUnitsInstrumentName);
        guidance.Should().Contain(OldestQuarantinedObligationAgeInstrumentName);
        guidance.Should().Contain(FencedBodiesRunningInstrumentName);
        guidance.Should().Contain("authorize").And.Contain("redact");
        guidance.Should().Contain("host-owned").And.Contain("OpenTelemetry");
    }
}
