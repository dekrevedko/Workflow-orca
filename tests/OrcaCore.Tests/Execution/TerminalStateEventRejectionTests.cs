
namespace OrcaCore.Tests;

public class TerminalStateEventRejectionTests
{
    private sealed class MyState
    {
        public string Id { get; set; } = "done";
    }

    [Fact]
    public async Task Completed_instance_rejects_further_events()
    {
        var definition = new WorkflowBuilder<MyState>("CompletedWorkflow")
            .Init()
            .End()
            .Build();

        await using var engine = new WorkflowEngine();
        var snapshot = await engine.ForDefinition(definition).Start(new MyState());

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            engine.Instance(snapshot.InstanceId).RaiseEvent(
                new EventEnvelope("AnyEvent", "done", null, "evt-1")));

        Assert.Contains("terminal state", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Failed_instance_rejects_further_events()
    {
        var definition = new WorkflowBuilder<MyState>("FailedWorkflow")
            .Init()
            .Then<FailStep>()
            .End()
            .Build();

        await using var engine = new WorkflowEngine();
        var snapshot = await engine.ForDefinition(definition).Start(new MyState());
        Assert.Equal(WorkflowStatus.Failed, snapshot.Status);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            engine.Instance(snapshot.InstanceId).RaiseEvent(
                new EventEnvelope("AnyEvent", "done", null, "evt-2")));

        Assert.Contains("terminal state", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    private sealed class FailStep : IStep<MyState>
    {
        public string StepId => "Fail";

        public Task<StepResult> ExecuteAsync(StepContext<MyState> context)
        {
            return Task.FromResult<StepResult>(new StepResult.Failed(new Exception("boom")));
        }
    }
}

