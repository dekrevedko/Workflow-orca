using System.Diagnostics;
using System.Diagnostics.Metrics;
using OrcaCore.Abstractions.Diagnostics;

namespace OrcaCore.Providers.RabbitMq.Diagnostics;

public static class OrcaCoreRabbitMqProviderDiagnostics
{
    public const string SourceName = OrcaCoreDiagnostics.RabbitMqProviderSourceName;

    public static ActivitySource ActivitySource { get; } = new(SourceName);

    public static Meter Meter { get; } = new(SourceName);
}
