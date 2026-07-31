using System.Runtime.CompilerServices;
using OrcaCore.Internal;

namespace OrcaCore;

/// <summary>Selects workflow execution mode before any mode-specific capability is authored.</summary>
public static class Workflow
{
    public static EphemeralWorkflowInitBuilder<TState> Ephemeral<TState>(
        DefinitionId definitionId,
        DefinitionVersion definitionVersion)
    {
        ArgumentNullException.ThrowIfNull(definitionId);
        ArgumentNullException.ThrowIfNull(definitionVersion);
        return new(AuthoringKernelProxy.InvokeStatic(
            "OrcaCore.Core.Authoring.Workflow",
            nameof(Ephemeral),
            [typeof(TState)],
            definitionId,
            definitionVersion));
    }

    public static DurableWorkflowInitBuilder<TState> Durable<TState>(
        DefinitionId definitionId,
        DefinitionVersion definitionVersion)
    {
        ArgumentNullException.ThrowIfNull(definitionId);
        ArgumentNullException.ThrowIfNull(definitionVersion);
        return new(AuthoringKernelProxy.InvokeStatic(
            "OrcaCore.Core.Authoring.Workflow",
            nameof(Durable),
            [typeof(TState)],
            definitionId,
            definitionVersion));
    }
}

public sealed class EphemeralWorkflowInitBuilder<TState>
{
    private readonly object implementation;
    internal EphemeralWorkflowInitBuilder(object implementation) => this.implementation = implementation;

    public EphemeralWorkflowBuilder<TInput, TState> Init<TInput>(Func<TInput, TState> createState) =>
        new(AuthoringKernelProxy.Invoke(
            implementation,
            nameof(Init),
            [typeof(TInput)],
            createState)!);
}

public sealed class DurableWorkflowInitBuilder<TState>
{
    private readonly object implementation;
    internal DurableWorkflowInitBuilder(object implementation) => this.implementation = implementation;

    public DurableWorkflowBuilder<TInput, TState> Init<TInput>(Func<TInput, TState> createState) =>
        new(AuthoringKernelProxy.Invoke(
            implementation,
            nameof(Init),
            [typeof(TInput)],
            createState)!);
}

public sealed class EphemeralWorkflowBuilder<TInput, TState>
{
    private readonly object implementation;
    internal EphemeralWorkflowBuilder(object implementation) => this.implementation = implementation;

    public EphemeralWorkflowBuilder<TInput, TState> Then<TStep>() where TStep : IStep<TState>
    {
        AuthoringKernelProxy.Invoke(implementation, nameof(Then), [typeof(TStep)]);
        return this;
    }

    public EphemeralWorkflowBuilder<TInput, TState> Then(Func<StepContext<TState>, ValueTask> body)
    {
        AuthoringKernelProxy.Invoke(implementation, nameof(Then), Type.EmptyTypes, body);
        return this;
    }

    public EphemeralWorkflowBuilder<TInput, TState> Then(
        Func<StepContext<TState>, CancellationToken, ValueTask> body)
    {
        AuthoringKernelProxy.Invoke(implementation, nameof(Then), Type.EmptyTypes, body);
        return this;
    }

    public EphemeralWorkflowBuilder<TInput, TState> WithRetry(
        int maxAttempts,
        TimeSpan? fixedDelay = null)
    {
        AuthoringKernelProxy.Invoke(
            implementation,
            nameof(WithRetry),
            Type.EmptyTypes,
            maxAttempts,
            fixedDelay);
        return this;
    }

    public EphemeralWorkflowBuilder<TInput, TState> WithStepTimeout(TimeSpan timeout)
    {
        AuthoringKernelProxy.Invoke(implementation, nameof(WithStepTimeout), Type.EmptyTypes, timeout);
        return this;
    }

    public EphemeralWorkflowBuilder<TInput, TState> WithTransientPool(TransientPoolName pool)
    {
        AuthoringKernelProxy.Invoke(implementation, nameof(WithTransientPool), Type.EmptyTypes, pool);
        return this;
    }

    public EphemeralWorkflowBuilder<TInput, TState> CompleteWithin(TimeSpan timeout)
    {
        AuthoringKernelProxy.Invoke(implementation, nameof(CompleteWithin), Type.EmptyTypes, timeout);
        return this;
    }

    public EphemeralWorkflowBuilder<TInput, TState> If(
        Func<ReadOnlyStateSnapshot<TState>, bool> condition,
        Action<EphemeralNestedBuilder<TInput, TState>> then,
        Action<EphemeralNestedBuilder<TInput, TState>>? otherwise = null)
    {
        AuthoringKernelProxy.Invoke(implementation, nameof(If), Type.EmptyTypes, condition, then, otherwise);
        return this;
    }

    public EphemeralWorkflowBuilder<TInput, TState> While(
        Func<ReadOnlyStateSnapshot<TState>, bool> condition,
        Action<EphemeralNestedBuilder<TInput, TState>> body)
    {
        AuthoringKernelProxy.Invoke(implementation, nameof(While), Type.EmptyTypes, condition, body);
        return this;
    }

    public EphemeralWorkflowBuilder<TInput, TState> Wait(
        EventName eventName,
        Func<ReadOnlyStateSnapshot<TState>, CorrelationId> correlation)
    {
        AuthoringKernelProxy.Invoke(implementation, nameof(Wait), Type.EmptyTypes, eventName, correlation);
        return this;
    }

    public EphemeralWorkflowBuilder<TInput, TState> Wait(
        EventName eventName,
        Func<ReadOnlyStateSnapshot<TState>, CorrelationId> correlation,
        TimeSpan timeout)
    {
        AuthoringKernelProxy.Invoke(
            implementation,
            nameof(Wait),
            Type.EmptyTypes,
            eventName,
            correlation,
            timeout);
        return this;
    }

    public EphemeralWorkflowBuilder<TInput, TState> Delay(TimeSpan duration)
    {
        AuthoringKernelProxy.Invoke(implementation, nameof(Delay), Type.EmptyTypes, duration);
        return this;
    }

    public EphemeralWorkflowParallelJoinBuilder<TInput, TState, TResult> Parallel<TResult>(
        Action<EphemeralWorkflowParallelBranchScopeBuilder<TInput, TState, TResult>> branches) =>
        new(AuthoringKernelProxy.Invoke(
            implementation,
            nameof(Parallel),
            [typeof(TResult)],
            branches)!);

    public EphemeralForEachJoinBuilder<TInput, TState, TResult> ForEach<TItem, TItemState, TResult>(
        Func<ReadOnlyStateSnapshot<TState>, IReadOnlyList<TItem>> items,
        ForEachOptions options,
        Func<ForEachItemInput<TItem>, TItemState> input,
        Action<EphemeralItemBuilder<TItemState, TResult>> body) =>
        new(AuthoringKernelProxy.Invoke(
            implementation,
            nameof(ForEach),
            [typeof(TItem), typeof(TItemState), typeof(TResult)],
            items,
            options,
            input,
            body)!);

    public EphemeralWorkflowCompletionBuilder<TInput> End() =>
        new(AuthoringKernelProxy.Invoke(implementation, nameof(End), Type.EmptyTypes)!);

    public EphemeralWorkflowCompletionBuilder<TInput> End(WorkflowOutcomeName outcome) =>
        new(AuthoringKernelProxy.Invoke(implementation, nameof(End), Type.EmptyTypes, outcome)!);

    public EphemeralWorkflowCompletionBuilder<TInput, TOutput> End<TOutput>(
        Func<ReadOnlyStateSnapshot<TState>, TOutput> output) =>
        new(AuthoringKernelProxy.Invoke(
            implementation,
            nameof(End),
            [typeof(TOutput)],
            output)!);

    public EphemeralWorkflowCompletionBuilder<TInput, TOutput> End<TOutput>(
        Func<ReadOnlyStateSnapshot<TState>, TOutput> output,
        WorkflowOutcomeName outcome) =>
        new(AuthoringKernelProxy.Invoke(
            implementation,
            nameof(End),
            [typeof(TOutput)],
            output,
            outcome)!);
}

public sealed class DurableWorkflowBuilder<TInput, TState>
{
    private readonly object implementation;
    internal DurableWorkflowBuilder(object implementation) => this.implementation = implementation;

    public DurableWorkflowBuilder<TInput, TState> Then<TStep>() where TStep : IStep<TState>
    {
        AuthoringKernelProxy.Invoke(implementation, nameof(Then), [typeof(TStep)]);
        return this;
    }

    public DurableWorkflowBuilder<TInput, TState> WithRetry(
        int maxAttempts,
        TimeSpan? fixedDelay = null)
    {
        AuthoringKernelProxy.Invoke(
            implementation,
            nameof(WithRetry),
            Type.EmptyTypes,
            maxAttempts,
            fixedDelay);
        return this;
    }

    public DurableWorkflowBuilder<TInput, TState> WithStepTimeout(TimeSpan timeout)
    {
        AuthoringKernelProxy.Invoke(implementation, nameof(WithStepTimeout), Type.EmptyTypes, timeout);
        return this;
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    public DurableWorkflowBuilder<TInput, TState> CompleteWithin(TimeSpan timeout)
    {
        AuthoringKernelProxy.Invoke(implementation, nameof(CompleteWithin), Type.EmptyTypes, timeout);
        return this;
    }

    public DurableWorkflowBuilder<TInput, TState> If(
        Func<ReadOnlyStateSnapshot<TState>, bool> condition,
        Action<DurableNestedBuilder<TInput, TState>> then,
        Action<DurableNestedBuilder<TInput, TState>>? otherwise = null)
    {
        AuthoringKernelProxy.Invoke(implementation, nameof(If), Type.EmptyTypes, condition, then, otherwise);
        return this;
    }

    public DurableWorkflowBuilder<TInput, TState> While(
        Func<ReadOnlyStateSnapshot<TState>, bool> condition,
        Action<DurableNestedBuilder<TInput, TState>> body)
    {
        AuthoringKernelProxy.Invoke(implementation, nameof(While), Type.EmptyTypes, condition, body);
        return this;
    }

    public DurableWorkflowBuilder<TInput, TState> Wait(
        EventName eventName,
        Func<ReadOnlyStateSnapshot<TState>, CorrelationId> correlation)
    {
        AuthoringKernelProxy.Invoke(implementation, nameof(Wait), Type.EmptyTypes, eventName, correlation);
        return this;
    }

    public DurableWorkflowBuilder<TInput, TState> Wait(
        EventName eventName,
        Func<ReadOnlyStateSnapshot<TState>, CorrelationId> correlation,
        TimeSpan timeout)
    {
        AuthoringKernelProxy.Invoke(
            implementation,
            nameof(Wait),
            Type.EmptyTypes,
            eventName,
            correlation,
            timeout);
        return this;
    }

    public DurableWorkflowBuilder<TInput, TState> Delay(TimeSpan duration)
    {
        AuthoringKernelProxy.Invoke(implementation, nameof(Delay), Type.EmptyTypes, duration);
        return this;
    }

    public DurableWorkflowParallelJoinBuilder<TInput, TState, TResult> Parallel<TResult>(
        Action<DurableWorkflowParallelBranchScopeBuilder<TInput, TState, TResult>> branches) =>
        new(AuthoringKernelProxy.Invoke(
            implementation,
            nameof(Parallel),
            [typeof(TResult)],
            branches)!);

    public DurableForEachJoinBuilder<TInput, TState, TResult> ForEach<TItem, TItemState, TResult>(
        Func<ReadOnlyStateSnapshot<TState>, IReadOnlyList<TItem>> items,
        ForEachOptions options,
        Func<ForEachItemInput<TItem>, TItemState> input,
        Action<DurableItemBuilder<TItemState, TResult>> body) =>
        new(AuthoringKernelProxy.Invoke(
            implementation,
            nameof(ForEach),
            [typeof(TItem), typeof(TItemState), typeof(TResult)],
            items,
            options,
            input,
            body)!);

    public DurableWorkflowBuilder<TInput, TState> AcquireResources(
        ResourceLeaseRequest request,
        Action<DurableLeaseWorkflowBuilder<TInput, TState>> body)
    {
        AuthoringKernelProxy.Invoke(
            implementation,
            nameof(AcquireResources),
            Type.EmptyTypes,
            request,
            body);
        return this;
    }

    public DurableWorkflowBuilder<TInput, TState> AcquireResources(
        Func<ReadOnlyStateSnapshot<TState>, ResourceLeaseRequest> request,
        Action<DurableLeaseWorkflowBuilder<TInput, TState>> body)
    {
        AuthoringKernelProxy.Invoke(
            implementation,
            nameof(AcquireResources),
            Type.EmptyTypes,
            request,
            body);
        return this;
    }

    public DurableWorkflowCompletionBuilder<TInput> ContinueAsNew(
        Func<ReadOnlyStateSnapshot<TState>, TState> replacementState) =>
        new(AuthoringKernelProxy.Invoke(
            implementation,
            nameof(ContinueAsNew),
            Type.EmptyTypes,
            replacementState)!);

    public DurableWorkflowCompletionBuilder<TInput> End() =>
        new(AuthoringKernelProxy.Invoke(implementation, nameof(End), Type.EmptyTypes)!);

    public DurableWorkflowCompletionBuilder<TInput> End(WorkflowOutcomeName outcome) =>
        new(AuthoringKernelProxy.Invoke(implementation, nameof(End), Type.EmptyTypes, outcome)!);

    public DurableWorkflowCompletionBuilder<TInput, TOutput> End<TOutput>(
        Func<ReadOnlyStateSnapshot<TState>, TOutput> output) =>
        new(AuthoringKernelProxy.Invoke(
            implementation,
            nameof(End),
            [typeof(TOutput)],
            output)!);

    public DurableWorkflowCompletionBuilder<TInput, TOutput> End<TOutput>(
        Func<ReadOnlyStateSnapshot<TState>, TOutput> output,
        WorkflowOutcomeName outcome) =>
        new(AuthoringKernelProxy.Invoke(
            implementation,
            nameof(End),
            [typeof(TOutput)],
            output,
            outcome)!);
}

public sealed class EphemeralWorkflowCompletionBuilder<TInput>
{
    private readonly object implementation;
    internal EphemeralWorkflowCompletionBuilder(object implementation) => this.implementation = implementation;

    [MethodImpl(MethodImplOptions.NoInlining)]
    public EphemeralWorkflowDefinition<TInput> Build() =>
        (EphemeralWorkflowDefinition<TInput>)AuthoringKernelProxy.Invoke(
            implementation,
            nameof(Build),
            Type.EmptyTypes)!;

    public Validation<EphemeralWorkflowDefinition<TInput>> TryBuild() =>
        (Validation<EphemeralWorkflowDefinition<TInput>>)AuthoringKernelProxy.Invoke(
            implementation,
            nameof(TryBuild),
            Type.EmptyTypes)!;
}

public sealed class EphemeralWorkflowCompletionBuilder<TInput, TOutput>
{
    private readonly object implementation;
    internal EphemeralWorkflowCompletionBuilder(object implementation) => this.implementation = implementation;

    public EphemeralWorkflowDefinition<TInput, TOutput> Build() =>
        (EphemeralWorkflowDefinition<TInput, TOutput>)AuthoringKernelProxy.Invoke(
            implementation,
            nameof(Build),
            Type.EmptyTypes)!;

    public Validation<EphemeralWorkflowDefinition<TInput, TOutput>> TryBuild() =>
        (Validation<EphemeralWorkflowDefinition<TInput, TOutput>>)AuthoringKernelProxy.Invoke(
            implementation,
            nameof(TryBuild),
            Type.EmptyTypes)!;
}

public sealed class DurableWorkflowCompletionBuilder<TInput>
{
    private readonly object implementation;
    internal DurableWorkflowCompletionBuilder(object implementation) => this.implementation = implementation;

    public DurableWorkflowDefinition<TInput> Build() =>
        (DurableWorkflowDefinition<TInput>)AuthoringKernelProxy.Invoke(
            implementation,
            nameof(Build),
            Type.EmptyTypes)!;

    public Validation<DurableWorkflowDefinition<TInput>> TryBuild() =>
        (Validation<DurableWorkflowDefinition<TInput>>)AuthoringKernelProxy.Invoke(
            implementation,
            nameof(TryBuild),
            Type.EmptyTypes)!;
}

public sealed class DurableWorkflowCompletionBuilder<TInput, TOutput>
{
    private readonly object implementation;
    internal DurableWorkflowCompletionBuilder(object implementation) => this.implementation = implementation;

    public DurableWorkflowDefinition<TInput, TOutput> Build() =>
        (DurableWorkflowDefinition<TInput, TOutput>)AuthoringKernelProxy.Invoke(
            implementation,
            nameof(Build),
            Type.EmptyTypes)!;

    public Validation<DurableWorkflowDefinition<TInput, TOutput>> TryBuild() =>
        (Validation<DurableWorkflowDefinition<TInput, TOutput>>)AuthoringKernelProxy.Invoke(
            implementation,
            nameof(TryBuild),
            Type.EmptyTypes)!;
}
