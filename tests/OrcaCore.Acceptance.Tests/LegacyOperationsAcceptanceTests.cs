using AwesomeAssertions;
using Microsoft.Extensions.DependencyInjection;
using OrcaCore.Abstractions.Ids;
using OrcaCore.Engine.Ephemeral;
using OrcaCore.Hosting;
using OrcaCore.TestSupport;
using Xunit;

namespace OrcaCore.Acceptance.Tests;

/// <summary>
/// Preserves the superseded engine-local stuck-query contract. Task 7.7 removes these
/// list/query/lifecycle-remediation surfaces from the exact v1 application interface.
/// </summary>
public sealed class LegacyOperationsAcceptanceTests
{
    [Fact]
    public async Task StuckStep_IsSignalledAndQueryableByInstance()
    {
        var clock = new Clock(new DateTimeOffset(2026, 7, 2, 12, 0, 0, TimeSpan.Zero));
        var engine = new EphemeralWorkflowEngine(
            clock.TimeProvider,
            new EphemeralWorkflowEngineOptions
            {
                StuckStepThreshold = TimeSpan.FromSeconds(5)
            });
        using var provider = CreateProviderForExistingEngine(engine);
        var definition = Workflow
            .Ephemeral<TestState>(DefinitionId.New(), DefinitionVersion.Initial)
            .Init<string>(_ => new TestState())
            .Then(_ =>
            {
                clock.Advance(TimeSpan.FromSeconds(6));
                return ValueTask.CompletedTask;
            })
            .End()
            .Build();
        var definitionHandle = provider.GetRequiredService<IWorkflowDefinitionRegistry>()
            .Register(definition)
            .GetHandleOrThrow();

        var instance = (await definitionHandle.StartOrGetAsync(
                "start",
                StartIdempotencyKey.Create("stuck-step"),
                TestContext.Current.CancellationToken))
            .GetHandleOrThrow();
        var operational = engine.Management.Instance(instance.InstanceId);

        operational.Get().HasStuckStep.Should().BeTrue();
        operational.GetLifecycleEvents().Should().Contain(
            lifecycle => lifecycle.EventName == "StepStuckDetected");
    }

    [Fact]
    public async Task StuckInstance_IsSignalledAndQueryableByInstance()
    {
        var clock = new Clock(new DateTimeOffset(2026, 7, 2, 12, 0, 0, TimeSpan.Zero));
        var engine = new EphemeralWorkflowEngine(clock.TimeProvider);
        using var provider = CreateProviderForExistingEngine(engine);
        var definition = Workflow
            .Ephemeral<TestState>(DefinitionId.New(), DefinitionVersion.Initial)
            .Init<string>(_ => new TestState())
            .Wait(WorkflowEventContract.Create(EventName.Create("Ready"), EventContractVersion.Initial), _ => CorrelationId.Create("item-1"))
            .End()
            .Build();
        var definitionHandle = provider.GetRequiredService<IWorkflowDefinitionRegistry>()
            .Register(definition)
            .GetHandleOrThrow();
        var instance = (await definitionHandle.StartOrGetAsync(
                "start",
                StartIdempotencyKey.Create("stuck-instance"),
                TestContext.Current.CancellationToken))
            .GetHandleOrThrow();
        clock.Advance(TimeSpan.FromSeconds(6));

        engine.Management.All().DetectStuck(TimeSpan.FromSeconds(5));
        var operational = engine.Management.Instance(instance.InstanceId);

        operational.Get().IsStuck.Should().BeTrue();
        operational.GetLifecycleEvents().Should().Contain(
            lifecycle => lifecycle.EventName == "InstanceStuckDetected");
    }

    private static ServiceProvider CreateProviderForExistingEngine(
        EphemeralWorkflowEngine engine)
    {
        var services = new ServiceCollection();
        services.AddSingleton(engine);
        services.AddOrcaCoreEphemeralEngine(new EphemeralEngineHostOptions
        {
            StructuredExecution = new StructuredExecutionHostOptions
            {
                MaxConcurrentExecutionPathsPerInstance = 4,
                StepThrottles = []
            },
            TransientPools = []
        });
        return services.BuildServiceProvider();
    }

    public sealed class TestState;
}
