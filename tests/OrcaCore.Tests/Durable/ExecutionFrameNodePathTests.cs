
namespace OrcaCore.Tests.Durable;

public sealed class ExecutionFrameNodePathTests
{
    private sealed class WaitStep : IStep<MyState>
    {
        public string StepId => "WaitStep";

        public Task<StepResult> ExecuteAsync(StepContext<MyState> context) =>
            Task.FromResult<StepResult>(new StepResult.Completed());
    }

    private sealed record MyState(string Id = "state-1");

    [Fact]
    public async Task Root_and_nested_frames_capture_node_paths()
    {
        var definition = new WorkflowBuilder<MyState>("FramePathWorkflow")
            .Init()
            .While(
                _ => true,
                body => body
                    .Wait("Approval", state => state.Id)
                    .Then<WaitStep>())
            .End()
            .Build();

        var instance = new WorkflowInstance<MyState>("instance-1", definition.DefinitionId, new MyState());
        var correlationIndex = new CorrelationIndex();

        await WorkflowRuntime.ExecuteAsync(instance, definition, correlationIndex);

        Assert.Equal(WorkflowStatus.Waiting, instance.RuntimeState.Status);
        Assert.Equal("", instance.RuntimeState.MainPath.Frames[0].NodePath);
        Assert.Equal("0/body", instance.RuntimeState.MainPath.Frames[1].NodePath);
    }
}
