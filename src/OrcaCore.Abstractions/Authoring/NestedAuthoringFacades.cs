using OrcaCore.Internal;

namespace OrcaCore;

public sealed class EphemeralNestedBuilder<TInput, TState>
{
    private readonly object implementation;
    internal EphemeralNestedBuilder(object implementation) => this.implementation = implementation;

    public EphemeralNestedBuilder<TInput, TState> Then<TStep>() where TStep : IStep<TState>
    {
        AuthoringKernelProxy.Invoke(implementation, nameof(Then), [typeof(TStep)]);
        return this;
    }

    public EphemeralNestedBuilder<TInput, TState> Then(Func<StepContext<TState>, ValueTask> body)
    {
        AuthoringKernelProxy.Invoke(implementation, nameof(Then), Type.EmptyTypes, body);
        return this;
    }

    public EphemeralNestedBuilder<TInput, TState> Then(
        Func<StepContext<TState>, CancellationToken, ValueTask> body)
    {
        AuthoringKernelProxy.Invoke(implementation, nameof(Then), Type.EmptyTypes, body);
        return this;
    }

    public EphemeralNestedBuilder<TInput, TState> WithRetry(
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

    public EphemeralNestedBuilder<TInput, TState> WithStepTimeout(TimeSpan timeout)
    {
        AuthoringKernelProxy.Invoke(implementation, nameof(WithStepTimeout), Type.EmptyTypes, timeout);
        return this;
    }

    public EphemeralNestedBuilder<TInput, TState> WithTransientPool(TransientPoolName pool)
    {
        AuthoringKernelProxy.Invoke(implementation, nameof(WithTransientPool), Type.EmptyTypes, pool);
        return this;
    }

    public EphemeralNestedBuilder<TInput, TState> If(
        Func<ReadOnlyStateSnapshot<TState>, bool> condition,
        Action<EphemeralNestedBuilder<TInput, TState>> then,
        Action<EphemeralNestedBuilder<TInput, TState>>? otherwise = null)
    {
        AuthoringKernelProxy.Invoke(implementation, nameof(If), Type.EmptyTypes, condition, then, otherwise);
        return this;
    }

    public EphemeralNestedBuilder<TInput, TState> Wait(
        WorkflowEventContract eventContract,
        Func<ReadOnlyStateSnapshot<TState>, CorrelationId> correlation)
    {
        AuthoringKernelProxy.Invoke(implementation, nameof(Wait), Type.EmptyTypes, eventContract, correlation);
        return this;
    }

    public EphemeralNestedBuilder<TInput, TState> Wait(
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

    public EphemeralNestedBuilder<TInput, TState> Wait<TPayload>(
        WorkflowEventContract<TPayload> eventContract,
        Func<ReadOnlyStateSnapshot<TState>, CorrelationId> correlation)
    {
        AuthoringKernelProxy.Invoke(implementation, nameof(Wait), [typeof(TPayload)], eventContract, correlation);
        return this;
    }

    public EphemeralNestedBuilder<TInput, TState> Wait<TPayload>(
        WorkflowEventContract<TPayload> eventContract,
        Func<ReadOnlyStateSnapshot<TState>, CorrelationId> correlation,
        TimeSpan timeout)
    {
        AuthoringKernelProxy.Invoke(implementation, nameof(Wait), [typeof(TPayload)], eventContract, correlation, timeout);
        return this;
    }

    public EphemeralNestedBuilder<TInput, TState> Delay(TimeSpan duration)
    {
        AuthoringKernelProxy.Invoke(implementation, nameof(Delay), Type.EmptyTypes, duration);
        return this;
    }
}

public sealed class DurableNestedBuilder<TInput, TState>
{
    private readonly object implementation;
    internal DurableNestedBuilder(object implementation) => this.implementation = implementation;

    public DurableNestedBuilder<TInput, TState> Then<TStep>() where TStep : IStep<TState>
    {
        AuthoringKernelProxy.Invoke(implementation, nameof(Then), [typeof(TStep)]);
        return this;
    }

    public DurableNestedBuilder<TInput, TState> WithRetry(
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

    public DurableNestedBuilder<TInput, TState> WithStepTimeout(TimeSpan timeout)
    {
        AuthoringKernelProxy.Invoke(implementation, nameof(WithStepTimeout), Type.EmptyTypes, timeout);
        return this;
    }

    public DurableNestedBuilder<TInput, TState> If(
        Func<ReadOnlyStateSnapshot<TState>, bool> condition,
        Action<DurableNestedBuilder<TInput, TState>> then,
        Action<DurableNestedBuilder<TInput, TState>>? otherwise = null)
    {
        AuthoringKernelProxy.Invoke(implementation, nameof(If), Type.EmptyTypes, condition, then, otherwise);
        return this;
    }

    public DurableNestedBuilder<TInput, TState> Wait(
        WorkflowEventContract eventContract,
        Func<ReadOnlyStateSnapshot<TState>, CorrelationId> correlation)
    {
        AuthoringKernelProxy.Invoke(implementation, nameof(Wait), Type.EmptyTypes, eventContract, correlation);
        return this;
    }

    public DurableNestedBuilder<TInput, TState> Wait(
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

    public DurableNestedBuilder<TInput, TState> Wait<TPayload>(
        WorkflowEventContract<TPayload> eventContract,
        Func<ReadOnlyStateSnapshot<TState>, CorrelationId> correlation)
    {
        AuthoringKernelProxy.Invoke(implementation, nameof(Wait), [typeof(TPayload)], eventContract, correlation);
        return this;
    }

    public DurableNestedBuilder<TInput, TState> Wait<TPayload>(
        WorkflowEventContract<TPayload> eventContract,
        Func<ReadOnlyStateSnapshot<TState>, CorrelationId> correlation,
        TimeSpan timeout)
    {
        AuthoringKernelProxy.Invoke(implementation, nameof(Wait), [typeof(TPayload)], eventContract, correlation, timeout);
        return this;
    }

    public DurableNestedBuilder<TInput, TState> Delay(TimeSpan duration)
    {
        AuthoringKernelProxy.Invoke(implementation, nameof(Delay), Type.EmptyTypes, duration);
        return this;
    }

    public DurableNestedBuilder<TInput, TState> AcquireResources(
        ResourceLeaseRequest request,
        Action<DurableLeaseNestedBuilder<TInput, TState>> body)
    {
        AuthoringKernelProxy.Invoke(
            implementation,
            nameof(AcquireResources),
            Type.EmptyTypes,
            request,
            body);
        return this;
    }

    public DurableNestedBuilder<TInput, TState> AcquireResources(
        Func<ReadOnlyStateSnapshot<TState>, ResourceLeaseRequest> request,
        Action<DurableLeaseNestedBuilder<TInput, TState>> body)
    {
        AuthoringKernelProxy.Invoke(
            implementation,
            nameof(AcquireResources),
            Type.EmptyTypes,
            request,
            body);
        return this;
    }
}

public sealed class DurableLeaseWorkflowBuilder<TInput, TState>
{
    private readonly object implementation;
    internal DurableLeaseWorkflowBuilder(object implementation) => this.implementation = implementation;

    public DurableLeaseWorkflowBuilder<TInput, TState> Then<TStep>() where TStep : IStep<TState>
    {
        AuthoringKernelProxy.Invoke(implementation, nameof(Then), [typeof(TStep)]);
        return this;
    }

    public DurableLeaseWorkflowBuilder<TInput, TState> WithRetry(
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

    public DurableLeaseWorkflowBuilder<TInput, TState> WithStepTimeout(TimeSpan timeout)
    {
        AuthoringKernelProxy.Invoke(implementation, nameof(WithStepTimeout), Type.EmptyTypes, timeout);
        return this;
    }

    public DurableLeaseWorkflowBuilder<TInput, TState> If(
        Func<ReadOnlyStateSnapshot<TState>, bool> condition,
        Action<DurableLeaseNestedBuilder<TInput, TState>> then,
        Action<DurableLeaseNestedBuilder<TInput, TState>>? otherwise = null)
    {
        AuthoringKernelProxy.Invoke(implementation, nameof(If), Type.EmptyTypes, condition, then, otherwise);
        return this;
    }

    public DurableLeaseWorkflowBuilder<TInput, TState> Wait(
        WorkflowEventContract eventContract,
        Func<ReadOnlyStateSnapshot<TState>, CorrelationId> correlation)
    {
        AuthoringKernelProxy.Invoke(implementation, nameof(Wait), Type.EmptyTypes, eventContract, correlation);
        return this;
    }

    public DurableLeaseWorkflowBuilder<TInput, TState> Wait(
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

    public DurableLeaseWorkflowBuilder<TInput, TState> Wait<TPayload>(
        WorkflowEventContract<TPayload> eventContract,
        Func<ReadOnlyStateSnapshot<TState>, CorrelationId> correlation)
    {
        AuthoringKernelProxy.Invoke(implementation, nameof(Wait), [typeof(TPayload)], eventContract, correlation);
        return this;
    }

    public DurableLeaseWorkflowBuilder<TInput, TState> Wait<TPayload>(
        WorkflowEventContract<TPayload> eventContract,
        Func<ReadOnlyStateSnapshot<TState>, CorrelationId> correlation,
        TimeSpan timeout)
    {
        AuthoringKernelProxy.Invoke(implementation, nameof(Wait), [typeof(TPayload)], eventContract, correlation, timeout);
        return this;
    }

    public DurableLeaseWorkflowBuilder<TInput, TState> Delay(TimeSpan duration)
    {
        AuthoringKernelProxy.Invoke(implementation, nameof(Delay), Type.EmptyTypes, duration);
        return this;
    }
}

public sealed class DurableLeaseNestedBuilder<TInput, TState>
{
    private readonly object implementation;
    internal DurableLeaseNestedBuilder(object implementation) => this.implementation = implementation;

    public DurableLeaseNestedBuilder<TInput, TState> Then<TStep>() where TStep : IStep<TState>
    {
        AuthoringKernelProxy.Invoke(implementation, nameof(Then), [typeof(TStep)]);
        return this;
    }

    public DurableLeaseNestedBuilder<TInput, TState> WithRetry(
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

    public DurableLeaseNestedBuilder<TInput, TState> WithStepTimeout(TimeSpan timeout)
    {
        AuthoringKernelProxy.Invoke(implementation, nameof(WithStepTimeout), Type.EmptyTypes, timeout);
        return this;
    }

    public DurableLeaseNestedBuilder<TInput, TState> If(
        Func<ReadOnlyStateSnapshot<TState>, bool> condition,
        Action<DurableLeaseNestedBuilder<TInput, TState>> then,
        Action<DurableLeaseNestedBuilder<TInput, TState>>? otherwise = null)
    {
        AuthoringKernelProxy.Invoke(implementation, nameof(If), Type.EmptyTypes, condition, then, otherwise);
        return this;
    }

    public DurableLeaseNestedBuilder<TInput, TState> Wait(
        WorkflowEventContract eventContract,
        Func<ReadOnlyStateSnapshot<TState>, CorrelationId> correlation)
    {
        AuthoringKernelProxy.Invoke(implementation, nameof(Wait), Type.EmptyTypes, eventContract, correlation);
        return this;
    }

    public DurableLeaseNestedBuilder<TInput, TState> Wait(
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

    public DurableLeaseNestedBuilder<TInput, TState> Wait<TPayload>(
        WorkflowEventContract<TPayload> eventContract,
        Func<ReadOnlyStateSnapshot<TState>, CorrelationId> correlation)
    {
        AuthoringKernelProxy.Invoke(implementation, nameof(Wait), [typeof(TPayload)], eventContract, correlation);
        return this;
    }

    public DurableLeaseNestedBuilder<TInput, TState> Wait<TPayload>(
        WorkflowEventContract<TPayload> eventContract,
        Func<ReadOnlyStateSnapshot<TState>, CorrelationId> correlation,
        TimeSpan timeout)
    {
        AuthoringKernelProxy.Invoke(implementation, nameof(Wait), [typeof(TPayload)], eventContract, correlation, timeout);
        return this;
    }

    public DurableLeaseNestedBuilder<TInput, TState> Delay(TimeSpan duration)
    {
        AuthoringKernelProxy.Invoke(implementation, nameof(Delay), Type.EmptyTypes, duration);
        return this;
    }
}
