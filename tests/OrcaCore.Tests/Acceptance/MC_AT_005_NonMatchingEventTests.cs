
namespace OrcaCore.Tests;

public class MC_AT_005_NonMatchingEventTests
{
    private sealed class OrderState
    {
        public string OrderId { get; set; } = "order-123";
    }

    [Fact]
    public async Task Wrong_correlation_id_does_not_resume_workflow()
    {
        var definition = new WorkflowBuilder<OrderState>("OrderWorkflow")
            .Init()
            .Wait("OrderApproved", s => s.OrderId)
            .End()
            .Build();

        await using var engine = new WorkflowEngine();
        var typed = engine.ForDefinition(definition);
        var snapshot = await typed.Start(new OrderState());
        Assert.Equal(WorkflowStatus.Waiting, snapshot.Status);

        // Act â€” wrong CorrelationId
        await engine.Instance(snapshot.InstanceId).RaiseEvent(new EventEnvelope(
            EventName: "OrderApproved",
            CorrelationId: "order-999",
            Payload: null,
            EventId: Guid.NewGuid().ToString("N")));

        // Assert â€” still waiting
        var current = engine.Instance(snapshot.InstanceId).Get();
        Assert.Equal(WorkflowStatus.Waiting, current.Status);
        Assert.Single(engine.Instance(snapshot.InstanceId).GetActiveWaits());
    }

    [Fact]
    public async Task Wrong_event_name_does_not_resume_workflow()
    {
        var definition = new WorkflowBuilder<OrderState>("OrderWorkflow")
            .Init()
            .Wait("OrderApproved", s => s.OrderId)
            .End()
            .Build();

        await using var engine = new WorkflowEngine();
        var typed = engine.ForDefinition(definition);
        var snapshot = await typed.Start(new OrderState());

        // Act â€” wrong EventName
        await engine.Instance(snapshot.InstanceId).RaiseEvent(new EventEnvelope(
            EventName: "WrongEvent",
            CorrelationId: "order-123",
            Payload: null,
            EventId: Guid.NewGuid().ToString("N")));

        // Assert â€” still waiting
        var current = engine.Instance(snapshot.InstanceId).Get();
        Assert.Equal(WorkflowStatus.Waiting, current.Status);
        Assert.Single(engine.Instance(snapshot.InstanceId).GetActiveWaits());
    }
}
