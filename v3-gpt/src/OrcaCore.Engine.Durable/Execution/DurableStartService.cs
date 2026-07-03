using OrcaCore.Abstractions.Durable;
using OrcaCore.Abstractions.Ids;
using OrcaCore.Engine.Durable.Versioning;

namespace OrcaCore.Engine.Durable.Execution;

internal sealed class DurableStartService(DurableCommandProcessor commandProcessor)
{
    private readonly SemaphoreSlim gate = new(1, 1);
    private readonly Dictionary<string, StartedInstance> startedInstances = [];

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
                DurableVersionCompatibility.EnsureCompatible(
                    existing.DefinitionId,
                    existing.DefinitionVersion,
                    request.DefinitionId,
                    request.DefinitionVersion);
                return new StartOrGetResult(existing.InstanceId, false);
            }

            var durableExisting = await TryGetDurableExistingAsync(request, cancellationToken).ConfigureAwait(false);
            if (durableExisting is not null)
            {
                return durableExisting;
            }

            var instanceId = InstanceId.New();
            var result = await commandProcessor
                .ProcessAsync(
                    new StartWorkflowCommand
                    {
                        CommandId = CommandId.New(),
                        InstanceId = instanceId,
                        RequestedAt = request.RequestedAt,
                        DefinitionId = request.DefinitionId,
                        DefinitionVersion = request.DefinitionVersion,
                        IdempotencyKey = request.IdempotencyKey
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

            startedInstances.Add(
                request.IdempotencyKey,
                new StartedInstance(instanceId, request.DefinitionId, request.DefinitionVersion));
            return new StartOrGetResult(instanceId, true);
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

        DurableVersionCompatibility.EnsureCompatible(
            durableExisting.Value.DefinitionId,
            durableExisting.Value.DefinitionVersion,
            request.DefinitionId,
            request.DefinitionVersion);
        startedInstances[request.IdempotencyKey] = new StartedInstance(
            durableExisting.Value.InstanceId,
            durableExisting.Value.DefinitionId,
            durableExisting.Value.DefinitionVersion);
        return new StartOrGetResult(durableExisting.Value.InstanceId, false);
    }

    private sealed record StartedInstance(
        InstanceId InstanceId,
        DefinitionId DefinitionId,
        DefinitionVersion DefinitionVersion);
}

internal sealed record StartOrGetRequest(
    string IdempotencyKey,
    DefinitionId DefinitionId,
    DefinitionVersion DefinitionVersion,
    object? Input,
    DateTimeOffset RequestedAt);

internal sealed record StartOrGetResult(InstanceId InstanceId, bool Created);
