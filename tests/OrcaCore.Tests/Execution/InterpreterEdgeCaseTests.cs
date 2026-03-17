
namespace OrcaCore.Tests;

public class InterpreterEdgeCaseTests
{
    private sealed class MyState
    {
        public string Id { get; set; } = "corr-1";
        public int Count { get; set; }
        public bool Flag { get; set; }
        public List<string> Log { get; set; } = [];
    }

    private sealed class IncrementStep : IStep<MyState>
    {
        public string StepId => "Increment";
        public Task<StepResult> ExecuteAsync(StepContext<MyState> context)
        {
            context.State.Count++;
            return Task.FromResult<StepResult>(new StepResult.Completed());
        }
    }

    private sealed class LogStep : IStep<MyState>
    {
        public string StepId => "Log";
        public Task<StepResult> ExecuteAsync(StepContext<MyState> context)
        {
            context.State.Log.Add("executed");
            return Task.FromResult<StepResult>(new StepResult.Completed());
        }
    }

    private sealed class SetFlagStep : IStep<MyState>
    {
        public string StepId => "SetFlag";
        public Task<StepResult> ExecuteAsync(StepContext<MyState> context)
        {
            context.State.Flag = true;
            return Task.FromResult<StepResult>(new StepResult.Completed());
        }
    }

    private sealed class FailStep : IStep<MyState>
    {
        public string StepId => "Fail";
        public Task<StepResult> ExecuteAsync(StepContext<MyState> context)
            => Task.FromResult<StepResult>(new StepResult.Failed(new Exception("boom")));
    }

    private sealed class ThrowStep : IStep<MyState>
    {
        public string StepId => "Throw";
        public Task<StepResult> ExecuteAsync(StepContext<MyState> context)
            => throw new InvalidOperationException("unhandled");
    }

    // â”€â”€ Empty workflows â”€â”€

    [Fact]
    public async Task Empty_workflow_completes_immediately()
    {
        var def = new WorkflowBuilder<MyState>("Empty").Init().End().Build();
        await using var engine = new WorkflowEngine();
        var snapshot = await engine.ForDefinition(def).Start(new MyState());

        Assert.Equal(WorkflowStatus.Completed, snapshot.Status);
    }

    // â”€â”€ If edge cases â”€â”€

    [Fact]
    public async Task If_with_empty_then_branch_skips()
    {
        var def = new WorkflowBuilder<MyState>("IfEmptyThen")
            .Init()
            .If(_ => true, then: _ => { })
            .Then<IncrementStep>()
            .End()
            .Build();

        await using var engine = new WorkflowEngine();
        var snapshot = await engine.ForDefinition(def).Start(new MyState());

        Assert.Equal(WorkflowStatus.Completed, snapshot.Status);
        Assert.Equal(1, engine.Instance(snapshot.InstanceId).GetState<MyState>().Count);
    }

    [Fact]
    public async Task If_with_empty_else_branch_skips()
    {
        var def = new WorkflowBuilder<MyState>("IfEmptyElse")
            .Init()
            .If(_ => false, then: b => b.Then<IncrementStep>())
            .Then<LogStep>()
            .End()
            .Build();

        await using var engine = new WorkflowEngine();
        var snapshot = await engine.ForDefinition(def).Start(new MyState());

        Assert.Equal(WorkflowStatus.Completed, snapshot.Status);
        Assert.Equal(0, engine.Instance(snapshot.InstanceId).GetState<MyState>().Count);
        Assert.Single(engine.Instance(snapshot.InstanceId).GetState<MyState>().Log);
    }

    [Fact]
    public async Task If_else_branch_executes_when_condition_false()
    {
        var def = new WorkflowBuilder<MyState>("IfElse")
            .Init()
            .If(_ => false,
                then: b => b.Then<LogStep>(),
                @else: b => b.Then<SetFlagStep>())
            .End()
            .Build();

        await using var engine = new WorkflowEngine();
        var snapshot = await engine.ForDefinition(def).Start(new MyState());

        var state = engine.Instance(snapshot.InstanceId).GetState<MyState>();
        Assert.True(state.Flag);
        Assert.Empty(state.Log);
    }

    // â”€â”€ While edge cases â”€â”€

    [Fact]
    public async Task While_with_initially_false_condition_skips_body()
    {
        var def = new WorkflowBuilder<MyState>("WhileFalse")
            .Init()
            .While(_ => false, body => body.Then<IncrementStep>())
            .End()
            .Build();

        await using var engine = new WorkflowEngine();
        var snapshot = await engine.ForDefinition(def).Start(new MyState());

        Assert.Equal(WorkflowStatus.Completed, snapshot.Status);
        Assert.Equal(0, engine.Instance(snapshot.InstanceId).GetState<MyState>().Count);
    }

    [Fact]
    public async Task While_executes_body_correct_number_of_times()
    {
        var def = new WorkflowBuilder<MyState>("While3")
            .Init()
            .While(s => s.Count < 3, body => body.Then<IncrementStep>())
            .End()
            .Build();

        await using var engine = new WorkflowEngine();
        var snapshot = await engine.ForDefinition(def).Start(new MyState());

        Assert.Equal(3, engine.Instance(snapshot.InstanceId).GetState<MyState>().Count);
    }

    // â”€â”€ Failure inside compound structures â”€â”€

    [Fact]
    public async Task Failure_inside_if_body_fails_workflow()
    {
        var def = new WorkflowBuilder<MyState>("FailInIf")
            .Init()
            .If(_ => true, then: b => b.Then<FailStep>())
            .Then<IncrementStep>()
            .End()
            .Build();

        await using var engine = new WorkflowEngine();
        var snapshot = await engine.ForDefinition(def).Start(new MyState());

        Assert.Equal(WorkflowStatus.Failed, snapshot.Status);
        Assert.Equal("Fail", snapshot.Error!.StepId);
        Assert.Equal(0, engine.Instance(snapshot.InstanceId).GetState<MyState>().Count);
    }

    [Fact]
    public async Task Exception_inside_while_body_fails_workflow()
    {
        var def = new WorkflowBuilder<MyState>("ThrowInWhile")
            .Init()
            .While(_ => true, body => body.Then<ThrowStep>())
            .End()
            .Build();

        await using var engine = new WorkflowEngine();
        var snapshot = await engine.ForDefinition(def).Start(new MyState());

        Assert.Equal(WorkflowStatus.Failed, snapshot.Status);
        Assert.Equal("Throw", snapshot.Error!.StepId);
    }

    [Fact]
    public async Task Failure_inside_while_stops_iteration()
    {
        var def = new WorkflowBuilder<MyState>("FailInWhile")
            .Init()
            .While(_ => true, body => body
                .Then<IncrementStep>()
                .Then<FailStep>())
            .End()
            .Build();

        await using var engine = new WorkflowEngine();
        var snapshot = await engine.ForDefinition(def).Start(new MyState());

        Assert.Equal(WorkflowStatus.Failed, snapshot.Status);
        Assert.Equal(1, engine.Instance(snapshot.InstanceId).GetState<MyState>().Count);
    }

    // â”€â”€ Nested structures â”€â”€

    [Fact]
    public async Task If_inside_while_executes_correctly()
    {
        var def = new WorkflowBuilder<MyState>("IfInWhile")
            .Init()
            .While(s => s.Count < 4, body => body
                .If(s => s.Count % 2 == 0,
                    then: b => b.Then<LogStep>())
                .Then<IncrementStep>())
            .End()
            .Build();

        await using var engine = new WorkflowEngine();
        var snapshot = await engine.ForDefinition(def).Start(new MyState());

        var state = engine.Instance(snapshot.InstanceId).GetState<MyState>();
        Assert.Equal(4, state.Count);
        Assert.Equal(2, state.Log.Count); // executed at count 0 and 2
    }

    [Fact]
    public async Task While_inside_if_executes_correctly()
    {
        var def = new WorkflowBuilder<MyState>("WhileInIf")
            .Init()
            .If(_ => true,
                then: b => b.While(s => s.Count < 3, body => body.Then<IncrementStep>()))
            .End()
            .Build();

        await using var engine = new WorkflowEngine();
        var snapshot = await engine.ForDefinition(def).Start(new MyState());

        Assert.Equal(3, engine.Instance(snapshot.InstanceId).GetState<MyState>().Count);
    }

    [Fact]
    public async Task Deeply_nested_while_while_executes_correctly()
    {
        // Outer while runs 2x, inner while runs 3x first time, exits immediately second time
        // because Count=3 means (3%3!=0 || 3==0) is false
        var def = new WorkflowBuilder<MyState>("DeepWhile")
            .Init()
            .While(s => s.Log.Count < 2, body => body
                .While(s => s.Count % 3 != 0 || s.Count == 0, innerBody => innerBody
                    .Then<IncrementStep>())
                .Then<LogStep>())
            .End()
            .Build();

        await using var engine = new WorkflowEngine();
        var snapshot = await engine.ForDefinition(def).Start(new MyState());

        var state = engine.Instance(snapshot.InstanceId).GetState<MyState>();
        Assert.Equal(3, state.Count);  // inner while only runs during first outer iteration
        Assert.Equal(2, state.Log.Count);
    }

    // â”€â”€ Wait inside If â”€â”€

    [Fact]
    public async Task Wait_inside_if_then_branch_suspends_and_resumes()
    {
        var def = new WorkflowBuilder<MyState>("WaitInIf")
            .Init()
            .If(_ => true,
                then: b => b
                    .Then<IncrementStep>()
                    .Wait("Approval", s => s.Id)
                    .Then<IncrementStep>())
            .End()
            .Build();

        await using var engine = new WorkflowEngine();
        var snapshot = await engine.ForDefinition(def).Start(new MyState());
        Assert.Equal(WorkflowStatus.Waiting, snapshot.Status);
        Assert.Equal(1, engine.Instance(snapshot.InstanceId).GetState<MyState>().Count);

        await engine.Instance(snapshot.InstanceId)
            .RaiseEvent(new EventEnvelope("Approval", "corr-1", null, "evt-1"));

        Assert.Equal(WorkflowStatus.Completed, engine.Instance(snapshot.InstanceId).Get().Status);
        Assert.Equal(2, engine.Instance(snapshot.InstanceId).GetState<MyState>().Count);
    }

    [Fact]
    public async Task Wait_inside_if_does_not_re_execute_prior_steps()
    {
        var def = new WorkflowBuilder<MyState>("WaitInIfNoReExec")
            .Init()
            .If(_ => true,
                then: b => b
                    .Then<LogStep>()
                    .Wait("Approval", s => s.Id)
                    .Then<IncrementStep>())
            .End()
            .Build();

        await using var engine = new WorkflowEngine();
        var snapshot = await engine.ForDefinition(def).Start(new MyState());

        await engine.Instance(snapshot.InstanceId)
            .RaiseEvent(new EventEnvelope("Approval", "corr-1", null, "evt-1"));

        var state = engine.Instance(snapshot.InstanceId).GetState<MyState>();
        Assert.Single(state.Log); // LogStep only ran once, not re-executed on resume
        Assert.Equal(1, state.Count);
    }

    // â”€â”€ Multiple sequential waits â”€â”€

    [Fact]
    public async Task Three_sequential_waits_complete_in_order()
    {
        var def = new WorkflowBuilder<MyState>("ThreeWaits")
            .Init()
            .Wait("A", s => s.Id)
            .Then<IncrementStep>()
            .Wait("B", s => s.Id)
            .Then<IncrementStep>()
            .Wait("C", s => s.Id)
            .Then<IncrementStep>()
            .End()
            .Build();

        await using var engine = new WorkflowEngine();
        var snapshot = await engine.ForDefinition(def).Start(new MyState());
        var scope = engine.Instance(snapshot.InstanceId);

        await scope.RaiseEvent(new EventEnvelope("A", "corr-1", null, "evt-a"));
        Assert.Equal(WorkflowStatus.Waiting, scope.Get().Status);
        Assert.Equal(1, scope.GetState<MyState>().Count);

        await scope.RaiseEvent(new EventEnvelope("B", "corr-1", null, "evt-b"));
        Assert.Equal(WorkflowStatus.Waiting, scope.Get().Status);
        Assert.Equal(2, scope.GetState<MyState>().Count);

        await scope.RaiseEvent(new EventEnvelope("C", "corr-1", null, "evt-c"));
        Assert.Equal(WorkflowStatus.Completed, scope.Get().Status);
        Assert.Equal(3, scope.GetState<MyState>().Count);
    }

    // â”€â”€ Parallel edge cases â”€â”€

    [Fact]
    public async Task Single_branch_parallel_completes()
    {
        var def = new WorkflowBuilder<MyState>("SingleBranch")
            .Init()
            .Parallel(p => p.Branch("A", b => b.Then<IncrementStep>()))
            .End()
            .Build();

        await using var engine = new WorkflowEngine();
        var snapshot = await engine.ForDefinition(def).Start(new MyState());

        Assert.Equal(WorkflowStatus.Completed, snapshot.Status);
        Assert.Equal(1, engine.Instance(snapshot.InstanceId).GetState<MyState>().Count);
    }

    [Fact]
    public async Task Parallel_with_one_failing_branch_fails_workflow()
    {
        var def = new WorkflowBuilder<MyState>("ParallelFail")
            .Init()
            .Parallel(p => p
                .Branch("A", b => b.Then<IncrementStep>())
                .Branch("B", b => b.Then<FailStep>()))
            .End()
            .Build();

        await using var engine = new WorkflowEngine();
        var snapshot = await engine.ForDefinition(def).Start(new MyState());

        Assert.Equal(WorkflowStatus.Failed, snapshot.Status);
    }

    [Fact]
    public async Task Parallel_after_wait_executes()
    {
        var def = new WorkflowBuilder<MyState>("WaitThenParallel")
            .Init()
            .Wait("Start", s => s.Id)
            .Parallel(p => p
                .Branch("A", b => b.Then<IncrementStep>())
                .Branch("B", b => b.Then<IncrementStep>()))
            .End()
            .Build();

        await using var engine = new WorkflowEngine();
        var snapshot = await engine.ForDefinition(def).Start(new MyState());
        Assert.Equal(WorkflowStatus.Waiting, snapshot.Status);

        await engine.Instance(snapshot.InstanceId)
            .RaiseEvent(new EventEnvelope("Start", "corr-1", null, "evt-1"));

        var state = engine.Instance(snapshot.InstanceId).GetState<MyState>();
        Assert.Equal(WorkflowStatus.Completed, engine.Instance(snapshot.InstanceId).Get().Status);
        Assert.Equal(2, state.Count);
    }

    [Fact]
    public async Task Two_parallel_blocks_in_sequence()
    {
        var def = new WorkflowBuilder<MyState>("TwoParallels")
            .Init()
            .Parallel(p => p
                .Branch("A", b => b.Then<IncrementStep>())
                .Branch("B", b => b.Then<IncrementStep>()))
            .Parallel(p => p
                .Branch("C", b => b.Then<IncrementStep>())
                .Branch("D", b => b.Then<IncrementStep>()))
            .End()
            .Build();

        await using var engine = new WorkflowEngine();
        var snapshot = await engine.ForDefinition(def).Start(new MyState());

        Assert.Equal(WorkflowStatus.Completed, snapshot.Status);
        Assert.Equal(4, engine.Instance(snapshot.InstanceId).GetState<MyState>().Count);
    }

    // â”€â”€ StepResult.Yield â”€â”€

    private sealed class YieldStep : IStep<MyState>
    {
        public string StepId => "Yield";
        public Task<StepResult> ExecuteAsync(StepContext<MyState> context)
        {
            context.State.Count++;
            return Task.FromResult<StepResult>(new StepResult.Yield());
        }
    }

    [Fact]
    public async Task Yield_suspends_workflow()
    {
        var def = new WorkflowBuilder<MyState>("YieldTest")
            .Init()
            .Then<YieldStep>()
            .Then<IncrementStep>()
            .End()
            .Build();

        await using var engine = new WorkflowEngine();
        var snapshot = await engine.ForDefinition(def).Start(new MyState());

        // YieldStep ran but workflow didn't continue to IncrementStep
        Assert.Equal(1, engine.Instance(snapshot.InstanceId).GetState<MyState>().Count);
        // Yield returns Suspended which causes the main loop to exit
        // Status depends on whether Yield transitions to Waiting
    }

    // â”€â”€ WaitLong â”€â”€

    [Fact]
    public async Task WaitLong_inside_if_fails_workflow()
    {
        var nodes = new List<IWorkflowNode>
        {
            new IfNode<MyState>(_ => true,
                new List<IWorkflowNode> { new WaitLongNode<MyState>("Evt", s => s.Id) }.AsReadOnly(),
                new List<IWorkflowNode>().AsReadOnly())
        };
        var def = new WorkflowDefinition<MyState>("WaitLongInIf", nodes.AsReadOnly());

        await using var engine = new WorkflowEngine();
        var snapshot = await engine.ForDefinition(def).Start(new MyState());

        Assert.Equal(WorkflowStatus.Failed, snapshot.Status);
        Assert.Contains("WaitLong requires durable mode", snapshot.Error!.Exception.Message);
    }

    // â”€â”€ Dynamic wait from business step â”€â”€

    private sealed class DynamicWaitStep : IStep<MyState>
    {
        public string StepId => "DynamicWait";
        public Task<StepResult> ExecuteAsync(StepContext<MyState> context)
            => Task.FromResult<StepResult>(new StepResult.WaitForEvent("DynEvt", context.State.Id));
    }

    [Fact]
    public async Task Business_step_returning_WaitForEvent_suspends()
    {
        var def = new WorkflowBuilder<MyState>("DynWait")
            .Init()
            .Then<DynamicWaitStep>()
            .Then<IncrementStep>()
            .End()
            .Build();

        await using var engine = new WorkflowEngine();
        var snapshot = await engine.ForDefinition(def).Start(new MyState());

        Assert.Equal(WorkflowStatus.Waiting, snapshot.Status);
        var waits = engine.Instance(snapshot.InstanceId).GetActiveWaits();
        Assert.Single(waits);
        Assert.Equal("DynEvt", waits[0].EventName);

        await engine.Instance(snapshot.InstanceId)
            .RaiseEvent(new EventEnvelope("DynEvt", "corr-1", null, "evt-1"));

        Assert.Equal(WorkflowStatus.Completed, engine.Instance(snapshot.InstanceId).Get().Status);
        Assert.Equal(1, engine.Instance(snapshot.InstanceId).GetState<MyState>().Count);
    }

    // â”€â”€ Steps after compound structures â”€â”€

    [Fact]
    public async Task Step_after_if_executes()
    {
        var def = new WorkflowBuilder<MyState>("StepAfterIf")
            .Init()
            .If(_ => true, then: b => b.Then<SetFlagStep>())
            .Then<IncrementStep>()
            .End()
            .Build();

        await using var engine = new WorkflowEngine();
        var snapshot = await engine.ForDefinition(def).Start(new MyState());

        var state = engine.Instance(snapshot.InstanceId).GetState<MyState>();
        Assert.True(state.Flag);
        Assert.Equal(1, state.Count);
    }

    [Fact]
    public async Task Step_after_while_executes()
    {
        var def = new WorkflowBuilder<MyState>("StepAfterWhile")
            .Init()
            .While(s => s.Count < 2, body => body.Then<IncrementStep>())
            .Then<SetFlagStep>()
            .End()
            .Build();

        await using var engine = new WorkflowEngine();
        var snapshot = await engine.ForDefinition(def).Start(new MyState());

        var state = engine.Instance(snapshot.InstanceId).GetState<MyState>();
        Assert.Equal(2, state.Count);
        Assert.True(state.Flag);
    }

    // â”€â”€ ResumedEvent payload flow â”€â”€

    private sealed class CapturePayloadStep : IStep<MyState>
    {
        public string StepId => "Capture";
        public Task<StepResult> ExecuteAsync(StepContext<MyState> context)
        {
            if (context.ResumedEvent?.Payload is string payload)
                context.State.Log.Add(payload);
            return Task.FromResult<StepResult>(new StepResult.Completed());
        }
    }

    [Fact]
    public async Task Resumed_event_payload_available_to_next_step()
    {
        var def = new WorkflowBuilder<MyState>("PayloadFlow")
            .Init()
            .Wait("Evt", s => s.Id)
            .Then<CapturePayloadStep>()
            .End()
            .Build();

        await using var engine = new WorkflowEngine();
        var snapshot = await engine.ForDefinition(def).Start(new MyState());

        await engine.Instance(snapshot.InstanceId)
            .RaiseEvent(new EventEnvelope("Evt", "corr-1", "hello", "evt-1"));

        var state = engine.Instance(snapshot.InstanceId).GetState<MyState>();
        Assert.Equal(["hello"], state.Log);
    }

    [Fact]
    public async Task Resumed_event_payload_only_available_to_first_step_after_wait()
    {
        var def = new WorkflowBuilder<MyState>("PayloadConsumed")
            .Init()
            .Wait("Evt", s => s.Id)
            .Then<CapturePayloadStep>()
            .Then<CapturePayloadStep>()
            .End()
            .Build();

        await using var engine = new WorkflowEngine();
        var snapshot = await engine.ForDefinition(def).Start(new MyState());

        await engine.Instance(snapshot.InstanceId)
            .RaiseEvent(new EventEnvelope("Evt", "corr-1", "hello", "evt-1"));

        var state = engine.Instance(snapshot.InstanceId).GetState<MyState>();
        // Only the first CapturePayloadStep should see the payload
        Assert.Single(state.Log);
        Assert.Equal("hello", state.Log[0]);
    }
}

