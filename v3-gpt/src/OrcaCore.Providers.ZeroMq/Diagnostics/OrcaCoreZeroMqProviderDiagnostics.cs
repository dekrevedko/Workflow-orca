using System.Diagnostics;
using System.Diagnostics.Metrics;
using OrcaCore.Abstractions.Diagnostics;

namespace OrcaCore.Providers.ZeroMq.Diagnostics;

public static class OrcaCoreZeroMqProviderDiagnostics
{
    public const string SourceName = OrcaCoreDiagnostics.ZeroMqProviderSourceName;

    public static ActivitySource ActivitySource { get; } = new(SourceName);

    public static Meter Meter { get; } = new(SourceName);
}
