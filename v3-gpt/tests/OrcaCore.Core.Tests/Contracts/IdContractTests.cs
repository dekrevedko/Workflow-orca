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
}
