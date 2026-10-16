using OrcaCore;
using OrcaCore.Durable.Hosting;
using OrcaCore.Hosting;

var fixedDefinitionId = DefinitionId.Parse("44f38e8a-7a68-43dc-a184-5a0d555f1b20");
var payloadlessContract = WorkflowEventContract.Create(
    EventName.Create("approval-requested"), EventContractVersion.Initial);
var typedContract = WorkflowEventContract<Approval>.Create(
    EventName.Create("approval-decided"), new EventContractVersion(2));

var ephemeralDefinition = Workflow.Ephemeral<State>(fixedDefinitionId, DefinitionVersion.Initial)
    .Init<Input>(input => new State(input.Correlation))
    .Wait(payloadlessContract, state => state.Value.Correlation)
    .Wait(typedContract, state => state.Value.Correlation, TimeSpan.FromMinutes(5))
    .End()
    .Build();
var durableDefinition = Workflow.Durable<State>(fixedDefinitionId, DefinitionVersion.Initial)
    .Init<Input>(input => new State(input.Correlation))
    .Wait(payloadlessContract, state => state.Value.Correlation)
    .Wait(typedContract, state => state.Value.Correlation, TimeSpan.FromMinutes(5))
    .Publish(payloadlessContract, state => state.Value.Correlation)
    .Publish(typedContract, state => state.Value.Correlation, state => new Approval(true))
    .End()
    .Build();

static async ValueTask ExerciseBoundaryAsync(
    IWorkflowEventIngress ingress,
    IWorkflowEventDispatcher dispatcher,
    IWorkflowDefinitionRegistry registry,
    OrcaCoreEphemeralEngineBuilder ephemeralCatalog,
    OrcaCoreDurableEngineBuilder durableCatalog,
    EphemeralWorkflowDefinition<Input> ephemeralDefinition,
    DurableWorkflowDefinition<Input> durableDefinition,
    WorkflowOutboundEvent outboundEvent,
    CancellationToken token)
{
    var direct = new WorkflowEventRoute.Direct(
        InstanceId.Parse("42189848-dd2a-48c2-af6f-b09458c5e164"));
    var correlation = new WorkflowEventRoute.Correlation(durableDefinition.DefinitionId);
    var fanout = new WorkflowEventRoute.DefinitionFanout(durableDefinition.DefinitionId);
    var start = new WorkflowEventRoute.StartOrDeliver<Input>(durableDefinition.DefinitionId,
        durableDefinition.DefinitionVersion, StartIdempotencyKey.Create("start-or-deliver"),
        new Input(CorrelationId.Create("catalog")));
    var descriptor = WorkflowEventContract.Create(EventName.Create("catalog-ready"), EventContractVersion.Initial);
    var typedDescriptor = WorkflowEventContract<Approval>.Create(
        EventName.Create("catalog-approved"), EventContractVersion.Initial);
    var inbound = WorkflowInboundEvent.Create(descriptor, EventId.Create("payloadless"),
        CorrelationId.Create("catalog"), null, DateTimeOffset.UtcNow, direct);
    var typedInbound = WorkflowInboundEvent<Approval>.Create(typedDescriptor, EventId.Create("typed"),
        CorrelationId.Create("catalog"), null, DateTimeOffset.UtcNow, start, new Approval(true));

    _ = await ingress.AcceptAsync(inbound, token);
    WorkflowEventAcceptanceResult result = await ingress.AcceptAsync(typedInbound, token);
    _ = result switch
    {
        WorkflowEventAcceptanceResult.Accepted => true,
        WorkflowEventAcceptanceResult.Duplicate => true,
        WorkflowEventAcceptanceResult.Rejected(var reason) => reason switch
        {
            WorkflowEventAcceptanceRejection.EventConflict => false,
            WorkflowEventAcceptanceRejection.DirectInstanceNotFound => false,
            WorkflowEventAcceptanceRejection.DirectInstanceTerminal => false,
            WorkflowEventAcceptanceRejection.StartConflict => false,
            WorkflowEventAcceptanceRejection.FanoutLimitExceeded => false,
            _ => false
        },
        _ => false
    };
    _ = correlation;
    _ = fanout;
    _ = outboundEvent.GetPayload(typedDescriptor);
    _ = await dispatcher.DispatchAsync(outboundEvent, token);

    EphemeralWorkflowRef<Input> ephemeralReference = ephemeralDefinition.Reference;
    DurableWorkflowRef<Input> durableReference = durableDefinition.Reference;
    _ = registry.GetRequiredHandle(ephemeralReference);
    _ = registry.GetRequiredHandle(durableReference);
    ephemeralCatalog.AddWorkflow(ephemeralDefinition);
    durableCatalog.AddWorkflow(durableDefinition);
}

_ = ephemeralDefinition.Reference;
_ = durableDefinition.Reference;
_ = (Func<IWorkflowEventIngress, IWorkflowEventDispatcher, IWorkflowDefinitionRegistry,
    OrcaCoreEphemeralEngineBuilder, OrcaCoreDurableEngineBuilder,
    EphemeralWorkflowDefinition<Input>, DurableWorkflowDefinition<Input>, WorkflowOutboundEvent,
    CancellationToken, ValueTask>)ExerciseBoundaryAsync;

internal sealed record Input(CorrelationId Correlation);
internal sealed record State(CorrelationId Correlation);
internal sealed record Approval(bool Approved);
