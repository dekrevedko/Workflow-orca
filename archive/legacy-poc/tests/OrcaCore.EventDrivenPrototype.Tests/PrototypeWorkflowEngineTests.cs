using OrcaCore.Abstractions.Contracts;
using OrcaCore.Abstractions.Enums;
using OrcaCore.Abstractions.Models;
using OrcaCore.EventDrivenPrototype.Definitions;
using OrcaCore.EventDrivenPrototype.Engine;
using OrcaCore.EventDrivenPrototype.Persistence;
using OrcaCore.EventDrivenPrototype.Projections;

namespace OrcaCore.EventDrivenPrototype.Tests;

public sealed class PrototypeWorkflowEngineTests
{
    [Fact]
    public async Task StartAsync_runs_straight_line_workflow_and_emits_committed_events()
    {
        var store = new InMemoryPrototypeStore();
        var engine = new EventDrivenWorkflowEngine(store);

        engine.Register(new EventDrivenWorkflowDefinition<CounterState>(
            "counter",
            "v1",
            [
                new InlineStep<CounterState>("increment", static ctx =>
                {
                    ctx.State.Count++;
                    return Task.FromResult<StepResult>(new StepResult.Completed());
                })
            ]));

        var instanceId = await engine.StartAsync("counter", "v1", new CounterState());
        var summary = await engine.GetSummaryAsync(instanceId);

        Assert.NotNull(summary);
        Assert.Equal(WorkflowStatus.Completed, summary.Status);
        Assert.Equal(0, summary.ActiveWaitCount);

        var state = await engine.GetStateAsync<CounterState>(instanceId);

        Assert.Equal(1, state.Count);

        var stream = await store.LoadStreamAsync(instanceId, CancellationToken.None);

        Assert.Contains(stream, e => e.EventType == PrototypeEventTypes.WorkflowStarted);
        Assert.Contains(stream, e => e.EventType == PrototypeEventTypes.StepCompleted);
        Assert.Contains(stream, e => e.EventType == PrototypeEventTypes.WorkflowCompleted);
    }

    [Fact]
    public async Task Matching_event_resumes_after_engine_restart_from_checkpoint_and_stream_tail()
    {
        var store = new InMemoryPrototypeStore();
        var definition = new EventDrivenWorkflowDefinition<CounterState>(
            "approval",
            "v1",
            [
                new InlineStep<CounterState>("seed", static ctx =>
                {
                    ctx.State.Count = 10;
                    return Task.FromResult<StepResult>(new StepResult.Completed());
                }),
                new InlineStep<CounterState>("wait", static _ =>
                    Task.FromResult<StepResult>(new StepResult.WaitForEvent("approved", "order-1"))),
                new InlineStep<CounterState>("resume", static ctx =>
                {
                    ctx.State.Count += (int?)ctx.ResumedEvent?.Payload ?? 0;
                    return Task.FromResult<StepResult>(new StepResult.Completed());
                })
            ]);

        var engine1 = new EventDrivenWorkflowEngine(store);
        engine1.Register(definition);

        var instanceId = await engine1.StartAsync("approval", "v1", new CounterState());
        var waitingSummary = await engine1.GetSummaryAsync(instanceId);

        Assert.Equal(WorkflowStatus.Waiting, waitingSummary!.Status);

        var engine2 = new EventDrivenWorkflowEngine(store);
        engine2.Register(definition);

        await engine2.RaiseEventToInstanceAsync(instanceId, new EventEnvelope("approved", "order-1", 5, "evt-1"));

        var summary = await engine2.GetSummaryAsync(instanceId);
        var state = await engine2.GetStateAsync<CounterState>(instanceId);

        Assert.Equal(WorkflowStatus.Completed, summary!.Status);
        Assert.Equal(15, state.Count);
    }

    [Fact]
    public async Task Out_of_order_event_is_buffered_then_consumed_when_wait_registers()
    {
        var store = new InMemoryPrototypeStore();
        var engine = new EventDrivenWorkflowEngine(store);

        engine.Register(new EventDrivenWorkflowDefinition<CounterState>(
            "buffering",
            "v1",
            [
                new InlineStep<CounterState>("pre", static ctx =>
                {
                    ctx.State.Count++;
                    return Task.FromResult<StepResult>(new StepResult.Completed());
                }),
                new InlineStep<CounterState>("wait", static _ =>
                    Task.FromResult<StepResult>(new StepResult.WaitForEvent("ready", "job-1"))),
                new InlineStep<CounterState>("post", static ctx =>
                {
                    ctx.State.Count += (int?)ctx.ResumedEvent?.Payload ?? 0;
                    return Task.FromResult<StepResult>(new StepResult.Completed());
                })
            ]));

        var instanceId = await engine.StartAsync("buffering", "v1", new CounterState());

        await engine.RaiseEventToInstanceAsync(instanceId, new EventEnvelope("ready", "job-1", 4, "evt-1"));

        var state = await engine.GetStateAsync<CounterState>(instanceId);
        var summary = await engine.GetSummaryAsync(instanceId);

        Assert.Equal(5, state.Count);
        Assert.Equal(WorkflowStatus.Completed, summary!.Status);
        Assert.Empty(await engine.GetActiveWaitsAsync(instanceId));
    }

    [Fact]
    public async Task Duplicate_event_is_ignored_after_restart_using_inbox_state()
    {
        var store = new InMemoryPrototypeStore();
        var definition = new EventDrivenWorkflowDefinition<CounterState>(
            "dedup",
            "v1",
            [
                new InlineStep<CounterState>("wait", static _ =>
                    Task.FromResult<StepResult>(new StepResult.WaitForEvent("ready", "job-1"))),
                new InlineStep<CounterState>("resume", static ctx =>
                {
                    ctx.State.Count++;
                    return Task.FromResult<StepResult>(new StepResult.Completed());
                })
            ]);

        var engine1 = new EventDrivenWorkflowEngine(store);
        engine1.Register(definition);

        var instanceId = await engine1.StartAsync("dedup", "v1", new CounterState());

        await engine1.RaiseEventToInstanceAsync(instanceId, new EventEnvelope("ready", "job-1", null, "evt-1"));

        var engine2 = new EventDrivenWorkflowEngine(store);
        engine2.Register(definition);

        await engine2.RaiseEventToInstanceAsync(instanceId, new EventEnvelope("ready", "job-1", null, "evt-1"));

        var state = await engine2.GetStateAsync<CounterState>(instanceId);
        var inbox = await store.GetInboxAsync(instanceId, CancellationToken.None);

        Assert.Equal(1, state.Count);
        Assert.Single(inbox, x => x.EventId == "evt-1");
    }

    [Fact]
    public async Task Correlation_targeted_routing_resolves_single_waiting_instance_from_projection()
    {
        var store = new InMemoryPrototypeStore();
        var engine = new EventDrivenWorkflowEngine(store);
        var definition = new EventDrivenWorkflowDefinition<CounterState>(
            "routing",
            "v1",
            [
                new InlineStep<CounterState>("wait", static _ =>
                    Task.FromResult<StepResult>(new StepResult.WaitForEvent("ready", "unset"))),
                new InlineStep<CounterState>("resume", static ctx =>
                {
                    ctx.State.Count += (int?)ctx.ResumedEvent?.Payload ?? 0;
                    return Task.FromResult<StepResult>(new StepResult.Completed());
                })
            ]);

        engine.Register(definition);

        var firstId = await engine.StartAsync(
            "routing",
            "v1",
            new CounterState(),
            correlationIdOverride: "order-1");
        var secondId = await engine.StartAsync(
            "routing",
            "v1",
            new CounterState(),
            correlationIdOverride: "order-2");

        await engine.RaiseEventByCorrelationAsync(new EventEnvelope("ready", "order-2", 7, "evt-2"));

        var first = await engine.GetSummaryAsync(firstId);
        var second = await engine.GetStateAsync<CounterState>(secondId);

        Assert.Equal(WorkflowStatus.Waiting, first!.Status);
        Assert.Equal(7, second.Count);
    }

    [Fact]
    public async Task Concurrent_resume_attempts_on_same_instance_serialize_to_one_committed_outcome()
    {
        var store = new InMemoryPrototypeStore();
        var definition = new EventDrivenWorkflowDefinition<CounterState>(
            "concurrent",
            "v1",
            [
                new InlineStep<CounterState>("wait", static _ =>
                    Task.FromResult<StepResult>(new StepResult.WaitForEvent("ready", "job-1"))),
                new InlineStep<CounterState>("resume", static ctx =>
                {
                    ctx.State.Count++;
                    return Task.FromResult<StepResult>(new StepResult.Completed());
                })
            ]);

        var engine = new EventDrivenWorkflowEngine(store);
        engine.Register(definition);

        var instanceId = await engine.StartAsync("concurrent", "v1", new CounterState());
        var barrier = new Barrier(2);

        var first = Task.Run(async () =>
        {
            barrier.SignalAndWait();
            await engine.RaiseEventToInstanceAsync(instanceId, new EventEnvelope("ready", "job-1", null, "evt-1"));
        });

        var second = Task.Run(async () =>
        {
            barrier.SignalAndWait();
            await engine.RaiseEventToInstanceAsync(instanceId, new EventEnvelope("ready", "job-1", null, "evt-1"));
        });

        await Task.WhenAll(first, second);

        var state = await engine.GetStateAsync<CounterState>(instanceId);
        var inbox = await store.GetInboxAsync(instanceId, CancellationToken.None);

        Assert.Equal(1, state.Count);
        Assert.Single(inbox, x => x.EventId == "evt-1");
    }

    public sealed class CounterState
    {
        public int Count { get; set; }
    }

    private sealed class InlineStep<TState>(string stepId, Func<StepContext<TState>, Task<StepResult>> handler) : IStep<TState>
    {
        public string StepId { get; } = stepId;

        public Task<StepResult> ExecuteAsync(StepContext<TState> context) => handler(context);
    }
}
