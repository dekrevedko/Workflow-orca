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
        return new(TypedAuthoringBoundary.Operations.Ephemeral<TState>(definitionId, definitionVersion));
    }

    public static DurableWorkflowInitBuilder<TState> Durable<TState>(
        DefinitionId definitionId,
        DefinitionVersion definitionVersion)
    {
        ArgumentNullException.ThrowIfNull(definitionId);
        ArgumentNullException.ThrowIfNull(definitionVersion);
        return new(TypedAuthoringBoundary.Operations.Durable<TState>(definitionId, definitionVersion));
    }
}

public sealed class EphemeralWorkflowInitBuilder<TState>
{
    private readonly AuthoringKernelHandle implementation;
    internal EphemeralWorkflowInitBuilder(AuthoringKernelHandle implementation) => this.implementation = implementation;

    public EphemeralWorkflowBuilder<TInput, TState> Init<TInput>(Func<TInput, TState> createState) =>
        new(TypedAuthoringBoundary.Operations.Init(implementation, createState));
}

public sealed class DurableWorkflowInitBuilder<TState>
{
    private readonly AuthoringKernelHandle implementation;
    internal DurableWorkflowInitBuilder(AuthoringKernelHandle implementation) => this.implementation = implementation;

    public DurableWorkflowBuilder<TInput, TState> Init<TInput>(Func<TInput, TState> createState) =>
        new(TypedAuthoringBoundary.Operations.Init(implementation, createState));
}

public sealed class EphemeralWorkflowBuilder<TInput, TState>
{
    private readonly AuthoringKernelHandle implementation;
    internal EphemeralWorkflowBuilder(AuthoringKernelHandle implementation) => this.implementation = implementation;

    public EphemeralWorkflowBuilder<TInput, TState> Then<TStep>() where TStep : IStep<TState>
    {
        TypedAuthoringBoundary.Operations.Then<TInput, TState, NoAuthoringValue, TStep>(implementation);
        return this;
    }

    public EphemeralWorkflowBuilder<TInput, TState> Then(Func<StepContext<TState>, ValueTask> body)
    {
        TypedAuthoringBoundary.Operations.Then<TInput, TState, NoAuthoringValue>(implementation, body);
        return this;
    }

    public EphemeralWorkflowBuilder<TInput, TState> Then(
        Func<StepContext<TState>, CancellationToken, ValueTask> body)
    {
        TypedAuthoringBoundary.Operations.Then<TInput, TState, NoAuthoringValue>(implementation, body);
        return this;
    }

    public EphemeralWorkflowBuilder<TInput, TState> WithRetry(int maxAttempts, TimeSpan? fixedDelay = null)
    {
        TypedAuthoringBoundary.Operations.WithRetry<TInput, TState, NoAuthoringValue>(implementation, maxAttempts, fixedDelay);
        return this;
    }

    public EphemeralWorkflowBuilder<TInput, TState> WithStepTimeout(TimeSpan timeout)
    {
        TypedAuthoringBoundary.Operations.WithStepTimeout<TInput, TState, NoAuthoringValue>(implementation, timeout);
        return this;
    }

    public EphemeralWorkflowBuilder<TInput, TState> WithTransientPool(TransientPoolName pool)
    {
        TypedAuthoringBoundary.Operations.WithTransientPool<TInput, TState, NoAuthoringValue>(implementation, pool);
        return this;
    }

    public EphemeralWorkflowBuilder<TInput, TState> CompleteWithin(TimeSpan timeout)
    {
        TypedAuthoringBoundary.Operations.CompleteWithin<TInput, TState>(implementation, timeout);
        return this;
    }

    public EphemeralWorkflowBuilder<TInput, TState> If(
        Func<ReadOnlyStateSnapshot<TState>, bool> condition,
        Action<EphemeralNestedBuilder<TInput, TState>> then,
        Action<EphemeralNestedBuilder<TInput, TState>>? otherwise = null)
    {
        TypedAuthoringBoundary.Operations.If<TInput, TState, NoAuthoringValue>(
            implementation,
            condition,
            nested => then(new(nested)),
            otherwise is null ? null : nested => otherwise(new(nested)));
        return this;
    }

    public EphemeralWorkflowBuilder<TInput, TState> While(
        Func<ReadOnlyStateSnapshot<TState>, bool> condition,
        Action<EphemeralNestedBuilder<TInput, TState>> body)
    {
        TypedAuthoringBoundary.Operations.While<TInput, TState>(implementation, condition, nested => body(new(nested)));
        return this;
    }

    public EphemeralWorkflowBuilder<TInput, TState> Wait(
        WorkflowEventContract eventContract,
        Func<ReadOnlyStateSnapshot<TState>, CorrelationId> correlation)
    {
        TypedAuthoringBoundary.Operations.Wait<TInput, TState, NoAuthoringValue>(implementation, eventContract, correlation, null);
        return this;
    }

    public EphemeralWorkflowBuilder<TInput, TState> Wait(
        WorkflowEventContract eventContract,
        Func<ReadOnlyStateSnapshot<TState>, CorrelationId> correlation,
        TimeSpan timeout)
    {
        TypedAuthoringBoundary.Operations.Wait<TInput, TState, NoAuthoringValue>(implementation, eventContract, correlation, timeout);
        return this;
    }

    public EphemeralWorkflowBuilder<TInput, TState> Wait<TPayload>(
        WorkflowEventContract<TPayload> eventContract,
        Func<ReadOnlyStateSnapshot<TState>, CorrelationId> correlation) =>
        Wait((WorkflowEventContract)eventContract, correlation);

    public EphemeralWorkflowBuilder<TInput, TState> Wait<TPayload>(
        WorkflowEventContract<TPayload> eventContract,
        Func<ReadOnlyStateSnapshot<TState>, CorrelationId> correlation,
        TimeSpan timeout) =>
        Wait((WorkflowEventContract)eventContract, correlation, timeout);

    public EphemeralWorkflowBuilder<TInput, TState> Delay(TimeSpan duration)
    {
        TypedAuthoringBoundary.Operations.Delay<TInput, TState, NoAuthoringValue>(implementation, duration);
        return this;
    }

    public EphemeralWorkflowParallelJoinBuilder<TInput, TState, TResult> Parallel<TResult>(
        Action<EphemeralWorkflowParallelBranchScopeBuilder<TInput, TState, TResult>> branches) =>
        new(TypedAuthoringBoundary.Operations.Parallel<TInput, TState, TResult>(
            implementation,
            scope => branches(new(scope))));

    public EphemeralForEachJoinBuilder<TInput, TState, TResult> ForEach<TItem, TItemState, TResult>(
        Func<ReadOnlyStateSnapshot<TState>, IReadOnlyList<TItem>> items,
        ForEachOptions options,
        Func<ForEachItemInput<TItem>, TItemState> input,
        Action<EphemeralItemBuilder<TItemState, TResult>> body) =>
        new(TypedAuthoringBoundary.Operations.ForEach<TInput, TState, TItem, TItemState, TResult>(
            implementation,
            items,
            options,
            input,
            nested => body(new(nested))));

    public EphemeralWorkflowCompletionBuilder<TInput> End() =>
        new(TypedAuthoringBoundary.Operations.End<TInput, TState>(implementation));

    public EphemeralWorkflowCompletionBuilder<TInput> End(WorkflowOutcomeName outcome) =>
        new(TypedAuthoringBoundary.Operations.End<TInput, TState>(implementation, outcome));

    public EphemeralWorkflowCompletionBuilder<TInput, TOutput> End<TOutput>(
        Func<ReadOnlyStateSnapshot<TState>, TOutput> output) =>
        new(TypedAuthoringBoundary.Operations.End<TInput, TState, TOutput>(implementation, output));

    public EphemeralWorkflowCompletionBuilder<TInput, TOutput> End<TOutput>(
        Func<ReadOnlyStateSnapshot<TState>, TOutput> output,
        WorkflowOutcomeName outcome) =>
        new(TypedAuthoringBoundary.Operations.End<TInput, TState, TOutput>(implementation, output, outcome));
}

public sealed class DurableWorkflowBuilder<TInput, TState>
{
    private readonly AuthoringKernelHandle implementation;
    internal DurableWorkflowBuilder(AuthoringKernelHandle implementation) => this.implementation = implementation;

    public DurableWorkflowBuilder<TInput, TState> Then<TStep>() where TStep : IStep<TState>
    {
        TypedAuthoringBoundary.Operations.Then<TInput, TState, NoAuthoringValue, TStep>(implementation);
        return this;
    }

    public DurableWorkflowBuilder<TInput, TState> WithRetry(int maxAttempts, TimeSpan? fixedDelay = null)
    {
        TypedAuthoringBoundary.Operations.WithRetry<TInput, TState, NoAuthoringValue>(implementation, maxAttempts, fixedDelay);
        return this;
    }

    public DurableWorkflowBuilder<TInput, TState> WithStepTimeout(TimeSpan timeout)
    {
        TypedAuthoringBoundary.Operations.WithStepTimeout<TInput, TState, NoAuthoringValue>(implementation, timeout);
        return this;
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    public DurableWorkflowBuilder<TInput, TState> CompleteWithin(TimeSpan timeout)
    {
        TypedAuthoringBoundary.Operations.CompleteWithin<TInput, TState>(implementation, timeout);
        return this;
    }

    public DurableWorkflowBuilder<TInput, TState> If(
        Func<ReadOnlyStateSnapshot<TState>, bool> condition,
        Action<DurableNestedBuilder<TInput, TState>> then,
        Action<DurableNestedBuilder<TInput, TState>>? otherwise = null)
    {
        TypedAuthoringBoundary.Operations.If<TInput, TState, NoAuthoringValue>(
            implementation,
            condition,
            nested => then(new(nested)),
            otherwise is null ? null : nested => otherwise(new(nested)));
        return this;
    }

    public DurableWorkflowBuilder<TInput, TState> While(
        Func<ReadOnlyStateSnapshot<TState>, bool> condition,
        Action<DurableNestedBuilder<TInput, TState>> body)
    {
        TypedAuthoringBoundary.Operations.While<TInput, TState>(implementation, condition, nested => body(new(nested)));
        return this;
    }

    public DurableWorkflowBuilder<TInput, TState> Wait(
        WorkflowEventContract eventContract,
        Func<ReadOnlyStateSnapshot<TState>, CorrelationId> correlation)
    {
        TypedAuthoringBoundary.Operations.Wait<TInput, TState, NoAuthoringValue>(implementation, eventContract, correlation, null);
        return this;
    }

    public DurableWorkflowBuilder<TInput, TState> Wait(
        WorkflowEventContract eventContract,
        Func<ReadOnlyStateSnapshot<TState>, CorrelationId> correlation,
        TimeSpan timeout)
    {
        TypedAuthoringBoundary.Operations.Wait<TInput, TState, NoAuthoringValue>(implementation, eventContract, correlation, timeout);
        return this;
    }

    public DurableWorkflowBuilder<TInput, TState> Wait<TPayload>(
        WorkflowEventContract<TPayload> eventContract,
        Func<ReadOnlyStateSnapshot<TState>, CorrelationId> correlation) =>
        Wait((WorkflowEventContract)eventContract, correlation);

    public DurableWorkflowBuilder<TInput, TState> Wait<TPayload>(
        WorkflowEventContract<TPayload> eventContract,
        Func<ReadOnlyStateSnapshot<TState>, CorrelationId> correlation,
        TimeSpan timeout) =>
        Wait((WorkflowEventContract)eventContract, correlation, timeout);

    public DurableWorkflowBuilder<TInput, TState> Publish(
        WorkflowEventContract eventContract,
        Func<ReadOnlyStateSnapshot<TState>, CorrelationId> correlation)
    {
        TypedAuthoringBoundary.Operations.Publish<TInput, TState, NoAuthoringValue>(implementation, eventContract, correlation);
        return this;
    }

    public DurableWorkflowBuilder<TInput, TState> Publish<TPayload>(
        WorkflowEventContract<TPayload> eventContract,
        Func<ReadOnlyStateSnapshot<TState>, CorrelationId> correlation,
        Func<ReadOnlyStateSnapshot<TState>, TPayload> payload)
    {
        TypedAuthoringBoundary.Operations.Publish<TInput, TState, NoAuthoringValue, TPayload>(implementation, eventContract, correlation, payload);
        return this;
    }

    public DurableWorkflowBuilder<TInput, TState> Delay(TimeSpan duration)
    {
        TypedAuthoringBoundary.Operations.Delay<TInput, TState, NoAuthoringValue>(implementation, duration);
        return this;
    }

    public DurableWorkflowParallelJoinBuilder<TInput, TState, TResult> Parallel<TResult>(
        Action<DurableWorkflowParallelBranchScopeBuilder<TInput, TState, TResult>> branches) =>
        new(TypedAuthoringBoundary.Operations.Parallel<TInput, TState, TResult>(
            implementation,
            scope => branches(new(scope))));

    public DurableForEachJoinBuilder<TInput, TState, TResult> ForEach<TItem, TItemState, TResult>(
        Func<ReadOnlyStateSnapshot<TState>, IReadOnlyList<TItem>> items,
        ForEachOptions options,
        Func<ForEachItemInput<TItem>, TItemState> input,
        Action<DurableItemBuilder<TItemState, TResult>> body) =>
        new(TypedAuthoringBoundary.Operations.ForEach<TInput, TState, TItem, TItemState, TResult>(
            implementation,
            items,
            options,
            input,
            nested => body(new(nested))));

    public DurableWorkflowBuilder<TInput, TState> AcquireResources(
        ResourceLeaseRequest request,
        Action<DurableLeaseWorkflowBuilder<TInput, TState>> body)
    {
        TypedAuthoringBoundary.Operations.AcquireResources<TInput, TState, NoAuthoringValue>(
            implementation,
            request,
            nested => body(new(nested)));
        return this;
    }

    public DurableWorkflowBuilder<TInput, TState> AcquireResources(
        Func<ReadOnlyStateSnapshot<TState>, ResourceLeaseRequest> request,
        Action<DurableLeaseWorkflowBuilder<TInput, TState>> body)
    {
        TypedAuthoringBoundary.Operations.AcquireResources<TInput, TState, NoAuthoringValue>(
            implementation,
            request,
            nested => body(new(nested)));
        return this;
    }

    public DurableWorkflowCompletionBuilder<TInput> ContinueAsNew(
        Func<ReadOnlyStateSnapshot<TState>, TState> replacementState) =>
        new(TypedAuthoringBoundary.Operations.ContinueAsNew<TInput, TState>(implementation, replacementState));

    public DurableWorkflowCompletionBuilder<TInput> End() =>
        new(TypedAuthoringBoundary.Operations.End<TInput, TState>(implementation));

    public DurableWorkflowCompletionBuilder<TInput> End(WorkflowOutcomeName outcome) =>
        new(TypedAuthoringBoundary.Operations.End<TInput, TState>(implementation, outcome));

    public DurableWorkflowCompletionBuilder<TInput, TOutput> End<TOutput>(
        Func<ReadOnlyStateSnapshot<TState>, TOutput> output) =>
        new(TypedAuthoringBoundary.Operations.End<TInput, TState, TOutput>(implementation, output));

    public DurableWorkflowCompletionBuilder<TInput, TOutput> End<TOutput>(
        Func<ReadOnlyStateSnapshot<TState>, TOutput> output,
        WorkflowOutcomeName outcome) =>
        new(TypedAuthoringBoundary.Operations.End<TInput, TState, TOutput>(implementation, output, outcome));
}

public sealed class EphemeralWorkflowCompletionBuilder<TInput>
{
    private readonly AuthoringKernelHandle implementation;
    internal EphemeralWorkflowCompletionBuilder(AuthoringKernelHandle implementation) => this.implementation = implementation;
    [MethodImpl(MethodImplOptions.NoInlining)]
    public EphemeralWorkflowDefinition<TInput> Build() =>
        TypedAuthoringBoundary.Operations.BuildEphemeral<TInput>(implementation);
    public Validation<EphemeralWorkflowDefinition<TInput>> TryBuild() => TypedAuthoringBoundary.Operations.TryBuildEphemeral<TInput>(implementation);
}

public sealed class EphemeralWorkflowCompletionBuilder<TInput, TOutput>
{
    private readonly AuthoringKernelHandle implementation;
    internal EphemeralWorkflowCompletionBuilder(AuthoringKernelHandle implementation) => this.implementation = implementation;
    public EphemeralWorkflowDefinition<TInput, TOutput> Build() => TypedAuthoringBoundary.Operations.BuildEphemeral<TInput, TOutput>(implementation);
    public Validation<EphemeralWorkflowDefinition<TInput, TOutput>> TryBuild() => TypedAuthoringBoundary.Operations.TryBuildEphemeral<TInput, TOutput>(implementation);
}

public sealed class DurableWorkflowCompletionBuilder<TInput>
{
    private readonly AuthoringKernelHandle implementation;
    internal DurableWorkflowCompletionBuilder(AuthoringKernelHandle implementation) => this.implementation = implementation;
    public DurableWorkflowDefinition<TInput> Build() => TypedAuthoringBoundary.Operations.BuildDurable<TInput>(implementation);
    public Validation<DurableWorkflowDefinition<TInput>> TryBuild() => TypedAuthoringBoundary.Operations.TryBuildDurable<TInput>(implementation);
}

public sealed class DurableWorkflowCompletionBuilder<TInput, TOutput>
{
    private readonly AuthoringKernelHandle implementation;
    internal DurableWorkflowCompletionBuilder(AuthoringKernelHandle implementation) => this.implementation = implementation;
    public DurableWorkflowDefinition<TInput, TOutput> Build() => TypedAuthoringBoundary.Operations.BuildDurable<TInput, TOutput>(implementation);
    public Validation<DurableWorkflowDefinition<TInput, TOutput>> TryBuild() => TypedAuthoringBoundary.Operations.TryBuildDurable<TInput, TOutput>(implementation);
}
