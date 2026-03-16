using OrcaCore.Abstractions;
using OrcaCore.Runtime;

namespace OrcaCore.Tests;

public class MC_AT_001_SimpleWorkflowCompletionTests
{
    private sealed class MyState
    {
        public string Result { get; set; } = "";
    }

    private sealed class SetResultStep : IStep<MyState>
    {
        public string StepId => "SetResult";

        public Task<StepResult> ExecuteAsync(StepContext<MyState> context)
        {
            context.State.Result = "done";
            return Task.FromResult<StepResult>(new StepResult.Completed());
        }
    }

    [Fact]
    public async Task Start_runs_simple_workflow_to_completion()
    {
        // Arrange
        var definition = new WorkflowBuilder<MyState>("SimpleWorkflow")
            .Init()
            .Step<SetResultStep>()
            .End()
            .Build();

        await using var engine = new WorkflowEngine();
        var typed = engine.ForDefinition(definition);

        // Act
        var snapshot = await typed.Start(new MyState());

        // Assert
        Assert.Equal(WorkflowStatus.Completed, snapshot.Status);

        var state = engine.Instance(snapshot.InstanceId).GetState<MyState>();
        Assert.Equal("done", state.Result);
    }
}
