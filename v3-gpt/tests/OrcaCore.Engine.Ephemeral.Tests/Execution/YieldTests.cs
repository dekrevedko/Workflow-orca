using AwesomeAssertions;
using OrcaCore.Abstractions.Ids;
using OrcaCore.Abstractions.Instances;
using OrcaCore.Abstractions.Steps;
using OrcaCore.Core.Building;
using OrcaCore.Engine.Ephemeral;
using Xunit;

namespace OrcaCore.Engine.Ephemeral.Tests.Execution;

public sealed class YieldTests
{
    [Fact]
    public async Task Run_YieldingStep_RemainsRunningBetweenContinuations()
    {
        var gate = new YieldGate(blockBeforeCompletion: true);
        var engine = new EphemeralWorkflowEngine();
        var definition = YieldingDefinition(new TestState { RemainingYields = 1 }, gate);
        engine.RegisterDefinition(definition);

        var start = engine.StartAsync<string, TestState>(
            definition.DefinitionId,
            "start",
            TestContext.Current.CancellationToken);
        await gate.Yielded.Task.WaitAsync(TestContext.Current.CancellationToken);
        await gate.BlockedBeforeCompletion.Task.WaitAsync(TestContext.Current.CancellationToken);
        var running = await WaitForSingleSnapshotAsync(engine);
        gate.ReleaseCompletion();
        var completed = await start.WaitAsync(TestContext.Current.CancellationToken);

        running.Status.Should().Be(WorkflowStatus.Running);
        completed.Status.Should().Be(WorkflowStatus.Completed);
    }

    [Fact]
    public async Task Run_YieldingStep_ReentersSameStepUntilCompleted()
    {
        var state = new TestState { RemainingYields = 3 };
        var engine = new EphemeralWorkflowEngine();
        var definition = YieldingDefinition(state, new YieldGate(blockBeforeCompletion: false));
        engine.RegisterDefinition(definition);

        await engine.StartAsync<string, TestState>(
            definition.DefinitionId,
            "start",
            TestContext.Current.CancellationToken);

        state.Attempts.Should().Be(4);
        state.CompletedEffects.Should().Be(1);
    }

    [Fact]
    public async Task Run_YieldingStep_CommitsProgressForEachYield()
    {
        var gate = new YieldGate(blockBeforeCompletion: true);
        var engine = new EphemeralWorkflowEngine();
        var definition = YieldingDefinition(new TestState { RemainingYields = 1 }, gate);
        engine.RegisterDefinition(definition);

        var start = engine.StartAsync<string, TestState>(
            definition.DefinitionId,
            "start",
            TestContext.Current.CancellationToken);
        await gate.Yielded.Task.WaitAsync(TestContext.Current.CancellationToken);
        await gate.BlockedBeforeCompletion.Task.WaitAsync(TestContext.Current.CancellationToken);
        var snapshot = await WaitForSingleSnapshotAsync(engine);
        var state = engine.Management.Instance(snapshot.InstanceId).GetState<TestState>();
        gate.ReleaseCompletion();
        await start.WaitAsync(TestContext.Current.CancellationToken);

        state.Progress.Should().Be(1);
        state.CompletedEffects.Should().Be(0);
    }

    [Fact]
    public async Task Run_YieldingStep_DoesNotDuplicateCompletedEffects()
    {
        var state = new TestState { RemainingYields = 2 };
        var engine = new EphemeralWorkflowEngine();
        var definition = YieldingDefinition(state, new YieldGate(blockBeforeCompletion: false));
        engine.RegisterDefinition(definition);

        await engine.StartAsync<string, TestState>(
            definition.DefinitionId,
            "start",
            TestContext.Current.CancellationToken);

        state.Progress.Should().Be(2);
        state.CompletedEffects.Should().Be(1);
    }

    [Fact]
    public async Task Run_YieldingStep_ReleasesLaneBetweenContinuations()
    {
        var gate = new YieldGate(blockBeforeCompletion: true);
        var engine = new EphemeralWorkflowEngine();
        var definition = YieldingDefinition(new TestState { RemainingYields = 1 }, gate);
        engine.RegisterDefinition(definition);

        var start = engine.StartAsync<string, TestState>(
            definition.DefinitionId,
            "start",
            TestContext.Current.CancellationToken);
        await gate.Yielded.Task.WaitAsync(TestContext.Current.CancellationToken);
        var running = await WaitForSingleSnapshotAsync(engine);

        running.Status.Should().Be(WorkflowStatus.Running);
        gate.ReleaseCompletion();
        await start.WaitAsync(TestContext.Current.CancellationToken);
    }

    private static OrcaCore.Core.Definitions.WorkflowDefinition<TestState> YieldingDefinition(
        TestState state,
        YieldGate gate)
    {
        return new WorkflowBuilder<TestState>()
            .Init<string>(_ => state)
            .Then(() => new YieldingStep(gate))
            .End()
            .Build(DefinitionId.New(), DefinitionVersion.Initial);
    }

    private static async Task<WorkflowInstanceSnapshot> WaitForSingleSnapshotAsync(EphemeralWorkflowEngine engine)
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(2));
        while (!timeout.IsCancellationRequested)
        {
            var snapshots = engine.Management.All().List();
            if (snapshots.Count == 1)
            {
                return snapshots[0];
            }

            await Task.Delay(10, timeout.Token).ConfigureAwait(false);
        }

        throw new TimeoutException("Timed out waiting for yielded instance snapshot.");
    }

    private sealed class TestState
    {
        public int Attempts { get; set; }

        public int CompletedEffects { get; set; }

        public int Progress { get; set; }

        public int RemainingYields { get; set; }
    }

    private sealed class YieldGate(bool blockBeforeCompletion)
    {
        private readonly TaskCompletionSource releaseCompletion = new(TaskCreationOptions.RunContinuationsAsynchronously);

        internal TaskCompletionSource BlockedBeforeCompletion { get; } =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        internal TaskCompletionSource Yielded { get; } =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        internal bool BlockBeforeCompletion { get; } = blockBeforeCompletion;

        internal Task WaitForReleaseAsync()
        {
            return releaseCompletion.Task;
        }

        internal void ReleaseCompletion()
        {
            releaseCompletion.TrySetResult();
        }
    }

    private sealed class YieldingStep(YieldGate gate) : IStep<TestState>
    {
        public async ValueTask<StepResult> ExecuteAsync(
            StepContext<TestState> context,
            CancellationToken cancellationToken)
        {
            context.State.Attempts++;
            if (context.State.RemainingYields > 0)
            {
                context.State.RemainingYields--;
                context.State.Progress++;
                gate.Yielded.TrySetResult();
                return new StepResult.Yield();
            }

            if (gate.BlockBeforeCompletion)
            {
                gate.BlockedBeforeCompletion.TrySetResult();
                await gate.WaitForReleaseAsync().WaitAsync(cancellationToken).ConfigureAwait(false);
            }

            context.State.CompletedEffects++;
            return new StepResult.Completed();
        }
    }
}
