namespace OrcaCore.Abstractions.Messaging;

public sealed record DispatchMessage
{
    private static readonly IReadOnlyDictionary<string, string> EmptyHeaders =
        new Dictionary<string, string>(StringComparer.Ordinal);

    public DispatchMessage(
        string messageId,
        string idempotencyKey,
        string messageType,
        string channel,
        string destination,
        DispatchPayload payload,
        string? correlationId = null,
        string? causationId = null,
        string? instanceId = null,
        string? parentInstanceId = null,
        string? rootInstanceId = null,
        string? resumeTokenId = null,
        IReadOnlyDictionary<string, string>? headers = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(messageId);
        ArgumentException.ThrowIfNullOrWhiteSpace(idempotencyKey);
        ArgumentException.ThrowIfNullOrWhiteSpace(messageType);
        ArgumentException.ThrowIfNullOrWhiteSpace(channel);
        ArgumentException.ThrowIfNullOrWhiteSpace(destination);

        MessageId = messageId;
        IdempotencyKey = idempotencyKey;
        MessageType = messageType;
        Channel = channel;
        Destination = destination;
        Payload = payload;
        CorrelationId = correlationId;
        CausationId = causationId;
        InstanceId = instanceId;
        ParentInstanceId = parentInstanceId;
        RootInstanceId = rootInstanceId;
        ResumeTokenId = resumeTokenId;
        Headers = headers ?? EmptyHeaders;
    }

    public string MessageId { get; init; }

    public string IdempotencyKey { get; init; }

    public string MessageType { get; init; }

    public string Channel { get; init; }

    public string Destination { get; init; }

    public DispatchPayload Payload { get; init; }

    public string? CorrelationId { get; init; }

    public string? CausationId { get; init; }

    public string? InstanceId { get; init; }

    public string? ParentInstanceId { get; init; }

    public string? RootInstanceId { get; init; }

    public string? ResumeTokenId { get; init; }

    public IReadOnlyDictionary<string, string> Headers { get; init; }

    public string EventName => MessageType;

    public string OutboxId => MessageId;
}
