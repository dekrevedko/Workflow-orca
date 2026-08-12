using System.Security.Cryptography;
using System.Text.Json.Serialization;

namespace OrcaCore.Runtime.Protocol.ResourceGovernance;

/// <summary>One validated record in a serialized durable-resource governance stream.</summary>
[JsonConverter(typeof(ResourceGovernanceRecordJsonConverter))]
public sealed class ResourceGovernanceRecord
{
    /// <summary>The only protocol format accepted by the first release.</summary>
    public const string V1Format = "orcacore-resource-governance-v1";

    private readonly byte[] payload;

    private ResourceGovernanceRecord(long sequence, string formatId, byte[] payload, string checksum)
    {
        Sequence = sequence;
        FormatId = formatId;
        this.payload = payload;
        Checksum = checksum;
    }

    /// <summary>Gets the positive stream sequence.</summary>
    public long Sequence { get; }

    /// <summary>Gets the protocol format identifier.</summary>
    public string FormatId { get; }

    /// <summary>Gets the defensively copied payload.</summary>
    public ReadOnlyMemory<byte> Payload => payload;

    /// <summary>Gets the lower-case SHA-256 payload checksum.</summary>
    public string Checksum { get; }

    /// <summary>Validates and materializes one record read from durable storage.</summary>
    public static ResourceGovernanceRecord FromPersisted(
        long sequence,
        string formatId,
        ReadOnlyMemory<byte> payload,
        string checksum)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(sequence, 1);
        ArgumentException.ThrowIfNullOrWhiteSpace(formatId);
        ArgumentException.ThrowIfNullOrWhiteSpace(checksum);
        if (!string.Equals(formatId, V1Format, StringComparison.Ordinal))
        {
            throw new NotSupportedException($"Resource governance format '{formatId}' is not supported.");
        }

        var copy = payload.ToArray();
        var actual = Convert.ToHexString(SHA256.HashData(copy)).ToLowerInvariant();
        if (!string.Equals(checksum, actual, StringComparison.Ordinal))
        {
            throw new ArgumentException("Resource governance record checksum does not match its payload.", nameof(checksum));
        }

        return new ResourceGovernanceRecord(sequence, formatId, copy, checksum);
    }
}

/// <summary>A complete validated durable-resource governance stream.</summary>
public sealed class ResourceGovernanceStream
{
    private ResourceGovernanceStream(long version, IReadOnlyList<ResourceGovernanceRecord> records)
    {
        Version = version;
        Records = records;
    }

    /// <summary>Gets the stream version.</summary>
    public long Version { get; }

    /// <summary>Gets the defensively copied complete sequence.</summary>
    public IReadOnlyList<ResourceGovernanceRecord> Records { get; }

    /// <summary>Validates a complete loaded sequence from one through <paramref name="version"/>.</summary>
    public static ResourceGovernanceStream Create(
        long version,
        IReadOnlyList<ResourceGovernanceRecord> records)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(version);
        ArgumentNullException.ThrowIfNull(records);
        if (records.Any(record => record is null))
        {
            throw new ArgumentException("Resource governance records cannot contain null.", nameof(records));
        }

        var copy = records.ToArray();
        if (version == 0)
        {
            if (copy.Length != 0)
            {
                throw new ArgumentException("Version zero requires an empty governance stream.", nameof(records));
            }

            return new ResourceGovernanceStream(0, Array.AsReadOnly(copy));
        }

        if (copy.LongLength != version)
        {
            throw new ArgumentException("Governance stream version must equal its complete record count.", nameof(records));
        }

        for (var index = 0; index < copy.Length; index++)
        {
            if (copy[index].Sequence != index + 1L)
            {
                throw new ArgumentException(
                    "Governance stream records must be the complete ordered sequence from one through Version.",
                    nameof(records));
            }
        }

        return new ResourceGovernanceStream(version, Array.AsReadOnly(copy));
    }
}

/// <summary>Closed result of an expected-version governance append.</summary>
public abstract record ResourceGovernanceAppendResult
{
    private protected ResourceGovernanceAppendResult()
    {
    }

    /// <summary>The whole batch committed.</summary>
    public sealed record Committed(long Version) : ResourceGovernanceAppendResult;

    /// <summary>The expected version lost to the supplied actual version.</summary>
    public sealed record Conflict(long ActualVersion) : ResourceGovernanceAppendResult;
}
