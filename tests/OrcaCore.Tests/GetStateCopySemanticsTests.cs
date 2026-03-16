using OrcaCore.Abstractions;
using OrcaCore.Runtime;

namespace OrcaCore.Tests;

public class GetStateCopySemanticsTests
{
    private sealed class MyState
    {
        public string Value { get; set; } = "original";
    }

    private sealed class NoOpStep : IStep<MyState>
    {
        public string StepId => "NoOp";
        public Task<StepResult> ExecuteAsync(StepContext<MyState> context)
            => Task.FromResult<StepResult>(new StepResult.Completed());
    }

    [Fact]
    public async Task GetState_returns_copy_not_live_reference()
    {
        var definition = new WorkflowBuilder<MyState>("CopyTest")
            .Init()
            .Step<NoOpStep>()
            .End()
            .Build();

        await using var engine = new WorkflowEngine();
        var typed = engine.ForDefinition(definition);
        var snapshot = await typed.Start(new MyState());

        var state1 = engine.Instance(snapshot.InstanceId).GetState<MyState>();
        state1.Value = "mutated";

        var state2 = engine.Instance(snapshot.InstanceId).GetState<MyState>();
        Assert.Equal("original", state2.Value);
    }
}
