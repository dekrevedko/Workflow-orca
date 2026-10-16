namespace OrcaCore.Abstractions.Diagnostics;

/// <summary>Defines the stable BCL metric names emitted by OrcaCore runtime owners.</summary>
public static class OrcaCoreMetrics
{
    public const string InstancesActive = "orca.instances.active";
    public const string InstancesStuck = "orca.instances.stuck";
    public const string WaitsActive = "orca.waits.active";
    public const string OutboxPending = "orca.outbox.pending";
    public const string StreamEvents = "orca.stream.events";
    public const string CheckpointsCount = "orca.checkpoints.count";
    public const string CheckpointsLag = "orca.checkpoints.lag";
    public const string ResourcePoolWaiters = "orca.resource_pool.waiters";
    public const string ResourcePoolTickets = "orca.resource_pool.tickets";
    public const string ResourcePoolReservedUnits = "orca.resource_pool.reserved_units";
    public const string ResourcePoolOverCapacityDebt = "orca.resource_pool.over_capacity_debt";
    public const string ResourcePoolReconciliationDue = "orca.resource_pool.reconciliation_due";

    public const string CommandsProcessed = "orca.commands.processed";
    public const string EventsApplied = "orca.events.applied";
    public const string StepsCompleted = "orca.steps.completed";
    public const string StepsFailed = "orca.steps.failed";
    public const string OutboxDispatched = "orca.outbox.dispatched";
    public const string ResourcePoolReconciliations = "orca.resource_pool.reconciliations";
    public const string LifecycleEvents = "orca.lifecycle.events";
    public const string InboxDuplicates = "orca.inbox.duplicates";

    public const string CommandsDuration = "orca.commands.duration";
    public const string StepsDuration = "orca.steps.duration";
    public const string ProviderCommitDuration = "orca.provider.commit.duration";
    public const string OutboxDispatchDuration = "orca.outbox.dispatch.duration";
    public const string WaitsDuration = "orca.waits.duration";

    public const string DriverSegmentDuration = "orca.driver.segment.duration";
    public const string ContinuationPendingCount = "orca.continuation.pending.count";
    public const string ContinuationLag = "orca.continuation.lag";
    public const string ExternalOutboxPendingCount = "orca.outbox.external.pending.count";
    public const string DriverPoisonCount = "orca.driver.poison.count";
    public const string DriverRegistrationConflictCount = "orca.driver.registration_conflict.count";

    public const string GovernanceConfiguredLimit = "orca.governance.configured_limit";
    public const string GovernanceActiveSlots = "orca.governance.active_slots";
    public const string GovernanceWaitDepth = "orca.governance.wait_depth";
    public const string GovernanceCancellations = "orca.governance.cancellations";
}
