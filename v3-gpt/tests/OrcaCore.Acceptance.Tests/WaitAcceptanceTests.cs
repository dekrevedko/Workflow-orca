using AwesomeAssertions;
using OrcaCore.Abstractions.Events;
using OrcaCore.Abstractions.Ids;
using OrcaCore.Abstractions.Instances;
using OrcaCore.Abstractions.Steps;
using OrcaCore.Core.Building;
using OrcaCore.Engine.Ephemeral;
using Xunit;

namespace OrcaCore.Acceptance.Tests;

public sealed class WaitAcceptanceTests
{
    private static readonly CorrelationId Correlation = new("order-123");

    [Fact]
    [Trait("AC", "AC-101")]
    public async Task Wait_EntersWaiting_WithInspectableActiveWait()
    {
        var state = new TestState();
        var engine = new EphemeralWorkflowEngine();
        var definition = Definition(state);
        engine.RegisterDefinition(definition);

        var snapshot = await engine.StartAsync<string, TestState>(
            definition.DefinitionId,
            "start",
            TestContext.Current.CancellationToken);

        snapshot.Status.Should().Be(WorkflowStatus.Waiting);
        snapshot.ActiveWaits.Should().ContainSingle()
            .Which.EventName.Should().Be("Approved");
    }

    [Fact]
    [Trait("AC", "AC-102")]
    public async Task MatchingEvent_ResumesExactlyOnce_WithPayload()
    {
        var state = new TestState();
        var engine = new EphemeralWorkflowEngine();
        var definition = Definition(state);
        engine.RegisterDefinition(definition);
        var waiting = await engine.StartAsync<string, TestState>(
            definition.DefinitionId,
            "start",
            TestContext.Current.CancellationToken);

        var resumed = await engine.RaiseEventAsync<TestState>(
            waiting.InstanceId,
            Event("Approved", Correlation, "accepted"),
            TestContext.Current.CancellationToken);

        resumed.Status.Should().Be(WorkflowStatus.Completed);
        state.Payloads.Should().Equal(["accepted"]);
    }

    [Fact]
    [Trait("AC", "AC-103")]
    public async Task NonMatchingEvent_DoesNotResume()
    {
        var state = new TestState();
        var engine = new EphemeralWorkflowEngine();
        var definition = Definition(state);
        engine.RegisterDefinition(definition);
        var waiting = await engine.StartAsync<string, TestState>(
            definition.DefinitionId,
            "start",
            TestContext.Current.CancellationToken);

        var snapshot = await engine.RaiseEventAsync<TestState>(
            waiting.InstanceId,
            Event("Approved", new CorrelationId("other"), "ignored"),
            TestContext.Current.CancellationToken);

        snapshot.Status.Should().Be(WorkflowStatus.Waiting);
        state.Payloads.Should().BeEmpty();
    }

    [Fact]
    [Trait("AC", "AC-006")]
    public async Task ConcurrentResumeAttempts_ProduceOneSequentialOutcome()
    {
        var state = new TestState();
        var engine = new EphemeralWorkflowEngine();
        var definition = Definition(state);
        engine.RegisterDefinition(definition);
        var waiting = await engine.StartAsync<string, TestState>(
            definition.DefinitionId,
            "start",
            TestContext.Current.CancellationToken);

        var first = engine.RaiseEventAsync<TestState>(
            waiting.InstanceId,
            Event("Approved", Correlation, "first"),
            TestContext.Current.CancellationToken);
        var second = engine.RaiseEventAsync<TestState>(
            waiting.InstanceId,
            Event("Approved", Correlation, "second"),
            TestContext.Current.CancellationToken);

        await Task.WhenAll(first, second).WaitAsync(TestContext.Current.CancellationToken);

        state.Payloads.Should().HaveCount(1);
    }

    private static OrcaCore.Core.Definitions.WorkflowDefinition<TestState> Definition(TestState state)
    {
        return new WorkflowBuilder<TestState>()
            .Init<string>(_ => state)
            .Wait("Approved", _ => Correlation)
            .Then(() => new CapturePayloadStep())
            .End()
            .Build(DefinitionId.New(), DefinitionVersion.Initial);
    }

    private static EventEnvelope Event(string name, CorrelationId correlationId, object? payload)
    {
        return new EventEnvelope
        {
            EventId = EventId.New(),
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
