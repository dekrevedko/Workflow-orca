using AwesomeAssertions;
using OrcaCore.Engine.Ephemeral.Execution;
using Xunit;

namespace OrcaCore.Engine.Ephemeral.Tests.Execution;

public sealed class RuntimeTimerRecordTests
{
    [Fact]
    public void RuntimeTimerRecord_AssignsStableLogicalTimerIdentity()
    {
        var timer = new RuntimeTimerRecord(
            null,
            DateTimeOffset.UtcNow);

        timer.TimerId.Should().NotBe(default);
        timer.TimerId.Should().Be(timer.TimerId);
    }
}
