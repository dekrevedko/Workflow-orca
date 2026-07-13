using AwesomeAssertions;
using OrcaCore.Abstractions.Ids;
using OrcaCore.Abstractions.Instances;
using OrcaCore.Abstractions.Steps;
using OrcaCore.Core.Building;
using OrcaCore.Engine.Ephemeral;
using OrcaCore.TestSupport;
using Xunit;

namespace OrcaCore.Acceptance.Tests;

public sealed class TimerAcceptanceTests
{
    private static readonly CorrelationId Correlation = new("order-123");

    [Fact]
    [Trait("AC", "AC-111")]
    public async Task EphemeralDelay_CompletesAfterDueTime()
    {
        var clock = new Clock(new DateTimeOffset(2026, 7, 2, 12, 0, 0, TimeSpan.Zero));
        var engine = new EphemeralWorkflowEngine(clock.TimeProvider);
        var sink = new List<string>();
        var definition = new WorkflowBuilder<TimerState>()
            .Init<string>(_ => new TimerState(sink))
            .Delay(TimeSpan.FromSeconds(30))
            .Then(() => new RecordingStep())
            .End()
            .Build(DefinitionId.New(), DefinitionVersion.Initial);
        engine.RegisterDefinition(definition);

        var started = await engine.StartAsync<string, TimerState>(
            definition.DefinitionId,
            "start",
            TestContext.Current.CancellationToken);
        clock.Advance(TimeSpan.FromSeconds(30));

        var fired = await engine.FireDueTimersAsync(TestContext.Current.CancellationToken);

        started.Status.Should().Be(WorkflowStatus.Waiting);
        fired.Should().ContainSingle()
            .Which.Status.Should().Be(WorkflowStatus.Completed);
        sink.Should().Equal(["continued"]);
    }

    [Fact]
    [Trait("AC", "AC-112")]
    public async Task TimerEventRace_SelectsOneWinnerDeterministically()
    {
        var clock = new Clock(new DateTimeOffset(2026, 7, 2, 12, 0, 0, TimeSpan.Zero));
        var engine = new EphemeralWorkflowEngine(clock.TimeProvider);
        var state = new RaceState();
        var definition = new WorkflowBuilder<RaceState>()
            .Init<string>(_ => state)
            .Wait("Approved", _ => Correlation, TimeSpan.FromSeconds(30))
            .Then(() => new RecordRaceOutcomeStep())
            .End()
            .Build(DefinitionId.New(), DefinitionVersion.Initial);
        engine.RegisterDefinition(definition);
        await engine.StartAsync<string, RaceState>(
            definition.DefinitionId,
            "start",
            TestContext.Current.CancellationToken);
        clock.Advance(TimeSpan.FromSeconds(30));

        var fired = await engine.FireDueTimersAsync(TestContext.Current.CancellationToken);

        fired.Should().ContainSingle()
            .Which.Status.Should().Be(WorkflowStatus.Completed);
        state.Outcomes.Should().Equal(["timeout"]);
    }

    private sealed record TimerState(List<string> Sink);

    private sealed class RaceState
    {
        public List<string> Outcomes { get; } = [];
    }

    private sealed class RecordingStep : IStep<TimerState>
    {
        public ValueTask<StepResult> ExecuteAsync(
            StepContext<TimerState> context,
            CancellationToken cancellationToken)
        {
            context.State.Sink.Add("continued");
            return ValueTask.FromResult<StepResult>(new StepResult.Completed());
        }
    }

    private sealed class RecordRaceOutcomeStep : IStep<RaceState>
    {
        public ValueTask<StepResult> ExecuteAsync(
            StepContext<RaceState> context,
            CancellationToken cancellationToken)
        {
            context.State.Outcomes.Add(context.ResumedEvent is null ? "timeout" : "event");
            return ValueTask.FromResult<StepResult>(new StepResult.Completed());
        }
    }
}
