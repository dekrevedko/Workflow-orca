using System.Reflection;
using AwesomeAssertions;
using Xunit;

namespace OrcaCore.Core.Tests.Contracts;

public sealed class PublicWorkflowValueContractTests
{
    [Fact]
    public void FingerprintAndLocation_AreOpaqueRuntimeCreatedValues()
    {
        AssertOpaqueValue<DefinitionFingerprint>();
        AssertOpaqueValue<PayloadFingerprint>();
        AssertOpaqueValue<AuthoredLocation>();
    }

    [Theory]
    [InlineData("workflow:$")]
    [InlineData("workflow:$/n:00000001/if:true/parallel:00000002/foreach:body/lease:body")]
    [InlineData("dag:$/dag-node:00000001")]
    public void AuthoredLocation_AcceptsOnlyCanonicalGrammar(string value)
    {
        ConstructLocation(value).Value.Should().Be(value);
    }

    [Theory]
    [InlineData("workflow")]
    [InlineData("workflow:$/n:1")]
    [InlineData("workflow:$/if:True")]
    [InlineData("workflow:$/branch:00000001")]
    [InlineData("dag:$/dag-node:000000001")]
    public void AuthoredLocation_RejectsNonCanonicalGrammar(string value)
    {
        var act = () => ConstructLocation(value);

        act.Should().Throw<TargetInvocationException>()
            .WithInnerException<ArgumentException>();
    }

    [Fact]
    public void Validation_ExposesOnlyImmutableDiagnosticsAndTryGetValueProjection()
    {
        var type = typeof(Validation<string>);

        type.IsClass.Should().BeTrue();
        type.IsSealed.Should().BeTrue();
        type.GetConstructors(BindingFlags.Public | BindingFlags.Instance).Should().BeEmpty();
        type.GetProperty(nameof(Validation<string>.Diagnostics))!.SetMethod.Should().BeNull();
        type.GetMethod(nameof(Validation<string>.TryGetValue)).Should().NotBeNull();
    }

    private static void AssertOpaqueValue<T>()
    {
        var type = typeof(T);
        type.IsClass.Should().BeTrue();
        type.IsSealed.Should().BeTrue();
        type.GetConstructors(BindingFlags.Public | BindingFlags.Instance).Should().BeEmpty();
        type.GetProperty("Value")!.SetMethod.Should().BeNull();
    }

    private static AuthoredLocation ConstructLocation(string value) =>
        (AuthoredLocation)typeof(AuthoredLocation)
            .GetConstructors(BindingFlags.NonPublic | BindingFlags.Instance)
            .Single()
            .Invoke([value]);
}
