using System.Diagnostics;
using System.Diagnostics.Metrics;
using OrcaCore.Abstractions.Diagnostics;
using OrcaCore.Abstractions.Durable;
using OrcaCore.Abstractions.Providers;
using OrcaCore.Engine.Durable.Driver;
using OrcaCore.Engine.Durable.Execution;
using OrcaCore.Engine.Durable.Outbox;
using OrcaCore.Runtime.Protocol.ResourceGovernance;

namespace OrcaCore.Engine.Durable.Diagnostics;

/// <summary>Owns durable engine BCL diagnostics sources and immutable gauge snapshots.</summary>
internal static class OrcaCoreDurableDiagnostics
{
    internal const string QuarantinedUnitsInstrumentName = "orca.resource_pool.quarantined_units";
    internal const string OldestQuarantinedAgeInstrumentName =
        "orca.resource_pool.oldest_quarantined_obligation_age";
    internal const string FencedBodiesRunningInstrumentName = "orca.execution.fenced_bodies.running";

    private static readonly object OperationalGate = new();
    private static IReadOnlyDictionary<PoolStateKey, PoolOperationalSnapshot> leaseStates =
        new Dictionary<PoolStateKey, PoolOperationalSnapshot>();
    private static FleetGaugeSnapshot fleet = FleetGaugeSnapshot.Empty;
    private static long fencedBodiesRunning;

    internal const string SourceName = OrcaCoreDiagnostics.DurableSourceName;

    internal static ActivitySource ActivitySource { get; } = new(SourceName);

    internal static Meter Meter { get; } = new(SourceName);

    private static readonly Counter<long> CommandsProcessed =
        Meter.CreateCounter<long>(OrcaCoreMetrics.CommandsProcessed);
    private static readonly Counter<long> EventsApplied =
        Meter.CreateCounter<long>(OrcaCoreMetrics.EventsApplied);
    private static readonly Counter<long> StepsCompleted =
        Meter.CreateCounter<long>(OrcaCoreMetrics.StepsCompleted);
    private static readonly Counter<long> StepsFailed =
        Meter.CreateCounter<long>(OrcaCoreMetrics.StepsFailed);
    private static readonly Counter<long> OutboxDispatched =
        Meter.CreateCounter<long>(OrcaCoreMetrics.OutboxDispatched);
    private static readonly Counter<long> ResourcePoolReconciliations =
        Meter.CreateCounter<long>(OrcaCoreMetrics.ResourcePoolReconciliations);
    private static readonly Counter<long> LifecycleEvents =
        Meter.CreateCounter<long>(OrcaCoreMetrics.LifecycleEvents);
    private static readonly Counter<long> InboxDuplicates =
        Meter.CreateCounter<long>(OrcaCoreMetrics.InboxDuplicates);
    private static readonly Counter<long> DriverPoisons =
        Meter.CreateCounter<long>(OrcaCoreMetrics.DriverPoisonCount);
    private static readonly Counter<long> DriverRegistrationConflicts =
        Meter.CreateCounter<long>(OrcaCoreMetrics.DriverRegistrationConflictCount);

    private static readonly Histogram<double> CommandDuration =
        Meter.CreateHistogram<double>(OrcaCoreMetrics.CommandsDuration, unit: OrcaCoreDiagnostics.SecondsUnit);
    private static readonly Histogram<double> StepDuration =
        Meter.CreateHistogram<double>(OrcaCoreMetrics.StepsDuration, unit: OrcaCoreDiagnostics.SecondsUnit);
    private static readonly Histogram<double> ProviderCommitDuration =
        Meter.CreateHistogram<double>(OrcaCoreMetrics.ProviderCommitDuration, unit: OrcaCoreDiagnostics.SecondsUnit);
    private static readonly Histogram<double> OutboxDispatchDuration =
        Meter.CreateHistogram<double>(OrcaCoreMetrics.OutboxDispatchDuration, unit: OrcaCoreDiagnostics.SecondsUnit);
    private static readonly Histogram<double> WaitDuration =
        Meter.CreateHistogram<double>(OrcaCoreMetrics.WaitsDuration, unit: OrcaCoreDiagnostics.SecondsUnit);
    private static readonly Histogram<double> DriverSegmentDuration =
        Meter.CreateHistogram<double>(OrcaCoreMetrics.DriverSegmentDuration, unit: OrcaCoreDiagnostics.SecondsUnit);
    private static readonly Histogram<double> ContinuationLag =
        Meter.CreateHistogram<double>(OrcaCoreMetrics.ContinuationLag, unit: OrcaCoreDiagnostics.SecondsUnit);

    private static readonly ObservableGauge<long>[] FleetGauges =
    [
        Meter.CreateObservableGauge(OrcaCoreMetrics.InstancesActive, () => Volatile.Read(ref fleet).ActiveInstances),
        Meter.CreateObservableGauge(OrcaCoreMetrics.InstancesStuck, () => Volatile.Read(ref fleet).StuckInstances),
        Meter.CreateObservableGauge(OrcaCoreMetrics.WaitsActive, () => Volatile.Read(ref fleet).ActiveWaits),
        Meter.CreateObservableGauge(OrcaCoreMetrics.OutboxPending, () => Volatile.Read(ref fleet).OutboxStates),
        Meter.CreateObservableGauge(
            OrcaCoreMetrics.ContinuationPendingCount,
            () => Volatile.Read(ref fleet).ContinuationStates),
        Meter.CreateObservableGauge(
            OrcaCoreMetrics.ExternalOutboxPendingCount,
            () => Volatile.Read(ref fleet).ExternalOutboxStates),
        Meter.CreateObservableGauge(OrcaCoreMetrics.StreamEvents, () => Volatile.Read(ref fleet).StreamEvents),
        Meter.CreateObservableGauge(OrcaCoreMetrics.CheckpointsCount, () => Volatile.Read(ref fleet).CheckpointCount),
        Meter.CreateObservableGauge(OrcaCoreMetrics.CheckpointsLag, () => Volatile.Read(ref fleet).CheckpointLag),
        Meter.CreateObservableGauge(
            OrcaCoreMetrics.ResourcePoolWaiters,
            () => Volatile.Read(ref fleet).ResourcePoolWaiters),
        Meter.CreateObservableGauge(
            OrcaCoreMetrics.ResourcePoolTickets,
            () => Volatile.Read(ref fleet).ResourcePoolTickets),
        Meter.CreateObservableGauge(
            OrcaCoreMetrics.ResourcePoolReservedUnits,
            () => Volatile.Read(ref fleet).ResourcePoolReservedUnits),
        Meter.CreateObservableGauge(
            OrcaCoreMetrics.ResourcePoolOverCapacityDebt,
            () => Volatile.Read(ref fleet).ResourcePoolOverCapacityDebt),
        Meter.CreateObservableGauge(
            OrcaCoreMetrics.ResourcePoolReconciliationDue,
            () => Volatile.Read(ref fleet).ResourcePoolReconciliationDue)
    ];

    private static readonly ObservableGauge<long> QuarantinedUnits = Meter.CreateObservableGauge(
        QuarantinedUnitsInstrumentName,
        ObserveQuarantinedUnits);
    private static readonly ObservableGauge<double> OldestQuarantinedAge = Meter.CreateObservableGauge(
        OldestQuarantinedAgeInstrumentName,
        ObserveOldestQuarantinedAge,
        unit: OrcaCoreDiagnostics.SecondsUnit);
    private static readonly ObservableGauge<long> FencedBodiesRunning = Meter.CreateObservableGauge(
        FencedBodiesRunningInstrumentName,
        () => Interlocked.Read(ref fencedBodiesRunning));

    internal static void RefreshOperatorStatistics(
        WorkflowOperatorStatistics statistics,
        IReadOnlyList<ResourcePoolSnapshot> resourcePools)
    {
        ArgumentNullException.ThrowIfNull(statistics);
        ArgumentNullException.ThrowIfNull(resourcePools);
        _ = FleetGauges;
        var pressure = statistics.Pressure;
        IReadOnlyDictionary<PoolStateKey, PoolOperationalSnapshot> currentLeaseStates;
        lock (OperationalGate)
        {
            currentLeaseStates = leaseStates;
        }

        Volatile.Write(
            ref fleet,
            new FleetGaugeSnapshot(
                ActiveInstanceMeasurements(statistics),
                StuckInstanceMeasurements(statistics),
                ActiveWaitMeasurements(statistics),
                OutboxStateMeasurements(pressure),
                QueueStateMeasurements(statistics.ProviderName, true, pressure),
                QueueStateMeasurements(statistics.ProviderName, false, pressure),
                ProviderMeasurement(statistics.ProviderName, pressure.StreamEventCount),
                ProviderMeasurement(statistics.ProviderName, pressure.CheckpointCount),
                ProviderMeasurement(statistics.ProviderName, pressure.CheckpointLag),
                PoolMeasurements(resourcePools, pool => pool.QueuedWaiters.Count),
                PoolStateMeasurements(resourcePools, currentLeaseStates, snapshot => snapshot.TicketCount),
                PoolStateMeasurements(resourcePools, currentLeaseStates, snapshot => snapshot.ReservedUnits),
                PoolMeasurements(
                    resourcePools,
                    pool => Math.Max(0, pool.HeldTickets.Sum(ticket => ticket.Count) - pool.Capacity)),
                PoolMeasurements(resourcePools, pool => pool.ExpiredTickets.Count)));
    }

    internal static void RecordCommand(WorkflowRuntimeObservation observation)
    {
        ArgumentNullException.ThrowIfNull(observation);
        var tags = RuntimeTags(observation);
        CommandsProcessed.Add(1, tags);
        CommandDuration.Record(observation.Duration.TotalSeconds, tags);
        if (observation.InboxDuplicate)
        {
            InboxDuplicates.Add(1, ModeTags());
        }

        if (observation.ProviderCommitAttempted)
        {
            var providerTags = new TagList
            {
                { OrcaCoreDiagnostics.ProviderNameKey, observation.ProviderName },
                { OrcaCoreDiagnostics.ProviderOperationKey, observation.ProviderOperation }
            };
            ProviderCommitDuration.Record(observation.ProviderCommitDuration.TotalSeconds, providerTags);
        }

        foreach (var workflowEvent in observation.Events)
        {
            var eventTags = ModeTags();
            eventTags.Add(OrcaCoreDiagnostics.EventTypeKey, workflowEvent.EventType);
            EventsApplied.Add(1, eventTags);
            if (workflowEvent.StepPath is { } stepPath)
            {
                var stepTags = ModeTags();
                stepTags.Add(OrcaCoreDiagnostics.StepPathKey, stepPath);
                if (workflowEvent.ErrorKind is { } errorKind)
                {
                    stepTags.Add(OrcaCoreDiagnostics.ErrorKindKey, errorKind);
                    StepsFailed.Add(1, stepTags);
                }
                else
                {
                    StepsCompleted.Add(1, stepTags);
                }

                if (workflowEvent.StepDuration is { } duration)
                {
                    StepDuration.Record(duration.TotalSeconds, stepTags);
                }
            }

            if (workflowEvent.LifecycleEventName is { } lifecycle)
            {
                LifecycleEvents.Add(
                    1,
                    new KeyValuePair<string, object?>(OrcaCoreDiagnostics.LifecycleEventNameKey, lifecycle),
                    new KeyValuePair<string, object?>(OrcaCoreDiagnostics.DurableKey, workflowEvent.DurableLifecycle));
            }

            if (workflowEvent.WaitEventName is { } waitName && workflowEvent.WaitDuration is { } waitDuration)
            {
                WaitDuration.Record(
                    waitDuration.TotalSeconds,
                    new KeyValuePair<string, object?>(OrcaCoreDiagnostics.WaitEventNameKey, waitName));
            }

            if (workflowEvent.ParkReason is DurableParkReason.Poison)
            {
                DriverPoisons.Add(1, ModeTags());
            }

        }
    }

    internal static void RecordRegistrationConflict() =>
        DriverRegistrationConflicts.Add(1, ModeTags());

    internal static void RecordOutboxDispatch(OutboxDispatchObservation observation)
    {
        ArgumentNullException.ThrowIfNull(observation);
        var tags = new TagList
        {
            { OrcaCoreDiagnostics.OutboxKindKey, observation.Kind },
            { OrcaCoreDiagnostics.OutboxResultKey, observation.Result.ToString() }
        };
        OutboxDispatched.Add(1, tags);
        OutboxDispatchDuration.Record(observation.Duration.TotalSeconds, tags);
    }

    internal static void RecordDriverSegment(DurableDriverSegmentObservation observation)
    {
        var tags = new TagList
        {
            { OrcaCoreDiagnostics.DefinitionIdKey, observation.DefinitionId.ToString() },
            { OrcaCoreDiagnostics.DefinitionVersionKey, observation.DefinitionVersion.ToString() },
            { OrcaCoreDiagnostics.DriverOutcomeKey, observation.Outcome }
        };
        DriverSegmentDuration.Record(observation.Duration.TotalSeconds, tags);
    }

    internal static void RecordContinuation(DurableContinuationObservation observation)
    {
        ContinuationLag.Record(
            observation.Lag.TotalSeconds,
            new KeyValuePair<string, object?>(OrcaCoreDiagnostics.ProviderNameKey, observation.ProviderName),
            new KeyValuePair<string, object?>(OrcaCoreDiagnostics.DriverOutcomeKey, observation.Outcome));
    }

    internal static void RecordResourcePoolReconciliation(string poolName, string outcome)
    {
        ResourcePoolReconciliations.Add(
            1,
            new KeyValuePair<string, object?>(OrcaCoreDiagnostics.ResourcePoolNameKey, poolName),
            new KeyValuePair<string, object?>(OrcaCoreDiagnostics.CommandOutcomeKey, outcome));
    }

    internal static void RefreshLeaseObligations(IEnumerable<DurableResourceLeaseObligationSnapshot> obligations)
    {
        ArgumentNullException.ThrowIfNull(obligations);
        var next = obligations
            .SelectMany(obligation => obligation.Tickets.Select(ticket => new
            {
                Pool = ticket.Pool.Value,
                State = ResourceState(obligation.Status),
                ticket.Units,
                obligation.QuarantinedAt
            }))
            .Where(item => item.State is not null)
            .GroupBy(item => new PoolStateKey(item.Pool, item.State!))
            .ToDictionary(
                group => group.Key,
                group => new PoolOperationalSnapshot(
                    group.LongCount(),
                    group.Sum(item => (long)item.Units),
                    group.Where(item => item.QuarantinedAt is not null)
                        .Select(item => item.QuarantinedAt!.Value)
                        .DefaultIfEmpty()
                        .Min()));
        lock (OperationalGate)
        {
            leaseStates = next;
        }
    }

    internal static void TrackFencedBody(Task physicalBody)
    {
        ArgumentNullException.ThrowIfNull(physicalBody);
        Interlocked.Increment(ref fencedBodiesRunning);
        _ = physicalBody.ContinueWith(
            static _ => Interlocked.Decrement(ref fencedBodiesRunning),
            CancellationToken.None,
            TaskContinuationOptions.ExecuteSynchronously,
            TaskScheduler.Default);
    }

    private static Measurement<long>[] ActiveInstanceMeasurements(WorkflowOperatorStatistics statistics) =>
        statistics.Groups
            .Where(group => IsActive(group.Status))
            .Select(group => new Measurement<long>(
                group.Count,
                new KeyValuePair<string, object?>(
                    OrcaCoreDiagnostics.ExecutionModeKey,
                    OrcaCoreDiagnostics.DurableExecutionMode),
                new KeyValuePair<string, object?>(OrcaCoreDiagnostics.DefinitionIdKey, group.DefinitionId.ToString()),
                new KeyValuePair<string, object?>(
                    OrcaCoreDiagnostics.DefinitionVersionKey,
                    group.DefinitionVersion.ToString()),
                new KeyValuePair<string, object?>(OrcaCoreDiagnostics.StatusKey, group.Status.ToString())))
            .ToArray();

    private static Measurement<long>[] StuckInstanceMeasurements(WorkflowOperatorStatistics statistics) =>
        statistics.StuckGroups
            .Select(group => new Measurement<long>(
                group.Count,
                new KeyValuePair<string, object?>(
                    OrcaCoreDiagnostics.ExecutionModeKey,
                    OrcaCoreDiagnostics.DurableExecutionMode),
                new KeyValuePair<string, object?>(
                    OrcaCoreDiagnostics.DefinitionIdKey,
                    group.DefinitionId.ToString())))
            .ToArray();

    private static Measurement<long>[] ActiveWaitMeasurements(WorkflowOperatorStatistics statistics) =>
        statistics.ActiveWaitGroups
            .Select(group => new Measurement<long>(
                group.Count,
                new KeyValuePair<string, object?>(
                    OrcaCoreDiagnostics.ExecutionModeKey,
                    OrcaCoreDiagnostics.DurableExecutionMode),
                new KeyValuePair<string, object?>(
                    OrcaCoreDiagnostics.DefinitionIdKey,
                    group.DefinitionId.ToString()),
                new KeyValuePair<string, object?>(
                    OrcaCoreDiagnostics.WaitEventNameKey,
                    group.EventName.Value)))
            .ToArray();

    private static Measurement<long>[] OutboxStateMeasurements(WorkflowOperationalPressure pressure) =>
    [
        StateMeasurement(
            OrcaCoreDiagnostics.PendingOutboxState,
            pressure.ContinuationPendingCount + pressure.ExternalOutboxPendingCount),
        StateMeasurement(
            OrcaCoreDiagnostics.RetryableOutboxState,
            pressure.ContinuationRetryableCount + pressure.ExternalOutboxRetryableCount),
        StateMeasurement(
            OrcaCoreDiagnostics.ClaimedOutboxState,
            pressure.ContinuationClaimedCount + pressure.ExternalOutboxClaimedCount),
        StateMeasurement(
            OrcaCoreDiagnostics.PoisonedOutboxState,
            pressure.ContinuationPoisonedCount + pressure.ExternalOutboxPoisonedCount)
    ];

    private static Measurement<long>[] QueueStateMeasurements(
        string providerName,
        bool continuation,
        WorkflowOperationalPressure pressure) =>
    [
        QueueStateMeasurement(providerName, continuation, OrcaCoreDiagnostics.PendingOutboxState, continuation
            ? pressure.ContinuationPendingCount
            : pressure.ExternalOutboxPendingCount),
        QueueStateMeasurement(providerName, continuation, OrcaCoreDiagnostics.RetryableOutboxState, continuation
            ? pressure.ContinuationRetryableCount
            : pressure.ExternalOutboxRetryableCount),
        QueueStateMeasurement(providerName, continuation, OrcaCoreDiagnostics.ClaimedOutboxState, continuation
            ? pressure.ContinuationClaimedCount
            : pressure.ExternalOutboxClaimedCount),
        QueueStateMeasurement(providerName, continuation, OrcaCoreDiagnostics.PoisonedOutboxState, continuation
            ? pressure.ContinuationPoisonedCount
            : pressure.ExternalOutboxPoisonedCount)
    ];

    private static Measurement<long> StateMeasurement(string state, long value) =>
        new(value, new KeyValuePair<string, object?>(OrcaCoreDiagnostics.OutboxStateKey, state));

    private static Measurement<long> QueueStateMeasurement(
        string providerName,
        bool continuation,
        string state,
        long value) =>
        new(
            value,
            new KeyValuePair<string, object?>(OrcaCoreDiagnostics.ProviderNameKey, providerName),
            new KeyValuePair<string, object?>(
                OrcaCoreDiagnostics.QueueLaneKey,
                continuation
                    ? OrcaCoreDiagnostics.ContinuationQueueLane
                    : OrcaCoreDiagnostics.ExternalOutboxQueueLane),
            new KeyValuePair<string, object?>(OrcaCoreDiagnostics.OutboxStateKey, state));

    private static Measurement<long>[] ProviderMeasurement(string providerName, long value) =>
    [
        new(value, new KeyValuePair<string, object?>(OrcaCoreDiagnostics.ProviderNameKey, providerName))
    ];

    private static Measurement<long>[] PoolMeasurements(
        IReadOnlyList<ResourcePoolSnapshot> pools,
        Func<ResourcePoolSnapshot, long> select) =>
        pools.Select(pool => new Measurement<long>(
            select(pool),
            new KeyValuePair<string, object?>(OrcaCoreDiagnostics.ResourcePoolNameKey, pool.Name))).ToArray();

    private static Measurement<long>[] PoolStateMeasurements(
        IReadOnlyList<ResourcePoolSnapshot> pools,
        IReadOnlyDictionary<PoolStateKey, PoolOperationalSnapshot> obligationStates,
        Func<PoolOperationalSnapshot, long> select)
    {
        var states = pools
            .SelectMany(pool => pool.HeldTickets.Select(ticket => new
            {
                Pool = pool.Name,
                State = ticket.ReviewMarked
                    ? OrcaCoreDiagnostics.ReviewMarkedResourceState
                    : OrcaCoreDiagnostics.HeldResourceState,
                ticket.Count
            }))
            .GroupBy(item => new PoolStateKey(item.Pool, item.State))
            .ToDictionary(
                group => group.Key,
                group => new PoolOperationalSnapshot(
                    group.LongCount(),
                    group.Sum(item => (long)item.Count),
                    default));
        foreach (var (key, value) in obligationStates)
        {
            states[key] = value;
        }

        return states
            .OrderBy(pair => pair.Key.PoolName, StringComparer.Ordinal)
            .ThenBy(pair => pair.Key.State, StringComparer.Ordinal)
            .Select(pair => new Measurement<long>(
                select(pair.Value),
                new KeyValuePair<string, object?>(OrcaCoreDiagnostics.ResourcePoolNameKey, pair.Key.PoolName),
                new KeyValuePair<string, object?>(OrcaCoreDiagnostics.ResourcePoolStateKey, pair.Key.State)))
            .ToArray();
    }

    private static IEnumerable<Measurement<long>> ObserveQuarantinedUnits()
    {
        IReadOnlyDictionary<PoolStateKey, PoolOperationalSnapshot> snapshot;
        lock (OperationalGate)
        {
            snapshot = leaseStates;
        }

        return snapshot
            .Where(pair => pair.Key.State == OrcaCoreDiagnostics.QuarantinedResourceState)
            .Select(pair => new Measurement<long>(
                pair.Value.ReservedUnits,
                new KeyValuePair<string, object?>(OrcaCoreDiagnostics.ResourcePoolNameKey, pair.Key.PoolName)));
    }

    private static IEnumerable<Measurement<double>> ObserveOldestQuarantinedAge()
    {
        IReadOnlyDictionary<PoolStateKey, PoolOperationalSnapshot> snapshot;
        lock (OperationalGate)
        {
            snapshot = leaseStates;
        }

        var now = TimeProvider.System.GetUtcNow();
        return snapshot
            .Where(pair => pair.Key.State == OrcaCoreDiagnostics.QuarantinedResourceState)
            .Select(pair => new Measurement<double>(
                pair.Value.OldestQuarantinedAt == default
                    ? 0
                    : Math.Max(0, (now - pair.Value.OldestQuarantinedAt).TotalSeconds),
                new KeyValuePair<string, object?>(OrcaCoreDiagnostics.ResourcePoolNameKey, pair.Key.PoolName)));
    }

    private static TagList RuntimeTags(WorkflowRuntimeObservation observation)
    {
        var tags = ModeTags();
        tags.Add(OrcaCoreDiagnostics.CommandTypeKey, observation.CommandType);
        tags.Add(OrcaCoreDiagnostics.CommandOutcomeKey, observation.Outcome.ToString());
        if (observation.DefinitionId is { } definitionId)
        {
            tags.Add(OrcaCoreDiagnostics.DefinitionIdKey, definitionId.ToString());
        }

        if (observation.DefinitionVersion is { } definitionVersion)
        {
            tags.Add(OrcaCoreDiagnostics.DefinitionVersionKey, definitionVersion.ToString());
        }

        if (observation.Status is { } status)
        {
            tags.Add(OrcaCoreDiagnostics.StatusKey, status.ToString());
        }

        return tags;
    }

    private static TagList ModeTags() =>
        new() { { OrcaCoreDiagnostics.ExecutionModeKey, OrcaCoreDiagnostics.DurableExecutionMode } };

    private static bool IsActive(global::OrcaCore.WorkflowInstanceStatus status) =>
        status is global::OrcaCore.WorkflowInstanceStatus.Pending or
            global::OrcaCore.WorkflowInstanceStatus.Running or
            global::OrcaCore.WorkflowInstanceStatus.Waiting or
            global::OrcaCore.WorkflowInstanceStatus.CancellationRequested;

    private static string? ResourceState(DurableResourceLeaseObligationStatus status) =>
        status switch
        {
            DurableResourceLeaseObligationStatus.PendingCommit => OrcaCoreDiagnostics.PendingCommitResourceState,
            DurableResourceLeaseObligationStatus.Held => OrcaCoreDiagnostics.HeldResourceState,
            DurableResourceLeaseObligationStatus.ReviewMarked => OrcaCoreDiagnostics.ReviewMarkedResourceState,
            DurableResourceLeaseObligationStatus.AmbiguousHeld => OrcaCoreDiagnostics.AmbiguousHeldResourceState,
            DurableResourceLeaseObligationStatus.Quarantined => OrcaCoreDiagnostics.QuarantinedResourceState,
            _ => null
        };

    private sealed record PoolStateKey(string PoolName, string State);

    private sealed record PoolOperationalSnapshot(
        long TicketCount,
        long ReservedUnits,
        DateTimeOffset OldestQuarantinedAt);

    private sealed record FleetGaugeSnapshot(
        Measurement<long>[] ActiveInstances,
        Measurement<long>[] StuckInstances,
        Measurement<long>[] ActiveWaits,
        Measurement<long>[] OutboxStates,
        Measurement<long>[] ContinuationStates,
        Measurement<long>[] ExternalOutboxStates,
        Measurement<long>[] StreamEvents,
        Measurement<long>[] CheckpointCount,
        Measurement<long>[] CheckpointLag,
        Measurement<long>[] ResourcePoolWaiters,
        Measurement<long>[] ResourcePoolTickets,
        Measurement<long>[] ResourcePoolReservedUnits,
        Measurement<long>[] ResourcePoolOverCapacityDebt,
        Measurement<long>[] ResourcePoolReconciliationDue)
    {
        internal static FleetGaugeSnapshot Empty { get; } =
            new([], [], [], [], [], [], [], [], [], [], [], [], [], []);
    }
}
