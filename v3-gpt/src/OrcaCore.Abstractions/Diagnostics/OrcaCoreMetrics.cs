namespace OrcaCore.Abstractions.Diagnostics;

/// <summary>
/// Defines stable OrcaCore metric instrument names.
/// </summary>
public static class OrcaCoreMetrics
{
    public const string InstancesActiveName = "orca.instances.active";
    public const string InstancesStuckName = "orca.instances.stuck";
    public const string WaitsActiveName = "orca.waits.active";
    public const string OutboxPendingName = "orca.outbox.pending";
    public const string StreamEventsName = "orca.stream.events";
    public const string CheckpointsCountName = "orca.checkpoints.count";
    public const string CheckpointsLagName = "orca.checkpoints.lag";
    public const string ResourcePoolWaitersName = "orca.resource_pool.waiters";
    public const string ResourcePoolTicketsName = "orca.resource_pool.tickets";

    public const string CommandsProcessedName = "orca.commands.processed";
    public const string EventsAppliedName = "orca.events.applied";
    public const string StepsCompletedName = "orca.steps.completed";
    public const string StepsFailedName = "orca.steps.failed";
    public const string OutboxDispatchedName = "orca.outbox.dispatched";
    public const string LifecycleEventsName = "orca.lifecycle.events";
    public const string InboxDuplicatesName = "orca.inbox.duplicates";

    public const string CommandsDurationName = "orca.commands.duration";
    public const string StepsDurationName = "orca.steps.duration";
    public const string ProviderCommitDurationName = "orca.provider.commit.duration";
    public const string OutboxDispatchDurationName = "orca.outbox.dispatch.duration";
    public const string WaitsDurationName = "orca.waits.duration";
}
