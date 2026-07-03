using AwesomeAssertions;
using OrcaCore.Abstractions.Errors;
using OrcaCore.Abstractions.Primitives;

namespace OrcaCore.Core.Tests.Primitives;

public sealed class ResultTests
{
    [Fact]
    public void Success_ExposesValue_AndIsSuccess()
    {
        var result = Result<int>.Success(42);

        result.IsSuccess.Should().BeTrue();
        result.IsFailure.Should().BeFalse();
        result.Value.Should().Be(42);
    }

    [Fact]
    public void Failure_ExposesError_AndValueThrows()
    {
        var error = new OrcaCoreException("failed");
        var result = Result<int>.Failure(error);

        result.IsFailure.Should().BeTrue();
        result.Error.Should().BeSameAs(error);
        var act = () => result.Value;
        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void Map_OnSuccess_Transforms()
    {
        var result = Result<int>.Success(2).Map(x => x * 3);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Be(6);
    }

    [Fact]
    public void Map_OnFailure_PropagatesError()
    {
        var error = new OrcaCoreException("failed");
        var result = Result<int>.Failure(error).Map(x => x * 3);

        result.IsFailure.Should().BeTrue();
        result.Error.Should().BeSameAs(error);
    }

    [Fact]
    public void Bind_ChainsResults_ShortCircuitsOnFailure()
    {
        var error = new OrcaCoreException("failed");
        var first = Result<int>.Failure(error);
        var second = Result<string>.Success("ok");

        var chained = first.Bind(_ => second);

        chained.IsFailure.Should().BeTrue();
        chained.Error.Should().BeSameAs(error);
    }

    [Fact]
    public void Match_OnSuccess_InvokesSuccessArm()
    {
        var result = Result<int>.Success(7);

        var value = result.Match(v => v + 1, _ => -1);

        value.Should().Be(8);
    }

    [Fact]
    public void Match_OnFailure_InvokesFailureArm()
    {
        var error = new OrcaCoreException("failed");
        var result = Result<int>.Failure(error);

        var value = result.Match(_ => 1, e => e.Message.Length);

        value.Should().Be("failed".Length);
    }

    [Fact]
    public void DefaultStruct_IsFailure_AndValueThrows()
    {
        Result<int> result = default;

        result.IsFailure.Should().BeTrue();
        var act = () => result.Value;
        act.Should().Throw<InvalidOperationException>();
    }
}
