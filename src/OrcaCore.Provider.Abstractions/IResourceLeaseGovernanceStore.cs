using OrcaCore.Abstractions.Primitives;
using OrcaCore.Abstractions.Providers;

namespace OrcaCore.Provider.Abstractions.ResourceGovernance;

/// <summary>
/// Persists confirmation bindings and release tombstones in the same serialized
/// governance aggregate that owns durable resource capacity.
/// </summary>
public interface IResourceLeaseGovernanceStore
{
    /// <summary>
    /// Gets retained causal release evidence for one exact protection token.
    /// </summary>
    Task<Option<ResourcePoolReleaseEvidence>> GetReleaseEvidenceAsync(
        LeaseProtectionToken protectionToken,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the retained token bound to one accepted confirmation identity.
    /// </summary>
    Task<Option<LeaseProtectionToken>> GetConfirmationBindingAsync(
        StopConfirmationId confirmationId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Atomically binds a trusted confirmation and releases its exact holder.
    /// </summary>
    Task<ResourcePoolStopConfirmationStatus> ConfirmAndReleaseAsync(
        ResourcePoolStopConfirmationRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Purges retained release evidence after the provider deduplication window.
    /// Active ownership cannot be purged.
    /// </summary>
    Task PurgeReleaseEvidenceAsync(
        InstanceId instanceId,
        CancellationToken cancellationToken = default);
}
