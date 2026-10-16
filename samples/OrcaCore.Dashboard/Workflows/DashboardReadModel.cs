using OrcaCore.Dashboard.Telemetry;

namespace OrcaCore.Dashboard.Workflows;

public sealed class DashboardReadModel(
    DashboardTelemetryStore telemetry,
    TimeProvider timeProvider)
{
    public DashboardSnapshot GetSnapshot()
    {
        telemetry.CollectObservableMetrics();

        var metrics = telemetry.GetMetrics()
            .GroupBy(
                metric => new
                {
                    metric.Name,
                    Tags = string.Join(", ", metric.Tags
                        .OrderBy(tag => tag.Key, StringComparer.Ordinal)
                        .Select(tag => $"{tag.Key}={tag.Value}"))
                })
            .Select(group => new MetricSeries(
                group.Key.Name,
                group.Key.Tags,
                group.Sum(point => point.Value),
                group.Count(),
                group.Max(point => point.ObservedAt)))
            .OrderBy(series => series.Name, StringComparer.Ordinal)
            .ThenBy(series => series.Tags, StringComparer.Ordinal)
            .ToArray();

        return new DashboardSnapshot(
            metrics,
            telemetry.GetLogs()
                .OrderByDescending(log => log.Timestamp)
                .Take(50)
                .Select(log => new DashboardLog(
                    log.Timestamp,
                    log.Category,
                    log.Level.ToString(),
                    log.EventId,
                    log.Message))
                .ToArray(),
            telemetry.GetSpans().OrderByDescending(span => span.StartedAt).Take(50).ToArray(),
            timeProvider.GetUtcNow());
    }
}

public sealed record DashboardSnapshot(
    IReadOnlyList<MetricSeries> Metrics,
    IReadOnlyList<DashboardLog> Logs,
    IReadOnlyList<DashboardTelemetryStore.DashboardTraceSpan> Spans,
    DateTimeOffset GeneratedAt);

public sealed record DashboardLog(
    DateTimeOffset Timestamp,
    string Category,
    string Level,
    int EventId,
    string Message);

public sealed record MetricSeries(
    string Name,
    string Tags,
    double Sum,
    int Count,
    DateTimeOffset LastSeen);
