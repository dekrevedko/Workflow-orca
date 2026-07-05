using System.Diagnostics;
using System.Diagnostics.Metrics;
using OrcaCore.Abstractions.Diagnostics;

namespace OrcaCore.Providers.InMemory.Diagnostics;

public static class OrcaCoreInMemoryProviderDiagnostics
{
    public const string SourceName = OrcaCoreDiagnostics.InMemoryProviderSourceName;

    public static ActivitySource ActivitySource { get; } = new(SourceName);

    public static Meter Meter { get; } = new(SourceName);
}
