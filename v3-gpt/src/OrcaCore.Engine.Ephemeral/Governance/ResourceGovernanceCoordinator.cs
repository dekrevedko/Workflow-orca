using System.Collections.Concurrent;

namespace OrcaCore.Engine.Ephemeral.Governance;

internal sealed class ResourceGovernanceCoordinator
{
    private readonly SemaphoreSlim? advancementSemaphore;
    private readonly ConcurrentDictionary<string, SemaphoreSlim> namedPools;
    private readonly SemaphoreSlim? stepSemaphore;

    internal ResourceGovernanceCoordinator(EphemeralWorkflowEngineOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        advancementSemaphore = CreateSemaphore(options.MaxConcurrentAdvancements, nameof(options.MaxConcurrentAdvancements));
        stepSemaphore = CreateSemaphore(options.MaxConcurrentSteps, nameof(options.MaxConcurrentSteps));
        namedPools = new ConcurrentDictionary<string, SemaphoreSlim>(
            options.NamedPools.Select(pair =>
            {
                if (pair.Value <= 0)
                {
                    throw new ArgumentOutOfRangeException(
                        nameof(options.NamedPools),
                        pair.Value,
                        "Named pool limits must be positive.");
                }

                return new KeyValuePair<string, SemaphoreSlim>(
                    pair.Key,
                    new SemaphoreSlim(pair.Value, pair.Value));
            }),
            StringComparer.Ordinal);
    }

    internal async ValueTask<GovernanceLease> EnterAdvancementAsync(CancellationToken cancellationToken)
    {
        return await EnterAsync(advancementSemaphore, null, cancellationToken).ConfigureAwait(false);
    }

    internal async ValueTask<GovernanceLease> EnterStepAsync(
        string? poolKey,
        CancellationToken cancellationToken)
    {
        var stepLease = await EnterAsync(stepSemaphore, null, cancellationToken).ConfigureAwait(false);
        if (poolKey is null || !namedPools.TryGetValue(poolKey, out var pool))
        {
            return stepLease;
        }

        try
        {
            var poolLease = await EnterAsync(pool, null, cancellationToken).ConfigureAwait(false);
            return new GovernanceLease([stepLease, poolLease]);
        }
        catch
        {
            await stepLease.DisposeAsync().ConfigureAwait(false);
            throw;
        }
    }

    private static SemaphoreSlim? CreateSemaphore(int? limit, string parameterName)
    {
        if (limit is null)
        {
            return null;
        }

        if (limit <= 0)
        {
            throw new ArgumentOutOfRangeException(parameterName, limit, "Concurrency limits must be positive.");
        }

        return new SemaphoreSlim(limit.Value, limit.Value);
    }

    private static async ValueTask<GovernanceLease> EnterAsync(
        SemaphoreSlim? semaphore,
        string? poolKey,
        CancellationToken cancellationToken)
    {
        if (semaphore is null)
        {
            return GovernanceLease.Empty;
        }

        await semaphore.WaitAsync(cancellationToken).ConfigureAwait(false);
        return new GovernanceLease(semaphore, poolKey);
    }
}

internal sealed class GovernanceLease : IAsyncDisposable
{
    internal static GovernanceLease Empty { get; } = new(null, null);

    private readonly IReadOnlyList<GovernanceLease>? innerLeases;
    private readonly string? poolKey;
    private readonly SemaphoreSlim? semaphore;

    internal GovernanceLease(SemaphoreSlim? semaphore, string? poolKey)
    {
        this.semaphore = semaphore;
        this.poolKey = poolKey;
    }

    internal GovernanceLease(IReadOnlyList<GovernanceLease> innerLeases)
    {
        this.innerLeases = innerLeases;
    }

    public async ValueTask DisposeAsync()
    {
        if (innerLeases is not null)
        {
            for (var index = innerLeases.Count - 1; index >= 0; index--)
            {
                await innerLeases[index].DisposeAsync().ConfigureAwait(false);
            }

            return;
        }

        _ = poolKey;
        semaphore?.Release();
    }
}
