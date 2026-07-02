using OrcaCore.Abstractions.Instances;

namespace OrcaCore.Core.Lifecycle;

internal readonly record struct LifecycleTransition(WorkflowStatus Current, LifecycleTrigger Trigger);
