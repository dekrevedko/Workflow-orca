using System.Reflection;
using OrcaCore.Abstractions.Events;
using OrcaCore.Abstractions.Steps;

namespace OrcaCore.Engine.Ephemeral.Execution;

internal static class RuntimeStepContextFactory
{
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
}
