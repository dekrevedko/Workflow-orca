using OrcaCore.Runtime.Protocol.ResourceGovernance;

namespace OrcaCore.Provider.Abstractions.ResourceGovernance;

/// <summary>Persists one serialized resource-governance aggregate per configured partition.</summary>
public interface IDurableResourceGovernanceStore
{
    /// <summary>Reads the committed version and latest full-state record together for current-state recovery.</summary>
    ValueTask<ResourceGovernanceHead> LoadLatestAsync(
        ResourceGovernancePartitionId partitionId,
        CancellationToken cancellationToken = default);

    /// <summary>Loads the complete validated governance stream for explicit history inspection.</summary>
    ValueTask<ResourceGovernanceStream> LoadAsync(
        ResourceGovernancePartitionId partitionId,
        CancellationToken cancellationToken = default);

    /// <summary>Atomically appends a non-empty consecutive batch or returns a version conflict.</summary>
    ValueTask<ResourceGovernanceAppendResult> AppendAsync(
        ResourceGovernancePartitionId partitionId,
        long expectedVersion,
        IReadOnlyList<ResourceGovernanceRecord> records,
        CancellationToken cancellationToken = default);
}
