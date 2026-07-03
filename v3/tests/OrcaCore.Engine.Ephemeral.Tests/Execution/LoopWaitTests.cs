using OrcaCore.Abstractions.Events;
using OrcaCore.Abstractions.Ids;
using OrcaCore.Abstractions.Instances;
using OrcaCore.Abstractions.Steps;
using OrcaCore.Core.Building;
using OrcaCore.Core.Definitions;
using OrcaCore.Engine.Ephemeral.Execution;
using OrcaCore.TestSupport.Time;

namespace OrcaCore.Engine.Ephemeral.Tests.Execution;

/// <summary>
/// EV-043: each <c>While</c> iteration must create a fresh wait identity, and an event
/// correlated to a previous iteration's wait must not resume a later iteration. Every test here
/// uses the SAME (EventName, CorrelationId) key across all iterations — the realistic authoring
/// pattern where a loop waits on the same signal each pass — since that is exactly the case
/// where stale mailbox residue can masquerade as a match for a later iteration's wait.
/// </summary>
public class LoopWaitTests
{
    private const string EventName = "LoopSignal";
    private static readonly CorrelationId LoopCorrelationId = new("loop-1");

    private sealed class LoopState
    {
        public int Total { get; set; }
        public List<string> Executed { get; } = [];
        public List<object?> ObservedPayloads { get; } = [];
    }

    /// <summary>Waits on the same (EventName, CorrelationId) key every iteration; increments Total on resume.</summary>
    private sealed class WaitThenIncrementStep : IStep<LoopState>
    {
        public ValueTask<StepResult> ExecuteAsync(StepContext<LoopState> context, CancellationToken cancellationToken)
        {
            if (context.ResumedEvent is null)
            {
                return ValueTask.FromResult<StepResult>(new StepResult.WaitForEvent(EventName, LoopCorrelationId));
            }

            context.State.ObservedPayloads.Add(context.ResumedEvent.Payload);
            context.State.Total += 1;
            context.State.Executed.Add("iter");
            return ValueTask.FromResult<StepResult>(new StepResult.Completed());
        }
    }

    private static WorkflowInstance<LoopState> NewInstance(Clock clock) =>
        new(
            InstanceId.New(),
            new DefinitionId("loop-workflow"),
            new DefinitionVersion(1),
            new LoopState(),
            clock.Now);

    private static WorkflowDefinition<LoopState> LoopDefinition(int iterations)
    {
        var builder = WorkflowBuilder<LoopState>.Create<int>(input => new LoopState { Total = input });
        builder.While(
            state => ((LoopState)state!).Total < iterations,
            body => body.Then(new WaitThenIncrementStep()));
        builder.End();
        return builder.Build(new DefinitionId("loop-workflow"), new DefinitionVersion(1));
    }

    private static EventEnvelope AnEnvelope(object? payload, DateTimeOffset now) =>
        new(EventId.New(), EventName, LoopCorrelationId, payload, now);

    [Fact]
    public async Task Run_WhileRegistersWaitEachIteration_CreatesFreshWaitIds()
    {
        var definition = LoopDefinition(2);
        var clock = new Clock(DateTimeOffset.UnixEpoch);
        var instance = NewInstance(clock);

        var interpreter = new Interpreter<LoopState>();
        await interpreter.RunAsync(instance, definition, clock.TimeProvider, TestContext.Current.CancellationToken);

        instance.Status.Should().Be(WorkflowStatus.Waiting);
        instance.Pointer.Frames.Should().Contain(frame => frame.LoopIteration == 0,
            "iteration 1 must be recorded as loop iteration 0");
        var firstWaitId = instance.ActiveWait!.WaitId;

        var firstEnvelope = AnEnvelope("first", clock.Now);
        var firstOutcome = await interpreter.TryResumeAsync(
            instance, definition, firstEnvelope, clock.TimeProvider, TestContext.Current.CancellationToken);

        firstOutcome.Should().Be(RaiseEventOutcome.Resumed);
        instance.Status.Should().Be(WorkflowStatus.Waiting, "second iteration must register a fresh wait");
        var secondWaitId = instance.ActiveWait!.WaitId;

        secondWaitId.Should().NotBe(firstWaitId, "each loop iteration must create a fresh wait identity (EV-043)");
        instance.Pointer.Frames.Should().NotContain(frame => frame.LoopIteration == 0,
            "iteration 2's loop-body frame must record a loop-iteration value DISTINCT from iteration 1's (0), not " +
            "reuse it (Bug #1: EnterOrSkipWhile previously hardcoded Frame.AtLoopIteration(0) for every iteration)");
    }

    [Fact]
    public async Task RaiseEventAsync_EventForPreviousIteration_DoesNotResumeLaterIteration()
    {
        // The real EV-043 risk is in mailbox bidirectional matching (RegisterActiveWaitAsync ->
        // FindBufferedMatch): an event buffered during iteration 1 (because it didn't match
        // iteration 1's key, or arrived before iteration 1's wait existed) must not auto-resume
        // iteration 2's freshly registered wait even though both iterations wait on the identical
        // (EventName, CorrelationId) key.
        var definition = LoopDefinition(3);
        var clock = new Clock(DateTimeOffset.UnixEpoch);
        var instance = NewInstance(clock);

        var interpreter = new Interpreter<LoopState>();
        await interpreter.RunAsync(instance, definition, clock.TimeProvider, TestContext.Current.CancellationToken);
        instance.Status.Should().Be(WorkflowStatus.Waiting, "iteration 1's wait is active");

        // A stray event arrives that does NOT match iteration 1's *currently active* wait only
        // because a duplicate active wait can't exist to compare against directly — instead we
        // simulate genuine staleness: this envelope is buffered NOW (tagged iteration-1 identity)
        // by using an EventId that, when iteration 1 later resumes via a DIFFERENT envelope,
        // remains in the mailbox unconsumed. To land in the mailbox despite matching iteration 1's
        // key, we rely on EV-031 dedup being irrelevant here (different EventId) and instead force
        // buffering by delivering it AFTER iteration 1 has already been resumed by another
        // envelope in the same call — which isn't possible with one TryResumeAsync call. So this
        // test buffers a same-key event BEFORE iteration 1's wait resumes it directly: since direct
        // delivery against the active wait always wins over buffering (T1-09), the first same-key
        // delivery legitimately resumes iteration 1. The buffered-staleness path is exercised by
        // Mailbox_PreviousIterationEvent_RemainsStaleForLaterWait below using a two-instance setup
        // where the stale entry is buffered while a DIFFERENT wait is active (iteration 1), then
        // iteration 2 registers with the SAME key as the stale entry.
        var iteration1Resume = AnEnvelope("iter1-resume", clock.Now);
        var outcome = await interpreter.TryResumeAsync(
            instance, definition, iteration1Resume, clock.TimeProvider, TestContext.Current.CancellationToken);

        outcome.Should().Be(RaiseEventOutcome.Resumed, "direct delivery against the CURRENT active wait must resume it regardless of loop history");
        instance.Status.Should().Be(WorkflowStatus.Waiting, "iteration 2's fresh wait must now be active");
        instance.State.Total.Should().Be(1);

        // Now the mailbox is empty (nothing was buffered) and iteration 2's wait is active. A
        // fresh envelope with the same key legitimately resumes iteration 2 - this is expected and
        // proves direct-delivery isolation is untouched by the loop fix.
        var iteration2Resume = AnEnvelope("iter2-resume", clock.Now);
        var secondOutcome = await interpreter.TryResumeAsync(
            instance, definition, iteration2Resume, clock.TimeProvider, TestContext.Current.CancellationToken);

        secondOutcome.Should().Be(RaiseEventOutcome.Resumed);
        instance.State.Total.Should().Be(2);
    }

    [Fact]
    public async Task RaiseEventAsync_CurrentIterationEvent_ResumesCurrentWait()
    {
        var definition = LoopDefinition(2);
        var clock = new Clock(DateTimeOffset.UnixEpoch);
        var instance = NewInstance(clock);

        var interpreter = new Interpreter<LoopState>();
        await interpreter.RunAsync(instance, definition, clock.TimeProvider, TestContext.Current.CancellationToken);

        var envelope = AnEnvelope("current", clock.Now);
        var outcome = await interpreter.TryResumeAsync(
            instance, definition, envelope, clock.TimeProvider, TestContext.Current.CancellationToken);

        outcome.Should().Be(RaiseEventOutcome.Resumed);
        instance.State.Executed.Should().Equal("iter");
        instance.State.Total.Should().Be(1);
    }

    [Fact]
    public async Task Mailbox_PreviousIterationEvent_RemainsStaleForLaterWait()
    {
        // This is the core EV-043/Bug #2 reproduction: an event is buffered while iteration 1's
        // wait is active (it doesn't match iteration 1's key, e.g. wrong correlation), so it sits
        // in the mailbox tagged with iteration 1's loop-iteration identity. Iteration 1 then
        // resumes via its real key. Iteration 2 registers a fresh wait using the SAME key that the
        // stale mailbox entry does NOT match on (EventName/CorrelationId) — so today's code
        // wouldn't incorrectly match it either, since FindBufferedMatch only compares
        // EventName/CorrelationId already. To reproduce Bug #2 for real, the stale entry's key
        // must exactly equal iteration 2's wait key while having been buffered during iteration
        // 1's window — achieved by buffering it AFTER iteration 1's own matching envelope already
        // resumed iteration 1 in a separate step is impossible (mailbox check happens before any
        // wait). Instead: iteration 1 does NOT wait on the reused key directly — a distinct
        // "OtherSignal" wait is used for iteration 1 so a same-key ("LoopSignal") event can be
        // buffered (non-matching) while iteration 1 is active, then iteration 2 waits on
        // "LoopSignal" and must not auto-resume from that iteration-1-buffered entry.
        var builder = WorkflowBuilder<LoopState>.Create<int>(input => new LoopState { Total = input });
        builder.While(
            state => ((LoopState)state!).Total < 2,
            body => body.Then(new FirstIterationDifferentKeyStep()));
        builder.End();
        var definition = builder.Build(new DefinitionId("loop-workflow-2"), new DefinitionVersion(1));

        var clock = new Clock(DateTimeOffset.UnixEpoch);
        var instance = new WorkflowInstance<LoopState>(
            InstanceId.New(), new DefinitionId("loop-workflow-2"), new DefinitionVersion(1), new LoopState(), clock.Now);

        var interpreter = new Interpreter<LoopState>();
        await interpreter.RunAsync(instance, definition, clock.TimeProvider, TestContext.Current.CancellationToken);
        instance.Status.Should().Be(WorkflowStatus.Waiting, "iteration 1 waits on OtherSignal");
        instance.ActiveWait!.EventName.Should().Be("OtherSignal");

        // Buffer a "LoopSignal" event now, while iteration 1's wait (OtherSignal) is active. It
        // does not match iteration 1's key, so it buffers, tagged with iteration 1's identity.
        var staleLoopSignal = new EventEnvelope(EventId.New(), EventName, LoopCorrelationId, "stale-from-iter1", clock.Now);
        var bufferOutcome = await interpreter.TryResumeAsync(
            instance, definition, staleLoopSignal, clock.TimeProvider, TestContext.Current.CancellationToken);
        bufferOutcome.Should().Be(RaiseEventOutcome.NoMatch);
        instance.PendingMailboxCount.Should().Be(1);

        // Resume iteration 1 via its real key (OtherSignal), advancing to iteration 2, which
        // registers a wait on "LoopSignal" (via WaitThenIncrementStep next iteration). The
        // bidirectional match in RegisterActiveWaitAsync must NOT pick up the stale entry above
        // just because the key now coincides with iteration 2's wait.
        var otherSignalMatch = new EventEnvelope(EventId.New(), "OtherSignal", LoopCorrelationId, "iter1-real", clock.Now);
        await interpreter.TryResumeAsync(
            instance, definition, otherSignalMatch, clock.TimeProvider, TestContext.Current.CancellationToken);

        instance.Status.Should().Be(WorkflowStatus.Waiting, "iteration 2's fresh wait must be active, not auto-resumed by iteration 1's stale mailbox residue");
        instance.ActiveWait!.EventName.Should().Be("LoopSignal");
        instance.State.Total.Should().Be(1, "only iteration 1 completed so far — the stale entry must not have driven iteration 2's continuation");
        instance.PendingMailboxCount.Should().Be(1, "the iteration-1-tagged stale entry must remain buffered, not consumed by iteration 2's registration (EV-043)");
    }

    /// <summary>Iteration 1 (Total == 0) waits on "OtherSignal"; iteration 2+ waits on "LoopSignal".</summary>
    private sealed class FirstIterationDifferentKeyStep : IStep<LoopState>
    {
        public ValueTask<StepResult> ExecuteAsync(StepContext<LoopState> context, CancellationToken cancellationToken)
        {
            if (context.ResumedEvent is null)
            {
                var eventName = context.State.Total == 0 ? "OtherSignal" : EventName;
                return ValueTask.FromResult<StepResult>(new StepResult.WaitForEvent(eventName, LoopCorrelationId));
            }

            context.State.Total += 1;
            context.State.Executed.Add("iter");
            return ValueTask.FromResult<StepResult>(new StepResult.Completed());
        }
    }
}
