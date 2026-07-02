using OrcaCore.Abstractions.Primitives;
using Xunit;

namespace OrcaCore.Core.Tests.Primitives;

public sealed class ValidationTests
{
    [Fact]
    public void Valid_IsValid_NoErrors()
    {
        var validation = Validation<string>.Valid("value");

        Assert.True(validation.IsValid);
        Assert.Equal("value", validation.Value);
        Assert.Empty(validation.Errors);
    }

    [Fact]
    public void Invalid_CollectsAllErrors_PreservesOrder()
    {
        var first = new ValidationError("first", "First error", "root.first");
        var second = new ValidationError("second", "Second error", "root.second");

        var validation = Validation<string>.Invalid([first, second]);

        Assert.False(validation.IsValid);
        Assert.Equal([first, second], validation.Errors);
        Assert.Throws<InvalidOperationException>(() => validation.Value);
    }

    [Fact]
    public void Combine_TwoInvalids_MergesErrorLists()
    {
        var firstError = new ValidationError("first", "First error");
        var secondError = new ValidationError("second", "Second error");
        var first = Validation<string>.Invalid([firstError]);
        var second = Validation<string>.Invalid([secondError]);

        var combined = first.Combine(second, (left, right) => left + right);

        Assert.False(combined.IsValid);
        Assert.Equal([firstError, secondError], combined.Errors);
    }
}
