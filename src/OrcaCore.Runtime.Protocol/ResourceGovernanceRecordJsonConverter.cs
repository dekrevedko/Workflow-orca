using System.Text.Json;
using System.Text.Json.Serialization;

namespace OrcaCore.Runtime.Protocol.ResourceGovernance;

internal sealed class ResourceGovernanceRecordJsonConverter : JsonConverter<ResourceGovernanceRecord>
{
    public ResourceGovernanceRecordJsonConverter()
    {
    }

    public override ResourceGovernanceRecord Read(
        ref Utf8JsonReader reader,
        Type typeToConvert,
        JsonSerializerOptions options)
    {
        var persisted = JsonSerializer.Deserialize<PersistedRecord>(ref reader, options)
            ?? throw new JsonException("The resource-governance record was empty.");
        return ResourceGovernanceRecord.FromPersisted(
            persisted.Sequence,
            persisted.FormatId,
            persisted.Payload,
            persisted.Checksum);
    }

    public override void Write(
        Utf8JsonWriter writer,
        ResourceGovernanceRecord value,
        JsonSerializerOptions options) =>
        JsonSerializer.Serialize(
            writer,
            new PersistedRecord(value.Sequence, value.FormatId, value.Payload.ToArray(), value.Checksum),
            options);

    private sealed record PersistedRecord(long Sequence, string FormatId, byte[] Payload, string Checksum);
}
