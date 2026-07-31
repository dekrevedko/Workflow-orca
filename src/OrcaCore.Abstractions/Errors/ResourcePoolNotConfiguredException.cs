namespace OrcaCore;

/// <summary>Reports that a durable lease request names one or more unconfigured pools.</summary>
public sealed class ResourcePoolNotConfiguredException : OrcaCoreException
{
    internal ResourcePoolNotConfiguredException(IReadOnlyList<ResourcePoolName> missingPools)
        : base(
            "WF-RESOURCE-POOL-NOT-CONFIGURED",
            "One or more durable resource pools are not configured.")
    {
        ArgumentNullException.ThrowIfNull(missingPools);
        if (missingPools.Count == 0)
        {
            throw new ArgumentException("At least one missing pool is required.", nameof(missingPools));
        }

        if (missingPools.Any(pool => pool is null))
        {
            throw new ArgumentException("Missing pools cannot contain null.", nameof(missingPools));
        }

        MissingPools = Array.AsReadOnly(missingPools
            .Distinct()
            .OrderBy(pool => pool.Value, StringComparer.Ordinal)
            .ToArray());
    }

    /// <summary>Gets the copied distinct missing names in ordinal order.</summary>
    public IReadOnlyList<ResourcePoolName> MissingPools { get; }

    /// <summary>Creates the typed failure for a copied set of unconfigured pools.</summary>
    public static ResourcePoolNotConfiguredException For(IReadOnlyList<ResourcePoolName> missingPools)
        => new(missingPools);
}
