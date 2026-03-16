using OrcaCore.Abstractions;

namespace OrcaCore.Runtime;

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
                instance, definition, correlationIndex, matchedWait.BranchId, envelope, cancellationToken);
            return;
        }

        var currentStep = definition.Steps[instance.RuntimeState.ExecutionPointer];
        if (currentStep is WaitStep<TState>)
        {
            // Top-level wait: advance past it and continue
            instance.RuntimeState.ExecutionPointer++;
            await WorkflowRuntime.ExecuteAsync(instance, definition, correlationIndex, cancellationToken, envelope);
        }
        else
        {
            // Nested wait (inside While/If): buffer event so HandleWait finds it,
            // then re-enter at the saved NestedPointers position
            instance.RuntimeState.PendingEvents.Add(
                new PendingEvent(envelope, DateTimeOffset.UtcNow, Consumed: false));
            await WorkflowRuntime.ExecuteAsync(instance, definition, correlationIndex, cancellationToken);
        }
    }
}
