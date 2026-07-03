using AwesomeAssertions;
using OrcaCore.Abstractions.Primitives;

namespace OrcaCore.Core.Tests.Primitives;

public sealed class OptionTests
{
    [Fact]
    public void Some_HasValue()
    {
        var option = Option<int>.Some(9);

        option.HasValue.Should().BeTrue();
        option.Value.Should().Be(9);
    }

    [Fact]
    public void None_ValueThrows()
    {
        var option = Option<int>.None();

        option.HasValue.Should().BeFalse();
        var act = () => option.Value;
        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void None_GetValueOrDefault_ReturnsFallback()
    {
        var option = Option<int>.None();

        option.GetValueOrDefault(5).Should().Be(5);
    }

    [Fact]
    public void OptionMap_OnNone_StaysNone()
    {
        var option = Option<int>.None().Map(x => x + 1);

        option.HasValue.Should().BeFalse();
    }

    [Fact]
    public void DefaultStruct_IsNone_AndValueThrows()
    {
        Option<int> option = default;

        option.HasValue.Should().BeFalse();
        var act = () => option.Value;
        act.Should().Throw<InvalidOperationException>();
    }
}
