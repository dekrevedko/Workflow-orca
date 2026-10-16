using OrcaCore.Provider.Abstractions.ResourceGovernance;
using OrcaCore.Runtime.Protocol.ResourceGovernance;

namespace OrcaCore.Providers.SqlServer.Internal;

/// <summary>Applies resource-governance transitions to one transactionally loaded SQL Server state document.</summary>
internal sealed class SqlServerResourceGovernanceStateEngine : IDurableResourceGovernanceStore
{
    private readonly Lock gate = new();
    private readonly Dictionary<string, List<ResourceGovernanceRecord>> streams =
        new(StringComparer.Ordinal);

    internal SqlServerResourceGovernanceStateEngine()
    {
    }

    internal SqlServerResourceGovernanceStateEngine(StateSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        foreach (var stream in snapshot.Streams)
        {
            streams.Add(stream.PartitionId, stream.Records.ToList());
        }
    }

    internal StateSnapshot Capture()
    {
        lock (gate)
        {
            return new StateSnapshot(streams
                .Select(pair => new GovernanceStreamState(pair.Key, pair.Value.ToArray()))
                .ToArray());
        }
    }

    internal sealed record StateSnapshot(IReadOnlyList<GovernanceStreamState> Streams);

    internal sealed record GovernanceStreamState(
        string PartitionId,
        IReadOnlyList<ResourceGovernanceRecord> Records);

    /// <inheritdoc />
    public ValueTask<ResourceGovernanceStream> LoadAsync(
        ResourceGovernancePartitionId partitionId,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(partitionId);
        cancellationToken.ThrowIfCancellationRequested();
        lock (gate)
        {
            if (!streams.TryGetValue(partitionId.Value, out var records))
            {
                return ValueTask.FromResult(ResourceGovernanceStream.Create(0, []));
            }

            return ValueTask.FromResult(
                ResourceGovernanceStream.Create(records.Count, records.ToArray()));
        }
    }

    /// <inheritdoc />
    public ValueTask<ResourceGovernanceAppendResult> AppendAsync(
        ResourceGovernancePartitionId partitionId,
        long expectedVersion,
        IReadOnlyList<ResourceGovernanceRecord> records,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(partitionId);
        ArgumentOutOfRangeException.ThrowIfNegative(expectedVersion);
        ArgumentNullException.ThrowIfNull(records);
        cancellationToken.ThrowIfCancellationRequested();
        if (records.Count == 0)
        {
            throw new ArgumentException("A governance append batch cannot be empty.", nameof(records));
        }

        var copy = records.ToArray();
        if (copy.Any(record => record is null))
        {
            throw new ArgumentException("A governance append batch cannot contain null.", nameof(records));
        }

        for (var index = 0; index < copy.Length; index++)
        {
            if (copy[index].Sequence != expectedVersion + index + 1L)
            {
                throw new ArgumentException(
                    "A governance append batch must be consecutive after its expected version.",
                    nameof(records));
            }
        }

        lock (gate)
        {
            if (!streams.TryGetValue(partitionId.Value, out var persisted))
            {
                persisted = [];
                streams.Add(partitionId.Value, persisted);
            }

            if (persisted.Count != expectedVersion)
            {
                return ValueTask.FromResult<ResourceGovernanceAppendResult>(
                    new ResourceGovernanceAppendResult.Conflict(persisted.Count));
            }

            persisted.AddRange(copy);
            return ValueTask.FromResult<ResourceGovernanceAppendResult>(
                new ResourceGovernanceAppendResult.Committed(persisted.Count));
        }
    }
}
