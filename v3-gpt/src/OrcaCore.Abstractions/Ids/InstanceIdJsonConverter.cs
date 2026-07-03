using System.Text.Json;
using System.Text.Json.Serialization;

namespace OrcaCore.Abstractions.Ids;

/// <summary>
/// Converts instance identifiers to and from their stable GUID text form.
/// </summary>
public sealed class InstanceIdJsonConverter : JsonConverter<InstanceId>
{
    /// <inheritdoc />
    public override InstanceId Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        return new InstanceId(Guid.Parse(reader.GetString()!));
    }

    /// <inheritdoc />
    public override void Write(Utf8JsonWriter writer, InstanceId value, JsonSerializerOptions options)
    {
        writer.WriteStringValue(value.Value);
    }
}
