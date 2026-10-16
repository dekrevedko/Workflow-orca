using AwesomeAssertions;
using OrcaCore.Abstractions.Ids;
using OrcaCore.Abstractions.Instances;
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
        var definition = global::OrcaCore.Workflow.Ephemeral<TestState>(DefinitionId.New(), DefinitionVersion.Initial)
            .Init<string>(_ => new TestState())
            .Then(() => new CompleteStep())
            .End("Done")
            .Build();
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
