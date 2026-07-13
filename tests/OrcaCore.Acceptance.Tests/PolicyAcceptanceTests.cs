using AwesomeAssertions;
using OrcaCore.Abstractions.Errors;
using OrcaCore.Abstractions.Ids;
using OrcaCore.Abstractions.Instances;
using OrcaCore.Abstractions.Steps;
using OrcaCore.Core.Building;
using OrcaCore.Engine.Ephemeral;
using OrcaCore.TestSupport;
using Xunit;

namespace OrcaCore.Acceptance.Tests;

public sealed class PolicyAcceptanceTests
{
    [Fact]
    [Trait("AC", "AC-113")]
    public async Task StepTimeoutPolicy_TriggersConfiguredAction()
    {
        var clock = new Clock(new DateTimeOffset(2026, 7, 2, 12, 0, 0, TimeSpan.Zero));
        var engine = new EphemeralWorkflowEngine(clock.TimeProvider);
        var step = new NeverCompletesStep();
        var definition = new WorkflowBuilder<TimeoutState>()
            .Init<string>(_ => new TimeoutState())
            .WithTimeout(TimeSpan.FromSeconds(30))
            .Then(() => step)
            .End()
            .Build(DefinitionId.New(), DefinitionVersion.Initial);
        engine.RegisterDefinition(definition);

        var startTask = engine.StartAsync<string, TimeoutState>(
            definition.DefinitionId,
            "start",
            TestContext.Current.CancellationToken);
        await step.Started.Task.WaitAsync(TestContext.Current.CancellationToken);
        clock.Advance(TimeSpan.FromSeconds(30));

        var snapshot = await startTask.WaitAsync(TestContext.Current.CancellationToken);

        snapshot.Status.Should().Be(WorkflowStatus.Failed);
        snapshot.ErrorSummary.Should().Contain("timed out");
    }

    [Fact]
    [Trait("AC", "AC-510")]
    public async Task RetryPolicy_IsBoundedAndIdempotent()
    {
        var state = new RetryState();
        var engine = new EphemeralWorkflowEngine();
        var definition = new WorkflowBuilder<RetryState>()
            .Init<string>(_ => state)
            .WithRetry(maxAttempts: 2)
            .Then(() => new AlwaysFailsStep())
            .Then(() => new ShouldNotRunStep())
            .End()
            .Build(DefinitionId.New(), DefinitionVersion.Initial);
        engine.RegisterDefinition(definition);

        var snapshot = await engine.StartAsync<string, RetryState>(
            definition.DefinitionId,
            "start",
            TestContext.Current.CancellationToken);

        snapshot.Status.Should().Be(WorkflowStatus.Failed);
        state.Attempts.Should().Be(2);
        state.AfterFailureStepRan.Should().BeFalse();
    }

    private sealed class TimeoutState;

    private sealed class RetryState
    {
        public int Attempts { get; set; }

        public bool AfterFailureStepRan { get; set; }
    }

    private sealed class NeverCompletesStep : IStep<TimeoutState>
    {
        public TaskCompletionSource Started { get; } =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        public async ValueTask<StepResult> ExecuteAsync(
            StepContext<TimeoutState> context,
            CancellationToken cancellationToken)
        {
            var delay = Task.Delay(Timeout.InfiniteTimeSpan, context.TimeProvider, cancellationToken);
            Started.TrySetResult();
            await delay.ConfigureAwait(false);
            return new StepResult.Completed();
        }
    }

    private sealed class AlwaysFailsStep : IStep<RetryState>
    {
        public ValueTask<StepResult> ExecuteAsync(
            StepContext<RetryState> context,
            CancellationToken cancellationToken)
        {
            context.State.Attempts++;
            return ValueTask.FromResult<StepResult>(
                new StepResult.Failed(new WorkflowDefinitionException("permanent")));
        }
    }

    private sealed class ShouldNotRunStep : IStep<RetryState>
    {
        public ValueTask<StepResult> ExecuteAsync(
            StepContext<RetryState> context,
            CancellationToken cancellationToken)
        {
            context.State.AfterFailureStepRan = true;
            return ValueTask.FromResult<StepResult>(new StepResult.Completed());
        }
    }
}
