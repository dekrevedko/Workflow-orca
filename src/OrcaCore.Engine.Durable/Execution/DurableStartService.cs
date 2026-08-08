using OrcaCore.Abstractions.Durable;
using OrcaCore.Abstractions.Ids;
using OrcaCore.Abstractions.Providers;

namespace OrcaCore.Engine.Durable.Execution;

internal sealed class DurableStartService(DurableCommandProcessor commandProcessor)
{
    private readonly SemaphoreSlim gate = new(1, 1);
    private readonly Dictionary<string, StartedWorkflowIdempotencyRecord> startedInstances = [];

    internal async Task<StartOrGetResult> StartOrGetAsync(
        StartOrGetRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        await gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (startedInstances.TryGetValue(request.IdempotencyKey, out var existing))
            {
                return Matches(existing, request)
                    ? StartOrGetResult.Existing(existing.InstanceId)
                    : StartOrGetResult.StartedConflict(existing);
            }

            var durableExisting = await TryGetDurableExistingAsync(request, cancellationToken).ConfigureAwait(false);
            if (durableExisting is not null)
            {
                return durableExisting;
            }

            var instanceId = InstanceId.Parse(Guid.CreateVersion7().ToString());
            var result = await commandProcessor
                .ProcessAsync(
                    new StartWorkflowCommand
                    {
                        CommandId = CommandId.New(),
                        InstanceId = instanceId,
                        RequestedAt = request.RequestedAt,
                        DefinitionId = request.DefinitionId,
                        DefinitionVersion = request.DefinitionVersion,
                        IdempotencyKey = request.IdempotencyKey,
                        DefinitionFingerprint = request.DefinitionFingerprint,
                        InputFingerprint = request.InputFingerprint,
                        InputContentType = request.Input?.ContentType,
                        InputPayload = request.Input?.Payload
                    },
                    cancellationToken)
                .ConfigureAwait(false);
            if (result.Outcome != DurableCommandOutcome.Committed)
            {
                var winner = await TryGetDurableExistingAsync(request, cancellationToken).ConfigureAwait(false);
                if (winner is not null)
                {
                    return winner;
                }

                var pendingConflict = await TryGetPendingConflictAsync(request, cancellationToken).ConfigureAwait(false);
                if (pendingConflict is not null)
                {
                    return StartOrGetResult.PendingConflict(pendingConflict);
                }

                throw new InvalidOperationException(
                    $"StartOrGet could not start workflow for key '{request.IdempotencyKey}': {result.Message}");
            }

            startedInstances.Add(request.IdempotencyKey, ToRecord(request, instanceId));
            return StartOrGetResult.NewlyCreated(instanceId);
        }
        finally
        {
            gate.Release();
        }
    }

    private async Task<StartOrGetResult?> TryGetDurableExistingAsync(
        StartOrGetRequest request,
        CancellationToken cancellationToken)
    {
        var durableExisting = await commandProcessor
            .GetStartedAsync(request.IdempotencyKey, cancellationToken)
            .ConfigureAwait(false);
        if (!durableExisting.HasValue)
        {
            return null;
        }

        startedInstances[request.IdempotencyKey] = durableExisting.Value;
        return Matches(durableExisting.Value, request)
            ? StartOrGetResult.Existing(durableExisting.Value.InstanceId)
            : StartOrGetResult.StartedConflict(durableExisting.Value);
    }

    private async Task<InboxStartIntentRecord?> TryGetPendingConflictAsync(
        StartOrGetRequest request,
        CancellationToken cancellationToken)
    {
        var pending = await commandProcessor
            .GetStartIntentAsync(request.IdempotencyKey, cancellationToken)
            .ConfigureAwait(false);
        if (!pending.HasValue)
        {
            return null;
        }

        var intent = pending.Value;
        return intent.State == InboxStartIntentState.Poisoned ||
               !intent.DefinitionId.Equals(request.DefinitionId) ||
               !intent.DefinitionVersion.Equals(request.DefinitionVersion) ||
               !string.Equals(
                   intent.WorkflowInputFingerprint,
                   request.InputFingerprint,
                   StringComparison.Ordinal)
            ? intent
            : null;
    }

    private static bool Matches(StartedWorkflowIdempotencyRecord existing, StartOrGetRequest request)
    {
        return existing.DefinitionId.Equals(request.DefinitionId) &&
               existing.DefinitionVersion.Equals(request.DefinitionVersion) &&
               string.Equals(
                   existing.DefinitionFingerprint,
                   request.DefinitionFingerprint,
                   StringComparison.Ordinal) &&
               string.Equals(existing.InputFingerprint, request.InputFingerprint, StringComparison.Ordinal);
    }

    private static StartedWorkflowIdempotencyRecord ToRecord(
        StartOrGetRequest request,
        InstanceId instanceId)
    {
        return new StartedWorkflowIdempotencyRecord(
            request.IdempotencyKey,
            instanceId,
            request.DefinitionId,
            request.DefinitionVersion,
            request.DefinitionFingerprint,
            request.InputFingerprint);
    }
}

internal sealed record StartOrGetRequest(
    string IdempotencyKey,
    DefinitionId DefinitionId,
    DefinitionVersion DefinitionVersion,
    string DefinitionFingerprint,
    string InputFingerprint,
    SerializedPayload? Input,
    DateTimeOffset RequestedAt);

internal sealed record StartOrGetResult(
    InstanceId? InstanceId,
    bool Created,
    StartedWorkflowIdempotencyRecord? ConflictingBinding,
    InboxStartIntentRecord? ConflictingPendingIntent)
{
    internal static StartOrGetResult NewlyCreated(InstanceId instanceId) =>
        new(instanceId, true, null, null);

    internal static StartOrGetResult Existing(InstanceId instanceId) =>
        new(instanceId, false, null, null);

    internal static StartOrGetResult StartedConflict(StartedWorkflowIdempotencyRecord conflict) =>
        new(conflict.InstanceId, false, conflict, null);

    internal static StartOrGetResult PendingConflict(InboxStartIntentRecord conflict) =>
        new(null, false, null, conflict);
}
