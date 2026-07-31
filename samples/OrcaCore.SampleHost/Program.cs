using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using OrcaCore;
using OrcaCore.Hosting;
using OrcaCore.Providers.InMemory;

namespace OrcaCore.SampleHost;

/// <summary>
/// The minimal way to run OrcaCore inside a .NET Generic Host.
/// </summary>
/// <remarks>
/// The application explicitly chooses one provider role and one engine role. The durable engine
/// owns its hosted continuation, timer, and operational loops; no catch-all registration or
/// implicit workflow mode is involved.
/// </remarks>
public static class SampleHostApplication
{
    public static IHost Build(string[] args)
    {
        var builder = Host.CreateApplicationBuilder(args);
        builder.Services
            .AddOrcaCoreInMemoryDurableProvider()
            .AddOrcaCoreDurableEngine(new DurableEngineHostOptions
            {
                StructuredExecution = new StructuredExecutionHostOptions
                {
                    MaxConcurrentExecutionPathsPerInstance = 32,
                    StepThrottles = Array.Empty<StepExecutionThrottle>()
                },
                ResourcePools = new DurableResourcePoolOptions
                {
                    PartitionId = ResourceGovernancePartitionId.Create("sample"),
                    Pools = Array.Empty<DurableResourcePoolDefinition>()
                }
            });
        return builder.Build();
    }
}

internal static class Program
{
    private static async Task Main(string[] args)
    {
        await SampleHostApplication.Build(args).RunAsync().ConfigureAwait(false);
    }
}
