using System.Diagnostics;
using System.Diagnostics.Metrics;
using OrcaCore.Abstractions.Diagnostics;
using OrcaCore.Abstractions.Durable;
using OrcaCore.Runtime.Protocol.ResourceGovernance;

namespace OrcaCore.Engine.Durable.Diagnostics;

/// <summary>
/// Owns durable engine BCL diagnostics sources.
/// </summary>
public static class OrcaCoreDurableDiagnostics
{
    private static readonly object OperationalGate = new();
    private static IReadOnlyDictionary<string, PoolOperationalSnapshot> poolOperationalSnapshots =
        new Dictionary<string, PoolOperationalSnapshot>(StringComparer.Ordinal);
    private static long fencedBodiesRunning;

    public const string SourceName = OrcaCoreDiagnostics.DurableSourceName;
    public const string QuarantinedUnitsInstrumentName = "orcacore.resource.quarantined.units";
    public const string OldestQuarantinedObligationAgeInstrumentName =
        "orcacore.resource.quarantined.oldest_age";
    public const string FencedBodiesRunningInstrumentName =
        "orcacore.execution.fenced_bodies.running";

    public static ActivitySource ActivitySource { get; } = new(SourceName);

    public static Meter Meter { get; } = new(SourceName);

    private static readonly ObservableGauge<long> QuarantinedUnits = Meter.CreateObservableGauge(
        QuarantinedUnitsInstrumentName,
        ObserveQuarantinedUnits,
        unit: "{unit}",
        description: "Logical durable resource units retained by quarantined obligations.");

    private static readonly ObservableGauge<double> OldestQuarantinedObligationAge =
        Meter.CreateObservableGauge(
            OldestQuarantinedObligationAgeInstrumentName,
            ObserveOldestQuarantinedObligationAge,
            unit: "s",
            description: "Age in seconds of the oldest quarantined obligation in each resource pool.");

    private static readonly ObservableGauge<long> FencedBodiesRunning = Meter.CreateObservableGauge(
        FencedBodiesRunningInstrumentName,
        () => Interlocked.Read(ref fencedBodiesRunning),
        unit: "{body}",
        description: "Timed-out or cancelled physical step bodies still running without commit authority.");

    internal static void RefreshQuarantine(
        IEnumerable<DurableResourceLeaseObligationSnapshot> obligations)
    {
        ArgumentNullException.ThrowIfNull(obligations);
        var quarantined = obligations
            .Where(obligation => obligation.Status == DurableResourceLeaseObligationStatus.Quarantined)
            .SelectMany(obligation => obligation.Tickets.Select(ticket => new
            {
                Pool = ticket.Pool.Value,
                ticket.Units,
                obligation.QuarantinedAt
            }))
            .GroupBy(item => item.Pool, StringComparer.Ordinal)
            .ToDictionary(
                group => group.Key,
                group => new PoolOperationalSnapshot(
                    group.Sum(item => item.Units),
                    group.Where(item => item.QuarantinedAt is not null)
                        .Select(item => item.QuarantinedAt!.Value)
                        .DefaultIfEmpty()
                        .Min()),
                StringComparer.Ordinal);

        lock (OperationalGate)
        {
            poolOperationalSnapshots = quarantined;
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

    private static IEnumerable<Measurement<long>> ObserveQuarantinedUnits()
    {
        IReadOnlyDictionary<string, PoolOperationalSnapshot> snapshot;
        lock (OperationalGate)
        {
            snapshot = poolOperationalSnapshots;
        }

        return snapshot.Select(pair => new Measurement<long>(
            pair.Value.QuarantinedUnits,
            new KeyValuePair<string, object?>(OrcaCoreDiagnostics.ResourcePoolNameKey, pair.Key)));
    }

    private static IEnumerable<Measurement<double>> ObserveOldestQuarantinedObligationAge()
    {
        IReadOnlyDictionary<string, PoolOperationalSnapshot> snapshot;
        lock (OperationalGate)
        {
            snapshot = poolOperationalSnapshots;
        }

        var now = TimeProvider.System.GetUtcNow();
        return snapshot.Select(pair => new Measurement<double>(
            pair.Value.OldestQuarantinedAt == default
                ? 0
                : Math.Max(0, (now - pair.Value.OldestQuarantinedAt).TotalSeconds),
            new KeyValuePair<string, object?>(OrcaCoreDiagnostics.ResourcePoolNameKey, pair.Key)));
    }

    private sealed record PoolOperationalSnapshot(
        long QuarantinedUnits,
        DateTimeOffset OldestQuarantinedAt);
}
