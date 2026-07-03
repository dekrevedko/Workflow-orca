using OrcaCore.Abstractions.Errors;
using OrcaCore.Abstractions.Events;
using OrcaCore.Abstractions.Ids;
using OrcaCore.Abstractions.Instances;
using OrcaCore.Abstractions.Steps;
using OrcaCore.Core.Building;
using OrcaCore.Core.Definitions;
using OrcaCore.Engine.Ephemeral.Execution;
using OrcaCore.TestSupport.Time;

namespace OrcaCore.Engine.Ephemeral.Tests.Execution;

public class MailboxTests
{
    private sealed class OrderState
    {
        public int Total { get; set; }
        public List<string> Executed { get; } = [];
        public EventEnvelope? ObservedResumedEvent { get; set; }
        public int ContinuationCount { get; set; }
    }

    private sealed class WaitForApprovalStep(string eventName, CorrelationId correlationId) : IStep<OrderState>
    {
        public ValueTask<StepResult> ExecuteAsync(StepContext<OrderState> context, CancellationToken cancellationToken) =>
            ValueTask.FromResult<StepResult>(new StepResult.WaitForEvent(eventName, correlationId));
    }

    private sealed class CaptureResumedEventStep : IStep<OrderState>
    {
        public ValueTask<StepResult> ExecuteAsync(StepContext<OrderState> context, CancellationToken cancellationToken)
        {
            context.State.ObservedResumedEvent = context.ResumedEvent;
            context.State.ContinuationCount++;
            context.State.Executed.Add("after-wait");
            return ValueTask.FromResult<StepResult>(new StepResult.Completed());
        }
    }

    private sealed class ThrowingAfterWaitStep(Exception exception) : IStep<OrderState>
    {
        public ValueTask<StepResult> ExecuteAsync(StepContext<OrderState> context, CancellationToken cancellationToken) =>
            throw exception;
    }

    private static WorkflowInstance<OrderState> NewInstance(Clock clock) =>
        new(
            InstanceId.New(),
            new DefinitionId("order-workflow"),
            new DefinitionVersion(1),
            new OrderState(),
            clock.Now);

    private static WorkflowDefinition<OrderState> ApprovalDefinition() =>
        WorkflowDefinitionFor(builder =>
        {
            builder.Then(new WaitForApprovalStep("ApprovalReceived", new CorrelationId("order-1")));
            builder.Then(new CaptureResumedEventStep());
            builder.End("Approved");
        });

    private static WorkflowDefinition<OrderState> WorkflowDefinitionFor(Action<WorkflowBuilder<OrderState>> configure)
    {
        var builder = WorkflowBuilder<OrderState>.Create<int>(input => new OrderState { Total = input });
        configure(builder);
        return builder.Build(new DefinitionId("order-workflow"), new DefinitionVersion(1));
    }

    [Fact]
    public async Task RaiseEventAsync_BeforeWait_BuffersEvent()
    {
        var definition = ApprovalDefinition();
        var clock = new Clock(DateTimeOffset.UnixEpoch);
        var instance = NewInstance(clock);

        var interpreter = new Interpreter<OrderState>();

        var envelope = new EventEnvelope(
            EventId.New(),
            "ApprovalReceived",
            new CorrelationId("order-1"),
            Payload: "approved-payload",
            clock.Now);

        // No active wait yet (instance hasn't even started running the body).
        var outcome = await interpreter.TryResumeAsync(
            instance, definition, envelope, clock.TimeProvider, TestContext.Current.CancellationToken);

        outcome.Should().Be(RaiseEventOutcome.NoMatch);
        instance.Status.Should().Be(WorkflowStatus.Running);
        instance.PendingMailboxCount.Should().Be(1, "the event arrived before any wait exists and must be buffered (EV-030)");

        await interpreter.RunAsync(instance, definition, clock.TimeProvider, TestContext.Current.CancellationToken);

        instance.Status.Should().Be(WorkflowStatus.Completed);
        instance.State.Executed.Should().Equal("after-wait");
        instance.State.ObservedResumedEvent.Should().Be(envelope);
        instance.PendingMailboxCount.Should().Be(0, "the buffered event must be removed once the resuming transition commits (EV-032)");
    }

    [Fact]
    public async Task Run_RegisteringMatchingWait_ConsumesBufferedEventAndContinues()
    {
        var definition = ApprovalDefinition();
        var clock = new Clock(DateTimeOffset.UnixEpoch);
        var instance = NewInstance(clock);

        var interpreter = new Interpreter<OrderState>();
        var envelope = new EventEnvelope(
            EventId.New(),
            "ApprovalReceived",
            new CorrelationId("order-1"),
            Payload: "approved-payload",
            clock.Now);

        await interpreter.TryResumeAsync(
            instance, definition, envelope, clock.TimeProvider, TestContext.Current.CancellationToken);

        await interpreter.RunAsync(instance, definition, clock.TimeProvider, TestContext.Current.CancellationToken);

        instance.Status.Should().Be(WorkflowStatus.Completed);
        instance.ActiveWait.Should().BeNull();
        instance.State.ObservedResumedEvent.Should().Be(envelope);
    }

    [Fact]
    public async Task RaiseEventAsync_DuplicatePendingEvent_BuffersOnlyOnce()
    {
        var definition = ApprovalDefinition();
        var clock = new Clock(DateTimeOffset.UnixEpoch);
        var instance = NewInstance(clock);

        var interpreter = new Interpreter<OrderState>();
        var eventId = EventId.New();
        var envelope = new EventEnvelope(eventId, "ApprovalReceived", new CorrelationId("order-1"), "first", clock.Now);
        var duplicateEnvelope = new EventEnvelope(eventId, "ApprovalReceived", new CorrelationId("order-1"), "duplicate", clock.Now);

        await interpreter.TryResumeAsync(instance, definition, envelope, clock.TimeProvider, TestContext.Current.CancellationToken);
        await interpreter.TryResumeAsync(instance, definition, duplicateEnvelope, clock.TimeProvider, TestContext.Current.CancellationToken);

        instance.PendingMailboxCount.Should().Be(1, "a duplicate EventId must not create a second buffered entry (EV-031)");

        await interpreter.RunAsync(instance, definition, clock.TimeProvider, TestContext.Current.CancellationToken);

        instance.Status.Should().Be(WorkflowStatus.Completed);
        instance.State.ContinuationCount.Should().Be(1);
        instance.State.ObservedResumedEvent.Should().Be(envelope);
        instance.State.Executed.Should().Equal("after-wait");
    }

    [Fact]
    public async Task RaiseEventAsync_DuplicateConsumedEvent_DoesNotContinueAgain()
    {
        var definition = ApprovalDefinition();
        var clock = new Clock(DateTimeOffset.UnixEpoch);
        var instance = NewInstance(clock);

        var interpreter = new Interpreter<OrderState>();
        await interpreter.RunAsync(instance, definition, clock.TimeProvider, TestContext.Current.CancellationToken);

        var eventId = EventId.New();
        var envelope = new EventEnvelope(eventId, "ApprovalReceived", new CorrelationId("order-1"), "first", clock.Now);

        var firstOutcome = await interpreter.TryResumeAsync(
            instance, definition, envelope, clock.TimeProvider, TestContext.Current.CancellationToken);

        firstOutcome.Should().Be(RaiseEventOutcome.Resumed);
        instance.Status.Should().Be(WorkflowStatus.Completed);

        var duplicateEnvelope = new EventEnvelope(eventId, "ApprovalReceived", new CorrelationId("order-1"), "second", clock.Now);

        var secondOutcome = await interpreter.TryResumeAsync(
            instance, definition, duplicateEnvelope, clock.TimeProvider, TestContext.Current.CancellationToken);

        secondOutcome.Should().Be(RaiseEventOutcome.NoMatch);
        instance.State.ContinuationCount.Should().Be(1, "a duplicate delivery of an already-consumed EventId must not continue a second time (EV-031)");
        instance.State.Executed.Should().Equal("after-wait");
        instance.State.ObservedResumedEvent.Should().Be(envelope);
    }

    [Fact]
    public async Task RaiseEventAsync_ResumeTransitionFails_EventRemainsAvailable()
    {
        var exception = new InvalidOperationException("continuation boom");
        var definition = WorkflowDefinitionFor(builder =>
        {
            builder.Then(new WaitForApprovalStep("ApprovalReceived", new CorrelationId("order-1")));
            builder.Then(new ThrowingAfterWaitStep(exception));
            builder.End();
        });

        var clock = new Clock(DateTimeOffset.UnixEpoch);
        var instance = NewInstance(clock);

        var interpreter = new Interpreter<OrderState>();
        await interpreter.RunAsync(instance, definition, clock.TimeProvider, TestContext.Current.CancellationToken);

        var envelope = new EventEnvelope(EventId.New(), "ApprovalReceived", new CorrelationId("order-1"), "payload", clock.Now);

        var outcome = await interpreter.TryResumeAsync(
            instance, definition, envelope, clock.TimeProvider, TestContext.Current.CancellationToken);

        outcome.Should().Be(RaiseEventOutcome.Resumed);
        instance.Status.Should().Be(WorkflowStatus.Failed);

        // EV-032: an event is consumed only once (1) it matched a wait AND (2) the resulting
        // transition committed. The continuation step above threw, so the transition did not
        // commit successfully — the event must not have been marked-consumed by the failed
        // attempt (mutate-then-mark-consumed ordering, never mark-then-mutate).
        instance.IsEventConsumed(envelope.EventId).Should().BeFalse(
            "a failed continuation must leave the event available/re-matchable, never mark it consumed before the mutation it drove has committed (EV-032)");
    }

    [Fact]
    public async Task Run_EndWithActiveWait_FailsOrCancelsByExplicitPolicy()
    {
        // Straight-line execution can never reach End with ActiveWait still set through normal
        // control flow (a Wait node suspends the run loop itself). CR-032 must still guard the
        // End transition defensively, so this test forces the otherwise-unreachable illegal
        // state directly and proves the interpreter refuses to silently Complete over it.
        var definition = WorkflowDefinitionFor(builder =>
        {
            builder.Then(new CaptureResumedEventStep());
            builder.End("Approved");
        });

        var clock = new Clock(DateTimeOffset.UnixEpoch);
        var instance = NewInstance(clock);
        instance.ActiveWait = new ActiveWait(WaitId.New(), "ApprovalReceived", new CorrelationId("order-1"), clock.Now);

        var interpreter = new Interpreter<OrderState>();
        await interpreter.RunAsync(instance, definition, clock.TimeProvider, TestContext.Current.CancellationToken);

        instance.Status.Should().Be(WorkflowStatus.Failed);
        instance.ErrorSummary.Should().NotBeNull();
        instance.ActiveWait.Should().NotBeNull("CR-032: an unresolved wait must not be silently dropped by the completion guard");
    }
}
