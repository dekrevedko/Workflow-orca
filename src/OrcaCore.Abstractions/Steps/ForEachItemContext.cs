namespace OrcaCore.Abstractions.Steps;

/// <summary>
/// Identifies the item (or partition) a ForEach body step is currently processing.
/// </summary>
/// <remarks>
/// A ForEach body runs once per work item. Because the body shares the workflow's business state,
/// it cannot tell which item it is on from the state alone — this context supplies that. The
/// <see cref="Index"/> is stable and unique for the lifetime of the work item regardless of
/// dispatch order or concurrency, so it is safe to use even when the body suspends on I/O under a
/// bounded <c>maxConcurrency</c>.
/// </remarks>
public sealed class ForEachItemContext
{
    private readonly IReadOnlyList<object?> partitionItems;

    /// <summary>
    /// Initializes a ForEach item context for one work item (a partition of one or more items).
    /// </summary>
    public ForEachItemContext(int index, IReadOnlyList<object?> partitionItems)
    {
        ArgumentNullException.ThrowIfNull(partitionItems);

        Index = index;
        this.partitionItems = partitionItems;
    }

    /// <summary>
    /// Gets the stable, unique index of this work item within the ForEach.
    /// </summary>
    public int Index { get; }

    /// <summary>
    /// Gets the number of items in this work item's partition (1 for the default
    /// item-per-branch partitioner).
    /// </summary>
    public int Count => partitionItems.Count;

    /// <summary>
    /// Gets the single item for this work item. Use with the default partitioner, which yields one
    /// item per branch; throws when the partition holds more than one item.
    /// </summary>
    public TItem Item<TItem>()
    {
        if (partitionItems.Count != 1)
        {
            throw new InvalidOperationException(
                $"This ForEach work item holds {partitionItems.Count} items; use Items<TItem>() for a partitioned ForEach.");
        }

        return Cast<TItem>(partitionItems[0]);
    }

    /// <summary>
    /// Gets every item in this work item's partition, typed.
    /// </summary>
    public IReadOnlyList<TItem> Items<TItem>()
    {
        var typed = new TItem[partitionItems.Count];
        for (var index = 0; index < partitionItems.Count; index++)
        {
            typed[index] = Cast<TItem>(partitionItems[index]);
        }

        return typed;
    }

    private static TItem Cast<TItem>(object? value)
    {
        if (value is TItem item)
        {
            return item;
        }

        if (value is null && default(TItem) is null)
        {
            return default!;
        }

        throw new InvalidCastException(
            $"ForEach item of type '{value?.GetType().Name ?? "null"}' cannot be read as '{typeof(TItem).Name}'.");
    }
}
