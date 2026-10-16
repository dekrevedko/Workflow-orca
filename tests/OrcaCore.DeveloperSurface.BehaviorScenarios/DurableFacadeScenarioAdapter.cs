namespace OrcaCore.DeveloperSurface.BehaviorScenarios;

internal static class DurableFacadeScenarioAdapter
{
    internal static DurableDefinitionHandle<TInput> Register<TInput>(
        DurableScenarioRuntime runtime,
        DurableWorkflowDefinition<TInput> definition) =>
        runtime.Register(definition);
}
