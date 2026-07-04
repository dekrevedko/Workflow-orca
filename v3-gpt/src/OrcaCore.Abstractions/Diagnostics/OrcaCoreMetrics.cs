using System.Collections.Concurrent;
using System.Diagnostics;
using System.Diagnostics.Metrics;
using OrcaCore.Abstractions.Ids;
using OrcaCore.Abstractions.Instances;

namespace OrcaCore.Abstractions.Diagnostics;

/// <summary>
/// Records OrcaCore runtime metrics through BCL instruments.
/// </summary>
public static class OrcaCoreMetrics
{
    public const string CommandsProcessedName = "orca.commands.processed";
    public const string CommandsDurationName = "orca.commands.duration";
    public const string OutboxDispatchedName = "orca.outbox.dispatched";
    public const string InstancesActiveName = "orca.instances.active";

    private static readonly ConcurrentDictionary<InstanceId, ActiveInstanceMetric> ActiveInstances = new();

    private static readonly Counter<long> CommandsProcessed =
        OrcaCoreDiagnostics.Meter.CreateCounter<long>(CommandsProcessedName);

    private static readonly Histogram<double> CommandsDuration =
        OrcaCoreDiagnostics.Meter.CreateHistogram<double>(CommandsDurationName, "s");

    private static readonly Counter<long> OutboxDispatched =
        OrcaCoreDiagnostics.Meter.CreateCounter<long>(OutboxDispatchedName);

    private static readonly ObservableGauge<long> ActiveInstancesGauge =
        OrcaCoreDiagnostics.Meter.CreateObservableGauge(
            InstancesActiveName,
            ObserveActiveInstances);

    public static void RecordCommandProcessed(
        InstanceId instanceId,
        string commandType,
        string outcome,
        DefinitionId? definitionId,
        DefinitionVersion? definitionVersion,
        WorkflowStatus? status,
        TimeSpan duration)
    {
        var tags = CommandTags(commandType, outcome, definitionId, definitionVersion, status);
        CommandsProcessed.Add(1, tags);
        CommandsDuration.Record(duration.TotalSeconds, tags);
        RecordInstanceState(instanceId, definitionId, definitionVersion, status);
    }

    public static void RecordOutboxDispatches(
        int successCount,
        int retryableFailureCount,
        int permanentFailureCount)
    {
        if (successCount > 0)
        {
            OutboxDispatched.Add(successCount, DispatchTags("success"));
        }

        if (retryableFailureCount > 0)
        {
            OutboxDispatched.Add(retryableFailureCount, DispatchTags("retryable"));
        }

        if (permanentFailureCount > 0)
        {
            OutboxDispatched.Add(permanentFailureCount, DispatchTags("permanent"));
        }
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
            { "orca.execution.mode", "durable" },
            { "orca.command.type", commandType },
            { "outcome", outcome }
        };

        if (definitionId is not null)
        {
            tags.Add("orca.definition.id", definitionId.Value.ToString());
        }

        if (definitionVersion is not null)
        {
            tags.Add("orca.definition.version", definitionVersion.Value.ToString());
        }

        if (status is not null)
        {
            tags.Add("orca.status", status.Value.ToString());
        }

        return tags;
    }

    private static TagList DispatchTags(string result)
    {
        return new TagList
        {
            { "result", result }
        };
    }

    private static void RecordInstanceState(
        InstanceId instanceId,
        DefinitionId? definitionId,
        DefinitionVersion? definitionVersion,
        WorkflowStatus? status)
    {
        if (definitionId is null || definitionVersion is null || status is null)
        {
            return;
        }

        if (IsTerminal(status.Value))
        {
            ActiveInstances.TryRemove(instanceId, out _);
            return;
        }

        ActiveInstances[instanceId] = new ActiveInstanceMetric(
            definitionId.Value,
            definitionVersion.Value,
            status.Value);
    }

    private static IEnumerable<Measurement<long>> ObserveActiveInstances()
    {
        foreach (var group in ActiveInstances.Values
                     .GroupBy(metric => new
                     {
                         metric.DefinitionId,
                         metric.DefinitionVersion,
                         metric.Status
                     }))
        {
            var tags = new TagList
            {
                { "orca.execution.mode", "durable" },
                { "orca.definition.id", group.Key.DefinitionId.ToString() },
                { "orca.definition.version", group.Key.DefinitionVersion.ToString() },
                { "orca.status", group.Key.Status.ToString() }
            };
            yield return new Measurement<long>(group.LongCount(), tags);
        }
    }

    private static bool IsTerminal(WorkflowStatus status)
    {
        return status is WorkflowStatus.Completed
            or WorkflowStatus.Failed
            or WorkflowStatus.Cancelled
            or WorkflowStatus.Terminated
            or WorkflowStatus.Compensated
            or WorkflowStatus.CompensationFailed;
    }

    private sealed record ActiveInstanceMetric(
        DefinitionId DefinitionId,
        DefinitionVersion DefinitionVersion,
        WorkflowStatus Status);
}
