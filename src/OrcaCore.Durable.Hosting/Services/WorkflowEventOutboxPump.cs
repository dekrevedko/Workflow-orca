using OrcaCore.Abstractions.Providers;
using OrcaCore.Engine.Durable.Internal;
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

internal sealed class WorkflowEventMessageDispatcher(IWorkflowEventDispatcher dispatcher)
    : IMessageDispatcher, IDetailedOutboxMessageDispatcher
{
    public async Task<DispatchResult> DispatchAsync(
        OutboxWrite record,
        CancellationToken cancellationToken) =>
        (await DispatchDetailedAsync(record, cancellationToken).ConfigureAwait(false)).Result;

    public async Task<OutboxDispatchOutcome> DispatchDetailedAsync(
        OutboxWrite record,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(record);
        if (!string.Equals(record.Kind, OutboxKinds.WorkflowEvent, StringComparison.Ordinal))
        {
            throw new InvalidOperationException("Only workflow-event outbox records may reach the application dispatcher.");
        }

        var data = DurableWorkflowOutboundEventCodec.Decode(record.Payload);
        var outboundEvent = DurableApplicationContractFactory.WorkflowOutboundEvent(data);
        var result = await dispatcher.DispatchAsync(outboundEvent, cancellationToken).ConfigureAwait(false) ??
            throw new InvalidOperationException("The workflow-event dispatcher returned no result.");
        return result switch
        {
            WorkflowEventDispatchResult.Succeeded => new OutboxDispatchOutcome(DispatchResult.Success),
            WorkflowEventDispatchResult.RetryableFailure retryable => new OutboxDispatchOutcome(
                DispatchResult.RetryableFailure,
                retryable.Failure.Code,
                retryable.Failure.Detail),
            WorkflowEventDispatchResult.PermanentFailure permanent => new OutboxDispatchOutcome(
                DispatchResult.PermanentFailure,
                permanent.Failure.Code,
                permanent.Failure.Detail),
            _ => throw new InvalidOperationException("The workflow-event dispatcher returned an unknown result.")
        };
    }
}
