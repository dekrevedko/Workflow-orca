using System.Diagnostics;
using System.Diagnostics.Metrics;
using OrcaCore.Abstractions.Diagnostics;

namespace OrcaCore.Providers.PostgreSql.Diagnostics;

public static class OrcaCorePostgreSqlProviderDiagnostics
{
    public const string SourceName = OrcaCoreDiagnostics.PostgreSqlProviderSourceName;

    public static ActivitySource ActivitySource { get; } = new(SourceName);

    public static Meter Meter { get; } = new(SourceName);
}
