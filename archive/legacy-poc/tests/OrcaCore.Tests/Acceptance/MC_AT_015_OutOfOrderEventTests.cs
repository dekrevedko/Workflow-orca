
namespace OrcaCore.Tests;

public class MC_AT_015_OutOfOrderEventTests
{
    private sealed class MyState
    {
        public string Id { get; set; } = "corr-1";
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
    public async Task Out_of_order_event_is_buffered_and_consumed_when_wait_appears()
    {
        // Arrange: Init â†’ StepA â†’ Wait("EventA") â†’ StepB â†’ Wait("EventB") â†’ End
        var definition = new WorkflowBuilder<MyState>("BufferWorkflow")
            .Init()
            .Then<StepA>()
            .Wait("EventA", s => s.Id)
            .Then<StepB>()
            .Wait("EventB", s => s.Id)
            .End()
            .Build();

        await using var engine = new WorkflowEngine();
        var typed = engine.ForDefinition(definition);
        var snapshot = await typed.Start(new MyState());

        // After start: StepA executed, waiting for EventA
        Assert.Equal(WorkflowStatus.Waiting, snapshot.Status);

        var scope = engine.Instance(snapshot.InstanceId);

        // Act 1: Send EventB first â€” no active wait for it yet, should be buffered
        await scope.RaiseEvent(new EventEnvelope(
            EventName: "EventB",
            CorrelationId: "corr-1",
            Payload: null,
            EventId: "evt-b-1"));

        // Still waiting for EventA
        Assert.Equal(WorkflowStatus.Waiting, scope.Get().Status);

        // Act 2: Send EventA â€” matches active wait, resumes workflow
        await scope.RaiseEvent(new EventEnvelope(
            EventName: "EventA",
            CorrelationId: "corr-1",
            Payload: null,
            EventId: "evt-a-1"));

        // Workflow should have: resumed from EventA â†’ StepB â†’ Wait("EventB") â†’ found buffered EventB â†’ completed
        Assert.Equal(WorkflowStatus.Completed, scope.Get().Status);

        var state = scope.GetState<MyState>();
        Assert.True(state.StepAExecuted);
        Assert.True(state.StepBExecuted);
    }
}

