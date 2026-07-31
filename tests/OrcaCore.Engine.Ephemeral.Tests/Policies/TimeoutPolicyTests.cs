using AwesomeAssertions;
using OrcaCore.Abstractions.Ids;
using OrcaCore.Abstractions.Instances;
using OrcaCore.Abstractions.Steps;
using OrcaCore.Core.Building;
using OrcaCore.Engine.Ephemeral;
using OrcaCore.TestSupport;
using Xunit;

namespace OrcaCore.Engine.Ephemeral.Tests.Policies;

public sealed class TimeoutPolicyTests
{
    [Fact]
    public async Task StepTimeout_FailInstance_MarksFailedWithTimeoutDetails()
    {
        var clock = new Clock(new DateTimeOffset(2026, 7, 2, 12, 0, 0, TimeSpan.Zero));
        var engine = new EphemeralWorkflowEngine(clock.TimeProvider);
        var step = new NeverCompletesStep();
        var definition = global::OrcaCore.Workflow.Ephemeral<TimeoutState>(DefinitionId.New(), DefinitionVersion.Initial)
            .Init<string>(_ => new TimeoutState())
            .WithTimeout(TimeSpan.FromSeconds(30))
            .Then(() => step)
            .End()
            .Build();
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

    private sealed class TimeoutState;

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
}
