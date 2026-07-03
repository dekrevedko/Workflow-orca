using AwesomeAssertions;
using OrcaCore.Abstractions.Ids;
using OrcaCore.Abstractions.Instances;
using OrcaCore.Abstractions.Steps;
using OrcaCore.Core.Building;
using OrcaCore.Core.Definitions;
using OrcaCore.Engine.Ephemeral;
using OrcaCore.Engine.Ephemeral.Timers;
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

    [Fact]
    public async Task FireDueTimersAsync_TimerContinuationYields_DrainsYieldContinuation()
    {
        var clock = new Clock(new DateTimeOffset(2026, 7, 2, 12, 0, 0, TimeSpan.Zero));
        var engine = new EphemeralWorkflowEngine(clock.TimeProvider);
        var state = new YieldAfterTimerState();
        var definition = new WorkflowBuilder<YieldAfterTimerState>()
            .Init<string>(_ => state)
            .Delay(TimeSpan.FromMinutes(5))
            .Then(() => new YieldOnceStep())
            .End()
            .Build(DefinitionId.New(), DefinitionVersion.Initial);
        engine.RegisterDefinition(definition);
        await engine.StartAsync<string, YieldAfterTimerState>(
            definition.DefinitionId,
            "start",
            TestContext.Current.CancellationToken);
        clock.Advance(TimeSpan.FromMinutes(5));

        var fired = await engine.FireDueTimersAsync(TestContext.Current.CancellationToken);

        fired.Should().ContainSingle()
            .Which.Status.Should().Be(WorkflowStatus.Completed);
        state.Attempts.Should().Be(2);
    }

    [Fact]
    public async Task TimerService_ConcurrentScheduleCancelAndClaim_DoesNotCorruptDueTimers()
    {
        var service = new EphemeralTimerService(TimeProvider.System);
        var instanceId = InstanceId.New();
        var snapshot = new WorkflowInstanceSnapshot
        {
            InstanceId = instanceId,
            DefinitionId = DefinitionId.New(),
            DefinitionVersion = DefinitionVersion.Initial,
            Status = WorkflowStatus.Waiting,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };

        await Parallel.ForEachAsync(
            Enumerable.Range(0, 2_000),
            TestContext.Current.CancellationToken,
            (index, _) =>
            {
                var timer = service.Schedule(instanceId, TimeSpan.Zero, _ => Task.FromResult(snapshot));
                if (index % 3 == 0)
                {
                    service.Cancel(timer);
                }

                if (index % 5 == 0)
                {
                    service.ClaimDueTimers();
                }

                return ValueTask.CompletedTask;
            });

        var due = service.ClaimDueTimers();

        due.Select(timer => timer.Token).Should().OnlyHaveUniqueItems();
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

    private sealed class YieldAfterTimerState
    {
        public int Attempts { get; set; }
    }

    private sealed class YieldOnceStep : IStep<YieldAfterTimerState>
    {
        public ValueTask<StepResult> ExecuteAsync(
            StepContext<YieldAfterTimerState> context,
            CancellationToken cancellationToken)
        {
            context.State.Attempts++;
            return ValueTask.FromResult<StepResult>(
                context.State.Attempts == 1
                    ? new StepResult.Yield()
                    : new StepResult.Completed());
        }
    }
}
