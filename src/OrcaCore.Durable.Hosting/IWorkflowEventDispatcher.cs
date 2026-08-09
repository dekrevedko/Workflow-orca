namespace OrcaCore.Durable.Hosting;

/// <summary>Dispatches complete application-shaped outbound workflow events.</summary>
public interface IWorkflowEventDispatcher
{
    ValueTask<WorkflowEventDispatchResult> DispatchAsync(
        WorkflowOutboundEvent outboundEvent,
        CancellationToken cancellationToken = default);
}
