using System.Diagnostics;
using System.Diagnostics.Metrics;
using OrcaCore.Abstractions.Diagnostics;
using OrcaCore.Abstractions.Ids;
using OrcaCore.Abstractions.Instances;
using OrcaCore.Abstractions.Providers;
using OrcaCore.Engine.Durable.Diagnostics;

namespace OrcaCore.Hosting.Telemetry;

internal sealed class OrcaCoreTelemetryInstruments
{
    private Measurement<long>[] activeInstanceMeasurements = [];

    private readonly Counter<long> commandsProcessed =
        OrcaCoreDurableDiagnostics.Meter.CreateCounter<long>(OrcaCoreMetrics.CommandsProcessedName);

    private readonly Histogram<double> commandsDuration =
        OrcaCoreDurableDiagnostics.Meter.CreateHistogram<double>(OrcaCoreMetrics.CommandsDurationName, "s");

    private readonly Counter<long> outboxDispatched =
        OrcaCoreDurableDiagnostics.Meter.CreateCounter<long>(OrcaCoreMetrics.OutboxDispatchedName);

    private readonly Histogram<double> outboxDispatchDuration =
        OrcaCoreDurableDiagnostics.Meter.CreateHistogram<double>(
            OrcaCoreMetrics.OutboxDispatchDurationName,
            "s");

    private readonly ObservableGauge<long> activeInstances;

    public OrcaCoreTelemetryInstruments()
    {
        activeInstances = OrcaCoreDurableDiagnostics.Meter.CreateObservableGauge<long>(
            OrcaCoreMetrics.InstancesActiveName,
            ObserveActiveInstances);
    }

    public void RecordCommandProcessed(
        string commandType,
        string outcome,
        DefinitionId? definitionId,
        DefinitionVersion? definitionVersion,
        WorkflowStatus? status,
        TimeSpan duration)
    {
        _ = activeInstances;
        var tags = CommandTags(commandType, outcome, definitionId, definitionVersion, status);
        commandsProcessed.Add(1, tags);
        commandsDuration.Record(duration.TotalSeconds, tags);
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

    public void UpdateActiveInstances(WorkflowStatistics statistics)
    {
        ArgumentNullException.ThrowIfNull(statistics);

        var measurements = statistics.Groups
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

        Volatile.Write(ref activeInstanceMeasurements, measurements);
    }

    private IEnumerable<Measurement<long>> ObserveActiveInstances()
    {
        return Volatile.Read(ref activeInstanceMeasurements);
    }

    private static TagList CommandTags(
        string commandType,
        string outcome,
        DefinitionId? definitionId,
        DefinitionVersion? definitionVersion,
        WorkflowStatus? status)
    {
        var tags = new TagList
        {
            { OrcaCoreDiagnostics.ExecutionModeKey, OrcaCoreDiagnostics.DurableExecutionMode },
            { OrcaCoreDiagnostics.CommandTypeKey, commandType },
            { OrcaCoreDiagnostics.CommandOutcomeKey, outcome }
        };

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

    private static bool IsActive(WorkflowStatus status)
    {
        return status is WorkflowStatus.Running
            or WorkflowStatus.Waiting
            or WorkflowStatus.Paused;
    }
}
