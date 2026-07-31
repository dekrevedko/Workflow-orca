namespace OrcaCore.Hosting;

/// <summary>
/// Configures one durable workflow-engine host.
/// </summary>
public sealed class DurableEngineHostOptions
{
    /// <summary>Gets structured execution limits for this host.</summary>
    public StructuredExecutionHostOptions StructuredExecution { get; init; } = null!;

    /// <summary>Gets the durable resource-pool catalog for this host.</summary>
    public DurableResourcePoolOptions ResourcePools { get; init; } = null!;

    internal ValidatedDurableEngineHostOptions ValidateAndCopy()
    {
        ArgumentNullException.ThrowIfNull(StructuredExecution);
        ArgumentNullException.ThrowIfNull(ResourcePools);
        ArgumentNullException.ThrowIfNull(ResourcePools.PartitionId);
        ArgumentNullException.ThrowIfNull(ResourcePools.Pools);

        if (StructuredExecution.MaxConcurrentExecutionPathsPerInstance <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(StructuredExecution),
                StructuredExecution.MaxConcurrentExecutionPathsPerInstance,
                "The execution-path limit must be positive.");
        }

        ArgumentNullException.ThrowIfNull(StructuredExecution.StepThrottles);
        var throttleTypes = new HashSet<Type>();
        var throttles = new List<StepExecutionThrottle>();
        foreach (var throttle in StructuredExecution.StepThrottles)
        {
            ArgumentNullException.ThrowIfNull(throttle);
            if (!throttleTypes.Add(throttle.StepType))
            {
                throw new ArgumentException(
                    $"StepThrottles contains duplicate exact step type '{throttle.StepType.FullName}'.",
                    nameof(StructuredExecution));
            }

            throttles.Add(throttle);
        }

        var structured = new StructuredExecutionHostOptions
        {
            MaxConcurrentExecutionPathsPerInstance =
                StructuredExecution.MaxConcurrentExecutionPathsPerInstance,
            StepThrottles = Array.AsReadOnly(throttles.ToArray())
        };
        var pools = new Dictionary<string, DurableResourcePoolDefinition>(StringComparer.Ordinal);
        foreach (var pool in ResourcePools.Pools)
        {
            ArgumentNullException.ThrowIfNull(pool);
            if (!pools.TryAdd(pool.Name.Value, pool))
            {
                throw new ArgumentException(
                    $"ResourcePools contains duplicate name '{pool.Name.Value}'.",
                    nameof(ResourcePools));
            }
        }

        return new ValidatedDurableEngineHostOptions(
            structured,
            ResourcePools.PartitionId,
            new Dictionary<string, DurableResourcePoolDefinition>(pools, StringComparer.Ordinal));
    }
}

internal sealed record ValidatedDurableEngineHostOptions(
    StructuredExecutionHostOptions StructuredExecution,
    ResourceGovernancePartitionId PartitionId,
    IReadOnlyDictionary<string, DurableResourcePoolDefinition> ResourcePools);
