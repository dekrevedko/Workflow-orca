using OrcaCore.Abstractions;
using OrcaCore.Runtime;

namespace OrcaCore.Tests;

public class MC_AT_002_ConditionalPathTests
{
    private sealed class MyState
    {
        public bool IsValid { get; set; }
        public bool StepAExecuted { get; set; }
        public bool StepBExecuted { get; set; }
    }

    private sealed class StepA : IStep<MyState>
    {
        public string StepId => "StepA";
        public Task<StepResult> ExecuteAsync(StepContext<MyState> context)
        {
            context.State.StepAExecuted = true;
            return Task.FromResult<StepResult>(new StepResult.Completed());
        }
    }

    private sealed class StepB : IStep<MyState>
    {
        public string StepId => "StepB";
        public Task<StepResult> ExecuteAsync(StepContext<MyState> context)
        {
            context.State.StepBExecuted = true;
            return Task.FromResult<StepResult>(new StepResult.Completed());
        }
    }

    [Fact]
    public async Task If_true_executes_then_branch_only()
    {
        var definition = new WorkflowBuilder<MyState>("IfWorkflow")
            .Init()
            .If(s => s.IsValid,
                then: b => b.Step<StepA>(),
                @else: b => b.Step<StepB>())
            .End()
            .Build();

        await using var engine = new WorkflowEngine();
        var typed = engine.ForDefinition(definition);
        var snapshot = await typed.Start(new MyState { IsValid = true });

        Assert.Equal(WorkflowStatus.Completed, snapshot.Status);
        var state = engine.Instance(snapshot.InstanceId).GetState<MyState>();
        Assert.True(state.StepAExecuted);
        Assert.False(state.StepBExecuted);
    }

    [Fact]
    public async Task If_false_executes_else_branch_only()
    {
        var definition = new WorkflowBuilder<MyState>("IfWorkflow")
            .Init()
            .If(s => s.IsValid,
                then: b => b.Step<StepA>(),
                @else: b => b.Step<StepB>())
            .End()
            .Build();

        await using var engine = new WorkflowEngine();
        var typed = engine.ForDefinition(definition);
        var snapshot = await typed.Start(new MyState { IsValid = false });

        Assert.Equal(WorkflowStatus.Completed, snapshot.Status);
        var state = engine.Instance(snapshot.InstanceId).GetState<MyState>();
        Assert.False(state.StepAExecuted);
        Assert.True(state.StepBExecuted);
    }
}
