namespace OrcaCore.Core.Lifecycle;

internal readonly record struct LifecycleTransition(
    global::OrcaCore.WorkflowInstanceStatus Current,
    LifecycleTrigger Trigger);
