using System.Collections.Concurrent;
using System.Linq.Expressions;
using System.Reflection;
using System.Runtime.CompilerServices;
using OrcaCore.Core.Building;

namespace OrcaCore.Core.Execution;

internal static class StructuredInvocationCache
{
    private delegate ValueTask<StructuredStepInvocationResult> StepInvoker(
        object step,
        object state,
        StepExecutionContext execution,
        EventEnvelope? resumedEvent,
        TimeProvider timeProvider,
        ForEachItemContext? forEachItem,
        ResourceLeaseExecutionContext? resourceLease,
        CancellationToken cancellationToken);

    private static readonly ConditionalWeakTable<Delegate, BoundInvocation> BoundInvocations = new();
    private static readonly ConcurrentDictionary<Delegate, Func<object?[], object?>> DelegateInvokers = [];

    private sealed record BoundInvocation(Func<object?[], object?> Invoke);

    // Preserve delegate identity for fingerprints without permanently retaining application closures.
    [return: System.Diagnostics.CodeAnalysis.NotNullIfNotNull(nameof(callback))]
    internal static Func<TResult>? Bind<TResult>(Func<TResult>? callback) =>
        BindCore(callback, arguments => callback!());

    [return: System.Diagnostics.CodeAnalysis.NotNullIfNotNull(nameof(callback))]
    internal static Func<T, TResult>? Bind<T, TResult>(Func<T, TResult>? callback) =>
        BindCore(callback, arguments => callback!((T)arguments[0]!));

    [return: System.Diagnostics.CodeAnalysis.NotNullIfNotNull(nameof(callback))]
    internal static Func<T1, T2, TResult>? Bind<T1, T2, TResult>(Func<T1, T2, TResult>? callback) =>
        BindCore(callback, arguments => callback!((T1)arguments[0]!, (T2)arguments[1]!));

    private static TDelegate? BindCore<TDelegate>(TDelegate? callback, Func<object?[], object?> invoke)
        where TDelegate : Delegate
    {
        if (callback is not null) BoundInvocations.GetValue(callback, _ => new BoundInvocation(invoke));
        return callback;
    }

    internal static void BindState<TState>()
    {
        StepInvokers.TryAdd(typeof(TState), ExecuteTypedStepAsync<TState>);
        ParentSnapshotFactories.TryAdd(typeof(TState), state => new ReadOnlyParentSnapshot<TState>((TState)state));
        BranchSnapshotFactories.TryAdd(typeof(TState), state => new ReadOnlyBranchSnapshot<TState>((TState)state));
    }

    internal static void RequireLegacyReflection()
    {
        if (!RuntimeFeature.IsDynamicCodeSupported ||
            !System.Text.Json.JsonSerializer.IsReflectionEnabledByDefault ||
            global::OrcaCore.Internal.WorkflowJsonMetadataRegistry.Seal() is not null)
        {
            throw new InvalidOperationException("The compiled workflow is missing a closed typed runtime binding.");
        }
    }
    private static readonly ConcurrentDictionary<Type, StepInvoker> StepInvokers = [];
    private static readonly ConcurrentDictionary<Type, Func<object, object>> ParentSnapshotFactories = [];
    private static readonly ConcurrentDictionary<Type, Func<object, object>> BranchSnapshotFactories = [];

    public static object? Invoke(Delegate callback, params object?[] arguments)
    {
        ArgumentNullException.ThrowIfNull(callback);
        ArgumentNullException.ThrowIfNull(arguments);
        try
        {
            if (BoundInvocations.TryGetValue(callback, out var bound)) return bound.Invoke(arguments);
            if (!global::OrcaCore.Internal.WorkflowJsonMetadataRegistry.DynamicCodeEnabled ||
            !global::OrcaCore.Internal.WorkflowJsonMetadataRegistry.LegacyReflectionEnabled)
                throw new InvalidOperationException("The compiled workflow is missing a closed typed callback binding.");
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

    public static async ValueTask<StructuredStepInvocationResult> ExecuteStepAsync(
        Type stateType,
        object step,
        object state,
        StepExecutionContext execution,
        EventEnvelope? resumedEvent,
        TimeProvider timeProvider,
        ForEachItemContext? forEachItem,
        ResourceLeaseExecutionContext? resourceLease,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(stateType);
        try
        {
            if (!StepInvokers.TryGetValue(stateType, out var invoker))
            {
                if (!global::OrcaCore.Internal.WorkflowJsonMetadataRegistry.DynamicCodeEnabled ||
            !global::OrcaCore.Internal.WorkflowJsonMetadataRegistry.LegacyReflectionEnabled)
                    throw new InvalidOperationException("The compiled workflow is missing a closed typed step binding.");
                invoker = StepInvokers.GetOrAdd(stateType, BuildStepInvoker);
            }
            return await invoker(step, state, execution, resumedEvent, timeProvider,
                forEachItem, resourceLease, cancellationToken).ConfigureAwait(false);
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

    public static object CreateParentSnapshot(Type stateType, object state)
    {
        if (ParentSnapshotFactories.TryGetValue(stateType, out var factory)) return factory(state);
        if (!global::OrcaCore.Internal.WorkflowJsonMetadataRegistry.DynamicCodeEnabled ||
            !global::OrcaCore.Internal.WorkflowJsonMetadataRegistry.LegacyReflectionEnabled)
            throw new InvalidOperationException("The compiled workflow is missing a closed typed parent snapshot binding.");
        return ParentSnapshotFactories.GetOrAdd(stateType,
            type => BuildSnapshotFactory(typeof(ReadOnlyParentSnapshot<>), type))(state);
    }

    public static object CreateBranchSnapshot(Type stateType, object state)
    {
        if (BranchSnapshotFactories.TryGetValue(stateType, out var factory)) return factory(state);
        if (!global::OrcaCore.Internal.WorkflowJsonMetadataRegistry.DynamicCodeEnabled ||
            !global::OrcaCore.Internal.WorkflowJsonMetadataRegistry.LegacyReflectionEnabled)
            throw new InvalidOperationException("The compiled workflow is missing a closed typed branch snapshot binding.");
        return BranchSnapshotFactories.GetOrAdd(stateType,
            type => BuildSnapshotFactory(typeof(ReadOnlyBranchSnapshot<>), type))(state);
    }

    [System.Diagnostics.CodeAnalysis.RequiresDynamicCode("Unconfigured legacy JIT workflow adapter only.")]
    [System.Diagnostics.CodeAnalysis.RequiresUnreferencedCode("Unconfigured legacy JIT workflow adapter only.")]
    private static Func<object?[], object?> BuildDelegateInvoker(Delegate callback)
    {
        RequireLegacyReflection();
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

    [System.Diagnostics.CodeAnalysis.RequiresDynamicCode("Unconfigured legacy JIT workflow adapter only.")]
    [System.Diagnostics.CodeAnalysis.RequiresUnreferencedCode("Unconfigured legacy JIT workflow adapter only.")]
    private static StepInvoker BuildStepInvoker(Type stateType)
    {
        RequireLegacyReflection();
        var method = typeof(StructuredInvocationCache)
            .GetMethod(nameof(ExecuteTypedStepAsync), BindingFlags.Static | BindingFlags.NonPublic)!
            .MakeGenericMethod(stateType);
        return method.CreateDelegate<StepInvoker>();
    }

    private static async ValueTask<StructuredStepInvocationResult> ExecuteTypedStepAsync<TState>(
        object step,
        object state,
        StepExecutionContext execution,
        EventEnvelope? resumedEvent,
        TimeProvider timeProvider,
        ForEachItemContext? forEachItem,
        ResourceLeaseExecutionContext? resourceLease,
        CancellationToken cancellationToken)
    {
        if (step is not IStep<TState> typedStep || state is not TState typedState)
        {
            throw new InvalidOperationException(
                $"Compiled branch step does not match state type '{typeof(TState).FullName}'.");
        }

        var context = new StepContext<TState>(
            typedState, execution, resumedEvent, timeProvider, forEachItem, resourceLease);
        var result = await typedStep.ExecuteAsync(context, cancellationToken).ConfigureAwait(false);
        return new StructuredStepInvocationResult(result, context.State!);
    }

    [System.Diagnostics.CodeAnalysis.RequiresDynamicCode("Unconfigured legacy JIT workflow adapter only.")]
    [System.Diagnostics.CodeAnalysis.RequiresUnreferencedCode("Unconfigured legacy JIT workflow adapter only.")]
    private static Func<object, object> BuildSnapshotFactory(Type openSnapshotType, Type stateType)
    {
        RequireLegacyReflection();
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

internal sealed record StructuredStepInvocationResult(StepResult Result, object State);
