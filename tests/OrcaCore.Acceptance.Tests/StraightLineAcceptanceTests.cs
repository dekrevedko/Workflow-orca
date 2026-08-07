using Microsoft.Extensions.DependencyInjection;
using AwesomeAssertions;
using OrcaCore.Abstractions.Errors;
using OrcaCore.Abstractions.Ids;
using OrcaCore.Core.Building;
using OrcaCore.Engine.Ephemeral;
using OrcaCore.Hosting;
using Xunit;

namespace OrcaCore.Acceptance.Tests;

public sealed class StraightLineAcceptanceTests
{
    [Fact]
    [Trait("AC", "AC-001")]
    public async Task StraightLine_Completes_StateReflectsSteps()
    {
        using var provider = PublicAcceptanceHost.CreateEphemeralProvider();
        var registry = provider.GetRequiredService<IWorkflowDefinitionRegistry>();
        var definition = global::OrcaCore.Workflow.Ephemeral<StraightLineState>(DefinitionId.New(), DefinitionVersion.Initial)
            .Init<string>(_ => new StraightLineState())
            .Then(context =>
            {
                context.State.Sink.Add("first");
                return ValueTask.CompletedTask;
            })
            .Then(context =>
            {
                context.State.Sink.Add("second");
                return ValueTask.CompletedTask;
            })
            .End()
            .Build();

        var definitionHandle = registry.Register(definition).GetHandleOrThrow();
        var instance = (await definitionHandle.StartOrGetAsync(
            "start",
            StartIdempotencyKey.Create("straight-line"),
            TestContext.Current.CancellationToken));
        var instanceHandle = instance.GetHandleOrThrow();
        var snapshot = await instanceHandle.GetSnapshotAsync(TestContext.Current.CancellationToken);
        var state = await instanceHandle.GetStateAsync<StraightLineState>(TestContext.Current.CancellationToken);

        snapshot.Status.Should().Be(WorkflowInstanceStatus.Completed);
        state.Sink.Should().Equal(["first", "second"]);
    }

    [Fact]
    [Trait("AC", "AC-004")]
    public async Task FailingStep_FailsInstance_ErrorInspectable()
    {
        using var provider = PublicAcceptanceHost.CreateEphemeralProvider(
            services => services.AddTransient<FailingStep>());
        var registry = provider.GetRequiredService<IWorkflowDefinitionRegistry>();
        var definition = global::OrcaCore.Workflow.Ephemeral<StraightLineState>(DefinitionId.New(), DefinitionVersion.Initial)
            .Init<string>(_ => new StraightLineState())
            .Then<FailingStep>()
            .End()
            .Build();

        var definitionHandle = registry.Register(definition).GetHandleOrThrow();
        var instance = (await definitionHandle.StartOrGetAsync(
            "start",
            StartIdempotencyKey.Create("failing-step"),
            TestContext.Current.CancellationToken));
        var snapshot = await instance.GetHandleOrThrow()
            .GetSnapshotAsync(TestContext.Current.CancellationToken);

        snapshot.Status.Should().Be(WorkflowInstanceStatus.Failed);
        snapshot.Failure.Should().NotBeNull();
        snapshot.Failure!.Message.Should().Contain("acceptance failure");
    }

    public sealed record StraightLineState
    {
        public List<string> Sink { get; init; } = [];
    }

    public sealed class FailingStep : IStep<StraightLineState>
    {
        public ValueTask<StepResult> ExecuteAsync(
            StepContext<StraightLineState> context,
            CancellationToken cancellationToken) =>
            ValueTask.FromResult<StepResult>(
                new StepResult.Failed(new WorkflowLifecycleException("acceptance failure")));
    }
}

internal static class PublicAcceptanceHost
{
    internal static ServiceProvider CreateEphemeralProvider(
        Action<IServiceCollection>? configure = null,
        TimeProvider? timeProvider = null)
    {
        var services = new ServiceCollection();
        configure?.Invoke(services);
        if (timeProvider is not null)
        {
            services.AddSingleton(timeProvider);
        }

        services.AddOrcaCoreEphemeralEngine(new EphemeralEngineHostOptions
        {
            StructuredExecution = new StructuredExecutionHostOptions
            {
                MaxConcurrentExecutionPathsPerInstance = 8,
                StepThrottles = []
            },
            TransientPools = []
        });
        services.AddSingleton<ProcessLocalEventRouter>();
        return services.BuildServiceProvider();
    }
}
