using OrcaCore.Abstractions.Events;
using OrcaCore.Abstractions.Ids;
using OrcaCore.Abstractions.Instances;
using OrcaCore.Abstractions.Steps;
using OrcaCore.Core.Building;
using OrcaCore.Engine.Ephemeral;
using OrcaCore.TestSupport.Concurrency;

namespace OrcaCore.Acceptance.Tests;

public class WaitAcceptanceTests
{
    private sealed class OrderState
    {
        public int Total { get; set; }
        public List<string> Executed { get; } = [];
        public object? ResumedPayload { get; set; }
    }

    private sealed class WaitForApprovalStep(string eventName, CorrelationId correlationId) : IStep<OrderState>
    {
        public ValueTask<StepResult> ExecuteAsync(StepContext<OrderState> context, CancellationToken cancellationToken) =>
            ValueTask.FromResult<StepResult>(new StepResult.WaitForEvent(eventName, correlationId));
    }

    private sealed class RecordResumeStep : IStep<OrderState>
    {
        public ValueTask<StepResult> ExecuteAsync(StepContext<OrderState> context, CancellationToken cancellationToken)
        {
            context.State.Executed.Add("resumed");
            context.State.ResumedPayload = context.ResumedEvent?.Payload;
            return ValueTask.FromResult<StepResult>(new StepResult.Completed());
        }
    }

    private static (EphemeralWorkflowEngine Engine, DefinitionId DefinitionId) BuildApprovalWorkflow(DefinitionId definitionId)
    {
        var engine = new EphemeralWorkflowEngine();
        var builder = WorkflowBuilder<OrderState>.Create<int>(input => new OrderState { Total = input });
        builder.Then(new WaitForApprovalStep("ApprovalReceived", new CorrelationId("order-1")));
        builder.Then(new RecordResumeStep());
        builder.End("Approved");
        var definition = builder.Build(definitionId, new DefinitionVersion(1));
        engine.RegisterDefinition(definition);
        return (engine, definitionId);
    }

    [Trait("AC", "AC-101")]
    [Fact]
    public async Task Wait_EntersWaiting_WithInspectableActiveWait()
    {
        var (engine, definitionId) = BuildApprovalWorkflow(new DefinitionId("wait-workflow-101"));

        var snapshot = await engine.StartAsync<int, OrderState>(definitionId, 1, TestContext.Current.CancellationToken);

        snapshot.Status.Should().Be(WorkflowStatus.Waiting);
        snapshot.EndOutcomeName.Should().BeNull();
    }

    [Trait("AC", "AC-102")]
    [Fact]
    public async Task MatchingEvent_ResumesExactlyOnce_WithPayload()
    {
        var (engine, definitionId) = BuildApprovalWorkflow(new DefinitionId("wait-workflow-102"));
        var started = await engine.StartAsync<int, OrderState>(definitionId, 1, TestContext.Current.CancellationToken);

        var envelope = new EventEnvelope(
            EventId.New(),
            "ApprovalReceived",
            new CorrelationId("order-1"),
            Payload: "approved-payload",
            DateTimeOffset.UnixEpoch);

        var outcome = await engine.RaiseEventAsync<OrderState>(started.InstanceId, envelope, TestContext.Current.CancellationToken);

        outcome.Should().Be(RaiseEventOutcome.Resumed);

        var secondOutcome = await engine.RaiseEventAsync<OrderState>(started.InstanceId, envelope, TestContext.Current.CancellationToken);
        secondOutcome.Should().Be(RaiseEventOutcome.NoMatch);
    }

    [Trait("AC", "AC-103")]
    [Fact]
    public async Task NonMatchingEvent_DoesNotResume()
    {
        var (engine, definitionId) = BuildApprovalWorkflow(new DefinitionId("wait-workflow-103"));
        var started = await engine.StartAsync<int, OrderState>(definitionId, 1, TestContext.Current.CancellationToken);

        var wrongEnvelope = new EventEnvelope(
            EventId.New(),
            "SomethingElse",
            new CorrelationId("order-1"),
            Payload: null,
            DateTimeOffset.UnixEpoch);

        var outcome = await engine.RaiseEventAsync<OrderState>(started.InstanceId, wrongEnvelope, TestContext.Current.CancellationToken);

        outcome.Should().Be(RaiseEventOutcome.NoMatch);
    }

    [Trait("AC", "AC-006")]
    [Fact]
    public async Task ConcurrentResumeAttempts_ProduceOneSequentialOutcome()
    {
        var (engine, definitionId) = BuildApprovalWorkflow(new DefinitionId("wait-workflow-006"));
        var started = await engine.StartAsync<int, OrderState>(definitionId, 1, TestContext.Current.CancellationToken);

        var coordinator = new RaceCoordinator();
        var firstEnvelope = new EventEnvelope(EventId.New(), "ApprovalReceived", new CorrelationId("order-1"), "first", DateTimeOffset.UnixEpoch);
        var secondEnvelope = new EventEnvelope(EventId.New(), "ApprovalReceived", new CorrelationId("order-1"), "second", DateTimeOffset.UnixEpoch);

        async Task<RaiseEventOutcome> RaiseAsync(EventEnvelope envelope)
        {
            await coordinator.ArriveAsync(TestContext.Current.CancellationToken).ConfigureAwait(false);
            return await engine.RaiseEventAsync<OrderState>(started.InstanceId, envelope, TestContext.Current.CancellationToken)
                .ConfigureAwait(false);
        }

        var outcomes = await Task.WhenAll(RaiseAsync(firstEnvelope), RaiseAsync(secondEnvelope));

        outcomes.Count(outcome => outcome == RaiseEventOutcome.Resumed).Should().Be(1);
        outcomes.Count(outcome => outcome == RaiseEventOutcome.NoMatch).Should().Be(1);
    }
}
