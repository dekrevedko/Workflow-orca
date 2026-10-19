using AwesomeAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Time.Testing;
using OrcaCore.Hosting;
using OrcaCore.TestSupport;
using Xunit;

namespace OrcaCore.Engine.Ephemeral.Tests.Retention;

[Trait("AC", "AC-534")]
[Trait("AC", "AC-536")]
[Trait("AC", "AC-537")]
[Trait("AC", "AC-538")]
[Trait("AC", "AC-540")]
public sealed class RetentionInterleavingTests
{
    private static readonly TimeSpan Guard = TimeSpan.FromSeconds(10);
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task IdleAge_IsMonotonicFromSettlement_NotRefreshedByReadOrReplay(bool enabled)
    {
        using var host = new Host(enabled ? 10 : null);
        host.Clock.Created.Should().Be(enabled ? 1 : 0);
        var definition = host.Register();
        var instance = await host.Start(definition, "first");
        host.Clock.Advance(TimeSpan.FromSeconds(9));
        host.Clock.WallJump = TimeSpan.FromDays(1000);
        (await instance.GetOutputAsync(Token)).Should().BeOfType<WorkflowOutputResult<List<string>>.Available>();
        var replay = await definition.StartOrGetAsync("first", StartIdempotencyKey.Create("first"), Token);
        replay.Should().BeOfType<WorkflowStartResult<WorkflowInstanceHandle<List<string>>>.Accepted>().Which.WasExisting.Should().BeTrue();
        host.Clock.Advance(TimeSpan.FromSeconds(1));
        if (enabled)
        {
            host.Registry.ListInstances().Should().BeEmpty();
            host.Engine.Retention.TrackedCount.Should().Be(0);
            var error = await Record.ExceptionAsync(async () => await instance.GetSnapshotAsync(Token));
            error.Should().BeOfType<WorkflowInstanceNotFoundException>();
        }
        else (await instance.GetSnapshotAsync(Token)).Status.Should().Be(WorkflowInstanceStatus.Completed);
        host.Engine.Dispose();
        host.Clock.Active.Should().Be(0);
        host.Clock.Advance(TimeSpan.FromHours(1));
    }

    [Fact]
    public async Task EqualTimestampOrder_IsHostWide_AndReadsDoNotRefreshOrdinal()
    {
        using var host = new Host(2);
        var a = host.Register(); var b = host.Register();
        var oldest = await host.Start(a, "oldest");
        var next = await host.Start(b, "next");
        await oldest.GetStateAsync<State>(Token);
        await a.StartOrGetAsync("oldest", StartIdempotencyKey.Create("oldest"), Token);
        var newest = await host.Start(a, "newest");
        host.Registry.ListInstances().Select(x => x.InstanceId).Should().BeEquivalentTo(new[] { next.InstanceId, newest.InstanceId });
        var gone = await Record.ExceptionAsync(async () => await oldest.GetSnapshotAsync(Token));
        gone.Should().BeOfType<WorkflowInstanceNotFoundException>();
    }

    [Theory]
    [InlineData("snapshot")]
    [InlineData("state")]
    [InlineData("output")]
    public async Task AdmittedQuery_PinsThroughDetachment_AndAgeRetiresAtFinalRelease(string operation)
    {
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        InstanceId? victim = null;
        using var host = new Host(1, async id =>
        {
            if (id != victim) return;
            entered.TrySetResult();
            await release.Task.WaitAsync(Guard, Token);
        });
        var definition = host.Register();
        var instance = await host.Start(definition, "victim"); victim = instance.InstanceId;
        async Task<object?> Read() => operation switch
        {
            "snapshot" => await instance.GetSnapshotAsync(Token),
            "state" => await instance.GetStateAsync<State>(Token),
            _ => await instance.GetOutputAsync(Token)
        };
        var query = Read();
        try
        {
            await entered.Task.WaitAsync(Guard, Token);
            await host.Start(definition, "pressure");
            host.Registry.ListInstances().Should().HaveCount(2, "pins are additional to the eligible terminal budget");
            host.Clock.Advance(TimeSpan.FromSeconds(10));
            host.Registry.ListInstances().Should().HaveCount(1);
        }
        finally { release.TrySetResult(); }
        var detached = await query.WaitAsync(Guard, Token);
        detached.Should().NotBeNull();
        host.Registry.ListInstances().Should().BeEmpty();
        var stale = await Record.ExceptionAsync(async () => await instance.GetSnapshotAsync(Token));
        stale.Should().BeOfType<WorkflowInstanceNotFoundException>();
        if (detached is State state) state.Items.Should().Equal("victim");
        if (detached is WorkflowOutputResult<List<string>>.Available output) output.Output.Should().Equal("victim");
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task PreexistingWaiters_DetachSuccessOrFailure_OneCancellationDoesNotCancelOthers(bool fail)
    {
        using var host = new Host(1);
        var definition = host.Register(wait: true, fail: fail);
        var instance = await host.Start(definition, "waiting");
        using var local = new CancellationTokenSource();
        var cancelled = instance.WaitForOutputAsync(local.Token).AsTask();
        var first = instance.WaitForOutputAsync(Token).AsTask();
        var second = instance.WaitForOutputAsync(Token).AsTask();
        local.Cancel();
        (await Record.ExceptionAsync(async () => await cancelled)).Should().BeAssignableTo<OperationCanceledException>();
        await host.Start(host.Register(), "pressure");
        host.Clock.Advance(TimeSpan.FromSeconds(20));
        first.IsCompleted.Should().BeFalse(); second.IsCompleted.Should().BeFalse();
        await host.Deliver(instance, "waiting");
        if (fail)
        {
            (await Record.ExceptionAsync(async () => await first.WaitAsync(Guard, Token))).Should().BeOfType<WorkflowOutputUnavailableException>();
            (await Record.ExceptionAsync(async () => await second.WaitAsync(Guard, Token))).Should().BeOfType<WorkflowOutputUnavailableException>();
        }
        else
        {
            (await first.WaitAsync(Guard, Token)).Should().Equal("waiting");
            (await second.WaitAsync(Guard, Token)).Should().Equal("waiting");
        }
        await host.Start(host.Register(), "after");
        host.Router.AcceptedEntryCount(instance.InstanceId).Should().Be(0);
        host.Registry.ListInstances().Should().HaveCount(1);
    }

    [Theory]
    [InlineData("snapshot")]
    [InlineData("state")]
    [InlineData("output")]
    [InlineData("wait")]
    [InlineData("cancel")]
    [InlineData("terminate")]
    [InlineData("lookup")]
    public async Task RetiredHandle_EveryOperationFailsForOriginalId(string operation)
    {
        using var host = new Host(1);
        var definition = host.Register();
        var old = await host.Start(definition, "old");
        await host.Start(definition, "new");
        var error = await Record.ExceptionAsync(async () =>
        {
            switch (operation)
            {
                case "snapshot": await old.GetSnapshotAsync(Token); break;
                case "state": await old.GetStateAsync<State>(Token); break;
                case "output": await old.GetOutputAsync(Token); break;
                case "wait": await old.WaitForOutputAsync(Token); break;
                case "cancel": await old.RequestCancellationAsync(Token); break;
                case "terminate": await old.TerminateAsync(Token); break;
                default: await definition.GetInstanceAsync(old.InstanceId, Token); break;
            }
        });
        error.Should().BeOfType<WorkflowInstanceNotFoundException>().Which.InstanceId.Should().Be(old.InstanceId);
    }

    [Fact]
    public async Task DetachedStateAndOutput_DoNotMutateRetainedPayloads_AndMismatchErrorsPrecedeRetirement()
    {
        using var host = new Host(1);
        var definition = host.Register(); var other = host.Register();
        var instance = await host.Start(definition, "payload");
        var state = await instance.GetStateAsync<State>(Token); state.Items.Add("changed");
        var output = (WorkflowOutputResult<List<string>>.Available)await instance.GetOutputAsync(Token); output.Output.Add("changed");
        (await instance.GetStateAsync<State>(Token)).Items.Should().Equal("payload");
        ((WorkflowOutputResult<List<string>>.Available)await instance.GetOutputAsync(Token)).Output.Should().Equal("payload");
        (await Record.ExceptionAsync(async () => await instance.GetStateAsync<List<string>>(Token))).Should().BeOfType<WorkflowStateTypeMismatchException>();
        (await Record.ExceptionAsync(async () => await other.GetInstanceAsync(instance.InstanceId, Token))).Should().BeOfType<WorkflowInstanceDefinitionMismatchException>();
        var conflict = await definition.StartOrGetAsync("different", StartIdempotencyKey.Create("payload"), Token);
        conflict.Should().BeOfType<WorkflowStartResult<WorkflowInstanceHandle<List<string>>>.Conflict>();
        await host.Start(other, "new");
        state.Items.Should().Equal("payload", "changed"); output.Output.Should().Equal("payload", "changed");
        (await Record.ExceptionAsync(async () => await other.GetInstanceAsync(instance.InstanceId, Token))).Should().BeOfType<WorkflowInstanceNotFoundException>();
    }

    public sealed record State(string Key, List<string> Items);

    private sealed class Host : IDisposable
    {
        private readonly ServiceProvider services = new ServiceCollection().BuildServiceProvider();
        internal TestClock Clock { get; } = new();
        internal EphemeralWorkflowEngine Engine { get; }
        internal EphemeralWorkflowDefinitionRegistry Registry { get; }
        internal EphemeralWorkflowEventRouter Router { get; }
        internal Host(int? count, Func<InstanceId, ValueTask>? pin = null)
        {
            var options = EphemeralWorkflowEngineOptions.FromHostOptions(new EphemeralEngineHostOptions
            {
                StructuredExecution = new() { MaxConcurrentExecutionPathsPerInstance = 2, StepThrottles = [] }, TransientPools = [],
                TerminalRetention = count is { } n ? EphemeralTerminalRetentionPolicy.Create(n, TimeSpan.FromSeconds(10), TimeSpan.FromSeconds(1)) : null
            }) with { OperationPinAcquired = pin };
            Engine = new(Clock, options, services); Registry = new(Engine); Router = new(Engine, Registry);
        }
        internal EphemeralDefinitionHandle<string, List<string>> Register(bool wait = false, bool fail = false)
        {
            var builder = Workflow.Ephemeral<State>(DefinitionId.New(), DefinitionVersion.Initial)
                .Init<string>(key => new State(key, [key]));
            if (wait) builder.Wait(WorkflowEventContract.Create(EventName.Create("retention-go"), EventContractVersion.Initial), s => CorrelationId.Create(s.Value.Key));
            if (fail) builder.Then(_ => throw new InvalidOperationException("terminal failure"));
            return Registry.Register(builder.End(state => state.Value.Items).Build()).GetHandleOrThrow();
        }
        internal async Task<WorkflowInstanceHandle<List<string>>> Start(EphemeralDefinitionHandle<string, List<string>> definition, string key) =>
            (await definition.StartOrGetAsync(key, StartIdempotencyKey.Create(key), Token)).GetHandleOrThrow();
        internal ValueTask<EphemeralEventRouteResult> Deliver(WorkflowInstanceHandle<List<string>> instance, string key) =>
            Router.RouteToInstanceAsync(instance.InstanceId, EphemeralTestEvent.Create(EventId.Create(Guid.NewGuid().ToString()), EventName.Create("retention-go"), CorrelationId.Create(key), Clock.GetUtcNow()), Token);
        public void Dispose() { Engine.Dispose(); services.Dispose(); }
    }

    private sealed class TestClock : TimeProvider
    {
        private readonly FakeTimeProvider time = new(new DateTimeOffset(2026, 10, 17, 12, 0, 0, TimeSpan.Zero));
        internal int Created; internal int Active; internal TimeSpan WallJump;
        public override DateTimeOffset GetUtcNow() => time.GetUtcNow() + WallJump;
        public override long GetTimestamp() => time.GetTimestamp();
        public override long TimestampFrequency => time.TimestampFrequency;
        internal void Advance(TimeSpan delta) => time.Advance(delta);
        public override ITimer CreateTimer(TimerCallback callback, object? state, TimeSpan dueTime, TimeSpan period)
        {
            Created++; Active++;
            return new Timer(time.CreateTimer(callback, state, dueTime, period), () => Active--);
        }
        private sealed class Timer(ITimer inner, Action closed) : ITimer
        {
            private int disposed;
            public bool Change(TimeSpan dueTime, TimeSpan period) => inner.Change(dueTime, period);
            public void Dispose() { if (Interlocked.Exchange(ref disposed, 1) == 0) { inner.Dispose(); closed(); } }
            public ValueTask DisposeAsync() { Dispose(); return ValueTask.CompletedTask; }
        }
    }
}
