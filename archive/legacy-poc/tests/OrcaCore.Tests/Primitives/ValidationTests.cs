using OrcaCore.Abstractions.Primitives;

namespace OrcaCore.Tests.Primitives;

public sealed class ValidationTests
{
    private static ValidationError E(string code, string msg) => new(code, msg);

    [Fact]
    public void Valid_has_no_errors()
    {
        var v = Validation<int>.Valid(10);

        Assert.True(v.IsValid);
        Assert.Empty(v.Errors);
        Assert.Equal(10, v.Value);
    }

    [Fact]
    public void Invalid_collects_errors()
    {
        var v = Validation<int>.Invalid(E("A", "one"), E("B", "two"));

        Assert.False(v.IsValid);
        Assert.Equal(2, v.Errors.Count);
        Assert.Equal("A", v.Errors[0].Code);
        Assert.Equal("B", v.Errors[1].Code);
    }

    [Fact]
    public void Merge_two_invalids_concatenates_errors()
    {
        var a = Validation<int>.Invalid(E("1", "a"));
        var b = Validation<int>.Invalid(E("2", "b"));
        var m = Validation<int>.Merge(a, b);

        Assert.False(m.IsValid);
        Assert.Equal(2, m.Errors.Count);
    }

    [Fact]
    public void Merge_valid_and_invalid_keeps_errors()
    {
        var a = Validation<int>.Valid(1);
        var b = Validation<int>.Invalid(E("x", "err"));
        var m = Validation<int>.Merge(a, b);

        Assert.False(m.IsValid);
        Assert.Single(m.Errors);
    }

    [Fact]
    public void Merge_invalid_and_valid_keeps_errors()
    {
        var a = Validation<int>.Invalid(E("x", "err"));
        var b = Validation<int>.Valid(99);
        var m = Validation<int>.Merge(a, b);

        Assert.False(m.IsValid);
        Assert.Single(m.Errors);
    }

    [Fact]
    public void Merge_two_valids_with_same_value_returns_valid()
    {
        var a = Validation<int>.Valid(2);
        var b = Validation<int>.Valid(2);
        var m = Validation<int>.Merge(a, b);

        Assert.True(m.IsValid);
        Assert.Equal(2, m.Value);
    }

    [Fact]
    public void Merge_two_valids_with_different_values_returns_conflict_error()
    {
        var a = Validation<int>.Valid(1);
        var b = Validation<int>.Valid(2);
        var m = Validation<int>.Merge(a, b);

        Assert.False(m.IsValid);
        Assert.Single(m.Errors);
        Assert.Equal("VALIDATION_MERGE_CONFLICT", m.Errors[0].Code);
    }

    [Fact]
    public void Map_on_valid_applies_mapper()
    {
        var v = Validation<int>.Valid(5).Map(x => x * 2);

        Assert.True(v.IsValid);
        Assert.Equal(10, v.Value);
    }

    [Fact]
    public void Map_on_invalid_preserves_errors()
    {
        var v = Validation<int>.Invalid(E("c", "m")).Map(x => x * 2);

        Assert.False(v.IsValid);
        Assert.Single(v.Errors);
        Assert.Equal("c", v.Errors[0].Code);
    }

    [Fact]
    public void Bind_on_valid_chains()
    {
        var v = Validation<int>.Valid(3).Bind(x =>
            Validation<string>.Valid((x + 1).ToString()));

        Assert.True(v.IsValid);
        Assert.Equal("4", v.Value);
    }

    [Fact]
    public void Bind_on_invalid_short_circuits()
    {
        var v = Validation<int>.Invalid(E("e", "msg")).Bind(_ => Validation<string>.Valid("x"));

        Assert.False(v.IsValid);
        Assert.Single(v.Errors);
    }

    [Fact]
    public void Match_branches()
    {
        var ok = Validation<int>.Valid(7).Match(_ => 0, x => x);
        var bad = Validation<int>.Invalid(E("c", "m")).Match(errs => errs.Count, _ => 0);

        Assert.Equal(7, ok);
        Assert.Equal(1, bad);
    }
}
