using AwesomeAssertions;
using Microsoft.Extensions.Time.Testing;
using Microsoft.Extensions.DependencyInjection;
using OrcaCore.TestSupport;
using Xunit;

namespace OrcaCore.Engine.Ephemeral.Tests.Ownership;

[Trait("AC", "AC-628")]
[Trait("AC", "AC-629")]
[Trait("AC", "AC-630")]
public sealed class SettlementReceiptTests
{
    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    [Trait("AC", "AC-632")]
    public async Task RetiredReceiptHandle_DoesNotKeepItsDisposedDependencyGraphAlive(bool retired, bool resultful)
    {
        var (handle, dependency) = RetireAndDropOwner(retired, resultful);
        await handle.WaitForSettlementAsync(TestContext.Current.CancellationToken);
        var error = await Record.ExceptionAsync(async () => await handle.GetSnapshotAsync(TestContext.Current.CancellationToken));
        if (retired) error.Should().BeOfType<WorkflowInstanceNotFoundException>();
        else error.Should().BeOfType<ObjectDisposedException>();
        using var cancelled = new CancellationTokenSource();
        cancelled.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(async () => await handle.GetSnapshotAsync(cancelled.Token));
        GC.Collect(); GC.WaitForPendingFinalizers(); GC.Collect();
        dependency.IsAlive.Should().BeFalse("a retained receipt/retired handle must not retain the engine's dependency graph");
        GC.KeepAlive(handle);
    }

    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
    private static (WorkflowInstanceHandle, WeakReference) RetireAndDropOwner(bool retired, bool resultful)
    {
        using var services = new ServiceCollection().AddSingleton(_ => new DependencyMarker()).BuildServiceProvider();
        var weak = new WeakReference(services.GetRequiredService<DependencyMarker>());
        using var engine = new EphemeralWorkflowEngine(TimeProvider.System, new EphemeralWorkflowEngineOptions
        { TerminalRetention = new(1, TimeSpan.FromHours(1), TimeSpan.FromMinutes(1)) }, services);
        using var registry = new EphemeralWorkflowDefinitionRegistry(engine);
        var root = Workflow.Ephemeral<State>(DefinitionId.New(), DefinitionVersion.Initial).Init<string>(value => new State(value));
        var start = resultful
            ? new Func<string, WorkflowInstanceHandle>(value => registry.Register(root.End(state => state.Value.Value).Build()).GetHandleOrThrow()
                .StartOrGetAsync(value, StartIdempotencyKey.Create(value), TestContext.Current.CancellationToken).AsTask().GetAwaiter().GetResult().GetHandleOrThrow())
            : value => registry.Register(root.End().Build()).GetHandleOrThrow()
                .StartOrGetAsync(value, StartIdempotencyKey.Create(value), TestContext.Current.CancellationToken).AsTask().GetAwaiter().GetResult().GetHandleOrThrow();
        var old = start("old");
        if (retired)
        {
            var pressure = registry.Register(Workflow.Ephemeral<State>(DefinitionId.New(), DefinitionVersion.Initial)
                .Init<string>(value => new State(value)).End().Build()).GetHandleOrThrow();
            _ = pressure.StartOrGetAsync("pressure", StartIdempotencyKey.Create("pressure"), TestContext.Current.CancellationToken).AsTask().GetAwaiter().GetResult();
            engine.TryGetFacadeInstance(old.InstanceId, out _).Should().BeFalse();
        }
        return (old, weak);
    }

    private sealed class DependencyMarker;

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task IdleIsNotSettlement_ObserversCancelLocally_AndOriginalReceiptSurvivesRetirement(bool bounded)
    {
        using var engine = CreateEngine(bounded);
        var registry = new EphemeralWorkflowDefinitionRegistry(engine);
        var definition = registry.Register(Workflow.Ephemeral<State>(DefinitionId.New(), DefinitionVersion.Initial)
            .Init<string>(value => new State(value))
            .Wait(WorkflowEventContract.Create(EventName.Create("continue"), EventContractVersion.Initial), _ => CorrelationId.Create("idle"))
            .End(state => state.Value.Value).Build()).GetHandleOrThrow();
        var instance = (await definition.StartOrGetAsync("idle", StartIdempotencyKey.Create("idle"), TestContext.Current.CancellationToken)).GetHandleOrThrow();
        using var observer = new CancellationTokenSource();
        var cancelled = instance.WaitForSettlementAsync(observer.Token).AsTask();
        var waiting = instance.WaitForSettlementAsync(TestContext.Current.CancellationToken).AsTask();
        waiting.IsCompleted.Should().BeFalse("an accepted event wait is nonterminal even at zero physical owners");
        observer.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => cancelled.WaitAsync(TimeSpan.FromSeconds(10), TestContext.Current.CancellationToken));
        waiting.IsCompleted.Should().BeFalse();
        (await instance.GetSnapshotAsync(TestContext.Current.CancellationToken)).Status.Should().Be(WorkflowInstanceStatus.Waiting);
        await instance.TerminateAsync(TestContext.Current.CancellationToken);
        await waiting.WaitAsync(TimeSpan.FromSeconds(10), TestContext.Current.CancellationToken);
        await instance.WaitForSettlementAsync(TestContext.Current.CancellationToken);
        var pressure = registry.Register(Workflow.Ephemeral<State>(DefinitionId.New(), DefinitionVersion.Initial)
            .Init<string>(value => new State(value)).End().Build()).GetHandleOrThrow();
        var other = (await pressure.StartOrGetAsync("pressure", StartIdempotencyKey.Create("pressure"), TestContext.Current.CancellationToken)).GetHandleOrThrow();
        await other.WaitForSettlementAsync(TestContext.Current.CancellationToken);
        await instance.WaitForSettlementAsync(TestContext.Current.CancellationToken);
        if (bounded)
            await Assert.ThrowsAsync<WorkflowInstanceNotFoundException>(async () => await instance.GetSnapshotAsync(TestContext.Current.CancellationToken));
        using var preCancelled = new CancellationTokenSource();
        preCancelled.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(async () => await instance.WaitForSettlementAsync(preCancelled.Token));
    }

    [Theory]
    [InlineData(false, "root")]
    [InlineData(true, "root")]
    [InlineData(false, "branch")]
    [InlineData(true, "branch")]
    [InlineData(false, "item")]
    [InlineData(true, "item")]
    [Trait("AC", "AC-623")]
    public async Task LogicalTimeout_DoesNotSettleBlockedPhysicalWork(bool bounded, string scope)
    {
        var clock = new FakeTimeProvider();
        using var engine = new EphemeralWorkflowEngine(clock, new EphemeralWorkflowEngineOptions
        {
            TerminalRetention = bounded ? new(1, TimeSpan.FromHours(1), TimeSpan.FromMinutes(1)) : null
        });
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var registry = new EphemeralWorkflowDefinitionRegistry(engine);
        async ValueTask Body(StepContext<State> _)
        {
            entered.TrySetResult();
            await release.Task.WaitAsync(TimeSpan.FromSeconds(10), TestContext.Current.CancellationToken);
            throw new IOException("late loser");
        }
        var root = Workflow.Ephemeral<State>(DefinitionId.New(), DefinitionVersion.Initial).Init<string>(value => new State(value));
        var body = scope switch
        {
            "root" => root.Then(Body).WithStepTimeout(TimeSpan.FromSeconds(1)),
            "branch" => root.Parallel<string>(branches => branches.Branch<State>(AuthoredBranchId.Create("timed"),
                    parent => parent.Value, branch => branch.Then(Body).WithStepTimeout(TimeSpan.FromSeconds(1)).Return(state => state.Value.Value)))
                .WhenAll((parent, _) => parent.Value),
            _ => root.ForEach<string, State, string>(_ => ["item"], ForEachOptions.Create(1),
                    item => new State(item.Item), branch => branch.Then(Body).WithStepTimeout(TimeSpan.FromSeconds(1)).Return(state => state.Value.Value))
                .WhenAll((parent, _) => parent.Value)
        };
        var definition = registry.Register(body.End(state => state.Value.Value).Build()).GetHandleOrThrow();
        var start = definition.StartOrGetAsync("input", StartIdempotencyKey.Create("timeout"), TestContext.Current.CancellationToken).AsTask();
        try
        {
            await entered.Task.WaitAsync(TimeSpan.FromSeconds(10), TestContext.Current.CancellationToken);
            clock.Advance(TimeSpan.FromSeconds(1));
            var instance = (await start.WaitAsync(TimeSpan.FromSeconds(10), TestContext.Current.CancellationToken)).GetHandleOrThrow();
            var unavailable = await Assert.ThrowsAsync<WorkflowOutputUnavailableException>(async () => await instance.WaitForOutputAsync(TestContext.Current.CancellationToken));
            unavailable.InnerException.Should().BeNull();
            var settlement = instance.WaitForSettlementAsync(TestContext.Current.CancellationToken).AsTask();
            settlement.IsCompleted.Should().BeFalse();
            release.TrySetResult();
            await settlement.WaitAsync(TimeSpan.FromSeconds(10), TestContext.Current.CancellationToken);
            var after = await Assert.ThrowsAsync<WorkflowOutputUnavailableException>(async () => await instance.WaitForOutputAsync(TestContext.Current.CancellationToken));
            after.InnerException.Should().BeNull();
            after.Failure!.Code.Should().Be(unavailable.Failure!.Code);
        }
        finally { release.TrySetResult(); }
    }

    private static EphemeralWorkflowEngine CreateEngine(bool bounded) => new(TimeProvider.System,
        new EphemeralWorkflowEngineOptions { TerminalRetention = bounded ? new(1, TimeSpan.FromHours(1), TimeSpan.FromMinutes(1)) : null });
    private sealed record State(string Value);

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    [Trait("AC", "AC-631")]
    public async Task ConcurrentObservers_AroundTerminalPublication_NeverMissCompletion(bool bounded)
    {
        using var engine = CreateEngine(bounded);
        var registry = new EphemeralWorkflowDefinitionRegistry(engine);
        var definition = registry.Register(Workflow.Ephemeral<State>(DefinitionId.New(), DefinitionVersion.Initial)
            .Init<string>(value => new State(value))
            .Wait(WorkflowEventContract.Create(EventName.Create("never"), EventContractVersion.Initial), _ => CorrelationId.Create("race"))
            .End().Build()).GetHandleOrThrow();
        var instance = (await definition.StartOrGetAsync("input", StartIdempotencyKey.Create("race"), TestContext.Current.CancellationToken)).GetHandleOrThrow();
        var start = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var completed = 0;
        var observers = Enumerable.Range(0, 32).Select(async _ =>
        {
            await start.Task;
            await instance.WaitForSettlementAsync(TestContext.Current.CancellationToken);
            Interlocked.Increment(ref completed);
        }).ToArray();
        start.TrySetResult();
        await instance.TerminateAsync(TestContext.Current.CancellationToken);
        await Task.WhenAll(observers).WaitAsync(TimeSpan.FromSeconds(10), TestContext.Current.CancellationToken);
        completed.Should().Be(32);
        await instance.WaitForSettlementAsync(TestContext.Current.CancellationToken);
    }

    [Fact]
    [Trait("AC", "AC-632")]
    public async Task AgeExpiry_AndKeyReuse_DoNotRedirectTheOriginalReceipt()
    {
        var clock = new FakeTimeProvider();
        using var engine = new EphemeralWorkflowEngine(clock, new EphemeralWorkflowEngineOptions
        { TerminalRetention = new(10, TimeSpan.FromMinutes(1), TimeSpan.FromSeconds(10)) });
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var effects = 0;
        var definition = new EphemeralWorkflowDefinitionRegistry(engine).Register(Workflow.Ephemeral<State>(DefinitionId.New(), DefinitionVersion.Initial)
            .Init<string>(value => new State(value)).Then(async _ =>
            {
                if (Interlocked.Increment(ref effects) == 1) return;
                entered.TrySetResult();
                await release.Task.WaitAsync(TimeSpan.FromSeconds(10), TestContext.Current.CancellationToken);
            }).End().Build()).GetHandleOrThrow();
        var key = StartIdempotencyKey.Create("reused");
        var old = (await definition.StartOrGetAsync("input", key, TestContext.Current.CancellationToken)).GetHandleOrThrow();
        await old.WaitForSettlementAsync(TestContext.Current.CancellationToken);
        clock.Advance(TimeSpan.FromMinutes(2));
        await Assert.ThrowsAsync<WorkflowInstanceNotFoundException>(async () => await definition.GetInstanceAsync(old.InstanceId, TestContext.Current.CancellationToken));
        var replacement = definition.StartOrGetAsync("input", key, TestContext.Current.CancellationToken).AsTask();
        try
        {
            await entered.Task.WaitAsync(TimeSpan.FromSeconds(10), TestContext.Current.CancellationToken);
            replacement.IsCompleted.Should().BeFalse();
            old.WaitForSettlementAsync(TestContext.Current.CancellationToken).IsCompletedSuccessfully.Should().BeTrue();
            release.TrySetResult();
            var accepted = (await replacement).Should().BeOfType<WorkflowStartResult<WorkflowInstanceHandle>.Accepted>().Subject;
            accepted.WasExisting.Should().BeFalse();
            accepted.Handle.InstanceId.Should().NotBe(old.InstanceId);
            await accepted.Handle.WaitForSettlementAsync(TestContext.Current.CancellationToken);
        }
        finally { release.TrySetResult(); }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    [Trait("AC", "AC-634")]
    public async Task Receipt_DoesNotWaitForQueryPin_AndRemainsPassiveAfterCompletedDisposal(bool bounded)
    {
        var pinned = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        using var engine = new EphemeralWorkflowEngine(TimeProvider.System, new EphemeralWorkflowEngineOptions
        {
            TerminalRetention = bounded ? new(1, TimeSpan.FromHours(1), TimeSpan.FromMinutes(1)) : null,
            OperationPinAcquired = _ => { pinned.TrySetResult(); return new ValueTask(release.Task); }
        });
        var registry = new EphemeralWorkflowDefinitionRegistry(engine);
        var definition = registry.Register(Workflow.Ephemeral<State>(DefinitionId.New(), DefinitionVersion.Initial)
            .Init<string>(value => new State(value))
            .Wait(WorkflowEventContract.Create(EventName.Create("never"), EventContractVersion.Initial), _ => CorrelationId.Create("never"))
            .End().Build()).GetHandleOrThrow();
        var instance = (await definition.StartOrGetAsync("input", StartIdempotencyKey.Create("pin"), TestContext.Current.CancellationToken)).GetHandleOrThrow();
        var query = instance.GetSnapshotAsync(TestContext.Current.CancellationToken).AsTask();
        try
        {
            await pinned.Task.WaitAsync(TimeSpan.FromSeconds(10), TestContext.Current.CancellationToken);
            await instance.TerminateAsync(TestContext.Current.CancellationToken);
            await instance.WaitForSettlementAsync(TestContext.Current.CancellationToken).AsTask().WaitAsync(TimeSpan.FromSeconds(10), TestContext.Current.CancellationToken);
            query.IsCompleted.Should().BeFalse("a query pin is not physical work for the receipt");
            engine.TryGetFacadeInstance(instance.InstanceId, out var runtime).Should().BeTrue();
            runtime!.Lifetime.TryOwn(out var rejected).Should().BeFalse("proof seals future owned instance work");
            rejected.Should().BeNull();
            runtime.Lifetime.TryEnterCallback(out var callback).Should().BeFalse();
            callback.Should().BeNull();
        }
        finally { release.TrySetResult(); }
        await query;
        (await engine.HostLifetime.StopAsync(Timeout.InfiniteTimeSpan, TestContext.Current.CancellationToken))
            .Should().BeOfType<OrcaCore.Hosting.EphemeralHostShutdownResult.Completed>();
        engine.Dispose();
        instance.WaitForSettlementAsync(TestContext.Current.CancellationToken).IsCompletedSuccessfully.Should().BeTrue();
        await instance.WaitForSettlementAsync(TestContext.Current.CancellationToken);
    }
}
