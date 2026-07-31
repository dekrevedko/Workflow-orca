using AwesomeAssertions;
using OrcaCore.Abstractions.Ids;
using OrcaCore.Abstractions.Instances;
using OrcaCore.Abstractions.Steps;
using OrcaCore.Core.Definitions;
using OrcaCore.Engine.Ephemeral;
using OrcaCore.TestSupport;
using Xunit;

namespace OrcaCore.Engine.Ephemeral.Tests.Execution;

public sealed class DeadlineExecutionTests
{
    [Fact]
    public async Task CompleteWithin_ExpiresWhileDelayed_TerminalizesAsTimedOut()
    {
        var clock = new Clock(new DateTimeOffset(2026, 7, 29, 12, 0, 0, TimeSpan.Zero));
        var definition = global::OrcaCore.Workflow.Ephemeral<State>(
                DefinitionId.New(),
                DefinitionVersion.Initial)
            .Init<string>(_ => new State())
            .CompleteWithin(TimeSpan.FromMinutes(5))
            .Delay(TimeSpan.FromMinutes(10))
            .End()
            .Build();
        var engine = new EphemeralWorkflowEngine(clock.TimeProvider);
        engine.RegisterDefinition((WorkflowDefinition<State>)definition.RuntimeDefinition);

        var waiting = await engine.StartAsync<string, State>(
            definition.DefinitionId,
            "start",
            TestContext.Current.CancellationToken);
        clock.Advance(TimeSpan.FromMinutes(5));

        var fired = await engine.FireDueTimersAsync(TestContext.Current.CancellationToken);

        waiting.Status.Should().Be(WorkflowStatus.Waiting);
        fired.Should().ContainSingle()
            .Which.Status.Should().Be(WorkflowStatus.TimedOut);
        fired[0].ErrorSummary.Should().Contain("WF-DEADLINE-EXCEEDED");
    }

    [Fact]
    public async Task StructuralWaitTimeout_Wins_CancelsEventObligationAndFailsWithTypedCode()
    {
        var clock = new Clock(new DateTimeOffset(2026, 7, 29, 12, 0, 0, TimeSpan.Zero));
        var definition = global::OrcaCore.Workflow.Ephemeral<State>(
                DefinitionId.New(),
                DefinitionVersion.Initial)
            .Init<string>(_ => new State())
            .Wait(
                EventName.Create("approval"),
                _ => CorrelationId.Create("order-1"),
                TimeSpan.FromMinutes(1))
            .End()
            .Build();
        var engine = new EphemeralWorkflowEngine(clock.TimeProvider);
        engine.RegisterDefinition((WorkflowDefinition<State>)definition.RuntimeDefinition);

        var waiting = await engine.StartAsync<string, State>(
            definition.DefinitionId,
            "start",
            TestContext.Current.CancellationToken);
        clock.Advance(TimeSpan.FromMinutes(1));

        var fired = await engine.FireDueTimersAsync(TestContext.Current.CancellationToken);

        waiting.Status.Should().Be(WorkflowStatus.Waiting);
        fired.Should().ContainSingle()
            .Which.Status.Should().Be(WorkflowStatus.Failed);
        fired[0].ErrorSummary.Should().Contain("WF-WAIT-TIMEOUT");
        fired[0].ActiveWaits.Should().BeEmpty();
    }

    [Fact]
    public async Task TimedOutTokenIgnoringAttempt_IsFencedWhileRetryCommitsDetachedWinner()
    {
        var clock = new Clock(new DateTimeOffset(2026, 7, 29, 14, 0, 0, TimeSpan.Zero));
        var gate = new LateAttemptGate();
        var definition = global::OrcaCore.Workflow.Ephemeral<MutableState>(
                DefinitionId.New(),
                DefinitionVersion.Initial)
            .Init<string>(_ => new MutableState())
            .WithRetry(2)
            .WithTimeout(TimeSpan.FromMinutes(1))
            .Then(() => new TokenIgnoringRetryStep(gate))
            .End()
            .Build();
        var engine = new EphemeralWorkflowEngine(clock.TimeProvider);
        engine.RegisterDefinition(definition);

        var run = engine.StartAsync<string, MutableState>(
            definition.DefinitionId,
            "start",
            TestContext.Current.CancellationToken);
        await gate.FirstStarted.Task.WaitAsync(
            TimeSpan.FromSeconds(5),
            TestContext.Current.CancellationToken);
        clock.Advance(TimeSpan.FromMinutes(1));

        await gate.SecondStarted.Task.WaitAsync(
            TimeSpan.FromSeconds(5),
            TestContext.Current.CancellationToken);
        var completed = await run;
        gate.ReleaseFirst.TrySetResult();
        await gate.FirstFinished.Task.WaitAsync(
            TimeSpan.FromSeconds(5),
            TestContext.Current.CancellationToken);
        var state = engine.Management.Instance(completed.InstanceId).GetState<MutableState>();

        gate.OperationIds.Should().HaveCount(2);
        gate.OperationIds.Distinct().Should().ContainSingle();
        gate.AttemptNumbers.Should().Equal(1, 2);
        state.Log.Should().Equal("winner");
    }

    private sealed class State;

    private sealed class MutableState
    {
        public List<string> Log { get; set; } = [];
    }

    private sealed class LateAttemptGate
    {
        internal TaskCompletionSource FirstStarted { get; } =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        internal TaskCompletionSource SecondStarted { get; } =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        internal TaskCompletionSource ReleaseFirst { get; } =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        internal TaskCompletionSource FirstFinished { get; } =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        internal List<string> OperationIds { get; } = [];

        internal List<int> AttemptNumbers { get; } = [];
    }

    private sealed class TokenIgnoringRetryStep(LateAttemptGate gate) : IStep<MutableState>
    {
        public async ValueTask<StepResult> ExecuteAsync(
            StepContext<MutableState> context,
            CancellationToken cancellationToken)
        {
            lock (gate)
            {
                gate.OperationIds.Add(context.Execution.OperationId.ToString());
                gate.AttemptNumbers.Add(context.Execution.AttemptNumber);
            }

            if (context.Execution.AttemptNumber == 1)
            {
                gate.FirstStarted.TrySetResult();
                await gate.ReleaseFirst.Task;
                context.State.Log.Add("late");
                gate.FirstFinished.TrySetResult();
                return new StepResult.Completed();
            }

            gate.SecondStarted.TrySetResult();
            context.State.Log.Add("winner");
            return new StepResult.Completed();
        }
    }
}
