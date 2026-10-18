using AwesomeAssertions;
using Microsoft.Extensions.DependencyInjection;
using OrcaCore.TestSupport;
using Xunit;

namespace OrcaCore.Engine.Ephemeral.Tests.Retention;

[Trait("AC", "AC-536")]
[Trait("AC", "AC-539")]
[Trait("AC", "AC-540")]
public sealed class TerminalRetentionTests
{
    [Fact]
    public async Task CountSpansDefinitions_AndTheStartToObserveGapCanRepeatAnEffect()
    {
        using var services = new ServiceCollection().BuildServiceProvider();
        using var engine = new EphemeralWorkflowEngine(TimeProvider.System, new EphemeralWorkflowEngineOptions
        {
            TerminalRetention = new(1, TimeSpan.FromHours(1), TimeSpan.FromMinutes(1))
        }, services);
        var registry = new EphemeralWorkflowDefinitionRegistry(engine);
        var effects = 0;
        EphemeralWorkflowDefinition<string, string> Definition() => Workflow.Ephemeral<State>(DefinitionId.New(), DefinitionVersion.Initial)
            .Init<string>(value => new State(value)).Then(_ => { effects++; return ValueTask.CompletedTask; })
            .End(state => state.Value.Value).Build();
        var first = registry.Register(Definition()).GetHandleOrThrow();
        var other = registry.Register(Definition()).GetHandleOrThrow();
        var key = StartIdempotencyKey.Create("expired-key");
        var accepted = await first.StartOrGetAsync("first", key, TestContext.Current.CancellationToken);
        var old = accepted.GetHandleOrThrow();
        effects.Should().Be(1);
        await other.StartOrGetAsync("second", StartIdempotencyKey.Create("other"), TestContext.Current.CancellationToken);
        var gap = await Record.ExceptionAsync(async () => await old.WaitForOutputAsync(TestContext.Current.CancellationToken));
        gap.Should().BeOfType<WorkflowInstanceNotFoundException>();
        var helperGap = await Record.ExceptionAsync(async () => await accepted.WaitForOutputAsync(TestContext.Current.CancellationToken));
        helperGap.Should().BeOfType<WorkflowInstanceNotFoundException>();
        var retry = await first.StartOrGetAsync("first", key, TestContext.Current.CancellationToken);
        retry.Should().BeOfType<WorkflowStartResult<WorkflowInstanceHandle<string>>.Accepted>().Which.WasExisting.Should().BeFalse();
        retry.GetHandleOrThrow().InstanceId.Should().NotBe(old.InstanceId);
        effects.Should().Be(3, "not-found after acceptance cannot prove that the side effect never ran");
        registry.ListInstances().Should().HaveCount(1);
    }

    [Fact]
    public async Task ExpiredKey_CompetingStartsCreateOneReplacement_AndChangedInputStillConflicts()
    {
        using var engine = new EphemeralWorkflowEngine(TimeProvider.System, new EphemeralWorkflowEngineOptions
        { TerminalRetention = new(1, TimeSpan.FromHours(1), TimeSpan.FromMinutes(1)) });
        var registry = new EphemeralWorkflowDefinitionRegistry(engine);
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var effects = 0;
        var definition = Workflow.Ephemeral<State>(DefinitionId.New(), DefinitionVersion.Initial)
            .Init<string>(value => new State(value)).Then(async _ =>
            {
                if (Interlocked.Increment(ref effects) == 1) return;
                entered.TrySetResult();
                await release.Task.WaitAsync(TimeSpan.FromSeconds(10), TestContext.Current.CancellationToken);
            }).End(state => state.Value.Value).Build();
        var handle = registry.Register(definition).GetHandleOrThrow();
        var key = StartIdempotencyKey.Create("key");
        var old = (await handle.StartOrGetAsync("input", key, TestContext.Current.CancellationToken)).GetHandleOrThrow();
        var pressure = registry.Register(Workflow.Ephemeral<State>(DefinitionId.New(), DefinitionVersion.Initial)
            .Init<string>(value => new State(value)).End().Build()).GetHandleOrThrow();
        await pressure.StartOrGetAsync("pressure", StartIdempotencyKey.Create("pressure"), TestContext.Current.CancellationToken);
        var replacement = handle.StartOrGetAsync("input", key, TestContext.Current.CancellationToken).AsTask();
        try
        {
            await entered.Task.WaitAsync(TimeSpan.FromSeconds(10), TestContext.Current.CancellationToken);
            var replay = handle.StartOrGetAsync("input", key, TestContext.Current.CancellationToken).AsTask();
            var changed = handle.StartOrGetAsync("changed", key, TestContext.Current.CancellationToken).AsTask();
            release.TrySetResult();
            var first = (await replacement).GetHandleOrThrow();
            (await replay).GetHandleOrThrow().InstanceId.Should().Be(first.InstanceId);
            first.InstanceId.Should().NotBe(old.InstanceId);
            (await changed).Should().BeOfType<WorkflowStartResult<WorkflowInstanceHandle<string>>.Conflict>();
            effects.Should().Be(2);
        }
        finally { release.TrySetResult(); }
    }

    [Fact]
    public async Task RetainedHandle_DoesNotRootTheRetiredRuntime()
    {
        using var engine = new EphemeralWorkflowEngine(TimeProvider.System, new EphemeralWorkflowEngineOptions
        { TerminalRetention = new(1, TimeSpan.FromHours(1), TimeSpan.FromMinutes(1)) });
        var registry = new EphemeralWorkflowDefinitionRegistry(engine);
        var definition = registry.Register(Workflow.Ephemeral<State>(DefinitionId.New(), DefinitionVersion.Initial)
            .Init<string>(value => new State(value)).End().Build()).GetHandleOrThrow();
        var (handle, weak) = CreateWeak(engine, definition);
        weak.IsAlive.Should().BeTrue();
        await definition.StartOrGetAsync("new", StartIdempotencyKey.Create("new"), TestContext.Current.CancellationToken);
        // Diagnostic GC belongs to this reachability regression only, never the product.
        GC.Collect(); GC.WaitForPendingFinalizers(); GC.Collect();
        weak.IsAlive.Should().BeFalse();
        GC.KeepAlive(handle);
    }

    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
    private static (WorkflowInstanceHandle, WeakReference) CreateWeak(EphemeralWorkflowEngine engine, EphemeralDefinitionHandle<string> definition)
    {
        var handle = definition.StartOrGetAsync("old", StartIdempotencyKey.Create("old"), TestContext.Current.CancellationToken).AsTask().GetAwaiter().GetResult().GetHandleOrThrow();
        engine.TryGetFacadeInstance(handle.InstanceId, out var runtime).Should().BeTrue();
        return (handle, new WeakReference(runtime));
    }

    private sealed record State(string Value);
}
