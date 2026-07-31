using System.Diagnostics;
using System.Diagnostics.Metrics;
using Microsoft.Extensions.Logging;
using OrcaCore.Abstractions.Diagnostics;

namespace OrcaCore.Dashboard.Telemetry;

public sealed class DashboardTelemetryStore : ILoggerProvider, IDisposable
{
    private const int MaxMetrics = 1_000;
    private const int MaxLogs = 1_000;
    private const int MaxSpans = 500;

    private readonly object gate = new();
    private readonly TimeProvider timeProvider;
    private readonly MeterListener meterListener = new();
    private readonly ActivityListener activityListener;
    private readonly Queue<DashboardMetricPoint> metrics = [];
    private readonly Queue<DashboardLogEntry> logs = [];
    private readonly Queue<DashboardTraceSpan> spans = [];
    private readonly AsyncLocal<IReadOnlyList<IReadOnlyDictionary<string, object?>>?> scopes = new();

    public DashboardTelemetryStore()
        : this(TimeProvider.System)
    {
    }

    public DashboardTelemetryStore(TimeProvider timeProvider)
    {
        this.timeProvider = timeProvider;
        activityListener = new ActivityListener
        {
            ShouldListenTo = source => OrcaCoreDiagnostics.ActivitySourceNames.Contains(source.Name),
            Sample = (ref ActivityCreationOptions<ActivityContext> _) =>
                ActivitySamplingResult.AllDataAndRecorded,
            ActivityStopped = RecordSpan
        };
        ActivitySource.AddActivityListener(activityListener);
    }

    public void Start()
    {
        meterListener.InstrumentPublished = (instrument, listener) =>
        {
            if (OrcaCoreDiagnostics.MeterNames.Contains(instrument.Meter.Name))
            {
                listener.EnableMeasurementEvents(instrument);
            }
        };
        meterListener.SetMeasurementEventCallback<long>(RecordMeasurement);
        meterListener.SetMeasurementEventCallback<int>(RecordMeasurement);
        meterListener.SetMeasurementEventCallback<double>(RecordMeasurement);
        meterListener.Start();
    }

    public void CollectObservableMetrics()
    {
        meterListener.RecordObservableInstruments();
    }

    public IReadOnlyList<DashboardMetricPoint> GetMetrics()
    {
        lock (gate)
        {
            return metrics.ToArray();
        }
    }

    public IReadOnlyList<DashboardLogEntry> GetLogs()
    {
        lock (gate)
        {
            return logs.ToArray();
        }
    }

    public IReadOnlyList<DashboardTraceSpan> GetSpans()
    {
        lock (gate)
        {
            return spans.ToArray();
        }
    }

    public ILogger CreateLogger(string categoryName)
    {
        return new DashboardLogger(this, categoryName);
    }

    public void Dispose()
    {
        activityListener.Dispose();
        meterListener.Dispose();
    }

    private void RecordMeasurement<T>(
        Instrument instrument,
        T measurement,
        ReadOnlySpan<KeyValuePair<string, object?>> tags,
        object? state)
        where T : struct
    {
        var point = new DashboardMetricPoint(
            instrument.Name,
            Convert.ToDouble(measurement),
            CaptureTags(tags),
            timeProvider.GetUtcNow());

        lock (gate)
        {
            EnqueueBounded(metrics, point, MaxMetrics);
        }
    }

    private void RecordSpan(Activity activity)
    {
        var span = new DashboardTraceSpan(
            activity.OperationName,
            activity.TraceId.ToString(),
            activity.SpanId.ToString(),
            activity.ParentSpanId == default ? null : activity.ParentSpanId.ToString(),
            activity.Duration,
            new DateTimeOffset(DateTime.SpecifyKind(activity.StartTimeUtc, DateTimeKind.Utc)),
            CaptureTags(activity.TagObjects));

        lock (gate)
        {
            EnqueueBounded(spans, span, MaxSpans);
        }
    }

    private IDisposable PushScope(IReadOnlyDictionary<string, object?> scope)
    {
        var prior = scopes.Value;
        scopes.Value = prior is null
            ? [scope]
            : [.. prior, scope];
        return new ScopePopper(() => scopes.Value = prior);
    }

    private void Record<TState>(
        string category,
        LogLevel level,
        Microsoft.Extensions.Logging.EventId eventId,
        TState state,
        Exception? exception,
        Func<TState, Exception?, string> formatter)
    {
        var properties = new Dictionary<string, object?>(StringComparer.Ordinal);
        if (scopes.Value is not null)
        {
            foreach (var scope in scopes.Value)
            {
                foreach (var (key, value) in scope)
                {
                    properties[key] = value;
                }
            }
        }

        foreach (var (key, value) in CaptureState(state))
        {
            properties[key] = value;
        }

        var entry = new DashboardLogEntry(
            timeProvider.GetUtcNow(),
            category,
            level,
            eventId.Id,
            formatter(state, exception),
            exception?.GetType().Name,
            exception?.Message,
            properties);

        lock (gate)
        {
            EnqueueBounded(logs, entry, MaxLogs);
        }
    }

    private static void EnqueueBounded<T>(Queue<T> queue, T value, int maxCount)
    {
        queue.Enqueue(value);
        while (queue.Count > maxCount)
        {
            queue.Dequeue();
        }
    }

    private static IReadOnlyDictionary<string, string> CaptureTags(IEnumerable<KeyValuePair<string, object?>> tags)
    {
        var captured = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var (key, value) in tags)
        {
            captured[key] = value?.ToString() ?? string.Empty;
        }

        return captured;
    }

    private static IReadOnlyDictionary<string, string> CaptureTags(
        ReadOnlySpan<KeyValuePair<string, object?>> tags)
    {
        var captured = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var tag in tags)
        {
            captured[tag.Key] = tag.Value?.ToString() ?? string.Empty;
        }

        return captured;
    }

    private static IReadOnlyDictionary<string, object?> CaptureState<TState>(TState state)
    {
        return state is IEnumerable<KeyValuePair<string, object?>> pairs
            ? pairs.ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.Ordinal)
            : new Dictionary<string, object?>(StringComparer.Ordinal);
    }

    private sealed class DashboardLogger(
        DashboardTelemetryStore store,
        string categoryName) : ILogger
    {
        public IDisposable? BeginScope<TState>(TState state)
            where TState : notnull
        {
            return store.PushScope(CaptureState(state));
        }

        public bool IsEnabled(LogLevel logLevel)
        {
            return true;
        }

        public void Log<TState>(
            LogLevel logLevel,
            Microsoft.Extensions.Logging.EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            store.Record(categoryName, logLevel, eventId, state, exception, formatter);
        }
    }

    private sealed class ScopePopper(Action pop) : IDisposable
    {
        public void Dispose()
        {
            pop();
        }
    }

    public sealed record DashboardMetricPoint(
        string Name,
        double Value,
        IReadOnlyDictionary<string, string> Tags,
        DateTimeOffset ObservedAt);

    public sealed record DashboardLogEntry(
        DateTimeOffset Timestamp,
        string Category,
        LogLevel Level,
        int EventId,
        string Message,
        string? ExceptionType,
        string? ExceptionMessage,
        IReadOnlyDictionary<string, object?> Properties);

    public sealed record DashboardTraceSpan(
        string Name,
        string TraceId,
        string SpanId,
        string? ParentSpanId,
        TimeSpan Duration,
        DateTimeOffset StartedAt,
        IReadOnlyDictionary<string, string> Tags);
}
