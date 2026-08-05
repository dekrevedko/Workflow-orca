using OrcaCore;

var fixedDefinitionId = DefinitionId.Parse("b5d94580-4dd8-41f1-b485-033facdf82d8");
var payloadless = WorkflowEventContract.Create(EventName.Create("ready"), EventContractVersion.Initial);
var typed = WorkflowEventContract<Payload>.Create(EventName.Create("completed"), new EventContractVersion(2));

_ = fixedDefinitionId;
_ = payloadless;
_ = typed;
_ = typeof(WorkflowEventRoute);
_ = typeof(WorkflowInboundEvent);
_ = typeof(WorkflowInboundEvent<>);
_ = typeof(WorkflowEventAcceptanceResult);
_ = typeof(WorkflowEventAcceptanceRejection);
_ = typeof(WorkflowOutboundEvent);
_ = typeof(EphemeralWorkflowRef<>);
_ = typeof(EphemeralWorkflowRef<,>);
_ = typeof(DurableWorkflowRef<>);
_ = typeof(DurableWorkflowRef<,>);

internal sealed record Payload(string Value);
