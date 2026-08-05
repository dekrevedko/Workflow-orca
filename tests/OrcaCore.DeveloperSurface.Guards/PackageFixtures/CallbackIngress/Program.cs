using OrcaCore;
using OrcaCore.Durable.Hosting;

static async ValueTask ExerciseIngressAsync(
    IWorkflowEventIngress ingress,
    InstanceId instanceId,
    DefinitionId definitionId,
    DefinitionVersion definitionVersion,
    CorrelationId correlationId,
    CancellationToken token)
{
    var payloadlessContract = WorkflowEventContract.Create(
        EventName.Create("ready"), EventContractVersion.Initial);
    var typedContract = WorkflowEventContract<Payload>.Create(
        EventName.Create("ready-with-payload"), EventContractVersion.Initial);
    WorkflowEventRoute[] routes =
    [
        new WorkflowEventRoute.Direct(instanceId),
        new WorkflowEventRoute.Correlation(definitionId),
        new WorkflowEventRoute.DefinitionFanout(definitionId),
        new WorkflowEventRoute.StartOrDeliver<WorkflowInput>(definitionId, definitionVersion,
            StartIdempotencyKey.Create("callback-start"), new WorkflowInput("from-callback"))
    ];
    var payloadless = WorkflowInboundEvent.Create(payloadlessContract, EventId.Create("delivery-1"),
        correlationId, null, DateTimeOffset.UtcNow, routes[0]);
    var typed = WorkflowInboundEvent<Payload>.Create(typedContract, EventId.Create("delivery-2"),
        correlationId, EventId.Create("upstream-1"), DateTimeOffset.UtcNow, routes[3], new Payload("ok"));

    _ = await ingress.AcceptAsync(payloadless, token);
    var result = await ingress.AcceptAsync(typed, token);
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
}

_ = (Func<IWorkflowEventIngress, InstanceId, DefinitionId, DefinitionVersion, CorrelationId,
    CancellationToken, ValueTask>)ExerciseIngressAsync;

internal sealed record WorkflowInput(string Value);
internal sealed record Payload(string Value);
