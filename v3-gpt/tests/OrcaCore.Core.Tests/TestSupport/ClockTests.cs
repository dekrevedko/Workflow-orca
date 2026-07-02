using OrcaCore.TestSupport;
using Xunit;

namespace OrcaCore.Core.Tests.TestSupport;

public sealed class ClockTests
{
    [Fact]
    public void Clock_Advance_MovesTimeExactly()
    {
        var start = new DateTimeOffset(2026, 7, 2, 10, 15, 0, TimeSpan.Zero);
        var clock = new Clock(start);

        clock.Advance(TimeSpan.FromMinutes(17));

        Assert.Equal(start.AddMinutes(17), clock.Now);
    }
}
