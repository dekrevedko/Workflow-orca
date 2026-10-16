using System.Text.Json;
using OrcaCore.Abstractions.Ids;
using OrcaCore.Abstractions.Serialization;

namespace OrcaCore.Abstractions.Durable;

/// <summary>
/// Payload of an internal <c>continue</c> outbox record (DR-034): the restart-safe signal that
/// a committed advancement left the instance runnable. It is claimed only by the continuation
/// pump under a kind-partitioned claim (DR-037); duplicate or stale signals are harmless
/// because the driver reloads the committed position and performs no duplicate work.
/// </summary>
public sealed record DurableContinuationSignal
{
    /// <summary>
    /// Gets the runnable instance to advance.
    /// </summary>
    public required InstanceId InstanceId { get; init; }

    /// <summary>
    /// Gets when the runnable-leaving commit was decided.
    /// </summary>
    public required DateTimeOffset OccurredAt { get; init; }

    /// <summary>
    /// Gets the durable backoff deadline for a failed continuation attempt, when applicable.
    /// </summary>
    public DateTimeOffset? NotBefore { get; init; }

    /// <summary>
    /// Serializes this signal to the outbox payload representation.
    /// </summary>
    public byte[] Serialize()
    {
        return JsonSerializer.SerializeToUtf8Bytes(
            this,
            OrcaCoreJsonSerializerContext.Default.DurableContinuationSignal);
    }

    /// <summary>
    /// Deserializes a <c>continue</c> outbox record payload.
    /// </summary>
    public static DurableContinuationSignal Deserialize(byte[] payload)
    {
        ArgumentNullException.ThrowIfNull(payload);

        return JsonSerializer.Deserialize(
                payload,
                OrcaCoreJsonSerializerContext.Default.DurableContinuationSignal)
            ?? throw new JsonException("Durable continuation signal payload could not be deserialized.");
    }
}
