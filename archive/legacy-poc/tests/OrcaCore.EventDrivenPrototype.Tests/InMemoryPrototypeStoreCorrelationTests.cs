using OrcaCore.Abstractions.Contracts;
using OrcaCore.Abstractions.Models;
using OrcaCore.Abstractions.Primitives;
using OrcaCore.EventDrivenPrototype.Definitions;
using OrcaCore.EventDrivenPrototype.Engine;
using OrcaCore.EventDrivenPrototype.Persistence;

namespace OrcaCore.EventDrivenPrototype.Tests;

public sealed class InMemoryPrototypeStoreCorrelationTests
{
    private sealed class WaitStep : IStep<CounterState>
    {
        public string StepId => "wait";

        public Task<StepResult> ExecuteAsync(StepContext<CounterState> context) =>
            Task.FromResult<StepResult>(new StepResult.WaitForEvent("ready", "corr-1"));
    }

    private sealed class CounterState
    {
        public int Count { get; set; }
    }

    [Fact]
    public async Task TryResolveByCorrelationAsync_returns_None_when_no_active_wait()
    {
        var store = new InMemoryPrototypeStore();
        var resolved = await store.TryResolveByCorrelationAsync("ready", "corr-1", CancellationToken.None);

        Assert.False(resolved.HasValue);
    }

    [Fact]
    public async Task TryResolveByCorrelationAsync_returns_Some_instance_id_when_single_match()
    {
        var store = new InMemoryPrototypeStore();
        var engine = new EventDrivenWorkflowEngine(store);
        var definition = new EventDrivenWorkflowDefinition<CounterState>(
            "r",
            "v1",
            [new WaitStep()]);

        engine.Register(definition);
        var instanceId = await engine.StartAsync("r", "v1", new CounterState(), correlationIdOverride: "corr-1");

        var resolved = await store.TryResolveByCorrelationAsync("ready", "corr-1", CancellationToken.None);

        Assert.True(resolved.HasValue);
        Assert.Equal(instanceId, resolved.Value);
    }

    [Fact]
    public async Task RaiseEventByCorrelationAsync_throws_when_TryResolve_is_None()
    {
        var store = new InMemoryPrototypeStore();
        var engine = new EventDrivenWorkflowEngine(store);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            engine.RaiseEventByCorrelationAsync(
                new EventEnvelope("ready", "missing", null, "e1"),
                CancellationToken.None));

        Assert.Contains("No active wait", ex.Message, StringComparison.Ordinal);
        Assert.Contains("ready", ex.Message, StringComparison.Ordinal);
        Assert.Contains("missing", ex.Message, StringComparison.Ordinal);
    }
}
