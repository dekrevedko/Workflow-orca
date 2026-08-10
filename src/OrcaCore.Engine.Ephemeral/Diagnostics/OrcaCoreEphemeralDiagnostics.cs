using System.Diagnostics;
using System.Diagnostics.Metrics;
using OrcaCore.Abstractions.Diagnostics;
using OrcaCore.Engine.Ephemeral.Execution;

namespace OrcaCore.Engine.Ephemeral.Diagnostics;

/// <summary>Owns ephemeral BCL diagnostics and grouped host/operator statistics.</summary>
internal static class OrcaCoreEphemeralDiagnostics
{
    private static EphemeralGaugeSnapshot gauges = EphemeralGaugeSnapshot.Empty;

    internal const string SourceName = OrcaCoreDiagnostics.EphemeralSourceName;

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

    private static readonly Histogram<double> CommandsDuration =
        Meter.CreateHistogram<double>(OrcaCoreMetrics.CommandsDuration, unit: OrcaCoreDiagnostics.SecondsUnit);
    private static readonly Histogram<double> StepsDuration =
        Meter.CreateHistogram<double>(OrcaCoreMetrics.StepsDuration, unit: OrcaCoreDiagnostics.SecondsUnit);
    private static readonly Histogram<double> ProviderCommitDuration =
        Meter.CreateHistogram<double>(OrcaCoreMetrics.ProviderCommitDuration, unit: OrcaCoreDiagnostics.SecondsUnit);
    private static readonly Histogram<double> OutboxDispatchDuration =
        Meter.CreateHistogram<double>(OrcaCoreMetrics.OutboxDispatchDuration, unit: OrcaCoreDiagnostics.SecondsUnit);
    private static readonly Histogram<double> WaitsDuration =
        Meter.CreateHistogram<double>(OrcaCoreMetrics.WaitsDuration, unit: OrcaCoreDiagnostics.SecondsUnit);

    private static readonly ObservableGauge<long> ActiveInstances = Meter.CreateObservableGauge(
        OrcaCoreMetrics.InstancesActive,
        () => Volatile.Read(ref gauges).ActiveInstances);
    private static readonly ObservableGauge<long> StuckInstances = Meter.CreateObservableGauge(
        OrcaCoreMetrics.InstancesStuck,
        () => Volatile.Read(ref gauges).StuckInstances);
    private static readonly ObservableGauge<long> ActiveWaits = Meter.CreateObservableGauge(
        OrcaCoreMetrics.WaitsActive,
        () => Volatile.Read(ref gauges).ActiveWaits);

    internal static Activity? StartOperation(string operationName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(operationName);
        var activityName = operationName switch
        {
            OrcaCoreDiagnostics.StartOperation => OrcaCoreDiagnostics.CommandProcessActivity,
            OrcaCoreDiagnostics.RaiseEventOperation => OrcaCoreDiagnostics.EventApplyActivity,
            OrcaCoreDiagnostics.FireDueTimersOperation => OrcaCoreDiagnostics.StepExecuteActivity,
            _ => OrcaCoreDiagnostics.CommandProcessActivity
        };
        var activity = ActivitySource.StartActivity(activityName, ActivityKind.Internal);
        activity?.SetTag(OrcaCoreDiagnostics.ExecutionModeKey, OrcaCoreDiagnostics.EphemeralExecutionMode);
        activity?.SetTag(OrcaCoreDiagnostics.CommandTypeKey, operationName);
        return activity;
    }

    internal static void RefreshStatistics(IEnumerable<EphemeralWorkflowInstanceSnapshot> snapshots)
    {
        ArgumentNullException.ThrowIfNull(snapshots);
        _ = ActiveInstances;
        _ = StuckInstances;
        _ = ActiveWaits;
        var materialized = snapshots.ToArray();
        Volatile.Write(
            ref gauges,
            new EphemeralGaugeSnapshot(
                GroupMeasurements(materialized),
                materialized
                    .Where(snapshot => snapshot.IsStuck || snapshot.HasStuckStep)
                    .GroupBy(snapshot => snapshot.DefinitionId)
                    .Select(group => Measurement(group.LongCount(), group.Key, status: null))
                    .ToArray(),
                materialized
                    .SelectMany(snapshot => snapshot.ActiveWaits.Select(wait => new
                    {
                        snapshot.DefinitionId,
                        wait.EventContract.EventName
                    }))
                    .GroupBy(wait => new { wait.DefinitionId, wait.EventName })
                    .Select(group => new Measurement<long>(
                        group.LongCount(),
                        new KeyValuePair<string, object?>(
                            OrcaCoreDiagnostics.ExecutionModeKey,
                            OrcaCoreDiagnostics.EphemeralExecutionMode),
                        new KeyValuePair<string, object?>(
                            OrcaCoreDiagnostics.DefinitionIdKey,
                            group.Key.DefinitionId.ToString()),
                        new KeyValuePair<string, object?>(
                            OrcaCoreDiagnostics.WaitEventNameKey,
                            group.Key.EventName.Value)))
                    .ToArray()));
    }

    internal static EphemeralOperatorStatistics CaptureStatistics(
        IEnumerable<EphemeralWorkflowInstanceSnapshot> snapshots)
    {
        ArgumentNullException.ThrowIfNull(snapshots);
        var materialized = snapshots.ToArray();
        return new EphemeralOperatorStatistics(
            materialized
                .GroupBy(snapshot => new
                {
                    snapshot.DefinitionId,
                    snapshot.DefinitionVersion,
                    snapshot.Status
                })
                .OrderBy(group => group.Key.DefinitionId.Value)
                .ThenBy(group => group.Key.DefinitionVersion.Value)
                .ThenBy(group => group.Key.Status)
                .Select(group => new EphemeralOperatorStatisticsGroup(
                    group.Key.DefinitionId,
                    group.Key.DefinitionVersion,
                    group.Key.Status,
                    group.LongCount()))
                .ToArray(),
            materialized.LongCount(snapshot => IsActive(snapshot.Status)),
            materialized.LongCount(snapshot => snapshot.IsStuck || snapshot.HasStuckStep),
            materialized.Sum(snapshot => (long)snapshot.ActiveWaits.Count));
    }

    internal static void RecordCommand(
        global::OrcaCore.DefinitionId definitionId,
        string commandType,
        global::OrcaCore.WorkflowInstanceStatus status,
        TimeSpan duration)
    {
        ArgumentNullException.ThrowIfNull(definitionId);
        ArgumentException.ThrowIfNullOrWhiteSpace(commandType);
        var tags = CommandTags(definitionId, commandType, status);
        CommandsProcessed.Add(1, tags);
        CommandsDuration.Record(duration.TotalSeconds, tags);
    }

    internal static void RecordWorkflowStarted(global::OrcaCore.DefinitionId definitionId)
    {
        LifecycleEvents.Add(
            1,
            new KeyValuePair<string, object?>(
                OrcaCoreDiagnostics.LifecycleEventNameKey,
                OrcaCoreDiagnostics.InstanceStartedLifecycleEvent),
            new KeyValuePair<string, object?>(OrcaCoreDiagnostics.DurableKey, false),
            new KeyValuePair<string, object?>(OrcaCoreDiagnostics.DefinitionIdKey, definitionId.ToString()));
    }

    internal static void RecordEventDelivered(
        global::OrcaCore.DefinitionId definitionId,
        global::OrcaCore.EventName eventName,
        global::OrcaCore.WorkflowInstanceStatus status)
    {
        EventsApplied.Add(
            1,
            new KeyValuePair<string, object?>(
                OrcaCoreDiagnostics.ExecutionModeKey,
                OrcaCoreDiagnostics.EphemeralExecutionMode),
            new KeyValuePair<string, object?>(OrcaCoreDiagnostics.EventTypeKey, eventName.Value),
            new KeyValuePair<string, object?>(OrcaCoreDiagnostics.DefinitionIdKey, definitionId.ToString()),
            new KeyValuePair<string, object?>(OrcaCoreDiagnostics.StatusKey, status.ToString()));
    }

    internal static void RecordWaitMatched(
        global::OrcaCore.DefinitionId definitionId,
        global::OrcaCore.EventName eventName,
        TimeSpan duration) =>
        WaitsDuration.Record(
            duration.TotalSeconds,
            new KeyValuePair<string, object?>(
                OrcaCoreDiagnostics.ExecutionModeKey,
                OrcaCoreDiagnostics.EphemeralExecutionMode),
            new KeyValuePair<string, object?>(OrcaCoreDiagnostics.DefinitionIdKey, definitionId.ToString()),
            new KeyValuePair<string, object?>(OrcaCoreDiagnostics.WaitEventNameKey, eventName.Value));

    internal static Activity? StartStep(
        global::OrcaCore.DefinitionId definitionId,
        string stepPath)
    {
        var activity = ActivitySource.StartActivity(OrcaCoreDiagnostics.StepExecuteActivity, ActivityKind.Internal);
        activity?.SetTag(OrcaCoreDiagnostics.ExecutionModeKey, OrcaCoreDiagnostics.EphemeralExecutionMode);
        activity?.SetTag(OrcaCoreDiagnostics.DefinitionIdKey, definitionId.ToString());
        activity?.SetTag(OrcaCoreDiagnostics.StepPathKey, stepPath);
        return activity;
    }

    internal static void RecordStep(
        global::OrcaCore.DefinitionId definitionId,
        string stepPath,
        TimeSpan duration,
        string? errorKind)
    {
        var tags = new TagList
        {
            { OrcaCoreDiagnostics.ExecutionModeKey, OrcaCoreDiagnostics.EphemeralExecutionMode },
            { OrcaCoreDiagnostics.DefinitionIdKey, definitionId.ToString() },
            { OrcaCoreDiagnostics.StepPathKey, stepPath }
        };
        StepsDuration.Record(duration.TotalSeconds, tags);
        if (errorKind is null)
        {
            StepsCompleted.Add(1, tags);
            return;
        }

        tags.Add(OrcaCoreDiagnostics.ErrorKindKey, errorKind);
        StepsFailed.Add(1, tags);
    }

    internal static void RecordTerminalLifecycle(
        global::OrcaCore.DefinitionId definitionId,
        global::OrcaCore.WorkflowInstanceStatus status) =>
        LifecycleEvents.Add(
            1,
            new KeyValuePair<string, object?>(OrcaCoreDiagnostics.LifecycleEventNameKey, status.ToString()),
            new KeyValuePair<string, object?>(OrcaCoreDiagnostics.DurableKey, false),
            new KeyValuePair<string, object?>(OrcaCoreDiagnostics.DefinitionIdKey, definitionId.ToString()));

    internal static void RecordHostCompatibilityFailure(string reason)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(reason);
        StepsFailed.Add(
            1,
            new KeyValuePair<string, object?>(
                OrcaCoreDiagnostics.ExecutionModeKey,
                OrcaCoreDiagnostics.EphemeralExecutionMode),
            new KeyValuePair<string, object?>(OrcaCoreDiagnostics.ErrorKindKey, reason));
    }

    private static Measurement<long>[] GroupMeasurements(
        IReadOnlyList<EphemeralWorkflowInstanceSnapshot> snapshots) =>
        snapshots
            .Where(snapshot => IsActive(snapshot.Status))
            .GroupBy(snapshot => new
            {
                snapshot.DefinitionId,
                snapshot.DefinitionVersion,
                snapshot.Status
            })
            .Select(group => new Measurement<long>(
                group.LongCount(),
                new KeyValuePair<string, object?>(
                    OrcaCoreDiagnostics.ExecutionModeKey,
                    OrcaCoreDiagnostics.EphemeralExecutionMode),
                new KeyValuePair<string, object?>(
                    OrcaCoreDiagnostics.DefinitionIdKey,
                    group.Key.DefinitionId.ToString()),
                new KeyValuePair<string, object?>(
                    OrcaCoreDiagnostics.DefinitionVersionKey,
                    group.Key.DefinitionVersion.ToString()),
                new KeyValuePair<string, object?>(OrcaCoreDiagnostics.StatusKey, group.Key.Status.ToString())))
            .ToArray();

    private static Measurement<long> Measurement(
        long value,
        global::OrcaCore.DefinitionId definitionId,
        global::OrcaCore.WorkflowInstanceStatus? status) =>
        status is { } current
            ? new Measurement<long>(
                value,
                new KeyValuePair<string, object?>(
                    OrcaCoreDiagnostics.ExecutionModeKey,
                    OrcaCoreDiagnostics.EphemeralExecutionMode),
                new KeyValuePair<string, object?>(OrcaCoreDiagnostics.DefinitionIdKey, definitionId.ToString()),
                new KeyValuePair<string, object?>(OrcaCoreDiagnostics.StatusKey, current.ToString()))
            : new Measurement<long>(
                value,
                new KeyValuePair<string, object?>(
                    OrcaCoreDiagnostics.ExecutionModeKey,
                    OrcaCoreDiagnostics.EphemeralExecutionMode),
                new KeyValuePair<string, object?>(OrcaCoreDiagnostics.DefinitionIdKey, definitionId.ToString()));

    private static TagList CommandTags(
        global::OrcaCore.DefinitionId definitionId,
        string commandType,
        global::OrcaCore.WorkflowInstanceStatus status) =>
        new()
        {
            { OrcaCoreDiagnostics.ExecutionModeKey, OrcaCoreDiagnostics.EphemeralExecutionMode },
            { OrcaCoreDiagnostics.CommandTypeKey, commandType },
            { OrcaCoreDiagnostics.DefinitionIdKey, definitionId.ToString() },
            { OrcaCoreDiagnostics.CommandOutcomeKey, status.ToString() },
            { OrcaCoreDiagnostics.StatusKey, status.ToString() }
        };

    private static bool IsActive(global::OrcaCore.WorkflowInstanceStatus status) =>
        status is global::OrcaCore.WorkflowInstanceStatus.Pending or
            global::OrcaCore.WorkflowInstanceStatus.Running or
            global::OrcaCore.WorkflowInstanceStatus.Waiting or
            global::OrcaCore.WorkflowInstanceStatus.CancellationRequested;

    private sealed record EphemeralGaugeSnapshot(
        Measurement<long>[] ActiveInstances,
        Measurement<long>[] StuckInstances,
        Measurement<long>[] ActiveWaits)
    {
        internal static EphemeralGaugeSnapshot Empty { get; } = new([], [], []);
    }
}

internal sealed record EphemeralOperatorStatisticsGroup(
    global::OrcaCore.DefinitionId DefinitionId,
    global::OrcaCore.DefinitionVersion DefinitionVersion,
    global::OrcaCore.WorkflowInstanceStatus Status,
    long Count);

internal sealed record EphemeralOperatorStatistics(
    IReadOnlyList<EphemeralOperatorStatisticsGroup> Groups,
    long ActiveInstanceCount,
    long StuckInstanceCount,
    long ActiveWaitCount);
