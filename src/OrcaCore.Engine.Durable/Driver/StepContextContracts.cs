namespace OrcaCore.Engine.Durable.Driver;

internal static class StepContextContracts
{
    internal static ResourceLeaseExecutionContext CreateLease(LeaseProtectionToken protectionToken)
    {
        ArgumentNullException.ThrowIfNull(protectionToken);
        return new ResourceLeaseExecutionContext(protectionToken);
    }

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

    internal static EventEnvelope CreateResumedEvent(
        EventId eventId,
        WorkflowEventContract eventContract,
        CorrelationId correlationId,
        DateTimeOffset occurredAt,
        ReadOnlyMemory<byte> payload) =>
        new(eventId, eventContract, correlationId, occurredAt, payload);
}
