using System.Reflection;
using OrcaCore.Abstractions.Providers;
using OrcaCore.Engine.Durable.Outbox;
using OrcaCore.Durable.Hosting;

namespace OrcaCore.Hosting.Services;

internal sealed class WorkflowEventOutboxPump
{
    private readonly DurableOutboxPump pump;

    internal WorkflowEventOutboxPump(
        IWorkflowOutboxStore outboxStore,
        IWorkflowEventDispatcher dispatcher,
        IOutboxPumpObserver? observer,
        TimeProvider timeProvider)
    {
        pump = new DurableOutboxPump(
            outboxStore,
            new WorkflowEventMessageDispatcher(dispatcher),
            observer,
            timeProvider);
    }

    internal Task<int> PumpOnceAsync(OutboxClaimRequest request, CancellationToken cancellationToken) =>
        pump.PumpOnceAsync(
            request with { KindSelector = OutboxKindSelector.Including(OutboxKinds.WorkflowEvent) },
            cancellationToken);
}

internal sealed class WorkflowEventMessageDispatcher(IWorkflowEventDispatcher dispatcher) : IMessageDispatcher
{
    private static readonly ConstructorInfo OutboundEventConstructor = typeof(WorkflowOutboundEvent)
        .GetConstructor(
            BindingFlags.Instance | BindingFlags.NonPublic,
            binder: null,
            [
                typeof(WorkflowEventContract),
                typeof(EventId),
                typeof(CorrelationId),
                typeof(EventId),
                typeof(DateTimeOffset),
                typeof(InstanceId),
                typeof(DefinitionId),
                typeof(DefinitionVersion),
                typeof(ReadOnlyMemory<byte>)
            ],
            modifiers: null) ??
        throw new InvalidOperationException("The approved WorkflowOutboundEvent constructor was not found.");

    public async Task<DispatchResult> DispatchAsync(
        OutboxWrite record,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(record);
        if (!string.Equals(record.Kind, OutboxKinds.WorkflowEvent, StringComparison.Ordinal))
        {
            throw new InvalidOperationException("Only workflow-event outbox records may reach the application dispatcher.");
        }

        var data = DurableWorkflowOutboundEventCodec.Decode(record.Payload);
        var eventContract = CreateEventContract(data);
        var outboundEvent = (WorkflowOutboundEvent)OutboundEventConstructor.Invoke(
        [
            eventContract,
            EventId.Create(data.EventId),
            CorrelationId.Create(data.CorrelationId),
            data.CausationEventId is null ? null : EventId.Create(data.CausationEventId),
            data.OccurredAt,
            InstanceId.Parse(data.OriginInstanceId),
            DefinitionId.Parse(data.OriginDefinitionId),
            new DefinitionVersion(data.OriginDefinitionVersion),
            new ReadOnlyMemory<byte>(data.Payload)
        ]);
        var result = await dispatcher.DispatchAsync(outboundEvent, cancellationToken).ConfigureAwait(false) ??
            throw new InvalidOperationException("The workflow-event dispatcher returned no result.");
        return result switch
        {
            WorkflowEventDispatchResult.Succeeded => DispatchResult.Success,
            WorkflowEventDispatchResult.RetryableFailure => DispatchResult.RetryableFailure,
            WorkflowEventDispatchResult.PermanentFailure => DispatchResult.PermanentFailure,
            _ => throw new InvalidOperationException("The workflow-event dispatcher returned an unknown result.")
        };
    }

    private static WorkflowEventContract CreateEventContract(DurableWorkflowOutboundEventData data)
    {
        var eventName = EventName.Create(data.EventName);
        var version = new EventContractVersion(data.EventContractVersion);
        if (data.PayloadTypeName is null)
        {
            return WorkflowEventContract.Create(eventName, version);
        }

        var payloadType = Type.GetType(data.PayloadTypeName, throwOnError: false) ??
            throw new InvalidOperationException(
                $"The workflow-event payload type '{data.PayloadTypeName}' is unavailable.");
        var descriptorType = typeof(WorkflowEventContract<>).MakeGenericType(payloadType);
        var create = descriptorType.GetMethod(
            nameof(WorkflowEventContract.Create),
            BindingFlags.Public | BindingFlags.Static | BindingFlags.DeclaredOnly,
            binder: null,
            [typeof(EventName), typeof(EventContractVersion)],
            modifiers: null) ??
            throw new InvalidOperationException("The typed workflow-event descriptor factory was not found.");
        return (WorkflowEventContract)(create.Invoke(null, [eventName, version]) ??
            throw new InvalidOperationException("The typed workflow-event descriptor factory returned no descriptor."));
    }
}
