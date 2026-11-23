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

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task PendingDurableOutputWaitEndsWhenItsObservingHostStops(bool useGenericHost)
    {
        var token = TestContext.Current.CancellationToken;
        var application = useGenericHost ? Host.CreateApplicationBuilder() : null;
        var services = application?.Services ?? new ServiceCollection();
        services.AddLogging();
        services.AddOrcaCoreInMemoryDurableProvider();
        services.AddOrcaCoreDurableEngine(HostOptions());
        using var host = application?.Build();
        using var standalone = host is null ? services.BuildServiceProvider() : null;
        var provider = host?.Services ?? standalone!;
        if (host is not null)
        {
            await host.StartAsync(token);
        }

        var definition = Workflow.Durable<int>(DefinitionId.New(), DefinitionVersion.Initial)
            .Init<int>(value => value)
            .Wait(WorkflowEventContract.Create(EventName.Create("later"), EventContractVersion.Initial),
                _ => CorrelationId.Create("pending-output"))
            .End(state => state.Value).Build();
        var registry = provider.GetRequiredService<IWorkflowDefinitionRegistry>();
        var registered = registry.Register(definition).GetHandleOrThrow();
        var start = await registered.StartOrGetAsync(17, StartIdempotencyKey.Create("pending-output"), token);
        using var caller = CancellationTokenSource.CreateLinkedTokenSource(token);
        try
        {
            var pending = start.WaitForOutputAsync(caller.Token).AsTask();
            Assert.False(pending.IsCompleted);
            if (host is not null)
            {
                await host.StopAsync(token);
            }
            else
            {
                standalone!.Dispose();
            }

            await Assert.ThrowsAnyAsync<OperationCanceledException>(
                () => pending.WaitAsync(TimeSpan.FromSeconds(5), token));
            Assert.False(caller.IsCancellationRequested);
        }
        finally
        {
            await caller.CancelAsync();
        }
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
