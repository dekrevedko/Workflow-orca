using OrcaCore.Abstractions.Primitives;

namespace OrcaCore.Core.Tests.Primitives;

public class ValidationTests
{
    [Fact]
    public void Valid_IsValid_NoErrors()
    {
        var validation = Validation<int>.Valid(5);

        validation.IsValid.Should().BeTrue();
        validation.Value.Should().Be(5);
        validation.Errors.Should().BeEmpty();
    }

    [Fact]
    public void Invalid_CollectsAllErrors_PreservesOrder()
    {
        var errors = new[]
        {
            new ValidationError("E1", "first"),
            new ValidationError("E2", "second"),
        };

        var validation = Validation<int>.Invalid(errors);

        validation.IsValid.Should().BeFalse();
        validation.Errors.Should().Equal(errors);
    }

    [Fact]
    public void Combine_TwoInvalids_MergesErrorLists()
    {
        var first = Validation<int>.Invalid([new ValidationError("E1", "first")]);
        var second = Validation<int>.Invalid([new ValidationError("E2", "second")]);

        var combined = first.Combine(second);

        combined.IsValid.Should().BeFalse();
        combined.Errors.Should().HaveCount(2);
        combined.Errors.Should().Contain(e => e.Code == "E1");
        combined.Errors.Should().Contain(e => e.Code == "E2");
    }

    [Fact]
    public void Combine_TwoValids_StaysValid()
    {
        var first = Validation<int>.Valid(1);
        var second = Validation<int>.Valid(2);

        var combined = first.Combine(second);

        combined.IsValid.Should().BeTrue();
        combined.Errors.Should().BeEmpty();
    }
}
