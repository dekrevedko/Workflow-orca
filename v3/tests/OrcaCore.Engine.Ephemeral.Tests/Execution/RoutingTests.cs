using OrcaCore.Abstractions.Errors;
using OrcaCore.Abstractions.Events;
using OrcaCore.Abstractions.Ids;
using OrcaCore.Abstractions.Instances;
using OrcaCore.Abstractions.Steps;
using OrcaCore.Core.Building;
using OrcaCore.Core.Definitions;
using OrcaCore.Engine.Ephemeral;

namespace OrcaCore.Engine.Ephemeral.Tests.Execution;

public class RoutingTests
{
    private sealed class OrderState
    {
        public int Total { get; set; }
        public List<string> Executed { get; } = [];
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
            return ValueTask.FromResult<StepResult>(new StepResult.Completed());
        }
    }

    private static (EphemeralWorkflowEngine Engine, DefinitionId DefinitionId) BuildApprovalWorkflow(
        DefinitionId definitionId, string eventName = "ApprovalReceived", string correlationValue = "order-1")
    {
        var engine = new EphemeralWorkflowEngine();
        var builder = WorkflowBuilder<OrderState>.Create<int>(input => new OrderState { Total = input });
        builder.Then(new WaitForEventStep(eventName, new CorrelationId(correlationValue)));
        builder.Then(new RecordResumeStep("resumed"));
        builder.End("Approved");
        var definition = builder.Build(definitionId, new DefinitionVersion(1));
        engine.RegisterDefinition(definition);
        return (engine, definitionId);
    }

    [Fact]
    public async Task RaiseByCorrelationAsync_OneActiveWait_ResumesThatInstance()
    {
        var (engine, definitionId) = BuildApprovalWorkflow(new DefinitionId("routing-workflow-1"));
        var started = await engine.StartAsync<int, OrderState>(definitionId, 1, TestContext.Current.CancellationToken);

        var envelope = new EventEnvelope(EventId.New(), "ApprovalReceived", new CorrelationId("order-1"), "payload", DateTimeOffset.UnixEpoch);

        var outcome = await engine.RaiseByCorrelationAsync<OrderState>(envelope, TestContext.Current.CancellationToken);

        outcome.Should().Be(RaiseEventOutcome.Resumed);
    }

    [Fact]
    public async Task RaiseByCorrelationAsync_ZeroActiveWaits_ReturnsNoActiveWait()
    {
        var engine = new EphemeralWorkflowEngine();
        var envelope = new EventEnvelope(EventId.New(), "ApprovalReceived", new CorrelationId("order-1"), "payload", DateTimeOffset.UnixEpoch);

        var act = async () => await engine.RaiseByCorrelationAsync<OrderState>(envelope, TestContext.Current.CancellationToken);

        (await act.Should().ThrowAsync<WorkflowRoutingException>())
            .WithMessage("*no active wait*", "the exception message should clearly state the no-match case (EV-012)");
    }

    [Fact]
    public async Task RaiseByCorrelationAsync_MultipleActiveWaits_ReturnsAmbiguousWithoutDelivery()
    {
        var engine = new EphemeralWorkflowEngine();
        var definitionId = new DefinitionId("routing-workflow-ambiguous");
        var builder = WorkflowBuilder<OrderState>.Create<int>(input => new OrderState { Total = input });
        builder.Then(new WaitForEventStep("ApprovalReceived", new CorrelationId("order-1")));
        builder.Then(new RecordResumeStep("resumed"));
        builder.End("Approved");
        var definition = builder.Build(definitionId, new DefinitionVersion(1));
        engine.RegisterDefinition(definition);

        var first = await engine.StartAsync<int, OrderState>(definitionId, 1, TestContext.Current.CancellationToken);
        var second = await engine.StartAsync<int, OrderState>(definitionId, 2, TestContext.Current.CancellationToken);

        var envelope = new EventEnvelope(EventId.New(), "ApprovalReceived", new CorrelationId("order-1"), "payload", DateTimeOffset.UnixEpoch);

        var act = async () => await engine.RaiseByCorrelationAsync<OrderState>(envelope, TestContext.Current.CancellationToken);

        (await act.Should().ThrowAsync<WorkflowRoutingException>())
            .WithMessage("*instance-targeted*", "the exception message should direct the caller to another routing mode (EV-012)");

        // Neither instance should have been resumed - ambiguity must not deliver to anyone.
        var stillWaitingOutcome = await engine.RaiseEventAsync<OrderState>(first.InstanceId, envelope, TestContext.Current.CancellationToken);
        stillWaitingOutcome.Should().Be(RaiseEventOutcome.Resumed, "the first instance's wait must still be active - ambiguity must not have delivered to it already");

        var secondEnvelope = new EventEnvelope(EventId.New(), "ApprovalReceived", new CorrelationId("order-1"), "payload-2", DateTimeOffset.UnixEpoch);
        var secondStillWaitingOutcome = await engine.RaiseEventAsync<OrderState>(second.InstanceId, secondEnvelope, TestContext.Current.CancellationToken);
        secondStillWaitingOutcome.Should().Be(RaiseEventOutcome.Resumed, "the second instance's wait must still be active - ambiguity must not have delivered to it already");
    }

    [Fact]
    public async Task RaiseByDefinitionAsync_MultipleDefinitions_DeliversOnlyTargetDefinition()
    {
        var engine = new EphemeralWorkflowEngine();
        var targetDefinitionId = new DefinitionId("routing-workflow-target");
        var otherDefinitionId = new DefinitionId("routing-workflow-other");

        var targetBuilder = WorkflowBuilder<OrderState>.Create<int>(input => new OrderState { Total = input });
        targetBuilder.Then(new WaitForEventStep("Broadcast", new CorrelationId("shared")));
        targetBuilder.Then(new RecordResumeStep("resumed"));
        targetBuilder.End("Done");
        engine.RegisterDefinition(targetBuilder.Build(targetDefinitionId, new DefinitionVersion(1)));

        var otherBuilder = WorkflowBuilder<OrderState>.Create<int>(input => new OrderState { Total = input });
        otherBuilder.Then(new WaitForEventStep("Broadcast", new CorrelationId("shared")));
        otherBuilder.Then(new RecordResumeStep("resumed"));
        otherBuilder.End("Done");
        engine.RegisterDefinition(otherBuilder.Build(otherDefinitionId, new DefinitionVersion(1)));

        var targetInstance = await engine.StartAsync<int, OrderState>(targetDefinitionId, 1, TestContext.Current.CancellationToken);
        var otherInstance = await engine.StartAsync<int, OrderState>(otherDefinitionId, 2, TestContext.Current.CancellationToken);

        var envelope = new EventEnvelope(EventId.New(), "Broadcast", new CorrelationId("shared"), "payload", DateTimeOffset.UnixEpoch);

        var outcomes = await engine.RaiseByDefinitionAsync<OrderState>(targetDefinitionId, envelope, TestContext.Current.CancellationToken);

        outcomes.Should().ContainKey(targetInstance.InstanceId);
        outcomes[targetInstance.InstanceId].Should().Be(RaiseEventOutcome.Resumed);
        outcomes.Should().NotContainKey(otherInstance.InstanceId, "fanout must not touch instances of other definitions");

        // The other definition's instance must still be untouched/waiting.
        var otherOutcome = await engine.RaiseEventAsync<OrderState>(otherInstance.InstanceId, envelope, TestContext.Current.CancellationToken);
        otherOutcome.Should().Be(RaiseEventOutcome.Resumed, "the other definition's instance must still have its wait active - fanout must not have touched it");
    }

    [Fact]
    public async Task WaitRegistration_DuplicateCorrelationAcrossInstances_Succeeds()
    {
        var engine = new EphemeralWorkflowEngine();
        var definitionId = new DefinitionId("routing-workflow-duplicate-registration");
        var builder = WorkflowBuilder<OrderState>.Create<int>(input => new OrderState { Total = input });
        builder.Then(new WaitForEventStep("ApprovalReceived", new CorrelationId("order-1")));
        builder.Then(new RecordResumeStep("resumed"));
        builder.End("Approved");
        var definition = builder.Build(definitionId, new DefinitionVersion(1));
        engine.RegisterDefinition(definition);

        // Two different instances waiting on the exact same (EventName, CorrelationId) - EV-011
        // says wait registration always succeeds with no registration-time uniqueness check.
        var first = await engine.StartAsync<int, OrderState>(definitionId, 1, TestContext.Current.CancellationToken);
        var second = await engine.StartAsync<int, OrderState>(definitionId, 2, TestContext.Current.CancellationToken);

        first.Status.Should().Be(WorkflowStatus.Waiting);
        second.Status.Should().Be(WorkflowStatus.Waiting);
    }

    [Fact]
    public async Task WaitMatch_RemovesWaitFromCorrelationIndex()
    {
        var (engine, definitionId) = BuildApprovalWorkflow(new DefinitionId("routing-workflow-removal"));
        var started = await engine.StartAsync<int, OrderState>(definitionId, 1, TestContext.Current.CancellationToken);

        var envelope = new EventEnvelope(EventId.New(), "ApprovalReceived", new CorrelationId("order-1"), "payload", DateTimeOffset.UnixEpoch);
        var resumeOutcome = await engine.RaiseByCorrelationAsync<OrderState>(envelope, TestContext.Current.CancellationToken);
        resumeOutcome.Should().Be(RaiseEventOutcome.Resumed);

        // Once matched, the wait must be gone from the index: a second correlation-targeted
        // delivery for the same key must find zero active waits, not stale re-delivery.
        var secondEnvelope = new EventEnvelope(EventId.New(), "ApprovalReceived", new CorrelationId("order-1"), "payload-2", DateTimeOffset.UnixEpoch);
        var act = async () => await engine.RaiseByCorrelationAsync<OrderState>(secondEnvelope, TestContext.Current.CancellationToken);

        await act.Should().ThrowAsync<WorkflowRoutingException>();
    }
}
