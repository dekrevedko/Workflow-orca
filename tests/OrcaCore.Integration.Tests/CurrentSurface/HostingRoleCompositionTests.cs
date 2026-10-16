using AwesomeAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using OrcaCore.Hosting;
using OrcaCore.Durable.Hosting;
using OrcaCore.Providers.InMemory;

namespace OrcaCore.Integration.Tests.CurrentSurface;

public sealed class HostingRoleCompositionTests
{
    [Fact]
    public void DurableEngineAndEventIngress_ExposeOnlyTheirOwnedApplicationCapabilities()
    {
        var engineServices = new ServiceCollection();
        engineServices.AddOrcaCoreInMemoryDurableProvider();
        engineServices.AddOrcaCoreDurableEngine(DurableTestHosts.CreateOptions());

        using var engine = engineServices.BuildServiceProvider();
        engine.GetRequiredService<IWorkflowDefinitionRegistry>().Should().NotBeNull();
        engine.GetRequiredService<IWorkflowEventIngress>().Should().NotBeNull();
        engine.GetServices<IHostedService>().Should().NotBeEmpty();

        var ingressServices = new ServiceCollection();
        ingressServices.AddOrcaCoreInMemoryDurableProvider();
        ingressServices.AddOrcaCoreDurableEventIngress();

        using var ingress = ingressServices.BuildServiceProvider();
        ingress.GetRequiredService<IWorkflowEventIngress>().Should().NotBeNull();
        ingress.GetService<IWorkflowDefinitionRegistry>().Should().BeNull();
        ingress.GetServices<IHostedService>().Should().BeEmpty();

        var ephemeralServices = new ServiceCollection();
        ephemeralServices.AddOrcaCoreEphemeralEngine(new EphemeralEngineHostOptions
        {
            StructuredExecution = new StructuredExecutionHostOptions
            {
                MaxConcurrentExecutionPathsPerInstance = 1,
                StepThrottles = []
            },
            TransientPools = []
        });
        using var ephemeral = ephemeralServices.BuildServiceProvider();
        ephemeral.GetService<IWorkflowEventIngress>().Should().BeNull();
    }

    [Fact]
    public void DurableRoles_RequireOneProviderAndCannotBeCombined()
    {
        var missingProvider = new ServiceCollection();
        var noProvider = () =>
            missingProvider.AddOrcaCoreDurableEngine(DurableTestHosts.CreateOptions());
        noProvider.Should().Throw<InvalidOperationException>()
            .WithMessage("*provider role*");

        var combined = new ServiceCollection();
        combined.AddOrcaCoreInMemoryDurableProvider();
        combined.AddOrcaCoreDurableEngine(DurableTestHosts.CreateOptions());
        var addIngress = () => combined.AddOrcaCoreDurableEventIngress();

        addIngress.Should().Throw<InvalidOperationException>()
            .WithMessage("*cannot be combined*");
    }
}
