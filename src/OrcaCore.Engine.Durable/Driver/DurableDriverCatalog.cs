using OrcaCore.Abstractions.Errors;
using OrcaCore.Abstractions.Ids;
using OrcaCore.Core.Compilation;
using OrcaCore.Core.Definitions;
using OrcaCore.Engine.Durable.Definitions;

namespace OrcaCore.Engine.Durable.Driver;

/// <summary>
/// Keeps one driver executor per registered definition version and enforces durable
/// capability at registration time (DR-010: unsupported shapes fail fast, never mid-flight).
/// </summary>
internal sealed class DurableDriverCatalog
{
    private readonly DurableDefinitionRegistry definitions;

    internal DurableDriverCatalog(DurableDefinitionRegistry definitions)
    {
        ArgumentNullException.ThrowIfNull(definitions);
        this.definitions = definitions;
    }

    internal void Register<TState>(WorkflowDefinition<TState> definition)
    {
        definitions.Register(definition);
    }

    internal IDurableDriverExecutor? Resolve(DefinitionId definitionId, DefinitionVersion definitionVersion)
    {
        return definitions.ResolveExecutor(definitionId, definitionVersion);
    }

    internal static IDurableDriverExecutor CreateExecutor<TState>(WorkflowDefinition<TState> definition)
    {
        ArgumentNullException.ThrowIfNull(definition);

        if (definition.CompiledPlan.Instructions.Count == 0)
        {
            throw new WorkflowDefinitionException(
                $"Definition '{definition.DefinitionId}' version '{definition.DefinitionVersion}' has no " +
                "compiled plan. Durable registration requires Workflow.Durable<TState>(...).Build().");
        }

        if (definition.CompiledPlan.Mode != WorkflowExecutionMode.Durable)
        {
            throw new WorkflowDefinitionException(
                $"Definition '{definition.DefinitionId}' version '{definition.DefinitionVersion}' was compiled " +
                $"for '{definition.CompiledPlan.Mode}', not durable execution.");
        }

        return new DurableFiberDriverExecutor<TState>(definition);
    }
}
