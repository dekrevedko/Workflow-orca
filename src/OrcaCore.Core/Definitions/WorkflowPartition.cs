namespace OrcaCore.Core.Definitions;

/// <summary>
/// A deterministic partition of fanout input items.
/// </summary>
internal sealed record WorkflowPartition<TItem>
{
    public WorkflowPartition(int index, IEnumerable<TItem> items)
    {
        if (index < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(index), index, "Partition index must be non-negative.");
        }

        ArgumentNullException.ThrowIfNull(items);

        Index = index;
        Items = new ReadOnlyList<TItem>(items);
    }

    public int Index { get; }

    public IReadOnlyList<TItem> Items { get; }
}
