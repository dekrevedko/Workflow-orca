using AwesomeAssertions;
using OrcaCore.Abstractions.Ids;
using OrcaCore.Abstractions.Instances;
using OrcaCore.Abstractions.Steps;
using OrcaCore.Core.Building;
using OrcaCore.Core.Definitions;
using OrcaCore.Engine.Ephemeral;
using OrcaCore.TestSupport;
using Xunit;

namespace OrcaCore.Engine.Ephemeral.Tests.Timers;

public sealed class EphemeralTimerTests
{
    [Fact]
    public async Task Delay_BeforeDueTime_RemainsWaiting()
    {
        var clock = new Clock(new DateTimeOffset(2026, 7, 2, 12, 0, 0, TimeSpan.Zero));
        var engine = new EphemeralWorkflowEngine(clock.TimeProvider);
        var sink = new List<string>();
        var definition = CreateDelayedDefinition(sink, TimeSpan.FromMinutes(5));
        engine.RegisterDefinition(definition);

        var started = await engine.StartAsync<string, TimerState>(
            definition.DefinitionId,
            "start",
            TestContext.Current.CancellationToken);
        clock.Advance(TimeSpan.FromMinutes(4));

        var fired = await engine.FireDueTimersAsync(TestContext.Current.CancellationToken);

        started.Status.Should().Be(WorkflowStatus.Waiting);
        fired.Should().BeEmpty();
        sink.Should().BeEmpty();
    }

    [Fact]
    public async Task Delay_AfterDueTime_ContinuesExactlyOnce()
    {
        var clock = new Clock(new DateTimeOffset(2026, 7, 2, 12, 0, 0, TimeSpan.Zero));
        var engine = new EphemeralWorkflowEngine(clock.TimeProvider);
        var sink = new List<string>();
        var definition = CreateDelayedDefinition(sink, TimeSpan.FromMinutes(5));
        engine.RegisterDefinition(definition);

        await engine.StartAsync<string, TimerState>(
            definition.DefinitionId,
            "start",
            TestContext.Current.CancellationToken);
        clock.Advance(TimeSpan.FromMinutes(5));

        var firstFire = await engine.FireDueTimersAsync(TestContext.Current.CancellationToken);
        var secondFire = await engine.FireDueTimersAsync(TestContext.Current.CancellationToken);

        firstFire.Should().ContainSingle()
            .Which.Status.Should().Be(WorkflowStatus.Completed);
        secondFire.Should().BeEmpty();
        sink.Should().Equal(["after-delay"]);
    }

    private static WorkflowDefinition<TimerState> CreateDelayedDefinition(List<string> sink, TimeSpan delay)
    {
        return new WorkflowBuilder<TimerState>()
            .Init<string>(_ => new TimerState(sink))
            .Delay(delay)
            .Then(() => new RecordingStep("after-delay"))
            .End()
            .Build(DefinitionId.New(), DefinitionVersion.Initial);
    }

    private sealed record TimerState(List<string> Sink);

    private sealed class RecordingStep(string value) : IStep<TimerState>
    {
        public ValueTask<StepResult> ExecuteAsync(
            StepContext<TimerState> context,
            CancellationToken cancellationToken)
        {
            context.State.Sink.Add(value);
            return ValueTask.FromResult<StepResult>(new StepResult.Completed());
        }
    }
}
