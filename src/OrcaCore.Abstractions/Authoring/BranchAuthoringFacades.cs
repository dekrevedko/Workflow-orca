using OrcaCore.Internal;

namespace OrcaCore;

public sealed class EphemeralBranchBuilder<TState, TResult>
{
    private readonly object implementation;
    internal EphemeralBranchBuilder(object implementation) => this.implementation = implementation;

    public EphemeralBranchBuilder<TState, TResult> Then<TStep>() where TStep : IStep<TState>
    {
        AuthoringKernelProxy.Invoke(implementation, nameof(Then), [typeof(TStep)]);
        return this;
    }

    public EphemeralBranchBuilder<TState, TResult> Then(Func<StepContext<TState>, ValueTask> body)
    {
        AuthoringKernelProxy.Invoke(implementation, nameof(Then), Type.EmptyTypes, body);
        return this;
    }

    public EphemeralBranchBuilder<TState, TResult> Then(
        Func<StepContext<TState>, CancellationToken, ValueTask> body)
    {
        AuthoringKernelProxy.Invoke(implementation, nameof(Then), Type.EmptyTypes, body);
        return this;
    }

    public EphemeralBranchBuilder<TState, TResult> WithRetry(
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

    public EphemeralBranchBuilder<TState, TResult> WithStepTimeout(TimeSpan timeout)
    {
        AuthoringKernelProxy.Invoke(implementation, nameof(WithStepTimeout), Type.EmptyTypes, timeout);
        return this;
    }

    public EphemeralBranchBuilder<TState, TResult> WithTransientPool(TransientPoolName pool)
    {
        AuthoringKernelProxy.Invoke(implementation, nameof(WithTransientPool), Type.EmptyTypes, pool);
        return this;
    }

    public EphemeralBranchBuilder<TState, TResult> If(
        Func<ReadOnlyStateSnapshot<TState>, bool> condition,
        Action<EphemeralBranchBuilder<TState, TResult>> then,
        Action<EphemeralBranchBuilder<TState, TResult>>? otherwise = null)
    {
        AuthoringKernelProxy.Invoke(implementation, nameof(If), Type.EmptyTypes, condition, then, otherwise);
        return this;
    }

    public EphemeralBranchBuilder<TState, TResult> Wait(
        WorkflowEventContract eventContract,
        Func<ReadOnlyStateSnapshot<TState>, CorrelationId> correlation)
    {
        AuthoringKernelProxy.Invoke(implementation, nameof(Wait), Type.EmptyTypes, eventContract, correlation);
        return this;
    }

    public EphemeralBranchBuilder<TState, TResult> Wait(
        WorkflowEventContract eventContract,
        Func<ReadOnlyStateSnapshot<TState>, CorrelationId> correlation,
        TimeSpan timeout)
    {
        AuthoringKernelProxy.Invoke(
            implementation,
            nameof(Wait),
            Type.EmptyTypes,
            eventContract,
            correlation,
            timeout);
        return this;
    }

    public EphemeralBranchBuilder<TState, TResult> Wait<TPayload>(
        WorkflowEventContract<TPayload> eventContract,
        Func<ReadOnlyStateSnapshot<TState>, CorrelationId> correlation)
    {
        AuthoringKernelProxy.Invoke(implementation, nameof(Wait), [typeof(TPayload)], eventContract, correlation);
        return this;
    }

    public EphemeralBranchBuilder<TState, TResult> Wait<TPayload>(
        WorkflowEventContract<TPayload> eventContract,
        Func<ReadOnlyStateSnapshot<TState>, CorrelationId> correlation,
        TimeSpan timeout)
    {
        AuthoringKernelProxy.Invoke(implementation, nameof(Wait), [typeof(TPayload)], eventContract, correlation, timeout);
        return this;
    }

    public EphemeralBranchBuilder<TState, TResult> Delay(TimeSpan duration)
    {
        AuthoringKernelProxy.Invoke(implementation, nameof(Delay), Type.EmptyTypes, duration);
        return this;
    }

    public EphemeralBranchBuilder<TState, TResult> Return(
        Func<ReadOnlyStateSnapshot<TState>, TResult> result)
    {
        AuthoringKernelProxy.Invoke(implementation, nameof(Return), Type.EmptyTypes, result);
        return this;
    }
}

public sealed class EphemeralItemBuilder<TState, TResult>
{
    private readonly object implementation;
    internal EphemeralItemBuilder(object implementation) => this.implementation = implementation;

    public EphemeralItemBuilder<TState, TResult> Then<TStep>() where TStep : IStep<TState>
    {
        AuthoringKernelProxy.Invoke(implementation, nameof(Then), [typeof(TStep)]);
        return this;
    }

    public EphemeralItemBuilder<TState, TResult> Then(Func<StepContext<TState>, ValueTask> body)
    {
        AuthoringKernelProxy.Invoke(implementation, nameof(Then), Type.EmptyTypes, body);
        return this;
    }

    public EphemeralItemBuilder<TState, TResult> Then(
        Func<StepContext<TState>, CancellationToken, ValueTask> body)
    {
        AuthoringKernelProxy.Invoke(implementation, nameof(Then), Type.EmptyTypes, body);
        return this;
    }

    public EphemeralItemBuilder<TState, TResult> WithRetry(
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

    public EphemeralItemBuilder<TState, TResult> WithStepTimeout(TimeSpan timeout)
    {
        AuthoringKernelProxy.Invoke(implementation, nameof(WithStepTimeout), Type.EmptyTypes, timeout);
        return this;
    }

    public EphemeralItemBuilder<TState, TResult> WithTransientPool(TransientPoolName pool)
    {
        AuthoringKernelProxy.Invoke(implementation, nameof(WithTransientPool), Type.EmptyTypes, pool);
        return this;
    }

    public EphemeralItemBuilder<TState, TResult> If(
        Func<ReadOnlyStateSnapshot<TState>, bool> condition,
        Action<EphemeralItemBuilder<TState, TResult>> then,
        Action<EphemeralItemBuilder<TState, TResult>>? otherwise = null)
    {
        AuthoringKernelProxy.Invoke(implementation, nameof(If), Type.EmptyTypes, condition, then, otherwise);
        return this;
    }

    public EphemeralItemBuilder<TState, TResult> Wait(
        WorkflowEventContract eventContract,
        Func<ReadOnlyStateSnapshot<TState>, CorrelationId> correlation)
    {
        AuthoringKernelProxy.Invoke(implementation, nameof(Wait), Type.EmptyTypes, eventContract, correlation);
        return this;
    }

    public EphemeralItemBuilder<TState, TResult> Wait(
        WorkflowEventContract eventContract,
        Func<ReadOnlyStateSnapshot<TState>, CorrelationId> correlation,
        TimeSpan timeout)
    {
        AuthoringKernelProxy.Invoke(
            implementation,
            nameof(Wait),
            Type.EmptyTypes,
            eventContract,
            correlation,
            timeout);
        return this;
    }

    public EphemeralItemBuilder<TState, TResult> Wait<TPayload>(
        WorkflowEventContract<TPayload> eventContract,
        Func<ReadOnlyStateSnapshot<TState>, CorrelationId> correlation)
    {
        AuthoringKernelProxy.Invoke(implementation, nameof(Wait), [typeof(TPayload)], eventContract, correlation);
        return this;
    }

    public EphemeralItemBuilder<TState, TResult> Wait<TPayload>(
        WorkflowEventContract<TPayload> eventContract,
        Func<ReadOnlyStateSnapshot<TState>, CorrelationId> correlation,
        TimeSpan timeout)
    {
        AuthoringKernelProxy.Invoke(implementation, nameof(Wait), [typeof(TPayload)], eventContract, correlation, timeout);
        return this;
    }

    public EphemeralItemBuilder<TState, TResult> Delay(TimeSpan duration)
    {
        AuthoringKernelProxy.Invoke(implementation, nameof(Delay), Type.EmptyTypes, duration);
        return this;
    }

    public EphemeralItemBuilder<TState, TResult> Return(
        Func<ReadOnlyStateSnapshot<TState>, TResult> result)
    {
        AuthoringKernelProxy.Invoke(implementation, nameof(Return), Type.EmptyTypes, result);
        return this;
    }
}

public sealed class DurableBranchBuilder<TState, TResult>
{
    private readonly object implementation;
    internal DurableBranchBuilder(object implementation) => this.implementation = implementation;

    public DurableBranchBuilder<TState, TResult> Then<TStep>() where TStep : IStep<TState>
    {
        AuthoringKernelProxy.Invoke(implementation, nameof(Then), [typeof(TStep)]);
        return this;
    }

    public DurableBranchBuilder<TState, TResult> WithRetry(
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

    public DurableBranchBuilder<TState, TResult> WithStepTimeout(TimeSpan timeout)
    {
        AuthoringKernelProxy.Invoke(implementation, nameof(WithStepTimeout), Type.EmptyTypes, timeout);
        return this;
    }

    public DurableBranchBuilder<TState, TResult> If(
        Func<ReadOnlyStateSnapshot<TState>, bool> condition,
        Action<DurableBranchBuilder<TState, TResult>> then,
        Action<DurableBranchBuilder<TState, TResult>>? otherwise = null)
    {
        AuthoringKernelProxy.Invoke(implementation, nameof(If), Type.EmptyTypes, condition, then, otherwise);
        return this;
    }

    public DurableBranchBuilder<TState, TResult> Wait(
        WorkflowEventContract eventContract,
        Func<ReadOnlyStateSnapshot<TState>, CorrelationId> correlation)
    {
        AuthoringKernelProxy.Invoke(implementation, nameof(Wait), Type.EmptyTypes, eventContract, correlation);
        return this;
    }

    public DurableBranchBuilder<TState, TResult> Wait(
        WorkflowEventContract eventContract,
        Func<ReadOnlyStateSnapshot<TState>, CorrelationId> correlation,
        TimeSpan timeout)
    {
        AuthoringKernelProxy.Invoke(
            implementation,
            nameof(Wait),
            Type.EmptyTypes,
            eventContract,
            correlation,
            timeout);
        return this;
    }

    public DurableBranchBuilder<TState, TResult> Wait<TPayload>(
        WorkflowEventContract<TPayload> eventContract,
        Func<ReadOnlyStateSnapshot<TState>, CorrelationId> correlation)
    {
        AuthoringKernelProxy.Invoke(implementation, nameof(Wait), [typeof(TPayload)], eventContract, correlation);
        return this;
    }

    public DurableBranchBuilder<TState, TResult> Publish(
        WorkflowEventContract eventContract,
        Func<ReadOnlyStateSnapshot<TState>, CorrelationId> correlation)
    {
        AuthoringKernelProxy.Invoke(implementation, nameof(Publish), Type.EmptyTypes, eventContract, correlation);
        return this;
    }

    public DurableBranchBuilder<TState, TResult> Publish<TPayload>(
        WorkflowEventContract<TPayload> eventContract,
        Func<ReadOnlyStateSnapshot<TState>, CorrelationId> correlation,
        Func<ReadOnlyStateSnapshot<TState>, TPayload> payload)
    {
        AuthoringKernelProxy.Invoke(implementation, nameof(Publish), [typeof(TPayload)], eventContract, correlation, payload);
        return this;
    }

    public DurableBranchBuilder<TState, TResult> Wait<TPayload>(
        WorkflowEventContract<TPayload> eventContract,
        Func<ReadOnlyStateSnapshot<TState>, CorrelationId> correlation,
        TimeSpan timeout)
    {
        AuthoringKernelProxy.Invoke(implementation, nameof(Wait), [typeof(TPayload)], eventContract, correlation, timeout);
        return this;
    }

    public DurableBranchBuilder<TState, TResult> Delay(TimeSpan duration)
    {
        AuthoringKernelProxy.Invoke(implementation, nameof(Delay), Type.EmptyTypes, duration);
        return this;
    }

    public DurableBranchBuilder<TState, TResult> AcquireResources(
        ResourceLeaseRequest request,
        Action<DurableLeaseBranchBuilder<TState, TResult>> body)
    {
        AuthoringKernelProxy.Invoke(
            implementation,
            nameof(AcquireResources),
            Type.EmptyTypes,
            request,
            body);
        return this;
    }

    public DurableBranchBuilder<TState, TResult> AcquireResources(
        Func<ReadOnlyStateSnapshot<TState>, ResourceLeaseRequest> request,
        Action<DurableLeaseBranchBuilder<TState, TResult>> body)
    {
        AuthoringKernelProxy.Invoke(
            implementation,
            nameof(AcquireResources),
            Type.EmptyTypes,
            request,
            body);
        return this;
    }

    public DurableBranchBuilder<TState, TResult> Return(
        Func<ReadOnlyStateSnapshot<TState>, TResult> result)
    {
        AuthoringKernelProxy.Invoke(implementation, nameof(Return), Type.EmptyTypes, result);
        return this;
    }
}

public sealed class DurableItemBuilder<TState, TResult>
{
    private readonly object implementation;
    internal DurableItemBuilder(object implementation) => this.implementation = implementation;

    public DurableItemBuilder<TState, TResult> Then<TStep>() where TStep : IStep<TState>
    {
        AuthoringKernelProxy.Invoke(implementation, nameof(Then), [typeof(TStep)]);
        return this;
    }

    public DurableItemBuilder<TState, TResult> WithRetry(
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

    public DurableItemBuilder<TState, TResult> WithStepTimeout(TimeSpan timeout)
    {
        AuthoringKernelProxy.Invoke(implementation, nameof(WithStepTimeout), Type.EmptyTypes, timeout);
        return this;
    }

    public DurableItemBuilder<TState, TResult> If(
        Func<ReadOnlyStateSnapshot<TState>, bool> condition,
        Action<DurableItemBuilder<TState, TResult>> then,
        Action<DurableItemBuilder<TState, TResult>>? otherwise = null)
    {
        AuthoringKernelProxy.Invoke(implementation, nameof(If), Type.EmptyTypes, condition, then, otherwise);
        return this;
    }

    public DurableItemBuilder<TState, TResult> Wait(
        WorkflowEventContract eventContract,
        Func<ReadOnlyStateSnapshot<TState>, CorrelationId> correlation)
    {
        AuthoringKernelProxy.Invoke(implementation, nameof(Wait), Type.EmptyTypes, eventContract, correlation);
        return this;
    }

    public DurableItemBuilder<TState, TResult> Wait(
        WorkflowEventContract eventContract,
        Func<ReadOnlyStateSnapshot<TState>, CorrelationId> correlation,
        TimeSpan timeout)
    {
        AuthoringKernelProxy.Invoke(
            implementation,
            nameof(Wait),
            Type.EmptyTypes,
            eventContract,
            correlation,
            timeout);
        return this;
    }

    public DurableItemBuilder<TState, TResult> Wait<TPayload>(
        WorkflowEventContract<TPayload> eventContract,
        Func<ReadOnlyStateSnapshot<TState>, CorrelationId> correlation)
    {
        AuthoringKernelProxy.Invoke(implementation, nameof(Wait), [typeof(TPayload)], eventContract, correlation);
        return this;
    }

    public DurableItemBuilder<TState, TResult> Publish(
        WorkflowEventContract eventContract,
        Func<ReadOnlyStateSnapshot<TState>, CorrelationId> correlation)
    {
        AuthoringKernelProxy.Invoke(implementation, nameof(Publish), Type.EmptyTypes, eventContract, correlation);
        return this;
    }

    public DurableItemBuilder<TState, TResult> Publish<TPayload>(
        WorkflowEventContract<TPayload> eventContract,
        Func<ReadOnlyStateSnapshot<TState>, CorrelationId> correlation,
        Func<ReadOnlyStateSnapshot<TState>, TPayload> payload)
    {
        AuthoringKernelProxy.Invoke(implementation, nameof(Publish), [typeof(TPayload)], eventContract, correlation, payload);
        return this;
    }

    public DurableItemBuilder<TState, TResult> Wait<TPayload>(
        WorkflowEventContract<TPayload> eventContract,
        Func<ReadOnlyStateSnapshot<TState>, CorrelationId> correlation,
        TimeSpan timeout)
    {
        AuthoringKernelProxy.Invoke(implementation, nameof(Wait), [typeof(TPayload)], eventContract, correlation, timeout);
        return this;
    }

    public DurableItemBuilder<TState, TResult> Delay(TimeSpan duration)
    {
        AuthoringKernelProxy.Invoke(implementation, nameof(Delay), Type.EmptyTypes, duration);
        return this;
    }

    public DurableItemBuilder<TState, TResult> AcquireResources(
        ResourceLeaseRequest request,
        Action<DurableLeaseItemBuilder<TState, TResult>> body)
    {
        AuthoringKernelProxy.Invoke(
            implementation,
            nameof(AcquireResources),
            Type.EmptyTypes,
            request,
            body);
        return this;
    }

    public DurableItemBuilder<TState, TResult> AcquireResources(
        Func<ReadOnlyStateSnapshot<TState>, ResourceLeaseRequest> request,
        Action<DurableLeaseItemBuilder<TState, TResult>> body)
    {
        AuthoringKernelProxy.Invoke(
            implementation,
            nameof(AcquireResources),
            Type.EmptyTypes,
            request,
            body);
        return this;
    }

    public DurableItemBuilder<TState, TResult> Return(
        Func<ReadOnlyStateSnapshot<TState>, TResult> result)
    {
        AuthoringKernelProxy.Invoke(implementation, nameof(Return), Type.EmptyTypes, result);
        return this;
    }
}

public sealed class DurableLeaseBranchBuilder<TState, TResult>
{
    private readonly object implementation;
    internal DurableLeaseBranchBuilder(object implementation) => this.implementation = implementation;

    public DurableLeaseBranchBuilder<TState, TResult> Then<TStep>() where TStep : IStep<TState>
    {
        AuthoringKernelProxy.Invoke(implementation, nameof(Then), [typeof(TStep)]);
        return this;
    }

    public DurableLeaseBranchBuilder<TState, TResult> WithRetry(
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

    public DurableLeaseBranchBuilder<TState, TResult> WithStepTimeout(TimeSpan timeout)
    {
        AuthoringKernelProxy.Invoke(implementation, nameof(WithStepTimeout), Type.EmptyTypes, timeout);
        return this;
    }

    public DurableLeaseBranchBuilder<TState, TResult> If(
        Func<ReadOnlyStateSnapshot<TState>, bool> condition,
        Action<DurableLeaseBranchBuilder<TState, TResult>> then,
        Action<DurableLeaseBranchBuilder<TState, TResult>>? otherwise = null)
    {
        AuthoringKernelProxy.Invoke(implementation, nameof(If), Type.EmptyTypes, condition, then, otherwise);
        return this;
    }

    public DurableLeaseBranchBuilder<TState, TResult> Wait(
        WorkflowEventContract eventContract,
        Func<ReadOnlyStateSnapshot<TState>, CorrelationId> correlation)
    {
        AuthoringKernelProxy.Invoke(implementation, nameof(Wait), Type.EmptyTypes, eventContract, correlation);
        return this;
    }

    public DurableLeaseBranchBuilder<TState, TResult> Wait(
        WorkflowEventContract eventContract,
        Func<ReadOnlyStateSnapshot<TState>, CorrelationId> correlation,
        TimeSpan timeout)
    {
        AuthoringKernelProxy.Invoke(
            implementation,
            nameof(Wait),
            Type.EmptyTypes,
            eventContract,
            correlation,
            timeout);
        return this;
    }

    public DurableLeaseBranchBuilder<TState, TResult> Wait<TPayload>(
        WorkflowEventContract<TPayload> eventContract,
        Func<ReadOnlyStateSnapshot<TState>, CorrelationId> correlation)
    {
        AuthoringKernelProxy.Invoke(implementation, nameof(Wait), [typeof(TPayload)], eventContract, correlation);
        return this;
    }

    public DurableLeaseBranchBuilder<TState, TResult> Publish(
        WorkflowEventContract eventContract,
        Func<ReadOnlyStateSnapshot<TState>, CorrelationId> correlation)
    {
        AuthoringKernelProxy.Invoke(implementation, nameof(Publish), Type.EmptyTypes, eventContract, correlation);
        return this;
    }

    public DurableLeaseBranchBuilder<TState, TResult> Publish<TPayload>(
        WorkflowEventContract<TPayload> eventContract,
        Func<ReadOnlyStateSnapshot<TState>, CorrelationId> correlation,
        Func<ReadOnlyStateSnapshot<TState>, TPayload> payload)
    {
        AuthoringKernelProxy.Invoke(implementation, nameof(Publish), [typeof(TPayload)], eventContract, correlation, payload);
        return this;
    }

    public DurableLeaseBranchBuilder<TState, TResult> Wait<TPayload>(
        WorkflowEventContract<TPayload> eventContract,
        Func<ReadOnlyStateSnapshot<TState>, CorrelationId> correlation,
        TimeSpan timeout)
    {
        AuthoringKernelProxy.Invoke(implementation, nameof(Wait), [typeof(TPayload)], eventContract, correlation, timeout);
        return this;
    }

    public DurableLeaseBranchBuilder<TState, TResult> Delay(TimeSpan duration)
    {
        AuthoringKernelProxy.Invoke(implementation, nameof(Delay), Type.EmptyTypes, duration);
        return this;
    }

    public DurableLeaseBranchBuilder<TState, TResult> Return(
        Func<ReadOnlyStateSnapshot<TState>, TResult> result)
    {
        AuthoringKernelProxy.Invoke(implementation, nameof(Return), Type.EmptyTypes, result);
        return this;
    }
}

public sealed class DurableLeaseItemBuilder<TState, TResult>
{
    private readonly object implementation;
    internal DurableLeaseItemBuilder(object implementation) => this.implementation = implementation;

    public DurableLeaseItemBuilder<TState, TResult> Then<TStep>() where TStep : IStep<TState>
    {
        AuthoringKernelProxy.Invoke(implementation, nameof(Then), [typeof(TStep)]);
        return this;
    }

    public DurableLeaseItemBuilder<TState, TResult> WithRetry(
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

    public DurableLeaseItemBuilder<TState, TResult> WithStepTimeout(TimeSpan timeout)
    {
        AuthoringKernelProxy.Invoke(implementation, nameof(WithStepTimeout), Type.EmptyTypes, timeout);
        return this;
    }

    public DurableLeaseItemBuilder<TState, TResult> If(
        Func<ReadOnlyStateSnapshot<TState>, bool> condition,
        Action<DurableLeaseItemBuilder<TState, TResult>> then,
        Action<DurableLeaseItemBuilder<TState, TResult>>? otherwise = null)
    {
        AuthoringKernelProxy.Invoke(implementation, nameof(If), Type.EmptyTypes, condition, then, otherwise);
        return this;
    }

    public DurableLeaseItemBuilder<TState, TResult> Wait(
        WorkflowEventContract eventContract,
        Func<ReadOnlyStateSnapshot<TState>, CorrelationId> correlation)
    {
        AuthoringKernelProxy.Invoke(implementation, nameof(Wait), Type.EmptyTypes, eventContract, correlation);
        return this;
    }

    public DurableLeaseItemBuilder<TState, TResult> Wait(
        WorkflowEventContract eventContract,
        Func<ReadOnlyStateSnapshot<TState>, CorrelationId> correlation,
        TimeSpan timeout)
    {
        AuthoringKernelProxy.Invoke(
            implementation,
            nameof(Wait),
            Type.EmptyTypes,
            eventContract,
            correlation,
            timeout);
        return this;
    }

    public DurableLeaseItemBuilder<TState, TResult> Wait<TPayload>(
        WorkflowEventContract<TPayload> eventContract,
        Func<ReadOnlyStateSnapshot<TState>, CorrelationId> correlation)
    {
        AuthoringKernelProxy.Invoke(implementation, nameof(Wait), [typeof(TPayload)], eventContract, correlation);
        return this;
    }

    public DurableLeaseItemBuilder<TState, TResult> Publish(
        WorkflowEventContract eventContract,
        Func<ReadOnlyStateSnapshot<TState>, CorrelationId> correlation)
    {
        AuthoringKernelProxy.Invoke(implementation, nameof(Publish), Type.EmptyTypes, eventContract, correlation);
        return this;
    }

    public DurableLeaseItemBuilder<TState, TResult> Publish<TPayload>(
        WorkflowEventContract<TPayload> eventContract,
        Func<ReadOnlyStateSnapshot<TState>, CorrelationId> correlation,
        Func<ReadOnlyStateSnapshot<TState>, TPayload> payload)
    {
        AuthoringKernelProxy.Invoke(implementation, nameof(Publish), [typeof(TPayload)], eventContract, correlation, payload);
        return this;
    }

    public DurableLeaseItemBuilder<TState, TResult> Wait<TPayload>(
        WorkflowEventContract<TPayload> eventContract,
        Func<ReadOnlyStateSnapshot<TState>, CorrelationId> correlation,
        TimeSpan timeout)
    {
        AuthoringKernelProxy.Invoke(implementation, nameof(Wait), [typeof(TPayload)], eventContract, correlation, timeout);
        return this;
    }

    public DurableLeaseItemBuilder<TState, TResult> Delay(TimeSpan duration)
    {
        AuthoringKernelProxy.Invoke(implementation, nameof(Delay), Type.EmptyTypes, duration);
        return this;
    }

    public DurableLeaseItemBuilder<TState, TResult> Return(
        Func<ReadOnlyStateSnapshot<TState>, TResult> result)
    {
        AuthoringKernelProxy.Invoke(implementation, nameof(Return), Type.EmptyTypes, result);
        return this;
    }
}

public sealed class EphemeralWorkflowParallelBranchScopeBuilder<TInput, TState, TResult>
{
    private readonly object implementation;
    internal EphemeralWorkflowParallelBranchScopeBuilder(object implementation) =>
        this.implementation = implementation;

    public EphemeralWorkflowParallelBranchScopeBuilder<TInput, TState, TResult> Branch<TBranchState>(
        AuthoredBranchId branchId,
        Func<ReadOnlyStateSnapshot<TState>, TBranchState> input,
        Action<EphemeralBranchBuilder<TBranchState, TResult>> body)
    {
        AuthoringKernelProxy.Invoke(
            implementation,
            nameof(Branch),
            [typeof(TBranchState)],
            branchId,
            input,
            body);
        return this;
    }
}

public sealed class DurableWorkflowParallelBranchScopeBuilder<TInput, TState, TResult>
{
    private readonly object implementation;
    internal DurableWorkflowParallelBranchScopeBuilder(object implementation) =>
        this.implementation = implementation;

    public DurableWorkflowParallelBranchScopeBuilder<TInput, TState, TResult> Branch<TBranchState>(
        AuthoredBranchId branchId,
        Func<ReadOnlyStateSnapshot<TState>, TBranchState> input,
        Action<DurableBranchBuilder<TBranchState, TResult>> body)
    {
        AuthoringKernelProxy.Invoke(
            implementation,
            nameof(Branch),
            [typeof(TBranchState)],
            branchId,
            input,
            body);
        return this;
    }
}

public sealed class EphemeralWorkflowParallelJoinBuilder<TInput, TState, TResult>
{
    private readonly object implementation;
    internal EphemeralWorkflowParallelJoinBuilder(object implementation) =>
        this.implementation = implementation;

    public EphemeralWorkflowBuilder<TInput, TState> WhenAll(
        Func<ReadOnlyStateSnapshot<TState>, IReadOnlyList<BranchResult<TResult>>, TState> merge) =>
        new(AuthoringKernelProxy.Invoke(
            implementation,
            nameof(WhenAll),
            Type.EmptyTypes,
            merge)!);

    public EphemeralWorkflowBuilder<TInput, TState> WhenAllOutcomes(
        Func<ReadOnlyStateSnapshot<TState>, IReadOnlyList<BranchOutcome<TResult>>, TState> merge) =>
        new(AuthoringKernelProxy.Invoke(
            implementation,
            nameof(WhenAllOutcomes),
            Type.EmptyTypes,
            merge)!);
}

public sealed class DurableWorkflowParallelJoinBuilder<TInput, TState, TResult>
{
    private readonly object implementation;
    internal DurableWorkflowParallelJoinBuilder(object implementation) =>
        this.implementation = implementation;

    public DurableWorkflowBuilder<TInput, TState> WhenAll(
        Func<ReadOnlyStateSnapshot<TState>, IReadOnlyList<BranchResult<TResult>>, TState> merge) =>
        new(AuthoringKernelProxy.Invoke(
            implementation,
            nameof(WhenAll),
            Type.EmptyTypes,
            merge)!);

    public DurableWorkflowBuilder<TInput, TState> WhenAllOutcomes(
        Func<ReadOnlyStateSnapshot<TState>, IReadOnlyList<BranchOutcome<TResult>>, TState> merge) =>
        new(AuthoringKernelProxy.Invoke(
            implementation,
            nameof(WhenAllOutcomes),
            Type.EmptyTypes,
            merge)!);
}

public sealed class EphemeralForEachJoinBuilder<TInput, TState, TResult>
{
    private readonly object implementation;
    internal EphemeralForEachJoinBuilder(object implementation) => this.implementation = implementation;

    public EphemeralWorkflowBuilder<TInput, TState> WhenAll(
        Func<ReadOnlyStateSnapshot<TState>, IReadOnlyList<ForEachItemResult<TResult>>, TState> merge) =>
        new(AuthoringKernelProxy.Invoke(
            implementation,
            nameof(WhenAll),
            Type.EmptyTypes,
            merge)!);

    public EphemeralWorkflowBuilder<TInput, TState> WhenAllOutcomes(
        Func<ReadOnlyStateSnapshot<TState>, IReadOnlyList<ForEachItemOutcome<TResult>>, TState> merge) =>
        new(AuthoringKernelProxy.Invoke(
            implementation,
            nameof(WhenAllOutcomes),
            Type.EmptyTypes,
            merge)!);
}

public sealed class DurableForEachJoinBuilder<TInput, TState, TResult>
{
    private readonly object implementation;
    internal DurableForEachJoinBuilder(object implementation) => this.implementation = implementation;

    public DurableWorkflowBuilder<TInput, TState> WhenAll(
        Func<ReadOnlyStateSnapshot<TState>, IReadOnlyList<ForEachItemResult<TResult>>, TState> merge) =>
        new(AuthoringKernelProxy.Invoke(
            implementation,
            nameof(WhenAll),
            Type.EmptyTypes,
            merge)!);

    public DurableWorkflowBuilder<TInput, TState> WhenAllOutcomes(
        Func<ReadOnlyStateSnapshot<TState>, IReadOnlyList<ForEachItemOutcome<TResult>>, TState> merge) =>
        new(AuthoringKernelProxy.Invoke(
            implementation,
            nameof(WhenAllOutcomes),
            Type.EmptyTypes,
            merge)!);
}
