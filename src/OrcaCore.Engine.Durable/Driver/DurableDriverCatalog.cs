using System.Collections.Concurrent;
using OrcaCore.Abstractions.Errors;
using OrcaCore.Abstractions.Ids;
using OrcaCore.Core.Definitions;
using OrcaCore.Engine.Durable.Definitions;

namespace OrcaCore.Engine.Durable.Driver;

/// <summary>
/// Keeps one driver executor per registered definition version and enforces durable
/// capability at registration time (DR-010: unsupported shapes fail fast, never mid-flight).
/// </summary>
internal sealed class DurableDriverCatalog
{
    private readonly ConcurrentDictionary<DurableDefinitionKey, IDurableDriverExecutor> executors = [];

    internal void Register<TState>(WorkflowDefinition<TState> definition)
    {
        ArgumentNullException.ThrowIfNull(definition);

        EnsureDurableSupported(definition.RootSequence, definition.DefinitionId, definition.DefinitionVersion);
        executors[new DurableDefinitionKey(definition.DefinitionId, definition.DefinitionVersion)] =
            new DurableDriverExecutor<TState>(definition);
    }

    internal IDurableDriverExecutor? Resolve(DefinitionId definitionId, DefinitionVersion definitionVersion)
    {
        return executors.TryGetValue(new DurableDefinitionKey(definitionId, definitionVersion), out var executor)
            ? executor
            : null;
    }

    private static void EnsureDurableSupported<TState>(
        SequenceNode<TState> sequence,
        DefinitionId definitionId,
        DefinitionVersion definitionVersion)
    {
        foreach (var node in sequence.Children)
        {
            switch (node)
            {
                case ForEachNode<TState> forEach:
                    throw new WorkflowDefinitionException(
                        $"Definition '{definitionId}' version '{definitionVersion}' uses lightweight ForEach " +
                        $"at '{forEach.NodeId}', which is an ephemeral-only in-instance fanout primitive " +
                        "(CP-010..013). The durable driver expresses fanout through RunChild/RunChildren; " +
                        "durable ForEach is not specified (DR-010).");
                case IfNode<TState> ifNode:
                    EnsureDurableSupported(ifNode.Then, definitionId, definitionVersion);
                    EnsureDurableSupported(ifNode.Else, definitionId, definitionVersion);
                    break;
                case WhileNode<TState> whileNode:
                    EnsureDurableSupported(whileNode.Body, definitionId, definitionVersion);
                    break;
                case ParallelNode<TState> parallelNode:
                    foreach (var branch in parallelNode.Branches)
                    {
                        EnsureDurableSupported(branch.Sequence, definitionId, definitionVersion);
                    }

                    break;
                case WhenFirstNode<TState> whenFirstNode:
                    foreach (var branch in whenFirstNode.Branches)
                    {
                        EnsureDurableSupported(branch.Sequence, definitionId, definitionVersion);
                    }

                    break;
            }
        }
    }
}
