using AwesomeAssertions;
using OrcaCore.Abstractions.Errors;
using OrcaCore.Abstractions.Ids;
using OrcaCore.Abstractions.Instances;
using OrcaCore.Abstractions.Steps;
using OrcaCore.Core.Building;
using OrcaCore.Engine.Ephemeral;
using Xunit;

namespace OrcaCore.Acceptance.Tests;

public sealed class StraightLineAcceptanceTests
{
    [Fact]
    [Trait("AC", "AC-001")]
    public async Task StraightLine_Completes_StateReflectsSteps()
    {
        var sink = new List<string>();
        var engine = new EphemeralWorkflowEngine();
        var definition = new WorkflowBuilder<StraightLineState>()
            .Init<string>(_ => new StraightLineState(sink))
            .Then(() => new RecordingStep("first"))
            .Then(() => new RecordingStep("second"))
            .End()
            .Build(DefinitionId.New(), DefinitionVersion.Initial);

        engine.RegisterDefinition(definition);

        var snapshot = await engine.StartAsync<string, StraightLineState>(
            definition.DefinitionId,
            "start",
            TestContext.Current.CancellationToken);

        snapshot.Status.Should().Be(WorkflowStatus.Completed);
        sink.Should().Equal(["first", "second"]);
    }

    [Fact]
    [Trait("AC", "AC-004")]
    public async Task FailingStep_FailsInstance_ErrorInspectable()
    {
        var engine = new EphemeralWorkflowEngine();
        var definition = new WorkflowBuilder<StraightLineState>()
            .Init<string>(_ => new StraightLineState([]))
            .Then(() => new FailingStep())
            .End()
            .Build(DefinitionId.New(), DefinitionVersion.Initial);

        engine.RegisterDefinition(definition);

        var snapshot = await engine.StartAsync<string, StraightLineState>(
            definition.DefinitionId,
            "start",
            TestContext.Current.CancellationToken);

        snapshot.Status.Should().Be(WorkflowStatus.Failed);
        snapshot.ErrorSummary.Should().Contain("acceptance failure");
    }

    private sealed record StraightLineState(List<string> Sink);

    private sealed class RecordingStep(string value) : IStep<StraightLineState>
    {
        public ValueTask<StepResult> ExecuteAsync(
            StepContext<StraightLineState> context,
            CancellationToken cancellationToken)
        {
            context.State.Sink.Add(value);
            return ValueTask.FromResult<StepResult>(new StepResult.Completed());
        }
    }

    private sealed class FailingStep : IStep<StraightLineState>
    {
        public ValueTask<StepResult> ExecuteAsync(
            StepContext<StraightLineState> context,
            CancellationToken cancellationToken)
        {
            return ValueTask.FromResult<StepResult>(
                new StepResult.Failed(new WorkflowDefinitionException("acceptance failure")));
        }
    }
}
