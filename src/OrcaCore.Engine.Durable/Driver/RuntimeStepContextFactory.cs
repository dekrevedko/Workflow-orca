using System.Reflection;
namespace OrcaCore.Engine.Durable.Driver;

internal static class RuntimeStepContextFactory
{
    internal static ResourceLeaseExecutionContext CreateLease(LeaseProtectionToken protectionToken)
    {
        ArgumentNullException.ThrowIfNull(protectionToken);
        var constructor = typeof(ResourceLeaseExecutionContext).GetConstructors(
                BindingFlags.Instance | BindingFlags.NonPublic)
            .Single(candidate => candidate.GetParameters().Length == 1);
        return (ResourceLeaseExecutionContext)constructor.Invoke([protectionToken]);
    }

    internal static StepExecutionContext CreateExecution(
        InstanceId instanceId,
        StepOperationId operationId,
        int attemptNumber)
    {
        var constructor = typeof(StepExecutionContext).GetConstructors(
                BindingFlags.Instance | BindingFlags.NonPublic)
            .Single(candidate => candidate.GetParameters().Length == 3);
        return (StepExecutionContext)constructor.Invoke([instanceId, operationId, attemptNumber]);
    }

    internal static StepContext<TState> Create<TState>(
        TState state,
        StepExecutionContext execution,
        EventEnvelope? resumedEvent,
        TimeProvider timeProvider,
        ForEachItemContext? forEachItem = null,
        ResourceLeaseExecutionContext? resourceLease = null)
    {
        var constructor = typeof(StepContext<TState>).GetConstructors(
                BindingFlags.Instance | BindingFlags.NonPublic)
            .Single(candidate => candidate.GetParameters().Length == 6);
        return (StepContext<TState>)constructor.Invoke(
            [state, execution, resumedEvent, timeProvider, forEachItem, resourceLease]);
    }

    internal static EventEnvelope CreateResumedEvent(
        EventId eventId,
        EventName eventName,
        CorrelationId correlationId,
        DateTimeOffset occurredAt,
        ReadOnlyMemory<byte> payload)
    {
        var constructor = typeof(EventEnvelope).GetConstructor(
            BindingFlags.Instance | BindingFlags.NonPublic,
            binder: null,
            [typeof(EventId), typeof(EventName), typeof(CorrelationId), typeof(DateTimeOffset), typeof(ReadOnlyMemory<byte>)],
            modifiers: null) ?? throw new InvalidOperationException(
                "The approved EventEnvelope constructor was not found.");
        return (EventEnvelope)constructor.Invoke([eventId, eventName, correlationId, occurredAt, payload]);
    }
}
