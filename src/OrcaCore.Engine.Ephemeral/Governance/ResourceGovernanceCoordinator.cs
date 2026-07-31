using System.Diagnostics.Metrics;
using System.Threading.Channels;
using OrcaCore.Engine.Ephemeral.Diagnostics;

namespace OrcaCore.Engine.Ephemeral.Governance;

internal sealed class ResourceGovernanceCoordinator
{
    private readonly Channel<byte> availability = Channel.CreateUnbounded<byte>(
        new UnboundedChannelOptions
        {
            AllowSynchronousContinuations = true,
            SingleReader = false,
            SingleWriter = false
        });
    private readonly Lock gate = new();
    private readonly Action? governanceWaitStarting;
    private readonly IReadOnlyDictionary<string, SlotState> transientPools;
    private readonly IReadOnlyDictionary<Type, SlotState> stepThrottles;
    private readonly ObservableGauge<long>[] gauges;
    private int waiterCount;

    internal ResourceGovernanceCoordinator(EphemeralWorkflowEngineOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        governanceWaitStarting = options.GovernanceWaitStarting;
        transientPools = options.TransientPools.ToDictionary(
            pair => pair.Key,
            pair => new SlotState("transient-pool", pair.Key, pair.Value),
            StringComparer.Ordinal);
        stepThrottles = options.StepThrottles.ToDictionary(
            pair => pair.Key,
            pair => new SlotState(
                "step-type",
                pair.Key.AssemblyQualifiedName ?? pair.Key.FullName ?? pair.Key.Name,
                pair.Value));
        gauges =
        [
            OrcaCoreEphemeralDiagnostics.Meter.CreateObservableGauge<long>(
                "orcacore.governance.configured_limit",
                () => Observe(snapshot => snapshot.ConfiguredLimit)),
            OrcaCoreEphemeralDiagnostics.Meter.CreateObservableGauge<long>(
                "orcacore.governance.active_slots",
                () => Observe(snapshot => snapshot.ActiveSlots)),
            OrcaCoreEphemeralDiagnostics.Meter.CreateObservableGauge<long>(
                "orcacore.governance.wait_depth",
                () => Observe(snapshot => snapshot.WaitDepth)),
            OrcaCoreEphemeralDiagnostics.Meter.CreateObservableGauge<long>(
                "orcacore.governance.cancellations",
                () => Observe(snapshot => snapshot.Cancellations))
        ];
    }

    internal async ValueTask<GovernanceLease> EnterStepAsync(
        Type? exactStepType,
        string? poolKey,
        CancellationToken cancellationToken)
    {
        var waiting = false;
        try
        {
            while (true)
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (TryEnterStep(exactStepType, poolKey, out var lease))
                {
                    return lease;
                }

                if (!waiting)
                {
                    waiting = true;
                    governanceWaitStarting?.Invoke();
                    Interlocked.Increment(ref waiterCount);
                    RecordWait(exactStepType, poolKey, 1);
                }

                _ = await availability.Reader.ReadAsync(cancellationToken).ConfigureAwait(false);
            }
        }
        catch (OperationCanceledException)
        {
            RecordCancellation(exactStepType, poolKey);
            throw;
        }
        finally
        {
            if (waiting)
            {
                RecordWait(exactStepType, poolKey, -1);
                Interlocked.Decrement(ref waiterCount);
            }
        }
    }

    internal bool TryEnterStep(
        Type? exactStepType,
        string? poolKey,
        out GovernanceLease lease)
    {
        stepThrottles.TryGetValue(exactStepType ?? typeof(void), out var step);
        transientPools.TryGetValue(poolKey ?? string.Empty, out var pool);

        lock (gate)
        {
            if (step?.IsSaturated == true || pool?.IsSaturated == true)
            {
                lease = GovernanceLease.Empty;
                return false;
            }

            step?.Grant();
            pool?.Grant();
        }

        lease = step is null && pool is null
            ? GovernanceLease.Empty
            : new GovernanceLease(() => Release(step, pool));
        return true;
    }

    internal IReadOnlyList<GovernanceDebugSnapshot> Snapshot()
    {
        lock (gate)
        {
            return stepThrottles.Values
                .Concat(transientPools.Values)
                .OrderBy(state => state.Kind, StringComparer.Ordinal)
                .ThenBy(state => state.Name, StringComparer.Ordinal)
                .Select(state => state.Snapshot())
                .ToArray();
        }
    }

    private IEnumerable<Measurement<long>> Observe(
        Func<GovernanceDebugSnapshot, long> selector) =>
        Snapshot().Select(snapshot => new Measurement<long>(
            selector(snapshot),
            new KeyValuePair<string, object?>("governance.kind", snapshot.Kind),
            new KeyValuePair<string, object?>("governance.name", snapshot.Name),
            new KeyValuePair<string, object?>("workflow.mode", "ephemeral")));

    private void Release(SlotState? step, SlotState? pool)
    {
        lock (gate)
        {
            step?.Release();
            pool?.Release();
        }

        var waiters = Math.Max(1, Volatile.Read(ref waiterCount));
        for (var index = 0; index < waiters; index++)
        {
            _ = availability.Writer.TryWrite(0);
        }
    }

    private void RecordWait(Type? exactStepType, string? poolKey, int delta)
    {
        lock (gate)
        {
            if (exactStepType is not null && stepThrottles.TryGetValue(exactStepType, out var step))
            {
                step.AdjustWaitDepth(delta);
            }

            if (poolKey is not null && transientPools.TryGetValue(poolKey, out var pool))
            {
                pool.AdjustWaitDepth(delta);
            }
        }
    }

    private void RecordCancellation(Type? exactStepType, string? poolKey)
    {
        lock (gate)
        {
            if (exactStepType is not null && stepThrottles.TryGetValue(exactStepType, out var step))
            {
                step.RecordCancellation();
            }

            if (poolKey is not null && transientPools.TryGetValue(poolKey, out var pool))
            {
                pool.RecordCancellation();
            }
        }
    }

    private sealed class SlotState
    {
        private int active;
        private long cancellations;
        private int waitDepth;

        internal SlotState(string kind, string name, int configuredLimit)
        {
            if (configuredLimit <= 0)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(configuredLimit),
                    configuredLimit,
                    "Governance limits must be positive.");
            }

            Kind = kind;
            Name = name;
            ConfiguredLimit = configuredLimit;
        }

        internal string Kind { get; }

        internal string Name { get; }

        internal int ConfiguredLimit { get; }

        internal bool IsSaturated => active >= ConfiguredLimit;

        internal void Grant() => active++;

        internal void Release()
        {
            if (active <= 0)
            {
                throw new InvalidOperationException("A governance slot was released without ownership.");
            }

            active--;
        }

        internal void AdjustWaitDepth(int delta) => waitDepth = checked(waitDepth + delta);

        internal void RecordCancellation() => cancellations++;

        internal GovernanceDebugSnapshot Snapshot() =>
            new(Kind, Name, ConfiguredLimit, active, waitDepth, cancellations);
    }
}

internal sealed class GovernanceLease : IAsyncDisposable
{
    internal static GovernanceLease Empty { get; } = new(null);

    private readonly Action? release;
    private int disposed;
    private int disposalTransferred;

    internal GovernanceLease(Action? release)
    {
        this.release = release;
    }

    public ValueTask DisposeAsync()
    {
        if (Volatile.Read(ref disposalTransferred) == 0)
        {
            Release();
        }

        return ValueTask.CompletedTask;
    }

    internal void RetainUntil(Task physicalAttempt)
    {
        ArgumentNullException.ThrowIfNull(physicalAttempt);
        if (Interlocked.CompareExchange(ref disposalTransferred, 1, 0) != 0)
        {
            throw new InvalidOperationException("Governance lease disposal was already transferred.");
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
            // Logical execution already fenced this attempt. Physical completion owns only release.
        }

        Release();
    }

    private void Release()
    {
        if (Interlocked.Exchange(ref disposed, 1) == 0)
        {
            release?.Invoke();
        }
    }
}

internal sealed record GovernanceDebugSnapshot(
    string Kind,
    string Name,
    int ConfiguredLimit,
    int ActiveSlots,
    int WaitDepth,
    long Cancellations);
