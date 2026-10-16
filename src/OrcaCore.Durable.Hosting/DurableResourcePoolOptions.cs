namespace OrcaCore.Hosting;

/// <summary>Defines immutable creation metadata for one durable resource pool.</summary>
public sealed class DurableResourcePoolDefinition
{
    private DurableResourcePoolDefinition(
        ResourcePoolName name,
        int capacity,
        TimeSpan reviewAfter)
    {
        Name = name;
        Capacity = capacity;
        ReviewAfter = reviewAfter;
    }

    public ResourcePoolName Name { get; }

    public int Capacity { get; }

    public TimeSpan ReviewAfter { get; }

    public static DurableResourcePoolDefinition Create(
        ResourcePoolName name,
        int capacity,
        TimeSpan reviewAfter)
    {
        ArgumentNullException.ThrowIfNull(name);
        ArgumentOutOfRangeException.ThrowIfLessThan(capacity, 1);
        if (reviewAfter <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(reviewAfter));
        }

        return new DurableResourcePoolDefinition(name, capacity, reviewAfter);
    }
}

/// <summary>Configures one serialized durable resource-governance partition.</summary>
public sealed class DurableResourcePoolOptions
{
    public required ResourceGovernancePartitionId PartitionId { get; init; }

    public required IReadOnlyList<DurableResourcePoolDefinition> Pools { get; init; }
}
