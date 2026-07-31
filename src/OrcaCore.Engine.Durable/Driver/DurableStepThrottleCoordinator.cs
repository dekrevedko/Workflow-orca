using System.Collections.Concurrent;
using System.Diagnostics.Metrics;
using System.Threading.Channels;
using OrcaCore.Engine.Durable.Diagnostics;

namespace OrcaCore.Engine.Durable.Driver;

internal sealed class DurableStepThrottleCoordinator
{
    private readonly ConcurrentDictionary<Type, ThrottleState> throttles;
    private readonly ObservableGauge<long>[] gauges;

    internal DurableStepThrottleCoordinator(IReadOnlyDictionary<Type, int>? configured = null)
    {
        throttles = new ConcurrentDictionary<Type, ThrottleState>(
            (configured ?? new Dictionary<Type, int>()).Select(pair =>
            {
                if (pair.Value <= 0)
                {
                    throw new ArgumentOutOfRangeException(
                        nameof(configured),
                        pair.Value,
                        "Step throttle limits must be positive.");
                }

                return new KeyValuePair<Type, ThrottleState>(
                    pair.Key,
                    new ThrottleState(pair.Key, pair.Value));
            }));
        gauges =
        [
            OrcaCoreDurableDiagnostics.Meter.CreateObservableGauge<long>(
                "orcacore.governance.configured_limit",
                () => Observe(snapshot => snapshot.ConfiguredLimit)),
            OrcaCoreDurableDiagnostics.Meter.CreateObservableGauge<long>(
                "orcacore.governance.active_slots",
                () => Observe(snapshot => snapshot.ActiveSlots)),
            OrcaCoreDurableDiagnostics.Meter.CreateObservableGauge<long>(
                "orcacore.governance.wait_depth",
                () => Observe(snapshot => snapshot.WaitDepth)),
            OrcaCoreDurableDiagnostics.Meter.CreateObservableGauge<long>(
                "orcacore.governance.cancellations",
                () => Observe(snapshot => snapshot.Cancellations))
        ];
    }

    internal bool IsConfigured(Type? exactStepType) =>
        exactStepType is not null && throttles.ContainsKey(exactStepType);

    internal bool TryEnter(Type? exactStepType, out DurableStepThrottleLease lease)
    {
        if (exactStepType is null || !throttles.TryGetValue(exactStepType, out var throttle))
        {
            lease = DurableStepThrottleLease.Empty;
            return true;
        }

        if (!throttle.Tokens.Reader.TryRead(out _))
        {
            lease = DurableStepThrottleLease.Empty;
            return false;
        }

        throttle.RecordGrant();
        lease = new DurableStepThrottleLease(throttle);
        return true;
    }

    internal async ValueTask<DurableStepThrottleLease> EnterAsync(
        Type exactStepType,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(exactStepType);
        if (!throttles.TryGetValue(exactStepType, out var throttle))
        {
            return DurableStepThrottleLease.Empty;
        }

        throttle.RecordWait();
        try
        {
            _ = await throttle.Tokens.Reader.ReadAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            throttle.RecordCancellation();
            throw;
        }
        finally
        {
            throttle.EndWait();
        }

        throttle.RecordGrant();
        return new DurableStepThrottleLease(throttle);
    }

    internal IReadOnlyList<StepThrottleDebugSnapshot> Snapshot() =>
        throttles.Values
            .OrderBy(throttle => throttle.StepType.FullName, StringComparer.Ordinal)
            .Select(throttle => throttle.Snapshot())
            .ToArray();

    private IEnumerable<Measurement<long>> Observe(
        Func<StepThrottleDebugSnapshot, long> selector) =>
        Snapshot().Select(snapshot => new Measurement<long>(
            selector(snapshot),
            new KeyValuePair<string, object?>("governance.kind", "step-type"),
            new KeyValuePair<string, object?>(
                "governance.name",
                snapshot.StepType.AssemblyQualifiedName ??
                snapshot.StepType.FullName ??
                snapshot.StepType.Name),
            new KeyValuePair<string, object?>("workflow.mode", "durable")));

    internal sealed class ThrottleState
    {
        private long active;
        private long cancellations;
        private long waitDepth;

        internal ThrottleState(Type stepType, int capacity)
        {
            StepType = stepType;
            Capacity = capacity;
            Tokens = Channel.CreateBounded<byte>(new BoundedChannelOptions(capacity)
            {
                AllowSynchronousContinuations = true,
                FullMode = BoundedChannelFullMode.Wait,
                SingleReader = false,
                SingleWriter = false
            });
            for (var index = 0; index < capacity; index++)
            {
                if (!Tokens.Writer.TryWrite(0))
                {
                    throw new InvalidOperationException("Could not initialize a durable step throttle.");
                }
            }
        }

        internal Type StepType { get; }

        internal int Capacity { get; }

        internal Channel<byte> Tokens { get; }

        internal void RecordGrant() => Interlocked.Increment(ref active);

        internal void RecordWait() => Interlocked.Increment(ref waitDepth);

        internal void EndWait() => Interlocked.Decrement(ref waitDepth);

        internal void RecordCancellation() => Interlocked.Increment(ref cancellations);

        internal void Release()
        {
            if (!Tokens.Writer.TryWrite(0))
            {
                throw new InvalidOperationException("A durable step-throttle token could not be returned.");
            }

            Interlocked.Decrement(ref active);
        }

        internal StepThrottleDebugSnapshot Snapshot() =>
            new(
                StepType,
                Capacity,
                checked((int)Volatile.Read(ref active)),
                checked((int)Volatile.Read(ref waitDepth)),
                Volatile.Read(ref cancellations));
    }
}

internal sealed class DurableStepThrottleLease : IAsyncDisposable
{
    internal static DurableStepThrottleLease Empty { get; } = new(null);

    private readonly DurableStepThrottleCoordinator.ThrottleState? throttle;
    private int released;
    private int transferred;

    internal DurableStepThrottleLease(DurableStepThrottleCoordinator.ThrottleState? throttle)
    {
        this.throttle = throttle;
    }

    public ValueTask DisposeAsync()
    {
        if (Volatile.Read(ref transferred) == 0)
        {
            Release();
        }

        return ValueTask.CompletedTask;
    }

    internal void RetainUntil(Task physicalAttempt)
    {
        ArgumentNullException.ThrowIfNull(physicalAttempt);
        if (Interlocked.CompareExchange(ref transferred, 1, 0) != 0)
        {
            throw new InvalidOperationException("Step-throttle lease ownership was already transferred.");
        }

        _ = ReleaseAfterAsync(physicalAttempt);
    }

    private async Task ReleaseAfterAsync(Task physicalAttempt)
    {
        try
        {
            await physicalAttempt.ConfigureAwait(false);
        }
        catch
        {
            // Logical execution owns the result. Physical completion owns only slot release.
        }

        Release();
    }

    private void Release()
    {
        if (Interlocked.Exchange(ref released, 1) == 0)
        {
            throttle?.Release();
        }
    }
}

internal sealed record StepThrottleDebugSnapshot(
    Type StepType,
    int ConfiguredLimit,
    int ActiveSlots,
    int WaitDepth,
    long Cancellations);
