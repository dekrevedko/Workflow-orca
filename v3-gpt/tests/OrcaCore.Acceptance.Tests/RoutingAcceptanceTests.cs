using AwesomeAssertions;
using OrcaCore.Abstractions.Events;
using OrcaCore.Abstractions.Errors;
using OrcaCore.Abstractions.Ids;
using OrcaCore.Abstractions.Instances;
using OrcaCore.Abstractions.Steps;
using OrcaCore.Core.Building;
using OrcaCore.Engine.Ephemeral;
using Xunit;

namespace OrcaCore.Acceptance.Tests;

public sealed class RoutingAcceptanceTests
{
    private static readonly CorrelationId Correlation = new("shared");

    [Fact]
    [Trait("AC", "AC-106")]
    public async Task CorrelationTargetedEvent_ResumesExactlyOne()
    {
        var state = new TestState();
        var engine = new EphemeralWorkflowEngine();
        var definition = Definition(state);
        engine.RegisterDefinition(definition);
        await StartAsync(engine, definition);

        var snapshot = await engine.RaiseEventByCorrelationAsync<TestState>(
            Event("Approved", Correlation, "payload"),
            TestContext.Current.CancellationToken);

        snapshot.Status.Should().Be(WorkflowStatus.Completed);
        state.Payloads.Should().Equal(["payload"]);
    }

    [Fact]
    [Trait("AC", "AC-107")]
    public async Task CorrelationTargetedEvent_RejectsMissingOrAmbiguous()
    {
        var engine = new EphemeralWorkflowEngine();
        var missing = async () => await engine.RaiseEventByCorrelationAsync<TestState>(
            Event("Approved", Correlation, "payload"),
            TestContext.Current.CancellationToken);

        await missing.Should().ThrowAsync<WorkflowRoutingException>()
            .WithMessage("*no active wait*");

        var first = Definition(new TestState());
        var second = Definition(new TestState());
        engine.RegisterDefinition(first);
        engine.RegisterDefinition(second);
        await StartAsync(engine, first);
        await StartAsync(engine, second);

        var ambiguous = async () => await engine.RaiseEventByCorrelationAsync<TestState>(
            Event("Approved", Correlation, "payload"),
            TestContext.Current.CancellationToken);

        await ambiguous.Should().ThrowAsync<WorkflowRoutingException>()
            .WithMessage("*ambiguous*");
    }

    [Fact]
    [Trait("AC", "AC-108")]
    public async Task DefinitionFanout_IsScopedToTargetDefinition()
    {
        var targetState = new TestState();
        var otherState = new TestState();
        var engine = new EphemeralWorkflowEngine();
        var target = Definition(targetState);
        var other = Definition(otherState);
        engine.RegisterDefinition(target);
        engine.RegisterDefinition(other);
        await StartAsync(engine, target);
        await StartAsync(engine, other);

        var snapshots = await engine.RaiseEventByDefinitionAsync<TestState>(
            target.DefinitionId,
            Event("Approved", Correlation, "payload"),
            TestContext.Current.CancellationToken);

        snapshots.Should().ContainSingle();
        targetState.Payloads.Should().Equal(["payload"]);
        otherState.Payloads.Should().BeEmpty();
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
