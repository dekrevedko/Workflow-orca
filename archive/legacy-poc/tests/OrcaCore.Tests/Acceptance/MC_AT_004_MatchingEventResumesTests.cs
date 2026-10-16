
namespace OrcaCore.Tests;

public class MC_AT_004_MatchingEventResumesTests
{
    private sealed class OrderState
    {
        public string OrderId { get; set; } = "order-42";
        public double Price { get; set; }
    }

    private sealed class CaptureEventStep : IStep<OrderState>
    {
        public string StepId => "CaptureEvent";

        public Task<StepResult> ExecuteAsync(StepContext<OrderState> context)
        {
            if (context.ResumedEvent is { Payload: double price })
            {
                context.State.Price = price;
            }

            return Task.FromResult<StepResult>(new StepResult.Completed());
        }
    }

    [Fact]
    public async Task Matching_event_resumes_waiting_workflow_to_completion()
    {
        // Arrange
        var definition = new WorkflowBuilder<OrderState>("OrderWorkflow")
            .Init()
            .Wait("OrderApproved", s => s.OrderId)
            .Then<CaptureEventStep>()
            .End()
            .Build();

        await using var engine = new WorkflowEngine();
        var typed = engine.ForDefinition(definition);
        var snapshot = await typed.Start(new OrderState());

        Assert.Equal(WorkflowStatus.Waiting, snapshot.Status);

        // Act
        var envelope = new EventEnvelope(
            EventName: "OrderApproved",
            CorrelationId: "order-42",
            Payload: 42.0,
            EventId: Guid.NewGuid().ToString("N"));

        await engine.Instance(snapshot.InstanceId).RaiseEvent(envelope);

        // Assert
        var final = engine.Instance(snapshot.InstanceId).Get();
        Assert.Equal(WorkflowStatus.Completed, final.Status);

        var state = engine.Instance(snapshot.InstanceId).GetState<OrderState>();
        Assert.Equal(42.0, state.Price);

        // Wait record should be Matched
        var waits = engine.Instance(snapshot.InstanceId).GetActiveWaits();
        Assert.Empty(waits); // no active waits remain
    }

    [Fact]
    public async Task Matching_event_does_not_resume_workflow_twice()
    {
        // RWI-AT-004: "the workflow does not resume twice"
        // Raising the same event (same EventId) a second time after the workflow has
        // already resumed must not trigger a second continuation.
        // The accumulating step adds the payload to Price; if it ran twice the value
        // would double – a single execution produces exactly the expected value.
        var definition = new WorkflowBuilder<OrderState>("OrderWorkflow")
            .Init()
            .Wait("OrderApproved", s => s.OrderId)
            .Then<AccumulatePriceStep>()
            .End()
            .Build();

        await using var engine = new WorkflowEngine();
        var typed = engine.ForDefinition(definition);
        var snapshot = await typed.Start(new OrderState());
        var scope = engine.Instance(snapshot.InstanceId);

        var envelope = new EventEnvelope(
            EventName: "OrderApproved",
            CorrelationId: "order-42",
            Payload: 10.0,
            EventId: "evt-once");

        // First raise – resumes the workflow, AccumulatePriceStep runs once → Price=10.0
        await scope.RaiseEvent(envelope);
        Assert.Equal(WorkflowStatus.Completed, scope.Get().Status);
        Assert.Equal(10.0, scope.GetState<OrderState>().Price);

        // Second raise with same EventId – deduplicated before any execution; Price still 10.0
        await scope.RaiseEvent(envelope);
        Assert.Equal(10.0, scope.GetState<OrderState>().Price);
    }

    private sealed class AccumulatePriceStep : IStep<OrderState>
    {
        public string StepId => "AccumulatePrice";

        public Task<StepResult> ExecuteAsync(StepContext<OrderState> context)
        {
            if (context.ResumedEvent?.Payload is double amount)
                context.State.Price += amount;
            return Task.FromResult<StepResult>(new StepResult.Completed());
        }
    }
}

