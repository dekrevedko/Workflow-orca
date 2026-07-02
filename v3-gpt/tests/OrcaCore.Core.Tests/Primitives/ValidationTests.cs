using AwesomeAssertions;
using OrcaCore.Abstractions.Primitives;
using Xunit;

namespace OrcaCore.Core.Tests.Primitives;

public sealed class ValidationTests
{
    [Fact]
    public void Valid_IsValid_NoErrors()
    {
        var validation = Validation<string>.Valid("value");

        validation.IsValid.Should().BeTrue();
        validation.Value.Should().Be("value");
        validation.Errors.Should().BeEmpty();
    }

    [Fact]
    public void Invalid_CollectsAllErrors_PreservesOrder()
    {
        var first = new ValidationError("first", "First error", "root.first");
        var second = new ValidationError("second", "Second error", "root.second");

        var validation = Validation<string>.Invalid([first, second]);

        validation.IsValid.Should().BeFalse();
        validation.Errors.Should().Equal([first, second]);
        var act = () => validation.Value;
        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void Combine_TwoInvalids_MergesErrorLists()
    {
        var firstError = new ValidationError("first", "First error");
        var secondError = new ValidationError("second", "Second error");
        var first = Validation<string>.Invalid([firstError]);
        var second = Validation<string>.Invalid([secondError]);

        var combined = first.Combine(second, (left, right) => left + right);

        combined.IsValid.Should().BeFalse();
        combined.Errors.Should().Equal([firstError, secondError]);
    }
}
