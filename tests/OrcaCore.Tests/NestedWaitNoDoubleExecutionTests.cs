using OrcaCore.Abstractions;
using OrcaCore.Runtime;

namespace OrcaCore.Tests;

public class NestedWaitNoDoubleExecutionTests
{
    private sealed class CounterState
    {
        public string Id { get; set; } = "corr-1";
        public int PreWaitCount { get; set; }
        public int PostWaitCount { get; set; }
        public int Iterations { get; set; }
        public bool Done => Iterations >= 2;
    }

    private sealed class PreWaitStep : IStep<CounterState>
    {
        public string StepId => "PreWait";
        public Task<StepResult> ExecuteAsync(StepContext<CounterState> context)
        {
            context.State.PreWaitCount++;
            return Task.FromResult<StepResult>(new StepResult.Completed());
        }
    }

    private sealed class PostWaitStep : IStep<CounterState>
    {
        public string StepId => "PostWait";
        public Task<StepResult> ExecuteAsync(StepContext<CounterState> context)
        {
            context.State.PostWaitCount++;
            context.State.Iterations++;
            return Task.FromResult<StepResult>(new StepResult.Completed());
        }
    }

    [Fact]
    public async Task Steps_before_wait_in_while_are_not_re_executed_on_resume()
    {
        var definition = new WorkflowBuilder<CounterState>("NoDoubleExec")
            .Init()
            .While(s => !s.Done, body => body
                .Step<PreWaitStep>()
                .Wait("Approval", s => s.Id)
                .Step<PostWaitStep>())
            .End()
            .Build();

        await using var engine = new WorkflowEngine();
        var typed = engine.ForDefinition(definition);
        var snapshot = await typed.Start(new CounterState());

        var scope = engine.Instance(snapshot.InstanceId);
        Assert.Equal(WorkflowStatus.Waiting, snapshot.Status);

        // Iteration 1: PreWaitStep ran once
        var state = scope.GetState<CounterState>();
        Assert.Equal(1, state.PreWaitCount);

        // Resume iteration 1
        await scope.RaiseEvent(new EventEnvelope("Approval", "corr-1", null, "evt-1"));
        Assert.Equal(WorkflowStatus.Waiting, scope.Get().Status);

        // PreWaitStep should have run exactly twice (once per iteration, NOT re-executed on resume)
        state = scope.GetState<CounterState>();
        Assert.Equal(2, state.PreWaitCount);
        Assert.Equal(1, state.PostWaitCount);

        // Resume iteration 2
        await scope.RaiseEvent(new EventEnvelope("Approval", "corr-1", null, "evt-2"));
        Assert.Equal(WorkflowStatus.Completed, scope.Get().Status);

        state = scope.GetState<CounterState>();
        Assert.Equal(2, state.PreWaitCount); // exactly 2 iterations
        Assert.Equal(2, state.PostWaitCount);
    }
}
