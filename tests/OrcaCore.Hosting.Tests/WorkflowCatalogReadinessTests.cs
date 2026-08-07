using AwesomeAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using OrcaCore.Hosting.Services;
using OrcaCore.Providers.InMemory;
using Xunit;

namespace OrcaCore.Hosting.Tests;

public sealed class WorkflowCatalogReadinessTests
{
    [Fact]
    public void InboxContinuationPump_IsOwnedOnlyByTheDefinitionEngineRole()
    {
        var engine = new ServiceCollection();
        engine.AddOrcaCoreInMemoryDurableProvider();
        engine.AddOrcaCoreDurableEngine(HostOptions());
        var callback = new ServiceCollection();
        callback.AddOrcaCoreInMemoryDurableProvider();
        callback.AddOrcaCoreDurableEventIngress();

        engine.Select(descriptor => descriptor.ServiceType.FullName).Should().Contain(
            "OrcaCore.Engine.Durable.Driver.DurableInboxContinuationPump");
        callback.Select(descriptor => descriptor.ServiceType.FullName).Should().NotContain(
            "OrcaCore.Engine.Durable.Driver.DurableInboxContinuationPump");
        callback.Where(descriptor => descriptor.ServiceType == typeof(IHostedService)).Should().BeEmpty();
    }

    [Fact]
    public async Task InboxSweepFailure_DoesNotGateTheSharedContinuationLane()
    {
        var continuationRan = false;

        await OrcaCoreContinuationPumpHostedService.RunPumpsOnceAsync(
            _ => Task.FromException(new InvalidOperationException("Injected inbox sweep failure.")),
            _ =>
            {
                continuationRan = true;
                return Task.CompletedTask;
            },
            NullLogger<OrcaCoreContinuationPumpHostedService>.Instance,
            TestContext.Current.CancellationToken);

        continuationRan.Should().BeTrue();
    }

    [Fact]
    public async Task DurableHost_RejectsAConflictingStagedBatchAtReadiness()
    {
        var definitionId = DefinitionId.New();
        var first = Workflow.Durable<CatalogState>(definitionId, DefinitionVersion.Initial)
            .Init<CatalogInput>(input => new CatalogState(input.Value))
            .End()
            .Build();
        var conflicting = Workflow.Durable<CatalogState>(definitionId, DefinitionVersion.Initial)
            .Init<CatalogInput>(input => new CatalogState(input.Value))
            .Delay(TimeSpan.FromSeconds(1))
            .End()
            .Build();
        var application = Host.CreateApplicationBuilder();
        application.Logging.ClearProviders();
        application.Services.AddOrcaCoreInMemoryDurableProvider();
        application.Services.AddOrcaCoreDurableEngine(HostOptions())
            .AddWorkflow(first)
            .AddWorkflow(conflicting);
        using var host = application.Build();

        Func<Task> start = () => host.StartAsync(TestContext.Current.CancellationToken);

        await start.Should().ThrowAsync<WorkflowDefinitionRegistrationConflictException>();
    }

    private static DurableEngineHostOptions HostOptions() => new()
    {
        StructuredExecution = new StructuredExecutionHostOptions
        {
            MaxConcurrentExecutionPathsPerInstance = 2,
            StepThrottles = []
        },
        ResourcePools = new DurableResourcePoolOptions
        {
            PartitionId = ResourceGovernancePartitionId.Create("catalog-readiness-tests"),
            Pools = []
        }
    };

    private sealed record CatalogInput(int Value);
    private sealed record CatalogState(int Value);
}
