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

/// <summary>
/// Converts event identifiers to and from their stable GUID text form.
/// </summary>
public sealed class EventIdJsonConverter : JsonConverter<EventId>
{
    /// <inheritdoc />
    public override EventId Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        return new EventId(reader.GetGuid());
    }

    /// <inheritdoc />
    public override void Write(Utf8JsonWriter writer, EventId value, JsonSerializerOptions options)
    {
        writer.WriteStringValue(value.Value);
    }
}

/// <summary>
/// Converts durable command identifiers to and from their stable GUID text form.
/// </summary>
public sealed class CommandIdJsonConverter : JsonConverter<CommandId>
{
    /// <inheritdoc />
    public override CommandId Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        return new CommandId(reader.GetGuid());
    }

    /// <inheritdoc />
    public override void Write(Utf8JsonWriter writer, CommandId value, JsonSerializerOptions options)
    {
        writer.WriteStringValue(value.Value);
    }
}

/// <summary>
/// Converts causation identifiers to and from their stable GUID text form.
/// </summary>
public sealed class CausationIdJsonConverter : JsonConverter<CausationId>
{
    /// <inheritdoc />
    public override CausationId Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        return new CausationId(reader.GetGuid());
    }

    /// <inheritdoc />
    public override void Write(Utf8JsonWriter writer, CausationId value, JsonSerializerOptions options)
    {
        writer.WriteStringValue(value.Value);
    }
}

/// <summary>
/// Converts definition identifiers to and from their stable GUID text form.
/// </summary>
public sealed class DefinitionIdJsonConverter : JsonConverter<DefinitionId>
{
    /// <inheritdoc />
    public override DefinitionId Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        return new DefinitionId(reader.GetGuid());
    }

    /// <inheritdoc />
    public override void Write(Utf8JsonWriter writer, DefinitionId value, JsonSerializerOptions options)
    {
        writer.WriteStringValue(value.Value);
    }
}

/// <summary>
/// Converts definition versions to and from their numeric form.
/// </summary>
public sealed class DefinitionVersionJsonConverter : JsonConverter<DefinitionVersion>
{
    /// <inheritdoc />
    public override DefinitionVersion Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        return new DefinitionVersion(reader.GetInt32());
    }

    /// <inheritdoc />
    public override void Write(Utf8JsonWriter writer, DefinitionVersion value, JsonSerializerOptions options)
    {
        writer.WriteNumberValue(value.Value);
    }
}

/// <summary>
/// Converts wait identifiers to and from their stable GUID text form.
/// </summary>
public sealed class WaitIdJsonConverter : JsonConverter<WaitId>
{
    /// <inheritdoc />
    public override WaitId Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        return new WaitId(reader.GetGuid());
    }

    /// <inheritdoc />
    public override void Write(Utf8JsonWriter writer, WaitId value, JsonSerializerOptions options)
    {
        writer.WriteStringValue(value.Value);
    }
}

/// <summary>
/// Converts timer identifiers to and from their stable GUID text form.
/// </summary>
public sealed class TimerIdJsonConverter : JsonConverter<TimerId>
{
    /// <inheritdoc />
    public override TimerId Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        return new TimerId(reader.GetGuid());
    }

    /// <inheritdoc />
    public override void Write(Utf8JsonWriter writer, TimerId value, JsonSerializerOptions options)
    {
        writer.WriteStringValue(value.Value);
    }
}

/// <summary>
/// Converts correlation identifiers to and from their stable string form.
/// </summary>
public sealed class CorrelationIdJsonConverter : JsonConverter<CorrelationId>
{
    /// <inheritdoc />
    public override CorrelationId Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        return new CorrelationId(reader.GetString() ?? string.Empty);
    }

    /// <inheritdoc />
    public override void Write(Utf8JsonWriter writer, CorrelationId value, JsonSerializerOptions options)
    {
        writer.WriteStringValue(value.Value);
    }
}

/// <summary>
/// Converts stream versions to and from their numeric form.
/// </summary>
public sealed class StreamVersionJsonConverter : JsonConverter<StreamVersion>
{
    /// <inheritdoc />
    public override StreamVersion Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        return new StreamVersion(reader.GetInt64());
    }

    /// <inheritdoc />
    public override void Write(Utf8JsonWriter writer, StreamVersion value, JsonSerializerOptions options)
    {
        writer.WriteNumberValue(value.Value);
    }
}

/// <summary>
/// Converts outbox record identifiers to and from their stable GUID text form.
/// </summary>
public sealed class OutboxRecordIdJsonConverter : JsonConverter<OutboxRecordId>
{
    /// <inheritdoc />
    public override OutboxRecordId Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        return new OutboxRecordId(reader.GetGuid());
    }

    /// <inheritdoc />
    public override void Write(Utf8JsonWriter writer, OutboxRecordId value, JsonSerializerOptions options)
    {
        writer.WriteStringValue(value.Value);
    }
}
