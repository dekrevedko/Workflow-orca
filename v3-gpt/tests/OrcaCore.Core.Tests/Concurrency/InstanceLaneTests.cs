using AwesomeAssertions;
using OrcaCore.Abstractions.Ids;
using OrcaCore.Core.Concurrency;
using OrcaCore.TestSupport;
using Xunit;

namespace OrcaCore.Core.Tests.Concurrency;

public sealed class InstanceLaneTests
{
    [Fact]
    public async Task RunAsync_ConcurrentCallsForSameInstance_DoNotOverlap()
    {
        var enqueued = new AsyncSignalCounter();
        var lane = new InstanceLane(_ => enqueued.Signal());
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
            _ =>
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
        var lane = new InstanceLane();
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
    public async Task RunAsync_WhenOperationThrows_ReleasesLaneForNextOperation()
    {
        var lane = new InstanceLane();
        var instanceId = InstanceId.New();

        var throwing = async () => await lane.RunAsync(
            instanceId,
            _ => throw new InvalidOperationException("boom"),
            TestContext.Current.CancellationToken);

        await throwing.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("boom");
        var result = await lane.RunAsync(
            instanceId,
            _ => Task.FromResult(42),
            TestContext.Current.CancellationToken);

        result.Should().Be(42);
    }

    [Fact]
    public async Task RunAsync_WhenCancellationRequestedBeforeEnqueue_DoesNotCreateLane()
    {
        var lane = new InstanceLane();
        using var cancellation = new CancellationTokenSource();
        await cancellation.CancelAsync();

        var canceled = async () => await lane.RunAsync(
            InstanceId.New(),
            _ => throw new InvalidOperationException("should not run"),
            cancellation.Token);

        await canceled.Should().ThrowAsync<OperationCanceledException>();
        lane.ActiveLaneCount.Should().Be(0);
    }

    [Fact]
    public async Task RunAsync_WhenBatchDrains_EvictsIdleLane()
    {
        var lane = new InstanceLane();

        await lane.RunAsync(
            InstanceId.New(),
            _ => Task.CompletedTask,
            TestContext.Current.CancellationToken);

        lane.ActiveLaneCount.Should().Be(0);
    }
}
