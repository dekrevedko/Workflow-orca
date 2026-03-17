using System.Text.Json;

namespace OrcaCore.Runtime.Durable.Persistence;

public sealed record OutboxRecord(
    string OutboxId,
    string InstanceId,
    string EventName,
    JsonElement? Payload,
    DateTimeOffset CreatedAt,
    bool Dispatched,
    int FailureCount = 0,
    DateTimeOffset? LastFailureAt = null,
    string? LastFailure = null,
    bool Poisoned = false);
