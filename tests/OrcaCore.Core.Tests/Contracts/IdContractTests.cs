using System.Text.Json;
using AwesomeAssertions;
using OrcaCore.Abstractions.Ids;
using Xunit;

namespace OrcaCore.Core.Tests.Contracts;

public sealed class IdContractTests
{
    [Fact]
    public void Ids_New_AreUniqueAndVersion7Ordered()
    {
        var first = InstanceId.New();
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

        AssertJsonRoundTrip(new InstanceId(guid), $"\"{guid}\"");
        AssertJsonRoundTrip(new EventId(guid), $"\"{guid}\"");
        AssertJsonRoundTrip(new CommandId(guid), $"\"{guid}\"");
        AssertJsonRoundTrip(new CausationId(guid), $"\"{guid}\"");
        AssertJsonRoundTrip(new DefinitionId(guid), $"\"{guid}\"");
        AssertJsonRoundTrip(new WaitId(guid), $"\"{guid}\"");
        AssertJsonRoundTrip(new TimerId(guid), $"\"{guid}\"");
        AssertJsonRoundTrip(new OutboxRecordId(guid), $"\"{guid}\"");
        AssertJsonRoundTrip(new CorrelationId("order-123"), "\"order-123\"");
        AssertJsonRoundTrip(new DefinitionVersion(7), "7");
        AssertJsonRoundTrip(new StreamVersion(42), "42");
    }

    private static InstanceId NextOrderedInstanceIdAfter(InstanceId first)
    {
        for (var attempt = 0; attempt < 10_000; attempt++)
        {
            var candidate = InstanceId.New();
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
