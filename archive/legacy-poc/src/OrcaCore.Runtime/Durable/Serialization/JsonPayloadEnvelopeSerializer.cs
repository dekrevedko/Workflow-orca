using System.Text;
using System.Text.Json;

namespace OrcaCore.Runtime.Durable.Serialization;

public sealed class JsonPayloadEnvelopeSerializer(
    IPayloadSchemaResolver schemaResolver,
    JsonSerializerOptions? serializerOptions = null) : IPayloadEnvelopeSerializer
{
    public const string JsonContentType = "application/json";

    private readonly JsonSerializerOptions _serializerOptions = serializerOptions ?? new JsonSerializerOptions();

    public Result<SerializedPayloadEnvelope> Serialize(object? value, Type declaredType)
    {
        ArgumentNullException.ThrowIfNull(declaredType);

        if (!schemaResolver.TryGetTypeKey(declaredType, out var typeKey))
            return Result<SerializedPayloadEnvelope>.Failure(
                new DurablePayloadSerializationException(
                    $"Payload type '{declaredType.FullName}' is not registered for durable serialization."));

        if (!schemaResolver.TryGetSchemaId(declaredType, out var schemaId))
            return Result<SerializedPayloadEnvelope>.Failure(
                new DurablePayloadSerializationException(
                    $"Payload type '{declaredType.FullName}' is missing a durable schema id."));

        if (!schemaResolver.TryResolveContentType(declaredType, out var contentType))
            return Result<SerializedPayloadEnvelope>.Failure(
                new DurablePayloadSerializationException(
                    $"Payload type '{declaredType.FullName}' is missing a durable content type."));

        try
        {
            var body = JsonSerializer.SerializeToUtf8Bytes(value, declaredType, _serializerOptions);
            return Result<SerializedPayloadEnvelope>.Success(
                new SerializedPayloadEnvelope(
                    new DispatchPayload(body, contentType, schemaId),
                    typeKey));
        }
        catch (Exception ex)
        {
            return Result<SerializedPayloadEnvelope>.Failure(
                new DurablePayloadSerializationException(
                    $"Payload type '{declaredType.FullName}' could not be serialized: {ex.Message}"));
        }
    }

    public Result<object?> Deserialize(DispatchPayload payload, string typeKey, Type targetType)
    {
        ArgumentNullException.ThrowIfNull(targetType);
        ArgumentException.ThrowIfNullOrWhiteSpace(typeKey);

        if (!schemaResolver.TryResolveType(typeKey, out var payloadType))
        {
            return Result<object?>.Failure(
                new DurablePayloadDeserializationException(
                    $"Persisted payload references unknown payload type key '{typeKey}'."));
        }

        if (!targetType.IsAssignableFrom(payloadType) && targetType != typeof(object))
        {
            return Result<object?>.Failure(
                new DurablePayloadDeserializationException(
                    $"Payload type '{payloadType.FullName}' cannot be assigned to '{targetType.FullName}'."));
        }

        try
        {
            if (payload.Body.IsEmpty)
                return Result<object?>.Success(null);

            var value = JsonSerializer.Deserialize(payload.Body.Span, payloadType, _serializerOptions);
            return Result<object?>.Success(value);
        }
        catch (Exception ex)
        {
            return Result<object?>.Failure(
                new DurablePayloadDeserializationException(
                    $"Payload type '{payloadType.FullName}' could not be deserialized: {ex.Message}"));
        }
    }

    internal static JsonElement ToJsonElement(DispatchPayload payload)
    {
        if (payload.Body.IsEmpty)
            return JsonDocument.Parse("null").RootElement.Clone();

        return JsonDocument.Parse(payload.Body).RootElement.Clone();
    }

    internal static DispatchPayload FromJsonElement(
        JsonElement element,
        string contentType,
        string schemaId)
    {
        var body = Encoding.UTF8.GetBytes(element.GetRawText());
        return new DispatchPayload(body, contentType, schemaId);
    }
}
