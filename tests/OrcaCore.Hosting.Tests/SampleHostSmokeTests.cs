using AwesomeAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using OrcaCore.Abstractions.Providers;
using OrcaCore.SampleHost;
using Xunit;

namespace OrcaCore.Hosting.Tests;

public sealed class SampleHostSmokeTests
{
    [Fact]
    public void SampleHost_StartsWithInMemoryProvider_ResolvesHostedServices()
    {
        using var host = SampleHostApplication.Build([]);

        host.Services.GetRequiredService<IWorkflowDefinitionRegistry>().Should().NotBeNull();
        host.Services.GetRequiredService<IWorkflowEventStore>().Should().NotBeNull();
        host.Services.GetRequiredService<IWorkflowProjectionStore>().Should().NotBeNull();
        host.Services.GetServices<IHostedService>()
            .Select(service => service.GetType().Name)
            .Should()
            .Equal(
                "OrcaCoreDurableWorkflowCatalogReadinessHostedService",
                "OrcaCoreContinuationPumpHostedService",
                "OrcaCoreTimerHostedService",
                "OrcaCoreOperationalSweepHostedService",
                "OrcaCoreWorkflowEventOutboxPumpHostedService",
                "OrcaCoreOutboxPumpHostedService");
    }
}
