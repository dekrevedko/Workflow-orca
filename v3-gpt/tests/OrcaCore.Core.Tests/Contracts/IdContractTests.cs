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

        Assert.NotEqual(first, second);
        Assert.Equal(7, first.Value.Version);
        Assert.Equal(7, second.Value.Version);
        Assert.True(
            first.Value.ToByteArray(bigEndian: true).AsSpan().SequenceCompareTo(
                second.Value.ToByteArray(bigEndian: true)) < 0);
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
