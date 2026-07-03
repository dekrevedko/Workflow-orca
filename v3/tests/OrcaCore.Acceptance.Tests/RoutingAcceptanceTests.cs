using OrcaCore.Abstractions.Errors;
using OrcaCore.Abstractions.Events;
using OrcaCore.Abstractions.Ids;
using OrcaCore.Abstractions.Instances;
using OrcaCore.Abstractions.Steps;
using OrcaCore.Core.Building;
using OrcaCore.Core.Definitions;
using OrcaCore.Engine.Ephemeral;

namespace OrcaCore.Acceptance.Tests;

public class RoutingAcceptanceTests
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

    private static WorkflowDefinition<OrderState> ApprovalDefinition(
        DefinitionId definitionId, string eventName = "ApprovalReceived", string correlationValue = "order-1")
    {
        var builder = WorkflowBuilder<OrderState>.Create<int>(input => new OrderState { Total = input });
        builder.Then(new WaitForEventStep(eventName, new CorrelationId(correlationValue)));
        builder.Then(new RecordResumeStep("resumed"));
        builder.End("Approved");
        return builder.Build(definitionId, new DefinitionVersion(1));
    }

    [Trait("AC", "AC-106")]
    [Fact]
    public async Task CorrelationTargetedEvent_ResumesExactlyOne()
    {
        var engine = new EphemeralWorkflowEngine();
        var definitionId = new DefinitionId("routing-acceptance-106");
        engine.RegisterDefinition(ApprovalDefinition(definitionId));

        var started = await engine.StartAsync<int, OrderState>(definitionId, 1, TestContext.Current.CancellationToken);
        started.Status.Should().Be(WorkflowStatus.Waiting);

        var envelope = new EventEnvelope(EventId.New(), "ApprovalReceived", new CorrelationId("order-1"), "payload", DateTimeOffset.UnixEpoch);

        var outcome = await engine.RaiseByCorrelationAsync<OrderState>(envelope, TestContext.Current.CancellationToken);

        outcome.Should().Be(RaiseEventOutcome.Resumed, "correlation-targeted delivery must resolve the single active wait and resume it (EV-012)");
    }

    [Trait("AC", "AC-107")]
    [Fact]
    public async Task CorrelationTargetedEvent_RejectsMissingOrAmbiguous()
    {
        var engine = new EphemeralWorkflowEngine();

        // Zero matches: no instance registered at all for this event/correlation pair.
        var missingEnvelope = new EventEnvelope(EventId.New(), "ApprovalReceived", new CorrelationId("no-such-order"), "payload", DateTimeOffset.UnixEpoch);
        var missingAct = async () => await engine.RaiseByCorrelationAsync<OrderState>(missingEnvelope, TestContext.Current.CancellationToken);
        await missingAct.Should().ThrowAsync<WorkflowRoutingException>("zero matches must fail with a clear no-active-wait error (EV-012)");

        // Ambiguous: two instances of the same definition waiting on the same key.
        var definitionId = new DefinitionId("routing-acceptance-107");
        engine.RegisterDefinition(ApprovalDefinition(definitionId));
        await engine.StartAsync<int, OrderState>(definitionId, 1, TestContext.Current.CancellationToken);
        await engine.StartAsync<int, OrderState>(definitionId, 2, TestContext.Current.CancellationToken);

        var ambiguousEnvelope = new EventEnvelope(EventId.New(), "ApprovalReceived", new CorrelationId("order-1"), "payload", DateTimeOffset.UnixEpoch);
        var ambiguousAct = async () => await engine.RaiseByCorrelationAsync<OrderState>(ambiguousEnvelope, TestContext.Current.CancellationToken);
        await ambiguousAct.Should().ThrowAsync<WorkflowRoutingException>("more than one match must fail with a clear ambiguity error (EV-012)");
    }

    [Trait("AC", "AC-108")]
    [Fact]
    public async Task DefinitionFanout_IsScopedToTargetDefinition()
    {
        var engine = new EphemeralWorkflowEngine();
        var targetDefinitionId = new DefinitionId("routing-acceptance-108-target");
        var otherDefinitionId = new DefinitionId("routing-acceptance-108-other");

        engine.RegisterDefinition(ApprovalDefinition(targetDefinitionId, "Broadcast", "shared"));
        engine.RegisterDefinition(ApprovalDefinition(otherDefinitionId, "Broadcast", "shared"));

        var targetFirst = await engine.StartAsync<int, OrderState>(targetDefinitionId, 1, TestContext.Current.CancellationToken);
        var targetSecond = await engine.StartAsync<int, OrderState>(targetDefinitionId, 2, TestContext.Current.CancellationToken);
        var otherInstance = await engine.StartAsync<int, OrderState>(otherDefinitionId, 3, TestContext.Current.CancellationToken);

        var envelope = new EventEnvelope(EventId.New(), "Broadcast", new CorrelationId("shared"), "payload", DateTimeOffset.UnixEpoch);

        var outcomes = await engine.RaiseByDefinitionAsync<OrderState>(targetDefinitionId, envelope, TestContext.Current.CancellationToken);

        outcomes.Should().HaveCount(2);
        outcomes[targetFirst.InstanceId].Should().Be(RaiseEventOutcome.Resumed);
        outcomes[targetSecond.InstanceId].Should().Be(RaiseEventOutcome.Resumed);
        outcomes.Should().NotContainKey(otherInstance.InstanceId, "fanout must be scoped to the target definition only, never engine-wide");

        // Confirm the other definition's instance is still untouched.
        var otherEnvelope = new EventEnvelope(EventId.New(), "Broadcast", new CorrelationId("shared"), "payload-2", DateTimeOffset.UnixEpoch);
        var otherOutcome = await engine.RaiseEventAsync<OrderState>(otherInstance.InstanceId, otherEnvelope, TestContext.Current.CancellationToken);
        otherOutcome.Should().Be(RaiseEventOutcome.Resumed, "the untargeted definition's instance must still have its wait active");
    }
}
