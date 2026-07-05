using Microsoft.Extensions.Logging;
using OrcaCore.Abstractions.Diagnostics;
using OrcaCore.Abstractions.Durable;
using OrcaCore.Abstractions.Instances;
using OrcaCore.Engine.Durable.Management;
using OrcaCore.Dashboard.Telemetry;

namespace OrcaCore.Dashboard.Workflows;

public sealed class DashboardReadModel(
    DurableManagement management,
    DashboardTelemetryStore telemetry,
    TimeProvider timeProvider)
{
    public async Task<DashboardSnapshot> GetSnapshotAsync(CancellationToken cancellationToken)
    {
        telemetry.CollectObservableMetrics();

        var generatedAt = timeProvider.GetUtcNow();
        var instances = await management.All().ListAsync(cancellationToken).ConfigureAwait(false);
        var statistics = await management.All().StatisticsAsync(cancellationToken).ConfigureAwait(false);
        var metrics = telemetry.GetMetrics();
        var logs = telemetry.GetLogs();
        var spans = telemetry.GetSpans();

        var workflows = new List<WorkflowView>();
        foreach (var instance in instances
                     .OrderByDescending(instance => instance.UpdatedAt)
                     .ThenBy(instance => instance.InstanceId.Value))
        {
            var history = await management
                .GetHistoryAsync(instance.InstanceId, cancellationToken)
                .ConfigureAwait(false);
            workflows.Add(ToWorkflowView(instance, history));
        }

        var metricSeries = CreateMetricSeries(metrics);
        var issues = CreateIssues(workflows, statistics, metrics, logs, generatedAt);
        var tiles = CreateTiles(statistics, workflows, metrics, logs);
        var status = issues.Any(issue => issue.Severity is IssueSeverity.Critical or IssueSeverity.Error)
            ? SystemStatus.Critical
            : issues.Any(issue => issue.Severity == IssueSeverity.Warning)
                ? SystemStatus.Degraded
                : SystemStatus.Healthy;

        return new DashboardSnapshot(
            status,
            tiles,
            issues,
            workflows,
            metricSeries,
            logs.OrderByDescending(log => log.Timestamp).Take(50).ToArray(),
            spans.OrderByDescending(span => span.StartedAt).Take(50).ToArray(),
            generatedAt);
    }

    private static WorkflowView ToWorkflowView(
        WorkflowInstanceSnapshot instance,
        IReadOnlyList<WorkflowEvent> history)
    {
        var steps = history
            .OrderBy(workflowEvent => workflowEvent.OccurredAt)
            .ThenBy(workflowEvent => workflowEvent.EventId.Value)
            .Select(ToStepView)
            .ToArray();
        var duration = IsTerminal(instance.Status)
            ? instance.UpdatedAt - instance.CreatedAt
            : DateTimeOffset.UtcNow - instance.CreatedAt;

        return new WorkflowView(
            instance.InstanceId.ToString(),
            instance.DefinitionId.ToString(),
            instance.DefinitionVersion.Value,
            instance.Status.ToString(),
            StatusSeverity(instance.Status, instance.ErrorSummary),
            instance.CreatedAt,
            instance.UpdatedAt,
            duration,
            instance.ErrorSummary,
            instance.ActiveWaits.Select(ToWaitView).ToArray(),
            steps);
    }

    private static ActiveWaitView ToWaitView(ActiveWaitSnapshot wait)
    {
        return new ActiveWaitView(
            wait.WaitId.ToString(),
            wait.EventName,
            wait.CorrelationId.ToString(),
            wait.RegisteredAt,
            wait.Status,
            wait.Mode,
            wait.BranchId);
    }

    private static WorkflowStepView ToStepView(WorkflowEvent workflowEvent)
    {
        return workflowEvent switch
        {
            WorkflowStartedEvent started => Step(
                started,
                "Instance started",
                "Running",
                IssueSeverity.Info,
                $"Definition {started.DefinitionId} v{started.DefinitionVersion.Value}",
                null),
            WorkflowStepCompletedEvent completed => Step(
                completed,
                completed.StepPath,
                "Completed",
                IssueSeverity.Info,
                "Step committed successfully.",
                completed.StepPath),
            WorkflowStepFailedEvent failed => Step(
                failed,
                failed.StepPath,
                "Failed",
                IssueSeverity.Error,
                failed.ErrorSummary,
                failed.StepPath),
            WorkflowWaitRegisteredEvent registered => Step(
                registered,
                registered.EventName,
                "Waiting",
                IssueSeverity.Warning,
                $"Waiting for correlation {registered.CorrelationId} in {registered.Mode} mode.",
                registered.BranchId),
            WorkflowWaitMatchedEvent matched => Step(
                matched,
                "Wait matched",
                "Running",
                IssueSeverity.Info,
                $"Matched inbound event {matched.MatchedEventId}.",
                matched.WaitId.ToString()),
            WorkflowTimerScheduledEvent scheduled => Step(
                scheduled,
                scheduled.WakeupName,
                "Timer scheduled",
                IssueSeverity.Info,
                $"Due at {scheduled.FireAt:u}.",
                scheduled.TimerId.ToString()),
            WorkflowTimerFiredEvent fired => Step(
                fired,
                "Timer fired",
                "Running",
                IssueSeverity.Info,
                "Timer wake-up accepted.",
                fired.TimerId.ToString()),
            WorkflowExternalJobStartedEvent started => Step(
                started,
                started.ExternalJobId,
                "External job dispatched",
                IssueSeverity.Info,
                started.TimeoutAt is null
                    ? "External work is waiting for completion."
                    : $"External work is waiting; timeout at {started.TimeoutAt:u}.",
                started.WaitId.ToString()),
            WorkflowExternalJobCompletedEvent completed => Step(
                completed,
                completed.ExternalJobId,
                "External job completed",
                IssueSeverity.Info,
                $"Completion event {completed.CompletionEventId} resumed the workflow.",
                completed.ExternalJobId),
            WorkflowExternalJobTimedOutEvent timedOut => Step(
                timedOut,
                timedOut.ExternalJobId,
                "External job timed out",
                IssueSeverity.Error,
                "The external job exceeded its timeout and forced failure handling.",
                timedOut.ExternalJobId),
            WorkflowExternalJobStopRequestedEvent stop => Step(
                stop,
                stop.ExternalJobId,
                "Stop requested",
                IssueSeverity.Warning,
                "Durable stop intent was recorded for external work.",
                stop.ExternalJobId),
            WorkflowTerminalEvent terminal => Step(
                terminal,
                "Terminal status",
                terminal.Status.ToString(),
                StatusSeverity(terminal.Status, null),
                $"Instance reached {terminal.Status}.",
                null),
            WorkflowCompletedEvent completed => Step(
                completed,
                "Instance completed",
                "Completed",
                IssueSeverity.Info,
                completed.OutcomeName is null
                    ? "Workflow completed."
                    : $"Workflow completed with outcome {completed.OutcomeName}.",
                completed.OutcomeName),
            WorkflowPausedEvent paused => Step(
                paused,
                "Instance paused",
                "Paused",
                IssueSeverity.Warning,
                "Workflow paused at a safe boundary.",
                null),
            WorkflowResumedEvent resumed => Step(
                resumed,
                "Instance resumed",
                "Running",
                IssueSeverity.Info,
                $"Buffered delivery handling: {resumed.BufferHandling}.",
                null),
            SagaCompensationFailedEvent failed => Step(
                failed,
                failed.ActionKey,
                "Compensation failed",
                IssueSeverity.Critical,
                failed.ErrorSummary,
                failed.ScopeId),
            _ => Step(
                workflowEvent,
                workflowEvent.GetType().Name.Replace("Event", string.Empty, StringComparison.Ordinal),
                "Recorded",
                IssueSeverity.Info,
                "Durable event committed.",
                null)
        };
    }

    private static WorkflowStepView Step(
        WorkflowEvent workflowEvent,
        string name,
        string status,
        IssueSeverity severity,
        string detail,
        string? correlation)
    {
        return new WorkflowStepView(
            workflowEvent.OccurredAt,
            name,
            status,
            severity,
            detail,
            workflowEvent.EventId.ToString(),
            workflowEvent.CommandId.ToString(),
            correlation);
    }

    private static IReadOnlyList<IssueView> CreateIssues(
        IReadOnlyList<WorkflowView> workflows,
        WorkflowStatistics statistics,
        IReadOnlyList<DashboardTelemetryStore.DashboardMetricPoint> metrics,
        IReadOnlyList<DashboardTelemetryStore.DashboardLogEntry> logs,
        DateTimeOffset generatedAt)
    {
        var issues = new List<IssueView>();

        foreach (var workflow in workflows.Where(workflow =>
                     workflow.Severity is IssueSeverity.Critical or IssueSeverity.Error).Take(10))
        {
            var title = workflow.Status == WorkflowStatus.CompensationFailed.ToString()
                ? "Compensation requires operator recovery"
                : "Workflow entered a failed terminal state";
            issues.Add(new IssueView(
                workflow.Severity,
                "Workflow",
                title,
                $"Instance {Shorten(workflow.InstanceId)} is {workflow.Status}.",
                workflow.ErrorSummary ?? LatestProblemDetail(workflow),
                "Open the workflow timeline, inspect the failing step, then replay, compensate, or recover from the owning runbook.",
                workflow.InstanceId,
                workflow.UpdatedAt));
        }

        foreach (var workflow in workflows.Where(workflow => workflow.ActiveWaits.Count > 0).Take(10))
        {
            var oldestWait = workflow.ActiveWaits.OrderBy(wait => wait.RegisteredAt).First();
            if (generatedAt - oldestWait.RegisteredAt > TimeSpan.FromMinutes(10))
            {
                issues.Add(new IssueView(
                    IssueSeverity.Warning,
                    "Workflow",
                    "Workflow is waiting longer than expected",
                    $"Instance {Shorten(workflow.InstanceId)} has been waiting for {Age(generatedAt - oldestWait.RegisteredAt)}.",
                    $"{oldestWait.EventName} with correlation {oldestWait.CorrelationId}.",
                    "Check the upstream producer or event bridge for the missing completion signal.",
                    workflow.InstanceId,
                    oldestWait.RegisteredAt));
            }
        }

        var permanentDispatches = SumMetric(metrics, OrcaCoreMetrics.OutboxDispatchedName, "result", "permanent");
        if (permanentDispatches > 0)
        {
            issues.Add(new IssueView(
                IssueSeverity.Critical,
                "Outbox",
                "Permanent dispatch failure recorded",
                $"{permanentDispatches:0} outbound message(s) were marked permanent failures.",
                "The outbox pump emitted result=permanent.",
                "Inspect destination credentials, schema compatibility, and poison-message handling before replaying.",
                "orca.outbox.dispatched:permanent",
                LatestMetric(metrics, OrcaCoreMetrics.OutboxDispatchedName, "result", "permanent")));
        }

        var retryableDispatches = SumMetric(metrics, OrcaCoreMetrics.OutboxDispatchedName, "result", "retryable");
        if (retryableDispatches > 0)
        {
            issues.Add(new IssueView(
                IssueSeverity.Warning,
                "Outbox",
                "Retryable dispatch failures observed",
                $"{retryableDispatches:0} outbound dispatch attempt(s) are retryable.",
                "The outbox pump emitted result=retryable.",
                "Watch retry pressure and check transient network or broker health if the count keeps rising.",
                "orca.outbox.dispatched:retryable",
                LatestMetric(metrics, OrcaCoreMetrics.OutboxDispatchedName, "result", "retryable")));
        }

        foreach (var log in logs
                     .Where(log => log.Level is LogLevel.Critical or LogLevel.Error)
                     .OrderByDescending(log => log.Timestamp)
                     .Take(10))
        {
            issues.Add(new IssueView(
                log.Level == LogLevel.Critical ? IssueSeverity.Critical : IssueSeverity.Error,
                "Log",
                log.EventId == 1102 ? "Outbox permanent failure log" : "Runtime error log",
                log.Message,
                log.ExceptionMessage ?? FormatProperties(log.Properties),
                "Use the trace id and structured properties to find the failing command or provider call.",
                Correlation(log),
                log.Timestamp));
        }

        if (statistics.Pressure.PendingOutboxCount > 0)
        {
            issues.Add(new IssueView(
                IssueSeverity.Warning,
                "Provider",
                "Outbox has pending work",
                $"{statistics.Pressure.PendingOutboxCount} outbox record(s) are waiting or retryable.",
                "Provider pressure reports pending outbox records.",
                "Confirm the outbox hosted service is running and downstream dispatch is healthy.",
                "provider.pressure.pending_outbox",
                generatedAt));
        }

        if (issues.Count == 0)
        {
            issues.Add(new IssueView(
                IssueSeverity.Info,
                "System",
                "No active incidents",
                "No failed workflows, error logs, or dispatch failures are visible in the current window.",
                "Telemetry and management projections agree.",
                "Keep watching command latency, active waits, and outbox pressure.",
                "dashboard",
                generatedAt));
        }

        return issues
            .OrderByDescending(issue => issue.Severity)
            .ThenByDescending(issue => issue.LastSeen)
            .Take(20)
            .ToArray();
    }

    private static IReadOnlyList<StatTile> CreateTiles(
        WorkflowStatistics statistics,
        IReadOnlyList<WorkflowView> workflows,
        IReadOnlyList<DashboardTelemetryStore.DashboardMetricPoint> metrics,
        IReadOnlyList<DashboardTelemetryStore.DashboardLogEntry> logs)
    {
        var commandDurations = metrics
            .Where(metric => metric.Name == OrcaCoreMetrics.CommandsDurationName)
            .Select(metric => metric.Value)
            .Order()
            .ToArray();
        var commandCount = SumMetric(metrics, OrcaCoreMetrics.CommandsProcessedName);
        var p95 = Percentile(commandDurations, 0.95);
        var failures = workflows.Count(workflow =>
            workflow.Severity is IssueSeverity.Critical or IssueSeverity.Error);
        var active = statistics.Pressure.ActiveInstanceCount;
        var errorLogs = logs.Count(log => log.Level >= LogLevel.Error);

        return
        [
            new StatTile(
                "System state",
                failures > 0 || errorLogs > 0 ? "Attention" : "Healthy",
                failures > 0
                    ? $"{failures} workflow issue(s)"
                    : $"{active} active instance(s)",
                failures > 0 || errorLogs > 0 ? TileState.Critical : TileState.Good),
            new StatTile(
                "Commands",
                commandCount.ToString("0"),
                commandDurations.Length == 0 ? "No command latency yet" : $"p95 {p95 * 1_000:0.#} ms",
                TileState.Neutral),
            new StatTile(
                "Active workflows",
                active.ToString(),
                $"{statistics.Pressure.PendingOutboxCount} pending outbox",
                statistics.Pressure.PendingOutboxCount > 0 ? TileState.Warning : TileState.Good),
            new StatTile(
                "Event pressure",
                statistics.Pressure.TotalStreamEvents.ToString(),
                $"{statistics.Pressure.CheckpointCount} checkpoint(s)",
                TileState.Neutral)
        ];
    }

    private static IReadOnlyList<MetricSeriesView> CreateMetricSeries(
        IReadOnlyList<DashboardTelemetryStore.DashboardMetricPoint> metrics)
    {
        return metrics
            .GroupBy(metric => new
            {
                metric.Name,
                Tags = string.Join(", ", metric.Tags.OrderBy(tag => tag.Key).Select(tag => $"{tag.Key}={tag.Value}"))
            })
            .Select(group => new MetricSeriesView(
                group.Key.Name,
                group.Key.Tags,
                group.Sum(metric => metric.Value),
                group.Count(),
                group.Max(metric => metric.ObservedAt)))
            .OrderBy(series => series.Name)
            .ThenBy(series => series.Tags)
            .ToArray();
    }

    private static IssueSeverity StatusSeverity(WorkflowStatus status, string? errorSummary)
    {
        return status switch
        {
            WorkflowStatus.CompensationFailed => IssueSeverity.Critical,
            WorkflowStatus.Failed or WorkflowStatus.Terminated => IssueSeverity.Error,
            WorkflowStatus.Cancelled or WorkflowStatus.Paused => IssueSeverity.Warning,
            _ when !string.IsNullOrWhiteSpace(errorSummary) => IssueSeverity.Error,
            _ => IssueSeverity.Info
        };
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

    private static double SumMetric(
        IEnumerable<DashboardTelemetryStore.DashboardMetricPoint> metrics,
        string name,
        string? tagName = null,
        string? tagValue = null)
    {
        return metrics
            .Where(metric => metric.Name == name &&
                (tagName is null ||
                    (metric.Tags.TryGetValue(tagName, out var actual) &&
                        string.Equals(actual, tagValue, StringComparison.Ordinal))))
            .Sum(metric => metric.Value);
    }

    private static DateTimeOffset LatestMetric(
        IEnumerable<DashboardTelemetryStore.DashboardMetricPoint> metrics,
        string name,
        string tagName,
        string tagValue)
    {
        return metrics
            .Where(metric => metric.Name == name &&
                metric.Tags.TryGetValue(tagName, out var actual) &&
                string.Equals(actual, tagValue, StringComparison.Ordinal))
            .Select(metric => metric.ObservedAt)
            .DefaultIfEmpty(DateTimeOffset.UtcNow)
            .Max();
    }

    private static double Percentile(IReadOnlyList<double> values, double percentile)
    {
        if (values.Count == 0)
        {
            return 0;
        }

        var index = (int)Math.Ceiling(percentile * values.Count) - 1;
        return values[Math.Clamp(index, 0, values.Count - 1)];
    }

    private static string LatestProblemDetail(WorkflowView workflow)
    {
        return workflow.Steps
            .LastOrDefault(step => step.Severity is IssueSeverity.Critical or IssueSeverity.Error)
            ?.Detail ?? "The workflow status is terminal and unhealthy.";
    }

    private static string FormatProperties(IReadOnlyDictionary<string, object?> properties)
    {
        return string.Join(", ", properties
            .Where(property => property.Key != "{OriginalFormat}")
            .OrderBy(property => property.Key)
            .Select(property => $"{property.Key}={property.Value}"));
    }

    private static string Correlation(DashboardTelemetryStore.DashboardLogEntry log)
    {
        if (log.Properties.TryGetValue("trace_id", out var traceId) && traceId is not null)
        {
            return traceId.ToString()!;
        }

        if (log.Properties.TryGetValue("orca.instance.id", out var instanceId) && instanceId is not null)
        {
            return instanceId.ToString()!;
        }

        return $"{log.Category}:{log.EventId}";
    }

    private static string Shorten(string value)
    {
        return value.Length <= 12 ? value : value[..8];
    }

    private static string Age(TimeSpan duration)
    {
        return duration.TotalHours >= 1
            ? $"{duration.TotalHours:0.#}h"
            : $"{duration.TotalMinutes:0.#}m";
    }
}

public sealed record DashboardSnapshot(
    SystemStatus Status,
    IReadOnlyList<StatTile> Tiles,
    IReadOnlyList<IssueView> Issues,
    IReadOnlyList<WorkflowView> Workflows,
    IReadOnlyList<MetricSeriesView> Metrics,
    IReadOnlyList<DashboardTelemetryStore.DashboardLogEntry> Logs,
    IReadOnlyList<DashboardTelemetryStore.DashboardTraceSpan> Spans,
    DateTimeOffset GeneratedAt);

public sealed record StatTile(
    string Label,
    string Value,
    string Detail,
    TileState State);

public sealed record IssueView(
    IssueSeverity Severity,
    string Area,
    string Title,
    string Impact,
    string Evidence,
    string RecommendedAction,
    string Correlation,
    DateTimeOffset LastSeen);

public sealed record WorkflowView(
    string InstanceId,
    string DefinitionId,
    int DefinitionVersion,
    string Status,
    IssueSeverity Severity,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt,
    TimeSpan Duration,
    string? ErrorSummary,
    IReadOnlyList<ActiveWaitView> ActiveWaits,
    IReadOnlyList<WorkflowStepView> Steps);

public sealed record ActiveWaitView(
    string WaitId,
    string EventName,
    string CorrelationId,
    DateTimeOffset RegisteredAt,
    string Status,
    string Mode,
    string? BranchId);

public sealed record WorkflowStepView(
    DateTimeOffset Time,
    string Name,
    string Status,
    IssueSeverity Severity,
    string Detail,
    string EventId,
    string CommandId,
    string? Correlation);

public sealed record MetricSeriesView(
    string Name,
    string Tags,
    double Sum,
    int Count,
    DateTimeOffset LastSeen);

public enum SystemStatus
{
    Healthy,
    Degraded,
    Critical
}

public enum IssueSeverity
{
    Info,
    Warning,
    Error,
    Critical
}

public enum TileState
{
    Neutral,
    Good,
    Warning,
    Critical
}
