using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using OrcaCore.Hosting;

namespace OrcaCore.SampleHost;

/// <summary>
/// The minimal way to run OrcaCore inside a .NET Generic Host.
/// </summary>
/// <remarks>
/// WHAT: registers the OrcaCore stack and its background services in a host, then runs it.
/// HOW: <c>AddOrcaCore()</c> wires the engines, durable command processor, management, and the
/// default in-memory provider; <c>AddOrcaCoreHostedServices()</c> adds the hosted outbox pump,
/// durable timer sweep, and operational sweep as <see cref="IHostedService"/>s that start with
/// the host. WHY: this is the seam a real application builds on — swap the in-memory provider for
/// a durable one (e.g. <c>AddOrcaCorePostgreSql</c>) and OrcaCore's background work (dispatch,
/// timers, expiry) runs automatically under the host lifetime. Unlike the console examples, which
/// pump timers and drive commands by hand, a hosted app lets the hosted services do that for you.
/// </remarks>
public static class SampleHostApplication
{
    public static IHost Build(string[] args)
    {
        var builder = Host.CreateApplicationBuilder(args);
        builder.Services
            .AddOrcaCore()
            .AddOrcaCoreHostedServices();
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
