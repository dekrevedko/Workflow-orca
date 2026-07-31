namespace OrcaCore;

/// <summary>Reports that provider evidence lost tickets for a committed live lease.</summary>
public sealed class LeaseLostException : OrcaCoreException
{
    internal LeaseLostException(
        LeaseProtectionToken protectionToken,
        IReadOnlyList<ResourcePoolName> missingPools)
        : base("WF-LEASE-LOST", "A committed durable resource lease no longer has its exact provider tickets.")
    {
        ArgumentNullException.ThrowIfNull(protectionToken);
        ArgumentNullException.ThrowIfNull(missingPools);
        if (missingPools.Count == 0 || missingPools.Any(pool => pool is null))
        {
            throw new ArgumentException("At least one missing pool is required.", nameof(missingPools));
        }

        ProtectionToken = protectionToken;
        MissingPools = Array.AsReadOnly(missingPools
            .Distinct()
            .OrderBy(pool => pool.Value, StringComparer.Ordinal)
            .ToArray());
    }

    public LeaseProtectionToken ProtectionToken { get; }

    public IReadOnlyList<ResourcePoolName> MissingPools { get; }
}
