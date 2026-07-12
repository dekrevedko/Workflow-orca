using OrcaCore.Abstractions.Providers;

namespace OrcaCore.Engine.Durable.Execution;

/// <summary>
/// Routes new writes to one configured codec and historical reads by payload content type.
/// </summary>
public sealed class ContentTypeWorkflowPayloadSerializer : IWorkflowPayloadSerializer
{
    private readonly IReadOnlyDictionary<string, IWorkflowPayloadCodec> codecs;
    private readonly IWorkflowPayloadCodec writeCodec;

    /// <summary>
    /// Creates a serializer from all readable codecs and the content type used for new writes.
    /// </summary>
    public ContentTypeWorkflowPayloadSerializer(
        IEnumerable<IWorkflowPayloadCodec> codecs,
        string writeContentType)
    {
        ArgumentNullException.ThrowIfNull(codecs);
        ArgumentException.ThrowIfNullOrWhiteSpace(writeContentType);

        var byContentType = new Dictionary<string, IWorkflowPayloadCodec>(StringComparer.OrdinalIgnoreCase);
        foreach (var codec in codecs)
        {
            ArgumentNullException.ThrowIfNull(codec);
            ArgumentException.ThrowIfNullOrWhiteSpace(codec.ContentType);
            if (!byContentType.TryAdd(codec.ContentType, codec))
            {
                throw new InvalidOperationException(
                    $"A duplicate workflow payload codec is registered for content type '{codec.ContentType}'.");
            }
        }

        if (!byContentType.TryGetValue(writeContentType, out writeCodec!))
        {
            throw new InvalidOperationException(
                $"The configured workflow payload write content type '{writeContentType}' has no registered codec.");
        }

        this.codecs = byContentType;
    }

    /// <inheritdoc />
    public SerializedPayload Serialize<TPayload>(TPayload payload)
    {
        var serialized = writeCodec.Serialize(payload);
        if (!string.Equals(serialized.ContentType, writeCodec.ContentType, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                $"Workflow payload codec '{writeCodec.GetType().Name}' returned content type " +
                $"'{serialized.ContentType}' instead of '{writeCodec.ContentType}'.");
        }

        return serialized;
    }

    /// <inheritdoc />
    public TPayload Deserialize<TPayload>(SerializedPayload payload)
    {
        ArgumentNullException.ThrowIfNull(payload);
        ArgumentException.ThrowIfNullOrWhiteSpace(payload.ContentType);
        if (!codecs.TryGetValue(payload.ContentType, out var codec))
        {
            throw new NotSupportedException(
                $"Workflow payload content type '{payload.ContentType}' has no registered codec.");
        }

        return codec.Deserialize<TPayload>(payload);
    }
}
