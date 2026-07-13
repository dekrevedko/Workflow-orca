using System.Diagnostics;
using System.Diagnostics.Metrics;
using OrcaCore.Abstractions.Diagnostics;

namespace OrcaCore.Providers.Redis.Diagnostics;

public static class OrcaCoreRedisProviderDiagnostics
{
    public const string SourceName = OrcaCoreDiagnostics.RedisProviderSourceName;

    public static ActivitySource ActivitySource { get; } = new(SourceName);

    public static Meter Meter { get; } = new(SourceName);
}
