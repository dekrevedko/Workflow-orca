
namespace OrcaCore.Tests;

public class MC_AT_006_ParallelWhenAllTests
{
    private sealed class MyState
    {
        public bool A { get; set; }
        public bool B { get; set; }
        public int JoinCount { get; set; }
    }

    private sealed class SetAStep : IStep<MyState>
    {
        public string StepId => "SetA";
        public Task<StepResult> ExecuteAsync(StepContext<MyState> context)
        {
            context.State.A = true;
            return Task.FromResult<StepResult>(new StepResult.Completed());
        }
    }

    private sealed class SetBStep : IStep<MyState>
    {
        public string StepId => "SetB";
        public Task<StepResult> ExecuteAsync(StepContext<MyState> context)
        {
            context.State.B = true;
            return Task.FromResult<StepResult>(new StepResult.Completed());
        }
    }

    private sealed class JoinStep : IStep<MyState>
    {
        public string StepId => "Join";
        public Task<StepResult> ExecuteAsync(StepContext<MyState> context)
        {
            context.State.JoinCount++;
            return Task.FromResult<StepResult>(new StepResult.Completed());
        }
    }

    [Fact]
    public async Task Parallel_branches_join_exactly_once_with_WhenAll()
    {
        var definition = new WorkflowBuilder<MyState>("ParallelWorkflow")
            .Init()
            .Parallel(p => p
                .Branch("A", b => b.Then<SetAStep>())
                .Branch("B", b => b.Then<SetBStep>()))
            .Then<JoinStep>()
            .End()
            .Build();

        await using var engine = new WorkflowEngine();
        var typed = engine.ForDefinition(definition);
        var snapshot = await typed.Start(new MyState());

        Assert.Equal(WorkflowStatus.Completed, snapshot.Status);
        var state = engine.Instance(snapshot.InstanceId).GetState<MyState>();
        Assert.True(state.A);
        Assert.True(state.B);
        Assert.Equal(1, state.JoinCount);
    }
}

