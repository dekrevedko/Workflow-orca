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

    internal IDurableDriverExecutor? Resolve(
        DefinitionId definitionId,
        DefinitionVersion definitionVersion,
        string? planFingerprint = null)
    {
        return definitions.ResolveExecutor(definitionId, definitionVersion, planFingerprint);
    }

    internal static IDurableDriverExecutor CreateExecutor<TState>(
        WorkflowDefinition<TState> definition,
        IServiceProvider? serviceProvider,
        int maxConcurrentExecutionPathsPerInstance,
        DurableStepThrottleCoordinator stepThrottles)
    {
        ArgumentNullException.ThrowIfNull(definition);

        var plan = (CompiledWorkflowPlan)WorkflowDefinitionRuntime.GetPlan(definition);
        if (plan.Instructions.Count == 0)
        {
            throw global::OrcaCore.Engine.Durable.Internal.DurableContractAdapter.DefinitionException(
                $"Definition '{definition.DefinitionId}' version '{definition.DefinitionVersion}' has no " +
                "compiled plan. Durable registration requires OrcaCore.Workflow.Durable<TState>(...).Init<TInput>(...).End().Build().");
        }

        if (plan.Mode != WorkflowExecutionMode.Durable)
        {
            throw global::OrcaCore.Engine.Durable.Internal.DurableContractAdapter.DefinitionException(
                $"Definition '{definition.DefinitionId}' version '{definition.DefinitionVersion}' was compiled " +
                $"for '{plan.Mode}', not durable execution.");
        }

        return new DurableFiberDriverExecutor<TState>(
            definition,
            serviceProvider,
            maxConcurrentExecutionPathsPerInstance,
            stepThrottles);
    }
}
