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
    private static readonly CorrelationId FirstCorrelation = CorrelationId.Create("first");
    private static readonly CorrelationId SecondCorrelation = CorrelationId.Create("second");

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
            Event(EventId.Create(Guid.CreateVersion7().ToString()), "Second", SecondCorrelation, "early"),
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
            Event(EventId.Create(Guid.CreateVersion7().ToString()), "Second", SecondCorrelation, "early-second"),
            TestContext.Current.CancellationToken);
        var completed = await engine.RaiseEventAsync<TestState>(
            waiting.InstanceId,
            Event(EventId.Create(Guid.CreateVersion7().ToString()), "First", FirstCorrelation, "first"),
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
        var eventId = EventId.Create(Guid.CreateVersion7().ToString());

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
            Event(EventId.Create(Guid.CreateVersion7().ToString()), "First", FirstCorrelation, "first"),
            TestContext.Current.CancellationToken);

        state.Payloads.Should().Equal(["first", "early-second"]);
    }

    [Fact]
    public async Task RaiseEventAsync_WhenMailboxLimitReached_RejectsAdditionalUnmatchedEvent()
    {
        var state = new TestState();
        var engine = new EphemeralWorkflowEngine(TimeProvider.System, new EphemeralWorkflowEngineOptions
        {
            MaxPendingEventsPerInstance = 1
        });
        var definition = OneWaitDefinition(state);
        engine.RegisterDefinition(definition);
        var waiting = await StartAsync(engine, definition);
        await engine.RaiseEventAsync<TestState>(
            waiting.InstanceId,
            Event(EventId.Create(Guid.CreateVersion7().ToString()), "Unmatched", CorrelationId.Create("one"), "first"),
            TestContext.Current.CancellationToken);

        var second = () => engine.RaiseEventAsync<TestState>(
            waiting.InstanceId,
            Event(EventId.Create(Guid.CreateVersion7().ToString()), "Unmatched", CorrelationId.Create("two"), "second"),
            TestContext.Current.CancellationToken);

        await second.Should().ThrowAsync<OrcaCore.Abstractions.Errors.WorkflowRoutingException>()
            .WithMessage("*mailbox*limit*1*");
    }

    [Fact]
    public async Task RaiseEventAsync_InvalidEnvelope_IsRejectedBeforeMutation()
    {
        var state = new TestState();
        var engine = new EphemeralWorkflowEngine();
        var definition = OneWaitDefinition(state);
        engine.RegisterDefinition(definition);
        var waiting = await StartAsync(engine, definition);
        var invalid = new EventEnvelope
        {
            EventId = null!,
            EventName = "First",
            CorrelationId = FirstCorrelation,
            OccurredAt = DateTimeOffset.UtcNow
        };

        var act = () => engine.RaiseEventAsync<TestState>(
            waiting.InstanceId,
            invalid,
            TestContext.Current.CancellationToken);

        await act.Should().ThrowAsync<ArgumentException>().WithMessage("*EventId*");
        engine.Management.Instance(waiting.InstanceId).Get().ActiveWaits.Should().ContainSingle();
    }

    [Fact]
    public async Task RaiseEventAsync_DuplicateConsumedEvent_DoesNotContinueAgain()
    {
        var state = new TestState();
        var engine = new EphemeralWorkflowEngine();
        var definition = OneWaitDefinition(state);
        engine.RegisterDefinition(definition);
        var waiting = await StartAsync(engine, definition);
        var eventId = EventId.Create(Guid.CreateVersion7().ToString());
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
    [Trait("Scenario", "EDGE-EV-008")]
    [Trait("AC", "AC-105")]
    public async Task EDGE_EV_008_ConcurrentDuplicateRaiseEvent_SameEventId_ResumesExactlyOnce()
    {
        var state = new TestState();
        var engine = new EphemeralWorkflowEngine();
        var definition = OneWaitDefinition(state);
        engine.RegisterDefinition(definition);
        var waiting = await StartAsync(engine, definition);
        var envelope = Event(EventId.Create(Guid.CreateVersion7().ToString()), "First", FirstCorrelation, "first");

        var gate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var deliveries = Enumerable.Range(0, 8)
            .Select(async _ =>
            {
                await gate.Task;
                return await engine.RaiseEventAsync<TestState>(
                    waiting.InstanceId,
                    envelope,
                    TestContext.Current.CancellationToken);
            })
            .ToArray();
        gate.SetResult();
        var snapshots = await Task.WhenAll(deliveries);

        snapshots.Should().OnlyContain(snapshot => snapshot.Status == WorkflowStatus.Completed);
        state.Payloads.Should().Equal(["first"], "exactly-once resume (EV-023) must hold under concurrent duplicate delivery");
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
        return global::OrcaCore.Workflow.Ephemeral<TestState>(DefinitionId.New(), DefinitionVersion.Initial)
            .Init<string>(_ => state)
            .Wait("First", _ => FirstCorrelation)
            .Then(() => new CapturePayloadStep())
            .End()
            .Build();
    }

    private static OrcaCore.Core.Definitions.WorkflowDefinition<TestState> TwoWaitDefinition(TestState state)
    {
        return global::OrcaCore.Workflow.Ephemeral<TestState>(DefinitionId.New(), DefinitionVersion.Initial)
            .Init<string>(_ => state)
            .Wait("First", _ => FirstCorrelation)
            .Then(() => new CapturePayloadStep())
            .Wait("Second", _ => SecondCorrelation)
            .Then(() => new CapturePayloadStep())
            .End()
            .Build();
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

        public int ResumeAttempts { get; set; }
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

    private sealed class UnsupportedOnceResultStep : IStep<TestState>
    {
        public ValueTask<StepResult> ExecuteAsync(
            StepContext<TestState> context,
            CancellationToken cancellationToken)
        {
            context.State.ResumeAttempts++;
            if (context.State.ResumeAttempts == 1)
            {
                return ValueTask.FromResult(
                    global::OrcaCore.TestSupport.LegacyStepResults.ContinueAsNew(context.State));
            }

            if (context.ResumedEvent?.Payload is string payload)
            {
                context.State.Payloads.Add(payload);
            }

            return ValueTask.FromResult<StepResult>(new StepResult.Completed());
        }
    }

}
