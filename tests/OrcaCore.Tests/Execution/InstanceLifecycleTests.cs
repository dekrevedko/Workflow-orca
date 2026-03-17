
namespace OrcaCore.Tests;

public class InstanceLifecycleTests
{
    [Theory]
    [InlineData(WorkflowStatus.Running, WorkflowStatus.Waiting)]
    [InlineData(WorkflowStatus.Running, WorkflowStatus.Completed)]
    [InlineData(WorkflowStatus.Running, WorkflowStatus.Failed)]
    [InlineData(WorkflowStatus.Waiting, WorkflowStatus.Running)]
    public void Valid_transitions_are_allowed(WorkflowStatus from, WorkflowStatus to)
    {
        var state = new RuntimeState
        {
            Status = from
        };

        InstanceLifecycle.TransitionTo(state, to);

        Assert.Equal(to, state.Status);
    }

    [Theory]
    [InlineData(WorkflowStatus.Waiting, WorkflowStatus.Completed)]
    [InlineData(WorkflowStatus.Waiting, WorkflowStatus.Waiting)]
    [InlineData(WorkflowStatus.Completed, WorkflowStatus.Running)]
    [InlineData(WorkflowStatus.Completed, WorkflowStatus.Failed)]
    [InlineData(WorkflowStatus.Failed, WorkflowStatus.Running)]
    public void Invalid_transitions_are_rejected(WorkflowStatus from, WorkflowStatus to)
    {
        var state = new RuntimeState
        {
            Status = from
        };

        var ex = Assert.Throws<InvalidOperationException>(() =>
            InstanceLifecycle.TransitionTo(state, to));

        Assert.Contains("Invalid workflow state transition", ex.Message, StringComparison.OrdinalIgnoreCase);
    }
}
