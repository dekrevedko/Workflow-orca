using AwesomeAssertions;
using Microsoft.Extensions.DependencyInjection;
using OrcaCore.Engine.Durable.Definitions;
using OrcaCore.Engine.Durable.Execution;
using OrcaCore.Hosting;
using OrcaCore.Providers.InMemory;
using Xunit;

namespace OrcaCore.Engine.Durable.Tests.Facade;

public sealed class DurableWorkflowCatalogTests
{
    [Fact]
    public void Builder_StagesBothDefinitionShapesAndExactReferencesResolve()
    {
        var resultless = ResultlessDefinition(DefinitionId.New());
        var resultful = ResultfulDefinition(DefinitionId.New());
        var services = new ServiceCollection();
        services.AddOrcaCoreInMemoryDurableProvider();
        var builder = services.AddOrcaCoreDurableEngine(HostOptions("configured"));

        builder.AddWorkflow(resultless).Should().BeSameAs(builder);
        builder.AddWorkflow(ResultlessDefinition(resultless.DefinitionId)).Should().BeSameAs(builder);
        builder.AddWorkflow(resultful).Should().BeSameAs(builder);

        using var provider = services.BuildServiceProvider();
        var registry = provider.GetRequiredService<IWorkflowDefinitionRegistry>();

        registry.GetRequiredHandle(resultless.Reference).DefinitionId
            .Should().Be(resultless.DefinitionId);
        registry.GetRequiredHandle(resultful.Reference).DefinitionId
            .Should().Be(resultful.DefinitionId);
    }

    [Fact]
    public async Task Builder_StagesMultipleVersionsOfTheSameDefinitionIdAtomically()
    {
        var definitionId = DefinitionId.New();
        var versionOne = ResultlessDefinition(definitionId, new DefinitionVersion(1));
        var versionTwo = ResultlessDefinition(definitionId, new DefinitionVersion(2));
        var services = new ServiceCollection();
        services.AddOrcaCoreInMemoryDurableProvider();
        var builder = services.AddOrcaCoreDurableEngine(HostOptions("configured"));

        builder.AddWorkflow(versionOne).AddWorkflow(versionTwo);

        await using var provider = services.BuildServiceProvider();
        var registry = provider.GetRequiredService<IWorkflowDefinitionRegistry>();
        var first = registry.GetRequiredHandle(versionOne.Reference);
        var second = registry.GetRequiredHandle(versionTwo.Reference);

        first.DefinitionVersion.Should().Be(new DefinitionVersion(1));
        second.DefinitionVersion.Should().Be(new DefinitionVersion(2));
        (await first.StartOrGetAsync(
            new CatalogInput(1),
            StartIdempotencyKey.Create("durable-catalog-version-one"),
            TestContext.Current.CancellationToken))
            .Should().BeOfType<WorkflowStartResult<WorkflowInstanceHandle>.Accepted>();
        (await second.StartOrGetAsync(
            new CatalogInput(2),
            StartIdempotencyKey.Create("durable-catalog-version-two"),
            TestContext.Current.CancellationToken))
            .Should().BeOfType<WorkflowStartResult<WorkflowInstanceHandle>.Accepted>();
    }

    [Fact]
    public void ExactLookup_RejectsAnotherModeAndDoesNotRegisterIt()
    {
        var durable = ResultlessDefinition(DefinitionId.New());
        var ephemeral = Workflow.Ephemeral<CatalogState>(
                DefinitionId.New(),
                DefinitionVersion.Initial)
            .Init<CatalogInput>(input => new CatalogState(input.Value))
            .End()
            .Build();
        var services = new ServiceCollection();
        services.AddOrcaCoreInMemoryDurableProvider();
        services.AddOrcaCoreDurableEngine(HostOptions("configured"))
            .AddWorkflow(durable);

        using var provider = services.BuildServiceProvider();
        var registry = provider.GetRequiredService<IWorkflowDefinitionRegistry>();

        Action lookup = () => registry.GetRequiredHandle(ephemeral.Reference);

        var failure = lookup.Should().Throw<WorkflowDefinitionNotRegisteredException>().Which;
        failure.Code.Should().Be("WF-DEFINITION-NOT-REGISTERED");
        failure.DefinitionId.Should().Be(ephemeral.Reference.DefinitionId);
        failure.DefinitionVersion.Should().Be(ephemeral.Reference.DefinitionVersion);
        failure.DefinitionFingerprint.Should().Be(ephemeral.Reference.DefinitionFingerprint);
        registry.Register(ephemeral).Should()
            .BeOfType<WorkflowRegistrationResult<EphemeralDefinitionHandle<CatalogInput>>.HostIncompatible>();
    }

    [Fact]
    public void StagedBatch_PreflightsEveryDefinitionBeforeInstallingAny()
    {
        var registry = CreateRegistry("configured");
        var clean = ResultlessDefinition(DefinitionId.New());
        var missingPool = DurableDefinitionUsingPool(clean.DefinitionId, "missing");
        IDurableStagedWorkflowDefinition[] staged =
        [
            new DurableStagedWorkflowDefinition<CatalogInput>(clean),
            new DurableStagedWorkflowDefinition<CatalogInput>(missingPool)
        ];

        Action install = () => registry.InstallStagedBatch(staged);

        install.Should().Throw<WorkflowDefinitionHostCompatibilityException>()
            .Which.Failure.Should()
            .BeOfType<DefinitionHostCompatibilityFailure.MissingDurableResourcePools>();
        Action lookupClean = () => registry.GetRequiredHandle(clean.Reference);
        lookupClean.Should().Throw<WorkflowDefinitionNotRegisteredException>();
    }

    [Fact]
    public void StagedBatch_RejectsFingerprintConflictWithoutInstallingAnyDefinition()
    {
        var registry = CreateRegistry("configured");
        var clean = ResultlessDefinition(DefinitionId.New());
        var conflicting = Workflow.Durable<CatalogState>(
                clean.DefinitionId,
                clean.DefinitionVersion)
            .Init<CatalogInput>(input => new CatalogState(input.Value))
            .Delay(TimeSpan.FromSeconds(1))
            .End()
            .Build();
        IDurableStagedWorkflowDefinition[] staged =
        [
            new DurableStagedWorkflowDefinition<CatalogInput>(clean),
            new DurableStagedWorkflowDefinition<CatalogInput>(conflicting)
        ];

        Action install = () => registry.InstallStagedBatch(staged);

        install.Should().Throw<WorkflowDefinitionRegistrationConflictException>();
        Action lookupClean = () => registry.GetRequiredHandle(clean.Reference);
        lookupClean.Should().Throw<WorkflowDefinitionNotRegisteredException>();
    }

    [Fact]
    public void CallbackOnlyIngress_ExposesNoCatalogAndRejectsEngineStaging()
    {
        var services = new ServiceCollection();
        services.AddOrcaCoreInMemoryDurableProvider();
        services.AddOrcaCoreDurableEventIngress().Should().BeSameAs(services);

        Action addEngine = () => services.AddOrcaCoreDurableEngine(HostOptions("configured"));

        addEngine.Should().Throw<InvalidOperationException>();
        using var provider = services.BuildServiceProvider();
        provider.GetService<IWorkflowDefinitionRegistry>().Should().BeNull();
    }

    private static DurableWorkflowDefinitionRegistry CreateRegistry(params string[] configuredPools)
    {
        var store = new InMemoryWorkflowProvider();
        var notifications = new DurableFacadeNotificationHub();
        var processor = new DurableCommandProcessor(store, runtimeObserver: notifications);
        var runtime = new DurableWorkflowRuntime(
            processor,
            new DurableDefinitionRegistry(),
            TimeProvider.System,
            projectionStore: store);
        return new DurableWorkflowDefinitionRegistry(
            runtime,
            store,
            store,
            processor,
            notifications,
            TimeProvider.System,
            configuredPools.Select(ResourcePoolName.Create));
    }

    private static DurableEngineHostOptions HostOptions(params string[] configuredPools) => new()
    {
        StructuredExecution = new StructuredExecutionHostOptions
        {
            MaxConcurrentExecutionPathsPerInstance = 2,
            StepThrottles = []
        },
        ResourcePools = new DurableResourcePoolOptions
        {
            PartitionId = ResourceGovernancePartitionId.Create("catalog-tests"),
            Pools = configuredPools.Select(pool => DurableResourcePoolDefinition.Create(
                    ResourcePoolName.Create(pool),
                    2,
                    TimeSpan.FromMinutes(5)))
                .ToArray()
        }
    };

    private static DurableWorkflowDefinition<CatalogInput> ResultlessDefinition(
        DefinitionId definitionId,
        DefinitionVersion? definitionVersion = null) =>
        Workflow.Durable<CatalogState>(
                definitionId,
                definitionVersion ?? DefinitionVersion.Initial)
            .Init<CatalogInput>(input => new CatalogState(input.Value))
            .End()
            .Build();

    private static DurableWorkflowDefinition<CatalogInput, CatalogOutput> ResultfulDefinition(
        DefinitionId definitionId) =>
        Workflow.Durable<CatalogState>(definitionId, DefinitionVersion.Initial)
            .Init<CatalogInput>(input => new CatalogState(input.Value))
            .End(snapshot => new CatalogOutput(snapshot.Value.Value))
            .Build();

    private static DurableWorkflowDefinition<CatalogInput> DurableDefinitionUsingPool(
        DefinitionId definitionId,
        string pool) =>
        Workflow.Durable<CatalogState>(definitionId, DefinitionVersion.Initial)
            .Init<CatalogInput>(input => new CatalogState(input.Value))
            .AcquireResources(
                ResourceLeaseRequest.Create(
                    ResourceLeaseRequirement.Require(ResourcePoolName.Create(pool))),
                lease => lease.Then<CatalogStep>())
            .End()
            .Build();

    private sealed record CatalogInput(int Value);
    private sealed record CatalogState(int Value);
    private sealed record CatalogOutput(int Value);

    private sealed class CatalogStep : IStep<CatalogState>
    {
        public ValueTask<StepResult> ExecuteAsync(
            StepContext<CatalogState> context,
            CancellationToken cancellationToken) =>
            ValueTask.FromResult<StepResult>(new StepResult.Completed());
    }
}
