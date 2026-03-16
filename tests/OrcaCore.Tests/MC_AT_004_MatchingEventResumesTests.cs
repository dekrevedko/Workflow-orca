using OrcaCore.Abstractions;
using OrcaCore.Runtime;

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
            .Step<CaptureEventStep>()
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
}
