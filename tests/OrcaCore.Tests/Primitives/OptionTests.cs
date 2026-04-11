using OrcaCore.Abstractions.Primitives;

namespace OrcaCore.Tests.Primitives;

public sealed class OptionTests
{
    [Fact]
    public void Some_has_value_and_round_trips_Value()
    {
        var opt = Option<int>.Some(42);

        Assert.True(opt.HasValue);
        Assert.Equal(42, opt.Value);
    }

    [Fact]
    public void None_has_no_value()
    {
        var opt = Option<int>.None;

        Assert.False(opt.HasValue);
        Assert.Throws<InvalidOperationException>(() => _ = opt.Value);
    }

    [Fact]
    public void Map_on_Some_applies_mapper()
    {
        var opt = Option<int>.Some(3).Map(x => x * 2);

        Assert.True(opt.HasValue);
        Assert.Equal(6, opt.Value);
    }

    [Fact]
    public void Map_on_None_stays_None()
    {
        var opt = Option<int>.None.Map(x => x * 2);

        Assert.False(opt.HasValue);
    }

    [Fact]
    public void Bind_on_Some_chains()
    {
        var opt = Option<int>.Some(2).Bind(x => Option<string>.Some((x * 2).ToString()));

        Assert.True(opt.HasValue);
        Assert.Equal("4", opt.Value);
    }

    [Fact]
    public void Bind_on_None_short_circuits()
    {
        var opt = Option<int>.None.Bind(_ => Option<string>.Some("ignored"));

        Assert.False(opt.HasValue);
    }

    [Fact]
    public void Match_invokes_correct_branch()
    {
        var a = Option<int>.Some(1).Match(() => 0, x => x + 1);
        var b = Option<int>.None.Match(() => 99, x => x);

        Assert.Equal(2, a);
        Assert.Equal(99, b);
    }

    [Fact]
    public void GetValueOrDefault_returns_default_for_None()
    {
        Assert.Equal(0, Option<int>.None.GetValueOrDefault());
        Assert.Equal(-1, Option<int>.None.GetValueOrDefault(-1));
    }

    [Fact]
    public void Equality_Some_and_None()
    {
        Assert.Equal(Option<string>.Some("x"), Option<string>.Some("x"));
        Assert.NotEqual(Option<string>.Some("x"), Option<string>.Some("y"));
        Assert.Equal(Option<string>.None, Option<string>.None);
        Assert.NotEqual(Option<string>.Some("a"), Option<string>.None);
    }

    [Fact]
    public void Bind_left_identity_for_success_path()
    {
        static Option<int> Inc(int x) => Option<int>.Some(x + 1);

        var opt = Option<int>.Some(5).Bind(Inc);
        var direct = Inc(5);

        Assert.Equal(direct, opt);
    }
}
