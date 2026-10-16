namespace OrcaCore.Core.Definitions;

/// <summary>
/// Deterministically partitions fanout input items for composition nodes.
/// </summary>
internal abstract record WorkflowPartitioner<TItem>
{
    public abstract IReadOnlyList<WorkflowPartition<TItem>> Partition(IReadOnlyList<TItem> items);

    public static WorkflowPartitioner<TItem> Items()
    {
        return new ItemWorkflowPartitioner<TItem>();
    }

    public static WorkflowPartitioner<TItem> Batch(int size)
    {
        return new FixedBatchWorkflowPartitioner<TItem>(size);
    }

    public static WorkflowPartitioner<TItem> BatchBy<TKey>(Func<TItem, TKey> keySelector)
    {
        ArgumentNullException.ThrowIfNull(keySelector);

        return new SelectorBatchWorkflowPartitioner<TItem, TKey>(keySelector);
    }

    public static WorkflowPartitioner<TItem> Custom(
        Func<IReadOnlyList<TItem>, IReadOnlyList<WorkflowPartition<TItem>>> partition)
    {
        ArgumentNullException.ThrowIfNull(partition);

        return new CustomWorkflowPartitioner<TItem>(partition);
    }
}

internal sealed record ItemWorkflowPartitioner<TItem> : WorkflowPartitioner<TItem>
{
    public override IReadOnlyList<WorkflowPartition<TItem>> Partition(IReadOnlyList<TItem> items)
    {
        ArgumentNullException.ThrowIfNull(items);

        return items
            .Select((item, index) => new WorkflowPartition<TItem>(index, [item]))
            .ToArray();
    }
}

internal sealed record FixedBatchWorkflowPartitioner<TItem>(int Size) : WorkflowPartitioner<TItem>
{
    public override IReadOnlyList<WorkflowPartition<TItem>> Partition(IReadOnlyList<TItem> items)
    {
        ArgumentNullException.ThrowIfNull(items);
        if (Size <= 0)
        {
            throw new InvalidOperationException("Batch size must be greater than zero.");
        }

        var partitions = new List<WorkflowPartition<TItem>>();
        for (var offset = 0; offset < items.Count; offset += Size)
        {
            partitions.Add(new WorkflowPartition<TItem>(
                partitions.Count,
                items.Skip(offset).Take(Size)));
        }

        return partitions;
    }
}

internal sealed record SelectorBatchWorkflowPartitioner<TItem, TKey>(
    Func<TItem, TKey> KeySelector) : WorkflowPartitioner<TItem>
{
    public override IReadOnlyList<WorkflowPartition<TItem>> Partition(IReadOnlyList<TItem> items)
    {
        ArgumentNullException.ThrowIfNull(items);

        var groups = new List<SelectorBatchGroup>();
        foreach (var item in items)
        {
            var key = KeySelector(item);
            var groupIndex = groups.FindIndex(group => EqualityComparer<TKey>.Default.Equals(group.Key, key));
            if (groupIndex < 0)
            {
                groups.Add(new SelectorBatchGroup(key, [item]));
                continue;
            }

            groups[groupIndex].Items.Add(item);
        }

        return groups
            .Select((group, index) => new WorkflowPartition<TItem>(index, group.Items))
            .ToArray();
    }

    private sealed record SelectorBatchGroup(TKey Key, List<TItem> Items);
}

internal sealed record CustomWorkflowPartitioner<TItem>(
    Func<IReadOnlyList<TItem>, IReadOnlyList<WorkflowPartition<TItem>>> PartitionItems) : WorkflowPartitioner<TItem>
{
    public override IReadOnlyList<WorkflowPartition<TItem>> Partition(IReadOnlyList<TItem> items)
    {
        ArgumentNullException.ThrowIfNull(items);

        return PartitionItems(items);
    }
}
