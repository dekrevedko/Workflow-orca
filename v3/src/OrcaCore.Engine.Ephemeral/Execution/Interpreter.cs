using System.Diagnostics;
using OrcaCore.Abstractions.Events;
using OrcaCore.Abstractions.Ids;
using OrcaCore.Abstractions.Instances;
using OrcaCore.Abstractions.Steps;
using OrcaCore.Core.Definitions;
using OrcaCore.Core.Lifecycle;

namespace OrcaCore.Engine.Ephemeral.Execution;

/// <summary>
/// Walks a <see cref="WorkflowDefinition{TState}"/> body — <c>Init</c> (already applied by the
/// engine facade before this runs) → business steps, <c>If</c>/<c>While</c> control flow,
/// <c>Wait</c> suspension → <c>End</c> — driving all orchestration decisions itself
/// (CR-010/CR-012). Position is tracked as a stack of <see cref="Frame"/>s (CR-015) so nested
/// containers (If inside While, ...) and rehydration are represented exactly. Only
/// <see cref="StepResult.Yield"/> remains out of scope until T1-15.
/// </summary>
internal sealed class Interpreter<TState>
{
    /// <summary>
    /// Runs <paramref name="instance"/> against <paramref name="definition"/> from its current
    /// position to the next suspension or terminal (CR-016). All mutations route through this
    /// single method so the execution lane (T1-06/CR-040) can later wrap it.
    /// </summary>
    public async ValueTask RunAsync(
        WorkflowInstance<TState> instance,
        WorkflowDefinition<TState> definition,
        TimeProvider timeProvider,
        CancellationToken cancellationToken)
    {
        if (instance.Pointer.Frames.Count == 0)
        {
            instance.Pointer = ExecutionPointer.Empty.Push(Frame.AtSequenceIndex(0));
        }

        await RunLoopAsync(instance, definition, timeProvider, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Attempts to match <paramref name="envelope"/> against <paramref name="instance"/>'s
    /// active wait (EV-020) and, on match, resumes execution from where the <c>Wait</c> left
    /// off (EV-022/EV-023). Returns the outcome without throwing for the routine no-match case.
    /// Caller (the engine facade) MUST invoke this only through the per-instance execution lane.
    /// </summary>
    public async ValueTask<RaiseEventOutcome> TryResumeAsync(
        WorkflowInstance<TState> instance,
        WorkflowDefinition<TState> definition,
        EventEnvelope envelope,
        TimeProvider timeProvider,
        CancellationToken cancellationToken)
    {
        // EV-031: a duplicate delivery of an already-consumed EventId must never buffer again
        // or drive a second continuation, regardless of the instance's current wait state.
        if (instance.ConsumedEventIds.Contains(envelope.EventId))
        {
            return RaiseEventOutcome.NoMatch;
        }

        var wait = instance.ActiveWait;
        var matchesActiveWait =
            instance.Status == WorkflowStatus.Waiting &&
            wait is { Status: WaitStatus.Active } &&
            wait.EventName == envelope.EventName &&
            wait.CorrelationId.Equals(envelope.CorrelationId);

        if (!matchesActiveWait)
        {
            // EV-030: buffer for a future matching Wait instead of dropping the event. EV-031:
            // a duplicate EventId already pending must not create a second mailbox entry. EV-043:
            // tag the entry with the loop-iteration identity in effect right now, so a later
            // iteration's wait registration cannot bidirectionally match stale earlier-iteration
            // residue (FindBufferedMatch checks this tag against the CURRENT identity).
            instance.Mailbox.TryAdd(envelope.EventId, new BufferedEvent(envelope, LoopIterationPath(instance.Pointer)));
            return RaiseEventOutcome.NoMatch;
        }

        await ResumeWaitAsync(instance, definition, wait!, envelope, timeProvider, cancellationToken).ConfigureAwait(false);

        return RaiseEventOutcome.Resumed;
    }

    /// <summary>
    /// Drives the resuming transition for <paramref name="envelope"/> matched against
    /// <paramref name="wait"/>. EV-032: the event is only marked consumed (and removed from the
    /// mailbox, if it was buffered there) after the transition it drives completes — mutate
    /// first, mark-consumed second, so a failed continuation leaves the event available/
    /// re-matchable rather than silently dropped.
    /// </summary>
    private async ValueTask ResumeWaitAsync(
        WorkflowInstance<TState> instance,
        WorkflowDefinition<TState> definition,
        ActiveWait wait,
        EventEnvelope envelope,
        TimeProvider timeProvider,
        CancellationToken cancellationToken)
    {
        wait.Status = WaitStatus.Matched;
        instance.ActiveWait = null;
        instance.PendingResumedEvent = envelope;
        Advance(instance, LifecycleTrigger.MatchWait);
        AdvanceSequenceIndex(instance);
        instance.UpdatedAt = timeProvider.GetUtcNow();

        await RunLoopAsync(instance, definition, timeProvider, cancellationToken).ConfigureAwait(false);

        if (instance.Status == WorkflowStatus.Failed)
        {
            // The continuation this event drove did not commit successfully — EV-032 requires
            // the event to remain available/re-matchable, so it must not be marked consumed.
            // If it had been buffered (bidirectional match), it also stays in the mailbox.
            return;
        }

        instance.ConsumedEventIds.Add(envelope.EventId);
        instance.Mailbox.Remove(envelope.EventId);
    }

    private async ValueTask RunLoopAsync(
        WorkflowInstance<TState> instance,
        WorkflowDefinition<TState> definition,
        TimeProvider timeProvider,
        CancellationToken cancellationToken)
    {
        while (instance.Status == WorkflowStatus.Running)
        {
            var (sequence, isRoot) = ResolveCurrentSequence(definition.Root, instance.Pointer);
            var index = instance.Pointer.Frames[^1].SequenceIndex!.Value;

            if (index >= sequence.Steps.Count)
            {
                if (!TryPopToParent(instance))
                {
                    // Fell off the end of the root sequence with no explicit End — nothing
                    // further to do; the caller observes whatever status is current.
                    break;
                }

                continue;
            }

            var node = sequence.Steps[index];

            switch (node)
            {
                case EndNode endNode:
                    if (instance.ActiveWait is { Status: WaitStatus.Active })
                    {
                        // CR-032: an instance shall not reach Completed while a runtime-owned
                        // wait is unresolved. No explicit cancel/ignore policy exists yet
                        // (later phase), so reaching End here is itself illegal — fail instead
                        // of silently dropping the unresolved wait.
                        Fail(instance, $"Cannot complete: wait '{instance.ActiveWait.EventName}' is still Active (CR-032).");
                        break;
                    }

                    instance.EndOutcomeName = endNode.OutcomeName;
                    Advance(instance, LifecycleTrigger.Complete);
                    break;

                case BusinessStepNode<TState> businessStepNode:
                    await ExecuteBusinessNodeAsync(instance, definition, businessStepNode, timeProvider, index, cancellationToken).ConfigureAwait(false);
                    if (instance.Status == WorkflowStatus.Running)
                    {
                        AdvanceSequenceIndex(instance);
                    }

                    break;

                case IfNode ifNode:
                    EnterIf(instance, ifNode);
                    break;

                case WhileNode whileNode:
                    EnterOrSkipWhile(instance, whileNode);
                    break;

                case WaitNode waitNode:
                    await EnterWaitAsync(instance, definition, waitNode, timeProvider, cancellationToken).ConfigureAwait(false);
                    break;

                case DefinitionNode when isRoot && index == 0:
                    // Root position 0 is always Init when present (CR-005): the engine facade
                    // already converted input to state before this loop starts, so the
                    // interpreter treats this position as a no-op to advance past.
                    AdvanceSequenceIndex(instance);
                    break;

                default:
                    throw new NotSupportedException(
                        $"Definition node '{node.GetType().Name}' is out of scope for the interpreter (T1-07).");
            }

            instance.UpdatedAt = timeProvider.GetUtcNow();
        }
    }

    /// <summary>
    /// Descends from <paramref name="root"/> following every frame pair but the trailing
    /// <see cref="Frame.SequenceIndex"/> frame, resolving the <see cref="SequenceNode"/> that
    /// trailing frame indexes into. Frames come in pairs per nesting level: a
    /// <see cref="Frame.SequenceIndex"/> frame naming the container's position in its enclosing
    /// sequence, followed by a <see cref="Frame.BranchId"/>/<see cref="Frame.LoopIteration"/>
    /// frame naming which child sequence of that container was entered (CR-015). Returns
    /// whether the resolved sequence is the definition root.
    /// </summary>
    private static (SequenceNode Sequence, bool IsRoot) ResolveCurrentSequence(SequenceNode root, ExecutionPointer pointer)
    {
        var sequence = root;
        var isRoot = true;

        for (var i = 0; i < pointer.Frames.Count - 1; i += 2)
        {
            var containerFrame = pointer.Frames[i];
            var childFrame = pointer.Frames[i + 1];
            var node = sequence.Steps[containerFrame.SequenceIndex!.Value];

            sequence = node switch
            {
                IfNode ifNode when childFrame.BranchId is { Name: "Then" } => ifNode.Then,
                IfNode ifNode when childFrame.BranchId is { Name: "Else" } => ifNode.Else,
                WhileNode whileNode when childFrame.LoopIteration is not null => whileNode.Body,
                _ => throw new UnreachableException($"Frame pair at depth {i} does not resolve to a nested sequence."),
            };
            isRoot = false;
        }

        return (sequence, isRoot);
    }

    /// <summary>Increments the innermost frame's sequence index in place, preserving outer frames.</summary>
    private static void AdvanceSequenceIndex(WorkflowInstance<TState> instance)
    {
        var frames = instance.Pointer.Frames;
        var innermost = frames[^1];
        var advanced = Frame.AtSequenceIndex(innermost.SequenceIndex!.Value + 1);

        instance.Pointer = ReplaceInnermost(instance.Pointer, advanced);
    }

    /// <summary>
    /// Pushes the appropriate branch frame (or skips straight past the If if both branches are
    /// empty) so the next loop iteration resolves into the chosen branch's sequence at index 0.
    /// </summary>
    private static void EnterIf(WorkflowInstance<TState> instance, IfNode ifNode)
    {
        var takeThen = ifNode.Condition(GetBoxedState(instance));
        var branchName = takeThen ? "Then" : "Else";

        instance.Pointer = instance.Pointer
            .Push(Frame.InBranch(new BranchId(takeThen ? 0 : 1, branchName)))
            .Push(Frame.AtSequenceIndex(0));
    }

    /// <summary>
    /// Evaluates the loop condition; if true, pushes a loop-body frame at index 0, tagged with a
    /// fresh per-instance iteration counter for this <paramref name="whileNode"/> (EV-043) so
    /// every iteration has a distinct <see cref="Frame.LoopIteration"/> — never the same value
    /// twice, which would make later-iteration waits indistinguishable from earlier ones. If
    /// false, advances past the While node in the enclosing sequence.
    /// </summary>
    private static void EnterOrSkipWhile(WorkflowInstance<TState> instance, WhileNode whileNode)
    {
        if (whileNode.Condition(GetBoxedState(instance)))
        {
            var iteration = instance.LoopIterationCounters.GetValueOrDefault(whileNode);
            instance.LoopIterationCounters[whileNode] = iteration + 1;

            instance.Pointer = instance.Pointer
                .Push(Frame.AtLoopIteration(iteration))
                .Push(Frame.AtSequenceIndex(0));
        }
        else
        {
            AdvanceSequenceIndex(instance);
        }
    }

    /// <summary>
    /// Registers an <see cref="ActiveWait"/> record (EV-021, EV-040) and moves the instance to
    /// <c>Waiting</c> (CR-030). The pointer is left positioned at this <see cref="WaitNode"/>
    /// itself so a later match (<see cref="TryResumeAsync"/>) advances past it and continues
    /// with the following step (EV-022/EV-023).
    /// </summary>
    private async ValueTask EnterWaitAsync(
        WorkflowInstance<TState> instance,
        WorkflowDefinition<TState> definition,
        WaitNode waitNode,
        TimeProvider timeProvider,
        CancellationToken cancellationToken)
    {
        var correlationId = waitNode.SelectCorrelationId(GetBoxedState(instance));
        await RegisterActiveWaitAsync(instance, definition, waitNode.EventName, correlationId, timeProvider, cancellationToken)
            .ConfigureAwait(false);
    }

    /// <summary>
    /// Creates the <see cref="ActiveWait"/> record and fires the shared <c>EnterWait</c>
    /// transition (EV-021, EV-040). EV-030 bidirectional matching: before suspending, checks the
    /// mailbox for an already-buffered event matching this brand-new wait and, if found, resumes
    /// immediately instead of leaving the instance <c>Waiting</c> for a future delivery.
    /// </summary>
    private async ValueTask RegisterActiveWaitAsync(
        WorkflowInstance<TState> instance,
        WorkflowDefinition<TState> definition,
        string eventName,
        CorrelationId correlationId,
        TimeProvider timeProvider,
        CancellationToken cancellationToken)
    {
        var wait = new ActiveWait(WaitId.New(), eventName, correlationId, timeProvider.GetUtcNow());
        instance.ActiveWait = wait;
        Advance(instance, LifecycleTrigger.EnterWait);

        var buffered = FindBufferedMatch(instance, eventName, correlationId);
        if (buffered is not null)
        {
            await ResumeWaitAsync(instance, definition, wait, buffered, timeProvider, cancellationToken).ConfigureAwait(false);
        }
    }

    /// <summary>
    /// Finds a mailbox entry matching <paramref name="eventName"/>/<paramref name="correlationId"/>
    /// AND the loop-iteration identity currently in effect on <see cref="WorkflowInstance{TState}.Pointer"/>
    /// (EV-043) — the pointer already reflects the new wait's frame(s) by the time this runs
    /// (pushed in <see cref="EnterOrSkipWhile"/> before the run loop reaches the Wait node, or
    /// left as-is for a <see cref="StepResult.WaitForEvent"/> wait registered outside any freshly
    /// entered loop frame), so comparing against it here correctly rejects entries buffered under
    /// an earlier iteration's identity even when the (name, correlation) key coincides. An empty
    /// identity (not inside any loop) matches only other empty-identity entries, preserving plain
    /// top-level mailbox buffering (EV-030, T1-09) unchanged.
    /// </summary>
    private static EventEnvelope? FindBufferedMatch(WorkflowInstance<TState> instance, string eventName, CorrelationId correlationId)
    {
        var currentIterationPath = LoopIterationPath(instance.Pointer);

        foreach (var buffered in instance.Mailbox.Values)
        {
            if (buffered.Envelope.EventName == eventName &&
                buffered.Envelope.CorrelationId.Equals(correlationId) &&
                buffered.LoopIterationPath.SequenceEqual(currentIterationPath))
            {
                return buffered.Envelope;
            }
        }

        return null;
    }

    /// <summary>
    /// The ordered sequence of <see cref="Frame.LoopIteration"/> values found in
    /// <paramref name="pointer"/>'s frames (EV-043) — the runtime "which loop iteration(s) am I
    /// currently inside" identity. Empty means not inside any loop right now. Two identities are
    /// equal iff their sequences are equal (<see cref="Enumerable.SequenceEqual{TSource}(IEnumerable{TSource}, IEnumerable{TSource})"/>).
    /// </summary>
    private static IReadOnlyList<int> LoopIterationPath(ExecutionPointer pointer)
    {
        List<int>? path = null;
        foreach (var frame in pointer.Frames)
        {
            if (frame.LoopIteration is { } iteration)
            {
                (path ??= []).Add(iteration);
            }
        }

        return path ?? [];
    }

    /// <summary>
    /// Called when a nested sequence (If branch or While body) has run past its last step. Pops
    /// back to the enclosing sequence and advances past the container that owned it — re-entering
    /// a While re-evaluates its condition (loop semantics); an If branch simply rejoins the
    /// enclosing sequence. Returns false if there is no parent to pop to (root exhausted).
    /// </summary>
    private static bool TryPopToParent(WorkflowInstance<TState> instance)
    {
        var frames = instance.Pointer.Frames;
        if (frames.Count <= 1)
        {
            return false;
        }

        var containerFrame = frames[^2];
        instance.Pointer = instance.Pointer.Pop().Pop();

        if (containerFrame.LoopIteration is not null)
        {
            // Loop body exhausted: leave the pointer at the While node's own index so the main
            // loop re-evaluates its condition on the next pass (re-entrant While semantics).
            return true;
        }

        // If branch exhausted: advance past the If node in the enclosing sequence.
        AdvanceSequenceIndex(instance);
        return true;
    }

    private static ExecutionPointer ReplaceInnermost(ExecutionPointer pointer, Frame replacement) =>
        pointer.Pop().Push(replacement);

    private static object? GetBoxedState(WorkflowInstance<TState> instance) => instance.State;

    private async ValueTask ExecuteBusinessNodeAsync(
        WorkflowInstance<TState> instance,
        WorkflowDefinition<TState> definition,
        BusinessStepNode<TState> businessStepNode,
        TimeProvider timeProvider,
        int stepIndex,
        CancellationToken cancellationToken)
    {
        var step = businessStepNode.CreateStep();
        var context = new StepContext<TState>
        {
            State = instance.State,
            TimeProvider = timeProvider,
            ResumedEvent = instance.PendingResumedEvent,
        };
        instance.PendingResumedEvent = null;

        StepResult result;
        try
        {
            result = await step.ExecuteAsync(context, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception exception)
        {
            Fail(instance, DescribeError(exception.GetType().Name, exception.Message, stepIndex, timeProvider));
            return;
        }

        switch (result)
        {
            case StepResult.Completed:
                break;

            case StepResult.Failed failed:
                Fail(instance, DescribeError(failed.Error.GetType().Name, failed.Error.Message, stepIndex, timeProvider));
                break;

            case StepResult.WaitForEvent waitForEvent:
                await RegisterActiveWaitAsync(
                    instance, definition, waitForEvent.EventName, waitForEvent.CorrelationId, timeProvider, cancellationToken)
                    .ConfigureAwait(false);
                break;

            case StepResult.Yield:
                throw new NotSupportedException(
                    "StepResult.Yield is not supported by the straight-line interpreter yet; yield lands in T1-15.");

            default:
                throw new UnreachableException($"Unhandled {nameof(StepResult)} variant '{result.GetType().Name}'.");
        }
    }

    private static void Fail(WorkflowInstance<TState> instance, string errorSummary)
    {
        instance.ErrorSummary = errorSummary;
        Advance(instance, LifecycleTrigger.Fail);
    }

    private static void Advance(WorkflowInstance<TState> instance, LifecycleTrigger trigger)
    {
        var result = LifecycleMachine.Fire(instance.Status, trigger);
        instance.Status = result.IsSuccess ? result.Value : throw result.Error;
    }

    private static string DescribeError(string errorType, string message, int stepIndex, TimeProvider timeProvider) =>
        $"{errorType}: {message} (at root[{stepIndex}], {timeProvider.GetUtcNow():O})";
}
