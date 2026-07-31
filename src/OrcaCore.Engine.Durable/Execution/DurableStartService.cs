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
                    ? new StartOrGetResult(existing.InstanceId, false, null)
                    : new StartOrGetResult(existing.InstanceId, false, existing);
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

                throw new InvalidOperationException(
                    $"StartOrGet could not start workflow for key '{request.IdempotencyKey}': {result.Message}");
            }

            startedInstances.Add(request.IdempotencyKey, ToRecord(request, instanceId));
            return new StartOrGetResult(instanceId, true, null);
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
            ? new StartOrGetResult(durableExisting.Value.InstanceId, false, null)
            : new StartOrGetResult(durableExisting.Value.InstanceId, false, durableExisting.Value);
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
    Abstractions.Providers.SerializedPayload? Input,
    DateTimeOffset RequestedAt);

internal sealed record StartOrGetResult(
    InstanceId InstanceId,
    bool Created,
    StartedWorkflowIdempotencyRecord? ConflictingBinding);
