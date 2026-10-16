using System.Collections.Concurrent;
using OrcaCore.Runtime.Durable.Persistence;

namespace OrcaCore.Runtime.Durable.Definitions;

internal sealed class DurableDefinitionRegistry(IWorkflowStore store)
{
    private readonly ConcurrentDictionary<string, DurableRegisteredDefinition> _definitions = new(StringComparer.Ordinal);
    private readonly ConcurrentDictionary<string, string> _definitionVersions = new(StringComparer.Ordinal);

    public async Task<IReadOnlyList<PersistedInstance>> RegisterAsync<TState>(
        DurableWorkflowDefinition<TState> definition,
        CancellationToken cancellationToken,
        Func<PersistedInstance, IWorkflowInstance> load)
    {
        var key = GetDefinitionKey(definition.DefinitionId, definition.DefinitionVersion);

        if (_definitions.TryGetValue(key, out var existingDefinition))
        {
            if (existingDefinition.StateType != typeof(TState))
            {
                throw new DefinitionAlreadyRegisteredException(
                    definition.DefinitionId,
                    definition.DefinitionVersion,
                    definition.DefinitionVersion,
                    $"Definition '{definition.DefinitionId}' version '{definition.DefinitionVersion}' is already registered with a different state type.");
            }

            return [];
        }

        if (_definitionVersions.TryGetValue(definition.DefinitionId, out var registeredVersion)
            && !string.Equals(registeredVersion, definition.DefinitionVersion, StringComparison.Ordinal))
        {
            throw new DefinitionAlreadyRegisteredException(
                definition.DefinitionId,
                definition.DefinitionVersion,
                registeredVersion,
                $"Definition '{definition.DefinitionId}' is already registered with version '{registeredVersion}', cannot register version '{definition.DefinitionVersion}'.");
        }

        var mismatched = await store.QueryAsync(
            status: WorkflowStatus.Waiting,
            definitionId: definition.DefinitionId,
            ct: cancellationToken);

        var mismatch = mismatched.FirstOrDefault(x =>
            !string.Equals(x.DefinitionVersion, definition.DefinitionVersion, StringComparison.Ordinal));

        if (mismatch is not null)
        {
            throw new DefinitionVersionMismatchException(
                definition.DefinitionId,
                definition.DefinitionVersion,
                mismatch.DefinitionVersion ?? "<missing>",
                mismatch.InstanceId);
        }

        _definitions[key] = new DurableRegisteredDefinition(
            definition.DefinitionId,
            definition.DefinitionVersion,
            typeof(TState),
            load);
        _definitionVersions[definition.DefinitionId] = definition.DefinitionVersion;

        var waiting = await store.QueryAsync(
            status: WorkflowStatus.Waiting,
            definitionId: definition.DefinitionId,
            definitionVersion: definition.DefinitionVersion,
            ct: cancellationToken);

        return waiting
            .Where(persisted => persisted.RuntimeState.ActiveWaits.Any(wait =>
                wait.Status == WaitStatus.Active && wait.Mode == WaitMode.Resident))
            .ToArray();
    }

    public bool TryResolve(PersistedInstance persisted, out DurableRegisteredDefinition definition)
    {
        var version = persisted.DefinitionVersion ?? throw new InvalidOperationException(
            $"Persisted durable instance '{persisted.InstanceId}' is missing DefinitionVersion.");

        return _definitions.TryGetValue(GetDefinitionKey(persisted.DefinitionId, version), out definition!);
    }

    public DurableRegisteredDefinition GetRequired(PersistedInstance persisted)
    {
        if (TryResolve(persisted, out var definition))
            return definition;

        if (_definitionVersions.TryGetValue(persisted.DefinitionId, out var expectedVersion))
        {
            throw new DefinitionVersionMismatchException(
                persisted.DefinitionId,
                expectedVersion,
                persisted.DefinitionVersion ?? "<missing>",
                persisted.InstanceId);
        }

        throw new InvalidOperationException(
            $"Durable definition '{persisted.DefinitionId}' version '{persisted.DefinitionVersion}' is not registered.");
    }

    private static string GetDefinitionKey(string definitionId, string definitionVersion) =>
        $"{definitionId}::{definitionVersion}";
}
