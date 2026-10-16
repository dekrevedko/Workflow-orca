
namespace OrcaCore.Tests;

public class MC_AT_007_ParallelWaitIsolationTests
{
    private sealed class MyState
    {
        public string Id { get; set; } = "corr-1";
        public bool BranchAResumed { get; set; }
        public bool BranchBResumed { get; set; }
    }

    private sealed class MarkAResumedStep : IStep<MyState>
    {
        public string StepId => "MarkAResumed";
        public Task<StepResult> ExecuteAsync(StepContext<MyState> context)
        {
            context.State.BranchAResumed = true;
            return Task.FromResult<StepResult>(new StepResult.Completed());
        }
    }

    private sealed class MarkBResumedStep : IStep<MyState>
    {
        public string StepId => "MarkBResumed";
        public Task<StepResult> ExecuteAsync(StepContext<MyState> context)
        {
            context.State.BranchBResumed = true;
            return Task.FromResult<StepResult>(new StepResult.Completed());
        }
    }

    [Fact]
    public async Task Different_waits_in_parallel_branches_remain_isolated()
    {
        var definition = new WorkflowBuilder<MyState>("ParallelWaitWorkflow")
            .Init()
            .Parallel(p => p
                .Branch("A", b => b
                    .Wait("EventA", s => s.Id)
                    .Then<MarkAResumedStep>())
                .Branch("B", b => b
                    .Wait("EventB", s => s.Id)
                    .Then<MarkBResumedStep>()))
            .End()
            .Build();

        await using var engine = new WorkflowEngine();
        var typed = engine.ForDefinition(definition);
        var snapshot = await typed.Start(new MyState());

        Assert.Equal(WorkflowStatus.Waiting, snapshot.Status);

        var scope = engine.Instance(snapshot.InstanceId);

        // Raise EventA â€” only branch A should resume
        await scope.RaiseEvent(new EventEnvelope("EventA", "corr-1", null, "evt-a"));

        var state = scope.GetState<MyState>();
        Assert.True(state.BranchAResumed);
        Assert.False(state.BranchBResumed);
        Assert.Equal(WorkflowStatus.Waiting, scope.Get().Status); // B still waiting

        // Raise EventB â€” branch B resumes, WhenAll fires, workflow completes
        await scope.RaiseEvent(new EventEnvelope("EventB", "corr-1", null, "evt-b"));

        state = scope.GetState<MyState>();
        Assert.True(state.BranchBResumed);
        Assert.Equal(WorkflowStatus.Completed, scope.Get().Status);
    }
}

