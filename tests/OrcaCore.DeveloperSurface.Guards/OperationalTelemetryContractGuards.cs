using System.Diagnostics.Metrics;
using AwesomeAssertions;
using OrcaCore.Engine.Durable.Diagnostics;

namespace OrcaCore.DeveloperSurface.Guards;

[Trait(GuardTraits.Phase, GuardTraits.Phase0)]
[Trait(GuardTraits.Disposition, GuardTraits.Infrastructure)]
public sealed class OperationalTelemetryContractGuards
{
    [Fact]
    public void DurableEngine_PublishesTheThreeExactBclOperationalInstruments()
    {
        var published = new HashSet<string>(StringComparer.Ordinal);
        using var listener = new MeterListener
        {
            InstrumentPublished = (instrument, current) =>
            {
                if (instrument.Meter.Name == OrcaCoreDurableDiagnostics.SourceName)
                {
                    published.Add(instrument.Name);
                    current.EnableMeasurementEvents(instrument);
                }
            }
        };
        listener.Start();
        _ = OrcaCoreDurableDiagnostics.Meter;

        published.Should().Contain(new[]
        {
            OrcaCoreDurableDiagnostics.QuarantinedUnitsInstrumentName,
            OrcaCoreDurableDiagnostics.OldestQuarantinedObligationAgeInstrumentName,
            OrcaCoreDurableDiagnostics.FencedBodiesRunningInstrumentName
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
        guidance.Should().Contain(OrcaCoreDurableDiagnostics.SourceName);
        guidance.Should().Contain(OrcaCoreDurableDiagnostics.QuarantinedUnitsInstrumentName);
        guidance.Should().Contain(OrcaCoreDurableDiagnostics.OldestQuarantinedObligationAgeInstrumentName);
        guidance.Should().Contain(OrcaCoreDurableDiagnostics.FencedBodiesRunningInstrumentName);
        guidance.Should().Contain("authorize").And.Contain("redact");
        guidance.Should().Contain("host-owned").And.Contain("OpenTelemetry");
    }
}
