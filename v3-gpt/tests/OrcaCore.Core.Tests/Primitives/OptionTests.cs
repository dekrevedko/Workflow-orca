using OrcaCore.Abstractions.Primitives;
using Xunit;

namespace OrcaCore.Core.Tests.Primitives;

public sealed class OptionTests
{
    [Fact]
    public void Some_HasValue()
    {
        var option = Option<string>.Some("value");

        Assert.True(option.HasValue);
        Assert.Equal("value", option.Value);
    }

    [Fact]
    public void None_ValueThrows()
    {
        var option = Option<string>.None;

        Assert.False(option.HasValue);
        Assert.Throws<InvalidOperationException>(() => option.Value);
    }

    [Fact]
    public void None_GetValueOrDefault_ReturnsFallback()
    {
        var option = Option<string>.None;

        Assert.Equal("fallback", option.GetValueOrDefault("fallback"));
    }

    [Fact]
    public void OptionMap_OnNone_StaysNone()
    {
        var option = Option<int>.None.Map(value => value.ToString());

        Assert.False(option.HasValue);
        Assert.Throws<InvalidOperationException>(() => option.Value);
    }

    [Fact]
    public void DefaultStructs_AreSafe()
    {
        Option<string> option = default;

        Assert.False(option.HasValue);
        Assert.Throws<InvalidOperationException>(() => option.Value);
    }
}
