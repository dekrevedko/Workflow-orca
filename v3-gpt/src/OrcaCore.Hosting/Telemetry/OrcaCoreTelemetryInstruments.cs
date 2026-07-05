using System.Diagnostics;
using System.Diagnostics.Metrics;
using OrcaCore.Abstractions.Diagnostics;
using OrcaCore.Abstractions.Ids;
using OrcaCore.Abstractions.Instances;
using OrcaCore.Abstractions.Providers;
using OrcaCore.Engine.Durable.Diagnostics;
using OrcaCore.Engine.Durable.Execution;

namespace OrcaCore.Hosting.Telemetry;

internal sealed class OrcaCoreTelemetryInstruments
{
    // All fleet gauges are published as one immutable snapshot written with a single atomic
    // reference swap, so a metric scrape never observes a partially-updated set (e.g. new waiters
    // with stale tickets).
    private FleetGaugeSnapshot fleetGauges = FleetGaugeSnapshot.Empty;

    private readonly Counter<long> commandsProcessed =
        OrcaCoreDurableDiagnostics.Meter.CreateCounter<long>(OrcaCoreMetrics.CommandsProcessedName);

    private readonly Counter<long> eventsApplied =
        OrcaCoreDurableDiagnostics.Meter.CreateCounter<long>(OrcaCoreMetrics.EventsAppliedName);

    private readonly Counter<long> stepsCompleted =
        OrcaCoreDurableDiagnostics.Meter.CreateCounter<long>(OrcaCoreMetrics.StepsCompletedName);

    private readonly Counter<long> stepsFailed =
        OrcaCoreDurableDiagnostics.Meter.CreateCounter<long>(OrcaCoreMetrics.StepsFailedName);

    private readonly Counter<long> outboxDispatched =
        OrcaCoreDurableDiagnostics.Meter.CreateCounter<long>(OrcaCoreMetrics.OutboxDispatchedName);

    private readonly Counter<long> lifecycleEvents =
        OrcaCoreDurableDiagnostics.Meter.CreateCounter<long>(OrcaCoreMetrics.LifecycleEventsName);

    private readonly Counter<long> inboxDuplicates =
        OrcaCoreDurableDiagnostics.Meter.CreateCounter<long>(OrcaCoreMetrics.InboxDuplicatesName);

    private readonly Histogram<double> commandsDuration =
        OrcaCoreDurableDiagnostics.Meter.CreateHistogram<double>(OrcaCoreMetrics.CommandsDurationName, "s");

    private readonly Histogram<double> stepsDuration =
        OrcaCoreDurableDiagnostics.Meter.CreateHistogram<double>(OrcaCoreMetrics.StepsDurationName, "s");

    private readonly Histogram<double> providerCommitDuration =
        OrcaCoreDurableDiagnostics.Meter.CreateHistogram<double>(
            OrcaCoreMetrics.ProviderCommitDurationName,
            "s");

    private readonly Histogram<double> outboxDispatchDuration =
        OrcaCoreDurableDiagnostics.Meter.CreateHistogram<double>(
            OrcaCoreMetrics.OutboxDispatchDurationName,
            "s");

    private readonly Histogram<double> waitsDuration =
        OrcaCoreDurableDiagnostics.Meter.CreateHistogram<double>(OrcaCoreMetrics.WaitsDurationName, "s");

    private readonly ObservableGauge<long>[] gauges;

    public OrcaCoreTelemetryInstruments()
    {
        gauges =
        [
            OrcaCoreDurableDiagnostics.Meter.CreateObservableGauge<long>(
                OrcaCoreMetrics.InstancesActiveName,
                () => Volatile.Read(ref fleetGauges).ActiveInstances),
            OrcaCoreDurableDiagnostics.Meter.CreateObservableGauge<long>(
                OrcaCoreMetrics.InstancesStuckName,
                () => Volatile.Read(ref fleetGauges).StuckInstances),
            OrcaCoreDurableDiagnostics.Meter.CreateObservableGauge<long>(
                OrcaCoreMetrics.WaitsActiveName,
                () => Volatile.Read(ref fleetGauges).ActiveWaits),
            OrcaCoreDurableDiagnostics.Meter.CreateObservableGauge<long>(
                OrcaCoreMetrics.OutboxPendingName,
                () => Volatile.Read(ref fleetGauges).OutboxStates),
            OrcaCoreDurableDiagnostics.Meter.CreateObservableGauge<long>(
                OrcaCoreMetrics.StreamEventsName,
                () => Volatile.Read(ref fleetGauges).StreamEvents),
            OrcaCoreDurableDiagnostics.Meter.CreateObservableGauge<long>(
                OrcaCoreMetrics.CheckpointsCountName,
                () => Volatile.Read(ref fleetGauges).CheckpointCounts),
            OrcaCoreDurableDiagnostics.Meter.CreateObservableGauge<long>(
                OrcaCoreMetrics.CheckpointsLagName,
                () => Volatile.Read(ref fleetGauges).CheckpointLag),
            OrcaCoreDurableDiagnostics.Meter.CreateObservableGauge<long>(
                OrcaCoreMetrics.ResourcePoolWaitersName,
                () => Volatile.Read(ref fleetGauges).ResourcePoolWaiters),
            OrcaCoreDurableDiagnostics.Meter.CreateObservableGauge<long>(
                OrcaCoreMetrics.ResourcePoolTicketsName,
                () => Volatile.Read(ref fleetGauges).ResourcePoolTickets)
        ];
    }

    public void RecordCommandProcessed(
        string commandType,
        string outcome,
        DefinitionId? definitionId,
        DefinitionVersion? definitionVersion,
        WorkflowStatus? status,
        TimeSpan duration)
    {
        _ = gauges;
        var tags = CommandTags(commandType, outcome, definitionId, definitionVersion, status);
        commandsProcessed.Add(1, tags);
        commandsDuration.Record(duration.TotalSeconds, tags);
    }

    public void RecordRuntimeSignals(WorkflowRuntimeObservation observation)
    {
        ArgumentNullException.ThrowIfNull(observation);

        if (observation.InboxDuplicate)
        {
            inboxDuplicates.Add(1, DurableModeTags());
        }

        if (observation.ProviderCommitAttempted)
        {
            providerCommitDuration.Record(
                observation.ProviderCommitDuration.TotalSeconds,
                ProviderCommitTags(observation.ProviderName, observation.ProviderOperation));
        }

        foreach (var workflowEvent in observation.Events)
        {
            var eventTags = EventTags(workflowEvent.EventType, workflowEvent.DefinitionId ?? observation.DefinitionId);
            eventsApplied.Add(1, eventTags);

            if (workflowEvent.StepPath is { } stepPath)
            {
                var stepTags = StepTags(observation.DefinitionId, stepPath);
                if (workflowEvent.ErrorKind is { } errorKind)
                {
                    stepTags.Add(OrcaCoreDiagnostics.ErrorKindKey, errorKind);
                    stepsFailed.Add(1, stepTags);
                }
                else
                {
                    stepsCompleted.Add(1, stepTags);
                }

                if (workflowEvent.StepDuration is { } stepDuration)
                {
                    stepsDuration.Record(stepDuration.TotalSeconds, StepTags(observation.DefinitionId, stepPath));
                }
            }

            if (workflowEvent.LifecycleEventName is { } lifecycleEventName)
            {
                lifecycleEvents.Add(1, LifecycleTags(lifecycleEventName, workflowEvent.DurableLifecycle));
            }

            if (workflowEvent.WaitEventName is { } waitEventName &&
                workflowEvent.WaitDuration is { } waitDuration)
            {
                waitsDuration.Record(
                    waitDuration.TotalSeconds,
                    WaitTags(waitEventName, workflowEvent.DefinitionId ?? observation.DefinitionId));
            }
        }
    }

    public void RecordOutboxDispatch(
        string kind,
        string result,
        TimeSpan duration)
    {
        var tags = new TagList
        {
            { OrcaCoreDiagnostics.OutboxKindKey, kind },
            { OrcaCoreDiagnostics.OutboxResultKey, result }
        };

        outboxDispatched.Add(1, tags);
        outboxDispatchDuration.Record(duration.TotalSeconds, tags);
    }

    public void UpdateFleetGauges(
        WorkflowStatistics statistics,
        IReadOnlyList<WorkflowInstanceSnapshot> instances,
        IReadOnlyList<ResourcePoolSnapshot> resourcePools,
        string providerName)
    {
        ArgumentNullException.ThrowIfNull(statistics);
        ArgumentNullException.ThrowIfNull(instances);
        ArgumentNullException.ThrowIfNull(resourcePools);
        ArgumentException.ThrowIfNullOrWhiteSpace(providerName);

        Volatile.Write(
            ref fleetGauges,
            new FleetGaugeSnapshot(
                ActiveInstanceMeasurements(statistics),
                StuckInstanceMeasurements(instances),
                ActiveWaitMeasurements(instances),
                OutboxStateMeasurements(statistics),
                ProviderGauge(providerName, statistics.Pressure.TotalStreamEvents),
                ProviderGauge(providerName, statistics.Pressure.CheckpointCount),
                ProviderGauge(providerName, statistics.Pressure.CheckpointLag),
                ResourcePoolWaiterMeasurements(resourcePools),
                ResourcePoolTicketMeasurements(resourcePools)));
    }

    private sealed record FleetGaugeSnapshot(
        Measurement<long>[] ActiveInstances,
        Measurement<long>[] StuckInstances,
        Measurement<long>[] ActiveWaits,
        Measurement<long>[] OutboxStates,
        Measurement<long>[] StreamEvents,
        Measurement<long>[] CheckpointCounts,
        Measurement<long>[] CheckpointLag,
        Measurement<long>[] ResourcePoolWaiters,
        Measurement<long>[] ResourcePoolTickets)
    {
        internal static readonly FleetGaugeSnapshot Empty =
            new([], [], [], [], [], [], [], [], []);
    }

    private static Measurement<long>[] ActiveInstanceMeasurements(WorkflowStatistics statistics)
    {
        return statistics.Groups
            .Where(group => IsActive(group.Status))
            .Select(group =>
            {
                var tags = new TagList
                {
                    { OrcaCoreDiagnostics.ExecutionModeKey, OrcaCoreDiagnostics.DurableExecutionMode },
                    { OrcaCoreDiagnostics.DefinitionIdKey, group.DefinitionId.ToString() },
                    { OrcaCoreDiagnostics.DefinitionVersionKey, group.DefinitionVersion.ToString() },
                    { OrcaCoreDiagnostics.StatusKey, group.Status.ToString() }
                };

                return new Measurement<long>(group.Count, tags);
            })
            .ToArray();
    }

    private static Measurement<long>[] StuckInstanceMeasurements(IReadOnlyList<WorkflowInstanceSnapshot> instances)
    {
        return instances
            .Where(instance => instance.IsStuck || instance.HasStuckStep)
            .GroupBy(instance => instance.DefinitionId)
            .Select(group =>
            {
                var tags = new TagList
                {
                    { OrcaCoreDiagnostics.ExecutionModeKey, OrcaCoreDiagnostics.DurableExecutionMode },
                    { OrcaCoreDiagnostics.DefinitionIdKey, group.Key.ToString() }
                };
                return new Measurement<long>(group.LongCount(), tags);
            })
            .ToArray();
    }

    private static Measurement<long>[] ActiveWaitMeasurements(IReadOnlyList<WorkflowInstanceSnapshot> instances)
    {
        return instances
            .SelectMany(instance => instance.ActiveWaits.Select(wait => new
            {
                instance.DefinitionId,
                wait.EventName
            }))
            .GroupBy(wait => new { wait.DefinitionId, wait.EventName })
            .Select(group =>
            {
                var tags = new TagList
                {
                    { OrcaCoreDiagnostics.ExecutionModeKey, OrcaCoreDiagnostics.DurableExecutionMode },
                    { OrcaCoreDiagnostics.WaitEventNameKey, group.Key.EventName },
                    { OrcaCoreDiagnostics.DefinitionIdKey, group.Key.DefinitionId.ToString() }
                };
                return new Measurement<long>(group.LongCount(), tags);
            })
            .ToArray();
    }

    private static Measurement<long>[] OutboxStateMeasurements(WorkflowStatistics statistics)
    {
        return
        [
            OutboxStateMeasurement("pending", statistics.Pressure.OutboxPendingCount),
            OutboxStateMeasurement("retryable", statistics.Pressure.OutboxRetryableCount),
            OutboxStateMeasurement("claimed", statistics.Pressure.OutboxClaimedCount)
        ];
    }

    private static Measurement<long> OutboxStateMeasurement(string state, long value)
    {
        var tags = new TagList
        {
            { OrcaCoreDiagnostics.OutboxStateKey, state }
        };
        return new Measurement<long>(value, tags);
    }

    private static Measurement<long>[] ProviderGauge(string providerName, long value)
    {
        var tags = new TagList
        {
            { OrcaCoreDiagnostics.ProviderNameKey, providerName }
        };
        return [new Measurement<long>(value, tags)];
    }

    private static Measurement<long>[] ResourcePoolWaiterMeasurements(IReadOnlyList<ResourcePoolSnapshot> resourcePools)
    {
        return resourcePools
            .Select(pool =>
            {
                var tags = new TagList
                {
                    { OrcaCoreDiagnostics.ResourcePoolNameKey, pool.Name }
                };
                return new Measurement<long>(pool.QueuedWaiters.Count, tags);
            })
            .ToArray();
    }

    private static Measurement<long>[] ResourcePoolTicketMeasurements(IReadOnlyList<ResourcePoolSnapshot> resourcePools)
    {
        return resourcePools
            .Select(pool =>
            {
                var tags = new TagList
                {
                    { OrcaCoreDiagnostics.ResourcePoolNameKey, pool.Name }
                };
                return new Measurement<long>(pool.HeldTickets.Sum(ticket => ticket.Count), tags);
            })
            .ToArray();
    }

    private static TagList DurableModeTags()
    {
        return new TagList
        {
            { OrcaCoreDiagnostics.ExecutionModeKey, OrcaCoreDiagnostics.DurableExecutionMode }
        };
    }

    private static TagList CommandTags(
        string commandType,
        string outcome,
        DefinitionId? definitionId,
        DefinitionVersion? definitionVersion,
        WorkflowStatus? status)
    {
        var tags = DurableModeTags();
        tags.Add(OrcaCoreDiagnostics.CommandTypeKey, commandType);
        tags.Add(OrcaCoreDiagnostics.CommandOutcomeKey, outcome);

        if (definitionId is not null)
        {
            tags.Add(OrcaCoreDiagnostics.DefinitionIdKey, definitionId.Value.ToString());
        }

        if (definitionVersion is not null)
        {
            tags.Add(OrcaCoreDiagnostics.DefinitionVersionKey, definitionVersion.Value.ToString());
        }

        if (status is not null)
        {
            tags.Add(OrcaCoreDiagnostics.StatusKey, status.Value.ToString());
        }

        return tags;
    }

    private static TagList EventTags(string eventType, DefinitionId? definitionId)
    {
        var tags = DurableModeTags();
        tags.Add(OrcaCoreDiagnostics.EventTypeKey, eventType);
        if (definitionId is not null)
        {
            tags.Add(OrcaCoreDiagnostics.DefinitionIdKey, definitionId.Value.ToString());
        }

        return tags;
    }

    private static TagList StepTags(DefinitionId? definitionId, string stepPath)
    {
        var tags = DurableModeTags();
        tags.Add(OrcaCoreDiagnostics.StepPathKey, stepPath);
        if (definitionId is not null)
        {
            tags.Add(OrcaCoreDiagnostics.DefinitionIdKey, definitionId.Value.ToString());
        }

        return tags;
    }

    private static TagList LifecycleTags(string eventName, bool durable)
    {
        return new TagList
        {
            { OrcaCoreDiagnostics.LifecycleEventNameKey, eventName },
            { OrcaCoreDiagnostics.DurableKey, durable }
        };
    }

    private static TagList WaitTags(string waitEventName, DefinitionId? definitionId)
    {
        var tags = new TagList
        {
            { OrcaCoreDiagnostics.WaitEventNameKey, waitEventName }
        };
        if (definitionId is not null)
        {
            tags.Add(OrcaCoreDiagnostics.DefinitionIdKey, definitionId.Value.ToString());
        }

        return tags;
    }

    private static TagList ProviderCommitTags(string providerName, string operation)
    {
        return new TagList
        {
            { OrcaCoreDiagnostics.ProviderNameKey, providerName },
            { OrcaCoreDiagnostics.ProviderOperationKey, operation }
        };
    }

    private static bool IsActive(WorkflowStatus status)
    {
        return status is WorkflowStatus.Running
            or WorkflowStatus.Waiting
            or WorkflowStatus.Paused;
    }
}
