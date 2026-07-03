using AwesomeAssertions;
using OrcaCore.Abstractions.Primitives;

namespace OrcaCore.Core.Tests.Primitives;

public sealed class ValidationTests
{
    [Fact]
    public void Valid_IsValid_NoErrors()
    {
        var validation = Validation<string>.Valid("ok");

        validation.IsValid.Should().BeTrue();
        validation.Value.Should().Be("ok");
        validation.Errors.Should().BeEmpty();
    }

    [Fact]
    public void Invalid_CollectsAllErrors_PreservesOrder()
    {
        var first = new ValidationError("A", "first", "path.a");
        var second = new ValidationError("B", "second", "path.b");
        var validation = Validation<string>.Invalid(first, second);

        validation.IsValid.Should().BeFalse();
        validation.Errors.Should().Equal(first, second);
    }

    [Fact]
    public void Combine_TwoInvalids_MergesErrorLists()
    {
        var first = Validation<string>.Invalid(new ValidationError("A", "one"));
        var second = Validation<string>.Invalid(new ValidationError("B", "two"));

        var combined = Validation<string>.Combine(first, second);

        combined.IsValid.Should().BeFalse();
        combined.Errors.Select(e => e.Code).Should().Equal("A", "B");
    }
}
