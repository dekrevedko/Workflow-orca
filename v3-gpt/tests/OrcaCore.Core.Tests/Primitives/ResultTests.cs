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

        Assert.True(result.IsSuccess);
        Assert.False(result.IsFailure);
        Assert.Equal(42, result.Value);
    }

    [Fact]
    public void Failure_ExposesError_AndValueThrows()
    {
        var error = new TestOrcaCoreException("failed");
        var result = Result<int>.Failure(error);

        Assert.True(result.IsFailure);
        Assert.False(result.IsSuccess);
        Assert.Same(error, result.Error);
        Assert.Throws<InvalidOperationException>(() => result.Value);
    }

    [Fact]
    public void Map_OnSuccess_Transforms()
    {
        var result = Result<int>.Success(4).Map(value => value * 2);

        Assert.True(result.IsSuccess);
        Assert.Equal(8, result.Value);
    }

    [Fact]
    public void Map_OnFailure_PropagatesError()
    {
        var error = new TestOrcaCoreException("failed");

        var result = Result<int>.Failure(error).Map(value => value * 2);

        Assert.True(result.IsFailure);
        Assert.Same(error, result.Error);
    }

    [Fact]
    public void Bind_ChainsResults_ShortCircuitsOnFailure()
    {
        var error = new TestOrcaCoreException("failed");

        var success = Result<int>.Success(2)
            .Bind(value => Result<string>.Success(value.ToString()));
        var failure = Result<int>.Failure(error)
            .Bind(value => Result<string>.Success(value.ToString()));

        Assert.Equal("2", success.Value);
        Assert.True(failure.IsFailure);
        Assert.Same(error, failure.Error);
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

        Assert.Equal(4, success);
        Assert.Equal(-1, failure);
        Assert.Equal(1, successCalls);
        Assert.Equal(1, failureCalls);
    }

    [Fact]
    public void DefaultStructs_AreSafe()
    {
        Result<string> result = default;

        Assert.True(result.IsFailure);
        Assert.False(result.IsSuccess);
        Assert.NotNull(result.Error);
        Assert.Throws<InvalidOperationException>(() => result.Value);
    }

    private sealed class TestOrcaCoreException(string message) : OrcaCoreException(message);
}
