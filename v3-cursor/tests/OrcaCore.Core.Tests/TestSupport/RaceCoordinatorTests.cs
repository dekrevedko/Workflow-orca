using AwesomeAssertions;
using OrcaCore.TestSupport;

namespace OrcaCore.Core.Tests.TestSupport;

public sealed class RaceCoordinatorTests
{
    [Fact]
    public async Task RaceCoordinator_TwoCallers_BothReachGateBeforeEitherProceeds()
    {
        var coordinator = new RaceCoordinator(TimeSpan.FromSeconds(2));
        var firstPastGate = false;
        var secondPastGate = false;

        var first = Task.Run(async () =>
        {
            await coordinator.EnterGateAsync(TestContext.Current.CancellationToken);
            firstPastGate = true;
        }, TestContext.Current.CancellationToken);

        var second = Task.Run(async () =>
        {
            await coordinator.EnterGateAsync(TestContext.Current.CancellationToken);
            secondPastGate = true;
        }, TestContext.Current.CancellationToken);

        await Task.Delay(50, TestContext.Current.CancellationToken);

        firstPastGate.Should().BeFalse();
        secondPastGate.Should().BeFalse();

        coordinator.ReleaseAll();
        await Task.WhenAll(first, second);

        firstPastGate.Should().BeTrue();
        secondPastGate.Should().BeTrue();
    }

    [Fact]
    public async Task RaceCoordinator_OneCallerNeverArrives_FailsWithTimeoutNotHang()
    {
        var coordinator = new RaceCoordinator(TimeSpan.FromMilliseconds(200));

        var act = async () => await coordinator.EnterGateAsync(TestContext.Current.CancellationToken);

        await act.Should().ThrowAsync<OperationCanceledException>();
    }
}
