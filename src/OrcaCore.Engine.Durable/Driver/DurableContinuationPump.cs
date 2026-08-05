using System.Text.Json;
using OrcaCore.Abstractions.Durable;
using OrcaCore.Abstractions.Ids;
using OrcaCore.Abstractions.Primitives;
using OrcaCore.Abstractions.Providers;
using OrcaCore.Engine.Durable.Aggregates;
using OrcaCore.Engine.Durable.Execution;

namespace OrcaCore.Engine.Durable.Driver;

using WorkflowStatus = global::OrcaCore.WorkflowInstanceStatus;

/// <summary>
/// Claims internal <c>continue</c> outbox records and drives the referenced instances to their
/// next suspension point (DR-034). The pump is the restart-safety net behind the in-process
/// happy path: the committing host usually continues immediately, so most claims resolve as
/// no-ops — the driver reloads the committed position, finds no work, and the record is marked
/// processed. Repeated drive failures follow the poison path: bounded retries paced by the
/// pump interval, then the instance parks with a diagnostic (DR-036).
/// </summary>
internal sealed class DurableContinuationPump(
    IWorkflowOutboxStore outboxStore,
    DurableWorkflowRuntime runtime,
    DurableCommandProcessor processor,
    TimeProvider? timeProvider = null,
    int maxDriveAttemptsBeforePark = DurableContinuationPump.DefaultMaxDriveAttemptsBeforePark,
    int maxDegreeOfParallelism = DurableContinuationPump.DefaultMaxDegreeOfParallelism,
    TimeSpan? initialFailureBackoff = null,
    IDurableDriverObserver? observer = null)
{
    /// <summary>
    /// Default bounded drive attempts before a failing instance parks as poison (DR-036).
    /// </summary>
    public const int DefaultMaxDriveAttemptsBeforePark = 5;

    /// <summary>
    /// Default bounded worker concurrency for one pump cycle (DR-033). Instances advance in
    /// parallel; commands for one instance stay lane-serialized inside the processor.
    /// </summary>
    public const int DefaultMaxDegreeOfParallelism = 8;

    /// <summary>
    /// Default durable backoff after the first failed advancement attempt.
    /// </summary>
    public static TimeSpan DefaultInitialFailureBackoff { get; } = TimeSpan.FromSeconds(1);

    private static TimeSpan MaximumFailureBackoff { get; } = TimeSpan.FromMinutes(1);

    private readonly TimeProvider timeProvider = timeProvider ?? TimeProvider.System;
    private readonly DurableAggregateLoader aggregateLoader = new(processor.EventStore);
    private readonly IDurableDriverObserver observer = observer ?? NullDurableDriverObserver.Instance;
    private readonly TimeSpan initialFailureBackoff = ValidateFailureBackoff(
        initialFailureBackoff ?? DefaultInitialFailureBackoff);

    /// <summary>
    /// Claims and processes one batch of continuation records. The claim is always restricted
    /// to the <c>continue</c> kind (DR-037), regardless of the selector on the request.
    /// </summary>
    public async Task<int> PumpOnceAsync(OutboxClaimRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(request.MaxCount);
        if (request.LeaseDuration <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(
                nameof(OutboxClaimRequest.LeaseDuration),
                request.LeaseDuration,
                "Lease duration must be positive.");
        }

        var records = await outboxStore
            .ClaimAsync(
                request with { KindSelector = OutboxKindSelector.Including(OutboxKinds.Continue) },
                cancellationToken)
            .ConfigureAwait(false);
        if (records.Count == 0)
        {
            return 0;
        }

        var recordsByInstance = new Dictionary<InstanceId, List<OutboxRecordId>>();
        foreach (var record in records)
        {
            DurableContinuationSignal signal;
            try
            {
                signal = DurableContinuationSignal.Deserialize(record.Payload);
            }
            catch (JsonException)
            {
                // An unreadable signal can never advance anything; retrying it would loop.
                await outboxStore
                    .MarkAsync(record.OutboxRecordId, OutboxRecordState.Poisoned, cancellationToken)
                    .ConfigureAwait(false);
                continue;
            }

            if (signal.NotBefore is { } notBefore && notBefore > timeProvider.GetUtcNow())
            {
                await outboxStore.ReleaseAsync(record.OutboxRecordId, cancellationToken).ConfigureAwait(false);
                continue;
            }

            await ObserveContinuationStartedAsync(signal, cancellationToken).ConfigureAwait(false);

            if (!recordsByInstance.TryGetValue(signal.InstanceId, out var recordIds))
            {
                recordsByInstance[signal.InstanceId] = recordIds = [];
            }

            recordIds.Add(record.OutboxRecordId);
        }

        var processed = 0;
        await Parallel.ForEachAsync(
            recordsByInstance,
            new ParallelOptions
            {
                MaxDegreeOfParallelism = maxDegreeOfParallelism,
                CancellationToken = cancellationToken
            },
            async (group, token) =>
            {
                var advanced = await AdvanceAsync(group.Key, group.Value, token).ConfigureAwait(false);
                Interlocked.Add(ref processed, advanced);
            })
            .ConfigureAwait(false);
        return processed;
    }

    private async Task<int> AdvanceAsync(
        InstanceId instanceId,
        IReadOnlyList<OutboxRecordId> recordIds,
        CancellationToken cancellationToken)
    {
        DurableSegmentResult result;
        try
        {
            var before = await aggregateLoader.LoadAsync(instanceId, cancellationToken).ConfigureAwait(false);
            if (before.ContinuationRetryNotBefore is { } notBefore && notBefore > timeProvider.GetUtcNow())
            {
                await ReleaseAllAsync(recordIds, cancellationToken).ConfigureAwait(false);
                return 0;
            }

            result = await runtime
                .DriveAsync(instanceId, DurableDriveMode.Required, cancellationToken)
                .ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            await MarkAllAsync(recordIds, OutboxRecordState.Retryable, CancellationToken.None)
                .ConfigureAwait(false);
            throw;
        }
        catch (Exception exception)
        {
            return await HandleDriveFailureAsync(instanceId, recordIds, exception).ConfigureAwait(false);
        }

        if (result.Outcome == DurableSegmentOutcome.Conflict)
        {
            // Another mutator kept moving the stream past the driver's bounded retries; the
            // signal stays claimable and a later cycle re-reads the committed position.
            await MarkAllAsync(recordIds, OutboxRecordState.Retryable, cancellationToken).ConfigureAwait(false);
            return 0;
        }

        if (result.Outcome == DurableSegmentOutcome.BudgetExhausted && !result.CommittedProgress)
        {
            // No progress commit means no successor continuation exists. Consuming this claim
            // would strand the runnable instance; keep the existing signal retryable instead.
            await MarkAllAsync(recordIds, OutboxRecordState.Retryable, cancellationToken)
                .ConfigureAwait(false);
            return 0;
        }

        if (!await ResetFailureStateAsync(instanceId, cancellationToken).ConfigureAwait(false))
        {
            await MarkAllAsync(recordIds, OutboxRecordState.Retryable, cancellationToken).ConfigureAwait(false);
            return 0;
        }

        await MarkAllAsync(recordIds, OutboxRecordState.Dispatched, cancellationToken).ConfigureAwait(false);
        return recordIds.Count;
    }

    private async Task<int> HandleDriveFailureAsync(
        InstanceId instanceId,
        IReadOnlyList<OutboxRecordId> recordIds,
        Exception exception)
    {
        DurableWorkflowAggregate aggregate;
        Option<CheckpointWrite> checkpoint;
        try
        {
            aggregate = await aggregateLoader.LoadAsync(instanceId, CancellationToken.None).ConfigureAwait(false);
            checkpoint = await processor.EventStore
                .LoadCheckpointAsync(instanceId, CancellationToken.None)
                .ConfigureAwait(false);
        }
        catch
        {
            await MarkAllAsync(recordIds, OutboxRecordState.Retryable, CancellationToken.None)
                .ConfigureAwait(false);
            return 0;
        }

        var positionStreamVersion = checkpoint.HasValue
            ? (StreamVersion?)checkpoint.Value.StreamVersion
            : null;
        var samePosition = aggregate.ContinuationFailurePositionStreamVersion == positionStreamVersion;
        var attempts = samePosition ? aggregate.ContinuationFailureCount + 1 : 1;
        var errorSummary = $"{exception.GetType().Name}: {exception.Message}";
        if (attempts < maxDriveAttemptsBeforePark)
        {
            var failure = await processor
                .ProcessAsync(
                    new DurableContinuationAttemptFailedCommand(
                        CommandId.New(),
                        instanceId,
                        timeProvider.GetUtcNow(),
                        errorSummary,
                        positionStreamVersion,
                        timeProvider.GetUtcNow().Add(FailureBackoff(attempts)),
                        aggregate.StreamVersion),
                    CancellationToken.None)
                .ConfigureAwait(false);
            // Keep the claimed signal retryable even when the failure fact committed. A
            // Waiting instance with a runnable sibling has no runnable checkpoint on this
            // failure decision, so continuation materialization may emit no successor. The
            // persisted ContinuationRetryNotBefore throttles this retained signal; when a
            // successor was also emitted, the next pump groups both by instance and drives once.
            await MarkAllAsync(recordIds, OutboxRecordState.Retryable, CancellationToken.None)
                .ConfigureAwait(false);
            return 0;
        }

        var parked = await processor
            .ProcessAsync(
                new DurableParkCommand(
                    CommandId.New(),
                    instanceId,
                    timeProvider.GetUtcNow(),
                    DurableParkReason.Poison,
                    $"Continuation advancement failed {attempts} times; last: " +
                    errorSummary,
                    attempts,
                    positionStreamVersion)
                {
                    ExpectedStreamVersion = aggregate.StreamVersion
                },
                CancellationToken.None)
            .ConfigureAwait(false);
        var parkedDisposition = parked.Outcome == DurableCommandOutcome.Conflict
            ? OutboxRecordState.Retryable
            : OutboxRecordState.Poisoned;
        await MarkAllAsync(recordIds, parkedDisposition, CancellationToken.None)
            .ConfigureAwait(false);
        return 0;
    }

    private async Task<bool> ResetFailureStateAsync(
        InstanceId instanceId,
        CancellationToken cancellationToken)
    {
        try
        {
            var aggregate = await aggregateLoader.LoadAsync(instanceId, cancellationToken).ConfigureAwait(false);
            if (aggregate.ContinuationFailureCount == 0 ||
                aggregate.IsTerminal ||
                aggregate.ParkReason is not null)
            {
                return true;
            }

            var reset = await processor
                .ProcessAsync(
                    new DurableContinuationAttemptResetCommand(
                        CommandId.New(),
                        instanceId,
                        timeProvider.GetUtcNow(),
                        aggregate.StreamVersion),
                    cancellationToken)
                .ConfigureAwait(false);
            return reset.Outcome != DurableCommandOutcome.Conflict;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch
        {
            return false;
        }
    }

    private TimeSpan FailureBackoff(int attempt)
    {
        var multiplier = Math.Pow(2, Math.Max(0, attempt - 1));
        var milliseconds = Math.Min(
            MaximumFailureBackoff.TotalMilliseconds,
            initialFailureBackoff.TotalMilliseconds * multiplier);
        return TimeSpan.FromMilliseconds(milliseconds);
    }

    private static TimeSpan ValidateFailureBackoff(TimeSpan value)
    {
        return value > TimeSpan.Zero
            ? value
            : throw new ArgumentOutOfRangeException(nameof(initialFailureBackoff), value, "Backoff must be positive.");
    }

    private async ValueTask ObserveContinuationStartedAsync(
        DurableContinuationSignal signal,
        CancellationToken cancellationToken)
    {
        var lag = timeProvider.GetUtcNow() - signal.OccurredAt;
        if (lag < TimeSpan.Zero)
        {
            lag = TimeSpan.Zero;
        }

        try
        {
            await observer
                .OnContinuationStartedAsync(
                    new DurableContinuationObservation(
                        ProviderName(outboxStore),
                        "claimed",
                        lag),
                    cancellationToken)
                .ConfigureAwait(false);
        }
        catch
        {
            // Driver observations are diagnostics and must not alter claim disposition.
        }
    }

    private static string ProviderName(object provider)
    {
        return provider.GetType().Name
            .Replace("WorkflowProvider", string.Empty, StringComparison.Ordinal)
            .Replace("WorkflowStore", string.Empty, StringComparison.Ordinal)
            .Replace("EventStore", string.Empty, StringComparison.Ordinal);
    }

    private async Task ReleaseAllAsync(
        IReadOnlyList<OutboxRecordId> recordIds,
        CancellationToken cancellationToken)
    {
        foreach (var recordId in recordIds)
        {
            await outboxStore.ReleaseAsync(recordId, cancellationToken).ConfigureAwait(false);
        }
    }

    private async Task MarkAllAsync(
        IReadOnlyList<OutboxRecordId> recordIds,
        OutboxRecordState state,
        CancellationToken cancellationToken)
    {
        foreach (var recordId in recordIds)
        {
            await outboxStore.MarkAsync(recordId, state, cancellationToken).ConfigureAwait(false);
        }
    }
}
