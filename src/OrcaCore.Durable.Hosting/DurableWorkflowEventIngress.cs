using OrcaCore.Abstractions.Providers;
using OrcaCore.Engine.Durable;
using OrcaCore.Engine.Durable.Execution;

namespace OrcaCore.Durable.Hosting;

internal sealed class DurableWorkflowEventIngress(
    DurableWorkflowRuntime runtime,
    IWorkflowProjectionStore projectionStore,
    IWorkflowInboxStore inboxStore,
    bool driveAfterAcceptance = true) : IWorkflowEventIngress
{
    private readonly DurableWorkflowEventIngressCore core = new(
        runtime,
        projectionStore,
        inboxStore,
        driveAfterAcceptance);

    public ValueTask<WorkflowEventAcceptanceResult> AcceptAsync(
        WorkflowInboundEvent inboundEvent,
        CancellationToken cancellationToken = default) =>
        core.AcceptAsync(inboundEvent, cancellationToken);

    public ValueTask<WorkflowEventAcceptanceResult> AcceptAsync<TPayload>(
        WorkflowInboundEvent<TPayload> inboundEvent,
        CancellationToken cancellationToken = default) =>
        core.AcceptAsync(inboundEvent, cancellationToken);
}
