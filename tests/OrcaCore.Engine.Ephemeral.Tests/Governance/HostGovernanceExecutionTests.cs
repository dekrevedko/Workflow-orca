using AwesomeAssertions;
using Microsoft.Extensions.DependencyInjection;
using OrcaCore.Hosting;
using Xunit;

namespace OrcaCore.Engine.Ephemeral.Tests.Governance;

public sealed class HostGovernanceExecutionTests
{
    private static readonly EventName StartWork = EventName.Create("start-work");

    [Fact]
    public async Task ExactNamedStepThrottle_IsSharedAcrossInstances()
    {
        var step = new BlockingNamedStep();
        using var provider = CreateProvider(
            step,
            new EphemeralEngineHostOptions
            {
                StructuredExecution = new StructuredExecutionHostOptions
                {
                    MaxConcurrentExecutionPathsPerInstance = 4,
                    StepThrottles = [StepExecutionThrottle.For<BlockingNamedStep>(1)]
                },
                TransientPools = []
            });
        var handle = Register(provider, Definition());
        var first = await StartWaitingAsync(handle, "step-first");
        var second = await StartWaitingAsync(handle, "step-second");

        var firstDelivery = DeliverAsync(provider, first, "step-first").AsTask();
        await step.WaitForEntriesAsync(1, TestContext.Current.CancellationToken);
        var secondDelivery = DeliverAsync(provider, second, "step-second").AsTask();

        step.EntryCount.Should().Be(1);
        step.MaxObserved.Should().Be(1);
        step.ReleaseOne();
        await step.WaitForEntriesAsync(2, TestContext.Current.CancellationToken);
        step.MaxObserved.Should().Be(1);
        step.ReleaseOne();

        (await firstDelivery).Status.Should().Be(EphemeralEventRouteStatus.Accepted);
        (await secondDelivery).Status.Should().Be(EphemeralEventRouteStatus.Accepted);
        (await first.GetSnapshotAsync(TestContext.Current.CancellationToken))
            .Status.Should().Be(WorkflowInstanceStatus.Completed);
        (await second.GetSnapshotAsync(TestContext.Current.CancellationToken))
            .Status.Should().Be(WorkflowInstanceStatus.Completed);
    }

    [Fact]
    public async Task ExactCaseSensitiveTransientPool_IsSharedAcrossInstances()
    {
        var step = new BlockingNamedStep();
        var pool = TransientPoolName.Create("payment-gateway");
        using var provider = CreateProvider(
            step,
            new EphemeralEngineHostOptions
            {
                StructuredExecution = new StructuredExecutionHostOptions
                {
                    MaxConcurrentExecutionPathsPerInstance = 4,
                    StepThrottles = []
                },
                TransientPools = [TransientPoolDefinition.Create(pool, 1)]
            });
        var handle = Register(provider, Definition(pool));
        var first = await StartWaitingAsync(handle, "pool-first");
        var second = await StartWaitingAsync(handle, "pool-second");

        var firstDelivery = DeliverAsync(provider, first, "pool-first").AsTask();
        await step.WaitForEntriesAsync(1, TestContext.Current.CancellationToken);
        var secondDelivery = DeliverAsync(provider, second, "pool-second").AsTask();

        step.EntryCount.Should().Be(1);
        step.MaxObserved.Should().Be(1);
        step.ReleaseOne();
        await step.WaitForEntriesAsync(2, TestContext.Current.CancellationToken);
        step.MaxObserved.Should().Be(1);
        step.ReleaseOne();

        (await firstDelivery).Status.Should().Be(EphemeralEventRouteStatus.Accepted);
        (await secondDelivery).Status.Should().Be(EphemeralEventRouteStatus.Accepted);
    }

    [Fact]
    public void Registration_RejectsEveryMissingTransientPoolBeforeRegistryMutation()
    {
        using var provider = CreateProvider(
            new BlockingNamedStep(),
            new EphemeralEngineHostOptions
            {
                StructuredExecution = new StructuredExecutionHostOptions
                {
                    MaxConcurrentExecutionPathsPerInstance = 1,
                    StepThrottles = []
                },
                TransientPools =
                [
                    TransientPoolDefinition.Create(TransientPoolName.Create("alpha"), 1)
                ]
            });
        var registry = provider.GetRequiredService<IWorkflowDefinitionRegistry>();
        var definitionId = DefinitionId.New();
        var incompatible = Workflow.Ephemeral<State>(
                definitionId,
                DefinitionVersion.Initial)
            .Init<string>(correlation => new State(correlation))
            .Then<ImmediateStep>()
            .WithTransientPool(TransientPoolName.Create("zeta"))
            .Then<ImmediateStep>()
            .WithTransientPool(TransientPoolName.Create("Alpha"))
            .End()
            .Build();

        var result = registry.Register(incompatible);
        var failure = result.Should()
            .BeOfType<WorkflowRegistrationResult<EphemeralDefinitionHandle<string>>.HostIncompatible>()
            .Which.Error.Should()
            .BeOfType<DefinitionHostCompatibilityFailure.MissingTransientPools>()
            .Subject;
        var compatible = Workflow.Ephemeral<State>(
                definitionId,
                DefinitionVersion.Initial)
            .Init<string>(correlation => new State(correlation))
            .End()
            .Build();
        var retry = registry.Register(compatible);

        failure.PoolNames.Select(name => name.Value).Should().Equal("Alpha", "zeta");
        retry.Should().BeOfType<
            WorkflowRegistrationResult<EphemeralDefinitionHandle<string>>.Registered>();
    }

    private static ServiceProvider CreateProvider(
        BlockingNamedStep step,
        EphemeralEngineHostOptions options)
    {
        var services = new ServiceCollection();
        services.AddSingleton(step);
        services.AddTransient<ImmediateStep>();
        services.AddOrcaCoreEphemeralEngine(options);
        return services.BuildServiceProvider();
    }

    private static EphemeralDefinitionHandle<string> Register(
        ServiceProvider provider,
        EphemeralWorkflowDefinition<string> definition) =>
        provider.GetRequiredService<IWorkflowDefinitionRegistry>()
            .Register(definition)
            .GetHandleOrThrow();

    private static EphemeralWorkflowDefinition<string> Definition(
        TransientPoolName? pool = null)
    {
        var builder = Workflow.Ephemeral<State>(
                DefinitionId.New(),
                DefinitionVersion.Initial)
            .Init<string>(correlation => new State(correlation))
            .Wait(
                WorkflowEventContract.Create(StartWork, EventContractVersion.Initial),
                state => CorrelationId.Create(state.Value.Correlation))
            .Then<BlockingNamedStep>();
        if (pool is not null)
        {
            builder.WithTransientPool(pool);
        }

        return builder.End().Build();
    }

    private static async Task<WorkflowInstanceHandle> StartWaitingAsync(
        EphemeralDefinitionHandle<string> handle,
        string correlation) =>
        (await handle.StartOrGetAsync(
            correlation,
            StartIdempotencyKey.Create($"start-{correlation}"),
            TestContext.Current.CancellationToken)).GetHandleOrThrow();

    private static ValueTask<EphemeralEventRouteResult> DeliverAsync(
        ServiceProvider provider,
        WorkflowInstanceHandle instance,
        string correlation) =>
        provider.GetRequiredService<EphemeralWorkflowEventRouter>().RouteToInstanceAsync(
            instance.InstanceId,
            EphemeralTestEvent.Create(
                EventId.Create($"event-{correlation}"),
                StartWork,
                CorrelationId.Create(correlation),
                DateTimeOffset.UtcNow),
            TestContext.Current.CancellationToken);

    private sealed class BlockingNamedStep : IStep<State>
    {
        private readonly SemaphoreSlim entered = new(0);
        private readonly SemaphoreSlim releases = new(0);
        private int active;
        private int enteredCount;
        private int maxObserved;

        internal int EntryCount => Volatile.Read(ref enteredCount);

        internal int MaxObserved => Volatile.Read(ref maxObserved);

        public async ValueTask<StepResult> ExecuteAsync(
            StepContext<State> context,
            CancellationToken cancellationToken)
        {
            var current = Interlocked.Increment(ref active);
            UpdateMaximum(current);
            Interlocked.Increment(ref enteredCount);
            entered.Release();
            await releases.WaitAsync(cancellationToken);
            Interlocked.Decrement(ref active);
            return new StepResult.Completed();
        }

        internal void ReleaseOne() => releases.Release();

        internal async Task WaitForEntriesAsync(
            int count,
            CancellationToken cancellationToken)
        {
            while (Volatile.Read(ref enteredCount) < count)
            {
                await entered.WaitAsync(cancellationToken);
            }
        }

        private void UpdateMaximum(int current)
        {
            while (true)
            {
                var observed = Volatile.Read(ref maxObserved);
                if (current <= observed ||
                    Interlocked.CompareExchange(
                        ref maxObserved,
                        current,
                        observed) == observed)
                {
                    return;
                }
            }
        }
    }

    private sealed class ImmediateStep : IStep<State>
    {
        public ValueTask<StepResult> ExecuteAsync(
            StepContext<State> context,
            CancellationToken cancellationToken) =>
            ValueTask.FromResult<StepResult>(new StepResult.Completed());
    }

    private sealed record State(string Correlation);
}
