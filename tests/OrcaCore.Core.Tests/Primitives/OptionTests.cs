using AwesomeAssertions;
using OrcaCore.Abstractions.Primitives;
using Xunit;

namespace OrcaCore.Core.Tests.Primitives;

public sealed class OptionTests
{
    [Fact]
    public void Some_HasValue()
    {
        var option = Option<string>.Some("value");

        option.HasValue.Should().BeTrue();
        option.Value.Should().Be("value");
    }

    [Fact]
    public void None_ValueThrows()
    {
        var option = Option<string>.None;

        option.HasValue.Should().BeFalse();
        var act = () => option.Value;
        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void None_GetValueOrDefault_ReturnsFallback()
    {
        var option = Option<string>.None;

        option.GetValueOrDefault("fallback").Should().Be("fallback");
    }

    [Fact]
    public void OptionMap_OnNone_StaysNone()
    {
        var option = Option<int>.None.Map(value => value.ToString());

        option.HasValue.Should().BeFalse();
        var act = () => option.Value;
        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void DefaultStructs_AreSafe()
    {
        Option<string> option = default;

        option.HasValue.Should().BeFalse();
        var act = () => option.Value;
        act.Should().Throw<InvalidOperationException>();
    }
}
