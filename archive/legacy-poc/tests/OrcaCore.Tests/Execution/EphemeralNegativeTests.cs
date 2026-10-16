using System.Linq.Expressions;

namespace OrcaCore.Tests;

public class EphemeralNegativeTests
{
    private sealed class MyState
    {
        public string Id { get; set; } = "corr-1";
        public int Count { get; set; }
    }

    private sealed class NoOpStep : IStep<MyState>
    {
        public string StepId => "NoOp";
        public Task<StepResult> ExecuteAsync(StepContext<MyState> context)
            => Task.FromResult<StepResult>(new StepResult.Completed());
    }

    private sealed class ThrowStep : IStep<MyState>
    {
        public string StepId => "Throw";
        public Task<StepResult> ExecuteAsync(StepContext<MyState> context)
            => throw new InvalidOperationException("step boom");
    }

    private sealed class OtherState
    {
        public string Id { get; set; } = "other";
    }

    // â”€â”€ Builder negative tests â”€â”€

    [Fact]
    public void Wait_before_Init_throws()
    {
        var builder = new WorkflowBuilder<MyState>("Test");
        var ex = Assert.Throws<InvalidOperationException>(() =>
            builder.Wait("Evt", s => s.Id));
        Assert.Contains("Init", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void While_before_Init_throws()
    {
        var builder = new WorkflowBuilder<MyState>("Test");
        var ex = Assert.Throws<InvalidOperationException>(() =>
            builder.While(_ => true, b => b.Then<NoOpStep>()));
        Assert.Contains("Init", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void If_before_Init_throws()
    {
        var builder = new WorkflowBuilder<MyState>("Test");
        var ex = Assert.Throws<InvalidOperationException>(() =>
            builder.If(_ => true, then: b => b.Then<NoOpStep>()));
        Assert.Contains("Init", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Parallel_before_Init_throws()
    {
        var builder = new WorkflowBuilder<MyState>("Test");
        var ex = Assert.Throws<InvalidOperationException>(() =>
            builder.Parallel(p => p.Branch("A", b => b.Then<NoOpStep>())));
        Assert.Contains("Init", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Wait_after_End_throws()
    {
        var builder = new WorkflowBuilder<MyState>("Test").Init().End();
        var ex = Assert.Throws<InvalidOperationException>(() =>
            builder.Wait("Evt", s => s.Id));
        Assert.Contains("after End", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void While_after_End_throws()
    {
        var builder = new WorkflowBuilder<MyState>("Test").Init().End();
        var ex = Assert.Throws<InvalidOperationException>(() =>
            builder.While(_ => true, b => b.Then<NoOpStep>()));
        Assert.Contains("after End", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void If_after_End_throws()
    {
        var builder = new WorkflowBuilder<MyState>("Test").Init().End();
        var ex = Assert.Throws<InvalidOperationException>(() =>
            builder.If(_ => true, then: b => b.Then<NoOpStep>()));
        Assert.Contains("after End", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Parallel_after_End_throws()
    {
        var builder = new WorkflowBuilder<MyState>("Test").Init().End();
        var ex = Assert.Throws<InvalidOperationException>(() =>
            builder.Parallel(p => p.Branch("A", b => b.Then<NoOpStep>())));
        Assert.Contains("after End", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    // â”€â”€ Engine instance lookup negatives â”€â”€

    [Fact]
    public async Task Instance_with_nonexistent_id_Get_throws()
    {
        await using var engine = new WorkflowEngine();
        var scope = engine.Instance("nonexistent-id");

        Assert.Throws<KeyNotFoundException>(() => scope.Get());
    }

    [Fact]
    public async Task Instance_with_nonexistent_id_GetState_throws()
    {
        await using var engine = new WorkflowEngine();
        var scope = engine.Instance("nonexistent-id");

        Assert.Throws<KeyNotFoundException>(() => scope.GetState<MyState>());
    }

    [Fact]
    public async Task Instance_with_nonexistent_id_RaiseEvent_throws()
    {
        await using var engine = new WorkflowEngine();
        var scope = engine.Instance("nonexistent-id");

        await Assert.ThrowsAsync<KeyNotFoundException>(() =>
            scope.RaiseEvent(new EventEnvelope("Evt", "corr-1", null, "evt-1")));
    }

    [Fact]
    public async Task Instance_with_nonexistent_id_GetActiveWaits_throws()
    {
        await using var engine = new WorkflowEngine();
        var scope = engine.Instance("nonexistent-id");

        Assert.Throws<KeyNotFoundException>(() => scope.GetActiveWaits());
    }

    // â”€â”€ ForDefinition negatives â”€â”€

    [Fact]
    public async Task ForDefinition_by_string_with_wrong_type_throws()
    {
        await using var engine = new WorkflowEngine();
        var def = new WorkflowBuilder<MyState>("TestDef").Init().End().Build();
        engine.ForDefinition(def);

        // Registered as MyState, try to resolve as OtherState
        Assert.Throws<InvalidCastException>(() =>
            engine.ForDefinition<OtherState>("TestDef"));
    }

    [Fact]
    public async Task ForDefinition_same_id_different_registrations_last_wins()
    {
        await using var engine = new WorkflowEngine();
        var def1 = new WorkflowBuilder<MyState>("SameId").Init().End().Build();
        var def2 = new WorkflowBuilder<MyState>("SameId").Init().Then<NoOpStep>().End().Build();

        engine.ForDefinition(def1);
        var typed = engine.ForDefinition(def2);

        // Should use def2 (last registration)
        var snapshot = await typed.Start(new MyState());
        Assert.Equal(WorkflowStatus.Completed, snapshot.Status);
    }

    // â”€â”€ Terminal state negatives â”€â”€

    [Fact]
    public async Task Completed_instance_GetActiveWaits_returns_empty()
    {
        await using var engine = new WorkflowEngine();
        var def = new WorkflowBuilder<MyState>("TestDef").Init().End().Build();
        var snapshot = await engine.ForDefinition(def).Start(new MyState());

        var waits = engine.Instance(snapshot.InstanceId).GetActiveWaits();
        Assert.Empty(waits);
    }

    [Fact]
    public async Task Failed_instance_GetActiveWaits_returns_empty()
    {
        await using var engine = new WorkflowEngine();
        var def = new WorkflowBuilder<MyState>("TestDef").Init().Then<ThrowStep>().End().Build();
        var snapshot = await engine.ForDefinition(def).Start(new MyState());

        Assert.Equal(WorkflowStatus.Failed, snapshot.Status);
        var waits = engine.Instance(snapshot.InstanceId).GetActiveWaits();
        Assert.Empty(waits);
    }

    // â”€â”€ Event dedup edge cases â”€â”€

    [Fact]
    public async Task Duplicate_event_id_to_waiting_instance_is_silently_ignored()
    {
        await using var engine = new WorkflowEngine();
        var def = new WorkflowBuilder<MyState>("TestDef")
            .Init()
            .Wait("Evt", s => s.Id)
            .Wait("Evt2", s => s.Id)
            .End()
            .Build();
        var snapshot = await engine.ForDefinition(def).Start(new MyState());
        var scope = engine.Instance(snapshot.InstanceId);

        await scope.RaiseEvent(new EventEnvelope("Evt", "corr-1", null, "evt-1"));
        Assert.Equal(WorkflowStatus.Waiting, scope.Get().Status);

        // Same EventId again â€” different event name but same ID, should be deduped
        await scope.RaiseEvent(new EventEnvelope("Evt2", "corr-1", null, "evt-1"));
        Assert.Equal(WorkflowStatus.Waiting, scope.Get().Status);
    }

    [Fact]
    public async Task Buffered_event_with_duplicate_id_is_silently_ignored()
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

        // Buffer Second event
        await scope.RaiseEvent(new EventEnvelope("Second", "corr-1", null, "evt-2"));
        // Send again with same EventId â€” should be deduped
        await scope.RaiseEvent(new EventEnvelope("Second", "corr-1", null, "evt-2"));

        // Resume with First
        await scope.RaiseEvent(new EventEnvelope("First", "corr-1", null, "evt-1"));

        Assert.Equal(WorkflowStatus.Completed, scope.Get().Status);
    }

    // â”€â”€ Correlation routing negatives â”€â”€

    [Fact]
    public async Task Engine_RaiseEvent_with_no_waiting_instances_throws()
    {
        await using var engine = new WorkflowEngine();
        var def = new WorkflowBuilder<MyState>("TestDef").Init().End().Build();
        await engine.ForDefinition(def).Start(new MyState());

        // No instances are waiting
        await Assert.ThrowsAsync<NoActiveWaitException>(() =>
            engine.RaiseEvent(new EventEnvelope("Evt", "corr-1", null, "evt-1")));
    }

    [Fact]
    public async Task Engine_RaiseEvent_with_wrong_correlation_throws()
    {
        await using var engine = new WorkflowEngine();
        var def = new WorkflowBuilder<MyState>("TestDef")
            .Init()
            .Wait("Evt", s => s.Id)
            .End()
            .Build();
        await engine.ForDefinition(def).Start(new MyState());

        // Wrong correlation ID
        await Assert.ThrowsAsync<NoActiveWaitException>(() =>
            engine.RaiseEvent(new EventEnvelope("Evt", "wrong-corr", null, "evt-1")));
    }

    [Fact]
    public async Task Engine_RaiseEvent_with_wrong_event_name_throws()
    {
        await using var engine = new WorkflowEngine();
        var def = new WorkflowBuilder<MyState>("TestDef")
            .Init()
            .Wait("Expected", s => s.Id)
            .End()
            .Build();
        await engine.ForDefinition(def).Start(new MyState());

        // Wrong event name â€” no correlation index entry for this
        await Assert.ThrowsAsync<NoActiveWaitException>(() =>
            engine.RaiseEvent(new EventEnvelope("WrongName", "corr-1", null, "evt-1")));
    }

    // â”€â”€ Unmatched event buffering on waiting instance â”€â”€

    [Fact]
    public async Task Unmatched_event_on_waiting_instance_does_not_throw()
    {
        await using var engine = new WorkflowEngine();
        var def = new WorkflowBuilder<MyState>("TestDef")
            .Init()
            .Wait("Expected", s => s.Id)
            .End()
            .Build();
        var snapshot = await engine.ForDefinition(def).Start(new MyState());
        var scope = engine.Instance(snapshot.InstanceId);

        // Wrong event name â€” instance is Waiting so it should buffer, not throw
        await scope.RaiseEvent(new EventEnvelope("Unrelated", "corr-1", null, "evt-1"));

        Assert.Equal(WorkflowStatus.Waiting, scope.Get().Status);
    }

    // â”€â”€ Query validator negatives â”€â”€

    [Fact]
    public void Query_with_indexer_access_is_rejected()
    {
        Expression<Func<WorkflowInstanceSnapshot, bool>> expression =
            x => x.InstanceId[0] == 'a';

        Assert.Throws<InvalidOperationException>(() =>
            WorkflowQueryValidator.Validate(expression));
    }

    [Fact]
    public void Query_with_new_expression_is_rejected()
    {
        Expression<Func<WorkflowInstanceSnapshot, bool>> expression =
            x => x.CreatedAt > new DateTimeOffset(2024, 1, 1, 0, 0, 0, TimeSpan.Zero);

        // new DateTimeOffset(...) is a NewExpression â€” should be rejected
        Assert.Throws<InvalidOperationException>(() =>
            WorkflowQueryValidator.Validate(expression));
    }

    [Fact]
    public void Query_with_conditional_ternary_is_rejected()
    {
        Expression<Func<WorkflowInstanceSnapshot, bool>> expression =
            x => (x.Status == WorkflowStatus.Completed ? true : false);

        Assert.Throws<InvalidOperationException>(() =>
            WorkflowQueryValidator.Validate(expression));
    }

    // â”€â”€ Lifecycle self-transition negatives â”€â”€

    [Theory]
    [InlineData(WorkflowStatus.Running, WorkflowStatus.Running)]
    [InlineData(WorkflowStatus.Waiting, WorkflowStatus.Waiting)]
    [InlineData(WorkflowStatus.Completed, WorkflowStatus.Completed)]
    [InlineData(WorkflowStatus.Failed, WorkflowStatus.Failed)]
    public void Self_transitions_are_rejected(WorkflowStatus status, WorkflowStatus same)
    {
        var state = new RuntimeState { Status = status };

        Assert.Throws<InvalidOperationException>(() =>
            InstanceLifecycle.TransitionTo(state, same));
    }

    // â”€â”€ WaitLong in ephemeral mode â”€â”€

    [Fact]
    public async Task WaitLong_in_ephemeral_workflow_fails_with_descriptive_error()
    {
        var nodes = new List<IWorkflowNode>
        {
            new WaitLongNode<MyState>("Evt", s => s.Id)
        };
        var def = new WorkflowDefinition<MyState>("EphemeralWaitLong", nodes.AsReadOnly());

        await using var engine = new WorkflowEngine();
        var snapshot = await engine.ForDefinition(def).Start(new MyState());

        Assert.Equal(WorkflowStatus.Failed, snapshot.Status);
        Assert.NotNull(snapshot.Error);
        Assert.Contains("durable", snapshot.Error.Exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    // â”€â”€ Parallel with all branches failing â”€â”€

    private sealed class FailStep : IStep<MyState>
    {
        public string StepId => "Fail";
        public Task<StepResult> ExecuteAsync(StepContext<MyState> context)
            => Task.FromResult<StepResult>(new StepResult.Failed(new Exception("boom")));
    }

    [Fact]
    public async Task Parallel_with_all_branches_failing_fails_workflow()
    {
        var def = new WorkflowBuilder<MyState>("AllFail")
            .Init()
            .Parallel(p => p
                .Branch("A", b => b.Then<FailStep>())
                .Branch("B", b => b.Then<FailStep>()))
            .End()
            .Build();

        await using var engine = new WorkflowEngine();
        var snapshot = await engine.ForDefinition(def).Start(new MyState());

        Assert.Equal(WorkflowStatus.Failed, snapshot.Status);
        Assert.NotNull(snapshot.Error);
    }

    // â”€â”€ Step exception produces Failed with exception info â”€â”€

    [Fact]
    public async Task Step_throwing_exception_captures_step_id_and_message()
    {
        var def = new WorkflowBuilder<MyState>("ThrowTest")
            .Init()
            .Then<ThrowStep>()
            .End()
            .Build();

        await using var engine = new WorkflowEngine();
        var snapshot = await engine.ForDefinition(def).Start(new MyState());

        Assert.Equal(WorkflowStatus.Failed, snapshot.Status);
        Assert.Equal("Throw", snapshot.Error!.StepId);
        Assert.Contains("step boom", snapshot.Error.Exception.Message);
    }

    // â”€â”€ Disposed engine â”€â”€

    [Fact]
    public async Task Disposed_engine_semaphores_prevent_resume()
    {
        var engine = new WorkflowEngine();
        var def = new WorkflowBuilder<MyState>("TestDef")
            .Init()
            .Wait("Evt", s => s.Id)
            .End()
            .Build();
        var snapshot = await engine.ForDefinition(def).Start(new MyState());
        var scope = engine.Instance(snapshot.InstanceId);

        await engine.DisposeAsync();

        // After dispose, semaphore is disposed â€” RaiseEvent should throw
        await Assert.ThrowsAsync<ObjectDisposedException>(() =>
            scope.RaiseEvent(new EventEnvelope("Evt", "corr-1", null, "evt-1")));
    }

    // â”€â”€ Selection scope on empty engine â”€â”€

    [Fact]
    public async Task All_on_empty_engine_returns_empty()
    {
        await using var engine = new WorkflowEngine();

        Assert.Empty(engine.All().List());
        Assert.Equal(0, engine.All().Count());
    }

    [Fact]
    public async Task Where_on_empty_engine_returns_empty()
    {
        await using var engine = new WorkflowEngine();

        Assert.Empty(engine.Where(x => x.Status == WorkflowStatus.Waiting).List());
        Assert.Equal(0, engine.Where(x => x.Status == WorkflowStatus.Waiting).Count());
    }

    // â”€â”€ Typed engine fanout on completed instances â”€â”€

    [Fact]
    public async Task Typed_RaiseEvent_fanout_to_completed_instances_throws()
    {
        await using var engine = new WorkflowEngine();
        var def = new WorkflowBuilder<MyState>("TestDef").Init().End().Build();
        var typed = engine.ForDefinition(def);
        await typed.Start(new MyState());

        // All instances are Completed â€” fanout should throw on each
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            typed.RaiseEvent(new EventEnvelope("Evt", "corr-1", null, "evt-1")));
    }

    // â”€â”€ CorrelationIndex: remove nonexistent is safe â”€â”€

    [Fact]
    public void CorrelationIndex_remove_nonexistent_does_not_throw()
    {
        var index = new CorrelationIndex();

        // Should not throw
        index.Remove("NoEvent", "no-corr", "no-instance");
    }

    [Fact]
    public void CorrelationIndex_remove_wrong_instance_does_not_affect_others()
    {
        var index = new CorrelationIndex();
        index.Add("Evt", "corr-1", "inst-1");
        index.Add("Evt", "corr-1", "inst-2");

        index.Remove("Evt", "corr-1", "inst-1");

        // inst-2 is still resolvable
        var result = index.ResolveExactlyOne("Evt", "corr-1");
        Assert.Equal("inst-2", result);
    }

    // â”€â”€ Multiple starts produce unique instance IDs â”€â”€

    [Fact]
    public async Task Multiple_starts_produce_unique_instance_ids()
    {
        await using var engine = new WorkflowEngine();
        var def = new WorkflowBuilder<MyState>("TestDef").Init().End().Build();
        var typed = engine.ForDefinition(def);

        var ids = new HashSet<string>();
        for (var i = 0; i < 50; i++)
        {
            var snapshot = await typed.Start(new MyState());
            Assert.True(ids.Add(snapshot.InstanceId), "Duplicate instance ID generated");
        }
    }
}
