namespace OrcaCore.Hosting;

/// <summary>
/// Configures structured workflow execution limits owned by one engine host.
/// </summary>
public sealed class StructuredExecutionHostOptions
{
    /// <summary>
    /// Gets the maximum number of runnable root, branch, or item execution paths
    /// admitted for one workflow instance.
    /// </summary>
    public int MaxConcurrentExecutionPathsPerInstance { get; init; }

    /// <summary>
    /// Gets host-wide concurrency limits for exact named step types.
    /// </summary>
    public IReadOnlyList<StepExecutionThrottle> StepThrottles { get; init; } = null!;

    internal ValidatedStructuredExecutionHostOptions ValidateAndCopy()
    {
        if (MaxConcurrentExecutionPathsPerInstance <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(MaxConcurrentExecutionPathsPerInstance),
                MaxConcurrentExecutionPathsPerInstance,
                "The execution-path limit must be positive.");
        }

        ArgumentNullException.ThrowIfNull(StepThrottles);
        var throttles = new Dictionary<Type, int>();
        foreach (var throttle in StepThrottles)
        {
            ArgumentNullException.ThrowIfNull(throttle);
            if (!throttles.TryAdd(throttle.StepType, throttle.MaxConcurrency))
            {
                throw new ArgumentException(
                    $"StepThrottles contains duplicate exact step type '{throttle.StepType.FullName}'.",
                    nameof(StepThrottles));
            }
        }

        return new ValidatedStructuredExecutionHostOptions(
            MaxConcurrentExecutionPathsPerInstance,
            new Dictionary<Type, int>(throttles));
    }
}

/// <summary>
/// Configures one host-wide concurrency limit for an exact named step type.
/// </summary>
public sealed class StepExecutionThrottle
{
    private StepExecutionThrottle(Type stepType, int maxConcurrency)
    {
        StepType = stepType;
        MaxConcurrency = maxConcurrency;
    }

    /// <summary>Gets the exact named step type governed by this throttle.</summary>
    public Type StepType { get; }

    /// <summary>Gets the maximum number of physical bodies admitted on this host.</summary>
    public int MaxConcurrency { get; }

    /// <summary>Creates a throttle for the exact named step type <typeparamref name="TStep"/>.</summary>
    public static StepExecutionThrottle For<TStep>(int maxConcurrency)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(maxConcurrency, 1);
        var stepType = typeof(TStep);
        if (!stepType.GetInterfaces().Any(
                candidate => candidate.IsGenericType &&
                    candidate.GetGenericTypeDefinition() == typeof(IStep<>)))
        {
            throw new ArgumentException(
                $"Type '{stepType.FullName}' must implement IStep<TState>.",
                nameof(TStep));
        }

        return new StepExecutionThrottle(stepType, maxConcurrency);
    }
}

internal sealed record ValidatedStructuredExecutionHostOptions(
    int MaxConcurrentExecutionPathsPerInstance,
    IReadOnlyDictionary<Type, int> StepThrottles);
