using AwesomeAssertions;
using OrcaCore.Abstractions.Events;
using OrcaCore.Abstractions.Ids;
using OrcaCore.Abstractions.Instances;
using OrcaCore.Abstractions.Steps;
using OrcaCore.Core.Building;
using OrcaCore.Engine.Ephemeral;
using OrcaCore.Engine.Ephemeral.Execution;
using Xunit;

namespace OrcaCore.Engine.Ephemeral.Tests.Execution;

public sealed class MailboxTests
{
    private static readonly CorrelationId FirstCorrelation = new("first");
    private static readonly CorrelationId SecondCorrelation = new("second");

    [Fact]
    public async Task RaiseEventAsync_BeforeWait_BuffersEvent()
    {
        var state = new TestState();
        var engine = new EphemeralWorkflowEngine();
        var definition = TwoWaitDefinition(state);
        engine.RegisterDefinition(definition);
        var waiting = await StartAsync(engine, definition);

        var snapshot = await engine.RaiseEventAsync<TestState>(
            waiting.InstanceId,
            Event(EventId.New(), "Second", SecondCorrelation, "early"),
            TestContext.Current.CancellationToken);

        snapshot.Status.Should().Be(WorkflowStatus.Waiting);
        snapshot.ActiveWaits.Should().ContainSingle()
            .Which.EventName.Should().Be("First");
        state.Payloads.Should().BeEmpty();
    }

    [Fact]
    public async Task Run_RegisteringMatchingWait_ConsumesBufferedEventAndContinues()
    {
        var state = new TestState();
        var engine = new EphemeralWorkflowEngine();
        var definition = TwoWaitDefinition(state);
        engine.RegisterDefinition(definition);
        var waiting = await StartAsync(engine, definition);

        await engine.RaiseEventAsync<TestState>(
            waiting.InstanceId,
            Event(EventId.New(), "Second", SecondCorrelation, "early-second"),
            TestContext.Current.CancellationToken);
        var completed = await engine.RaiseEventAsync<TestState>(
            waiting.InstanceId,
            Event(EventId.New(), "First", FirstCorrelation, "first"),
            TestContext.Current.CancellationToken);

        completed.Status.Should().Be(WorkflowStatus.Completed);
        completed.ActiveWaits.Should().BeEmpty();
        state.Payloads.Should().Equal(["first", "early-second"]);
    }

    [Fact]
    public async Task RaiseEventAsync_DuplicatePendingEvent_BuffersOnlyOnce()
    {
        var state = new TestState();
        var engine = new EphemeralWorkflowEngine();
        var definition = TwoWaitDefinition(state);
        engine.RegisterDefinition(definition);
        var waiting = await StartAsync(engine, definition);
        var eventId = EventId.New();

        await engine.RaiseEventAsync<TestState>(
            waiting.InstanceId,
            Event(eventId, "Second", SecondCorrelation, "early-second"),
            TestContext.Current.CancellationToken);
        await engine.RaiseEventAsync<TestState>(
            waiting.InstanceId,
            Event(eventId, "Second", SecondCorrelation, "duplicate"),
            TestContext.Current.CancellationToken);
        await engine.RaiseEventAsync<TestState>(
            waiting.InstanceId,
            Event(EventId.New(), "First", FirstCorrelation, "first"),
            TestContext.Current.CancellationToken);

        state.Payloads.Should().Equal(["first", "early-second"]);
    }

    [Fact]
    public async Task RaiseEventAsync_DuplicateConsumedEvent_DoesNotContinueAgain()
    {
        var state = new TestState();
        var engine = new EphemeralWorkflowEngine();
        var definition = OneWaitDefinition(state);
        engine.RegisterDefinition(definition);
        var waiting = await StartAsync(engine, definition);
        var eventId = EventId.New();
        var envelope = Event(eventId, "First", FirstCorrelation, "first");

        await engine.RaiseEventAsync<TestState>(
            waiting.InstanceId,
            envelope,
            TestContext.Current.CancellationToken);
        var snapshot = await engine.RaiseEventAsync<TestState>(
            waiting.InstanceId,
            envelope,
            TestContext.Current.CancellationToken);

        snapshot.Status.Should().Be(WorkflowStatus.Completed);
        state.Payloads.Should().Equal(["first"]);
    }

    [Fact]
    public async Task RaiseEventAsync_ResumeTransitionFails_EventRemainsAvailable()
    {
        var state = new TestState();
        var registry = new InMemoryInstanceRegistry();
        var engine = new EphemeralWorkflowEngine(TimeProvider.System, registry, new InstanceExecutionLane());
        var definition = new WorkflowBuilder<TestState>()
            .Init<string>(_ => state)
            .Wait("First", _ => FirstCorrelation)
            .Then(() => new UnsupportedResultStep())
            .End()
            .Build(DefinitionId.New(), DefinitionVersion.Initial);
        engine.RegisterDefinition(definition);
        var waiting = await StartAsync(engine, definition);
        var envelope = Event(EventId.New(), "First", FirstCorrelation, "first");

        var act = async () => await engine.RaiseEventAsync<TestState>(
            waiting.InstanceId,
            envelope,
            TestContext.Current.CancellationToken);

        await act.Should().ThrowAsync<NotSupportedException>()
            .WithMessage("*UnsupportedResult*");
        registry.TryGet(waiting.InstanceId, out var instance).Should().BeTrue();
        var snapshot = ((WorkflowInstance<TestState>)instance!).ToSnapshot();
        snapshot.Status.Should().Be(WorkflowStatus.Waiting);
        snapshot.ActiveWaits.Should().ContainSingle();
    }

    [Fact]
    public async Task Run_EndWithActiveWait_FailsOrCancelsByExplicitPolicy()
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
        var completed = await engine.RaiseEventAsync<TestState>(
            waiting.InstanceId,
            Event(EventId.New(), "First", FirstCorrelation, "first"),
            TestContext.Current.CancellationToken);

        completed.Status.Should().Be(WorkflowStatus.Failed);
        completed.ErrorSummary.Should().Contain("unresolved runtime work");
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

    private sealed class UnsupportedResultStep : IStep<TestState>
    {
        public ValueTask<StepResult> ExecuteAsync(
            StepContext<TestState> context,
            CancellationToken cancellationToken)
        {
            return ValueTask.FromResult<StepResult>(new UnsupportedResult());
        }
    }

    private sealed record UnsupportedResult : StepResult;
}
