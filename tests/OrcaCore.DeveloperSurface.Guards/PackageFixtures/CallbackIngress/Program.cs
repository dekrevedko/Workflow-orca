using OrcaCore;

static async ValueTask ExerciseIngressAsync(
    IWorkflowEventClient client,
    InstanceId instanceId,
    DefinitionId definitionId,
    CorrelationId correlationId,
    CancellationToken token)
{
    var eventId = EventId.Create("delivery-1");
    var payloadless = WorkflowEvent.Create(eventId, EventName.Create("ready"), correlationId, DateTimeOffset.UtcNow);
    var payload = WorkflowEvent<Payload>.Create(
        EventId.Create("delivery-2"), EventName.Create("ready"), correlationId, new Payload("ok"), DateTimeOffset.UtcNow);
    _ = await client.DeliverToInstanceAsync(instanceId, payloadless, token);
    _ = await client.DeliverToInstanceAsync(instanceId, payload, token);
    _ = await client.DeliverByCorrelationAsync(definitionId, payloadless, token);
    _ = await client.DeliverByCorrelationAsync(definitionId, payload, token);
    var noWait = await client.DeliverToInstanceAsync(instanceId, payloadless, token);
    if (noWait.Status == EventDeliveryStatus.NoActiveWait)
        _ = await client.DeliverToInstanceAsync(instanceId, payloadless, token);
}

_ = (Func<IWorkflowEventClient, InstanceId, DefinitionId, CorrelationId, CancellationToken, ValueTask>)
    ExerciseIngressAsync;

internal sealed record Payload(string Value);
