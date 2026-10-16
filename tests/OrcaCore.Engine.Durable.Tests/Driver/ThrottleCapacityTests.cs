using AwesomeAssertions;
using OrcaCore.Engine.Durable.Driver;
using Xunit;

namespace OrcaCore.Engine.Durable.Tests.Driver;

public sealed class ThrottleCapacityTests
{
    [Fact]
    public async Task CancelledWait_DoesNotLeakCapacityOrBlockTheNextWaiter()
    {
        var coordinator = new DurableStepThrottleCoordinator(new Dictionary<Type, int> { [typeof(ThrottleCapacityTests)] = 1 });
        coordinator.TryEnter(typeof(ThrottleCapacityTests), out var owner).Should().BeTrue();
        using var cancelled = new CancellationTokenSource();
        var abandoned = coordinator.EnterAsync(typeof(ThrottleCapacityTests), cancelled.Token).AsTask();
        var waiting = coordinator.EnterAsync(typeof(ThrottleCapacityTests), TestContext.Current.CancellationToken).AsTask();
        cancelled.Cancel();
        Func<Task> observe = () => abandoned;
        await observe.Should().ThrowAsync<OperationCanceledException>();
        waiting.IsCompleted.Should().BeFalse();
        await owner.DisposeAsync();
        await using var next = await waiting.WaitAsync(TestContext.Current.CancellationToken);
        var snapshot = coordinator.Snapshot().Should().ContainSingle().Subject;
        snapshot.ActiveSlots.Should().Be(1);
        snapshot.WaitDepth.Should().Be(0);
        snapshot.Cancellations.Should().Be(1);
        coordinator.TryEnter(typeof(string), out var unrelated).Should().BeTrue();
        await unrelated.DisposeAsync();
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task PhysicalAttemptCompletion_ReturnsExactlyOneTransferredSlot(bool fault)
    {
        var coordinator = new DurableStepThrottleCoordinator(new Dictionary<Type, int> { [typeof(ThrottleCapacityTests)] = 1 });
        coordinator.TryEnter(typeof(ThrottleCapacityTests), out var owner).Should().BeTrue();
        var physical = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        owner.RetainUntil(physical.Task);
        await owner.DisposeAsync();
        coordinator.TryEnter(typeof(ThrottleCapacityTests), out _).Should().BeFalse();
        var waiting = coordinator.EnterAsync(typeof(ThrottleCapacityTests), TestContext.Current.CancellationToken).AsTask();
        if (fault) physical.SetException(new InvalidOperationException("physical failure"));
        else physical.SetResult();
        var next = await waiting.WaitAsync(TestContext.Current.CancellationToken);
        await owner.DisposeAsync();
        coordinator.TryEnter(typeof(ThrottleCapacityTests), out _).Should().BeFalse();
        await next.DisposeAsync();
        await next.DisposeAsync();
        coordinator.TryEnter(typeof(ThrottleCapacityTests), out var final).Should().BeTrue();
        coordinator.TryEnter(typeof(ThrottleCapacityTests), out _).Should().BeFalse();
        await final.DisposeAsync();
    }
}
