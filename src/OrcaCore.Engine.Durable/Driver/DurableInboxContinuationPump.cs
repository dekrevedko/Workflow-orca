using OrcaCore.Abstractions.Durable;
using OrcaCore.Abstractions.Providers;
using OrcaCore.Engine.Durable.Execution;

namespace OrcaCore.Engine.Durable.Driver;

/// <summary>
/// Discovers accepted inbox records that became eligible outside the accepting process and
/// commits their wait-match transition. The provider commit owns the actual claim through the
/// observed route revisions plus the <c>Received</c> expected state; competing pumps may inspect
/// the same record, but only one can apply it and create the continuation outbox handoff.
/// </summary>
internal sealed class DurableInboxContinuationPump(
    IWorkflowInboxStore inboxStore,
    IWorkflowProjectionStore projectionStore,
    DurableWorkflowRuntime runtime,
    TimeProvider? timeProvider = null,
    int maxFailuresBeforePoison = DurableContinuationPump.DefaultMaxDriveAttemptsBeforePark,
    TimeSpan? initialFailureBackoff = null)
{
    private long afterAcceptanceSequence;
    private long pumpCount;
    private readonly TimeProvider timeProvider = timeProvider ?? TimeProvider.System;
    private readonly TimeSpan initialFailureBackoff = ValidateFailureBackoff(
        initialFailureBackoff ?? DurableContinuationPump.DefaultInitialFailureBackoff);
    private readonly int maxHandoffFailuresBeforePoison = ValidateFailureCount(maxFailuresBeforePoison);

    private static TimeSpan MaximumFailureBackoff { get; } = TimeSpan.FromMinutes(1);

    internal async Task<int> PumpOnceAsync(int maxCount, CancellationToken cancellationToken)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maxCount);
        var page = await ReadNextBatchAsync(maxCount, cancellationToken).ConfigureAwait(false);
        var records = page.Records;
        var applied = 0;
        foreach (var record in records)
        {
            if (record.HandoffRetryNotBefore is { } retryNotBefore && retryNotBefore > timeProvider.GetUtcNow())
            {
                continue;
            }

            try
            {
                if (await TryApplyAsync(record, cancellationToken).ConfigureAwait(false))
                {
                    applied++;
                }
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception exception)
            {
                var failedRecord = exception is InboxHandoffAttemptException attempt
                    ? attempt.Record
                    : record;
                var failure = exception is InboxHandoffAttemptException { InnerException: { } inner }
                    ? inner
                    : exception;
                await RecordFailureWithoutAbortingBatchAsync(failedRecord, failure, cancellationToken)
                    .ConfigureAwait(false);
            }
        }

        if (page.ForwardCursor is { } forwardCursor)
        {
            Interlocked.Exchange(ref afterAcceptanceSequence, forwardCursor);
        }

        return applied;
    }

    private async Task<(IReadOnlyList<InboxRecord> Records, long? ForwardCursor)> ReadNextBatchAsync(
        int maxCount,
        CancellationToken cancellationToken)
    {
        var cycle = Interlocked.Increment(ref pumpCount);
        var retryBudget = maxCount == 1
            ? (cycle % 2 == 0 ? 1 : 0)
            : Math.Max(1, maxCount / 2);
        var retryRecords = retryBudget == 0
            ? []
            : await inboxStore
                .ListHandoffRetriesAsync(timeProvider.GetUtcNow(), retryBudget, cancellationToken)
                .ConfigureAwait(false);
        var forwardBudget = maxCount - retryRecords.Count;
        if (forwardBudget == 0)
        {
            return (retryRecords, null);
        }

        var cursor = Interlocked.Read(ref afterAcceptanceSequence);
        var forwardRecords = await inboxStore
            .ListReceivedAsync(cursor, maxCount, cancellationToken)
            .ConfigureAwait(false);
        if (forwardRecords.Count == 0 && cursor != 0)
        {
            cursor = 0;
            Interlocked.Exchange(ref afterAcceptanceSequence, 0);
            forwardRecords = await inboxStore
                .ListReceivedAsync(0, maxCount, cancellationToken)
                .ConfigureAwait(false);
        }

        var retriedRecords = retryRecords
            .Select(record => new InboxRecordIdentity(record.EventId, record.InstanceId))
            .ToHashSet();
        var selectedForwardRecords = new List<InboxRecord>(forwardBudget);
        long? forwardCursor = null;
        foreach (var record in forwardRecords)
        {
            forwardCursor = record.AcceptanceSequence;
            if (!retriedRecords.Contains(new InboxRecordIdentity(record.EventId, record.InstanceId)))
            {
                selectedForwardRecords.Add(record);
            }

            if (selectedForwardRecords.Count == forwardBudget)
            {
                break;
            }
        }

        var records = retryRecords.Concat(selectedForwardRecords).ToArray();
        return (records, forwardCursor);
    }

    private async Task RecordFailureWithoutAbortingBatchAsync(
        InboxRecord record,
        Exception exception,
        CancellationToken cancellationToken)
    {
        if (record.HandoffFailureCount >= maxHandoffFailuresBeforePoison)
        {
            return;
        }

        var attempt = record.HandoffFailureCount + 1;
        var errorSummary = $"{exception.GetType().Name}: {exception.Message}";
        try
        {
            await inboxStore.RecordHandoffFailureAsync(
                new InboxRecordIdentity(record.EventId, record.InstanceId),
                InboxRecordState.Received,
                record.HandoffFailureCount,
                maxHandoffFailuresBeforePoison,
                timeProvider.GetUtcNow().Add(FailureBackoff(attempt)),
                "inbox-continuation-failed",
                $"Autonomous inbox handoff failed {attempt} times; last: {errorSummary}",
                cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch
        {
            // Failure-state persistence is itself provider work. Keep the accepted record in
            // Received and continue the batch; a healthy provider records the durable retry on
            // the same call, while a broader provider outage is retried by the hosted cycle.
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
            : throw new ArgumentOutOfRangeException(
                nameof(initialFailureBackoff),
                value,
                "Backoff must be positive.");
    }

    private static int ValidateFailureCount(int value)
    {
        return value > 0
            ? value
            : throw new ArgumentOutOfRangeException(
                nameof(maxFailuresBeforePoison),
                value,
                "Failure count must be positive.");
    }

    private async Task<bool> TryApplyAsync(
        InboxRecord record,
        CancellationToken cancellationToken)
    {
        if (record.Envelope is not { } envelope)
        {
            await inboxStore.MarkPoisonedAsync(
                new InboxRecordIdentity(record.EventId, record.InstanceId),
                InboxRecordState.Received,
                "inbox-envelope-missing",
                "The accepted inbox record has no normalized event envelope.",
                cancellationToken).ConfigureAwait(false);
            return false;
        }

        var target = await ResolveTargetAsync(record, envelope, cancellationToken).ConfigureAwait(false);
        if (target is null)
        {
            return false;
        }

        var eventName = EventName.Create(envelope.EventName);
        var eventVersion = new EventContractVersion(envelope.EventContractVersion);
        if (!target.ActiveWaits.Any(wait =>
                string.Equals(wait.EventName, eventName.Value, StringComparison.Ordinal) &&
                wait.EventContractVersion == eventVersion.Value &&
                wait.CorrelationId.Equals(envelope.CorrelationId)))
        {
            return false;
        }

        var match = await inboxStore.GetMatchSnapshotAsync(
            new InboxMatchRequest(
                target.InstanceId,
                target.DefinitionId,
                eventName,
                eventVersion,
                envelope.CorrelationId),
            cancellationToken).ConfigureAwait(false);
        if (match.PendingEvent?.Envelope is not { } pendingEnvelope)
        {
            return false;
        }

        DurableCommandResult result;
        try
        {
            result = await runtime.RaiseFacadeEventAsync(
                target.InstanceId,
                pendingEnvelope,
                cancellationToken,
                driveAfterDelivery: false,
                match.PendingEvent.EnvelopeFingerprint,
                match).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            throw new InboxHandoffAttemptException(match.PendingEvent, exception);
        }

        if (result.Outcome == DurableCommandOutcome.Conflict)
        {
            throw new InboxHandoffAttemptException(
                match.PendingEvent,
                new InvalidOperationException("The inbox handoff lost its conditional commit race."));
        }

        return result.Outcome == DurableCommandOutcome.Committed;
    }

    private async Task<WorkflowProjectionSnapshot?> ResolveTargetAsync(
        InboxRecord record,
        DurableEventEnvelope envelope,
        CancellationToken cancellationToken)
    {
        switch (envelope.Route.Kind)
        {
            case "direct" when envelope.Route.InstanceId is { } instanceId:
            {
                var target = await projectionStore.GetAsync(instanceId, cancellationToken).ConfigureAwait(false);
                if (!target.HasValue)
                {
                    await inboxStore.MarkPoisonedAsync(
                        new InboxRecordIdentity(record.EventId, record.InstanceId),
                        InboxRecordState.Received,
                        "direct-target-missing",
                        "The accepted direct target is no longer present in the provider projection.",
                        cancellationToken).ConfigureAwait(false);
                    return null;
                }

                if (IsTerminal(target.Value.Status))
                {
                    await inboxStore.MarkPoisonedAsync(
                        new InboxRecordIdentity(record.EventId, record.InstanceId),
                        InboxRecordState.Received,
                        "target-terminal",
                        "The direct target became terminal before this accepted event matched a wait.",
                        cancellationToken).ConfigureAwait(false);
                    return null;
                }

                return target.Value;
            }
            case "correlation" when envelope.Route.DefinitionId is { } definitionId:
            {
                var candidates = await projectionStore.FindActiveWaitsAsync(
                    definitionId,
                    EventName.Create(envelope.EventName),
                    new EventContractVersion(envelope.EventContractVersion),
                    envelope.CorrelationId,
                    cancellationToken).ConfigureAwait(false);
                if (candidates.Count <= 1)
                {
                    return candidates.SingleOrDefault();
                }

                await inboxStore.MarkPoisonedAsync(
                    new InboxRecordIdentity(record.EventId, record.InstanceId),
                    InboxRecordState.Received,
                    "ambiguous-active-wait",
                    "More than one persisted wait is eligible for the accepted correlation route.",
                    cancellationToken).ConfigureAwait(false);
                return null;
            }
            case "definition-fanout" when record.InstanceId is { } fanoutTargetId:
            {
                var target = await projectionStore.GetAsync(fanoutTargetId, cancellationToken).ConfigureAwait(false);
                if (!target.HasValue)
                {
                    await inboxStore.MarkPoisonedAsync(
                        new InboxRecordIdentity(record.EventId, fanoutTargetId),
                        InboxRecordState.Received,
                        "fanout-target-missing",
                        "The instance captured by the committed fanout snapshot is no longer present.",
                        cancellationToken).ConfigureAwait(false);
                    return null;
                }

                if (IsTerminal(target.Value.Status))
                {
                    await inboxStore.MarkPoisonedAsync(
                        new InboxRecordIdentity(record.EventId, fanoutTargetId),
                        InboxRecordState.Received,
                        "fanout-target-terminal",
                        "The instance captured by the fanout snapshot became terminal before delivery.",
                        cancellationToken).ConfigureAwait(false);
                    return null;
                }

                return target.Value;
            }
            default:
                // Start-or-deliver retains its accepted record for task 7.31, whose materializer
                // feeds the same continuation outbox lane.
                return null;
        }
    }

    private static bool IsTerminal(WorkflowInstanceStatus status) =>
        status is WorkflowInstanceStatus.Completed or WorkflowInstanceStatus.Failed or
            WorkflowInstanceStatus.TimedOut or WorkflowInstanceStatus.Cancelled or
            WorkflowInstanceStatus.Terminated;

    private sealed class InboxHandoffAttemptException(InboxRecord record, Exception innerException)
        : Exception("The autonomous inbox handoff attempt failed.", innerException)
    {
        internal InboxRecord Record { get; } = record;
    }
}
