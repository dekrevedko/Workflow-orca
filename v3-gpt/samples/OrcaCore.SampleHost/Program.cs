using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using OrcaCore.Hosting;

namespace OrcaCore.SampleHost;

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
