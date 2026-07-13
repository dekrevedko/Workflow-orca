
namespace OrcaCore.Tests;

public class WorkflowEngineTests
{
    private sealed class MyState
    {
        public string Id { get; set; } = "corr-1";
    }

    [Fact]
    public async Task ForDefinition_by_string_returns_typed_engine()
    {
        await using var engine = new WorkflowEngine();
        var def = new WorkflowBuilder<MyState>("TestDef").Init().End().Build();
        engine.ForDefinition(def); // register

        var typed = engine.ForDefinition<MyState>("TestDef");
        var snapshot = await typed.Start(new MyState());

        Assert.Equal(WorkflowStatus.Completed, snapshot.Status);
        Assert.Equal("TestDef", snapshot.DefinitionId);
    }

    [Fact]
    public async Task ForDefinition_by_string_throws_for_unknown()
    {
        await using var engine = new WorkflowEngine();

        Assert.Throws<KeyNotFoundException>(() => engine.ForDefinition<MyState>("Unknown"));
    }

    [Fact]
    public async Task Correlation_targeted_RaiseEvent_routes_to_correct_instance()
    {
        await using var engine = new WorkflowEngine();
        var def = new WorkflowBuilder<MyState>("TestDef")
            .Init()
            .Wait("Evt", s => s.Id)
            .End()
            .Build();

        var typed = engine.ForDefinition(def);
        var s1 = await typed.Start(new MyState { Id = "id-1" });
        var s2 = await typed.Start(new MyState { Id = "id-2" });

        await engine.RaiseEvent(new EventEnvelope("Evt", "id-2", null, "evt-1"));

        Assert.Equal(WorkflowStatus.Waiting, engine.Instance(s1.InstanceId).Get().Status);
        Assert.Equal(WorkflowStatus.Completed, engine.Instance(s2.InstanceId).Get().Status);
    }

    [Fact]
    public async Task Correlation_targeted_RaiseEvent_throws_when_no_match()
    {
        await using var engine = new WorkflowEngine();

        await Assert.ThrowsAsync<NoActiveWaitException>(() =>
            engine.RaiseEvent(new EventEnvelope("Evt", "no-match", null, "evt-1")));
    }

    [Fact]
    public async Task DisposeAsync_disposes_semaphores()
    {
        var engine = new WorkflowEngine();
        var def = new WorkflowBuilder<MyState>("TestDef").Init().End().Build();
        await engine.ForDefinition(def).Start(new MyState());

        await engine.DisposeAsync();

        // After dispose, the semaphore is disposed â€” trying to use it would throw
        // We verify dispose completed without error
    }

    [Fact]
    public async Task Snapshot_contains_correct_fields()
    {
        await using var engine = new WorkflowEngine();
        var def = new WorkflowBuilder<MyState>("TestDef").Init().End().Build();
        var before = DateTimeOffset.UtcNow;
        var snapshot = await engine.ForDefinition(def).Start(new MyState());
        var after = DateTimeOffset.UtcNow;

        Assert.NotEmpty(snapshot.InstanceId);
        Assert.Equal("TestDef", snapshot.DefinitionId);
        Assert.Equal(WorkflowStatus.Completed, snapshot.Status);
        Assert.InRange(snapshot.CreatedAt, before, after);
        Assert.InRange(snapshot.LastTransitionAt, before, after);
        Assert.Null(snapshot.Error);
    }

    [Fact]
    public async Task Failed_snapshot_contains_error()
    {
        await using var engine = new WorkflowEngine();
        var nodes = new List<IWorkflowNode>
        {
            new WaitLongNode<MyState>("Evt", s => s.Id)
        };
        var def = new WorkflowDefinition<MyState>("FailDef", nodes.AsReadOnly());
        var snapshot = await engine.ForDefinition(def).Start(new MyState());

        Assert.Equal(WorkflowStatus.Failed, snapshot.Status);
        Assert.NotNull(snapshot.Error);
        Assert.NotNull(snapshot.Error.Exception);
        Assert.NotNull(snapshot.Error.StepId);
        Assert.True(snapshot.Error.Timestamp > DateTimeOffset.MinValue);
    }
}
