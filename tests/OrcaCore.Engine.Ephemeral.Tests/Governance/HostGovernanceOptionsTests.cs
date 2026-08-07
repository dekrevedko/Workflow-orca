using AwesomeAssertions;
using Microsoft.Extensions.DependencyInjection;
using OrcaCore.Hosting;
using Xunit;

namespace OrcaCore.Engine.Ephemeral.Tests.Governance;

public sealed class HostGovernanceOptionsTests
{
    [Fact]
    public void StepThrottle_RejectsNonpositiveCapacityAndNonStepTypes()
    {
        Action invalidCapacity = () => StepExecutionThrottle.For<NamedStep>(0);
        Action invalidStep = () => StepExecutionThrottle.For<string>(1);

        invalidCapacity.Should().Throw<ArgumentOutOfRangeException>();
        invalidStep.Should().Throw<ArgumentException>()
            .WithMessage("*must implement IStep*");
    }

    [Fact]
    public void EphemeralOptions_ExposeTypedCatalogsAndRejectDuplicatePools()
    {
        var database = TransientPoolName.Create("database");
        var throttle = StepExecutionThrottle.For<NamedStep>(1);
        var pool = TransientPoolDefinition.Create(database, 3);

        throttle.StepType.Should().Be(typeof(NamedStep));
        throttle.MaxConcurrency.Should().Be(1);
        pool.Name.Should().Be(database);
        pool.Capacity.Should().Be(3);

        var duplicate = new EphemeralEngineHostOptions
        {
            StructuredExecution = new StructuredExecutionHostOptions
            {
                MaxConcurrentExecutionPathsPerInstance = 2,
                StepThrottles = [throttle]
            },
            TransientPools =
            [
                TransientPoolDefinition.Create(database, 1),
                TransientPoolDefinition.Create(TransientPoolName.Create("database"), 2)
            ]
        };

        Action add = () => new ServiceCollection().AddOrcaCoreEphemeralEngine(duplicate);

        add.Should().Throw<ArgumentException>().WithMessage("*duplicate*database*");
    }

    [Fact]
    public async Task Host_CombinesCopiedExactStepAndNamedPoolWithoutAGlobalBodyCeiling()
    {
        var database = TransientPoolName.Create("database");
        StepExecutionThrottle[] throttles = [StepExecutionThrottle.For<NamedStep>(1)];
        TransientPoolDefinition[] pools =
        [
            TransientPoolDefinition.Create(database, 1)
        ];
        var gate = new FirstInvocationGate();
        var services = new ServiceCollection();
        services.AddSingleton(gate);
        services.AddTransient<NamedStep>();
        services.AddTransient<OtherStep>();
        services.AddOrcaCoreEphemeralEngine(new EphemeralEngineHostOptions
        {
            StructuredExecution = new StructuredExecutionHostOptions
            {
                MaxConcurrentExecutionPathsPerInstance = 4,
                StepThrottles = throttles
            },
            TransientPools = pools
        });

        throttles[0] = StepExecutionThrottle.For<OtherStep>(10);
        pools[0] = TransientPoolDefinition.Create(
            TransientPoolName.Create("replacement"),
            10);
        using var provider = services.BuildServiceProvider();
        var registry = provider.GetRequiredService<IWorkflowDefinitionRegistry>();
        var governed = Workflow.Ephemeral<State>(
                DefinitionId.New(),
                DefinitionVersion.Initial)
            .Init<string>(_ => new State())
            .Then<NamedStep>()
            .WithTransientPool(database)
            .End()
            .Build();
        var unrelated = Workflow.Ephemeral<State>(
                DefinitionId.New(),
                DefinitionVersion.Initial)
            .Init<string>(_ => new State())
            .Wait(
                WorkflowEventContract.Create(EventName.Create("continue"), EventContractVersion.Initial),
                _ => CorrelationId.Create("unrelated"))
            .Then<OtherStep>()
            .End()
            .Build();
        var governedHandle = registry.Register(governed).GetHandleOrThrow();
        var unrelatedHandle = registry.Register(unrelated).GetHandleOrThrow();

        var independent = (await unrelatedHandle.StartOrGetAsync(
            "independent",
            StartIdempotencyKey.Create("copied-options-unrelated"),
            TestContext.Current.CancellationToken)).GetHandleOrThrow();
        var first = governedHandle.StartOrGetAsync(
            "first",
            StartIdempotencyKey.Create("copied-options-first"),
            TestContext.Current.CancellationToken).AsTask();
        await gate.FirstEntered.Task.WaitAsync(
            TimeSpan.FromSeconds(5),
            TestContext.Current.CancellationToken);
        var second = governedHandle.StartOrGetAsync(
            "second",
            StartIdempotencyKey.Create("copied-options-second"),
            TestContext.Current.CancellationToken).AsTask();
        var delivery = await provider.GetRequiredService<EphemeralWorkflowEventRouter>()
            .RouteToInstanceAsync(
                independent.InstanceId,
                EphemeralTestEvent.Create(
                    EventId.Create("copied-options-continue"),
                    EventName.Create("continue"),
                    CorrelationId.Create("unrelated"),
                    DateTimeOffset.UtcNow),
                TestContext.Current.CancellationToken);

        delivery.Status.Should().Be(EphemeralEventRouteStatus.Accepted);
        (await independent.GetSnapshotAsync(TestContext.Current.CancellationToken))
            .Status.Should().Be(WorkflowInstanceStatus.Completed);
        second.IsCompleted.Should().BeFalse();
        gate.InvocationCount.Should().Be(1);

        gate.ReleaseFirst.TrySetResult();
        _ = await first.WaitAsync(
            TimeSpan.FromSeconds(5),
            TestContext.Current.CancellationToken);
        _ = await second.WaitAsync(
            TimeSpan.FromSeconds(5),
            TestContext.Current.CancellationToken);
        gate.InvocationCount.Should().Be(2);
    }

    private sealed class FirstInvocationGate
    {
        internal TaskCompletionSource FirstEntered { get; } =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        internal TaskCompletionSource ReleaseFirst { get; } =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        internal int InvocationCount;
    }

    private sealed class NamedStep(FirstInvocationGate gate) : IStep<State>
    {
        public async ValueTask<StepResult> ExecuteAsync(
            StepContext<State> context,
            CancellationToken cancellationToken)
        {
            if (Interlocked.Increment(ref gate.InvocationCount) == 1)
            {
                gate.FirstEntered.TrySetResult();
                await gate.ReleaseFirst.Task.WaitAsync(cancellationToken);
            }

            return new StepResult.Completed();
        }
    }

    private sealed class OtherStep : IStep<State>
    {
        public ValueTask<StepResult> ExecuteAsync(
            StepContext<State> context,
            CancellationToken cancellationToken) =>
            ValueTask.FromResult<StepResult>(new StepResult.Completed());
    }

    private sealed class State;
}
