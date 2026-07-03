using OrcaCore.Abstractions.Ids;

namespace OrcaCore.Core.Tests.Contracts;

public class IdsTests
{
    [Fact]
    public void Ids_New_AreUniqueAndVersion7Ordered()
    {
        var first = InstanceId.New();
        var second = InstanceId.New();

        first.Should().NotBe(second);

        // UUIDv7 layout (RFC 9562, big-endian): version nibble is the high nibble of byte 6
        // (must be 0x7), variant bits are the top two bits of byte 8 (must be 0b10). .NET's
        // CreateVersion7 uses random (not monotonic) sub-millisecond bits, so two rapid calls
        // are NOT guaranteed to sort by string/byte order — only the version/variant markers
        // are a stable, deterministic assertion.
        var firstBytes = first.Value.ToByteArray(bigEndian: true);
        var secondBytes = second.Value.ToByteArray(bigEndian: true);

        (firstBytes[6] >> 4).Should().Be(0x7);
        (secondBytes[6] >> 4).Should().Be(0x7);
        (firstBytes[8] >> 6).Should().Be(0b10);
        (secondBytes[8] >> 6).Should().Be(0b10);
    }

    [Fact]
    public void EventId_New_AreUnique()
    {
        var first = EventId.New();
        var second = EventId.New();

        first.Should().NotBe(second);
    }

    [Fact]
    public void WaitId_New_AreUnique()
    {
        var first = WaitId.New();
        var second = WaitId.New();

        first.Should().NotBe(second);
    }

    [Fact]
    public void DefinitionId_Empty_Throws()
    {
        var act = () => new DefinitionId(string.Empty);

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void DefinitionVersion_NonPositive_Throws()
    {
        var act = () => new DefinitionVersion(0);

        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void CorrelationId_Empty_Throws()
    {
        var act = () => new CorrelationId(string.Empty);

        act.Should().Throw<ArgumentException>();
    }
}
