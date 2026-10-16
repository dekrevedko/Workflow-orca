using AwesomeAssertions;
using OrcaCore.Abstractions.Durable;
using OrcaCore.Abstractions.Ids;
using OrcaCore.Abstractions.Instances;
using OrcaCore.Abstractions.Providers;
using OrcaCore.Core.Definitions;
using OrcaCore.Engine.Durable.Definitions;
using OrcaCore.Engine.Durable.Execution;
using OrcaCore.Providers.InMemory;
using Xunit;

namespace OrcaCore.Engine.Durable.Tests.Driver;

public sealed class DurableStepThrottleExecutionTests
{
    [Fact]
    public async Task ExactStepThrottle_AcrossInstancesPersistsOnlyBlockedOwnerObligation()
    {
        var store = new InMemoryWorkflowProvider();
        var step = new BlockingNamedStep();
        var services = new StepServiceProvider(step);
        var registry = new DurableDefinitionRegistry(
            int.MaxValue,
            services,
            new Dictionary<Type, int> { [typeof(BlockingNamedStep)] = 1 });
        var runtime = CreateRuntime(store, registry);
        var publicDefinition = global::OrcaCore.Workflow.Durable<State>(
                DefinitionId.New(),
                DefinitionVersion.Initial)
            .Init<string>(_ => new State())
            .Then<BlockingNamedStep>()
            .End()
            .Build();
        var definition = RuntimeDefinition(publicDefinition);
        runtime.RegisterDefinition(definition);

        var first = runtime.StartOrGetAsync<string, State>(
            "throttle-first",
            definition.DefinitionId,
            definition.DefinitionVersion,
            "first",
            TestContext.Current.CancellationToken);
        await step.WaitForEntriesAsync(1, TestContext.Current.CancellationToken);

        var second = runtime.StartOrGetAsync<string, State>(
            "throttle-second",
            definition.DefinitionId,
            definition.DefinitionVersion,
            "second",
            TestContext.Current.CancellationToken);
        var secondInstance = await WaitForStartedAsync(
            store,
            "throttle-second",
            TestContext.Current.CancellationToken);
        var blockedEnvelope = await WaitForBlockedThrottleAsync(
            store,
            secondInstance,
            TestContext.Current.CancellationToken);

        blockedEnvelope.Fibers.Should().ContainSingle(fiber =>
            fiber.Blocked != null &&
            fiber.Blocked.Reason == DurableFiberBlockedReason.Resource &&
            fiber.Blocked.ObligationId.StartsWith("step-throttle:", StringComparison.Ordinal));
        registry.StepThrottleSnapshots.Should().ContainSingle().Which.Should().BeEquivalentTo(
            new
            {
                StepType = typeof(BlockingNamedStep),
                ConfiguredLimit = 1,
                ActiveSlots = 1,
                WaitDepth = 1,
                Cancellations = 0L
            });
        step.MaxObserved.Should().Be(1);

        step.ReleaseOne();
        await step.WaitForEntriesAsync(2, TestContext.Current.CancellationToken);
        step.MaxObserved.Should().Be(1);
        step.ReleaseOne();

        var firstResult = await first.WaitAsync(TestContext.Current.CancellationToken);
        var secondResult = await second.WaitAsync(TestContext.Current.CancellationToken);
        var completed = await store.ListAsync(
            new WorkflowProjectionQuery(),
            TestContext.Current.CancellationToken);
        completed.Where(snapshot =>
                snapshot.InstanceId == firstResult.InstanceId ||
                snapshot.InstanceId == secondResult.InstanceId)
            .Should().HaveCount(2)
            .And.OnlyContain(snapshot => snapshot.Status == WorkflowStatus.Completed);
    }

    [Fact]
    public async Task SaturatedBranchThrottle_ParksOnlyOwnerAndRunsSiblingBeforeGrant()
    {
        var store = new InMemoryWorkflowProvider();
        var blocking = new BlockingBranchStep();
        var sibling = new SignalBranchStep();
        var services = new StepServiceProvider(blocking, sibling);
        var registry = new DurableDefinitionRegistry(
            2,
            services,
            new Dictionary<Type, int> { [typeof(BlockingBranchStep)] = 1 });
        var runtime = CreateRuntime(store, registry);
        var holder = RuntimeDefinition<ParentState>(
            global::OrcaCore.Workflow.Durable<ParentState>(
                    DefinitionId.New(),
                    DefinitionVersion.Initial)
                .Init<string>(_ => new ParentState())
                .Parallel<int>(branches => branches.Branch(
                    AuthoredBranchId.Create("holder"),
                    _ => new BranchState(),
                    branch => branch
                        .Then<BlockingBranchStep>()
                        .Return(_ => 1)))
                .WhenAll((snapshot, _) => snapshot.Value)
                .End()
                .Build());
        var target = RuntimeDefinition<ParentState>(
            global::OrcaCore.Workflow.Durable<ParentState>(
                    DefinitionId.New(),
                    DefinitionVersion.Initial)
                .Init<string>(_ => new ParentState())
                .Parallel<int>(branches => branches
                    .Branch(
                        AuthoredBranchId.Create("throttled"),
                        _ => new BranchState(),
                        branch => branch
                            .Then<BlockingBranchStep>()
                            .Return(_ => 1))
                    .Branch(
                        AuthoredBranchId.Create("sibling"),
                        _ => new BranchState(),
                        branch => branch
                            .Then<SignalBranchStep>()
                            .Return(_ => 2)))
                .WhenAll((snapshot, _) => snapshot.Value)
                .End()
                .Build());
        runtime.RegisterDefinition(holder);
        runtime.RegisterDefinition(target);

        var holding = runtime.StartOrGetAsync<string, ParentState>(
            "throttle-holder",
            holder.DefinitionId,
            holder.DefinitionVersion,
            "holder",
            TestContext.Current.CancellationToken);
        await blocking.WaitForEntriesAsync(1, TestContext.Current.CancellationToken);
        var waiting = runtime.StartOrGetAsync<string, ParentState>(
            "throttle-target",
            target.DefinitionId,
            target.DefinitionVersion,
            "target",
            TestContext.Current.CancellationToken);

        await sibling.Executed.Task.WaitAsync(TestContext.Current.CancellationToken);
        blocking.EnteredCount.Should().Be(1);
        var targetId = await WaitForStartedAsync(
            store,
            "throttle-target",
            TestContext.Current.CancellationToken);
        _ = await WaitForBlockedThrottleAsync(
            store,
            targetId,
            TestContext.Current.CancellationToken);

        blocking.ReleaseOne();
        await blocking.WaitForEntriesAsync(2, TestContext.Current.CancellationToken);
        blocking.ReleaseOne();
        await holding.WaitAsync(TestContext.Current.CancellationToken);
        await waiting.WaitAsync(TestContext.Current.CancellationToken);

        var snapshots = await store.ListAsync(
            new WorkflowProjectionQuery { InstanceId = targetId },
            TestContext.Current.CancellationToken);
        snapshots.Should().ContainSingle().Which.Status.Should().Be(WorkflowStatus.Completed);
    }

    [Fact]
    public async Task ReplacementHost_ReadmitsBlockedOwnerWithoutPersistedHostSlot()
    {
        var store = new InMemoryWorkflowProvider();
        var firstStep = new BlockingNamedStep();
        var firstRegistry = new DurableDefinitionRegistry(
            1,
            new StepServiceProvider(firstStep),
            new Dictionary<Type, int> { [typeof(BlockingNamedStep)] = 1 });
        var firstHost = CreateRuntime(store, firstRegistry);
        var publicDefinition = global::OrcaCore.Workflow.Durable<State>(
                DefinitionId.New(),
                DefinitionVersion.Initial)
            .Init<string>(_ => new State())
            .Then<BlockingNamedStep>()
            .End()
            .Build();
        var definition = RuntimeDefinition(publicDefinition);
        firstHost.RegisterDefinition(definition);
        using var holderCancellation = new CancellationTokenSource();
        using var blockedCancellation = new CancellationTokenSource();

        var holding = firstHost.StartOrGetAsync<string, State>(
            "restart-holder",
            definition.DefinitionId,
            definition.DefinitionVersion,
            "holder",
            holderCancellation.Token);
        await firstStep.WaitForEntriesAsync(1, TestContext.Current.CancellationToken);
        var blocked = firstHost.StartOrGetAsync<string, State>(
            "restart-blocked",
            definition.DefinitionId,
            definition.DefinitionVersion,
            "blocked",
            blockedCancellation.Token);
        var blockedId = await WaitForStartedAsync(
            store,
            "restart-blocked",
            TestContext.Current.CancellationToken);
        _ = await WaitForBlockedThrottleAsync(
            store,
            blockedId,
            TestContext.Current.CancellationToken);

        blockedCancellation.Cancel();
        holderCancellation.Cancel();
        await blocked.Invoking(task => task).Should().ThrowAsync<OperationCanceledException>();
        await holding.Invoking(task => task).Should().ThrowAsync<OperationCanceledException>();

        var replacementStep = new BlockingNamedStep();
        var replacementRegistry = new DurableDefinitionRegistry(
            1,
            new StepServiceProvider(replacementStep),
            new Dictionary<Type, int> { [typeof(BlockingNamedStep)] = 1 });
        var replacement = CreateRuntime(store, replacementRegistry);
        replacement.RegisterDefinition(definition);
        var resumed = replacement.StartOrGetAsync<string, State>(
            "restart-blocked",
            definition.DefinitionId,
            definition.DefinitionVersion,
            "blocked",
            TestContext.Current.CancellationToken);

        await replacementStep.WaitForEntriesAsync(1, TestContext.Current.CancellationToken);
        replacementRegistry.StepThrottleSnapshots.Should().ContainSingle().Which.ActiveSlots.Should().Be(1);
        replacementStep.ReleaseOne();
        var result = await resumed.WaitAsync(TestContext.Current.CancellationToken);

        result.Created.Should().BeFalse();
        var snapshot = (await store.ListAsync(
            new WorkflowProjectionQuery { InstanceId = blockedId },
            TestContext.Current.CancellationToken)).Single();
        snapshot.Status.Should().Be(WorkflowStatus.Completed);
    }

    [Fact]
    public async Task ForcedTermination_CancelsPendingAdmissionWithoutReleasingGrantedSlot()
    {
        var store = new InMemoryWorkflowProvider();
        var step = new BlockingNamedStep();
        var registry = new DurableDefinitionRegistry(
            int.MaxValue,
            new StepServiceProvider(step),
            new Dictionary<Type, int> { [typeof(BlockingNamedStep)] = 1 });
        var runtime = CreateRuntime(store, registry);
        var definition = RuntimeDefinition(
            global::OrcaCore.Workflow.Durable<State>(
                    DefinitionId.New(),
                    DefinitionVersion.Initial)
                .Init<string>(_ => new State())
                .Then<BlockingNamedStep>()
                .End()
                .Build());
        runtime.RegisterDefinition(definition);

        var holding = runtime.StartOrGetAsync<string, State>(
            "terminate-holder",
            definition.DefinitionId,
            definition.DefinitionVersion,
            "holder",
            TestContext.Current.CancellationToken);
        await step.WaitForEntriesAsync(1, TestContext.Current.CancellationToken);

        var waiting = runtime.StartOrGetAsync<string, State>(
            "terminate-waiter",
            definition.DefinitionId,
            definition.DefinitionVersion,
            "waiter",
            TestContext.Current.CancellationToken);
        var waitingId = await WaitForStartedAsync(
            store,
            "terminate-waiter",
            TestContext.Current.CancellationToken);
        _ = await WaitForBlockedThrottleAsync(
            store,
            waitingId,
            TestContext.Current.CancellationToken);

        var terminated = await runtime.Management.TerminateAsync(
            waitingId,
            DateTimeOffset.UtcNow,
            global::OrcaCore.Engine.Durable.Management.DestructiveCommandSafety.Confirmed,
            TestContext.Current.CancellationToken);

        terminated.Outcome.Should().Be(DurableCommandOutcome.Committed);
        await waiting.Invoking(task => task).Should().ThrowAsync<OperationCanceledException>();
        registry.StepThrottleSnapshots.Should().ContainSingle().Which.Should().BeEquivalentTo(
            new
            {
                StepType = typeof(BlockingNamedStep),
                ConfiguredLimit = 1,
                ActiveSlots = 1,
                WaitDepth = 0,
                Cancellations = 1L
            });
        step.EnteredCount.Should().Be(1);

        step.ReleaseOne();
        await holding.WaitAsync(TestContext.Current.CancellationToken);
        var snapshot = (await store.ListAsync(
            new WorkflowProjectionQuery { InstanceId = waitingId },
            TestContext.Current.CancellationToken)).Single();
        snapshot.Status.Should().Be(WorkflowStatus.Terminated);
    }

    [Fact]
    public async Task ForcedTermination_DoesNotReleaseGrantedSlotUntilTokenIgnoringBodyReturns()
    {
        var store = new InMemoryWorkflowProvider();
        var step = new TokenIgnoringNamedStep();
        var registry = new DurableDefinitionRegistry(
            int.MaxValue,
            new StepServiceProvider(step),
            new Dictionary<Type, int> { [typeof(TokenIgnoringNamedStep)] = 1 });
        var runtime = CreateRuntime(store, registry);
        var definition = RuntimeDefinition(
            global::OrcaCore.Workflow.Durable<State>(
                    DefinitionId.New(),
                    DefinitionVersion.Initial)
                .Init<string>(_ => new State())
                .Then<TokenIgnoringNamedStep>()
                .End()
                .Build());
        runtime.RegisterDefinition(definition);

        var first = runtime.StartOrGetAsync<string, State>(
            "terminate-active-holder",
            definition.DefinitionId,
            definition.DefinitionVersion,
            "holder",
            TestContext.Current.CancellationToken);
        await step.WaitForEntriesAsync(1, TestContext.Current.CancellationToken);
        var firstId = await WaitForStartedAsync(
            store,
            "terminate-active-holder",
            TestContext.Current.CancellationToken);

        var terminated = await runtime.Management.TerminateAsync(
            firstId,
            TimeProvider.System.GetUtcNow(),
            global::OrcaCore.Engine.Durable.Management.DestructiveCommandSafety.Confirmed,
            TestContext.Current.CancellationToken);
        terminated.Outcome.Should().Be(DurableCommandOutcome.Committed);

        var second = runtime.StartOrGetAsync<string, State>(
            "terminate-active-waiter",
            definition.DefinitionId,
            definition.DefinitionVersion,
            "waiter",
            TestContext.Current.CancellationToken);
        var secondId = await WaitForStartedAsync(
            store,
            "terminate-active-waiter",
            TestContext.Current.CancellationToken);
        _ = await WaitForBlockedThrottleAsync(
            store,
            secondId,
            TestContext.Current.CancellationToken);
        step.EnteredCount.Should().Be(1);
        registry.StepThrottleSnapshots.Should().ContainSingle().Which.ActiveSlots.Should().Be(1);

        step.ReleaseOne();
        await step.WaitForEntriesAsync(2, TestContext.Current.CancellationToken);
        step.ReleaseOne();

        _ = await first.WaitAsync(TestContext.Current.CancellationToken);
        _ = await second.WaitAsync(TestContext.Current.CancellationToken);
        var snapshots = await store.ListAsync(
            new WorkflowProjectionQuery(),
            TestContext.Current.CancellationToken);
        snapshots.Single(snapshot => snapshot.InstanceId == firstId).Status
            .Should().Be(WorkflowStatus.Terminated);
        snapshots.Single(snapshot => snapshot.InstanceId == secondId).Status
            .Should().Be(WorkflowStatus.Completed);
    }

    private static DurableWorkflowRuntime CreateRuntime(
        InMemoryWorkflowProvider store,
        DurableDefinitionRegistry registry)
    {
        var processor = new DurableCommandProcessor(store);
        var management = new global::OrcaCore.Engine.Durable.Management.DurableManagement(
            store,
            null,
            store,
            processor);
        return new DurableWorkflowRuntime(
            processor,
            registry,
            TimeProvider.System,
            projectionStore: store,
            management: management);
    }

    private static WorkflowDefinition<State> RuntimeDefinition(object publicDefinition) =>
        RuntimeDefinition<State>(publicDefinition);

    private static WorkflowDefinition<TState> RuntimeDefinition<TState>(object publicDefinition) =>
        (WorkflowDefinition<TState>)publicDefinition.GetType()
            .GetProperty(
                "RuntimeDefinition",
                System.Reflection.BindingFlags.Instance |
                System.Reflection.BindingFlags.NonPublic)!
            .GetValue(publicDefinition)!;

    private static async Task<InstanceId> WaitForStartedAsync(
        InMemoryWorkflowProvider store,
        string key,
        CancellationToken cancellationToken)
    {
        while (true)
        {
            var started = await store.GetStartedAsync(key, cancellationToken);
            if (started.HasValue)
            {
                return started.Value.InstanceId;
            }

            await Task.Delay(TimeSpan.FromMilliseconds(10), TimeProvider.System, cancellationToken);
        }
    }

    private static async Task<DurableExecutionEnvelopeV2> WaitForBlockedThrottleAsync(
        InMemoryWorkflowProvider store,
        InstanceId instanceId,
        CancellationToken cancellationToken)
    {
        while (true)
        {
            var checkpoint = await store.LoadCheckpointAsync(instanceId, cancellationToken);
            if (checkpoint.HasValue)
            {
                var envelope = DurableExecutionEnvelopeV2.Deserialize(checkpoint.Value.Payload);
                if (envelope.Fibers.Any(fiber =>
                        fiber.Blocked?.ObligationId.StartsWith(
                            "step-throttle:",
                            StringComparison.Ordinal) == true))
                {
                    return envelope;
                }
            }

            await Task.Delay(TimeSpan.FromMilliseconds(10), TimeProvider.System, cancellationToken);
        }
    }

    private sealed class BlockingNamedStep : IStep<State>
    {
        private readonly SemaphoreSlim entered = new(0);
        private readonly SemaphoreSlim releases = new(0);
        private int active;
        private int enteredCount;
        private int maxObserved;

        internal int MaxObserved => Volatile.Read(ref maxObserved);

        internal int EnteredCount => Volatile.Read(ref enteredCount);

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
            context.ReplaceState(context.State with { Count = context.State.Count + 1 });
            return new StepResult.Completed();
        }

        internal void ReleaseOne() => releases.Release();

        internal async Task WaitForEntriesAsync(int count, CancellationToken cancellationToken)
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
                    Interlocked.CompareExchange(ref maxObserved, current, observed) == observed)
                {
                    return;
                }
            }
        }
    }

    private sealed class BlockingBranchStep : IStep<BranchState>
    {
        private readonly SemaphoreSlim entered = new(0);
        private readonly SemaphoreSlim releases = new(0);
        private int enteredCount;

        internal int EnteredCount => Volatile.Read(ref enteredCount);

        public async ValueTask<StepResult> ExecuteAsync(
            StepContext<BranchState> context,
            CancellationToken cancellationToken)
        {
            Interlocked.Increment(ref enteredCount);
            entered.Release();
            await releases.WaitAsync(cancellationToken);
            return new StepResult.Completed();
        }

        internal void ReleaseOne() => releases.Release();

        internal async Task WaitForEntriesAsync(int count, CancellationToken cancellationToken)
        {
            while (Volatile.Read(ref enteredCount) < count)
            {
                await entered.WaitAsync(cancellationToken);
            }
        }
    }

    private sealed class TokenIgnoringNamedStep : IStep<State>
    {
        private readonly SemaphoreSlim entered = new(0);
        private readonly SemaphoreSlim releases = new(0);
        private int enteredCount;

        internal int EnteredCount => Volatile.Read(ref enteredCount);

        public async ValueTask<StepResult> ExecuteAsync(
            StepContext<State> context,
            CancellationToken cancellationToken)
        {
            Interlocked.Increment(ref enteredCount);
            entered.Release();
            await releases.WaitAsync(CancellationToken.None);
            return new StepResult.Completed();
        }

        internal void ReleaseOne() => releases.Release();

        internal async Task WaitForEntriesAsync(int count, CancellationToken cancellationToken)
        {
            while (Volatile.Read(ref enteredCount) < count)
            {
                await entered.WaitAsync(cancellationToken);
            }
        }
    }

    private sealed class SignalBranchStep : IStep<BranchState>
    {
        internal TaskCompletionSource Executed { get; } =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        public ValueTask<StepResult> ExecuteAsync(
            StepContext<BranchState> context,
            CancellationToken cancellationToken)
        {
            Executed.TrySetResult();
            return ValueTask.FromResult<StepResult>(new StepResult.Completed());
        }
    }

    private sealed class StepServiceProvider(params object[] services) : IServiceProvider
    {
        public object? GetService(Type serviceType) =>
            services.SingleOrDefault(service => service.GetType() == serviceType);
    }

    private sealed record State
    {
        public int Count { get; init; }
    }

    private sealed record ParentState;

    private sealed record BranchState;
}
