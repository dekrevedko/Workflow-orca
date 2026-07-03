using AwesomeAssertions;
using OrcaCore.Abstractions.Events;
using OrcaCore.Abstractions.Ids;
using OrcaCore.Abstractions.Instances;
using OrcaCore.Abstractions.Steps;
using OrcaCore.Core.Building;
using OrcaCore.Engine.Ephemeral;
using OrcaCore.TestSupport;
using Xunit;

namespace OrcaCore.Engine.Ephemeral.Tests.Timers;

public sealed class TimerEventRaceTests
{
    private static readonly CorrelationId Correlation = new("order-123");

    [Fact]
    public async Task EventBeforeTimeout_ConsumesEventAndCancelsTimer()
    {
        var clock = new Clock(new DateTimeOffset(2026, 7, 2, 12, 0, 0, TimeSpan.Zero));
        var engine = new EphemeralWorkflowEngine(clock.TimeProvider);
        var state = new RaceState();
        var definition = Definition(state, TimeSpan.FromMinutes(5));
        engine.RegisterDefinition(definition);
        var waiting = await engine.StartAsync<string, RaceState>(
            definition.DefinitionId,
            "start",
            TestContext.Current.CancellationToken);

        var resumed = await engine.RaiseEventAsync<RaceState>(
            waiting.InstanceId,
            Event(clock.Now, "accepted"),
            TestContext.Current.CancellationToken);
        clock.Advance(TimeSpan.FromMinutes(5));
        var dueTimers = await engine.FireDueTimersAsync(TestContext.Current.CancellationToken);

        resumed.Status.Should().Be(WorkflowStatus.Completed);
        dueTimers.Should().BeEmpty();
        state.Outcomes.Should().Equal(["event:accepted"]);
    }

    [Fact]
    public async Task TimeoutBeforeEvent_CancelsWaitAndContinues()
    {
        var clock = new Clock(new DateTimeOffset(2026, 7, 2, 12, 0, 0, TimeSpan.Zero));
        var engine = new EphemeralWorkflowEngine(clock.TimeProvider);
        var state = new RaceState();
        var definition = Definition(state, TimeSpan.FromMinutes(5));
        engine.RegisterDefinition(definition);
        await engine.StartAsync<string, RaceState>(
            definition.DefinitionId,
            "start",
            TestContext.Current.CancellationToken);
        clock.Advance(TimeSpan.FromMinutes(5));

        var fired = await engine.FireDueTimersAsync(TestContext.Current.CancellationToken);

        fired.Should().ContainSingle()
            .Which.Status.Should().Be(WorkflowStatus.Completed);
        state.Outcomes.Should().Equal(["timeout"]);
    }

    [Fact]
    [Trait("AC", "AC-112")]
    public async Task TimeoutBeforeLateEvent_ConsumesTimedOutWaitSignature()
    {
        var clock = new Clock(new DateTimeOffset(2026, 7, 2, 12, 0, 0, TimeSpan.Zero));
        var engine = new EphemeralWorkflowEngine(clock.TimeProvider);
        var state = new RaceState();
        var definition = ReusedCorrelationDefinition(state, TimeSpan.FromMinutes(5));
        engine.RegisterDefinition(definition);
        await engine.StartAsync<string, RaceState>(
            definition.DefinitionId,
            "start",
            TestContext.Current.CancellationToken);
        clock.Advance(TimeSpan.FromMinutes(5));
        await engine.FireDueTimersAsync(TestContext.Current.CancellationToken);

        var late = await engine.RaiseEventAsync<RaceState>(
            engine.Management.All().Get().InstanceId,
            Event(clock.Now, "late"),
            TestContext.Current.CancellationToken);

        late.Status.Should().Be(WorkflowStatus.Waiting);
        state.Outcomes.Should().Equal(["timeout"]);
    }

    private static OrcaCore.Core.Definitions.WorkflowDefinition<RaceState> Definition(
        RaceState state,
        TimeSpan timeout)
    {
        return new WorkflowBuilder<RaceState>()
            .Init<string>(_ => state)
            .Wait("Approved", _ => Correlation, timeout)
            .Then(() => new RecordOutcomeStep())
            .End()
            .Build(DefinitionId.New(), DefinitionVersion.Initial);
    }

    private static OrcaCore.Core.Definitions.WorkflowDefinition<RaceState> ReusedCorrelationDefinition(
        RaceState state,
        TimeSpan timeout)
    {
        return new WorkflowBuilder<RaceState>()
            .Init<string>(_ => state)
            .Wait("Approved", _ => Correlation, timeout)
            .Then(() => new RecordOutcomeStep())
            .Wait("Approved", _ => Correlation)
            .Then(() => new RecordOutcomeStep())
            .End()
            .Build(DefinitionId.New(), DefinitionVersion.Initial);
    }

    private static EventEnvelope Event(DateTimeOffset occurredAt, string payload)
    {
        return new EventEnvelope
        {
            EventId = EventId.New(),
            EventName = "Approved",
            CorrelationId = Correlation,
            Payload = payload,
            OccurredAt = occurredAt
        };
    }

    private sealed class RaceState
    {
        public List<string> Outcomes { get; } = [];
    }

    private sealed class RecordOutcomeStep : IStep<RaceState>
    {
        public ValueTask<StepResult> ExecuteAsync(
            StepContext<RaceState> context,
            CancellationToken cancellationToken)
        {
            var outcome = context.ResumedEvent?.Payload is string payload
                ? $"event:{payload}"
                : "timeout";
            context.State.Outcomes.Add(outcome);
            return ValueTask.FromResult<StepResult>(new StepResult.Completed());
        }
    }
}
