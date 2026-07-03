using AwesomeAssertions;
using OrcaCore.TestSupport;

namespace OrcaCore.Core.Tests.TestSupport;

public sealed class ClockTests
{
    [Fact]
    public void Clock_Advance_MovesTimeExactly()
    {
        var start = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);
        var clock = new Clock(start);

        clock.Now.Should().Be(start);
        clock.Advance(TimeSpan.FromMinutes(5));
        clock.Now.Should().Be(start.AddMinutes(5));
    }
}
