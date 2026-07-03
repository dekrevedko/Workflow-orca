using AwesomeAssertions;
using OrcaCore.Abstractions.Ids;

namespace OrcaCore.Core.Tests.Contracts;

public sealed class IdsTests
{
    [Fact]
    public void Ids_New_AreUniqueAndVersion7Ordered()
    {
        AssertGuidIdOrdering(InstanceId.New, id => id.Value);
        AssertGuidIdOrdering(EventId.New, id => id.Value);
        AssertGuidIdOrdering(WaitId.New, id => id.Value);
        AssertGuidIdOrdering(DefinitionId.New, id => id.Value);
    }

    private static void AssertGuidIdOrdering<TId>(
        Func<TId> factory,
        Func<TId, Guid> guidSelector)
    {
        var first = factory();
        Thread.Sleep(2);
        var second = factory();

        first.Should().NotBe(second);

        Span<byte> firstBytes = stackalloc byte[16];
        Span<byte> secondBytes = stackalloc byte[16];
        guidSelector(first).TryWriteBytes(firstBytes, bigEndian: true, out _);
        guidSelector(second).TryWriteBytes(secondBytes, bigEndian: true, out _);

        firstBytes.SequenceCompareTo(secondBytes).Should().BeLessThan(0);
    }
}
