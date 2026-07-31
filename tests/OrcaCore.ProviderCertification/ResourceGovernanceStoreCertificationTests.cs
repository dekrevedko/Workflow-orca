using System.Security.Cryptography;
using System.Text;
using AwesomeAssertions;
using OrcaCore.Provider.Abstractions.ResourceGovernance;
using OrcaCore.Providers.InMemory;
using OrcaCore.Runtime.Protocol.ResourceGovernance;
using Xunit;

namespace OrcaCore.ProviderCertification;

public abstract class ResourceGovernanceStoreCertificationTests
{
    protected abstract IDurableResourceGovernanceStore CreateStore();

    [Fact]
    public async Task AppendAsync_CommitsWholeOrderedBatch_OrReturnsConflictWithoutPartialAppend()
    {
        var store = CreateStore();
        var partition = ResourceGovernancePartitionId.Create("default");
        var first = Record(1, "first");
        var second = Record(2, "second");

        var committed = await store.AppendAsync(
            partition,
            0,
            [first, second],
            TestContext.Current.CancellationToken);
        var conflict = await store.AppendAsync(
            partition,
            0,
            [Record(1, "loser")],
            TestContext.Current.CancellationToken);
        var loaded = await store.LoadAsync(partition, TestContext.Current.CancellationToken);

        committed.Should().BeEquivalentTo(new ResourceGovernanceAppendResult.Committed(2));
        conflict.Should().BeEquivalentTo(new ResourceGovernanceAppendResult.Conflict(2));
        loaded.Version.Should().Be(2);
        loaded.Records.Select(record => Encoding.UTF8.GetString(record.Payload.Span))
            .Should().Equal("first", "second");
    }

    [Fact]
    public async Task AppendAsync_RejectsEmptyNonconsecutiveOrMutableBatchesBeforeMutation()
    {
        var store = CreateStore();
        var partition = ResourceGovernancePartitionId.Create("default");
        var records = new[] { Record(1, "one") };
        await store.AppendAsync(partition, 0, records, TestContext.Current.CancellationToken);
        records[0] = Record(1, "mutated");

        await store.Invoking(candidate => candidate.AppendAsync(
                partition,
                1,
                [],
                TestContext.Current.CancellationToken).AsTask())
            .Should().ThrowAsync<ArgumentException>();
        await store.Invoking(candidate => candidate.AppendAsync(
                partition,
                1,
                [Record(3, "gap")],
                TestContext.Current.CancellationToken).AsTask())
            .Should().ThrowAsync<ArgumentException>();

        var loaded = await store.LoadAsync(partition, TestContext.Current.CancellationToken);
        Encoding.UTF8.GetString(loaded.Records.Single().Payload.Span).Should().Be("one");
    }

    internal static ResourceGovernanceRecord Record(long sequence, string payload)
    {
        var bytes = Encoding.UTF8.GetBytes(payload);
        var checksum = Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();
        return ResourceGovernanceRecord.FromPersisted(
            sequence,
            ResourceGovernanceRecord.V1Format,
            bytes,
            checksum);
    }
}

public sealed class InMemoryResourceGovernanceStoreCertificationTests
    : ResourceGovernanceStoreCertificationTests
{
    protected override IDurableResourceGovernanceStore CreateStore()
        => new InMemoryResourceGovernanceStore();
}

public sealed class ResourceGovernanceValueTests
{
    [Fact]
    public void Record_ValidatesAndDefensivelyCopiesPayload()
    {
        var payload = Encoding.UTF8.GetBytes("stable");
        var checksum = Convert.ToHexString(SHA256.HashData(payload)).ToLowerInvariant();
        var record = ResourceGovernanceRecord.FromPersisted(
            1,
            ResourceGovernanceRecord.V1Format,
            payload,
            checksum);

        payload[0] = (byte)'X';

        Encoding.UTF8.GetString(record.Payload.Span).Should().Be("stable");
        Action badSequence = () => ResourceGovernanceRecord.FromPersisted(
            0,
            ResourceGovernanceRecord.V1Format,
            Array.Empty<byte>(),
            Convert.ToHexString(SHA256.HashData(Array.Empty<byte>())).ToLowerInvariant());
        Action badFormat = () => ResourceGovernanceRecord.FromPersisted(
            1,
            "foreign",
            Array.Empty<byte>(),
            checksum);
        Action badChecksum = () => ResourceGovernanceRecord.FromPersisted(
            1,
            ResourceGovernanceRecord.V1Format,
            Array.Empty<byte>(),
            checksum);
        badSequence.Should().Throw<ArgumentOutOfRangeException>();
        badFormat.Should().Throw<NotSupportedException>();
        badChecksum.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Stream_RequiresExactCompleteSequenceAndCopiesCollection()
    {
        var records = new[]
        {
            ResourceGovernanceStoreCertificationTests.Record(1, "one"),
            ResourceGovernanceStoreCertificationTests.Record(2, "two")
        };
        var stream = ResourceGovernanceStream.Create(2, records);
        records[0] = ResourceGovernanceStoreCertificationTests.Record(1, "changed");

        Encoding.UTF8.GetString(stream.Records[0].Payload.Span).Should().Be("one");
        ResourceGovernanceStream.Create(0, []).Version.Should().Be(0);

        Action negative = () => ResourceGovernanceStream.Create(-1, []);
        Action missing = () => ResourceGovernanceStream.Create(2, [records[0]]);
        Action reordered = () => ResourceGovernanceStream.Create(
            2,
            [records[1], records[0]]);
        Action recordsAtZero = () => ResourceGovernanceStream.Create(0, [records[0]]);
        negative.Should().Throw<ArgumentOutOfRangeException>();
        missing.Should().Throw<ArgumentException>();
        reordered.Should().Throw<ArgumentException>();
        recordsAtZero.Should().Throw<ArgumentException>();
    }
}
