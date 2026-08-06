using AwesomeAssertions;
using Microsoft.Extensions.DependencyInjection;
using OrcaCore.Abstractions.Ids;
using OrcaCore.Hosting;
using OrcaCore.Providers.InMemory;
using Xunit;

namespace OrcaCore.Acceptance.Tests;

public sealed class ContinueAsNewAcceptanceTests
{
    [Fact]
    [Trait("AC", "AC-313")]
    public async Task ContinueAsNew_DurableInstance_RemainsQueryableByOriginalIdentity()
    {
        using var host = CreateHost();
        var definition = Workflow.Durable<ContinuationState>(
                DefinitionId.New(),
                DefinitionVersion.Initial)
            .Init<string>(_ => new ContinuationState(Generation: 0))
            .If(
                snapshot => snapshot.Value.Generation > 0,
                resumed => resumed.Wait(
                    WorkflowEventContract.Create(EventName.Create("continue-as-new-hold"), EventContractVersion.Initial),
                    _ => CorrelationId.Create("continued-generation")))
            .ContinueAsNew(snapshot => snapshot.Value with { Generation = 1 })
            .Build();
        var handle = host.GetRequiredService<IWorkflowDefinitionRegistry>()
            .Register(definition)
            .GetHandleOrThrow();

        var started = await handle.StartOrGetAsync(
            "input",
            StartIdempotencyKey.Create("continue-as-new-identity"),
            TestContext.Current.CancellationToken);
        var originalInstanceId = started.GetHandleOrThrow().InstanceId;
        var reopened = await handle.GetInstanceAsync(
            originalInstanceId,
            TestContext.Current.CancellationToken);
        var snapshot = await reopened.GetSnapshotAsync(TestContext.Current.CancellationToken);
        var state = await reopened.GetStateAsync<ContinuationState>(
            TestContext.Current.CancellationToken);

        reopened.InstanceId.Should().Be(originalInstanceId);
        snapshot.InstanceId.Should().Be(originalInstanceId);
        snapshot.Status.Should().Be(WorkflowStatus.Waiting);
        state.Should().Be(new ContinuationState(Generation: 1));
    }

    private static ServiceProvider CreateHost()
    {
        var services = new ServiceCollection();
        services.AddOrcaCoreInMemoryDurableProvider();
        services.AddOrcaCoreDurableEngine(new DurableEngineHostOptions
        {
            StructuredExecution = new StructuredExecutionHostOptions
            {
                MaxConcurrentExecutionPathsPerInstance = 4,
                StepThrottles = []
            },
            ResourcePools = new DurableResourcePoolOptions
            {
                PartitionId = ResourceGovernancePartitionId.Create(
                    $"acceptance-continue-{Guid.NewGuid():N}"),
                Pools = []
            }
        });
        return services.BuildServiceProvider();
    }

    public sealed record ContinuationState(int Generation);
}
