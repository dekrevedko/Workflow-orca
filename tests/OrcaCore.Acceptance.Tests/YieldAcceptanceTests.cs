using AwesomeAssertions;
using OrcaCore.Abstractions.Ids;
using OrcaCore.Abstractions.Steps;
using OrcaCore.Core.Building;
using OrcaCore.Engine.Ephemeral;
using Xunit;

namespace OrcaCore.Acceptance.Tests;

public sealed class YieldAcceptanceTests
{
    [Fact]
    [Trait("AC", "AC-013")]
    public async Task Yield_CommitsProgressAndCompletesExactlyOnce()
    {
        var state = new TestState { RemainingYields = 3 };
        var engine = new EphemeralWorkflowEngine();
        var definition = global::OrcaCore.Workflow.Ephemeral<TestState>(DefinitionId.New(), DefinitionVersion.Initial)
            .Init<string>(_ => state)
            .Then(() => new YieldingStep())
            .End()
            .Build();
        engine.RegisterDefinition(definition);

        await engine.StartAsync<string, TestState>(
            definition.DefinitionId,
            "start",
            TestContext.Current.CancellationToken);

        state.Progress.Should().Be(3);
        state.CompletedEffects.Should().Be(1);
    }

    private sealed class TestState
    {
        public int CompletedEffects { get; set; }

        public int Progress { get; set; }

        public int RemainingYields { get; set; }
    }

    private sealed class YieldingStep : IStep<TestState>
    {
        public ValueTask<StepResult> ExecuteAsync(
            StepContext<TestState> context,
            CancellationToken cancellationToken)
        {
            if (context.State.RemainingYields > 0)
            {
                context.State.RemainingYields--;
                context.State.Progress++;
                return ValueTask.FromResult<StepResult>(global::OrcaCore.TestSupport.LegacyStepResults.Yield());
            }

            context.State.CompletedEffects++;
            return ValueTask.FromResult<StepResult>(new StepResult.Completed());
        }
    }
}
