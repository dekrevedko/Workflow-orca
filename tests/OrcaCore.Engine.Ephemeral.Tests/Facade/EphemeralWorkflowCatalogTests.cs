using AwesomeAssertions;
using Microsoft.Extensions.DependencyInjection;
using OrcaCore.Hosting;
using Xunit;

namespace OrcaCore.Engine.Ephemeral.Tests.Facade;

public sealed class EphemeralWorkflowCatalogTests
{
    [Fact]
    public void Builder_StagesBothDefinitionShapesAndExactReferencesResolve()
    {
        var resultless = ResultlessDefinition(DefinitionId.New());
        var resultful = ResultfulDefinition(DefinitionId.New());
        var services = new ServiceCollection();
        var builder = services.AddOrcaCoreEphemeralEngine(HostOptions());

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
        var builder = services.AddOrcaCoreEphemeralEngine(HostOptions());

        builder.AddWorkflow(versionOne).AddWorkflow(versionTwo);

        await using var provider = services.BuildServiceProvider();
        var registry = provider.GetRequiredService<IWorkflowDefinitionRegistry>();
        var first = registry.GetRequiredHandle(versionOne.Reference);
        var second = registry.GetRequiredHandle(versionTwo.Reference);

        first.DefinitionVersion.Should().Be(new DefinitionVersion(1));
        second.DefinitionVersion.Should().Be(new DefinitionVersion(2));
        (await first.StartOrGetAsync(
            new CatalogInput(1),
            StartIdempotencyKey.Create("catalog-version-one"),
            TestContext.Current.CancellationToken))
            .Should().BeOfType<WorkflowStartResult<WorkflowInstanceHandle>.Accepted>();
        (await second.StartOrGetAsync(
            new CatalogInput(2),
            StartIdempotencyKey.Create("catalog-version-two"),
            TestContext.Current.CancellationToken))
            .Should().BeOfType<WorkflowStartResult<WorkflowInstanceHandle>.Accepted>();
    }

    [Fact]
    public void ExactLookup_NeverRegistersAndRejectsMissingOrStaleReferences()
    {
        var definitionId = DefinitionId.New();
        var installed = ResultlessDefinition(definitionId);
        var stale = Workflow.Ephemeral<CatalogState>(definitionId, DefinitionVersion.Initial)
            .Init<CatalogInput>(input => new CatalogState(input.Value))
            .Delay(TimeSpan.FromSeconds(1))
            .End()
            .Build();
        var direct = ResultlessDefinition(DefinitionId.New());
        var services = new ServiceCollection();
        services.AddOrcaCoreEphemeralEngine(HostOptions()).AddWorkflow(installed);

        using var provider = services.BuildServiceProvider();
        var registry = provider.GetRequiredService<IWorkflowDefinitionRegistry>();

        Action missing = () => registry.GetRequiredHandle(direct.Reference);
        Action staleLookup = () => registry.GetRequiredHandle(stale.Reference);

        var missingFailure = missing.Should().Throw<WorkflowDefinitionNotRegisteredException>().Which;
        missingFailure.Code.Should().Be("WF-DEFINITION-NOT-REGISTERED");
        missingFailure.DefinitionId.Should().Be(direct.Reference.DefinitionId);
        missingFailure.DefinitionVersion.Should().Be(direct.Reference.DefinitionVersion);
        missingFailure.DefinitionFingerprint.Should().Be(direct.Reference.DefinitionFingerprint);

        var staleFailure = staleLookup.Should().Throw<WorkflowDefinitionNotRegisteredException>().Which;
        staleFailure.DefinitionId.Should().Be(stale.Reference.DefinitionId);
        staleFailure.DefinitionVersion.Should().Be(stale.Reference.DefinitionVersion);
        staleFailure.DefinitionFingerprint.Should().Be(stale.Reference.DefinitionFingerprint);
        registry.Register(direct).GetHandleOrThrow().Should().BeSameAs(
            registry.GetRequiredHandle(direct.Reference));
    }

    [Fact]
    public void StagedBatch_PreflightsEveryDefinitionBeforeInstallingAny()
    {
        using var provider = CreateServices().BuildServiceProvider();
        var registry = new EphemeralWorkflowDefinitionRegistry(
            provider.GetRequiredService<EphemeralWorkflowEngine>());
        var clean = ResultlessDefinition(DefinitionId.New());
        var missingPool = Workflow.Ephemeral<CatalogState>(
                clean.DefinitionId,
                DefinitionVersion.Initial)
            .Init<CatalogInput>(input => new CatalogState(input.Value))
            .Then<CatalogStep>()
            .WithTransientPool(TransientPoolName.Create("zeta"))
            .Then<CatalogStep>()
            .WithTransientPool(TransientPoolName.Create("alpha"))
            .Then<CatalogStep>()
            .WithTransientPool(TransientPoolName.Create("zeta"))
            .End()
            .Build();
        IEphemeralStagedWorkflowDefinition[] staged =
        [
            new EphemeralStagedWorkflowDefinition<CatalogInput>(clean),
            new EphemeralStagedWorkflowDefinition<CatalogInput>(missingPool)
        ];

        Action install = () => registry.InstallStagedBatch(staged);

        var failure = install.Should().Throw<WorkflowDefinitionHostCompatibilityException>()
            .Which.Failure.Should()
            .BeOfType<DefinitionHostCompatibilityFailure.MissingTransientPools>()
            .Which;
        failure.PoolNames.Select(pool => pool.Value).Should().Equal("alpha", "zeta");
        Action lookupClean = () => registry.GetRequiredHandle(clean.Reference);
        lookupClean.Should().Throw<WorkflowDefinitionNotRegisteredException>();
    }

    [Fact]
    public void StagedBatch_RejectsFingerprintConflictWithoutInstallingAnyDefinition()
    {
        using var provider = CreateServices().BuildServiceProvider();
        var registry = new EphemeralWorkflowDefinitionRegistry(
            provider.GetRequiredService<EphemeralWorkflowEngine>());
        var clean = ResultlessDefinition(DefinitionId.New());
        var conflicting = Workflow.Ephemeral<CatalogState>(
                clean.DefinitionId,
                clean.DefinitionVersion)
            .Init<CatalogInput>(input => new CatalogState(input.Value))
            .Delay(TimeSpan.FromSeconds(1))
            .End()
            .Build();
        IEphemeralStagedWorkflowDefinition[] staged =
        [
            new EphemeralStagedWorkflowDefinition<CatalogInput>(clean),
            new EphemeralStagedWorkflowDefinition<CatalogInput>(conflicting)
        ];

        Action install = () => registry.InstallStagedBatch(staged);

        install.Should().Throw<WorkflowDefinitionRegistrationConflictException>();
        Action lookupClean = () => registry.GetRequiredHandle(clean.Reference);
        lookupClean.Should().Throw<WorkflowDefinitionNotRegisteredException>();
    }

    private static ServiceCollection CreateServices()
    {
        var services = new ServiceCollection();
        services.AddOrcaCoreEphemeralEngine(HostOptions());
        return services;
    }

    private static EphemeralEngineHostOptions HostOptions() => new()
    {
        StructuredExecution = new StructuredExecutionHostOptions
        {
            MaxConcurrentExecutionPathsPerInstance = 2,
            StepThrottles = []
        },
        TransientPools = []
    };

    private static EphemeralWorkflowDefinition<CatalogInput> ResultlessDefinition(
        DefinitionId definitionId,
        DefinitionVersion? definitionVersion = null) =>
        Workflow.Ephemeral<CatalogState>(
                definitionId,
                definitionVersion ?? DefinitionVersion.Initial)
            .Init<CatalogInput>(input => new CatalogState(input.Value))
            .End()
            .Build();

    private static EphemeralWorkflowDefinition<CatalogInput, CatalogOutput> ResultfulDefinition(
        DefinitionId definitionId) =>
        Workflow.Ephemeral<CatalogState>(definitionId, DefinitionVersion.Initial)
            .Init<CatalogInput>(input => new CatalogState(input.Value))
            .End(snapshot => new CatalogOutput(snapshot.Value.Value))
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
