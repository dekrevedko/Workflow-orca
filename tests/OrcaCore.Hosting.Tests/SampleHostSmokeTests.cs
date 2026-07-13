using AwesomeAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using OrcaCore.Abstractions.Providers;
using OrcaCore.Engine.Durable.Execution;
using OrcaCore.Engine.Durable.Management;
using OrcaCore.Engine.Ephemeral;
using OrcaCore.Hosting.Services;
using OrcaCore.SampleHost;
using Xunit;

namespace OrcaCore.Hosting.Tests;

public sealed class SampleHostSmokeTests
{
    [Fact]
    public void SampleHost_StartsWithInMemoryProvider_ResolvesHostedServices()
    {
        using var host = SampleHostApplication.Build([]);

        host.Services.GetRequiredService<EphemeralWorkflowEngine>().Should().NotBeNull();
        host.Services.GetRequiredService<DurableCommandProcessor>().Should().NotBeNull();
        host.Services.GetRequiredService<DurableManagement>().Should().NotBeNull();
        host.Services.GetRequiredService<IWorkflowEventStore>().Should().NotBeNull();
        host.Services.GetRequiredService<IWorkflowProjectionStore>().Should().NotBeNull();
        host.Services.GetServices<IHostedService>()
            .Should()
            .ContainSingle(service => service is OrcaCoreOutboxPumpHostedService)
            .And.ContainSingle(service => service is OrcaCoreTimerHostedService)
            .And.ContainSingle(service => service is OrcaCoreOperationalSweepHostedService);
    }
}
