using System.Text.Json;
using AwesomeAssertions;
using OrcaCore.Abstractions.Ids;
using OrcaCore.Core.Building;
using Xunit;

namespace OrcaCore.Core.Tests.Contracts;

public sealed class IdContractTests
{
    [Fact]
    public void DefinitionIdentity_IsNonDefaultableAndRejectsEmptyGuidText()
    {
        typeof(DefinitionId).IsClass.Should().BeTrue();
        typeof(DefinitionId).IsSealed.Should().BeTrue();
        typeof(DefinitionId).GetConstructors().Should().BeEmpty();

        var created = DefinitionId.New();
        created.Value.Should().NotBe(Guid.Empty);

        var parsed = DefinitionId.Parse(created.ToString());
        parsed.Should().Be(created);
        DefinitionId.TryParse(created.ToString(), out var tryParsed).Should().BeTrue();
        tryParsed.Should().Be(created);

        var empty = Guid.Empty.ToString();
        var parseEmpty = () => DefinitionId.Parse(empty);
        parseEmpty.Should().Throw<ArgumentException>();
        DefinitionId.TryParse(empty, out var emptyResult).Should().BeFalse();
        emptyResult.Should().BeNull();
        DefinitionId.TryParse(null, out var nullResult).Should().BeFalse();
        nullResult.Should().BeNull();
    }

    [Fact]
    public void DefinitionVersion_IsNonDefaultableAndPositive()
    {
        typeof(DefinitionVersion).IsClass.Should().BeTrue();
        typeof(DefinitionVersion).IsSealed.Should().BeTrue();
        DefinitionVersion.Initial.Value.Should().Be(1);
        new DefinitionVersion(7).Value.Should().Be(7);

        var zero = () => new DefinitionVersion(0);
        var negative = () => new DefinitionVersion(-1);
        zero.Should().Throw<ArgumentOutOfRangeException>();
        negative.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void ModeFirstAuthoring_RejectsNullDefinitionValuesAtTheBoundary()
    {
        var definitionId = DefinitionId.New();
        var definitionVersion = DefinitionVersion.Initial;

        var nullEphemeralId = () => global::OrcaCore.Workflow.Ephemeral<object>(null!, definitionVersion);
        var nullEphemeralVersion = () => global::OrcaCore.Workflow.Ephemeral<object>(definitionId, null!);
        var nullDurableId = () => global::OrcaCore.Workflow.Durable<object>(null!, definitionVersion);
        var nullDurableVersion = () => global::OrcaCore.Workflow.Durable<object>(definitionId, null!);

        nullEphemeralId.Should().Throw<ArgumentNullException>().WithParameterName("definitionId");
        nullEphemeralVersion.Should().Throw<ArgumentNullException>().WithParameterName("definitionVersion");
        nullDurableId.Should().Throw<ArgumentNullException>().WithParameterName("definitionId");
        nullDurableVersion.Should().Throw<ArgumentNullException>().WithParameterName("definitionVersion");
    }

    [Theory]
    [InlineData("00000000-0000-0000-0000-000000000000")]
    public void RuntimeGuidIdentityParsers_RejectEmptyGuidText(string empty)
    {
        var parseInstance = () => InstanceId.Parse(empty);
        var parseWait = () => WaitId.Parse(empty);

        parseInstance.Should().Throw<ArgumentException>();
        InstanceId.TryParse(empty, out var instanceId).Should().BeFalse();
        instanceId.Should().BeNull();

        parseWait.Should().Throw<ArgumentException>();
        WaitId.TryParse(empty, out var waitId).Should().BeFalse();
        waitId.Should().BeNull();
    }

    [Fact]
    public void Ids_New_AreUniqueAndVersion7Ordered()
    {
        var first = InstanceId.Parse(Guid.CreateVersion7().ToString());
        var second = NextOrderedInstanceIdAfter(first);

        first.Should().NotBe(second);
        first.Value.Version.Should().Be(7);
        second.Value.Version.Should().Be(7);
        first.Value.ToByteArray(bigEndian: true).AsSpan().SequenceCompareTo(
            second.Value.ToByteArray(bigEndian: true)).Should().BeLessThan(0);
    }

    [Fact]
    public void StronglyTypedIds_JsonRoundTripAsStableScalars()
    {
        var guid = Guid.Parse("018f3d31-7f2d-7ad0-a2b6-53e0ddcaf001");

        AssertJsonRoundTrip(InstanceId.Parse(guid.ToString()), $"\"{guid}\"");
        AssertJsonRoundTrip(EventId.Create(guid.ToString()), $"\"{guid}\"");
        AssertJsonRoundTrip(new CommandId(guid), $"\"{guid}\"");
        AssertJsonRoundTrip(new CausationId(guid), $"\"{guid}\"");
        AssertJsonRoundTrip(DefinitionId.Parse(guid.ToString()), $"\"{guid}\"");
        AssertJsonRoundTrip(WaitId.Parse(guid.ToString()), $"\"{guid}\"");
        AssertJsonRoundTrip(new TimerId(guid), $"\"{guid}\"");
        AssertJsonRoundTrip(new OutboxRecordId(guid), $"\"{guid}\"");
        AssertJsonRoundTrip(CorrelationId.Create("order-123"), "\"order-123\"");
        AssertJsonRoundTrip(new DefinitionVersion(7), "7");
        AssertJsonRoundTrip(new StreamVersion(42), "42");
    }

    private static InstanceId NextOrderedInstanceIdAfter(InstanceId first)
    {
        for (var attempt = 0; attempt < 10_000; attempt++)
        {
            var candidate = InstanceId.Parse(Guid.CreateVersion7().ToString());
            if (first.Value.ToByteArray(bigEndian: true).AsSpan().SequenceCompareTo(
                    candidate.Value.ToByteArray(bigEndian: true)) < 0)
            {
                return candidate;
            }
        }

        throw new InvalidOperationException("Could not create an ordered version-7 GUID after 10,000 attempts.");
    }

    private static void AssertJsonRoundTrip<T>(T value, string expectedJson)
    {
        JsonSerializer.Serialize(value).Should().Be(expectedJson);
        JsonSerializer.Deserialize<T>(expectedJson).Should().Be(value);
    }
}
