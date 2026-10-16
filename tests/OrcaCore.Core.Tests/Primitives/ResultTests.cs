using AwesomeAssertions;
using OrcaCore.Abstractions.Errors;
using OrcaCore.Abstractions.Primitives;
using Xunit;

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
        var error = new TestOrcaCoreException("failed");
        var result = Result<int>.Failure(error);

        result.IsFailure.Should().BeTrue();
        result.IsSuccess.Should().BeFalse();
        result.Error.Should().BeSameAs(error);
        var act = () => result.Value;
        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void Map_OnSuccess_Transforms()
    {
        var result = Result<int>.Success(4).Map(value => value * 2);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Be(8);
    }

    [Fact]
    public void Map_OnFailure_PropagatesError()
    {
        var error = new TestOrcaCoreException("failed");

        var result = Result<int>.Failure(error).Map(value => value * 2);

        result.IsFailure.Should().BeTrue();
        result.Error.Should().BeSameAs(error);
    }

    [Fact]
    public void Bind_ChainsResults_ShortCircuitsOnFailure()
    {
        var error = new TestOrcaCoreException("failed");

        var success = Result<int>.Success(2)
            .Bind(value => Result<string>.Success(value.ToString()));
        var failure = Result<int>.Failure(error)
            .Bind(value => Result<string>.Success(value.ToString()));

        success.Value.Should().Be("2");
        failure.IsFailure.Should().BeTrue();
        failure.Error.Should().BeSameAs(error);
    }

    [Fact]
    public void Match_InvokesExactlyOneArm()
    {
        var successCalls = 0;
        var failureCalls = 0;

        var success = Result<int>.Success(3).Match(
            value =>
            {
                successCalls++;
                return value + 1;
            },
            _ =>
            {
                failureCalls++;
                return -1;
            });
        var failure = Result<int>.Failure(new TestOrcaCoreException("failed")).Match(
            value =>
            {
                successCalls++;
                return value + 1;
            },
            _ =>
            {
                failureCalls++;
                return -1;
            });

        success.Should().Be(4);
        failure.Should().Be(-1);
        successCalls.Should().Be(1);
        failureCalls.Should().Be(1);
    }

    [Fact]
    public void DefaultStructs_AreSafe()
    {
        Result<string> result = default;

        result.IsFailure.Should().BeTrue();
        result.IsSuccess.Should().BeFalse();
        result.Error.Should().NotBeNull();
        var act = () => result.Value;
        act.Should().Throw<InvalidOperationException>();
    }

    private sealed class TestOrcaCoreException(string message)
        : OrcaCoreException("TEST-ERROR", message);
}
