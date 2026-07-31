using OrcaCore.Abstractions.Instances;

namespace OrcaCore.Core.Lifecycle;

public readonly record struct LifecycleTransition(WorkflowStatus Current, LifecycleTrigger Trigger);
