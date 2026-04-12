using System.Text.Json;

namespace OrcaCore.Runtime.Durable.Persistence;

public sealed record OutboxRecord(
    string OutboxId,
    string IdempotencyKey,
    string InstanceId,
    string? ParentInstanceId,
    string RootInstanceId,
    string? GroupId,
    string StreamId,
    int StreamVersion,
    int Sequence,
    string MessageType,
    string Channel,
    string Destination,
    SerializedPayloadEnvelope PayloadEnvelope,
    string? CorrelationId,
    string? CausationEventId,
    string? ResumeTokenId,
    OutboxStatus Status,
    int AttemptCount,
    DateTimeOffset CreatedAt,
    DateTimeOffset? LastAttemptAt,
    DateTimeOffset? NextAttemptAt,
    string? LeaseOwner,
    DateTimeOffset? LeaseExpiresAt,
    string? LastError)
{
    public OutboxRecord(
        string outboxId,
        string InstanceId,
        string EventName,
        JsonElement? Payload,
        DateTimeOffset CreatedAt,
        bool Dispatched,
        int FailureCount = 0,
        DateTimeOffset? LastFailureAt = null,
        string? LastFailure = null,
        bool Poisoned = false)
        : this(
            outboxId,
            outboxId,
            InstanceId,
            ParentInstanceId: null,
            RootInstanceId: InstanceId,
            GroupId: null,
            StreamId: InstanceId,
            StreamVersion: 0,
            Sequence: 0,
            MessageType: EventName,
            Channel: "legacy",
            Destination: EventName,
            PayloadEnvelope: new SerializedPayloadEnvelope(
                JsonPayloadEnvelopeSerializer.FromJsonElement(
                    Payload ?? JsonDocument.Parse("null").RootElement.Clone(),
                    JsonPayloadEnvelopeSerializer.JsonContentType,
                    "json"),
                "json"),
            CorrelationId: null,
            CausationEventId: null,
            ResumeTokenId: null,
            Status: Poisoned
                ? OutboxStatus.Poisoned
                : Dispatched
                    ? OutboxStatus.Dispatched
                    : OutboxStatus.Pending,
            AttemptCount: FailureCount,
            CreatedAt: CreatedAt,
            LastAttemptAt: LastFailureAt,
            NextAttemptAt: null,
            LeaseOwner: null,
            LeaseExpiresAt: null,
            LastError: LastFailure)
    {
    }

    public static string CreateDeterministicId(string instanceId, int streamVersion, int sequence) =>
        $"{instanceId}:{streamVersion}:{sequence}";

    public string EventName => MessageType;

    public JsonElement? Payload => JsonPayloadEnvelopeSerializer.ToJsonElement(PayloadEnvelope.Payload);

    public bool Dispatched => Status == OutboxStatus.Dispatched;

    public int FailureCount => AttemptCount;

    public DateTimeOffset? LastFailureAt => LastAttemptAt;

    public string? LastFailure => LastError;

    public bool Poisoned => Status == OutboxStatus.Poisoned;
}
