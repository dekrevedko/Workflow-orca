using AwesomeAssertions;
using OrcaCore.Abstractions.Ids;
using OrcaCore.Abstractions.Instances;
using OrcaCore.Abstractions.Steps;
using OrcaCore.Core.Building;
using OrcaCore.Engine.Ephemeral;
using Xunit;

namespace OrcaCore.Engine.Ephemeral.Tests.Lifecycle;

public sealed class LifecycleEventTests
{
    [Fact]
    public async Task WorkflowCompletion_PublishesCompletionLifecycleEvent()
    {
        var engine = new EphemeralWorkflowEngine();
        var definition = new WorkflowBuilder<TestState>()
            .Init<string>(_ => new TestState())
            .Then(() => new CompleteStep())
            .End("Done")
            .Build(DefinitionId.New(), DefinitionVersion.Initial);
        engine.RegisterDefinition(definition);

        var snapshot = await engine.StartAsync<string, TestState>(
            definition.DefinitionId,
            "start",
            TestContext.Current.CancellationToken);

        var lifecycleEvents = engine.Management.Instance(snapshot.InstanceId).GetLifecycleEvents();
        lifecycleEvents.Should().ContainSingle(lifecycleEvent =>
            lifecycleEvent.EventName == "InstanceCompleted" &&
            lifecycleEvent.Status == WorkflowStatus.Completed &&
            lifecycleEvent.Durable == false);
    }

    [Fact]
    public async Task StepFailure_PublishesStepFailedLifecycleEvent()
    {
        var engine = new EphemeralWorkflowEngine();
        var definition = new WorkflowBuilder<TestState>()
            .Init<string>(_ => new TestState())
            .Then(() => new FailingStep())
            .End()
            .Build(DefinitionId.New(), DefinitionVersion.Initial);
        engine.RegisterDefinition(definition);

        var snapshot = await engine.StartAsync<string, TestState>(
            definition.DefinitionId,
            "start",
            TestContext.Current.CancellationToken);

        var lifecycleEvents = engine.Management.Instance(snapshot.InstanceId).GetLifecycleEvents();
        snapshot.Status.Should().Be(WorkflowStatus.Failed);
        lifecycleEvents.Should().ContainSingle(lifecycleEvent =>
            lifecycleEvent.EventName == "StepFailed" &&
            lifecycleEvent.StepPath == "root/1" &&
            lifecycleEvent.Durable == false);
    }

    private sealed class TestState;

    private sealed class CompleteStep : IStep<TestState>
    {
        public ValueTask<StepResult> ExecuteAsync(
            StepContext<TestState> context,
            CancellationToken cancellationToken)
        {
            return ValueTask.FromResult<StepResult>(new StepResult.Completed());
        }
    }

    private sealed class FailingStep : IStep<TestState>
    {
        public ValueTask<StepResult> ExecuteAsync(
            StepContext<TestState> context,
            CancellationToken cancellationToken)
        {
            throw new InvalidOperationException("boom");
        }
    }
}
