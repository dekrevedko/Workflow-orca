using System.Diagnostics;
using System.Diagnostics.Metrics;
using OrcaCore.Abstractions.Diagnostics;

namespace OrcaCore.Engine.Durable.Diagnostics;

/// <summary>
/// Owns durable engine BCL diagnostics sources.
/// </summary>
public static class OrcaCoreDurableDiagnostics
{
    public const string SourceName = OrcaCoreDiagnostics.DurableSourceName;

    public static ActivitySource ActivitySource { get; } = new(SourceName);

    public static Meter Meter { get; } = new(SourceName);
}
