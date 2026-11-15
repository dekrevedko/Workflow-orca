using System.Collections.Concurrent;
using OrcaCore.Abstractions.Durable.Dag;
using OrcaCore.Abstractions.Ids;
using OrcaCore.Engine.Durable.Dag;

namespace OrcaCore.Durable.Hosting.Dag;

// Rebuilt from typed plan registrations per host boot. No storage string can
// nominate a CLR type, and only the existing Engine.Durable seam reads metadata.
internal sealed class DurableDagRunInputCatalog
{
    private readonly ConcurrentDictionary<(DefinitionId, DefinitionVersion), Entry> bindings = new();

    internal string Install(DagRunBindingV1 binding, Type inputType)
    {
        ArgumentNullException.ThrowIfNull(binding);
        ArgumentNullException.ThrowIfNull(inputType);
        var coordinate = (binding.DefinitionId, binding.DefinitionVersion);
        var proposed = new Entry(binding.Fingerprint, inputType,
            FormattableString.Invariant($"dag:{binding.DefinitionId}:{binding.DefinitionVersion.Value}:run-input:") +
            DurableDagInputShape.Digest(inputType));
        var installed = bindings.GetOrAdd(coordinate, proposed);
        if (installed != proposed)
            throw new InvalidOperationException("A different DAG run-input binding is already installed.");
        return installed.Key;
    }

    internal Type? Resolve(DagRunGraphV1 graph)
    {
        if (!bindings.TryGetValue((graph.Definition.DefinitionId, graph.Definition.DefinitionVersion), out var entry))
            return null;
        if (entry.Fingerprint != graph.Definition.Fingerprint || entry.Key != graph.RunInputContractKey)
            throw new InvalidDataException("Persisted DAG run input differs from the installed plan binding.");
        return entry.InputType;
    }

    private sealed record Entry(string Fingerprint, Type InputType, string Key);
}
