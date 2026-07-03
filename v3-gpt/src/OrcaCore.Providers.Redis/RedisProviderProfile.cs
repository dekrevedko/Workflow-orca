using OrcaCore.Abstractions.Providers;

namespace OrcaCore.Providers.Redis;

/// <summary>
/// Describes the deliberately limited Redis provider profile.
/// </summary>
public sealed class RedisProviderProfile
{
    /// <summary>
    /// Initializes a Redis provider profile for projection/cache roles.
    /// </summary>
    public RedisProviderProfile(IWorkflowProjectionStore projectionStore)
    {
        ProjectionStore = projectionStore ?? throw new ArgumentNullException(nameof(projectionStore));
    }

    /// <summary>
    /// Gets the supported projection store.
    /// </summary>
    public IWorkflowProjectionStore ProjectionStore { get; }

    /// <summary>
    /// Gets whether this profile supports durable event-store semantics.
    /// </summary>
    public bool SupportsEventStore => false;

    /// <summary>
    /// Gets the event store when supported; Redis does not implement this port.
    /// </summary>
    public IWorkflowEventStore? EventStore => null;
}
