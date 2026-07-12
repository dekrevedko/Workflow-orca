using System.Text.Json;
using OrcaCore.Abstractions.Providers;

namespace OrcaCore.Engine.Durable.Execution;

/// <summary>
/// Built-in JSON workflow payload codec. Hosting registers it as the default writer while the
/// content-type router keeps every registered codec available for historical reads.
/// </summary>
public sealed class JsonWorkflowPayloadSerializer : IWorkflowPayloadCodec
{
    /// <summary>
    /// Gets the stable content type used for JSON workflow payloads.
    /// </summary>
    public const string JsonContentType = "application/json";

    /// <inheritdoc />
    public string ContentType => JsonContentType;

    /// <inheritdoc />
    public SerializedPayload Serialize<TPayload>(TPayload payload)
    {
        return new SerializedPayload(
            JsonContentType,
            JsonSerializer.SerializeToUtf8Bytes(payload));
    }

    /// <inheritdoc />
    public TPayload Deserialize<TPayload>(SerializedPayload payload)
    {
        ArgumentNullException.ThrowIfNull(payload);
        if (!string.Equals(payload.ContentType, JsonContentType, StringComparison.OrdinalIgnoreCase))
        {
            throw new ArgumentException(
                $"Payload content type '{payload.ContentType}' is not supported.",
                nameof(payload));
        }

        return JsonSerializer.Deserialize<TPayload>(payload.Payload)
            ?? throw new JsonException($"Payload could not be deserialized as '{typeof(TPayload).Name}'.");
    }
}
