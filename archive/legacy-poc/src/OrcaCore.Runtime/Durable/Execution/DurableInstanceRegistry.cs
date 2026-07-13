using System.Collections.Concurrent;

namespace OrcaCore.Runtime.Durable.Execution;

internal sealed class DurableInstanceRegistry
{
    private readonly ConcurrentDictionary<string, DurableInstanceRegistration> _registrations = new(StringComparer.Ordinal);

    public IEnumerable<DurableInstanceRegistration> Values => _registrations.Values;

    public bool Contains(string instanceId) => _registrations.ContainsKey(instanceId);

    public bool TryGet(string instanceId, out DurableInstanceRegistration registration) =>
        _registrations.TryGetValue(instanceId, out registration!);

    public void Set(string instanceId, DurableInstanceRegistration registration) =>
        _registrations[instanceId] = registration;

    public bool TryRemove(string instanceId, out DurableInstanceRegistration registration) =>
        _registrations.TryRemove(instanceId, out registration!);

    public DurableInstanceRegistration GetRequired(string instanceId) =>
        _registrations.TryGetValue(instanceId, out var registration)
            ? registration
            : throw new KeyNotFoundException($"Durable instance '{instanceId}' is not loaded.");

    public IReadOnlyList<DurableInstanceRegistration> Drain()
    {
        var drained = new List<DurableInstanceRegistration>();
        foreach (var entry in _registrations.ToArray())
        {
            if (_registrations.TryRemove(entry.Key, out var registration))
                drained.Add(registration);
        }

        return drained;
    }
}
