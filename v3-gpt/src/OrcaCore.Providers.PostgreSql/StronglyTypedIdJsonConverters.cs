using System.Text.Json;
using System.Text.Json.Serialization;
using OrcaCore.Abstractions.Ids;

namespace OrcaCore.Providers.PostgreSql;

internal sealed class EventIdJsonConverter : JsonConverter<EventId>
{
    public override EventId Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        return new EventId(reader.GetGuid());
    }

    public override void Write(Utf8JsonWriter writer, EventId value, JsonSerializerOptions options)
    {
        writer.WriteStringValue(value.Value);
    }
}

internal sealed class InstanceIdJsonConverter : JsonConverter<InstanceId>
{
    public override InstanceId Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        return new InstanceId(reader.GetGuid());
    }

    public override void Write(Utf8JsonWriter writer, InstanceId value, JsonSerializerOptions options)
    {
        writer.WriteStringValue(value.Value);
    }
}

internal sealed class CommandIdJsonConverter : JsonConverter<CommandId>
{
    public override CommandId Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        return new CommandId(reader.GetGuid());
    }

    public override void Write(Utf8JsonWriter writer, CommandId value, JsonSerializerOptions options)
    {
        writer.WriteStringValue(value.Value);
    }
}

internal sealed class CausationIdJsonConverter : JsonConverter<CausationId>
{
    public override CausationId Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        return new CausationId(reader.GetGuid());
    }

    public override void Write(Utf8JsonWriter writer, CausationId value, JsonSerializerOptions options)
    {
        writer.WriteStringValue(value.Value);
    }
}

internal sealed class DefinitionIdJsonConverter : JsonConverter<DefinitionId>
{
    public override DefinitionId Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        return new DefinitionId(reader.GetGuid());
    }

    public override void Write(Utf8JsonWriter writer, DefinitionId value, JsonSerializerOptions options)
    {
        writer.WriteStringValue(value.Value);
    }
}

internal sealed class DefinitionVersionJsonConverter : JsonConverter<DefinitionVersion>
{
    public override DefinitionVersion Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        return new DefinitionVersion(reader.GetInt32());
    }

    public override void Write(Utf8JsonWriter writer, DefinitionVersion value, JsonSerializerOptions options)
    {
        writer.WriteNumberValue(value.Value);
    }
}

internal sealed class WaitIdJsonConverter : JsonConverter<WaitId>
{
    public override WaitId Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        return new WaitId(reader.GetGuid());
    }

    public override void Write(Utf8JsonWriter writer, WaitId value, JsonSerializerOptions options)
    {
        writer.WriteStringValue(value.Value);
    }
}

internal sealed class CorrelationIdJsonConverter : JsonConverter<CorrelationId>
{
    public override CorrelationId Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        return new CorrelationId(reader.GetString() ?? string.Empty);
    }

    public override void Write(Utf8JsonWriter writer, CorrelationId value, JsonSerializerOptions options)
    {
        writer.WriteStringValue(value.Value);
    }
}
