using OrcaCore;

namespace OrcaCore.Durable.Hosting;

/// <summary>Durably accepts one self-routing application event.</summary>
public interface IWorkflowEventIngress
{
    /// <summary>Accepts one payloadless inbound event.</summary>
    ValueTask<WorkflowEventAcceptanceResult> AcceptAsync(
        WorkflowInboundEvent inboundEvent,
        CancellationToken cancellationToken = default);

    /// <summary>Accepts one typed inbound event.</summary>
    ValueTask<WorkflowEventAcceptanceResult> AcceptAsync<TPayload>(
        WorkflowInboundEvent<TPayload> inboundEvent,
        CancellationToken cancellationToken = default);
}
