using System.Diagnostics;
using System.Diagnostics.Metrics;

namespace OrcaCore.Abstractions.Diagnostics;

/// <summary>
/// Exposes BCL diagnostic sources that hosts can bridge into their telemetry stack.
/// </summary>
public static class OrcaCoreDiagnostics
{
    /// <summary>
    /// Gets the stable activity source name for OrcaCore operations.
    /// </summary>
    public const string SourceName = "OrcaCore";

    /// <summary>
    /// Emits spans around engine and provider boundaries.
    /// </summary>
    public static ActivitySource ActivitySource { get; } = new(SourceName);

    /// <summary>
    /// Emits metrics around engine and provider boundaries.
    /// </summary>
    public static Meter Meter { get; } = new(SourceName);
}
