
namespace OrcaCore.Runtime.Routing;

internal static class ResumeRouter
{
    public static async Task ResumeAsync<TState>(
        WorkflowInstance<TState> instance,
        WorkflowDefinition<TState> definition,
        CorrelationIndex correlationIndex,
        EventEnvelope envelope,
        WaitRecord matchedWait,
        CancellationToken cancellationToken)
    {
        if (matchedWait.BranchId is not null)
        {
            await WorkflowRuntime.ResumeParallelBranchAsync(
                instance,
                definition,
                correlationIndex,
                matchedWait.BranchId,
                envelope,
                cancellationToken);
            return;
        }

        await WorkflowRuntime.ExecuteAsync(
            instance,
            definition,
            correlationIndex,
            cancellationToken,
            envelope);
    }
}
