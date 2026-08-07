using OrcaCore.Abstractions.Errors;
using OrcaCore.Abstractions.Ids;
using OrcaCore.Abstractions.Primitives;

namespace OrcaCore.Abstractions.Providers;

/// <summary>
/// Creates expected event-store conflict results.
/// </summary>
public static class EventStoreConflict
{
    /// <summary>
    /// Creates an expected-version mismatch result.
    /// </summary>
    public static Result<AppendEventsResult> ExpectedVersionMismatch(
        StreamVersion expectedVersion,
        StreamVersion actualVersion)
    {
        return Result<AppendEventsResult>.Failure(
            new WorkflowConcurrencyException(
                $"Append expected version '{expectedVersion}' but actual stream version is '{actualVersion}'."));
    }

    /// <summary>
    /// Creates a conflict result for a durable start idempotency key that is already bound.
    /// </summary>
    public static Result<AppendEventsResult> StartIdempotencyKeyAlreadyExists(string idempotencyKey)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(idempotencyKey);

        return Result<AppendEventsResult>.Failure(
            new WorkflowConcurrencyException(
                $"Start idempotency key '{idempotencyKey}' is already bound to a workflow instance."));
    }

    /// <summary>Creates a conflict result when a globally owned event identity already exists.</summary>
    public static Result<AppendEventsResult> EventIdAlreadyExists(EventId eventId)
    {
        ArgumentNullException.ThrowIfNull(eventId);
        return Result<AppendEventsResult>.Failure(
            new WorkflowConcurrencyException(
                $"Inbound event identity '{eventId}' is already durably owned."));
    }
}
