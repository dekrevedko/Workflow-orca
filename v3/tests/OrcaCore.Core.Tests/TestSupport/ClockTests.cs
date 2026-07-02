using OrcaCore.TestSupport.Time;

namespace OrcaCore.Core.Tests.TestSupport;

public class ClockTests
{
    [Fact]
    public void Clock_Advance_MovesTimeExactly()
    {
        var start = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);
        var clock = new Clock(start);

        clock.Advance(TimeSpan.FromMinutes(5));

        clock.Now.Should().Be(start + TimeSpan.FromMinutes(5));
    }
}
