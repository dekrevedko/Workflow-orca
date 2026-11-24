using OrcaCore.Abstractions.Ids;
using OrcaCore.Provider.Abstractions.ResourceGovernance;
using OrcaCore.Runtime.Protocol.ResourceGovernance;

namespace OrcaCore.Providers.InMemory;

/// <summary>Provides an atomic in-memory resource-governance stream per partition.</summary>
internal sealed class InMemoryResourceGovernanceStore(InMemoryWorkflowProvider? workflows = null) :
    ICheckpointGuardedResourceGovernanceStore
{
    private readonly Lock gate = new();
    private readonly Dictionary<string, List<ResourceGovernanceRecord>> streams =
        new(StringComparer.Ordinal);

    /// <inheritdoc />
    public ValueTask<ResourceGovernanceHead> LoadLatestAsync(
        ResourceGovernancePartitionId partitionId,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(partitionId);
        cancellationToken.ThrowIfCancellationRequested();
        lock (gate)
        {
            streams.TryGetValue(partitionId.Value, out var records);
            return ValueTask.FromResult(ResourceGovernanceHead.Create(
                partitionId, records?.Count ?? 0, records is { Count: > 0 } ? records[^1] : null));
        }
    }

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

    public ValueTask<ResourceGovernanceAppendResult> AppendIfCurrentAsync(
        ResourceGovernancePartitionId partitionId, long expectedVersion, IReadOnlyList<ResourceGovernanceRecord> records,
        IReadOnlyDictionary<InstanceId, long> checkpoints, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(checkpoints);
        cancellationToken.ThrowIfCancellationRequested();
        if (checkpoints.Count == 0) return AppendAsync(partitionId, expectedVersion, records, cancellationToken);
        return (workflows ?? throw new InvalidOperationException("Missing workflow checkpoint store.")).WithCurrentCheckpoints(
            checkpoints, current =>
            {
                if (current) return AppendAsync(partitionId, expectedVersion, records, cancellationToken);
                lock (gate)
                {
                    var version = streams.TryGetValue(partitionId.Value, out var stream) ? stream.Count : 0;
                    return ValueTask.FromResult<ResourceGovernanceAppendResult>(new ResourceGovernanceAppendResult.Conflict(version));
                }
            });
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
