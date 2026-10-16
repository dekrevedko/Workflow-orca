using AwesomeAssertions;
using Microsoft.Extensions.DependencyInjection;
using OrcaCore.Hosting;
using Xunit;

namespace OrcaCore.Engine.Ephemeral.Tests.Execution;

public sealed class NamedStepDependencyInjectionTests
{
    [Fact]
    public async Task ResultfulEnd_CommitsOutputStatusAndFixedOutcomeTogether()
    {
        using var provider = CreateServices().BuildServiceProvider();
        var outcome = WorkflowOutcomeName.Create("approved");
        var definition = Workflow.Ephemeral<State>(
                DefinitionId.New(),
                DefinitionVersion.Initial)
            .Init<Input>(input => new State(input.Value))
            .End(snapshot => new Output(snapshot.Value.Value + 1), outcome)
            .Build();
        var handle = provider.GetRequiredService<IWorkflowDefinitionRegistry>()
            .Register(definition)
            .GetHandleOrThrow();

        var start = await handle.StartOrGetAsync(
            new Input(7),
            StartIdempotencyKey.Create("resultful-end"),
            TestContext.Current.CancellationToken);
        var instance = start.GetHandleOrThrow();
        var snapshot = await instance.GetSnapshotAsync(TestContext.Current.CancellationToken);
        var output = await instance.GetOutputAsync(TestContext.Current.CancellationToken);

        snapshot.Status.Should().Be(WorkflowInstanceStatus.Completed);
        snapshot.Outcome.Should().Be(outcome);
        output.Should().BeOfType<WorkflowOutputResult<Output>.Available>()
            .Which.Output.Should().Be(new Output(8));
    }

    [Fact]
    public async Task ResultfulEnd_RejectsUnapprovedPolymorphicOutputBeforeCompletion()
    {
        using var provider = CreateServices().BuildServiceProvider();
        var definition = Workflow.Ephemeral<State>(
                DefinitionId.New(),
                DefinitionVersion.Initial)
            .Init<Input>(input => new State(input.Value))
            .End<PolymorphicOutputBase>(
                snapshot => new PolymorphicOutputDerived(
                    snapshot.Value.Value,
                    "must-not-be-dropped"))
            .Build();
        var handle = provider.GetRequiredService<IWorkflowDefinitionRegistry>()
            .Register(definition)
            .GetHandleOrThrow();

        Func<Task> start = async () =>
        {
            _ = await handle.StartOrGetAsync(
                new Input(7),
                StartIdempotencyKey.Create("polymorphic-output"),
                TestContext.Current.CancellationToken);
        };

        await start.Should().ThrowAsync<NotSupportedException>()
            .WithMessage("*polymorphic*not supported*");
    }

    [Fact]
    public async Task ResultfulEnd_RejectsCyclicOutputBeforeCompletion()
    {
        using var provider = CreateServices().BuildServiceProvider();
        var definition = Workflow.Ephemeral<State>(
                DefinitionId.New(),
                DefinitionVersion.Initial)
            .Init<Input>(input => new State(input.Value))
            .End(_ =>
            {
                var output = new CyclicOutput();
                output.Next = output;
                return output;
            })
            .Build();
        var handle = provider.GetRequiredService<IWorkflowDefinitionRegistry>()
            .Register(definition)
            .GetHandleOrThrow();

        Func<Task> start = async () =>
        {
            _ = await handle.StartOrGetAsync(
                new Input(7),
                StartIdempotencyKey.Create("cyclic-output"),
                TestContext.Current.CancellationToken);
        };

        await start.Should().ThrowAsync<System.Text.Json.JsonException>()
            .WithMessage("*Cyclic payload graph*");
    }

    [Fact]
    public async Task PublicNamedStep_IsResolvedFromHostServicesAndMayUseConstructorDependencies()
    {
        var dependency = new ProbeDependency();
        var services = CreateServices();
        services.AddSingleton(dependency);
        services.AddTransient<ConstructorInjectedStep>();
        using var provider = services.BuildServiceProvider();
        var definition = Workflow.Ephemeral<State>(
                DefinitionId.New(),
                DefinitionVersion.Initial)
            .Init<Input>(input => new State(input.Value))
            .Then<ConstructorInjectedStep>()
            .End()
            .Build();
        var handle = provider.GetRequiredService<IWorkflowDefinitionRegistry>()
            .Register(definition)
            .GetHandleOrThrow();

        var start = await handle.StartOrGetAsync(
            new Input(7),
            StartIdempotencyKey.Create("named-step-di"),
            TestContext.Current.CancellationToken);

        (await start.GetHandleOrThrow().GetSnapshotAsync(
                TestContext.Current.CancellationToken))
            .Status.Should().Be(WorkflowInstanceStatus.Completed);
        dependency.ExecutionCount.Should().Be(1);
    }

    private static ServiceCollection CreateServices()
    {
        var services = new ServiceCollection();
        services.AddOrcaCoreEphemeralEngine(new EphemeralEngineHostOptions
        {
            StructuredExecution = new StructuredExecutionHostOptions
            {
                MaxConcurrentExecutionPathsPerInstance = 2,
                StepThrottles = []
            },
            TransientPools = []
        });
        return services;
    }

    private sealed record Input(int Value);
    private sealed record State(int Value);
    private sealed record Output(int Value);
    private record PolymorphicOutputBase(int Value);
    private sealed record PolymorphicOutputDerived(int Value, string Detail)
        : PolymorphicOutputBase(Value);

    private sealed class CyclicOutput
    {
        public CyclicOutput? Next { get; set; }
    }

    private sealed class ProbeDependency
    {
        internal int ExecutionCount { get; set; }
    }

    private sealed class ConstructorInjectedStep(ProbeDependency dependency) : IStep<State>
    {
        public ValueTask<StepResult> ExecuteAsync(
            StepContext<State> context,
            CancellationToken cancellationToken)
        {
            dependency.ExecutionCount++;
            return ValueTask.FromResult<StepResult>(new StepResult.Completed());
        }
    }
}
