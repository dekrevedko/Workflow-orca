using System.Collections.Concurrent;
using OrcaCore.TestSupport;
using Xunit;

namespace OrcaCore.Core.Tests.TestSupport;

public sealed class RaceCoordinatorTests
{
    [Fact]
    public async Task RaceCoordinator_TwoCallers_BothReachGateBeforeEitherProceeds()
    {
        var clock = new Clock(new DateTimeOffset(2026, 7, 2, 10, 15, 0, TimeSpan.Zero));
        var coordinator = new RaceCoordinator(TimeSpan.FromSeconds(5), clock.TimeProvider);
        var observedArrivals = new ConcurrentQueue<int>();

        var first = Task.Run(async () =>
        {
            await coordinator.ArriveAndWaitAsync(TestContext.Current.CancellationToken);
            observedArrivals.Enqueue(coordinator.ArrivedCount);
        }, TestContext.Current.CancellationToken);

        await coordinator.WaitForArrivalsAsync(1, TestContext.Current.CancellationToken);
        Assert.False(first.IsCompleted);

        var second = Task.Run(async () =>
        {
            await coordinator.ArriveAndWaitAsync(TestContext.Current.CancellationToken);
            observedArrivals.Enqueue(coordinator.ArrivedCount);
        }, TestContext.Current.CancellationToken);

        await Task.WhenAll(first, second);

        Assert.Equal([2, 2], observedArrivals.Order());
    }

    [Fact]
    public async Task RaceCoordinator_OneCallerNeverArrives_FailsWithTimeoutNotHang()
    {
        var clock = new Clock(new DateTimeOffset(2026, 7, 2, 10, 15, 0, TimeSpan.Zero));
        var coordinator = new RaceCoordinator(TimeSpan.FromSeconds(5), clock.TimeProvider);

        var arrival = coordinator.ArriveAndWaitAsync(TestContext.Current.CancellationToken);
        await coordinator.WaitForArrivalsAsync(1, TestContext.Current.CancellationToken);
        clock.Advance(TimeSpan.FromSeconds(5));

        await Assert.ThrowsAsync<TimeoutException>(async () => await arrival);
    }
}
