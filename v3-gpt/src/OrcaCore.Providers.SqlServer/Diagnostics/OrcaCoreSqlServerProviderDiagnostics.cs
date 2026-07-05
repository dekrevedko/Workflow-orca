using System.Diagnostics;
using System.Diagnostics.Metrics;
using OrcaCore.Abstractions.Diagnostics;

namespace OrcaCore.Providers.SqlServer.Diagnostics;

public static class OrcaCoreSqlServerProviderDiagnostics
{
    public const string SourceName = OrcaCoreDiagnostics.SqlServerProviderSourceName;

    public static ActivitySource ActivitySource { get; } = new(SourceName);

    public static Meter Meter { get; } = new(SourceName);
}
