using System.Collections.Concurrent;
using OrcaCore.Abstractions.Errors;
using OrcaCore.Abstractions.Ids;
using OrcaCore.Abstractions.Instances;
using OrcaCore.Core.Definitions;

namespace OrcaCore.Engine.Durable.Definitions;

/// <summary>
/// Keeps the workflow definition versions a durable host can resume.
/// </summary>
public sealed class DurableDefinitionRegistry
{
    private readonly ConcurrentDictionary<DurableDefinitionKey, RegisteredDefinition> definitions = [];

    /// <summary>
    /// Registers one immutable workflow definition version.
    /// </summary>
    public void Register<TState>(WorkflowDefinition<TState> definition)
    {
        ArgumentNullException.ThrowIfNull(definition);

        var key = new DurableDefinitionKey(definition.DefinitionId, definition.DefinitionVersion);
        var registered = new RegisteredDefinition(definition, typeof(TState));
        definitions.AddOrUpdate(
            key,
            registered,
            (_, existing) => SameRegistration(existing, registered)
                ? existing
                : throw new WorkflowDefinitionException(
                    $"Workflow definition '{key.DefinitionId}' version '{key.DefinitionVersion}' " +
                    $"is already registered for state type '{existing.StateType.FullName}'."));
    }

    /// <summary>
    /// Resolves the definition version recorded on a durable instance snapshot.
    /// </summary>
    public WorkflowDefinition<TState> ResolveBound<TState>(WorkflowInstanceSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);

        return Resolve<TState>(snapshot.DefinitionId, snapshot.DefinitionVersion);
    }

    /// <summary>
    /// Resolves a registered workflow definition version.
    /// </summary>
    public WorkflowDefinition<TState> Resolve<TState>(
        DefinitionId definitionId,
        DefinitionVersion definitionVersion)
    {
        var key = new DurableDefinitionKey(definitionId, definitionVersion);
        if (!definitions.TryGetValue(key, out var registered))
        {
            throw new WorkflowDefinitionException(
                $"Workflow definition '{definitionId}' version '{definitionVersion}' is not registered.");
        }

        if (registered.Definition is WorkflowDefinition<TState> typed)
        {
            return typed;
        }

        throw new WorkflowDefinitionException(
            $"Workflow definition '{definitionId}' version '{definitionVersion}' is registered for " +
            $"state type '{registered.StateType.FullName}', not '{typeof(TState).FullName}'.");
    }

    /// <summary>
    /// Lists the registered definition versions.
    /// </summary>
    public IReadOnlyList<DurableRegisteredDefinition> List()
    {
        return definitions
            .Select(entry => new DurableRegisteredDefinition(
                entry.Key.DefinitionId,
                entry.Key.DefinitionVersion,
                entry.Value.StateType))
            .OrderBy(definition => definition.DefinitionId.Value)
            .ThenBy(definition => definition.DefinitionVersion.Value)
            .ToArray();
    }

    private static bool SameRegistration(
        RegisteredDefinition existing,
        RegisteredDefinition candidate)
    {
        return existing.StateType == candidate.StateType;
    }

    private sealed record RegisteredDefinition(object Definition, Type StateType);
}

/// <summary>
/// Identifies a durable workflow definition version.
/// </summary>
public readonly record struct DurableDefinitionKey(
    DefinitionId DefinitionId,
    DefinitionVersion DefinitionVersion);

/// <summary>
/// Describes one registered durable workflow definition version.
/// </summary>
public sealed record DurableRegisteredDefinition(
    DefinitionId DefinitionId,
    DefinitionVersion DefinitionVersion,
    Type StateType);
