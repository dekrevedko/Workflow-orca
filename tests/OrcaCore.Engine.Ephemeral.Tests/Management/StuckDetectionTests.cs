using AwesomeAssertions;
using OrcaCore.Abstractions.Ids;
using OrcaCore.Abstractions.Instances;
using OrcaCore.Abstractions.Steps;
using OrcaCore.Core.Building;
using OrcaCore.Engine.Ephemeral;
using OrcaCore.TestSupport;
using Xunit;

namespace OrcaCore.Engine.Ephemeral.Tests.Management;

public sealed class StuckDetectionTests
{
    [Fact]
    public async Task StepBeyondThreshold_EmitsStuckStepEventAndSetsFlag()
    {
        var clock = new Clock(new DateTimeOffset(2026, 7, 2, 12, 0, 0, TimeSpan.Zero));
        var engine = new EphemeralWorkflowEngine(
            clock.TimeProvider,
            new EphemeralWorkflowEngineOptions
            {
                StuckStepThreshold = TimeSpan.FromSeconds(5)
            });
        var definition = global::OrcaCore.Workflow.Ephemeral<TestState>(DefinitionId.New(), DefinitionVersion.Initial)
            .Init<string>(_ => new TestState())
            .Then(() => new SlowStep(clock, TimeSpan.FromSeconds(6)))
            .End()
            .Build();
        engine.RegisterDefinition(definition);

        var snapshot = await engine.StartAsync<string, TestState>(
            definition.DefinitionId,
            "start",
            TestContext.Current.CancellationToken);

        snapshot.HasStuckStep.Should().BeTrue();
        snapshot.IsStuck.Should().BeTrue();
        snapshot.StuckStepPath.Should().Be("root/1");
        snapshot.LifecycleEvents.Should().ContainSingle(lifecycleEvent =>
            lifecycleEvent.EventName == "StepStuckDetected" &&
            lifecycleEvent.StepPath == "root/1");
        engine.Management.All().Where(instance => instance.HasStuckStep).Count().Should().Be(1);
    }

    [Fact]
    public async Task RunningStep_IsMarkedStuckBeforeItReturns()
    {
        var clock = new Clock(new DateTimeOffset(2026, 7, 2, 12, 0, 0, TimeSpan.Zero));
        var step = new BlockingStep();
        var engine = new EphemeralWorkflowEngine(
            clock.TimeProvider,
            new EphemeralWorkflowEngineOptions { StuckStepThreshold = TimeSpan.FromSeconds(5) });
        var definition = global::OrcaCore.Workflow.Ephemeral<TestState>(DefinitionId.New(), DefinitionVersion.Initial)
            .Init<string>(_ => new TestState())
            .Then(() => step)
            .End()
            .Build();
        engine.RegisterDefinition(definition);
        var start = engine.StartAsync<string, TestState>(
            definition.DefinitionId,
            "start",
            TestContext.Current.CancellationToken);
        await step.Started.Task.WaitAsync(TestContext.Current.CancellationToken);

        clock.Advance(TimeSpan.FromSeconds(6));
        var running = engine.Management.All().Get();

        running.Status.Should().Be(WorkflowStatus.Running);
        running.HasStuckStep.Should().BeTrue();
        running.LifecycleEvents.Should().ContainSingle(item => item.EventName == "StepStuckDetected");

        step.Release();
        await start.WaitAsync(TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task InstanceWithoutProgressBeyondThreshold_EmitsStuckInstanceEventAndSetsFlag()
    {
        var clock = new Clock(new DateTimeOffset(2026, 7, 2, 12, 0, 0, TimeSpan.Zero));
        var engine = new EphemeralWorkflowEngine(clock.TimeProvider);
        var definition = global::OrcaCore.Workflow.Ephemeral<TestState>(DefinitionId.New(), DefinitionVersion.Initial)
            .Init<string>(_ => new TestState())
            .Wait("Ready", _ => CorrelationId.Create("item-1"))
            .End()
            .Build();
        engine.RegisterDefinition(definition);
        await engine.StartAsync<string, TestState>(
            definition.DefinitionId,
            "start",
            TestContext.Current.CancellationToken);
        clock.Advance(TimeSpan.FromSeconds(6));

        var marked = engine.Management.All().DetectStuck(TimeSpan.FromSeconds(5));

        marked.Should().ContainSingle(snapshot => snapshot.IsStuck);
        marked.Single().LifecycleEvents.Should().ContainSingle(lifecycleEvent =>
            lifecycleEvent.EventName == "InstanceStuckDetected");
        engine.Management.All().Where(instance => instance.IsStuck).Count().Should().Be(1);
    }

    private sealed class TestState;

    private sealed class SlowStep(Clock clock, TimeSpan duration) : IStep<TestState>
    {
        public ValueTask<StepResult> ExecuteAsync(
            StepContext<TestState> context,
            CancellationToken cancellationToken)
        {
            clock.Advance(duration);
            return ValueTask.FromResult<StepResult>(new StepResult.Completed());
        }
    }

    private sealed class BlockingStep : IStep<TestState>
    {
        private readonly TaskCompletionSource release = new(TaskCreationOptions.RunContinuationsAsynchronously);

        internal TaskCompletionSource Started { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        internal void Release() => release.TrySetResult();

        public async ValueTask<StepResult> ExecuteAsync(
            StepContext<TestState> context,
            CancellationToken cancellationToken)
        {
            Started.TrySetResult();
            await release.Task.WaitAsync(cancellationToken).ConfigureAwait(false);
            return new StepResult.Completed();
        }
    }
}
