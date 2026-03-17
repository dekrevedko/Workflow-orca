
namespace OrcaCore.Tests;

public class WorkflowRuntimeFrameTests
{
    private sealed class CounterState
    {
        public string Id { get; set; } = "corr-1";
        public int Count { get; set; }
        public bool Done { get; set; }
    }

    private sealed class IncrementStep : IStep<CounterState>
    {
        public string StepId => "Increment";

        public Task<StepResult> ExecuteAsync(StepContext<CounterState> context)
        {
            context.State.Count++;
            return Task.FromResult<StepResult>(new StepResult.Completed());
        }
    }

    private sealed class MarkDoneStep : IStep<CounterState>
    {
        public string StepId => "MarkDone";

        public Task<StepResult> ExecuteAsync(StepContext<CounterState> context)
        {
            context.State.Done = true;
            return Task.FromResult<StepResult>(new StepResult.Completed());
        }
    }

    private sealed class FailStep : IStep<CounterState>
    {
        public string StepId => "Fail";

        public Task<StepResult> ExecuteAsync(StepContext<CounterState> context)
        {
            return Task.FromResult<StepResult>(
                new StepResult.Failed(new InvalidOperationException("frame failure")));
        }
    }

    [Fact]
    public async Task Main_path_frames_are_empty_on_completion()
    {
        var definition = new WorkflowBuilder<CounterState>("FrameCompletion")
            .Init()
            .Then<IncrementStep>()
            .End()
            .Build();

        await using var engine = new WorkflowEngine();
        var snapshot = await engine.ForDefinition(definition).Start(new CounterState());

        var instance = engine.Store.Get<CounterState>(snapshot.InstanceId);
        Assert.Equal(WorkflowStatus.Completed, instance.RuntimeState.Status);
        Assert.True(instance.RuntimeState.MainPath.IsComplete);
        Assert.Empty(instance.RuntimeState.MainPath.Frames);
    }

    [Fact]
    public async Task Top_level_wait_preserves_root_frame_with_advanced_index()
    {
        var definition = new WorkflowBuilder<CounterState>("TopLevelWaitFrame")
            .Init()
            .Wait("Approval", s => s.Id)
            .Then<IncrementStep>()
            .End()
            .Build();

        await using var engine = new WorkflowEngine();
        var snapshot = await engine.ForDefinition(definition).Start(new CounterState());

        var instance = engine.Store.Get<CounterState>(snapshot.InstanceId);
        Assert.Equal(WorkflowStatus.Waiting, instance.RuntimeState.Status);
        Assert.Single(instance.RuntimeState.MainPath.Frames);

        var root = instance.RuntimeState.MainPath.CurrentFrame;
        Assert.Equal(FrameKind.Root, root.Kind);
        Assert.Equal(1, root.Index);
    }

    [Fact]
    public async Task Wait_inside_while_suspends_with_root_and_body_frames()
    {
        var definition = new WorkflowBuilder<CounterState>("WhileWaitFrame")
            .Init()
            .While(s => !s.Done, body => body
                .Wait("Approval", s => s.Id)
                .Then<MarkDoneStep>())
            .End()
            .Build();

        await using var engine = new WorkflowEngine();
        var snapshot = await engine.ForDefinition(definition).Start(new CounterState());

        var instance = engine.Store.Get<CounterState>(snapshot.InstanceId);
        Assert.Equal(WorkflowStatus.Waiting, instance.RuntimeState.Status);
        Assert.Equal(2, instance.RuntimeState.MainPath.Frames.Count);

        var root = instance.RuntimeState.MainPath.Frames[0];
        var body = instance.RuntimeState.MainPath.Frames[1];

        Assert.Equal(FrameKind.Root, root.Kind);
        Assert.Equal(0, root.Index);
        Assert.Equal(FrameKind.WhileBody, body.Kind);
        Assert.Equal(1, body.Index);
    }

    [Fact]
    public async Task Parallel_waits_create_branch_frames_and_leave_main_path_after_parallel_node()
    {
        var definition = new WorkflowBuilder<CounterState>("ParallelFrame")
            .Init()
            .Parallel(p => p
                .Branch("A", b => b.Wait("EventA", s => s.Id).Then<IncrementStep>())
                .Branch("B", b => b.Wait("EventB", s => s.Id).Then<IncrementStep>()))
            .Then<MarkDoneStep>()
            .End()
            .Build();

        await using var engine = new WorkflowEngine();
        var snapshot = await engine.ForDefinition(definition).Start(new CounterState());

        var instance = engine.Store.Get<CounterState>(snapshot.InstanceId);
        Assert.Equal(WorkflowStatus.Waiting, instance.RuntimeState.Status);
        Assert.NotNull(instance.RuntimeState.ActiveParallel);
        Assert.Equal(2, instance.RuntimeState.ActiveParallel!.BranchPaths.Count);
        Assert.Single(instance.RuntimeState.MainPath.Frames);
        Assert.Equal(1, instance.RuntimeState.MainPath.CurrentFrame.Index);

        var branchA = instance.RuntimeState.ActiveParallel.BranchPaths["A"];
        var branchB = instance.RuntimeState.ActiveParallel.BranchPaths["B"];

        Assert.Single(branchA.Frames);
        Assert.Single(branchB.Frames);
        Assert.Equal(FrameKind.ParallelBranch, branchA.CurrentFrame.Kind);
        Assert.Equal(FrameKind.ParallelBranch, branchB.CurrentFrame.Kind);
        Assert.Equal(1, branchA.CurrentFrame.Index);
        Assert.Equal(1, branchB.CurrentFrame.Index);
    }

    [Fact]
    public async Task Parallel_branch_resume_removes_only_completed_branch_path()
    {
        var definition = new WorkflowBuilder<CounterState>("ParallelBranchResume")
            .Init()
            .Parallel(p => p
                .Branch("A", b => b.Wait("EventA", s => s.Id).Then<IncrementStep>())
                .Branch("B", b => b.Wait("EventB", s => s.Id).Then<IncrementStep>()))
            .Then<MarkDoneStep>()
            .End()
            .Build();

        await using var engine = new WorkflowEngine();
        var snapshot = await engine.ForDefinition(definition).Start(new CounterState());

        await engine.Instance(snapshot.InstanceId)
            .RaiseEvent(new EventEnvelope("EventA", "corr-1", null, "evt-a"));

        var instance = engine.Store.Get<CounterState>(snapshot.InstanceId);
        Assert.Equal(WorkflowStatus.Waiting, instance.RuntimeState.Status);
        Assert.NotNull(instance.RuntimeState.ActiveParallel);
        Assert.Single(instance.RuntimeState.ActiveParallel!.BranchPaths);
        Assert.False(instance.RuntimeState.ActiveParallel.BranchPaths.ContainsKey("A"));
        Assert.True(instance.RuntimeState.ActiveParallel.BranchPaths.ContainsKey("B"));
        Assert.Equal(1, instance.RuntimeState.MainPath.CurrentFrame.Index);
        Assert.Equal(1, instance.BusinessState.Count);
    }

    [Fact]
    public async Task Failure_inside_while_body_moves_instance_to_failed_and_preserves_frame_context()
    {
        var definition = new WorkflowBuilder<CounterState>("WhileFailure")
            .Init()
            .While(s => !s.Done, body => body
                .Then<IncrementStep>()
                .Then<FailStep>())
            .End()
            .Build();

        await using var engine = new WorkflowEngine();
        var snapshot = await engine.ForDefinition(definition).Start(new CounterState());

        var instance = engine.Store.Get<CounterState>(snapshot.InstanceId);
        Assert.Equal(WorkflowStatus.Failed, instance.RuntimeState.Status);
        Assert.NotNull(instance.RuntimeState.Error);
        Assert.Equal("Fail", instance.RuntimeState.Error!.StepId);
        Assert.Equal(1, instance.BusinessState.Count);
        Assert.Equal(2, instance.RuntimeState.MainPath.Frames.Count);
        Assert.Equal(FrameKind.Root, instance.RuntimeState.MainPath.Frames[0].Kind);
        Assert.Equal(FrameKind.WhileBody, instance.RuntimeState.MainPath.Frames[1].Kind);
    }

    [Fact]
    public async Task Failure_inside_parallel_branch_fails_instance_and_keeps_parallel_context_observable()
    {
        var definition = new WorkflowBuilder<CounterState>("ParallelFailure")
            .Init()
            .Parallel(p => p
                .Branch("A", b => b.Then<IncrementStep>().Then<FailStep>())
                .Branch("B", b => b.Wait("EventB", s => s.Id).Then<IncrementStep>()))
            .Then<MarkDoneStep>()
            .End()
            .Build();

        await using var engine = new WorkflowEngine();
        var snapshot = await engine.ForDefinition(definition).Start(new CounterState());

        var instance = engine.Store.Get<CounterState>(snapshot.InstanceId);
        Assert.Equal(WorkflowStatus.Failed, instance.RuntimeState.Status);
        Assert.NotNull(instance.RuntimeState.Error);
        Assert.Equal("Fail", instance.RuntimeState.Error!.StepId);
        Assert.Equal(1, instance.BusinessState.Count);
        Assert.Null(instance.RuntimeState.ActiveParallel);
        Assert.Single(instance.RuntimeState.MainPath.Frames);
        Assert.Equal(FrameKind.Root, instance.RuntimeState.MainPath.CurrentFrame.Kind);
        Assert.Equal(1, instance.RuntimeState.MainPath.CurrentFrame.Index);
    }

    [Fact]
    public async Task Terminal_failure_rejects_further_resume_and_leaves_frame_state_stable()
    {
        var definition = new WorkflowBuilder<CounterState>("FailedTerminalFrame")
            .Init()
            .While(s => !s.Done, body => body
                .Wait("Approval", s => s.Id)
                .Then<FailStep>())
            .End()
            .Build();

        await using var engine = new WorkflowEngine();
        var snapshot = await engine.ForDefinition(definition).Start(new CounterState());

        await engine.Instance(snapshot.InstanceId)
            .RaiseEvent(new EventEnvelope("Approval", "corr-1", null, "evt-1"));

        var instance = engine.Store.Get<CounterState>(snapshot.InstanceId);
        Assert.Equal(WorkflowStatus.Failed, instance.RuntimeState.Status);
        Assert.Equal(2, instance.RuntimeState.MainPath.Frames.Count);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            engine.Instance(snapshot.InstanceId)
                .RaiseEvent(new EventEnvelope("Approval", "corr-1", null, "evt-2")));

        Assert.Contains("terminal state", ex.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(WorkflowStatus.Failed, instance.RuntimeState.Status);
        Assert.Equal(2, instance.RuntimeState.MainPath.Frames.Count);
    }
}

