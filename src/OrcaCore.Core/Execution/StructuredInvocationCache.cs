using System.Collections.Concurrent;
using System.Linq.Expressions;
using System.Reflection;
using OrcaCore.Abstractions.Events;
using OrcaCore.Abstractions.Steps;
using OrcaCore.Core.Building;

namespace OrcaCore.Core.Execution;

internal static class StructuredInvocationCache
{
    private delegate ValueTask<StepResult> StepInvoker(
        object step,
        object state,
        EventEnvelope? resumedEvent,
        TimeProvider timeProvider,
        CancellationToken cancellationToken);

    private static readonly ConcurrentDictionary<Delegate, Func<object?[], object?>> DelegateInvokers = [];
    private static readonly ConcurrentDictionary<Type, StepInvoker> StepInvokers = [];
    private static readonly ConcurrentDictionary<Type, Func<object, object>> ParentSnapshotFactories = [];
    private static readonly ConcurrentDictionary<Type, Func<object, object>> BranchSnapshotFactories = [];

    internal static object? Invoke(Delegate callback, params object?[] arguments)
    {
        ArgumentNullException.ThrowIfNull(callback);
        ArgumentNullException.ThrowIfNull(arguments);
        try
        {
            return DelegateInvokers.GetOrAdd(callback, BuildDelegateInvoker)(arguments);
        }
        catch (TargetInvocationException)
        {
            throw;
        }
        catch (Exception exception)
        {
            throw new TargetInvocationException(exception);
        }
    }

    internal static async ValueTask<StepResult> ExecuteStepAsync(
        Type stateType,
        object step,
        object state,
        EventEnvelope? resumedEvent,
        TimeProvider timeProvider,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(stateType);
        try
        {
            return await StepInvokers
                .GetOrAdd(stateType, BuildStepInvoker)(
                    step,
                    state,
                    resumedEvent,
                    timeProvider,
                    cancellationToken)
                .ConfigureAwait(false);
        }
        catch (TargetInvocationException)
        {
            throw;
        }
        catch (Exception exception)
        {
            throw new TargetInvocationException(exception);
        }
    }

    internal static object CreateParentSnapshot(Type stateType, object state) =>
        ParentSnapshotFactories.GetOrAdd(
            stateType,
            type => BuildSnapshotFactory(typeof(ReadOnlyParentSnapshot<>), type))(state);

    internal static object CreateBranchSnapshot(Type stateType, object state) =>
        BranchSnapshotFactories.GetOrAdd(
            stateType,
            type => BuildSnapshotFactory(typeof(ReadOnlyBranchSnapshot<>), type))(state);

    private static Func<object?[], object?> BuildDelegateInvoker(Delegate callback)
    {
        var arguments = Expression.Parameter(typeof(object[]), "arguments");
        var invoke = callback.GetType().GetMethod("Invoke") ??
            throw new InvalidOperationException("Delegate type has no Invoke method.");
        var converted = invoke.GetParameters()
            .Select((parameter, index) => Expression.Convert(
                Expression.ArrayIndex(arguments, Expression.Constant(index)),
                parameter.ParameterType))
            .ToArray();
        var call = Expression.Invoke(Expression.Constant(callback), converted);
        Expression body = invoke.ReturnType == typeof(void)
            ? Expression.Block(call, Expression.Constant(null, typeof(object)))
            : Expression.Convert(call, typeof(object));
        return Expression.Lambda<Func<object?[], object?>>(body, arguments).Compile();
    }

    private static StepInvoker BuildStepInvoker(Type stateType)
    {
        var method = typeof(StructuredInvocationCache)
            .GetMethod(nameof(ExecuteTypedStepAsync), BindingFlags.Static | BindingFlags.NonPublic)!
            .MakeGenericMethod(stateType);
        return method.CreateDelegate<StepInvoker>();
    }

    private static ValueTask<StepResult> ExecuteTypedStepAsync<TState>(
        object step,
        object state,
        EventEnvelope? resumedEvent,
        TimeProvider timeProvider,
        CancellationToken cancellationToken)
    {
        if (step is not IStep<TState> typedStep || state is not TState typedState)
        {
            throw new InvalidOperationException(
                $"Compiled branch step does not match state type '{typeof(TState).FullName}'.");
        }

        return typedStep.ExecuteAsync(
            new StepContext<TState>(typedState, resumedEvent, timeProvider),
            cancellationToken);
    }

    private static Func<object, object> BuildSnapshotFactory(Type openSnapshotType, Type stateType)
    {
        var snapshotType = openSnapshotType.MakeGenericType(stateType);
        var constructor = snapshotType.GetConstructor([stateType]) ??
            throw new InvalidOperationException(
                $"Snapshot type '{snapshotType.FullName}' has no state constructor.");
        var state = Expression.Parameter(typeof(object), "state");
        var body = Expression.Convert(
            Expression.New(constructor, Expression.Convert(state, stateType)),
            typeof(object));
        return Expression.Lambda<Func<object, object>>(body, state).Compile();
    }
}
