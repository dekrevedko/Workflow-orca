using System.Diagnostics;
using System.Text.Json;
using OrcaCore.Abstractions.Durable;
using OrcaCore.Abstractions.Errors;
using OrcaCore.Abstractions.Events;
using OrcaCore.Abstractions.Ids;
using OrcaCore.Abstractions.Instances;
using OrcaCore.Abstractions.Providers;
using OrcaCore.Abstractions.Steps;
using OrcaCore.Core.Definitions;
using OrcaCore.Engine.Durable.Aggregates;
using OrcaCore.Engine.Durable.Execution;

namespace OrcaCore.Engine.Durable.Driver;

/// <summary>
/// One advancement segment (DR-013): starts from the persisted position and executes kernel
/// command cycles sequentially until a suspension point, terminal state, conflict, park, or
/// segment budget. Each kernel command has exactly one atomic durable commit; the segment
/// never awaits external signals in memory.
/// </summary>
internal sealed partial class DurableDriverSegmentRun<TState>(
    IReadOnlyDictionary<string, SequenceNode<TState>> sequencesByPath,
    DurableDriverContext context)
{
    private readonly List<DurableDriverCursor> cursors = [];
    private readonly List<WaitId> pendingCancelWaits = [];
    private readonly List<TimerId> pendingCancelTimers = [];
    private readonly List<WaitId> pendingConsumedResumes = [];
    private readonly Dictionary<WaitId, DurableActiveWait> activeWaits =
        context.Aggregate.WaitState.ActiveWaits.ToDictionary(wait => wait.WaitId);
    private readonly Dictionary<TimerId, DurableActiveTimer> activeTimers =
        context.Aggregate.TimerState.ActiveTimers.ToDictionary(timer => timer.TimerId);
    private readonly Dictionary<WaitId, DurablePendingResume> pendingResumes =
        context.Aggregate.WaitState.PendingResumes.ToDictionary(pending => pending.WaitId);
    private readonly Stopwatch elapsed = Stopwatch.StartNew();

    private TState state = default!;
    private bool initialized;
    private StreamVersion currentVersion = context.Aggregate.StreamVersion;
    private int commandCount;

    internal bool TryLoad(out string? failure, out DurableParkReason parkReason)
    {
        failure = null;
        parkReason = DurableParkReason.RuntimeStateVersion;
        if (context.Envelope is null)
        {
            cursors.Add(new DurableDriverCursor
            {
                CursorId = "root",
                Frames = [new DurableExecutionFrame { SequencePath = "root", SequenceIndex = 0 }]
            });
            return true;
        }

        if (context.Envelope.EnvelopeVersion != DurableExecutionEnvelope.CurrentVersion)
        {
            failure = $"Checkpoint envelope version {context.Envelope.EnvelopeVersion} is not readable " +
                $"by this driver (supported: {DurableExecutionEnvelope.CurrentVersion}).";
            return false;
        }

        foreach (var record in context.Envelope.Position.Cursors)
        {
            var cursor = DurableDriverCursor.FromRecord(record);
            if (!ValidateFrames(cursor, out var frameFailure))
            {
                failure = frameFailure;
                parkReason = DurableParkReason.VersionBinding;
                return false;
            }

            cursors.Add(cursor);
        }

        if (cursors.Count == 0)
        {
            failure = "Checkpoint envelope carries no execution cursors.";
            return false;
        }

        try
        {
            state = context.Serializer.Deserialize<TState>(new SerializedPayload(
                context.Envelope.StateContentType,
                context.Envelope.StatePayload));
        }
        catch (Exception exception)
        {
            failure = $"Business state payload could not be deserialized: {exception.Message}";
            return false;
        }

        initialized = true;
        return true;
    }

    private bool ValidateFrames(DurableDriverCursor cursor, out string? failure)
    {
        failure = null;
        if (cursor.Frames.Count == 0)
        {
            failure = "Persisted cursor has no frames.";
            return false;
        }

        foreach (var frame in cursor.Frames)
        {
            if (!sequencesByPath.TryGetValue(frame.SequencePath, out var sequence))
            {
                failure = $"Persisted position references sequence '{frame.SequencePath}', which does " +
                    "not exist in the bound definition version.";
                return false;
            }

            if (frame.SequenceIndex < 0 || frame.SequenceIndex > sequence.Children.Count)
            {
                failure = $"Persisted position index {frame.SequenceIndex} is outside sequence " +
                    $"'{frame.SequencePath}' (length {sequence.Children.Count}).";
                return false;
            }
        }

        return true;
    }

    internal async Task<DurableSegmentResult> AdvanceAsync(CancellationToken cancellationToken)
    {
        while (true)
        {
            cancellationToken.ThrowIfCancellationRequested();
            MergeCompletedCursors();

            var cursor = NextRunnableCursor();
            if (cursor is null)
            {
                return cursors.All(candidate => candidate.Phase == DurableCursorPhase.Completed)
                    ? DurableSegmentResult.Idle
                    : DurableSegmentResult.Suspended;
            }

            if (commandCount >= context.Budget.MaxCommandsPerSegment ||
                elapsed.Elapsed >= context.Budget.MaxSegmentDuration)
            {
                return new DurableSegmentResult(
                    DurableSegmentOutcome.BudgetExhausted,
                    CommittedProgress: commandCount > 0);
            }

            var segmentResult = await AdvanceCursorAsync(cursor, cancellationToken).ConfigureAwait(false);
            if (segmentResult is not null)
            {
                return segmentResult;
            }
        }
    }

    private DurableDriverCursor? NextRunnableCursor()
    {
        return cursors
            .Where(IsRunnable)
            .OrderBy(cursor => cursor.CursorId, StringComparer.Ordinal)
            .FirstOrDefault();
    }

    private bool IsRunnable(DurableDriverCursor cursor)
    {
        return cursor.Phase switch
        {
            DurableCursorPhase.AtNode => true,
            DurableCursorPhase.Yielded => true,
            DurableCursorPhase.SuspendedOnWait =>
                cursor.WaitId is { } waitId && !activeWaits.ContainsKey(waitId),
            DurableCursorPhase.SuspendedOnTimer =>
                cursor.TimerId is { } timerId && !activeTimers.ContainsKey(timerId),
            DurableCursorPhase.SuspendedOnRetryBackoff =>
                cursor.TimerId is { } retryTimerId && !activeTimers.ContainsKey(retryTimerId),
            DurableCursorPhase.SuspendedOnChildren => HasPendingChildResume(cursor),
            _ => false
        };
    }

    /// <summary>
    /// Advances one cursor by one node (or one suspension resolution). Returns null to keep
    /// the segment going, or a result that ends the whole segment.
    /// </summary>
    private async Task<DurableSegmentResult?> AdvanceCursorAsync(
        DurableDriverCursor cursor,
        CancellationToken cancellationToken)
    {
        switch (cursor.Phase)
        {
            case DurableCursorPhase.SuspendedOnWait:
                ResolveWaitSuspension(cursor);
                return null;
            case DurableCursorPhase.SuspendedOnTimer:
                cursor.TimerId = null;
                cursor.Phase = DurableCursorPhase.AtNode;
                cursor.AdvanceIndex();
                return null;
            case DurableCursorPhase.SuspendedOnRetryBackoff:
                cursor.TimerId = null;
                cursor.RetryNotBefore = null;
                cursor.Phase = DurableCursorPhase.AtNode;
                return null;
            case DurableCursorPhase.SuspendedOnChildren:
                return await ResolveChildSuspensionAsync(cursor, cancellationToken).ConfigureAwait(false);
            case DurableCursorPhase.Yielded:
                cursor.Phase = DurableCursorPhase.AtNode;
                return null;
        }

        var sequence = sequencesByPath[cursor.Top.SequencePath];
        if (cursor.Top.SequenceIndex >= sequence.Children.Count)
        {
            return CompleteFrame(cursor);
        }

        var node = sequence.Children[cursor.Top.SequenceIndex];
        switch (node)
        {
            case InitNode<TState> initNode:
                RunInit(initNode);
                cursor.AdvanceIndex();
                return null;

            case BusinessStepNode<TState> stepNode:
                EnsureInitialized();
                return await RunStepAsync(cursor, stepNode, cancellationToken).ConfigureAwait(false);

            case IfNode<TState> ifNode:
                EnsureInitialized();
                PushFrame(cursor, ifNode.Condition(state) ? ifNode.Then : ifNode.Else, branchKey: null);
                return null;

            case WhileNode<TState> whileNode:
                EnsureInitialized();
                if (whileNode.Condition(state))
                {
                    PushFrame(cursor, whileNode.Body, branchKey: null, loopIteration: 0);
                }
                else
                {
                    cursor.AdvanceIndex();
                }

                return null;

            case ParallelNode<TState> parallelNode:
                EnsureInitialized();
                SplitCursor(cursor, parallelNode.Branches);
                return null;

            case WhenFirstNode<TState> whenFirstNode:
                EnsureInitialized();
                SplitCursor(cursor, whenFirstNode.Branches);
                return null;

            case WaitNode<TState> waitNode:
                EnsureInitialized();
                return await RegisterWaitAsync(
                    cursor,
                    waitNode.EventName,
                    waitNode.CorrelationSelector(state),
                    waitNode.Timeout,
                    cancellationToken).ConfigureAwait(false);

            case DelayNode<TState> delayNode:
                EnsureInitialized();
                return await ScheduleDelayAsync(cursor, delayNode, cancellationToken).ConfigureAwait(false);

            case EndNode<TState> endNode:
                EnsureInitialized();
                return await CompleteWorkflowAsync(cursor, endNode, cancellationToken).ConfigureAwait(false);

            case RunChildNode<TState> runChildNode:
                EnsureInitialized();
                return await RunChildAsync(cursor, runChildNode, cancellationToken).ConfigureAwait(false);

            case RunChildrenNode<TState> runChildrenNode:
                EnsureInitialized();
                return await RunChildrenAsync(cursor, runChildrenNode, cancellationToken).ConfigureAwait(false);

            default:
                return await ParkAsync(
                    DurableParkReason.Poison,
                    $"Node '{node.GetType().Name}' at '{node.NodeId}' is not supported by the durable driver.",
                    cancellationToken).ConfigureAwait(false);
        }
    }

    private void ResolveWaitSuspension(DurableDriverCursor cursor)
    {
        var waitId = cursor.WaitId!.Value;
        if (pendingResumes.TryGetValue(waitId, out var pending))
        {
            if (pending.EventName == DurableResourcePoolState.GrantedEventName)
            {
                // A pool-grant signal re-arms the guarded node instead of advancing past it:
                // the step re-runs and re-attempts its acquisition (at-least-once effects,
                // DR-014). Still-exhausted capacity simply queues again on a fresh wait.
                pendingConsumedResumes.Add(waitId);
                pendingResumes.Remove(waitId);
                cursor.WaitId = null;
                cursor.TimerId = null;
                cursor.Phase = DurableCursorPhase.AtNode;
                return;
            }

            // The wait matched; its envelope feeds the next step and its consumption commits
            // with the next advancement command.
            cursor.ResumeFromWaitId = waitId;
        }

        // No pending resume means the timeout timer won the race and the kernel cancelled
        // the wait atomically (DR-AC-020), or a synchronous grant never registered the
        // pessimistically persisted wait; the cursor continues without a resumed event.
        cursor.WaitId = null;
        cursor.TimerId = null;
        cursor.Phase = DurableCursorPhase.AtNode;
        cursor.AdvanceIndex();
    }

    private void RunInit(InitNode<TState> initNode)
    {
        if (initialized)
        {
            return;
        }

        object? input = null;
        if (context.Aggregate.StartInputPayload is { } payload &&
            context.Aggregate.StartInputContentType is { } contentType &&
            initNode.RehydrateInput is { } rehydrate)
        {
            input = rehydrate(new SerializedPayload(contentType, payload), context.Serializer);
        }

        state = initNode.CreateState(input);
        initialized = true;
    }

    private void EnsureInitialized()
    {
        if (!initialized)
        {
            throw new WorkflowDefinitionException(
                "Workflow execution reached a node before Init created state.");
        }
    }

    private static void PushFrame(
        DurableDriverCursor cursor,
        SequenceNode<TState> sequence,
        string? branchKey,
        int? loopIteration = null)
    {
        cursor.Frames.Add(new DurableExecutionFrame
        {
            SequencePath = sequence.NodeId,
            SequenceIndex = 0,
            LoopIteration = loopIteration,
            BranchKey = branchKey
        });
    }

    private void SplitCursor(DurableDriverCursor cursor, IReadOnlyList<ParallelBranch<TState>> branches)
    {
        cursors.Remove(cursor);
        foreach (var branch in branches)
        {
            cursors.Add(new DurableDriverCursor
            {
                CursorId = branch.Sequence.NodeId,
                Frames =
                [
                    .. cursor.Frames,
                    new DurableExecutionFrame
                    {
                        SequencePath = branch.Sequence.NodeId,
                        SequenceIndex = 0,
                        BranchKey = branch.BranchId.ToString()
                    }
                ]
            });
        }
    }

    /// <summary>
    /// Handles a cursor whose top frame ran past its sequence: pop into the enclosing
    /// container, iterate a While, or complete a parallel branch.
    /// </summary>
    private DurableSegmentResult? CompleteFrame(DurableDriverCursor cursor)
    {
        if (cursor.Frames.Count == 1)
        {
            // Root sequence exhausted without an End node: ephemeral parity keeps the
            // instance as committed; there is no further advancement.
            cursor.Phase = DurableCursorPhase.Completed;
            return null;
        }

        var parentFrame = cursor.Frames[^2];
        var parentSequence = sequencesByPath[parentFrame.SequencePath];
        var containerNode = parentSequence.Children[parentFrame.SequenceIndex];
        switch (containerNode)
        {
            case IfNode<TState>:
                cursor.Frames.RemoveAt(cursor.Frames.Count - 1);
                cursor.AdvanceIndex();
                return null;
            case WhileNode<TState> whileNode:
                var iteration = cursor.Top.LoopIteration ?? 0;
                cursor.Frames.RemoveAt(cursor.Frames.Count - 1);
                if (whileNode.Condition(state))
                {
                    PushFrame(cursor, whileNode.Body, branchKey: null, loopIteration: iteration + 1);
                }
                else
                {
                    cursor.AdvanceIndex();
                }

                return null;
            case ParallelNode<TState>:
            case WhenFirstNode<TState>:
                cursor.Phase = DurableCursorPhase.Completed;
                return null;
            default:
                throw new WorkflowDefinitionException(
                    $"Cursor '{cursor.CursorId}' completed a frame under unsupported container " +
                    $"'{containerNode.GetType().Name}'.");
        }
    }

    /// <summary>
    /// Joins completed branch cursors: Parallel waits for all branches; WhenFirst promotes
    /// the first completed branch and cancels the rest (their waits/timers are released on
    /// the next commit).
    /// </summary>
    private void MergeCompletedCursors()
    {
        while (true)
        {
            var madeProgress = false;
            var completedCandidates = cursors
                .Where(cursor => cursor.Phase == DurableCursorPhase.Completed && cursor.Frames.Count > 1)
                .ToArray();
            foreach (var completed in completedCandidates)
            {
                var parentFrame = completed.Frames[^2];
                var parentSequence = sequencesByPath[parentFrame.SequencePath];
                var containerNode = parentSequence.Children[parentFrame.SequenceIndex];
                var siblings = cursors
                    .Where(cursor =>
                        cursor.Frames.Count == completed.Frames.Count &&
                        cursor.Frames.Count >= 2 &&
                        FramesEqual(cursor.Frames[^2], parentFrame) &&
                        FramePrefixesEqual(cursor, completed))
                    .ToArray();

                switch (containerNode)
                {
                    case ParallelNode<TState> parallelNode:
                        if (siblings.Length < parallelNode.Branches.Count ||
                            siblings.Any(sibling => sibling.Phase != DurableCursorPhase.Completed))
                        {
                            continue;
                        }

                        Join(siblings, completed);
                        madeProgress = true;
                        break;
                    case WhenFirstNode<TState>:
                        var ownedCursors = CursorsOwnedByContainer(completed).ToArray();
                        foreach (var loser in ownedCursors.Where(candidate => candidate != completed))
                        {
                            ReleaseCursorObligations(loser);
                        }

                        Join(ownedCursors, completed);
                        madeProgress = true;
                        break;
                    default:
                        continue;
                }

                if (madeProgress)
                {
                    break;
                }
            }

            if (!madeProgress)
            {
                return;
            }
        }
    }

    private IEnumerable<DurableDriverCursor> CursorsOwnedByContainer(DurableDriverCursor completed)
    {
        var containerFrameIndex = completed.Frames.Count - 2;
        return cursors.Where(candidate =>
            candidate.Frames.Count > containerFrameIndex + 1 &&
            FramesMatchThrough(candidate, completed, containerFrameIndex));
    }

    private static bool FramesMatchThrough(
        DurableDriverCursor candidate,
        DurableDriverCursor completed,
        int inclusiveFrameIndex)
    {
        for (var index = 0; index <= inclusiveFrameIndex; index++)
        {
            if (!FramesEqual(candidate.Frames[index], completed.Frames[index]))
            {
                return false;
            }
        }

        return true;
    }

    private void Join(IReadOnlyList<DurableDriverCursor> group, DurableDriverCursor representative)
    {
        foreach (var member in group)
        {
            cursors.Remove(member);
        }

        var mergedFrames = representative.Frames.Take(representative.Frames.Count - 1).ToList();
        var merged = new DurableDriverCursor
        {
            CursorId = mergedFrames.Count == 1 ? "root" : mergedFrames[^1].SequencePath,
            Frames = mergedFrames
        };
        merged.AdvanceIndex();
        cursors.Add(merged);
    }

    private void ReleaseCursorObligations(DurableDriverCursor loser)
    {
        if (loser.WaitId is { } waitId)
        {
            if (activeWaits.TryGetValue(waitId, out var wait))
            {
                pendingCancelWaits.Add(waitId);
                if (wait.TimeoutTimerId is { } timeoutTimerId && activeTimers.ContainsKey(timeoutTimerId))
                {
                    pendingCancelTimers.Add(timeoutTimerId);
                }
            }
            else if (pendingResumes.ContainsKey(waitId))
            {
                pendingConsumedResumes.Add(waitId);
            }
        }

        if (loser.TimerId is { } timerId && activeTimers.ContainsKey(timerId))
        {
            pendingCancelTimers.Add(timerId);
        }

        if (loser.ResumeFromWaitId is { } resumeWaitId && pendingResumes.ContainsKey(resumeWaitId))
        {
            pendingConsumedResumes.Add(resumeWaitId);
        }
    }

    private static bool FramesEqual(DurableExecutionFrame left, DurableExecutionFrame right)
    {
        return left.SequencePath == right.SequencePath && left.SequenceIndex == right.SequenceIndex;
    }

    private static bool FramePrefixesEqual(DurableDriverCursor left, DurableDriverCursor right)
    {
        for (var index = 0; index < left.Frames.Count - 1; index++)
        {
            if (!FramesEqual(left.Frames[index], right.Frames[index]))
            {
                return false;
            }
        }

        return true;
    }

    private async Task<DurableSegmentResult?> RegisterWaitAsync(
        DurableDriverCursor cursor,
        string eventName,
        CorrelationId correlationId,
        TimeSpan? timeout,
        CancellationToken cancellationToken,
        WaitId? consumedWaitId = null)
    {
        var waitId = WaitId.New();
        var timerId = timeout is null ? (TimerId?)null : TimerId.New();
        cursor.Phase = DurableCursorPhase.SuspendedOnWait;
        cursor.WaitId = waitId;
        cursor.TimerId = timerId;
        cursor.ResumeFromWaitId = null;

        var now = context.TimeProvider.GetUtcNow();
        var command = new DurableWaitRegisteredCommand(
            CommandId.New(),
            context.InstanceId,
            now,
            waitId,
            eventName,
            correlationId,
            WaitMode.Resident,
            BranchScope(cursor))
        {
            Envelope = BuildEnvelope(),
            TimeoutTimerId = timerId,
            TimeoutFireAt = timeout is { } duration ? now.Add(duration) : null,
            ExpectedStreamVersion = currentVersion,
            ConsumedResumeWaitIds = TakeConsumedResumes(consumedWaitId),
            CancelWaitIds = TakeCancelWaits(),
            CancelTimerIds = TakeCancelTimers()
        };
        var failure = await CommitAsync(
            () => context.Processor.ProcessAsync(command, CancellationToken.None),
            command.ConsumedResumeWaitIds,
            command.CancelWaitIds,
            command.CancelTimerIds).ConfigureAwait(false);
        if (failure is not null)
        {
            return failure;
        }

        activeWaits[waitId] = new DurableActiveWait(
            waitId,
            eventName,
            correlationId,
            now,
            WaitMode.Resident,
            BranchScope(cursor),
            timerId);
        if (timerId is { } registeredTimerId)
        {
            activeTimers[registeredTimerId] = new DurableActiveTimer(
                registeredTimerId,
                now.Add(timeout!.Value),
                $"wait-timeout:{waitId}",
                now);
        }

        // A buffered delivery may have matched the wait inside the same commit; refresh the
        // local match view so the cursor resumes within this segment.
        await RefreshPendingResumesAsync(waitId, cancellationToken).ConfigureAwait(false);
        return null;
    }

    /// <summary>
    /// After a wait registration, the kernel may have matched a buffered delivery in the
    /// same decision. The interpreter must observe that committed fact deterministically,
    /// so it reloads the instance's pending resumes for the registered wait.
    /// </summary>
    private async Task RefreshPendingResumesAsync(WaitId waitId, CancellationToken cancellationToken)
    {
        var reloaded = await new DurableAggregateLoader(context.Processor.EventStore)
            .LoadAsync(context.InstanceId, cancellationToken)
            .ConfigureAwait(false);
        if (reloaded.StreamVersion != currentVersion)
        {
            // A foreign commit interleaved; the segment continues from its local view and
            // the expected-version guard resolves any divergence on the next command.
            return;
        }

        if (reloaded.WaitState.FindPendingResume(waitId) is { } pending)
        {
            pendingResumes[pending.WaitId] = pending;
            activeWaits.Remove(waitId);
        }
    }

    private async Task<DurableSegmentResult?> ScheduleDelayAsync(
        DurableDriverCursor cursor,
        DelayNode<TState> delayNode,
        CancellationToken cancellationToken)
    {
        var timerId = TimerId.New();
        cursor.Phase = DurableCursorPhase.SuspendedOnTimer;
        cursor.TimerId = timerId;

        var now = context.TimeProvider.GetUtcNow();
        var command = new ScheduleTimerCommand
        {
            CommandId = CommandId.New(),
            InstanceId = context.InstanceId,
            RequestedAt = now,
            TimerId = timerId,
            FireAt = now.Add(delayNode.Duration),
            WakeupName = delayNode.NodeId,
            Envelope = BuildEnvelope(),
            ExpectedStreamVersion = currentVersion
        };
        var failure = await CommitAsync(
            () => context.Processor.ProcessAsync(command, CancellationToken.None),
            [], [], []).ConfigureAwait(false);
        if (failure is not null)
        {
            return failure;
        }

        activeTimers[timerId] = new DurableActiveTimer(timerId, command.FireAt, delayNode.NodeId, now);
        return null;
    }

    private async Task<DurableSegmentResult?> CompleteWorkflowAsync(
        DurableDriverCursor cursor,
        EndNode<TState> endNode,
        CancellationToken cancellationToken)
    {
        if (cursors.Any(candidate => candidate != cursor && candidate.Phase != DurableCursorPhase.Completed))
        {
            var failCommand = new DurableFailCommand(
                CommandId.New(),
                context.InstanceId,
                context.TimeProvider.GetUtcNow(),
                "Workflow cannot complete with unresolved runtime work.",
                BuildEnvelope())
            {
                ExpectedStreamVersion = currentVersion
            };
            var failFailure = await CommitAsync(
                () => context.Processor.ProcessAsync(failCommand, CancellationToken.None),
                [], [], []).ConfigureAwait(false);
            return failFailure ?? DurableSegmentResult.Terminal;
        }

        cursor.Phase = DurableCursorPhase.Completed;
        var command = new DurableCompleteCommand(
            CommandId.New(),
            context.InstanceId,
            context.TimeProvider.GetUtcNow(),
            endNode.OutcomeName,
            BuildEnvelope())
        {
            ExpectedStreamVersion = currentVersion,
            ConsumedResumeWaitIds = TakeConsumedResumes(cursor.ResumeFromWaitId),
            CancelWaitIds = TakeCancelWaits(),
            CancelTimerIds = TakeCancelTimers()
        };
        var failure = await CommitAsync(
            () => context.Processor.ProcessAsync(command, CancellationToken.None),
            command.ConsumedResumeWaitIds,
            command.CancelWaitIds,
            command.CancelTimerIds).ConfigureAwait(false);
        return failure ?? DurableSegmentResult.Terminal;
    }

    internal async Task<DurableSegmentResult> ParkAsync(
        DurableParkReason reason,
        string errorSummary,
        CancellationToken cancellationToken)
    {
        var command = new DurableParkCommand(
            CommandId.New(),
            context.InstanceId,
            context.TimeProvider.GetUtcNow(),
            reason,
            errorSummary,
            FailedAttemptCount: 1,
            context.Envelope is null ? null : context.Aggregate.StreamVersion)
        {
            ExpectedStreamVersion = currentVersion
        };
        await context.Processor.ProcessAsync(command, cancellationToken).ConfigureAwait(false);
        return new DurableSegmentResult(DurableSegmentOutcome.Parked, errorSummary);
    }

    private DurableExecutionEnvelope BuildEnvelope()
    {
        var serialized = context.Serializer.Serialize(state);
        return new DurableExecutionEnvelope
        {
            EnvelopeVersion = DurableExecutionEnvelope.CurrentVersion,
            Position = new DurableExecutionPosition
            {
                Cursors = cursors
                    .OrderBy(cursor => cursor.CursorId, StringComparer.Ordinal)
                    .Select(cursor => cursor.ToRecord())
                    .ToArray()
            },
            StateContentType = serialized.ContentType,
            StatePayload = serialized.Payload
        };
    }

    private static string? BranchScope(DurableDriverCursor cursor)
    {
        for (var index = cursor.Frames.Count - 1; index >= 0; index--)
        {
            if (cursor.Frames[index].BranchKey is { } branchKey)
            {
                return branchKey;
            }
        }

        return null;
    }

    private IReadOnlyList<WaitId> TakeConsumedResumes(WaitId? consumedWaitId)
    {
        var consumed = new List<WaitId>(pendingConsumedResumes);
        pendingConsumedResumes.Clear();
        if (consumedWaitId is { } waitId && !consumed.Contains(waitId))
        {
            consumed.Add(waitId);
        }

        return consumed;
    }

    private IReadOnlyList<WaitId> TakeCancelWaits()
    {
        var cancels = pendingCancelWaits.ToArray();
        pendingCancelWaits.Clear();
        return cancels;
    }

    private IReadOnlyList<TimerId> TakeCancelTimers()
    {
        var cancels = pendingCancelTimers.ToArray();
        pendingCancelTimers.Clear();
        return cancels;
    }

    /// <summary>
    /// Issues one kernel command (one atomic durable commit, DR-013) and folds the outcome
    /// into the segment: committed advances the tracked stream version and the local fact
    /// views; anything else aborts the segment as a conflict for reload-retry (DR-030).
    /// </summary>
    private async Task<DurableSegmentResult?> CommitAsync(
        Func<Task<DurableCommandResult>> send,
        IReadOnlyList<WaitId> consumedResumes,
        IReadOnlyList<WaitId> cancelledWaits,
        IReadOnlyList<TimerId> cancelledTimers)
    {
        var result = await send().ConfigureAwait(false);
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

        foreach (var waitId in cancelledWaits)
        {
            if (activeWaits.Remove(waitId, out var wait) &&
                wait.TimeoutTimerId is { } timeoutTimerId)
            {
                activeTimers.Remove(timeoutTimerId);
            }

            pendingResumes.Remove(waitId);
        }

        foreach (var timerId in cancelledTimers)
        {
            activeTimers.Remove(timerId);
        }

        return null;
    }
}
