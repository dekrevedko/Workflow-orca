using System.Text.Json;
using OrcaCore.Abstractions.Durable;
using OrcaCore.Abstractions.Errors;
using OrcaCore.Abstractions.Events;
using OrcaCore.Abstractions.Ids;
using OrcaCore.Abstractions.Providers;
using OrcaCore.Abstractions.Steps;
using OrcaCore.Core.Definitions;
using OrcaCore.Engine.Durable.Aggregates;
using OrcaCore.Engine.Durable.Execution;

namespace OrcaCore.Engine.Durable.Driver;

/// <summary>
/// Business-step execution for one advancement segment: policy-aware step invocation and the
/// mapping from step results to kernel commands (DR-011/DR-014).
/// </summary>
internal sealed partial class DurableDriverSegmentRun<TState>
{
    private async Task<DurableSegmentResult?> RunStepAsync(
        DurableDriverCursor cursor,
        BusinessStepNode<TState> stepNode,
        CancellationToken cancellationToken)
    {
        if (stepNode.Policies.Timeout is { } timeout && cursor.TimeoutDeadline is null)
        {
            return await CommitTimeoutAdmissionAsync(cursor, stepNode, timeout, cancellationToken)
                .ConfigureAwait(false);
        }

        var resumedEvent = TakeResumedEvent(cursor, out var consumedWaitId);
        var execution = await ExecuteStepBodyAsync(cursor, stepNode, resumedEvent, cancellationToken)
            .ConfigureAwait(false);
        if (execution.NextRetryAttempt is { } nextRetryAttempt)
        {
            return await ScheduleRetryAsync(cursor, stepNode, nextRetryAttempt, cancellationToken)
                .ConfigureAwait(false);
        }

        if (execution.OperatorCancelled)
        {
            ClearStepPolicyState(cursor);
            return await CommitOperatorCancellationAsync(cancellationToken).ConfigureAwait(false);
        }

        var result = execution.Result!;
        ClearStepPolicyState(cursor);

        switch (result)
        {
            case StepResult.Completed:
            {
                cursor.AdvanceIndex();
                cursor.ResumeFromWaitId = null;
                var command = new DurableStepCompletedCommand(
                    CommandId.New(),
                    context.InstanceId,
                    context.TimeProvider.GetUtcNow(),
                    stepNode.NodeId,
                    BuildEnvelope())
                {
                    ExpectedStreamVersion = currentVersion,
                    ConsumedResumeWaitIds = TakeConsumedResumes(consumedWaitId),
                    CancelWaitIds = TakeCancelWaits(),
                    CancelTimerIds = TakeCancelTimers()
                };
                return await CommitAsync(
                    () => context.Processor.ProcessAsync(command, CancellationToken.None),
                    command.ConsumedResumeWaitIds,
                    command.CancelWaitIds,
                    command.CancelTimerIds).ConfigureAwait(false);
            }

            case StepResult.Failed failed:
            {
                var command = new DurableStepFailedCommand(
                    CommandId.New(),
                    context.InstanceId,
                    context.TimeProvider.GetUtcNow(),
                    stepNode.NodeId,
                    $"{failed.Error.GetType().Name}: {failed.Error.Message}",
                    initialized ? BuildEnvelope() : null)
                {
                    ExpectedStreamVersion = currentVersion
                };
                var failure = await CommitAsync(
                    () => context.Processor.ProcessAsync(command, CancellationToken.None),
                    [], [], []).ConfigureAwait(false);
                return failure ?? DurableSegmentResult.Terminal;
            }

            case StepResult.WaitForEvent wait:
                return await RegisterWaitAsync(
                    cursor,
                    wait.EventName,
                    wait.CorrelationId,
                    timeout: null,
                    cancellationToken,
                    consumedWaitId).ConfigureAwait(false);

            case StepResult.RunExternalJob externalJob:
                return await RunExternalJobAsync(cursor, externalJob, consumedWaitId, cancellationToken)
                    .ConfigureAwait(false);

            case StepResult.AcquireResources acquire:
                return await AcquireResourcesAsync(cursor, acquire, consumedWaitId, cancellationToken)
                    .ConfigureAwait(false);

            case StepResult.Yield:
            {
                cursor.YieldCount++;
                cursor.Phase = DurableCursorPhase.Yielded;
                cursor.ResumeFromWaitId = null;
                var command = new DurableYieldCommand(
                    CommandId.New(),
                    context.InstanceId,
                    context.TimeProvider.GetUtcNow(),
                    stepNode.NodeId,
                    BuildEnvelope())
                {
                    ExpectedStreamVersion = currentVersion,
                    ConsumedResumeWaitIds = TakeConsumedResumes(consumedWaitId)
                };
                var failure = await CommitAsync(
                    () => context.Processor.ProcessAsync(command, CancellationToken.None),
                    command.ConsumedResumeWaitIds,
                    [], []).ConfigureAwait(false);
                return failure ?? DurableSegmentResult.Yielded;
            }

            case StepResult.ContinueAsNew<TState> continueAsNew:
                return await ContinueAsNewAsync(continueAsNew, cancellationToken).ConfigureAwait(false);

            default:
                throw new NotSupportedException(
                    $"Step result '{result.GetType().Name}' is not supported by the durable driver.");
        }
    }

    private async Task<DurableSegmentResult?> ContinueAsNewAsync(
        StepResult.ContinueAsNew<TState> continueAsNew,
        CancellationToken cancellationToken)
    {
        state = continueAsNew.State;
        initialized = true;
        cursors.Clear();
        var root = sequencesByPath["root"];
        var firstGenerationNode = root.Children.Count > 0 && root.Children[0] is InitNode<TState>
            ? 1
            : 0;
        cursors.Add(new DurableDriverCursor
        {
            CursorId = "root",
            Frames =
            [
                new DurableExecutionFrame
                {
                    SequencePath = "root",
                    SequenceIndex = firstGenerationNode
                }
            ]
        });

        var serialized = context.Serializer.Serialize(state);
        var command = new ContinueAsNewCommand
        {
            CommandId = CommandId.New(),
            InstanceId = context.InstanceId,
            RequestedAt = context.TimeProvider.GetUtcNow(),
            StateContentType = serialized.ContentType,
            StatePayload = serialized.Payload,
            Envelope = BuildEnvelope(),
            ExpectedStreamVersion = currentVersion
        };
        var failure = await CommitAsync(
            () => context.Processor.ProcessAsync(command, CancellationToken.None),
            [],
            [],
            []).ConfigureAwait(false);
        return failure ?? DurableSegmentResult.ContinuedAsNew;
    }

    /// <summary>
    /// Dispatches external work and suspends the cursor on the completion wait (or the queued
    /// pool-acquisition wait). The cursor stays at the step node: an
    /// <c>ExternalJobCompleted</c> resume advances past it feeding the completion event to the
    /// next step, while a <c>ResourcePoolGranted</c> signal re-runs it (DR-031 triggers).
    /// </summary>
    private async Task<DurableSegmentResult?> RunExternalJobAsync(
        DurableDriverCursor cursor,
        StepResult.RunExternalJob externalJob,
        WaitId? consumedWaitId,
        CancellationToken cancellationToken)
    {
        var waitId = WaitId.New();
        cursor.Phase = DurableCursorPhase.SuspendedOnWait;
        cursor.WaitId = waitId;
        cursor.TimerId = null;
        cursor.ResumeFromWaitId = null;

        var now = context.TimeProvider.GetUtcNow();
        var command = new RunExternalJobCommand
        {
            CommandId = CommandId.New(),
            InstanceId = context.InstanceId,
            RequestedAt = now,
            ExternalJobId = externalJob.ExternalJobId,
            Payload = externalJob.Payload,
            Requirements = externalJob.Requirements ?? [],
            TimeoutAt = externalJob.Timeout is { } timeout ? now.Add(timeout) : null,
            WaitId = waitId,
            Envelope = BuildEnvelope(),
            ExpectedStreamVersion = currentVersion,
            ConsumedResumeWaitIds = TakeConsumedResumes(consumedWaitId)
        };
        var result = await context.Processor.ProcessAsync(command, CancellationToken.None).ConfigureAwait(false);
        if (result.Outcome == DurableCommandOutcome.NoOp)
        {
            // The job already exists from an earlier at-least-once attempt: suspend on the
            // committed job wait instead of a fresh one.
            var reloaded = await ReloadAtCurrentVersionAsync(cancellationToken).ConfigureAwait(false);
            if (reloaded?.ExternalJobState.Find(externalJob.ExternalJobId) is { } existing)
            {
                cursor.WaitId = existing.WaitId;
                return null;
            }

            return await ParkAsync(
                DurableParkReason.Poison,
                $"External job '{externalJob.ExternalJobId}' was rejected by the kernel " +
                "(resource requirements rejected or instance not runnable).",
                cancellationToken).ConfigureAwait(false);
        }

        var failure = FoldCommit(result, command.ConsumedResumeWaitIds);
        if (failure is not null)
        {
            return failure;
        }

        await RegisterCommittedWaitViewAsync(waitId, cancellationToken).ConfigureAwait(false);
        return null;
    }

    /// <summary>
    /// Acquires durable pool tickets for a guarded holder. Granted acquisitions continue the
    /// segment; exhausted capacity queues durably on the driver-chosen wait, and the grant
    /// signal later re-runs this node to re-attempt the acquisition (DR-031).
    /// </summary>
    private async Task<DurableSegmentResult?> AcquireResourcesAsync(
        DurableDriverCursor cursor,
        StepResult.AcquireResources acquire,
        WaitId? consumedWaitId,
        CancellationToken cancellationToken)
    {
        var waitId = WaitId.New();
        // The envelope is built pessimistically suspended: if the acquisition is granted the
        // wait never registers, and a restart resumes through the no-active-wait path, which
        // advances — the same outcome the in-segment path takes below.
        cursor.Phase = DurableCursorPhase.SuspendedOnWait;
        cursor.WaitId = waitId;
        cursor.TimerId = null;
        cursor.ResumeFromWaitId = null;

        var now = context.TimeProvider.GetUtcNow();
        var command = new AcquireResourcePoolCommand
        {
            CommandId = CommandId.New(),
            InstanceId = context.InstanceId,
            RequestedAt = now,
            HolderKey = acquire.HolderKey,
            Requirements = acquire.Requirements,
            ExpiresAt = acquire.LeaseDuration is { } lease ? now.Add(lease) : null,
            WaitId = waitId,
            Envelope = BuildEnvelope(),
            ExpectedStreamVersion = currentVersion,
            ConsumedResumeWaitIds = TakeConsumedResumes(consumedWaitId)
        };
        var result = await context.Processor.ProcessAsync(command, CancellationToken.None).ConfigureAwait(false);
        if (result.Outcome == DurableCommandOutcome.NoOp)
        {
            return await ParkAsync(
                DurableParkReason.Poison,
                $"Resource-pool acquisition for holder '{acquire.HolderKey}' was rejected; the " +
                "requirements can never be granted.",
                cancellationToken).ConfigureAwait(false);
        }

        var failure = FoldCommit(result, command.ConsumedResumeWaitIds);
        if (failure is not null)
        {
            return failure;
        }

        var suspended = await RegisterCommittedWaitViewAsync(waitId, cancellationToken).ConfigureAwait(false);
        if (!suspended)
        {
            // Granted synchronously: no wait registered, the tickets are committed and the
            // cursor continues past the guarded node within this segment.
            cursor.WaitId = null;
            cursor.Phase = DurableCursorPhase.AtNode;
            cursor.AdvanceIndex();
        }

        return null;
    }

    /// <summary>
    /// Folds a directly issued kernel command outcome into the segment bookkeeping, mirroring
    /// <see cref="CommitAsync"/> for commands whose no-op outcome needs special handling.
    /// </summary>
    private DurableSegmentResult? FoldCommit(
        DurableCommandResult result,
        IReadOnlyList<WaitId> consumedResumes)
    {
        if (result.Outcome != DurableCommandOutcome.Committed)
        {
            return new DurableSegmentResult(
                DurableSegmentOutcome.Conflict,
                result.Message ?? $"Kernel command outcome {result.Outcome}.");
        }

        currentVersion = result.StreamVersion;
        commandCount++;
        foreach (var waitId in consumedResumes)
        {
            pendingResumes.Remove(waitId);
        }

        return null;
    }

    /// <summary>
    /// Reloads the committed aggregate and mirrors the wait registered under
    /// <paramref name="waitId"/> into the segment's local view; returns false when no such
    /// wait committed (a synchronous grant). A foreign interleaving commit leaves the local
    /// view untouched — the cursor stays suspended and a later drive reloads everything.
    /// </summary>
    private async Task<bool> RegisterCommittedWaitViewAsync(WaitId waitId, CancellationToken cancellationToken)
    {
        var reloaded = await ReloadAtCurrentVersionAsync(cancellationToken).ConfigureAwait(false);
        if (reloaded is null)
        {
            return true;
        }

        if (reloaded.WaitState.ActiveWaits.FirstOrDefault(wait => wait.WaitId == waitId) is { } committedWait)
        {
            activeWaits[waitId] = committedWait;
            return true;
        }

        return false;
    }

    private async Task<DurableWorkflowAggregate?> ReloadAtCurrentVersionAsync(CancellationToken cancellationToken)
    {
        var reloaded = await new DurableAggregateLoader(context.Processor.EventStore)
            .LoadAsync(context.InstanceId, cancellationToken)
            .ConfigureAwait(false);
        return reloaded.StreamVersion == currentVersion ? reloaded : null;
    }

    /// <summary>
    /// Executes the user step with the ephemeral engine's policy semantics: bounded retry
    /// attempts and a TimeProvider-driven timeout. Effects are at-least-once (DR-014).
    /// </summary>
    private async Task<DurableStepBodyResult> ExecuteStepBodyAsync(
        DurableDriverCursor cursor,
        BusinessStepNode<TState> stepNode,
        EventEnvelope? resumedEvent,
        CancellationToken cancellationToken)
    {
        var committedState = context.Serializer.Serialize(state);
        using var stepCancellation = stepNode.Policies.Cancellation
            ? context.Processor.EnterStep(context.InstanceId, cancellationToken)
            : null;
        var policyToken = stepCancellation?.Token ?? cancellationToken;
        var timedOut = 0;
        var remainingTimeout = cursor.TimeoutDeadline is { } deadline
            ? deadline - context.TimeProvider.GetUtcNow()
            : (TimeSpan?)null;
        if (remainingTimeout <= TimeSpan.Zero)
        {
            RestoreCommittedState(committedState);
            return DurableStepBodyResult.Completed(new StepResult.Failed(
                new WorkflowLifecycleException(
                    $"Step '{stepNode.NodeId}' timed out at {cursor.TimeoutDeadline:O}.")));
        }

        using var timeoutCancellation = CancellationTokenSource.CreateLinkedTokenSource(policyToken);
        using var timeoutTimer = remainingTimeout is { } remaining
            ? context.TimeProvider.CreateTimer(
                _ =>
                {
                    Interlocked.Exchange(ref timedOut, 1);
                    _ = timeoutCancellation.CancelAsync();
                },
                null,
                remaining,
                Timeout.InfiniteTimeSpan)
            : null;
        var executionToken = timeoutTimer is null ? policyToken : timeoutCancellation.Token;
        var maxAttempts = stepNode.Policies.Retry?.MaxAttempts ?? 1;
        var step = stepNode.StepFactory();
        var firstAttempt = cursor.RetryAttempt > 0 ? cursor.RetryAttempt : 1;
        cursor.LogicalOperationKey ??= CreateLogicalOperationKey(cursor);

        for (var attempt = firstAttempt; attempt <= maxAttempts; attempt++)
        {
            try
            {
                var stepContext = new StepContext<TState>(state, resumedEvent, context.TimeProvider);
                var result = await step.ExecuteAsync(stepContext, executionToken).ConfigureAwait(false);
                if (result is StepResult.Failed && attempt < maxAttempts)
                {
                    RestoreCommittedState(committedState);
                    if (stepNode.Policies.Retry is { } retry && retry.Backoff > TimeSpan.Zero)
                    {
                        return DurableStepBodyResult.Retry(attempt + 1);
                    }

                    continue;
                }

                if (result is StepResult.Failed)
                {
                    RestoreCommittedState(committedState);
                }

                return DurableStepBodyResult.Completed(result);
            }
            catch (OperationCanceledException) when (
                stepCancellation?.OperatorCancellationRequested == true &&
                !cancellationToken.IsCancellationRequested)
            {
                RestoreCommittedState(committedState);
                return DurableStepBodyResult.Cancelled();
            }
            catch (OperationCanceledException) when (
                Volatile.Read(ref timedOut) != 0 &&
                !cancellationToken.IsCancellationRequested)
            {
                RestoreCommittedState(committedState);
                return DurableStepBodyResult.Completed(new StepResult.Failed(
                    new WorkflowLifecycleException(
                        $"Step '{stepNode.NodeId}' timed out after {stepNode.Policies.Timeout!.Duration}.")));
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                if (attempt < maxAttempts)
                {
                    RestoreCommittedState(committedState);
                    if (stepNode.Policies.Retry is { } retry && retry.Backoff > TimeSpan.Zero)
                    {
                        return DurableStepBodyResult.Retry(attempt + 1);
                    }

                    continue;
                }

                RestoreCommittedState(committedState);
                return DurableStepBodyResult.Completed(new StepResult.Failed(
                    new WorkflowLifecycleException($"{exception.GetType().Name}: {exception.Message}")));
            }
        }

        return DurableStepBodyResult.Completed(new StepResult.Failed(
            new WorkflowLifecycleException($"Step '{stepNode.NodeId}' exhausted its retry attempts.")));
    }

    private async Task<DurableSegmentResult?> ScheduleRetryAsync(
        DurableDriverCursor cursor,
        BusinessStepNode<TState> stepNode,
        int nextAttempt,
        CancellationToken cancellationToken)
    {
        var retry = stepNode.Policies.Retry!;
        var timerId = TimerId.New();
        var now = context.TimeProvider.GetUtcNow();
        var retryAt = now.Add(retry.Backoff);
        cursor.RetryAttempt = nextAttempt;
        cursor.RetryNotBefore = retryAt;
        cursor.Phase = DurableCursorPhase.SuspendedOnRetryBackoff;
        cursor.TimerId = timerId;

        var command = new ScheduleTimerCommand
        {
            CommandId = CommandId.New(),
            InstanceId = context.InstanceId,
            RequestedAt = now,
            TimerId = timerId,
            FireAt = retryAt,
            WakeupName = $"{stepNode.NodeId}:retry:{nextAttempt}",
            Envelope = BuildEnvelope(),
            ExpectedStreamVersion = currentVersion
        };
        var failure = await CommitAsync(
            () => context.Processor.ProcessAsync(command, CancellationToken.None),
            [],
            [],
            []).ConfigureAwait(false);
        if (failure is not null)
        {
            return failure;
        }

        activeTimers[timerId] = new DurableActiveTimer(timerId, retryAt, command.WakeupName, now);
        return null;
    }

    private async Task<DurableSegmentResult?> CommitTimeoutAdmissionAsync(
        DurableDriverCursor cursor,
        BusinessStepNode<TState> stepNode,
        TimeoutPolicy timeout,
        CancellationToken cancellationToken)
    {
        cursor.TimeoutDeadline = context.TimeProvider.GetUtcNow().Add(timeout.Duration);
        cursor.LogicalOperationKey ??= CreateLogicalOperationKey(cursor);
        cursor.Phase = DurableCursorPhase.Yielded;
        var command = new DurableYieldCommand(
            CommandId.New(),
            context.InstanceId,
            context.TimeProvider.GetUtcNow(),
            $"{stepNode.NodeId}:timeout-admission",
            BuildEnvelope())
        {
            ExpectedStreamVersion = currentVersion
        };
        var failure = await CommitAsync(
            () => context.Processor.ProcessAsync(command, CancellationToken.None),
            [],
            [],
            []).ConfigureAwait(false);
        return failure ?? DurableSegmentResult.PolicyBoundary;
    }

    private async Task<DurableSegmentResult> CommitOperatorCancellationAsync(
        CancellationToken cancellationToken)
    {
        var result = await context.Processor.ProcessAsync(
            new CancelWorkflowCommand
            {
                CommandId = CommandId.New(),
                InstanceId = context.InstanceId,
                RequestedAt = context.TimeProvider.GetUtcNow()
            },
            CancellationToken.None).ConfigureAwait(false);
        return result.Outcome is DurableCommandOutcome.Committed or DurableCommandOutcome.NoOp
            ? DurableSegmentResult.Terminal
            : new DurableSegmentResult(
                DurableSegmentOutcome.Conflict,
                result.Message ?? "Operator cancellation commit conflicted.");
    }

    private string CreateLogicalOperationKey(DurableDriverCursor cursor)
    {
        return string.Join(
            ':',
            context.Aggregate.ContinueAsNewGeneration,
            cursor.CursorId,
            cursor.Top.SequencePath,
            cursor.Top.SequenceIndex);
    }

    private static void ClearStepPolicyState(DurableDriverCursor cursor)
    {
        cursor.RetryAttempt = 0;
        cursor.RetryNotBefore = null;
        cursor.LogicalOperationKey = null;
        cursor.TimeoutDeadline = null;
    }

    private sealed record DurableStepBodyResult(
        StepResult? Result,
        int? NextRetryAttempt,
        bool OperatorCancelled)
    {
        internal static DurableStepBodyResult Completed(StepResult result) => new(result, null, false);

        internal static DurableStepBodyResult Retry(int nextAttempt) => new(null, nextAttempt, false);

        internal static DurableStepBodyResult Cancelled() => new(null, null, true);
    }

    private void RestoreCommittedState(SerializedPayload committedState)
    {
        state = context.Serializer.Deserialize<TState>(committedState);
    }

    private EventEnvelope? TakeResumedEvent(DurableDriverCursor cursor, out WaitId? consumedWaitId)
    {
        consumedWaitId = null;
        if (cursor.ResumeFromWaitId is not { } waitId ||
            !pendingResumes.TryGetValue(waitId, out var pending))
        {
            cursor.ResumeFromWaitId = null;
            return null;
        }

        consumedWaitId = waitId;
        return new EventEnvelope
        {
            EventId = pending.MatchedEventId,
            EventName = pending.EventName ?? string.Empty,
            // EventEnvelope requires a correlation; a broadcast wait resumes under a sentinel.
            CorrelationId = pending.CorrelationId ?? new CorrelationId("(uncorrelated)"),
            BranchId = pending.BranchId,
            Payload = ToResumedPayload(pending.Payload),
            PayloadContentType = pending.PayloadContentType,
            OccurredAt = pending.MatchedAt
        };
    }

    /// <summary>
    /// Durable resumes always observe the serialized payload form (a JsonElement for JSON
    /// payloads), with or without a restart in between, so behavior is restart-invariant.
    /// </summary>
    private static object? ToResumedPayload(byte[]? payload)
    {
        if (payload is null || payload.Length == 0)
        {
            return null;
        }

        try
        {
            using var document = JsonDocument.Parse(payload);
            return document.RootElement.Clone();
        }
        catch (JsonException)
        {
            return payload;
        }
    }

}
