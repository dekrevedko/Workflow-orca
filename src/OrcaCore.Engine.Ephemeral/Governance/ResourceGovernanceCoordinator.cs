using System.Collections.Concurrent;
using System.Threading.Channels;

namespace OrcaCore.Engine.Ephemeral.Governance;

internal sealed class ResourceGovernanceCoordinator
{
    private readonly Channel<byte>? advancementTokens;
    private readonly Action? governanceWaitStarting;
    private readonly ConcurrentDictionary<string, Channel<byte>> namedPools;
    private readonly Channel<byte>? stepTokens;

    internal ResourceGovernanceCoordinator(EphemeralWorkflowEngineOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        advancementTokens = CreateTokenChannel(
            options.MaxConcurrentAdvancements,
            nameof(options.MaxConcurrentAdvancements));
        governanceWaitStarting = options.GovernanceWaitStarting;
        stepTokens = CreateTokenChannel(options.MaxConcurrentSteps, nameof(options.MaxConcurrentSteps));
        namedPools = new ConcurrentDictionary<string, Channel<byte>>(
            options.NamedPools.Select(pair =>
            {
                if (pair.Value <= 0)
                {
                    throw new ArgumentOutOfRangeException(
                        nameof(options.NamedPools),
                        pair.Value,
                        "Named pool limits must be positive.");
                }

                return new KeyValuePair<string, Channel<byte>>(
                    pair.Key,
                    CreateTokenChannel(pair.Value, nameof(options.NamedPools))!);
            }),
            StringComparer.Ordinal);
    }

    internal ValueTask<GovernanceLease> EnterAdvancementAsync(CancellationToken cancellationToken)
    {
        return EnterAsync(advancementTokens, governanceWaitStarting, cancellationToken);
    }

    internal async ValueTask<GovernanceLease> EnterStepAsync(
        string? poolKey,
        CancellationToken cancellationToken)
    {
        GovernanceLease poolLease = GovernanceLease.Empty;
        if (poolKey is not null && namedPools.TryGetValue(poolKey, out var pool))
        {
            poolLease = await EnterAsync(pool, governanceWaitStarting, cancellationToken)
                .ConfigureAwait(false);
        }

        try
        {
            var stepLease = await EnterAsync(stepTokens, governanceWaitStarting, cancellationToken)
                .ConfigureAwait(false);
            return poolLease == GovernanceLease.Empty
                ? stepLease
                : new GovernanceLease([poolLease, stepLease]);
        }
        catch
        {
            if (poolLease != GovernanceLease.Empty)
            {
                await poolLease.DisposeAsync().ConfigureAwait(false);
            }
            throw;
        }
    }

    internal bool TryEnterStep(string? poolKey, out GovernanceLease lease)
    {
        GovernanceLease poolLease = GovernanceLease.Empty;
        if (poolKey is not null && namedPools.TryGetValue(poolKey, out var pool) &&
            !TryEnter(pool, out poolLease))
        {
            lease = GovernanceLease.Empty;
            return false;
        }

        if (!TryEnter(stepTokens, out var stepLease))
        {
            if (poolLease != GovernanceLease.Empty)
            {
                _ = poolLease.DisposeAsync();
            }
            lease = GovernanceLease.Empty;
            return false;
        }

        lease = poolLease == GovernanceLease.Empty
            ? stepLease
            : new GovernanceLease([poolLease, stepLease]);
        return true;
    }

    private static Channel<byte>? CreateTokenChannel(int? limit, string parameterName)
    {
        if (limit is null)
        {
            return null;
        }

        if (limit <= 0)
        {
            throw new ArgumentOutOfRangeException(parameterName, limit, "Concurrency limits must be positive.");
        }

        var channel = Channel.CreateBounded<byte>(new BoundedChannelOptions(limit.Value)
        {
            AllowSynchronousContinuations = true,
            FullMode = BoundedChannelFullMode.Wait,
            SingleReader = false,
            SingleWriter = false
        });
        for (var index = 0; index < limit.Value; index++)
        {
            if (!channel.Writer.TryWrite(0))
            {
                throw new InvalidOperationException("Could not initialize a governance token channel.");
            }
        }

        return channel;
    }

    private static async ValueTask<GovernanceLease> EnterAsync(
        Channel<byte>? tokens,
        Action? governanceWaitStarting,
        CancellationToken cancellationToken)
    {
        if (tokens is null)
        {
            return GovernanceLease.Empty;
        }

        governanceWaitStarting?.Invoke();
        _ = await tokens.Reader.ReadAsync(cancellationToken).ConfigureAwait(false);
        return new GovernanceLease(tokens.Writer);
    }

    private static bool TryEnter(Channel<byte>? tokens, out GovernanceLease lease)
    {
        if (tokens is null)
        {
            lease = GovernanceLease.Empty;
            return true;
        }

        if (tokens.Reader.TryRead(out _))
        {
            lease = new GovernanceLease(tokens.Writer);
            return true;
        }

        lease = GovernanceLease.Empty;
        return false;
    }
}

internal sealed class GovernanceLease : IAsyncDisposable
{
    internal static GovernanceLease Empty { get; } = new((ChannelWriter<byte>?)null);

    private readonly IReadOnlyList<GovernanceLease>? innerLeases;
    private readonly ChannelWriter<byte>? tokenWriter;
    private int disposed;

    internal GovernanceLease(ChannelWriter<byte>? tokenWriter)
    {
        this.tokenWriter = tokenWriter;
    }

    internal GovernanceLease(IReadOnlyList<GovernanceLease> innerLeases)
    {
        this.innerLeases = innerLeases;
    }

    public async ValueTask DisposeAsync()
    {
        if (Interlocked.Exchange(ref disposed, 1) != 0)
        {
            return;
        }

        if (innerLeases is not null)
        {
            for (var index = innerLeases.Count - 1; index >= 0; index--)
            {
                await innerLeases[index].DisposeAsync().ConfigureAwait(false);
            }

            return;
        }

        if (tokenWriter is not null && !tokenWriter.TryWrite(0))
        {
            throw new InvalidOperationException("A governance token could not be returned to its bounded channel.");
        }
    }
}
