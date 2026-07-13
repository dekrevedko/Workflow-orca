
namespace OrcaCore.Tests;

public class EventMatcherTests
{
    private static WaitRecord MakeWait(string eventName, string correlationId, WaitStatus status = WaitStatus.Active)
        => new(Guid.NewGuid().ToString("N"), eventName, correlationId, null, DateTimeOffset.UtcNow, status, WaitMode.Resident);

    [Fact]
    public void Matches_active_wait_with_same_event_and_correlation()
    {
        var waits = new[] { MakeWait("OrderApproved", "order-1") };
        var envelope = new EventEnvelope("OrderApproved", "order-1", null, "evt-1");

        var match = EventMatcher.FindMatch(waits, envelope);

        Assert.NotNull(match);
        Assert.Equal("OrderApproved", match.EventName);
    }

    [Fact]
    public void Returns_null_when_event_name_differs()
    {
        var waits = new[] { MakeWait("OrderApproved", "order-1") };
        var envelope = new EventEnvelope("OrderRejected", "order-1", null, "evt-1");

        Assert.Null(EventMatcher.FindMatch(waits, envelope));
    }

    [Fact]
    public void Returns_null_when_correlation_id_differs()
    {
        var waits = new[] { MakeWait("OrderApproved", "order-1") };
        var envelope = new EventEnvelope("OrderApproved", "order-999", null, "evt-1");

        Assert.Null(EventMatcher.FindMatch(waits, envelope));
    }

    [Fact]
    public void Returns_null_when_no_waits_exist()
    {
        var envelope = new EventEnvelope("OrderApproved", "order-1", null, "evt-1");

        Assert.Null(EventMatcher.FindMatch([], envelope));
    }

    [Fact]
    public void Skips_matched_waits()
    {
        var waits = new[]
        {
            MakeWait("OrderApproved", "order-1", WaitStatus.Matched),
            MakeWait("OrderApproved", "order-1", WaitStatus.Active)
        };
        var envelope = new EventEnvelope("OrderApproved", "order-1", null, "evt-1");

        var match = EventMatcher.FindMatch(waits, envelope);

        Assert.NotNull(match);
        Assert.Equal(WaitStatus.Active, match.Status);
    }

    [Fact]
    public void Skips_cancelled_waits()
    {
        var waits = new[] { MakeWait("OrderApproved", "order-1", WaitStatus.Cancelled) };
        var envelope = new EventEnvelope("OrderApproved", "order-1", null, "evt-1");

        Assert.Null(EventMatcher.FindMatch(waits, envelope));
    }

    [Fact]
    public void Returns_first_match_when_multiple_active_waits()
    {
        var wait1 = MakeWait("OrderApproved", "order-1");
        var wait2 = MakeWait("OrderApproved", "order-1");
        var envelope = new EventEnvelope("OrderApproved", "order-1", null, "evt-1");

        var match = EventMatcher.FindMatch([wait1, wait2], envelope);

        Assert.Equal(wait1.WaitId, match!.WaitId);
    }
}
