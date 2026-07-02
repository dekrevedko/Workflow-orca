using OrcaCore.TestSupport.Concurrency;

namespace OrcaCore.Core.Tests.TestSupport;

public class RaceCoordinatorTests
{
    [Fact]
    public async Task RaceCoordinator_TwoCallers_BothReachGateBeforeEitherProceeds()
    {
        var coordinator = new RaceCoordinator();
        var cancellationToken = TestContext.Current.CancellationToken;

        var first = coordinator.ArriveAsync(cancellationToken);
        first.IsCompleted.Should().BeFalse("the first caller must wait for the second to arrive");

        var second = coordinator.ArriveAsync(cancellationToken);

        await Task.WhenAll(first, second);

        first.IsCompleted.Should().BeTrue();
        second.IsCompleted.Should().BeTrue();
    }

    [Fact]
    public async Task RaceCoordinator_OneCallerNeverArrives_FailsWithTimeoutNotHang()
    {
        var coordinator = new RaceCoordinator(timeout: TimeSpan.FromMilliseconds(50));
        var cancellationToken = TestContext.Current.CancellationToken;

        var act = () => coordinator.ArriveAsync(cancellationToken);

        await act.Should().ThrowAsync<TimeoutException>();
    }
}
