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
    private readonly ConcurrentDictionary<(Guid Run, Type Type, string Key, string Fingerprint), bool> verified = new();

    internal bool Matches(DagRunSnapshotV1 run, string installedKey, Type inputType)
    {
        var bytes = DurableDagValues.VerifiedRunInput(run.RunInput, run.Graph.RunInputContractKey);
        if (run.Graph.RunInputContractKey != installedKey) return false;
        return verified.GetOrAdd((run.RunId, inputType, installedKey, run.RunInput.Fingerprint),
            _ => DurableDagValues.RoundTrips(bytes, inputType));
    }

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

    // Report the existing registration instead of throwing, so the facade can
    // return a closed conflict. Local sameness is CLR Type identity plus key.
    internal (string? Key, string? ExistingFingerprint, Type? ExistingType) TryInstall(DagRunBindingV1 binding, Type inputType)
    {
        var coordinate = (binding.DefinitionId, binding.DefinitionVersion);
        var proposed = new Entry(binding.Fingerprint, inputType,
            FormattableString.Invariant($"dag:{binding.DefinitionId}:{binding.DefinitionVersion.Value}:run-input:") +
            DurableDagInputShape.Digest(inputType));
        var installed = bindings.GetOrAdd(coordinate, proposed);
        return installed == proposed ? (installed.Key, null, null) : (null, installed.Fingerprint, installed.InputType);
    }

    internal Type? Resolve(DagRunGraphV1 graph)
    {
        if (!bindings.TryGetValue((graph.Definition.DefinitionId, graph.Definition.DefinitionVersion), out var entry))
            return null;
        if (entry.Fingerprint != graph.Definition.Fingerprint || entry.Key != graph.RunInputContractKey)
            return null;
        return entry.InputType;
    }

    private sealed record Entry(string Fingerprint, Type InputType, string Key);
}
