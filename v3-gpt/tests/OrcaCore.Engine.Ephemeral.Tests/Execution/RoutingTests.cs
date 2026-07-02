using AwesomeAssertions;
using OrcaCore.Abstractions.Events;
using OrcaCore.Abstractions.Errors;
using OrcaCore.Abstractions.Ids;
using OrcaCore.Abstractions.Instances;
using OrcaCore.Abstractions.Steps;
using OrcaCore.Core.Building;
using OrcaCore.Engine.Ephemeral;
using Xunit;

namespace OrcaCore.Engine.Ephemeral.Tests.Execution;

public sealed class RoutingTests
{
    private static readonly CorrelationId Correlation = new("shared");

    [Fact]
    public async Task RaiseByCorrelationAsync_OneActiveWait_ResumesThatInstance()
    {
        var state = new TestState();
        var engine = new EphemeralWorkflowEngine();
        var definition = Definition(state, "Approved", Correlation);
        engine.RegisterDefinition(definition);
        await StartAsync(engine, definition);

        var snapshot = await engine.RaiseEventByCorrelationAsync<TestState>(
            Event("Approved", Correlation, "payload"),
            TestContext.Current.CancellationToken);

        snapshot.Status.Should().Be(WorkflowStatus.Completed);
        state.Payloads.Should().Equal(["payload"]);
    }

    [Fact]
    public async Task RaiseByCorrelationAsync_ZeroActiveWaits_ReturnsNoActiveWait()
    {
        var engine = new EphemeralWorkflowEngine();

        var act = async () => await engine.RaiseEventByCorrelationAsync<TestState>(
            Event("Approved", Correlation, "payload"),
            TestContext.Current.CancellationToken);

        await act.Should().ThrowAsync<WorkflowRoutingException>()
            .WithMessage("*no active wait*");
    }

    [Fact]
    public async Task RaiseByCorrelationAsync_MultipleActiveWaits_ReturnsAmbiguousWithoutDelivery()
    {
        var firstState = new TestState();
        var secondState = new TestState();
        var engine = new EphemeralWorkflowEngine();
        var first = Definition(firstState, "Approved", Correlation);
        var second = Definition(secondState, "Approved", Correlation);
        engine.RegisterDefinition(first);
        engine.RegisterDefinition(second);
        await StartAsync(engine, first);
        await StartAsync(engine, second);

        var act = async () => await engine.RaiseEventByCorrelationAsync<TestState>(
            Event("Approved", Correlation, "payload"),
            TestContext.Current.CancellationToken);

        await act.Should().ThrowAsync<WorkflowRoutingException>()
            .WithMessage("*ambiguous*");
        firstState.Payloads.Should().BeEmpty();
        secondState.Payloads.Should().BeEmpty();
    }

    [Fact]
    public async Task RaiseByDefinitionAsync_MultipleDefinitions_DeliversOnlyTargetDefinition()
    {
        var targetState = new TestState();
        var otherState = new TestState();
        var engine = new EphemeralWorkflowEngine();
        var target = Definition(targetState, "Approved", Correlation);
        var other = Definition(otherState, "Approved", Correlation);
        engine.RegisterDefinition(target);
        engine.RegisterDefinition(other);
        await StartAsync(engine, target);
        await StartAsync(engine, other);

        var snapshots = await engine.RaiseEventByDefinitionAsync<TestState>(
            target.DefinitionId,
            Event("Approved", Correlation, "payload"),
            TestContext.Current.CancellationToken);

        snapshots.Should().ContainSingle()
            .Which.DefinitionId.Should().Be(target.DefinitionId);
        targetState.Payloads.Should().Equal(["payload"]);
        otherState.Payloads.Should().BeEmpty();
    }

    [Fact]
    public async Task WaitRegistration_DuplicateCorrelationAcrossInstances_Succeeds()
    {
        var engine = new EphemeralWorkflowEngine();
        var first = Definition(new TestState(), "Approved", Correlation);
        var second = Definition(new TestState(), "Approved", Correlation);
        engine.RegisterDefinition(first);
        engine.RegisterDefinition(second);

        var firstSnapshot = await StartAsync(engine, first);
        var secondSnapshot = await StartAsync(engine, second);

        firstSnapshot.Status.Should().Be(WorkflowStatus.Waiting);
        secondSnapshot.Status.Should().Be(WorkflowStatus.Waiting);
    }

    [Fact]
    public async Task WaitMatch_RemovesWaitFromCorrelationIndex()
    {
        var state = new TestState();
        var engine = new EphemeralWorkflowEngine();
        var definition = Definition(state, "Approved", Correlation);
        engine.RegisterDefinition(definition);
        await StartAsync(engine, definition);
        await engine.RaiseEventByCorrelationAsync<TestState>(
            Event("Approved", Correlation, "payload"),
            TestContext.Current.CancellationToken);

        var act = async () => await engine.RaiseEventByCorrelationAsync<TestState>(
            Event("Approved", Correlation, "again"),
            TestContext.Current.CancellationToken);

        await act.Should().ThrowAsync<WorkflowRoutingException>()
            .WithMessage("*no active wait*");
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

    private static OrcaCore.Core.Definitions.WorkflowDefinition<TestState> Definition(
        TestState state,
        string eventName,
        CorrelationId correlationId)
    {
        return new WorkflowBuilder<TestState>()
            .Init<string>(_ => state)
            .Wait(eventName, _ => correlationId)
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
