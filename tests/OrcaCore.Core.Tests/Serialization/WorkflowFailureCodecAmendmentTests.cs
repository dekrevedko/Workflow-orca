using System.Text;
using System.Text.Json;
using AwesomeAssertions;
using OrcaCore.Core.Internal;
using Xunit;

namespace OrcaCore.Core.Tests.Serialization;

[Trait("Requirement", "CR-014a")]
[Trait("AC", "AC-626")]
public sealed class WorkflowFailureCodecAmendmentTests
{
    private const string Legacy = """{"code":"CODE","message":"message","authoredLocation":"workflow:$","occurrence":{"version":1,"kind":"root"},"causes":[]}""";
    private const string Typed = """{"version":2,"code":"CODE","message":"message","authoredLocation":"workflow:$","occurrence":{"version":1,"kind":"root"},"exceptionType":"Opaque.DoesNotExist, NeverLoad","causes":[]}""";

    [Fact]
    public void LegacyFailure_RetainsItsExactBytesAndNullType()
    {
        var value = Read(Legacy);
        value.ExceptionType.Should().BeNull();
        Encoding.UTF8.GetString(CoreWorkflowValueCodec.Serialize(value, typeof(WorkflowFailure))).Should().Be(Legacy);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void TypedFailure_RoundTripsExactOrderAndMixedRecursiveForms(bool typedParent)
    {
        var parent = typedParent ? Typed : Legacy;
        var child = typedParent ? Legacy : Typed;
        var mixed = parent.Replace("\"causes\":[]", "\"causes\":[" + child + "]", StringComparison.Ordinal);
        var value = Read(mixed);
        value.ExceptionType.Should().Be(typedParent ? "Opaque.DoesNotExist, NeverLoad" : null);
        value.Causes.Single().ExceptionType.Should().Be(typedParent ? null : "Opaque.DoesNotExist, NeverLoad");
        Encoding.UTF8.GetString(CoreWorkflowValueCodec.Serialize(value, typeof(WorkflowFailure))).Should().Be(mixed);
    }

    [Theory]
    [InlineData("\"version\":2", "\"version\":1")]
    [InlineData("\"version\":2", "\"version\":2.5")]
    [InlineData("\"version\":2", "\"version\":\"2\"")]
    [InlineData("\"version\":2,", "")]
    [InlineData("\"version\":2", "\"version\":2,\"version\":2")]
    [InlineData("\"exceptionType\":\"Opaque.DoesNotExist, NeverLoad\"", "\"exceptionType\":null")]
    [InlineData("\"exceptionType\":\"Opaque.DoesNotExist, NeverLoad\"", "\"exceptionType\":\"\"")]
    [InlineData("\"exceptionType\":\"Opaque.DoesNotExist, NeverLoad\"", "\"exceptionType\":\"   \"")]
    [InlineData("\"exceptionType\":\"Opaque.DoesNotExist, NeverLoad\"", "\"exceptionType\":42")]
    [InlineData("\"exceptionType\":\"Opaque.DoesNotExist, NeverLoad\",", "")]
    [InlineData("\"code\":\"CODE\"", "\"code\":\"CODE\",\"extra\":true")]
    [InlineData("\"code\":\"CODE\"", "\"code\":\"CODE\",\"code\":\"OTHER\"")]
    [InlineData("\"authoredLocation\":\"workflow:$\"", "\"authoredLocation\":null")]
    [InlineData("\"version\":1,\"kind\":\"root\"", "\"version\":2,\"kind\":\"root\"")]
    [InlineData("\"causes\":[]", "\"causes\":[null]")]
    public void TypedFailure_RejectsEveryMalformedCase(string before, string after)
    {
        Action read = () => Read(Typed.Replace(before, after, StringComparison.Ordinal));
        read.Should().Throw<JsonException>();
    }

    private static WorkflowFailure Read(string value) => (WorkflowFailure)CoreWorkflowValueCodec.Deserialize(
        Encoding.UTF8.GetBytes(value), typeof(WorkflowFailure))!;
}
