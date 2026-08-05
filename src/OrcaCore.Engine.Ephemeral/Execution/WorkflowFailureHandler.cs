
namespace OrcaCore.Engine.Ephemeral.Execution;

internal sealed class WorkflowFailureHandler<TState>(TimeProvider timeProvider)
{
    internal void Fail(WorkflowInstance<TState> instance, Exception exception, string stepPath)
    {
        var occurredAt = timeProvider.GetUtcNow();
        instance.RecordLifecycleEvent("StepFailed", stepPath, global::OrcaCore.WorkflowInstanceStatus.Failed, occurredAt);
        instance.Fail(new WorkflowErrorDetails(
            exception.GetType().Name,
            exception.Message,
            stepPath,
            occurredAt));
    }
}
