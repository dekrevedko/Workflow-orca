using OrcaCore.Hosting;

namespace OrcaCore.Hosting;

/// <summary>
/// Configures one ephemeral workflow-engine host.
/// </summary>
public sealed class EphemeralEngineHostOptions
{
    /// <summary>Gets structured execution limits for this host.</summary>
    public StructuredExecutionHostOptions StructuredExecution { get; init; } = null!;

    /// <summary>Gets the host-local named transient-pool catalog.</summary>
    public IReadOnlyList<TransientPoolDefinition> TransientPools { get; init; } = null!;

    internal ValidatedEphemeralEngineHostOptions ValidateAndCopy()
    {
        ArgumentNullException.ThrowIfNull(StructuredExecution);
        ArgumentNullException.ThrowIfNull(TransientPools);
        if (StructuredExecution.MaxConcurrentExecutionPathsPerInstance <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(StructuredExecution),
                StructuredExecution.MaxConcurrentExecutionPathsPerInstance,
                "The execution-path limit must be positive.");
        }

        ArgumentNullException.ThrowIfNull(StructuredExecution.StepThrottles);
        var throttles = new Dictionary<Type, int>();
        foreach (var throttle in StructuredExecution.StepThrottles)
        {
            ArgumentNullException.ThrowIfNull(throttle);
            if (!throttles.TryAdd(throttle.StepType, throttle.MaxConcurrency))
            {
                throw new ArgumentException(
                    $"StepThrottles contains duplicate exact step type '{throttle.StepType.FullName}'.",
                    nameof(StructuredExecution));
            }
        }

        var pools = new Dictionary<string, int>(StringComparer.Ordinal);
        foreach (var pool in TransientPools)
        {
            ArgumentNullException.ThrowIfNull(pool);
            if (!pools.TryAdd(pool.Name.Value, pool.Capacity))
            {
                throw new ArgumentException(
                    $"TransientPools contains duplicate name '{pool.Name.Value}'.",
                    nameof(TransientPools));
            }
        }

        return new ValidatedEphemeralEngineHostOptions(
            StructuredExecution.MaxConcurrentExecutionPathsPerInstance,
            new Dictionary<Type, int>(throttles),
            new Dictionary<string, int>(pools, StringComparer.Ordinal));
    }
}

/// <summary>
/// Defines one host-local named transient concurrency pool.
/// </summary>
public sealed class TransientPoolDefinition
{
    private TransientPoolDefinition(TransientPoolName name, int capacity)
    {
        Name = name;
        Capacity = capacity;
    }

    /// <summary>Gets the exact case-sensitive pool name.</summary>
    public TransientPoolName Name { get; }

    /// <summary>Gets the number of physical bodies admitted on this host.</summary>
    public int Capacity { get; }

    /// <summary>Creates an immutable transient-pool definition.</summary>
    public static TransientPoolDefinition Create(TransientPoolName name, int capacity)
    {
        ArgumentNullException.ThrowIfNull(name);
        ArgumentOutOfRangeException.ThrowIfLessThan(capacity, 1);
        return new TransientPoolDefinition(name, capacity);
    }
}

internal sealed record ValidatedEphemeralEngineHostOptions(
    int MaxConcurrentExecutionPathsPerInstance,
    IReadOnlyDictionary<Type, int> StepThrottles,
    IReadOnlyDictionary<string, int> TransientPools);
