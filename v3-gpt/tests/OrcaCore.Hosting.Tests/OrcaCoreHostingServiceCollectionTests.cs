using AwesomeAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using OrcaCore.Abstractions.Providers;
using OrcaCore.Engine.Durable.Execution;
using OrcaCore.Engine.Durable.Management;
using OrcaCore.Engine.Ephemeral;
using OrcaCore.Hosting;
using OrcaCore.Hosting.Services;
using OrcaCore.Providers.InMemory;
using OrcaCore.Providers.RabbitMq;
using Xunit;

namespace OrcaCore.Hosting.Tests;

public sealed class OrcaCoreHostingServiceCollectionTests
{
    [Fact]
    public void AddOrcaCore_RegistersCoreEnginesAndInMemoryDefaults()
    {
        var services = new ServiceCollection();

        services.AddOrcaCore();

        using var provider = services.BuildServiceProvider();
        provider.GetRequiredService<EphemeralWorkflowEngine>().Should().NotBeNull();
        provider.GetRequiredService<DurableCommandProcessor>().Should().NotBeNull();
        provider.GetRequiredService<DurableManagement>().Should().NotBeNull();
        provider.GetRequiredService<IWorkflowEventStore>().Should().BeOfType<InMemoryWorkflowProvider>();
        provider.GetRequiredService<IWorkflowOutboxStore>().Should().BeOfType<InMemoryWorkflowProvider>();
        provider.GetRequiredService<IMessageDispatcher>().Should().BeOfType<InMemoryWorkflowProvider>();
    }

    [Fact]
    public void AddOrcaCoreRabbitMq_RegistersDispatcherWithoutScanningAssemblies()
    {
        var services = new ServiceCollection();

        services
            .AddOrcaCore()
            .AddOrcaCoreRabbitMq(new RabbitMqMessageDispatcherOptions
            {
                ConnectionString = "amqp://guest:guest@localhost:5672/",
                ExchangeName = "orca"
            });

        using var provider = services.BuildServiceProvider();
        provider.GetRequiredService<IMessageDispatcher>().Should().BeOfType<RabbitMqMessageDispatcher>();
    }

    [Fact]
    public void AddOrcaCoreHostedServices_RegistersPumpTimerAndSweepServices()
    {
        var services = new ServiceCollection();

        services.AddOrcaCore();
        services.Should().NotContain(descriptor => descriptor.ServiceType == typeof(IHostedService));

        services.AddOrcaCoreHostedServices();

        using var provider = services.BuildServiceProvider();
        var hostedServices = provider.GetServices<IHostedService>().ToArray();
        hostedServices.Should().ContainSingle(service => service is OrcaCoreOutboxPumpHostedService);
        hostedServices.Should().ContainSingle(service => service is OrcaCoreTimerHostedService);
        hostedServices.Should().ContainSingle(service => service is OrcaCoreOperationalSweepHostedService);
    }
}
