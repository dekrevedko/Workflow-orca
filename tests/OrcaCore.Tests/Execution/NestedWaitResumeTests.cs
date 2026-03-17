
namespace OrcaCore.Tests;

public class NestedWaitResumeTests
{
    private sealed class MyState
    {
        public string Id { get; set; } = "corr-1";
        public int PreCount { get; set; }
        public int PostCount { get; set; }
        public int Iterations { get; set; }
        public List<string> Log { get; set; } = [];
    }

    private sealed class PreStep : IStep<MyState>
    {
        public string StepId => "Pre";
        public Task<StepResult> ExecuteAsync(StepContext<MyState> context)
        {
            context.State.PreCount++;
            return Task.FromResult<StepResult>(new StepResult.Completed());
        }
    }

    private sealed class PostStep : IStep<MyState>
    {
        public string StepId => "Post";
        public Task<StepResult> ExecuteAsync(StepContext<MyState> context)
        {
            context.State.PostCount++;
            context.State.Iterations++;
            return Task.FromResult<StepResult>(new StepResult.Completed());
        }
    }

    private sealed class LogStep : IStep<MyState>
    {
        public string StepId => "Log";
        public Task<StepResult> ExecuteAsync(StepContext<MyState> context)
        {
            context.State.Log.Add($"iter-{context.State.Iterations}");
            return Task.FromResult<StepResult>(new StepResult.Completed());
        }
    }

    [Fact]
    public async Task Wait_inside_while_no_double_execution_across_3_iterations()
    {
        var def = new WorkflowBuilder<MyState>("WhileWait3")
            .Init()
            .While(s => s.Iterations < 3, body => body
                .Then<PreStep>()
                .Wait("Approval", s => s.Id)
                .Then<PostStep>())
            .End()
            .Build();

        await using var engine = new WorkflowEngine();
        var snapshot = await engine.ForDefinition(def).Start(new MyState());
        var scope = engine.Instance(snapshot.InstanceId);

        for (var i = 0; i < 3; i++)
        {
            Assert.Equal(WorkflowStatus.Waiting, scope.Get().Status);
            await scope.RaiseEvent(new EventEnvelope("Approval", "corr-1", null, $"evt-{i}"));
        }

        var state = scope.GetState<MyState>();
        Assert.Equal(WorkflowStatus.Completed, scope.Get().Status);
        Assert.Equal(3, state.PreCount);  // once per iteration, NOT doubled
        Assert.Equal(3, state.PostCount);
        Assert.Equal(3, state.Iterations);
    }

    [Fact]
    public async Task Wait_inside_if_inside_while_no_double_execution()
    {
        var def = new WorkflowBuilder<MyState>("IfWaitInWhile")
            .Init()
            .While(s => s.Iterations < 2, body => body
                .Then<PreStep>()
                .If(_ => true, then: ifBody => ifBody
                    .Wait("Approval", s => s.Id))
                .Then<PostStep>())
            .End()
            .Build();

        await using var engine = new WorkflowEngine();
        var snapshot = await engine.ForDefinition(def).Start(new MyState());
        var scope = engine.Instance(snapshot.InstanceId);

        for (var i = 0; i < 2; i++)
        {
            Assert.Equal(WorkflowStatus.Waiting, scope.Get().Status);
            await scope.RaiseEvent(new EventEnvelope("Approval", "corr-1", null, $"evt-{i}"));
        }

        var state = scope.GetState<MyState>();
        Assert.Equal(WorkflowStatus.Completed, scope.Get().Status);
        Assert.Equal(2, state.PreCount);
        Assert.Equal(2, state.PostCount);
    }

    [Fact]
    public async Task Multiple_steps_before_wait_in_while_only_run_once()
    {
        var def = new WorkflowBuilder<MyState>("MultiPreSteps")
            .Init()
            .While(s => s.Iterations < 1, body => body
                .Then<PreStep>()
                .Then<LogStep>()
                .Wait("Approval", s => s.Id)
                .Then<PostStep>())
            .End()
            .Build();

        await using var engine = new WorkflowEngine();
        var snapshot = await engine.ForDefinition(def).Start(new MyState());
        var scope = engine.Instance(snapshot.InstanceId);

        Assert.Equal(1, scope.GetState<MyState>().PreCount);
        Assert.Single(scope.GetState<MyState>().Log);

        await scope.RaiseEvent(new EventEnvelope("Approval", "corr-1", null, "evt-1"));

        var state = scope.GetState<MyState>();
        Assert.Equal(WorkflowStatus.Completed, scope.Get().Status);
        Assert.Equal(1, state.PreCount);  // not re-executed
        Assert.Single(state.Log);         // not re-executed
        Assert.Equal(1, state.PostCount);
    }

    [Fact]
    public async Task Steps_after_while_with_wait_execute()
    {
        var def = new WorkflowBuilder<MyState>("AfterWhileWait")
            .Init()
            .While(s => s.Iterations < 1, body => body
                .Wait("Approval", s => s.Id)
                .Then<PostStep>())
            .Then<LogStep>()
            .End()
            .Build();

        await using var engine = new WorkflowEngine();
        var snapshot = await engine.ForDefinition(def).Start(new MyState());
        var scope = engine.Instance(snapshot.InstanceId);

        await scope.RaiseEvent(new EventEnvelope("Approval", "corr-1", null, "evt-1"));

        var state = scope.GetState<MyState>();
        Assert.Equal(WorkflowStatus.Completed, scope.Get().Status);
        Assert.Equal(1, state.PostCount);
        Assert.Single(state.Log); // step after while executed
    }

    [Fact]
    public async Task Wait_inside_while_with_buffered_event()
    {
        var def = new WorkflowBuilder<MyState>("BufferedWhileWait")
            .Init()
            .While(s => s.Iterations < 1, body => body
                .Then<PreStep>()
                .Wait("First", s => s.Id)
                .Wait("Second", s => s.Id)
                .Then<PostStep>())
            .End()
            .Build();

        await using var engine = new WorkflowEngine();
        var snapshot = await engine.ForDefinition(def).Start(new MyState());
        var scope = engine.Instance(snapshot.InstanceId);

        // Send Second before First â€” should be buffered
        await scope.RaiseEvent(new EventEnvelope("Second", "corr-1", null, "evt-2"));
        Assert.Equal(WorkflowStatus.Waiting, scope.Get().Status);

        // Send First â€” resumes, Second should be consumed from buffer
        await scope.RaiseEvent(new EventEnvelope("First", "corr-1", null, "evt-1"));

        var state = scope.GetState<MyState>();
        Assert.Equal(WorkflowStatus.Completed, scope.Get().Status);
        Assert.Equal(1, state.PreCount);
        Assert.Equal(1, state.PostCount);
    }
}

