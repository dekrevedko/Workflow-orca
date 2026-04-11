using OrcaCore.Abstractions.Primitives;

namespace OrcaCore.Tests.Primitives;

public sealed class ResultTests
{
    [Fact]
    public void Success_carries_value()
    {
        var r = Result<int>.Success(7);

        Assert.True(r.IsSuccess);
        Assert.False(r.IsFailure);
        Assert.Equal(7, r.Value);
        Assert.Null(r.Error);
    }

    [Fact]
    public void Failure_carries_exception()
    {
        var ex = new InvalidOperationException("bad");
        var r = Result<int>.Failure(ex);

        Assert.False(r.IsSuccess);
        Assert.True(r.IsFailure);
        Assert.Same(ex, r.Error);
    }

    [Fact]
    public void Map_on_success_applies_mapper()
    {
        var r = Result<int>.Success(3).Map(x => x.ToString());

        Assert.True(r.IsSuccess);
        Assert.Equal("3", r.Value);
    }

    [Fact]
    public void Map_on_failure_preserves_error()
    {
        var ex = new ArgumentException("e");
        var r = Result<int>.Failure(ex).Map(x => x * 2);

        Assert.True(r.IsFailure);
        Assert.Same(ex, r.Error);
    }

    [Fact]
    public void Bind_on_success_chains()
    {
        var r = Result<int>.Success(2).Bind(x => Result<string>.Success((x * 3).ToString()));

        Assert.True(r.IsSuccess);
        Assert.Equal("6", r.Value);
    }

    [Fact]
    public void Bind_on_failure_short_circuits()
    {
        var ex = new InvalidOperationException("x");
        var r = Result<int>.Failure(ex).Bind(_ => Result<string>.Success("nope"));

        Assert.True(r.IsFailure);
        Assert.Same(ex, r.Error);
    }

    [Fact]
    public void Match_invokes_correct_branch()
    {
        var ok = Result<int>.Success(4).Match(_ => 0, x => x * 2);
        var bad = Result<int>.Failure(new Exception("e")).Match(_ => -1, x => x);

        Assert.Equal(8, ok);
        Assert.Equal(-1, bad);
    }

    [Fact]
    public void Bind_left_identity_success()
    {
        static Result<int> Inc(int x) => Result<int>.Success(x + 1);

        var r = Result<int>.Success(1).Bind(Inc);
        Assert.Equal(Inc(1), r);
    }
}
