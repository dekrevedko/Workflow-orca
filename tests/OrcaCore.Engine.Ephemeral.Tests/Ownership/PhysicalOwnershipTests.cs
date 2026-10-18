using System.Collections.Concurrent;
using AwesomeAssertions;
using AwesomeAssertions.Execution;
using Microsoft.Extensions.DependencyInjection;
using OrcaCore.Engine.Ephemeral.Execution;
using OrcaCore.TestSupport;
using Xunit;

namespace OrcaCore.Engine.Ephemeral.Tests.Ownership;

[Trait("AC", "AC-535")]
public sealed class PhysicalOwnershipTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    [Trait("Disposition", "Implemented")]
    [Trait("Slice", "Issue1Task3.1")]
    public async Task CancelledStartAfterInit_FencesTheRunningOrphan_AndReleasesIt(bool bounded)
    {
        var step = new Body(cooperative: true);
        using var host = new Host(bounded, step);
        var handle = host.Register(host.Definition());
        using var cancellation = new CancellationTokenSource();
        var start = handle.StartOrGetAsync("input", StartIdempotencyKey.Create("cancelled-start"), cancellation.Token).AsTask();
        try
        {
            await step.Entered.Task.WaitAsync(Guard, TestContext.Current.CancellationToken);
            var runtime = host.Initialized!;
            runtime.ToSnapshot().Status.Should().Be(WorkflowInstanceStatus.Running);
            cancellation.Cancel();
            var error = await Record.ExceptionAsync(async () => await start.WaitAsync(Guard, TestContext.Current.CancellationToken));
            error.Should().BeAssignableTo<OperationCanceledException>().Which.CancellationToken.Should().Be(cancellation.Token);
            host.Registry.ListInstances().Should().BeEmpty("an aborted start cannot publish a facade binding");
            var missing = await Record.ExceptionAsync(async () => await handle.GetInstanceAsync(runtime.InstanceId, TestContext.Current.CancellationToken));
            missing.Should().BeOfType<WorkflowInstanceNotFoundException>();
            step.Release.TrySetResult();
            var retry = await handle.StartOrGetAsync("different input", StartIdempotencyKey.Create("cancelled-start"), TestContext.Current.CancellationToken);
            retry.Should().BeOfType<WorkflowStartResult<WorkflowInstanceHandle>.Accepted>().Which.WasExisting.Should().BeFalse();
            retry.GetHandleOrThrow().InstanceId.Should().NotBe(runtime.InstanceId);
            using var assertions = new AssertionScope();
            runtime.ToSnapshot().Status.Should().Be(WorkflowInstanceStatus.Terminated,
                "a failed provisional start must fence the Running orphan before returning the original error");
            if (runtime.ToSnapshot().Status == WorkflowInstanceStatus.Terminated)
                await runtime.Lifetime.WhenSettled.WaitAsync(Guard, TestContext.Current.CancellationToken);
            host.Engine.TryGetFacadeInstance(runtime.InstanceId, out _).Should().BeFalse(
                "a settled provisional instance is eagerly released even when retention is disabled");
        }
        finally
        {
            cancellation.Cancel();
            step.Release.TrySetResult();
            await Observe(start);
        }
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(true, false)]
    [InlineData(false, true)]
    [InlineData(true, true)]
    [Trait("Disposition", "Implemented")]
    [Trait("Slice", "Issue1Task3.1")]
    public async Task ExceptionAfterInit_PreservesOriginalErrorAndPriorTerminal_AndReleasesUnpublishedUnit(bool bounded, bool terminal)
    {
        var body = new Body(synchronous: true);
        using var host = new Host(bounded, body);
        var handle = host.Register(host.Definition());
        var original = new InvalidOperationException("post-Init commit observer failed");
        var thrown = false;
        host.Engine.InstanceCommitted += _ =>
        {
            if (!thrown && (!terminal || host.Initialized!.ToSnapshot().Status == WorkflowInstanceStatus.Completed))
            {
                thrown = true;
                throw original;
            }
        };
        var error = await Record.ExceptionAsync(async () => await handle.StartOrGetAsync(
            "input", StartIdempotencyKey.Create("exception-start"), TestContext.Current.CancellationToken));
        error.Should().BeSameAs(original);
        host.Registry.ListInstances().Should().BeEmpty();
        var runtime = host.Initialized!;
        var missing = await Record.ExceptionAsync(async () => await handle.GetInstanceAsync(runtime.InstanceId, TestContext.Current.CancellationToken));
        missing.Should().BeOfType<WorkflowInstanceNotFoundException>();
        var retry = await handle.StartOrGetAsync("different input", StartIdempotencyKey.Create("exception-start"), TestContext.Current.CancellationToken);
        retry.Should().BeOfType<WorkflowStartResult<WorkflowInstanceHandle>.Accepted>().Which.WasExisting.Should().BeFalse();
        retry.GetHandleOrThrow().InstanceId.Should().NotBe(runtime.InstanceId);
        using var assertions = new AssertionScope();
        runtime.ToSnapshot().Status.Should().Be(terminal ? WorkflowInstanceStatus.Completed : WorkflowInstanceStatus.Terminated,
            "abort fences active state but cannot rewrite an already committed terminal status");
        if (runtime.ToSnapshot().Status == (terminal ? WorkflowInstanceStatus.Completed : WorkflowInstanceStatus.Terminated))
            await runtime.Lifetime.WhenSettled.WaitAsync(Guard, TestContext.Current.CancellationToken);
        host.Engine.TryGetFacadeInstance(runtime.InstanceId, out _).Should().BeFalse(
            "settled unpublished units must be released outside either retention profile");
    }

    public static TheoryData<bool, string, string, bool> TimeoutCases
    {
        get
        {
            var cases = new TheoryData<bool, string, string, bool>();
            foreach (var bounded in new[] { false, true })
            foreach (var shape in new[] { "root", "branch", "item" })
            foreach (var capacity in new[] { "none", "throttle", "pool" })
            foreach (var bulk in new[] { false, true })
                cases.Add(bounded, shape, capacity, bulk);
            return cases;
        }
    }

    [Theory]
    [MemberData(nameof(TimeoutCases))]
    [Trait("Disposition", "Implemented")]
    [Trait("Slice", "Issue1Task3.1")]
    public async Task TimedOutRootBranchOrItem_CannotBeRemovedBeforePhysicalReturn(
        bool bounded, string shape, string capacity, bool bulk)
    {
        var body = new Body();
        using var host = new Host(bounded, body, capacity);
        var handle = host.Register(host.Definition(shape, capacity, timeout: true));
        var start = host.Start(handle, "timed-out");
        try
        {
            await body.Entered.Task.WaitAsync(Guard, TestContext.Current.CancellationToken);
            host.Clock.Advance(StepTimeout);
            var instance = (await start.WaitAsync(Guard, TestContext.Current.CancellationToken)).GetHandleOrThrow();
            var terminal = await instance.GetSnapshotAsync(TestContext.Current.CancellationToken);
            terminal.Status.Should().Be(WorkflowInstanceStatus.Failed);
            body.PhysicalTask!.IsCompleted.Should().BeFalse("the timed-out body ignores cancellation");
            var removed = bulk ? host.Engine.EvictTerminalInstances() : host.Engine.EvictInstance(instance.InstanceId) ? 1 : 0;
            removed.Should().Be(0, "terminal status is not proof of physical settlement, even without a configured capacity slot");
            host.Engine.TryGetFacadeInstance(instance.InstanceId, out _).Should().BeTrue();
            body.Release.TrySetResult();
            await body.PhysicalTask.WaitAsync(Guard, TestContext.Current.CancellationToken);
            await host.Initialized!.Lifetime.WhenSettled.WaitAsync(Guard, TestContext.Current.CancellationToken);
            var after = await instance.GetSnapshotAsync(TestContext.Current.CancellationToken);
            after.Status.Should().Be(terminal.Status, "late completion cannot overwrite the terminal fence");
            (bulk ? host.Engine.EvictTerminalInstances() : host.Engine.EvictInstance(instance.InstanceId) ? 1 : 0)
                .Should().Be(1, "physical return makes the otherwise unpinned terminal unit eligible");
        }
        finally
        {
            body.Release.TrySetResult();
            if (body.PhysicalTask is not null) await body.PhysicalTask.WaitAsync(Guard, TestContext.Current.CancellationToken);
            await Observe(start);
        }
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(true, false)]
    [InlineData(false, true)]
    [InlineData(true, true)]
    [Trait("Disposition", "Implemented")]
    [Trait("Slice", "Issue1Task3.1")]
    public async Task PendingTerminalCommitCallback_ProtectsUnitBeforeAcceptance(bool bounded, bool bulk)
    {
        using var host = new Host(bounded, new Body(synchronous: true));
        var committed = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        using var release = new ManualResetEventSlim();
        host.Engine.InstanceCommitted += _ =>
        {
            if (host.Initialized!.ToSnapshot().Status != WorkflowInstanceStatus.Completed) return;
            committed.TrySetResult();
            if (!release.Wait(Guard, TestContext.Current.CancellationToken)) throw new TimeoutException("Test failed to release the commit observer.");
        };
        var handle = host.Register(host.Definition());
        var start = host.Start(handle, "pending-commit");
        try
        {
            await committed.Task.WaitAsync(Guard, TestContext.Current.CancellationToken);
            start.IsCompleted.Should().BeFalse();
            host.Registry.ListInstances().Should().BeEmpty();
            (bulk ? host.Engine.EvictTerminalInstances() : host.Engine.EvictInstance(host.Initialized!.InstanceId) ? 1 : 0)
                .Should().Be(0, "a completed step still has a runtime-owned commit callback before facade acceptance");
        }
        finally { release.Set(); await Observe(start); }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    [Trait("Disposition", "Implemented")]
    [Trait("Slice", "Issue1Task3.1")]
    public async Task RetryWinner_DoesNotHideTheTimedOutOlderPhysicalAttempt(bool bounded)
    {
        var body = new Body(retryWinner: true);
        using var host = new Host(bounded, body);
        var handle = host.Register(host.Definition(timeout: true, retry: true));
        var start = host.Start(handle, "retry");
        try
        {
            await body.Entered.Task.WaitAsync(Guard, TestContext.Current.CancellationToken);
            host.Clock.Advance(StepTimeout);
            var instance = (await start.WaitAsync(Guard, TestContext.Current.CancellationToken)).GetHandleOrThrow();
            (await instance.GetSnapshotAsync(TestContext.Current.CancellationToken)).Status.Should().Be(WorkflowInstanceStatus.Completed);
            body.Attempts.Should().Be(2);
            body.PhysicalTask!.IsCompleted.Should().BeFalse();
            host.Engine.EvictInstance(instance.InstanceId).Should().BeFalse(
                "a settled retry winner does not settle its token-ignoring predecessor");
        }
        finally
        {
            body.Release.TrySetResult();
            if (body.PhysicalTask is not null) await body.PhysicalTask.WaitAsync(Guard, TestContext.Current.CancellationToken);
            await Observe(start);
        }
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    [Trait("Disposition", "Control")]
    [Trait("Slice", "Issue1Task3.1")]
    public async Task AcceptanceBeforeStartTokenCancellation_KeepsTheHandleAndStartKey(bool bounded, bool delay)
    {
        using var host = new Host(bounded, new Body(synchronous: true));
        var handle = host.Register(host.Definition(wait: !delay, delay: delay));
        using var caller = new CancellationTokenSource();
        var key = StartIdempotencyKey.Create("accepted");
        var first = await handle.StartOrGetAsync("input", key, caller.Token);
        var instance = first.GetHandleOrThrow();
        caller.Cancel();
        host.Clock.Advance(TimeSpan.FromHours(1));
        var replay = await handle.StartOrGetAsync("input", key, TestContext.Current.CancellationToken);
        replay.GetHandleOrThrow().InstanceId.Should().Be(instance.InstanceId);
        (await instance.GetSnapshotAsync(TestContext.Current.CancellationToken)).Status.Should().Be(WorkflowInstanceStatus.Waiting);
        host.Engine.EvictTerminalInstances().Should().Be(0);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    [Trait("Disposition", "Control")]
    [Trait("Slice", "Issue1Task3.1")]
    public async Task SynchronousCompletion_PublishesOneCoherentUnit(bool bounded)
    {
        using var host = new Host(bounded, new Body(synchronous: true));
        var handle = host.Register(host.Definition());
        var key = StartIdempotencyKey.Create("sync");
        var first = (await handle.StartOrGetAsync("input", key, TestContext.Current.CancellationToken)).GetHandleOrThrow();
        var replay = (await handle.StartOrGetAsync("input", key, TestContext.Current.CancellationToken)).GetHandleOrThrow();
        replay.InstanceId.Should().Be(first.InstanceId);
        (await first.GetSnapshotAsync(TestContext.Current.CancellationToken)).Status.Should().Be(WorkflowInstanceStatus.Completed);
        host.Registry.ListInstances().Should().ContainSingle();
    }

    public static TheoryData<bool, string, bool> CommandCases
    {
        get
        {
            var cases = new TheoryData<bool, string, bool>();
            foreach (var bounded in new[] { false, true })
            foreach (var shape in new[] { "root", "branch", "item" })
            foreach (var terminate in new[] { false, true }) cases.Add(bounded, shape, terminate);
            return cases;
        }
    }

    [Theory]
    [MemberData(nameof(CommandCases))]
    [Trait("Disposition", "Control")]
    [Trait("Slice", "Issue1Task3.1")]
    public async Task AcceptedCancellationOrTermination_ProtectsPhysicalBodyUntilReturn(bool bounded, string shape, bool terminate)
    {
        var body = new Body();
        using var host = new Host(bounded, body);
        var handle = host.Register(host.Definition(shape, wait: true));
        var instance = (await host.Start(handle, "command")).GetHandleOrThrow();
        var delivery = new EphemeralWorkflowEventRouter(host.Engine, host.Registry).RouteToInstanceAsync(
            instance.InstanceId,
            EphemeralTestEvent.Create(EventId.Create("ownership-event"), EventName.Create("ownership-continue"), CorrelationId.Create("ownership"), host.Clock.Now),
            TestContext.Current.CancellationToken).AsTask();
        Task? command = null;
        try
        {
            await body.Entered.Task.WaitAsync(Guard, TestContext.Current.CancellationToken);
            command = terminate ? instance.TerminateAsync(TestContext.Current.CancellationToken).AsTask() : instance.RequestCancellationAsync(TestContext.Current.CancellationToken).AsTask();
            await body.CancellationObserved.Task.WaitAsync(Guard, TestContext.Current.CancellationToken);
            body.PhysicalTask!.IsCompleted.Should().BeFalse();
            host.Engine.TryGetFacadeInstance(instance.InstanceId, out _).Should().BeTrue();
            host.Engine.EvictTerminalInstances().Should().Be(0);
            body.Release.TrySetResult();
            await command.WaitAsync(Guard, TestContext.Current.CancellationToken);
            await Observe(delivery);
            (await instance.GetSnapshotAsync(TestContext.Current.CancellationToken)).Status.Should().Be(
                terminate ? WorkflowInstanceStatus.Terminated : WorkflowInstanceStatus.Cancelled);
        }
        finally
        {
            body.Release.TrySetResult();
            if (body.PhysicalTask is not null) await body.PhysicalTask.WaitAsync(Guard, TestContext.Current.CancellationToken);
            if (command is not null) await command.WaitAsync(Guard, TestContext.Current.CancellationToken);
            await Observe(delivery);
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    [Trait("Disposition", "Implemented")]
    [Trait("Slice", "Issue1Task3.1")]
    public async Task AbortedStartWithUnsettledPhysicalBody_IsFencedWithoutPublishing(bool bounded)
    {
        var body = new Body(retryCancellation: true);
        using var host = new Host(bounded, body);
        var handle = host.Register(host.Definition(timeout: true, retry: true));
        using var caller = new CancellationTokenSource();
        var start = handle.StartOrGetAsync("input", StartIdempotencyKey.Create("unsettled-cancel"), caller.Token).AsTask();
        try
        {
            await body.Entered.Task.WaitAsync(Guard, TestContext.Current.CancellationToken);
            host.Clock.Advance(StepTimeout);
            await body.RetryEntered.Task.WaitAsync(Guard, TestContext.Current.CancellationToken);
            caller.Cancel();
            var error = await Record.ExceptionAsync(async () => await start.WaitAsync(Guard, TestContext.Current.CancellationToken));
            error.Should().BeAssignableTo<OperationCanceledException>().Which.CancellationToken.Should().Be(caller.Token);
            body.PhysicalTask!.IsCompleted.Should().BeFalse();
            host.Registry.ListInstances().Should().BeEmpty();
            host.Engine.TryGetFacadeInstance(host.Initialized!.InstanceId, out _).Should().BeTrue(
                "abort cannot release a unit with an incomplete physical attempt");
            host.Initialized.ToSnapshot().Status.Should().Be(WorkflowInstanceStatus.Terminated);
            body.Release.TrySetResult();
            await body.PhysicalTask.WaitAsync(Guard, TestContext.Current.CancellationToken);
            await host.Removals.Removed(host.Initialized.InstanceId).WaitAsync(Guard, TestContext.Current.CancellationToken);
            host.Engine.TryGetFacadeInstance(host.Initialized.InstanceId, out _).Should().BeFalse();
            host.Registry.ListInstances().Should().BeEmpty();
        }
        finally
        {
            body.Release.TrySetResult();
            if (body.PhysicalTask is not null) await body.PhysicalTask.WaitAsync(Guard, TestContext.Current.CancellationToken);
            await Observe(start);
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    [Trait("Disposition", "Implemented")]
    [Trait("Slice", "Issue1Task3.1")]
    public async Task ExceptionAfterTimeout_PreservesTerminalAndProtectsRemainingAttempt(bool bounded)
    {
        var body = new Body();
        using var host = new Host(bounded, body);
        var original = new InvalidOperationException("terminal commit observer failed with a physical attempt outstanding");
        host.Engine.InstanceCommitted += _ =>
        {
            if (host.Initialized!.ToSnapshot().Status == WorkflowInstanceStatus.Failed) throw original;
        };
        var start = host.Start(host.Register(host.Definition(timeout: true)), "unsettled-exception");
        try
        {
            await body.Entered.Task.WaitAsync(Guard, TestContext.Current.CancellationToken);
            host.Clock.Advance(StepTimeout);
            var error = await Record.ExceptionAsync(async () => await start.WaitAsync(Guard, TestContext.Current.CancellationToken));
            error.Should().BeSameAs(original);
            body.PhysicalTask!.IsCompleted.Should().BeFalse();
            host.Registry.ListInstances().Should().BeEmpty();
            host.Initialized!.ToSnapshot().Status.Should().Be(WorkflowInstanceStatus.Failed);
            host.Engine.EvictInstance(host.Initialized.InstanceId).Should().BeFalse(
                "a failed provisional start remains owned while its timed-out body runs");
            body.Release.TrySetResult();
            await body.PhysicalTask.WaitAsync(Guard, TestContext.Current.CancellationToken);
            await host.Removals.Removed(host.Initialized.InstanceId).WaitAsync(Guard, TestContext.Current.CancellationToken);
            host.Engine.TryGetFacadeInstance(host.Initialized.InstanceId, out _).Should().BeFalse();
            host.Initialized.ToSnapshot().Status.Should().Be(WorkflowInstanceStatus.Failed);
        }
        finally
        {
            body.Release.TrySetResult();
            if (body.PhysicalTask is not null) await body.PhysicalTask.WaitAsync(Guard, TestContext.Current.CancellationToken);
            try { await start.WaitAsync(Guard, TestContext.Current.CancellationToken); }
            catch (InvalidOperationException error) when (ReferenceEquals(error, original)) { }
        }
    }

    [Theory]
    [InlineData(false, "throttle")]
    [InlineData(true, "throttle")]
    [InlineData(false, "pool")]
    [InlineData(true, "pool")]
    [Trait("Disposition", "Control")]
    [Trait("Slice", "Issue1Task3.1")]
    public async Task TimedOutPhysicalAttempt_HoldsItsSlotUntilAnotherInstanceCanEnter(bool bounded, string capacity)
    {
        var body = new Body(subsequentSynchronous: true);
        using var host = new Host(bounded, body, capacity);
        var handle = host.Register(host.Definition(capacity: capacity, timeout: true));
        var first = host.Start(handle, "slot-first");
        Task<WorkflowStartResult<WorkflowInstanceHandle>>? second = null;
        var secondCommitted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        try
        {
            await body.Entered.Task.WaitAsync(Guard, TestContext.Current.CancellationToken);
            host.Clock.Advance(StepTimeout);
            var firstInstance = (await first.WaitAsync(Guard, TestContext.Current.CancellationToken)).GetHandleOrThrow();
            host.Engine.InstanceCommitted += id =>
            {
                if (id != firstInstance.InstanceId && host.Engine.TryGetFacadeInstance(id, out var current) && current!.ToSnapshot().Status == WorkflowInstanceStatus.Completed)
                    secondCommitted.TrySetResult();
            };
            second = host.Start(handle, "slot-second");
            await host.GovernanceWait.Task.WaitAsync(Guard, TestContext.Current.CancellationToken);
            body.Attempts.Should().Be(1, "a logical timeout must not release the physical throttle or pool slot");
            body.Release.TrySetResult();
            await secondCommitted.Task.WaitAsync(Guard, TestContext.Current.CancellationToken);
            var secondInstance = (await second.WaitAsync(Guard, TestContext.Current.CancellationToken)).GetHandleOrThrow();
            body.Attempts.Should().Be(2);
            (await secondInstance.GetSnapshotAsync(TestContext.Current.CancellationToken)).Status.Should().Be(WorkflowInstanceStatus.Completed);
            host.Initialized!.ToSnapshot().Status.Should().Be(WorkflowInstanceStatus.Failed,
                "the old terminal state stays immutable even if count pressure later retires its public handle");
        }
        finally
        {
            body.Release.TrySetResult();
            if (body.PhysicalTask is not null) await body.PhysicalTask.WaitAsync(Guard, TestContext.Current.CancellationToken);
            await Observe(first);
            if (second is not null) await Observe(second);
        }
    }

    private static async Task Observe(Task task)
    {
        try { await task.WaitAsync(Guard, TestContext.Current.CancellationToken); }
        catch (OperationCanceledException) { }
    }

    private static readonly TimeSpan Guard = TimeSpan.FromSeconds(10);
    private static readonly TimeSpan StepTimeout = TimeSpan.FromSeconds(30);
    private static readonly TransientPoolName Pool = TransientPoolName.Create("ownership-pool");

    private sealed class Host : IDisposable
    {
        private readonly ServiceProvider services;
        internal Clock Clock { get; } = new(new DateTimeOffset(2026, 10, 17, 12, 0, 0, TimeSpan.Zero));
        internal RemovalObservation Removals { get; } = new();
        internal EphemeralWorkflowEngine Engine { get; }
        internal EphemeralWorkflowDefinitionRegistry Registry { get; }
        internal IWorkflowInstance? Initialized { get; private set; }
        internal TaskCompletionSource GovernanceWait { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        internal Host(bool bounded, Body body, string capacity = "none")
        {
            services = new ServiceCollection().AddSingleton(body).BuildServiceProvider();
            Engine = new EphemeralWorkflowEngine(Clock.TimeProvider, new EphemeralWorkflowEngineOptions
            {
                TerminalRetention = bounded ? new EphemeralTerminalRetentionSettings(1, TimeSpan.FromMinutes(1), TimeSpan.FromSeconds(1)) : null,
                MaxConcurrentExecutionPathsPerInstance = 2,
                GovernanceWaitStarting = () => GovernanceWait.TrySetResult(),
                StepThrottles = capacity == "throttle" ? new Dictionary<Type, int> { [typeof(Body)] = 1 } : new Dictionary<Type, int>(),
                TransientPools = capacity == "pool" ? new Dictionary<string, int> { [Pool.Value] = 1 } : new Dictionary<string, int>()
            }, Removals, new InstanceExecutionLane(), services);
            Registry = new EphemeralWorkflowDefinitionRegistry(Engine);
            Engine.InstanceCommitted += id =>
            {
                if (Initialized is null && Engine.TryGetFacadeInstance(id, out var instance))
                    Initialized = instance;
            };
        }

        internal EphemeralDefinitionHandle<string> Register(EphemeralWorkflowDefinition<string> definition) =>
            Registry.Register(definition).GetHandleOrThrow();

        internal Task<WorkflowStartResult<WorkflowInstanceHandle>> Start(EphemeralDefinitionHandle<string> handle, string key) =>
            handle.StartOrGetAsync("input", StartIdempotencyKey.Create(key), TestContext.Current.CancellationToken).AsTask();

        internal EphemeralWorkflowDefinition<string> Definition(string shape = "root", string capacity = "none", bool timeout = false, bool retry = false, bool wait = false, bool delay = false)
        {
            var root = Workflow.Ephemeral<State>(DefinitionId.New(), DefinitionVersion.Initial)
                .Init<string>(value => new State(value));
            if (delay) root.Delay(TimeSpan.FromDays(1));
            if (wait) root.Wait(WorkflowEventContract.Create(EventName.Create("ownership-continue"), EventContractVersion.Initial), _ => CorrelationId.Create("ownership"));
            if (shape == "branch")
                return root.Parallel<string>(branches => branches.Branch<State>(
                    AuthoredBranchId.Create("owned"), _ => new State("branch"), branch =>
                    {
                        var step = branch.Then<Body>();
                        if (timeout) step.WithStepTimeout(StepTimeout);
                        if (capacity == "pool") step.WithTransientPool(Pool);
                        step.Return(state => state.Value.Value);
                    })).WhenAll((parent, _) => parent.Value).End().Build();
            if (shape == "item")
                return root.ForEach<string, State, string>(_ => ["item"], ForEachOptions.Create(1, 1), item => new State(item.Item), body =>
                {
                    var step = body.Then<Body>();
                    if (timeout) step.WithStepTimeout(StepTimeout);
                    if (capacity == "pool") step.WithTransientPool(Pool);
                    step.Return(state => state.Value.Value);
                }).WhenAll((parent, _) => parent.Value).End().Build();
            var rootStep = root.Then<Body>();
            if (timeout) rootStep.WithStepTimeout(StepTimeout);
            if (retry) rootStep.WithRetry(2);
            if (capacity == "pool") rootStep.WithTransientPool(Pool);
            return rootStep.End().Build();
        }

        public void Dispose() { Engine.Dispose(); services.Dispose(); }
    }

    // Decorates the real registry solely to observe the definitive removal boundary.
    // Storage, lookups, and all execution remain the production implementations.
    private sealed class RemovalObservation : IInstanceRegistry
    {
        private readonly InMemoryInstanceRegistry inner = new();
        private readonly ConcurrentDictionary<InstanceId, TaskCompletionSource> removed = new();
        internal Task Removed(InstanceId id) => removed.GetOrAdd(id, _ => new(TaskCreationOptions.RunContinuationsAsynchronously)).Task;
        public void Save<TState>(WorkflowInstance<TState> instance) => inner.Save(instance);
        public IReadOnlyCollection<object> GetMany(IReadOnlyCollection<InstanceId> ids) => inner.GetMany(ids);
        public bool TryGet(InstanceId id, out object? instance) => inner.TryGet(id, out instance);
        public IReadOnlyCollection<object> List() => inner.List();
        public bool Remove(InstanceId id)
        {
            var result = inner.Remove(id);
            if (result) removed.GetOrAdd(id, _ => new(TaskCreationOptions.RunContinuationsAsynchronously)).TrySetResult();
            return result;
        }
    }

    public sealed record State(string Value);

    private sealed class Body(bool cooperative = false, bool synchronous = false, bool retryWinner = false, bool subsequentSynchronous = false, bool retryCancellation = false) : IStep<State>
    {
        internal TaskCompletionSource Entered { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        internal TaskCompletionSource Release { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        internal Task<StepResult>? PhysicalTask { get; private set; }
        internal int Attempts { get; private set; }
        internal TaskCompletionSource RetryEntered { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        internal TaskCompletionSource CancellationObserved { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public ValueTask<StepResult> ExecuteAsync(StepContext<State> context, CancellationToken cancellationToken)
        {
            Attempts++;
            if (synchronous || (retryWinner && context.Execution.AttemptNumber > 1) || (subsequentSynchronous && Attempts > 1))
                return ValueTask.FromResult<StepResult>(new StepResult.Completed());
            if (retryCancellation && context.Execution.AttemptNumber > 1)
                return new ValueTask<StepResult>(RunRetryAsync(cancellationToken));
            PhysicalTask = RunAsync(cancellationToken);
            Entered.TrySetResult();
            return new ValueTask<StepResult>(PhysicalTask);
        }

        private async Task<StepResult> RunRetryAsync(CancellationToken cancellationToken)
        {
            RetryEntered.TrySetResult();
            await Release.Task.WaitAsync(cancellationToken);
            return new StepResult.Completed();
        }

        private async Task<StepResult> RunAsync(CancellationToken cancellationToken)
        {
            using var registration = cancellationToken.Register(() => CancellationObserved.TrySetResult());
            if (cooperative) await Release.Task.WaitAsync(cancellationToken);
            else await Release.Task;
            return new StepResult.Completed();
        }
    }
}
