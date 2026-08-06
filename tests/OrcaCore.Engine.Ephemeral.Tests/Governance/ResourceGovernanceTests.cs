using AwesomeAssertions;
using Microsoft.Extensions.DependencyInjection;
using OrcaCore.Hosting;
using OrcaCore.TestSupport;
using Xunit;

namespace OrcaCore.Engine.Ephemeral.Tests.Governance;

public sealed class ResourceGovernanceTests
{
    private static readonly EventName StartWork = EventName.Create("start-work");

    [Fact]
    public async Task ExactNamedStepThrottle_IsSharedAcrossWorkflowInstances()
    {
        var gate = new StepGate();
        using var provider = CreateProvider(
            services => services.AddSingleton(new BlockingStep(gate)),
            new EphemeralEngineHostOptions
            {
                StructuredExecution = new StructuredExecutionHostOptions
                {
                    MaxConcurrentExecutionPathsPerInstance = 4,
                    StepThrottles = [StepExecutionThrottle.For<BlockingStep>(1)]
                },
                TransientPools = []
            });
        var handle = RegisterBlockingDefinition(provider);
        var first = await StartWaitingAsync(handle, "step-throttle-first");
        var second = await StartWaitingAsync(handle, "step-throttle-second");

        var firstDelivery = DeliverWorkAsync(
            provider,
            first,
            "step-throttle-first").AsTask();
        await gate.WaitForEntriesAsync(1, TestContext.Current.CancellationToken);
        var secondDelivery = DeliverWorkAsync(
            provider,
            second,
            "step-throttle-second").AsTask();

        gate.EnteredCount.Should().Be(1);
        gate.MaxObserved.Should().Be(1);
        gate.ReleaseOne();
        await gate.WaitForEntriesAsync(2, TestContext.Current.CancellationToken);
        gate.MaxObserved.Should().Be(1);
        gate.ReleaseOne();

        (await firstDelivery).Status.Should().Be(EventDeliveryStatus.Accepted);
        (await secondDelivery).Status.Should().Be(EventDeliveryStatus.Accepted);
        (await WaitForStatusAsync(
                first,
                WorkflowInstanceStatus.Completed,
                TestContext.Current.CancellationToken))
            .Status.Should().Be(WorkflowInstanceStatus.Completed);
        (await WaitForStatusAsync(
                second,
                WorkflowInstanceStatus.Completed,
                TestContext.Current.CancellationToken))
            .Status.Should().Be(WorkflowInstanceStatus.Completed);
    }

    [Fact]
    public async Task TransientPool_IsSharedAcrossDefinitions()
    {
        var gate = new StepGate();
        var pool = TransientPoolName.Create("database");
        using var provider = CreateProvider(
            services => services.AddSingleton(new BlockingStep(gate)),
            new EphemeralEngineHostOptions
            {
                StructuredExecution = new StructuredExecutionHostOptions
                {
                    MaxConcurrentExecutionPathsPerInstance = 4,
                    StepThrottles = []
                },
                TransientPools = [TransientPoolDefinition.Create(pool, 1)]
            });
        var registry = provider.GetRequiredService<IWorkflowDefinitionRegistry>();
        var firstHandle = registry.Register(BlockingDefinition(pool)).GetHandleOrThrow();
        var secondHandle = registry.Register(BlockingDefinition(pool)).GetHandleOrThrow();
        var first = await StartWaitingAsync(firstHandle, "transient-pool-first");
        var second = await StartWaitingAsync(secondHandle, "transient-pool-second");

        var firstDelivery = DeliverWorkAsync(
            provider,
            first,
            "transient-pool-first").AsTask();
        await gate.WaitForEntriesAsync(1, TestContext.Current.CancellationToken);
        var secondDelivery = DeliverWorkAsync(
            provider,
            second,
            "transient-pool-second").AsTask();

        gate.EnteredCount.Should().Be(1);
        gate.MaxObserved.Should().Be(1);
        gate.ReleaseOne();
        await gate.WaitForEntriesAsync(2, TestContext.Current.CancellationToken);
        gate.MaxObserved.Should().Be(1);
        gate.ReleaseOne();

        (await firstDelivery).Status.Should().Be(EventDeliveryStatus.Accepted);
        (await secondDelivery).Status.Should().Be(EventDeliveryStatus.Accepted);
    }

    [Fact]
    public async Task StepTimeout_DoesNotRunWhileWaitingForNamedStepPermit()
    {
        var clock = new Clock(
            new DateTimeOffset(2026, 7, 30, 12, 0, 0, TimeSpan.Zero));
        var gate = new StepGate();
        var step = new FirstInvocationBlocksStep(gate);
        using var provider = CreateProvider(
            services => services.AddSingleton(step),
            new EphemeralEngineHostOptions
            {
                StructuredExecution = new StructuredExecutionHostOptions
                {
                    MaxConcurrentExecutionPathsPerInstance = 4,
                    StepThrottles = [StepExecutionThrottle.For<FirstInvocationBlocksStep>(1)]
                },
                TransientPools = []
            },
            clock.TimeProvider);
        var registry = provider.GetRequiredService<IWorkflowDefinitionRegistry>();
        var blocking = TimedDefinition(timeout: null);
        var timed = TimedDefinition(TimeSpan.FromMinutes(1));
        var blockingHandle = registry.Register(blocking).GetHandleOrThrow();
        var timedHandle = registry.Register(timed).GetHandleOrThrow();
        var first = await StartWaitingAsync(blockingHandle, "timeout-permit-first");
        var second = await StartWaitingAsync(timedHandle, "timeout-permit-second");

        var firstDelivery = DeliverWorkAsync(
            provider,
            first,
            "timeout-permit-first").AsTask();
        await gate.WaitForEntriesAsync(1, TestContext.Current.CancellationToken);
        var secondDelivery = DeliverWorkAsync(
            provider,
            second,
            "timeout-permit-second").AsTask();
        clock.Advance(TimeSpan.FromMinutes(2));
        var waiting = await second.GetSnapshotAsync(TestContext.Current.CancellationToken);

        waiting.Status.Should().Be(WorkflowInstanceStatus.Waiting);
        waiting.Failure.Should().BeNull(
            "the step timeout starts only after the governance permit is acquired");
        step.InvocationCount.Should().Be(1);
        gate.ReleaseOne();

        _ = await firstDelivery;
        _ = await secondDelivery;
        await step.SecondEntered.Task.WaitAsync(
            TimeSpan.FromSeconds(5),
            TestContext.Current.CancellationToken);
        (await first.GetSnapshotAsync(TestContext.Current.CancellationToken))
            .Status.Should().Be(WorkflowInstanceStatus.Completed);
        (await WaitForStatusAsync(
                second,
                WorkflowInstanceStatus.Completed,
                TestContext.Current.CancellationToken))
            .Status.Should().Be(WorkflowInstanceStatus.Completed);
        step.InvocationCount.Should().Be(2);
    }

    [Fact]
    public async Task EventDelivery_RemainsSerializedWhileGovernedStepIsRunning()
    {
        var gate = new StepGate();
        using var provider = CreateProvider(
            services => services.AddSingleton(new BlockingStep(gate)),
            new EphemeralEngineHostOptions
            {
                StructuredExecution = new StructuredExecutionHostOptions
                {
                    MaxConcurrentExecutionPathsPerInstance = 4,
                    StepThrottles = []
                },
                TransientPools = []
            });
        var registry = provider.GetRequiredService<IWorkflowDefinitionRegistry>();
        var events = provider.GetRequiredService<IWorkflowEventClient>();
        var eventName = EventName.Create("ready");
        var correlation = CorrelationId.Create("serialized-governance");
        var definition = Workflow.Ephemeral<State>(
                DefinitionId.New(),
                DefinitionVersion.Initial)
            .Init<string>(_ => new State("serialized-governance"))
            .Wait(WorkflowEventContract.Create(eventName, EventContractVersion.Initial), _ => correlation)
            .Then<BlockingStep>()
            .End()
            .Build();
        var handle = registry.Register(definition).GetHandleOrThrow();
        var instance = (await handle.StartOrGetAsync(
            "start",
            StartIdempotencyKey.Create("serialized-governance"),
            TestContext.Current.CancellationToken)).GetHandleOrThrow();

        var first = events.DeliverToInstanceAsync(
            instance.InstanceId,
            Event("serialized-first", eventName, correlation),
            TestContext.Current.CancellationToken).AsTask();
        await gate.WaitForEntriesAsync(1, TestContext.Current.CancellationToken);
        var second = events.DeliverToInstanceAsync(
            instance.InstanceId,
            Event("serialized-second", eventName, correlation),
            TestContext.Current.CancellationToken).AsTask();

        second.IsCompleted.Should().BeFalse();
        gate.MaxObserved.Should().Be(1);
        gate.EnteredCount.Should().Be(1);
        gate.ReleaseOne();

        (await first).Status.Should().Be(EventDeliveryStatus.Accepted);
        (await second).Status.Should().Be(EventDeliveryStatus.Accepted);
        gate.EnteredCount.Should().Be(1);
        (await instance.GetSnapshotAsync(TestContext.Current.CancellationToken))
            .Status.Should().Be(WorkflowInstanceStatus.Completed);
    }

    private static ServiceProvider CreateProvider(
        Action<ServiceCollection> registerSteps,
        EphemeralEngineHostOptions options,
        TimeProvider? timeProvider = null)
    {
        var services = new ServiceCollection();
        registerSteps(services);
        if (timeProvider is not null)
        {
            services.AddSingleton(timeProvider);
        }

        services.AddOrcaCoreEphemeralEngine(options);
        return services.BuildServiceProvider();
    }

    private static EphemeralDefinitionHandle<string> RegisterBlockingDefinition(
        ServiceProvider provider) =>
        provider.GetRequiredService<IWorkflowDefinitionRegistry>()
            .Register(BlockingDefinition())
            .GetHandleOrThrow();

    private static EphemeralWorkflowDefinition<string> BlockingDefinition(
        TransientPoolName? pool = null)
    {
        var builder = Workflow.Ephemeral<State>(
                DefinitionId.New(),
                DefinitionVersion.Initial)
            .Init<string>(correlation => new State(correlation))
            .Wait(
                WorkflowEventContract.Create(StartWork, EventContractVersion.Initial),
                state => CorrelationId.Create(state.Value.Correlation))
            .Then<BlockingStep>();
        if (pool is not null)
        {
            builder.WithTransientPool(pool);
        }

        return builder.End().Build();
    }

    private static EphemeralWorkflowDefinition<string> TimedDefinition(
        TimeSpan? timeout)
    {
        var builder = Workflow.Ephemeral<State>(
                DefinitionId.New(),
                DefinitionVersion.Initial)
            .Init<string>(correlation => new State(correlation))
            .Wait(
                WorkflowEventContract.Create(StartWork, EventContractVersion.Initial),
                state => CorrelationId.Create(state.Value.Correlation))
            .Then<FirstInvocationBlocksStep>();
        if (timeout is { } value)
        {
            builder.WithStepTimeout(value);
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

    private static ValueTask<EventDeliveryResult> DeliverWorkAsync(
        ServiceProvider provider,
        WorkflowInstanceHandle instance,
        string correlation) =>
        provider.GetRequiredService<IWorkflowEventClient>().DeliverToInstanceAsync(
            instance.InstanceId,
            Event(
                $"event-{correlation}",
                StartWork,
                CorrelationId.Create(correlation)),
            TestContext.Current.CancellationToken);

    private static async Task<WorkflowInstanceSnapshot> WaitForStatusAsync(
        WorkflowInstanceHandle instance,
        WorkflowInstanceStatus status,
        CancellationToken cancellationToken)
    {
        while (true)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var snapshot = await instance.GetSnapshotAsync(cancellationToken);
            if (snapshot.Status == status)
            {
                return snapshot;
            }

            await Task.Yield();
        }
    }

    private static WorkflowEvent Event(
        string eventId,
        EventName eventName,
        CorrelationId correlation) =>
        WorkflowEvent.Create(
            EventId.Create(eventId),
            eventName,
            correlation,
            DateTimeOffset.UtcNow);

    private sealed record State(string Correlation);

    private sealed class BlockingStep(StepGate gate) : IStep<State>
    {
        public async ValueTask<StepResult> ExecuteAsync(
            StepContext<State> context,
            CancellationToken cancellationToken)
        {
            await gate.EnterAndWaitAsync(cancellationToken);
            return new StepResult.Completed();
        }
    }

    private sealed class FirstInvocationBlocksStep(StepGate gate) : IStep<State>
    {
        private int invocationCount;

        internal TaskCompletionSource SecondEntered { get; } =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        internal int InvocationCount => Volatile.Read(ref invocationCount);

        public async ValueTask<StepResult> ExecuteAsync(
            StepContext<State> context,
            CancellationToken cancellationToken)
        {
            if (Interlocked.Increment(ref invocationCount) == 1)
            {
                await gate.EnterAndWaitAsync(cancellationToken);
            }
            else
            {
                SecondEntered.TrySetResult();
            }

            return new StepResult.Completed();
        }
    }

    private sealed class StepGate
    {
        private readonly SemaphoreSlim entered = new(0);
        private readonly SemaphoreSlim releases = new(0);
        private int active;
        private int enteredCount;
        private int maxObserved;

        internal int EnteredCount => Volatile.Read(ref enteredCount);

        internal int MaxObserved => Volatile.Read(ref maxObserved);

        internal async Task EnterAndWaitAsync(CancellationToken cancellationToken)
        {
            var current = Interlocked.Increment(ref active);
            UpdateMaximum(current);
            Interlocked.Increment(ref enteredCount);
            entered.Release();
            await releases.WaitAsync(cancellationToken);
            Interlocked.Decrement(ref active);
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
}
