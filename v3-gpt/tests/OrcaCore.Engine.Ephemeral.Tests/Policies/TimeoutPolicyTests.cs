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
        var definition = new WorkflowBuilder<TimeoutState>()
            .Init<string>(_ => new TimeoutState())
            .WithTimeout(TimeSpan.FromSeconds(30))
            .Then<NeverCompletesStep>()
            .End()
            .Build(DefinitionId.New(), DefinitionVersion.Initial);
        engine.RegisterDefinition(definition);

        var startTask = engine.StartAsync<string, TimeoutState>(
            definition.DefinitionId,
            "start",
            TestContext.Current.CancellationToken);
        clock.Advance(TimeSpan.FromSeconds(30));

        var snapshot = await startTask.WaitAsync(TestContext.Current.CancellationToken);

        snapshot.Status.Should().Be(WorkflowStatus.Failed);
        snapshot.ErrorSummary.Should().Contain("timed out");
    }

    private sealed class TimeoutState;

    private sealed class NeverCompletesStep : IStep<TimeoutState>
    {
        public async ValueTask<StepResult> ExecuteAsync(
            StepContext<TimeoutState> context,
            CancellationToken cancellationToken)
        {
            await Task.Delay(Timeout.InfiniteTimeSpan, context.TimeProvider, cancellationToken)
                .ConfigureAwait(false);
            return new StepResult.Completed();
        }
    }
}
