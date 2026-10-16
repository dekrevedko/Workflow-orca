using OrcaCore.Core.Lifecycle;

namespace OrcaCore.Engine.Ephemeral.Execution;

internal static class WorkflowLifecycleTransition
{
    internal static void FireOrThrow<TState>(
        WorkflowInstance<TState> instance,
        LifecycleTrigger trigger)
    {
        var result = LifecycleMachine.Fire(instance.Status, trigger);
        if (result.IsFailure)
        {
            throw result.Error;
        }
    }
}
