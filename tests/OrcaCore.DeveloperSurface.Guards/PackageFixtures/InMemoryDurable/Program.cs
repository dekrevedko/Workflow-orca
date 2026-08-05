using OrcaCore;
using OrcaCore.Hosting;

var fixedDefinitionId = DefinitionId.Parse("4c6df7ac-2d9c-4df3-940b-36995c6ae788");
var ready = WorkflowEventContract.Create(EventName.Create("ready"), EventContractVersion.Initial);
var definition = Workflow.Durable<State>(fixedDefinitionId, DefinitionVersion.Initial)
    .Init<Input>(input => new State(input.Correlation))
    .Wait(ready, state => state.Value.Correlation)
    .End()
    .Build();

static void Configure(
    OrcaCoreDurableEngineBuilder catalog,
    IWorkflowDefinitionRegistry registry,
    DurableWorkflowDefinition<Input> definition)
{
    catalog.AddWorkflow(definition);
    DurableWorkflowRef<Input> reference = definition.Reference;
    _ = registry.GetRequiredHandle(reference);
}

Configure(default!, default!, definition);
_ = typeof(OrcaCore.Providers.InMemory.OrcaCoreInMemoryProviderServiceCollectionExtensions);

internal sealed record Input(CorrelationId Correlation);
internal sealed record State(CorrelationId Correlation);
