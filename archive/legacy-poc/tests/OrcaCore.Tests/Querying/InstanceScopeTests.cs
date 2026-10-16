
namespace OrcaCore.Tests;

public class InstanceScopeTests
{
    private sealed class MyState
    {
        public string Id { get; set; } = "corr-1";
        public string Value { get; set; } = "original";
    }

    private sealed class NoOpStep : IStep<MyState>
    {
        public string StepId => "NoOp";
        public Task<StepResult> ExecuteAsync(StepContext<MyState> context)
            => Task.FromResult<StepResult>(new StepResult.Completed());
    }

    [Fact]
    public async Task Get_returns_snapshot_with_all_fields()
    {
        await using var engine = new WorkflowEngine();
        var def = new WorkflowBuilder<MyState>("TestDef").Init().Then<NoOpStep>().End().Build();
        var snapshot = await engine.ForDefinition(def).Start(new MyState());

        var scope = engine.Instance(snapshot.InstanceId);
        var result = scope.Get();

        Assert.Equal(snapshot.InstanceId, result.InstanceId);
        Assert.Equal("TestDef", result.DefinitionId);
        Assert.Equal(WorkflowStatus.Completed, result.Status);
        Assert.True(result.LastTransitionAt >= result.CreatedAt);
    }

    [Fact]
    public async Task GetState_returns_deep_copy()
    {
        await using var engine = new WorkflowEngine();
        var def = new WorkflowBuilder<MyState>("TestDef").Init().End().Build();
        var snapshot = await engine.ForDefinition(def).Start(new MyState());

        var scope = engine.Instance(snapshot.InstanceId);
        var state1 = scope.GetState<MyState>();
        state1.Value = "mutated";

        var state2 = scope.GetState<MyState>();
        Assert.Equal("original", state2.Value);
    }

    [Fact]
    public async Task RaiseEvent_deduplicates_by_event_id()
    {
        await using var engine = new WorkflowEngine();
        var def = new WorkflowBuilder<MyState>("TestDef")
            .Init()
            .Wait("Evt", s => s.Id)
            .End()
            .Build();
        var snapshot = await engine.ForDefinition(def).Start(new MyState());
        var scope = engine.Instance(snapshot.InstanceId);

        await scope.RaiseEvent(new EventEnvelope("Evt", "corr-1", null, "evt-1"));
        Assert.Equal(WorkflowStatus.Completed, scope.Get().Status);

        // Same EventId again â€” should be silently ignored (dedup)
        // The workflow is now Completed. A duplicate of the already-consumed event
        // is caught by the ConsumedEventIds check before the terminal state check.
        await scope.RaiseEvent(new EventEnvelope("Evt", "corr-1", null, "evt-1"));
        Assert.Equal(WorkflowStatus.Completed, scope.Get().Status);
    }

    [Fact]
    public async Task RaiseEvent_buffers_when_no_matching_wait()
    {
        await using var engine = new WorkflowEngine();
        var def = new WorkflowBuilder<MyState>("TestDef")
            .Init()
            .Wait("First", s => s.Id)
            .Wait("Second", s => s.Id)
            .End()
            .Build();
        var snapshot = await engine.ForDefinition(def).Start(new MyState());
        var scope = engine.Instance(snapshot.InstanceId);

        // Send "Second" before "First" â€” should be buffered
        await scope.RaiseEvent(new EventEnvelope("Second", "corr-1", null, "evt-2"));
        Assert.Equal(WorkflowStatus.Waiting, scope.Get().Status);

        // Now send "First" â€” should resume and consume buffered "Second"
        await scope.RaiseEvent(new EventEnvelope("First", "corr-1", null, "evt-1"));
        Assert.Equal(WorkflowStatus.Completed, scope.Get().Status);
    }

    [Fact]
    public async Task RaiseEvent_on_terminal_throws()
    {
        await using var engine = new WorkflowEngine();
        var def = new WorkflowBuilder<MyState>("TestDef").Init().End().Build();
        var snapshot = await engine.ForDefinition(def).Start(new MyState());

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            engine.Instance(snapshot.InstanceId)
                .RaiseEvent(new EventEnvelope("Evt", "corr-1", null, "evt-1")));
    }

    [Fact]
    public async Task GetActiveWaits_returns_only_active()
    {
        await using var engine = new WorkflowEngine();
        var def = new WorkflowBuilder<MyState>("TestDef")
            .Init()
            .Wait("Evt", s => s.Id)
            .End()
            .Build();
        var snapshot = await engine.ForDefinition(def).Start(new MyState());
        var scope = engine.Instance(snapshot.InstanceId);

        var waits = scope.GetActiveWaits();
        Assert.Single(waits);
        Assert.Equal("Evt", waits[0].EventName);
        Assert.Equal("corr-1", waits[0].CorrelationId);

        await scope.RaiseEvent(new EventEnvelope("Evt", "corr-1", null, "evt-1"));
        Assert.Empty(scope.GetActiveWaits());
    }
}

