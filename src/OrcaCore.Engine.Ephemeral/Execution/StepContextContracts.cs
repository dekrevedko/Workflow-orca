namespace OrcaCore.Engine.Ephemeral.Execution;

internal static class StepContextContracts
{
    internal static StepExecutionContext CreateExecution(
        InstanceId instanceId,
        StepOperationId operationId,
        int attemptNumber) =>
        new(instanceId, operationId, attemptNumber);

    internal static StepContext<TState> Create<TState>(
        TState state,
        StepExecutionContext execution,
        EventEnvelope? resumedEvent,
        TimeProvider timeProvider,
        ForEachItemContext? forEachItem = null,
        ResourceLeaseExecutionContext? resourceLease = null) =>
        new(state, execution, resumedEvent, timeProvider, forEachItem, resourceLease);
}
