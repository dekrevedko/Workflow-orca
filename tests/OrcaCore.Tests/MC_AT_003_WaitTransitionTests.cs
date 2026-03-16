using OrcaCore.Abstractions;
using OrcaCore.Runtime;

namespace OrcaCore.Tests;

public class MC_AT_003_WaitTransitionTests
{
    private sealed class OrderState
    {
        public string OrderId { get; set; } = "order-42";
    }

    [Fact]
    public async Task Wait_transitions_instance_into_waiting_state()
    {
        // Arrange
        var definition = new WorkflowBuilder<OrderState>("OrderWorkflow")
            .Init()
            .Wait("OrderApproved", s => s.OrderId)
            .End()
            .Build();

        await using var engine = new WorkflowEngine();
        var typed = engine.ForDefinition(definition);

        // Act
        var snapshot = await typed.Start(new OrderState());

        // Assert
        Assert.Equal(WorkflowStatus.Waiting, snapshot.Status);

        var waits = engine.Instance(snapshot.InstanceId).GetActiveWaits();
        Assert.Single(waits);
        Assert.Equal("OrderApproved", waits[0].EventName);
        Assert.Equal("order-42", waits[0].CorrelationId);
        Assert.Equal(WaitStatus.Active, waits[0].Status);
    }
}
