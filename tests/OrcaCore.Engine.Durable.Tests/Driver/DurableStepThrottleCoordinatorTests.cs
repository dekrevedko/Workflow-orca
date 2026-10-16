using AwesomeAssertions;
using OrcaCore.Engine.Durable.Driver;
using Xunit;

namespace OrcaCore.Engine.Durable.Tests.Driver;

public sealed class DurableStepThrottleCoordinatorTests
{
    [Fact]
    public async Task ExactTypeThrottle_DoesNotApplyToAnotherStepType()
    {
        var coordinator = new DurableStepThrottleCoordinator(
            new Dictionary<Type, int> { [typeof(ThrottledStep)] = 1 });

        coordinator.TryEnter(typeof(ThrottledStep), out var first).Should().BeTrue();
        coordinator.TryEnter(typeof(ThrottledStep), out _).Should().BeFalse();
        coordinator.TryEnter(typeof(OtherStep), out var unrelated).Should().BeTrue();

        await unrelated.DisposeAsync();
        await first.DisposeAsync();
        coordinator.TryEnter(typeof(ThrottledStep), out var reacquired).Should().BeTrue();
        await reacquired.DisposeAsync();
    }

    [Fact]
    public async Task TimedOutPhysicalBody_RetainsSlotUntilItActuallyReturns()
    {
        var coordinator = new DurableStepThrottleCoordinator(
            new Dictionary<Type, int> { [typeof(ThrottledStep)] = 1 });
        coordinator.TryEnter(typeof(ThrottledStep), out var lease).Should().BeTrue();
        var physicalBody = new TaskCompletionSource(
            TaskCreationOptions.RunContinuationsAsynchronously);

        lease.RetainUntil(physicalBody.Task);
        await lease.DisposeAsync();

        coordinator.TryEnter(typeof(ThrottledStep), out _).Should().BeFalse();
        physicalBody.SetResult();
        await EventuallyAsync(() =>
            coordinator.TryEnter(typeof(ThrottledStep), out _));
    }

    [Fact]
    public async Task CancelledWait_IsCountedWithoutARejectionCounter()
    {
        var coordinator = new DurableStepThrottleCoordinator(
            new Dictionary<Type, int> { [typeof(ThrottledStep)] = 1 });
        coordinator.TryEnter(typeof(ThrottledStep), out var lease).Should().BeTrue();
        using var cancellation = new CancellationTokenSource();
        var waiting = coordinator.EnterAsync(typeof(ThrottledStep), cancellation.Token).AsTask();
        cancellation.Cancel();

        await waiting.Invoking(task => task).Should().ThrowAsync<OperationCanceledException>();
        coordinator.Snapshot().Should().ContainSingle().Which.Should().BeEquivalentTo(
            new
            {
                StepType = typeof(ThrottledStep),
                ConfiguredLimit = 1,
                ActiveSlots = 1,
                WaitDepth = 0,
                Cancellations = 1L
            });
        await lease.DisposeAsync();
    }

    private static async Task EventuallyAsync(Func<bool> condition)
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        while (!condition())
        {
            await Task.Delay(TimeSpan.FromMilliseconds(10), TimeProvider.System, timeout.Token);
        }
    }

    private sealed class ThrottledStep;

    private sealed class OtherStep;
}
