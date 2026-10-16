using AwesomeAssertions;
using OrcaCore.Abstractions.Ids;
using OrcaCore.Abstractions.Providers;
using Xunit;

namespace OrcaCore.ProviderCertification;

public abstract class MessageDispatcherCertificationTests
{
    protected virtual IReadOnlyList<DispatchResult> SupportedResults { get; } =
    [
        DispatchResult.Success,
        DispatchResult.RetryableFailure,
        DispatchResult.PermanentFailure
    ];

    protected abstract IMessageDispatcher CreateDispatcher(DispatchResult result);

    [Fact]
    public async Task DispatchAsync_TransportOutcome_ReturnsNormalizedDispatchResult()
    {
        foreach (var expected in SupportedResults)
        {
            var dispatcher = CreateDispatcher(expected);

            var result = await dispatcher.DispatchAsync(
                new OutboxWrite(OutboxRecordId.New(), "workflow.completed", [1, 2, 3]),
                TestContext.Current.CancellationToken);

            result.Should().Be(expected);
        }
    }
}
