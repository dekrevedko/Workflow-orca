using OrcaCore;
using OrcaCore.Provider.Abstractions.ResourceGovernance;
using OrcaCore.Runtime.Protocol.ResourceGovernance;

_ = new CopyingGovernanceStore();

internal sealed class CopyingGovernanceStore : IDurableResourceGovernanceStore
{
    private readonly object gate = new();
    private readonly Dictionary<string, List<ResourceGovernanceRecord>> streams = new(StringComparer.Ordinal);

    public ValueTask<ResourceGovernanceStream> LoadAsync(
        ResourceGovernancePartitionId partitionId,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(partitionId);
        cancellationToken.ThrowIfCancellationRequested();
        lock (gate)
        {
            var records = streams.TryGetValue(partitionId.Value, out var persisted) ? Copy(persisted) : [];
            return ValueTask.FromResult(ResourceGovernanceStream.Create(records.Count, records));
        }
    }

    public ValueTask<ResourceGovernanceAppendResult> AppendAsync(
        ResourceGovernancePartitionId partitionId,
        long expectedVersion,
        IReadOnlyList<ResourceGovernanceRecord> records,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(partitionId);
        ArgumentNullException.ThrowIfNull(records);
        cancellationToken.ThrowIfCancellationRequested();
        lock (gate)
        {
            if (!streams.TryGetValue(partitionId.Value, out var persisted))
            {
                persisted = [];
                streams.Add(partitionId.Value, persisted);
            }

            if (expectedVersion != persisted.Count)
            {
                return ValueTask.FromResult<ResourceGovernanceAppendResult>(
                    new ResourceGovernanceAppendResult.Conflict(persisted.Count));
            }

            var copiedBatch = Copy(records);
            if (copiedBatch.Count == 0) throw new ArgumentException("Append batch must be nonempty.", nameof(records));
            for (var index = 0; index < copiedBatch.Count; index++)
            {
                if (copiedBatch[index].Sequence != expectedVersion + index + 1)
                    throw new ArgumentException("Append batch must be consecutive from expectedVersion + 1.", nameof(records));
            }

            var prospective = Copy(persisted);
            prospective.AddRange(copiedBatch);
            _ = ResourceGovernanceStream.Create(prospective.Count, prospective);
            persisted.AddRange(copiedBatch);
            return ValueTask.FromResult<ResourceGovernanceAppendResult>(
                new ResourceGovernanceAppendResult.Committed(persisted.Count));
        }
    }

    private static List<ResourceGovernanceRecord> Copy(IEnumerable<ResourceGovernanceRecord> records) =>
        records.Select(record => ResourceGovernanceRecord.FromPersisted(
            record.Sequence,
            record.FormatId,
            record.Payload.ToArray(),
            record.Checksum)).ToList();
}
