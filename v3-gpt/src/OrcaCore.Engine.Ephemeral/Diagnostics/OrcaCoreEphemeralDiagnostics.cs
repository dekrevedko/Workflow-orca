using System.Diagnostics;
using System.Diagnostics.Metrics;
using OrcaCore.Abstractions.Diagnostics;

namespace OrcaCore.Engine.Ephemeral.Diagnostics;

/// <summary>
/// Owns the ephemeral engine telemetry source and meter.
/// </summary>
public static class OrcaCoreEphemeralDiagnostics
{
    public const string SourceName = OrcaCoreDiagnostics.EphemeralSourceName;

    public static ActivitySource ActivitySource { get; } = new(SourceName);

    public static Meter Meter { get; } = new(SourceName);
}
