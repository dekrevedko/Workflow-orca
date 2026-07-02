using OrcaCore.Abstractions.Errors;
using OrcaCore.Abstractions.Primitives;

namespace OrcaCore.Core.Tests.Primitives;

public class ResultTests
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
        var error = new OrcaCoreException("boom");
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
        var result = Result<int>.Success(2).Map(x => x * 10);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Be(20);
    }

    [Fact]
    public void Map_OnFailure_PropagatesError()
    {
        var error = new OrcaCoreException("boom");
        var result = Result<int>.Failure(error).Map(x => x * 10);

        result.IsFailure.Should().BeTrue();
        result.Error.Should().BeSameAs(error);
    }

    [Fact]
    public void Bind_ChainsResults_ShortCircuitsOnFailure()
    {
        var error = new OrcaCoreException("boom");

        var success = Result<int>.Success(2).Bind(x => Result<int>.Success(x + 1));
        var failure = Result<int>.Failure(error).Bind(x => Result<int>.Success(x + 1));

        success.Value.Should().Be(3);
        failure.IsFailure.Should().BeTrue();
        failure.Error.Should().BeSameAs(error);
    }

    [Fact]
    public void Match_InvokesExactlyOneArm()
    {
        var successCalls = 0;
        var failureCalls = 0;

        Result<int>.Success(1).Match(
            onSuccess: _ => { successCalls++; return true; },
            onFailure: _ => { failureCalls++; return false; });

        Result<int>.Failure(new OrcaCoreException("boom")).Match(
            onSuccess: _ => { successCalls++; return true; },
            onFailure: _ => { failureCalls++; return false; });

        successCalls.Should().Be(1);
        failureCalls.Should().Be(1);
    }

    [Fact]
    public void DefaultStruct_IsSafe_BehavesAsFailure()
    {
        var result = default(Result<int>);

        result.IsFailure.Should().BeTrue();
        result.IsSuccess.Should().BeFalse();

        var act = () => result.Value;
        act.Should().Throw<InvalidOperationException>();
    }
}
