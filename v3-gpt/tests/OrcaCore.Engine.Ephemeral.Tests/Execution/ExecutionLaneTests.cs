using AwesomeAssertions;
using OrcaCore.Abstractions.Ids;
using OrcaCore.Abstractions.Instances;
using OrcaCore.Abstractions.Steps;
using OrcaCore.Core.Building;
using OrcaCore.Engine.Ephemeral.Execution;
using OrcaCore.TestSupport;
using Xunit;

namespace OrcaCore.Engine.Ephemeral.Tests.Execution;

public sealed class ExecutionLaneTests
{
    [Fact]
    public async Task RunAsync_ConcurrentCallsForSameInstance_DoNotOverlap()
    {
        var enqueued = new AsyncSignalCounter();
        var lane = new InstanceExecutionLane(_ => enqueued.Signal());
        var instanceId = InstanceId.New();
        var firstEntered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var releaseFirst = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var secondEntered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        var first = lane.RunAsync(
            instanceId,
            async cancellationToken =>
            {
                firstEntered.SetResult();
                await releaseFirst.Task.WaitAsync(cancellationToken).ConfigureAwait(false);
            },
            TestContext.Current.CancellationToken);

        await firstEntered.Task.WaitAsync(TestContext.Current.CancellationToken);

        var second = lane.RunAsync(
            instanceId,
            cancellationToken =>
            {
                secondEntered.SetResult();
                return Task.CompletedTask;
            },
            TestContext.Current.CancellationToken);

        await enqueued.WaitForCountAsync(2, TestContext.Current.CancellationToken);
        var startedBeforeRelease = secondEntered.Task.IsCompleted;
        releaseFirst.SetResult();

        await Task.WhenAll(first, second).WaitAsync(TestContext.Current.CancellationToken);

        startedBeforeRelease.Should().BeFalse();
        secondEntered.Task.IsCompletedSuccessfully.Should().BeTrue();
    }

    [Fact]
    public async Task RunAsync_OperationsForDifferentInstances_CanOverlap()
    {
        var lane = new InstanceExecutionLane();
        var coordinator = new RaceCoordinator(TimeSpan.FromSeconds(5));

        var first = lane.RunAsync(
            InstanceId.New(),
            cancellationToken => coordinator.ArriveAndWaitAsync(cancellationToken),
            TestContext.Current.CancellationToken);
        var second = lane.RunAsync(
            InstanceId.New(),
            cancellationToken => coordinator.ArriveAndWaitAsync(cancellationToken),
            TestContext.Current.CancellationToken);

        await Task.WhenAll(first, second).WaitAsync(TestContext.Current.CancellationToken);

        coordinator.ArrivedCount.Should().Be(2);
    }

    [Fact]
    public async Task StartAsync_ConcurrentStartsForDifferentInstances_AllComplete()
    {
        var engine = new EphemeralWorkflowEngine();
        var coordinator = new RaceCoordinator(TimeSpan.FromSeconds(5));
        var definition = new WorkflowBuilder<TestState>()
            .Init<string>(_ => new TestState(coordinator))
            .Then(() => new CoordinatedStep())
            .End()
            .Build(DefinitionId.New(), DefinitionVersion.Initial);

        engine.RegisterDefinition(definition);

        var first = engine.StartAsync<string, TestState>(
            definition.DefinitionId,
            "first",
            TestContext.Current.CancellationToken);
        var second = engine.StartAsync<string, TestState>(
            definition.DefinitionId,
            "second",
            TestContext.Current.CancellationToken);

        var snapshots = await Task.WhenAll(first, second).WaitAsync(TestContext.Current.CancellationToken);

        snapshots.Should().OnlyContain(snapshot => snapshot.Status == WorkflowStatus.Completed);
        coordinator.ArrivedCount.Should().Be(2);
    }

    [Fact]
    public async Task RunAsync_WhenOperationThrows_ReleasesLaneForNextOperation()
    {
        var lane = new InstanceExecutionLane();
        var instanceId = InstanceId.New();

        var throwing = async () => await lane.RunAsync(
            instanceId,
            cancellationToken => throw new InvalidOperationException("boom"),
            TestContext.Current.CancellationToken);

        await throwing.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("boom");
        var result = await lane.RunAsync(
            instanceId,
            cancellationToken => Task.FromResult(42),
            TestContext.Current.CancellationToken);

        result.Should().Be(42);
    }

    [Fact]
    public async Task RunAsync_WhenBatchDrains_EvictsIdleLane()
    {
        var lane = new InstanceExecutionLane();

        await lane.RunAsync(
            InstanceId.New(),
            _ => Task.CompletedTask,
            TestContext.Current.CancellationToken);

        lane.ActiveLaneCount.Should().Be(0);
    }

    private sealed record TestState(RaceCoordinator Coordinator);

    private sealed class CoordinatedStep : IStep<TestState>
    {
        public async ValueTask<StepResult> ExecuteAsync(
            StepContext<TestState> context,
            CancellationToken cancellationToken)
        {
            await context.State.Coordinator.ArriveAndWaitAsync(cancellationToken).ConfigureAwait(false);

            return new StepResult.Completed();
        }
    }
}
