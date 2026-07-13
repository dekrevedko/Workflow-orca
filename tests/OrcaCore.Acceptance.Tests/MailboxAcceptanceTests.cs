using AwesomeAssertions;
using OrcaCore.Abstractions.Events;
using OrcaCore.Abstractions.Ids;
using OrcaCore.Abstractions.Instances;
using OrcaCore.Abstractions.Steps;
using OrcaCore.Core.Building;
using OrcaCore.Engine.Ephemeral;
using Xunit;

namespace OrcaCore.Acceptance.Tests;

public sealed class MailboxAcceptanceTests
{
    private static readonly CorrelationId FirstCorrelation = new("first");
    private static readonly CorrelationId SecondCorrelation = new("second");

    [Fact]
    [Trait("AC", "AC-104")]
    public async Task OutOfOrderEvent_IsBufferedThenConsumed()
    {
        var state = new TestState();
        var engine = new EphemeralWorkflowEngine();
        var definition = TwoWaitDefinition(state);
        engine.RegisterDefinition(definition);
        var waiting = await StartAsync(engine, definition);

        await engine.RaiseEventAsync<TestState>(
            waiting.InstanceId,
            Event(EventId.New(), "Second", SecondCorrelation, "early"),
            TestContext.Current.CancellationToken);
        var completed = await engine.RaiseEventAsync<TestState>(
            waiting.InstanceId,
            Event(EventId.New(), "First", FirstCorrelation, "first"),
            TestContext.Current.CancellationToken);

        completed.Status.Should().Be(WorkflowStatus.Completed);
        state.Payloads.Should().Equal(["first", "early"]);
    }

    [Fact]
    [Trait("AC", "AC-105")]
    public async Task DuplicateEventId_ProducesOneConsumptionAndContinuation()
    {
        var state = new TestState();
        var engine = new EphemeralWorkflowEngine();
        var definition = TwoWaitDefinition(state);
        engine.RegisterDefinition(definition);
        var waiting = await StartAsync(engine, definition);
        var duplicateId = EventId.New();

        await engine.RaiseEventAsync<TestState>(
            waiting.InstanceId,
            Event(duplicateId, "Second", SecondCorrelation, "early"),
            TestContext.Current.CancellationToken);
        await engine.RaiseEventAsync<TestState>(
            waiting.InstanceId,
            Event(duplicateId, "Second", SecondCorrelation, "duplicate"),
            TestContext.Current.CancellationToken);
        await engine.RaiseEventAsync<TestState>(
            waiting.InstanceId,
            Event(EventId.New(), "First", FirstCorrelation, "first"),
            TestContext.Current.CancellationToken);

        state.Payloads.Should().Equal(["first", "early"]);
    }

    [Fact]
    [Trait("AC", "AC-010")]
    public async Task CompletionBlockedByUnresolvedRuntimeWork()
    {
        var state = new TestState();
        var engine = new EphemeralWorkflowEngine();
        var definition = OneWaitDefinition(state);
        engine.RegisterDefinition(definition);
        var waiting = await StartAsync(engine, definition);

        await engine.RaiseEventAsync<TestState>(
            waiting.InstanceId,
            Event(EventId.New(), "Unclaimed", new CorrelationId("pending"), "pending"),
            TestContext.Current.CancellationToken);
        var snapshot = await engine.RaiseEventAsync<TestState>(
            waiting.InstanceId,
            Event(EventId.New(), "First", FirstCorrelation, "first"),
            TestContext.Current.CancellationToken);

        snapshot.Status.Should().Be(WorkflowStatus.Failed);
        snapshot.ErrorSummary.Should().Contain("unresolved runtime work");
    }

    private static Task<WorkflowInstanceSnapshot> StartAsync(
        EphemeralWorkflowEngine engine,
        OrcaCore.Core.Definitions.WorkflowDefinition<TestState> definition)
    {
        return engine.StartAsync<string, TestState>(
            definition.DefinitionId,
            "start",
            TestContext.Current.CancellationToken);
    }

    private static OrcaCore.Core.Definitions.WorkflowDefinition<TestState> OneWaitDefinition(TestState state)
    {
        return new WorkflowBuilder<TestState>()
            .Init<string>(_ => state)
            .Wait("First", _ => FirstCorrelation)
            .Then(() => new CapturePayloadStep())
            .End()
            .Build(DefinitionId.New(), DefinitionVersion.Initial);
    }

    private static OrcaCore.Core.Definitions.WorkflowDefinition<TestState> TwoWaitDefinition(TestState state)
    {
        return new WorkflowBuilder<TestState>()
            .Init<string>(_ => state)
            .Wait("First", _ => FirstCorrelation)
            .Then(() => new CapturePayloadStep())
            .Wait("Second", _ => SecondCorrelation)
            .Then(() => new CapturePayloadStep())
            .End()
            .Build(DefinitionId.New(), DefinitionVersion.Initial);
    }

    private static EventEnvelope Event(EventId eventId, string name, CorrelationId correlationId, object? payload)
    {
        return new EventEnvelope
        {
            EventId = eventId,
            EventName = name,
            CorrelationId = correlationId,
            Payload = payload,
            OccurredAt = DateTimeOffset.UtcNow
        };
    }

    private sealed class TestState
    {
        public List<string> Payloads { get; } = [];
    }

    private sealed class CapturePayloadStep : IStep<TestState>
    {
        public ValueTask<StepResult> ExecuteAsync(
            StepContext<TestState> context,
            CancellationToken cancellationToken)
        {
            if (context.ResumedEvent?.Payload is string payload)
            {
                context.State.Payloads.Add(payload);
            }

            return ValueTask.FromResult<StepResult>(new StepResult.Completed());
        }
    }
}
