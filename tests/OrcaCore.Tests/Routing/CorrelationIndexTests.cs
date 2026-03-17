
namespace OrcaCore.Tests;

public class CorrelationIndexTests
{
    [Fact]
    public void ResolveExactlyOne_returns_single_registered_instance()
    {
        var index = new CorrelationIndex();
        index.Add("EventA", "corr-1", "instance-1");

        var result = index.ResolveExactlyOne("EventA", "corr-1");

        Assert.Equal("instance-1", result);
    }

    [Fact]
    public void ResolveExactlyOne_throws_when_no_match_exists()
    {
        var index = new CorrelationIndex();

        var ex = Assert.Throws<NoActiveWaitException>(() =>
            index.ResolveExactlyOne("EventA", "corr-1"));

        Assert.Contains("No active wait", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ResolveExactlyOne_throws_when_multiple_matches_exist()
    {
        var index = new CorrelationIndex();
        index.Add("EventA", "corr-1", "instance-1");
        index.Add("EventA", "corr-1", "instance-2");

        var ex = Assert.Throws<AmbiguousCorrelationException>(() =>
            index.ResolveExactlyOne("EventA", "corr-1"));

        Assert.Contains("Ambiguous", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Remove_clears_last_instance_for_key()
    {
        var index = new CorrelationIndex();
        index.Add("EventA", "corr-1", "instance-1");
        index.Remove("EventA", "corr-1", "instance-1");

        var ex = Assert.Throws<NoActiveWaitException>(() =>
            index.ResolveExactlyOne("EventA", "corr-1"));

        Assert.Contains("No active wait", ex.Message, StringComparison.OrdinalIgnoreCase);
    }
}
