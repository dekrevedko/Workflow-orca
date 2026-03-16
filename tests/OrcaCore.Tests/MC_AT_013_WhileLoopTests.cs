using OrcaCore.Abstractions;
using OrcaCore.Runtime;

namespace OrcaCore.Tests;

public class MC_AT_013_WhileLoopTests
{
    private sealed class CounterState
    {
        public int Counter { get; set; }
    }

    private sealed class IncrementStep : IStep<CounterState>
    {
        public string StepId => "Increment";
        public Task<StepResult> ExecuteAsync(StepContext<CounterState> context)
        {
            context.State.Counter++;
            return Task.FromResult<StepResult>(new StepResult.Completed());
        }
    }

    [Fact]
    public async Task While_loop_executes_body_until_condition_is_false()
    {
        var definition = new WorkflowBuilder<CounterState>("LoopWorkflow")
            .Init()
            .While(s => s.Counter < 3, body => body.Step<IncrementStep>())
            .End()
            .Build();

        await using var engine = new WorkflowEngine();
        var typed = engine.ForDefinition(definition);
        var snapshot = await typed.Start(new CounterState());

        Assert.Equal(WorkflowStatus.Completed, snapshot.Status);
        Assert.Equal(3, engine.Instance(snapshot.InstanceId).GetState<CounterState>().Counter);
    }
}
