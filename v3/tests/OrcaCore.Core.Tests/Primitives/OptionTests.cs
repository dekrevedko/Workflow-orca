using OrcaCore.Abstractions.Primitives;

namespace OrcaCore.Core.Tests.Primitives;

public class OptionTests
{
    [Fact]
    public void Some_HasValue()
    {
        var option = Option<int>.Some(7);

        option.HasValue.Should().BeTrue();
        option.Value.Should().Be(7);
    }

    [Fact]
    public void None_ValueThrows()
    {
        var option = Option<int>.None;

        option.HasValue.Should().BeFalse();

        var act = () => option.Value;
        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void None_GetValueOrDefault_ReturnsFallback()
    {
        var option = Option<int>.None;

        option.GetValueOrDefault(99).Should().Be(99);
    }

    [Fact]
    public void OptionMap_OnNone_StaysNone()
    {
        var option = Option<int>.None.Map(x => x * 10);

        option.HasValue.Should().BeFalse();
    }

    [Fact]
    public void Map_OnSome_Transforms()
    {
        var option = Option<int>.Some(3).Map(x => x * 10);

        option.HasValue.Should().BeTrue();
        option.Value.Should().Be(30);
    }

    [Fact]
    public void Bind_ChainsOptions_ShortCircuitsOnNone()
    {
        var some = Option<int>.Some(2).Bind(x => Option<int>.Some(x + 1));
        var none = Option<int>.None.Bind(x => Option<int>.Some(x + 1));

        some.Value.Should().Be(3);
        none.HasValue.Should().BeFalse();
    }

    [Fact]
    public void Match_InvokesExactlyOneArm()
    {
        var someCalls = 0;
        var noneCalls = 0;

        Option<int>.Some(1).Match(
            onSome: _ => { someCalls++; return true; },
            onNone: () => { noneCalls++; return false; });

        Option<int>.None.Match(
            onSome: _ => { someCalls++; return true; },
            onNone: () => { noneCalls++; return false; });

        someCalls.Should().Be(1);
        noneCalls.Should().Be(1);
    }

    [Fact]
    public void DefaultStruct_IsSafe_BehavesAsNone()
    {
        var option = default(Option<int>);

        option.HasValue.Should().BeFalse();

        var act = () => option.Value;
        act.Should().Throw<InvalidOperationException>();
    }
}
