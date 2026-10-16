using AwesomeAssertions;
using OrcaCore.TestSupport;
using Xunit;

namespace OrcaCore.Core.Tests.TestSupport;

public sealed class AsyncSignalCounterTests
{
    [Fact]
    public async Task WaitForCountAsync_SignalsReachExpectedCount_Completes()
    {
        var counter = new AsyncSignalCounter();
        var observed = counter.WaitForCountAsync(2, TestContext.Current.CancellationToken);

        counter.Signal();
        observed.IsCompleted.Should().BeFalse();
        counter.Signal();

        await observed.WaitAsync(TestContext.Current.CancellationToken);
        counter.Count.Should().Be(2);
    }

    [Fact]
    public async Task WaitForCountAsync_CountAlreadyReached_CompletesImmediately()
    {
        var counter = new AsyncSignalCounter();
        counter.Signal();

        await counter.WaitForCountAsync(1, TestContext.Current.CancellationToken);

        counter.Count.Should().Be(1);
    }
}
