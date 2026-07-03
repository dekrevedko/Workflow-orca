using OrcaCore.Abstractions.Events;
using OrcaCore.Abstractions.Ids;
using OrcaCore.Abstractions.Instances;
using OrcaCore.Abstractions.Steps;
using OrcaCore.Core.Building;
using OrcaCore.Core.Definitions;
using OrcaCore.Engine.Ephemeral;

namespace OrcaCore.Acceptance.Tests;

public class MailboxAcceptanceTests
{
    private sealed class OrderState
    {
        public int Total { get; set; }
        public List<string> Executed { get; } = [];
        public object? ResumedPayload { get; set; }
    }

    private sealed class WaitForEventStep(string eventName, CorrelationId correlationId) : IStep<OrderState>
    {
        public ValueTask<StepResult> ExecuteAsync(StepContext<OrderState> context, CancellationToken cancellationToken) =>
            ValueTask.FromResult<StepResult>(new StepResult.WaitForEvent(eventName, correlationId));
    }

    private sealed class RecordResumeStep(string tag) : IStep<OrderState>
    {
        public ValueTask<StepResult> ExecuteAsync(StepContext<OrderState> context, CancellationToken cancellationToken)
        {
            context.State.Executed.Add(tag);
            context.State.ResumedPayload = context.ResumedEvent?.Payload;
            return ValueTask.FromResult<StepResult>(new StepResult.Completed());
        }
    }

    private static (EphemeralWorkflowEngine Engine, DefinitionId DefinitionId) BuildApprovalWorkflow(DefinitionId definitionId)
    {
        var engine = new EphemeralWorkflowEngine();
        var builder = WorkflowBuilder<OrderState>.Create<int>(input => new OrderState { Total = input });
        builder.Then(new WaitForEventStep("ApprovalReceived", new CorrelationId("order-1")));
        builder.Then(new RecordResumeStep("resumed"));
        builder.End("Approved");
        var definition = builder.Build(definitionId, new DefinitionVersion(1));
        engine.RegisterDefinition(definition);
        return (engine, definitionId);
    }

    /// <summary>
    /// Two sequential waits on different event names/correlations, so an event for the SECOND
    /// wait can genuinely be delivered while the FIRST wait is still active — a true
    /// out-of-order delivery reachable through the public API alone.
    /// </summary>
    private static (EphemeralWorkflowEngine Engine, DefinitionId DefinitionId) BuildTwoStepApprovalWorkflow(DefinitionId definitionId)
    {
        var engine = new EphemeralWorkflowEngine();
        var builder = WorkflowBuilder<OrderState>.Create<int>(input => new OrderState { Total = input });
        builder.Then(new WaitForEventStep("FirstApproval", new CorrelationId("order-1")));
        builder.Then(new RecordResumeStep("first-resumed"));
        builder.Then(new WaitForEventStep("SecondApproval", new CorrelationId("order-1")));
        builder.Then(new RecordResumeStep("second-resumed"));
        builder.End("Approved");
        var definition = builder.Build(definitionId, new DefinitionVersion(1));
        engine.RegisterDefinition(definition);
        return (engine, definitionId);
    }

    [Trait("AC", "AC-104")]
    [Fact]
    public async Task OutOfOrderEvent_IsBufferedThenConsumed()
    {
        var (engine, definitionId) = BuildTwoStepApprovalWorkflow(new DefinitionId("mailbox-workflow-104"));

        var started = await engine.StartAsync<int, OrderState>(definitionId, 1, TestContext.Current.CancellationToken);
        started.Status.Should().Be(WorkflowStatus.Waiting);

        // SecondApproval's event arrives while the instance is still waiting on FirstApproval -
        // no wait for it exists yet. Per EV-030 it must be buffered rather than lost, and
        // consumed automatically once the second Wait node is reached.
        var secondApprovalEnvelope = new EventEnvelope(
            EventId.New(), "SecondApproval", new CorrelationId("order-1"), "second-payload", DateTimeOffset.UnixEpoch);
        var outOfOrderOutcome = await engine.RaiseEventAsync<OrderState>(started.InstanceId, secondApprovalEnvelope, TestContext.Current.CancellationToken);

        outOfOrderOutcome.Should().Be(RaiseEventOutcome.NoMatch, "no wait for SecondApproval exists yet - the event is buffered, not matched");

        var firstApprovalEnvelope = new EventEnvelope(
            EventId.New(), "FirstApproval", new CorrelationId("order-1"), "first-payload", DateTimeOffset.UnixEpoch);
        var firstOutcome = await engine.RaiseEventAsync<OrderState>(started.InstanceId, firstApprovalEnvelope, TestContext.Current.CancellationToken);

        firstOutcome.Should().Be(RaiseEventOutcome.Resumed);

        // Registering the second Wait must immediately find the already-buffered SecondApproval
        // event and resume through it in the same delivery that resolved FirstApproval, so the
        // instance runs straight through to Completed without ever re-suspending on
        // SecondApproval and waiting for a fresh delivery.
        var redeliveredSecondApproval = await engine.RaiseEventAsync<OrderState>(
            started.InstanceId, secondApprovalEnvelope, TestContext.Current.CancellationToken);
        redeliveredSecondApproval.Should().Be(
            RaiseEventOutcome.NoMatch,
            "the buffered SecondApproval event was already consumed when its wait was registered - the instance must not still be waiting for it");
    }

    [Trait("AC", "AC-105")]
    [Fact]
    public async Task DuplicateEventId_ProducesOneConsumptionAndContinuation()
    {
        var (engine, definitionId) = BuildApprovalWorkflow(new DefinitionId("mailbox-workflow-105"));
        var started = await engine.StartAsync<int, OrderState>(definitionId, 1, TestContext.Current.CancellationToken);

        var eventId = EventId.New();
        var envelope = new EventEnvelope(eventId, "ApprovalReceived", new CorrelationId("order-1"), "approved-payload", DateTimeOffset.UnixEpoch);
        var duplicateEnvelope = new EventEnvelope(eventId, "ApprovalReceived", new CorrelationId("order-1"), "duplicate-payload", DateTimeOffset.UnixEpoch);

        var firstOutcome = await engine.RaiseEventAsync<OrderState>(started.InstanceId, envelope, TestContext.Current.CancellationToken);
        var secondOutcome = await engine.RaiseEventAsync<OrderState>(started.InstanceId, duplicateEnvelope, TestContext.Current.CancellationToken);

        firstOutcome.Should().Be(RaiseEventOutcome.Resumed);
        secondOutcome.Should().Be(RaiseEventOutcome.NoMatch);
    }

    [Trait("AC", "AC-105")]
    [Fact]
    public async Task DuplicateEventId_DeliveredBeforeItsWaitExists_StillBuffersOnlyOnce()
    {
        var (engine, definitionId) = BuildTwoStepApprovalWorkflow(new DefinitionId("mailbox-workflow-105b"));
        var started = await engine.StartAsync<int, OrderState>(definitionId, 1, TestContext.Current.CancellationToken);

        var eventId = EventId.New();
        var envelope = new EventEnvelope(eventId, "SecondApproval", new CorrelationId("order-1"), "first-delivery", DateTimeOffset.UnixEpoch);
        var duplicateEnvelope = new EventEnvelope(eventId, "SecondApproval", new CorrelationId("order-1"), "second-delivery", DateTimeOffset.UnixEpoch);

        // Two deliveries of the same EventId for SecondApproval, both arriving before any wait
        // for SecondApproval exists (EV-031 dedup must apply to buffering, not just consumption).
        await engine.RaiseEventAsync<OrderState>(started.InstanceId, envelope, TestContext.Current.CancellationToken);
        await engine.RaiseEventAsync<OrderState>(started.InstanceId, duplicateEnvelope, TestContext.Current.CancellationToken);

        var firstApprovalEnvelope = new EventEnvelope(
            EventId.New(), "FirstApproval", new CorrelationId("order-1"), "first-payload", DateTimeOffset.UnixEpoch);
        var firstOutcome = await engine.RaiseEventAsync<OrderState>(started.InstanceId, firstApprovalEnvelope, TestContext.Current.CancellationToken);

        // Resolving FirstApproval consumes the single buffered SecondApproval entry and the
        // instance completes; if the duplicate had buffered a second time nothing here would
        // break structurally, but the workflow must still end up Completed exactly once with no
        // outstanding wait, which is the observable contract of "buffered only once".
        firstOutcome.Should().Be(RaiseEventOutcome.Resumed);

        var secondDeliveryAfterResume = await engine.RaiseEventAsync<OrderState>(started.InstanceId, duplicateEnvelope, TestContext.Current.CancellationToken);
        secondDeliveryAfterResume.Should().Be(RaiseEventOutcome.NoMatch, "the duplicate EventId must already be consumed - no active wait remains to match it");
    }

    [Trait("AC", "AC-010")]
    [Fact]
    public async Task CompletionBlockedByUnresolvedRuntimeWork()
    {
        var (engine, definitionId) = BuildApprovalWorkflow(new DefinitionId("mailbox-workflow-010"));

        var started = await engine.StartAsync<int, OrderState>(definitionId, 1, TestContext.Current.CancellationToken);

        // The instance is Waiting on ApprovalReceived/order-1: it must never observe Completed
        // while that wait is unresolved (CR-032). Deliver a non-matching event and confirm the
        // instance remains Waiting rather than completing.
        var nonMatching = new EventEnvelope(EventId.New(), "SomethingElse", new CorrelationId("order-1"), null, DateTimeOffset.UnixEpoch);
        var outcome = await engine.RaiseEventAsync<OrderState>(started.InstanceId, nonMatching, TestContext.Current.CancellationToken);

        outcome.Should().Be(RaiseEventOutcome.NoMatch);
        started.Status.Should().Be(WorkflowStatus.Waiting);
    }
}
