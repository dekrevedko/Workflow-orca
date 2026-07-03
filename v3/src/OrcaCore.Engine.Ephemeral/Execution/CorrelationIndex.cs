using OrcaCore.Abstractions.Ids;

namespace OrcaCore.Engine.Ephemeral.Execution;

/// <summary>
/// Engine-owned multi-map over active waits (EV-011): <c>(EventName, CorrelationId) -&gt;
/// set of InstanceId</c>. Wait registration always succeeds — this index has no
/// registration-time uniqueness check; uniqueness is enforced only by the caller of
/// <see cref="TryResolveUnique"/> at correlation-targeted routing time (EV-012). Kept
/// internal and derived entirely from runtime state (never a source of truth on its own);
/// never exposed publicly (CR-021).
/// </summary>
internal sealed class CorrelationIndex
{
    private readonly Dictionary<(string EventName, CorrelationId CorrelationId), HashSet<InstanceId>> byKey = [];
    private readonly Dictionary<InstanceId, (string EventName, CorrelationId CorrelationId)> keyByInstance = [];

    /// <summary>
    /// Records that <paramref name="instanceId"/> now has an active wait on
    /// <paramref name="eventName"/>/<paramref name="correlationId"/>, replacing any prior
    /// entry for that instance (an instance has at most one resident wait — EV-021).
    /// </summary>
    public void Register(InstanceId instanceId, string eventName, CorrelationId correlationId)
    {
        Remove(instanceId);

        var key = (eventName, correlationId);
        keyByInstance[instanceId] = key;

        if (!byKey.TryGetValue(key, out var instanceIds))
        {
            instanceIds = [];
            byKey[key] = instanceIds;
        }

        instanceIds.Add(instanceId);
    }

    /// <summary>
    /// Removes any active-wait entry for <paramref name="instanceId"/> — called on match,
    /// cancellation, or the instance reaching a terminal status.
    /// </summary>
    public void Remove(InstanceId instanceId)
    {
        if (!keyByInstance.Remove(instanceId, out var key))
        {
            return;
        }

        if (byKey.TryGetValue(key, out var instanceIds))
        {
            instanceIds.Remove(instanceId);
            if (instanceIds.Count == 0)
            {
                byKey.Remove(key);
            }
        }
    }

    /// <summary>
    /// Resolves the instances currently registered with an active wait on
    /// <paramref name="eventName"/>/<paramref name="correlationId"/>. Zero or more than one
    /// match is not an error at this layer — EV-012 uniqueness is the caller's concern.
    /// </summary>
    public IReadOnlyCollection<InstanceId> Resolve(string eventName, CorrelationId correlationId) =>
        byKey.TryGetValue((eventName, correlationId), out var instanceIds)
            ? instanceIds
            : [];
}
