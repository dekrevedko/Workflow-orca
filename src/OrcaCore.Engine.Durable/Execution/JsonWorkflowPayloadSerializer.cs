using OrcaCore.Abstractions.Providers;
using OrcaCore.Core.Compilation;
using OrcaCore.Core.Internal;

namespace OrcaCore.Engine.Durable.Execution;

/// <summary>
/// Fixed JSON workflow payload serializer used by durable hosting.
/// </summary>
internal sealed class JsonWorkflowPayloadSerializer
{
    /// <summary>
    /// Gets the stable content type used for JSON workflow payloads.
    /// </summary>
    public const string JsonContentType = CoreWorkflowValueCodec.Format;

    /// <inheritdoc />
    public SerializedPayload Serialize<TPayload>(TPayload payload)
    {
        return new SerializedPayload(
            JsonContentType,
            CoreWorkflowValueCodec.Serialize(payload, typeof(TPayload)));
    }

    /// <inheritdoc />
    public TPayload Deserialize<TPayload>(SerializedPayload payload)
    {
        ArgumentNullException.ThrowIfNull(payload);
        ValidateContentType(payload);

        return (TPayload)CoreWorkflowValueCodec.Deserialize(payload.Payload, typeof(TPayload))!;
    }

    public object? Deserialize(SerializedPayload payload, Type payloadType)
    {
        ArgumentNullException.ThrowIfNull(payload);
        ArgumentNullException.ThrowIfNull(payloadType);
        ValidateContentType(payload);

        return CoreWorkflowValueCodec.Deserialize(payload.Payload, payloadType);
    }

    private static void ValidateContentType(SerializedPayload payload)
    {
        if (!string.Equals(payload.ContentType, JsonContentType, StringComparison.Ordinal))
        {
            throw new ArgumentException(
                $"Payload content type '{payload.ContentType}' is not supported.",
                nameof(payload));
        }
    }

}
