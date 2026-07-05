using System.Diagnostics;
using System.Diagnostics.Metrics;

namespace OrcaCore.Providers.Relational.Diagnostics;

public static class OrcaCoreRelationalProviderDiagnostics
{
    public const string SourceName = "OrcaCore.Providers.Relational";

    public static ActivitySource ActivitySource { get; } = new(SourceName);

    public static Meter Meter { get; } = new(SourceName);
}
