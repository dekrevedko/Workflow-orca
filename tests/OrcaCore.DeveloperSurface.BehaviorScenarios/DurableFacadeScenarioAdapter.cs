using OrcaCore.Abstractions.Providers;
using OrcaCore.Engine.Durable;
using OrcaCore.Engine.Durable.Execution;
using OrcaCore.Engine.Durable.Management;

namespace OrcaCore.DeveloperSurface.BehaviorScenarios;

internal static class DurableFacadeScenarioAdapter
{
    internal static DurableDefinitionHandle<TInput> Register<TInput>(
        DurableWorkflowRuntime runtime,
        IWorkflowProjectionStore projectionStore,
        IWorkflowEventStore eventStore,
        DurableManagement management,
        TimeProvider timeProvider,
        DurableWorkflowDefinition<TInput> definition,
        IEnumerable<ResourcePoolName>? configuredResourcePools = null)
    {
        var registry = new DurableWorkflowDefinitionRegistry(
            runtime,
            projectionStore,
            eventStore,
            management,
            new DurableFacadeNotificationHub(),
            timeProvider,
            configuredResourcePools);
        return registry.Register(definition).GetHandleOrThrow();
    }
}
