using System.Text.Json;
using System.Text.Json.Serialization;
using OrcaCore.Abstractions.Ids;

namespace OrcaCore.Abstractions.Providers;

internal sealed class InboxRouteKeyJsonConverter : JsonConverter<InboxRouteKey>
{
    public InboxRouteKeyJsonConverter()
    {
    }

    public override InboxRouteKey Read(
        ref Utf8JsonReader reader,
        Type typeToConvert,
        JsonSerializerOptions options)
    {
        var value = JsonSerializer.Deserialize<RouteValue>(ref reader, options) ??
            throw new JsonException("A persisted inbox route cannot be null.");
        return value.Kind switch
        {
            InboxRouteKinds.Direct => InboxRouteKey.Direct(
                value.InstanceId ?? throw new JsonException("A direct route requires an instance."),
                value.EventName,
                value.EventContractVersion,
                value.CorrelationId),
            InboxRouteKinds.Correlation => InboxRouteKey.Correlation(
                value.DefinitionId ?? throw new JsonException("A correlation route requires a definition."),
                value.EventName,
                value.EventContractVersion,
                value.CorrelationId),
            InboxRouteKinds.DefinitionFanoutTarget => InboxRouteKey.DefinitionFanoutTarget(
                value.InstanceId ?? throw new JsonException("A fanout target route requires an instance."),
                value.DefinitionId ?? throw new JsonException("A fanout target route requires a definition."),
                value.EventName,
                value.EventContractVersion,
                value.CorrelationId),
            _ => throw new JsonException($"Inbox route kind '{value.Kind}' is not supported.")
        };
    }

    public override void Write(
        Utf8JsonWriter writer,
        InboxRouteKey value,
        JsonSerializerOptions options)
    {
        ArgumentNullException.ThrowIfNull(value);
        JsonSerializer.Serialize(
            writer,
            new RouteValue(
                value.Kind,
                value.InstanceId,
                value.DefinitionId,
                value.EventName,
                value.EventContractVersion,
                value.CorrelationId),
            options);
    }

    private sealed record RouteValue(
        string Kind,
        InstanceId? InstanceId,
        DefinitionId? DefinitionId,
        EventName EventName,
        EventContractVersion EventContractVersion,
        CorrelationId CorrelationId);
}
