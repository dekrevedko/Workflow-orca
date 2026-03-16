using OrcaCore.Abstractions;

namespace OrcaCore.Runtime;

internal static class InstanceLifecycle
{
    public static void TransitionTo(RuntimeState state, WorkflowStatus target)
    {
        var current = state.Status;
        if (!IsValidTransition(current, target))
            throw new InvalidOperationException(
                $"Invalid workflow state transition from {current} to {target}.");

        state.Status = target;
        state.LastTransitionAt = DateTimeOffset.UtcNow;
    }

    private static bool IsValidTransition(WorkflowStatus from, WorkflowStatus to) =>
        (from, to) switch
        {
            (WorkflowStatus.Running, WorkflowStatus.Completed) => true,
            (WorkflowStatus.Running, WorkflowStatus.Failed) => true,
            (WorkflowStatus.Running, WorkflowStatus.Waiting) => true,
            (WorkflowStatus.Waiting, WorkflowStatus.Running) => true,
            _ => false
        };
}
