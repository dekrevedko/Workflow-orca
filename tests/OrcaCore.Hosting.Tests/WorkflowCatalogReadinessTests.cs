using AwesomeAssertions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using OrcaCore.Providers.InMemory;
using Xunit;

namespace OrcaCore.Hosting.Tests;

public sealed class WorkflowCatalogReadinessTests
{
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
