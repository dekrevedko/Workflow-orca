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
}
