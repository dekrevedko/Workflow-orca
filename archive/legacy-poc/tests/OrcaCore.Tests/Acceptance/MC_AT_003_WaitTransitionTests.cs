
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

        var wait = waits[0];
        Assert.Equal("OrderApproved", wait.EventName);
        Assert.Equal("order-42", wait.CorrelationId);
        Assert.Equal(WaitStatus.Active, wait.Status);

        // RWI-AT-003: "one active wait is inspectable" – verify all WaitRecord fields
        Assert.NotEmpty(wait.WaitId);           // unique identifier assigned by engine
        Assert.True(wait.RegisteredAt > DateTimeOffset.MinValue); // timestamp populated
        Assert.Equal(WaitMode.Resident, wait.Mode);  // ephemeral Wait uses Resident mode
        Assert.Null(wait.BranchId);             // top-level wait has no branch
    }
}
