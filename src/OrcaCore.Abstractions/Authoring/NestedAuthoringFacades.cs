using OrcaCore.Internal;

namespace OrcaCore;

public sealed class EphemeralNestedBuilder<TInput, TState>
{
    private readonly AuthoringKernelHandle implementation;
    internal EphemeralNestedBuilder(AuthoringKernelHandle implementation) => this.implementation = implementation;

    public EphemeralNestedBuilder<TInput, TState> Then<TStep>() where TStep : IStep<TState> { TypedAuthoringBoundary.Operations.Then<TInput, TState, NoAuthoringValue, TStep>(implementation); return this; }
    public EphemeralNestedBuilder<TInput, TState> Then(Func<StepContext<TState>, ValueTask> body) { TypedAuthoringBoundary.Operations.Then<TInput, TState, NoAuthoringValue>(implementation, body); return this; }
    public EphemeralNestedBuilder<TInput, TState> Then(Func<StepContext<TState>, CancellationToken, ValueTask> body) { TypedAuthoringBoundary.Operations.Then<TInput, TState, NoAuthoringValue>(implementation, body); return this; }
    public EphemeralNestedBuilder<TInput, TState> WithRetry(int maxAttempts, TimeSpan? fixedDelay = null) { TypedAuthoringBoundary.Operations.WithRetry<TInput, TState, NoAuthoringValue>(implementation, maxAttempts, fixedDelay); return this; }
    public EphemeralNestedBuilder<TInput, TState> WithStepTimeout(TimeSpan timeout) { TypedAuthoringBoundary.Operations.WithStepTimeout<TInput, TState, NoAuthoringValue>(implementation, timeout); return this; }
    public EphemeralNestedBuilder<TInput, TState> WithTransientPool(TransientPoolName pool) { TypedAuthoringBoundary.Operations.WithTransientPool<TInput, TState, NoAuthoringValue>(implementation, pool); return this; }

    public EphemeralNestedBuilder<TInput, TState> If(
        Func<ReadOnlyStateSnapshot<TState>, bool> condition,
        Action<EphemeralNestedBuilder<TInput, TState>> then,
        Action<EphemeralNestedBuilder<TInput, TState>>? otherwise = null)
    {
        TypedAuthoringBoundary.Operations.If<TInput, TState, NoAuthoringValue>(implementation, condition, nested => then(new(nested)), otherwise is null ? null : nested => otherwise(new(nested)));
        return this;
    }

    public EphemeralNestedBuilder<TInput, TState> Wait(WorkflowEventContract eventContract, Func<ReadOnlyStateSnapshot<TState>, CorrelationId> correlation) { TypedAuthoringBoundary.Operations.Wait<TInput, TState, NoAuthoringValue>(implementation, eventContract, correlation, null); return this; }
    public EphemeralNestedBuilder<TInput, TState> Wait(WorkflowEventContract eventContract, Func<ReadOnlyStateSnapshot<TState>, CorrelationId> correlation, TimeSpan timeout) { TypedAuthoringBoundary.Operations.Wait<TInput, TState, NoAuthoringValue>(implementation, eventContract, correlation, timeout); return this; }
    public EphemeralNestedBuilder<TInput, TState> Wait<TPayload>(WorkflowEventContract<TPayload> eventContract, Func<ReadOnlyStateSnapshot<TState>, CorrelationId> correlation) => Wait((WorkflowEventContract)eventContract, correlation);
    public EphemeralNestedBuilder<TInput, TState> Wait<TPayload>(WorkflowEventContract<TPayload> eventContract, Func<ReadOnlyStateSnapshot<TState>, CorrelationId> correlation, TimeSpan timeout) => Wait((WorkflowEventContract)eventContract, correlation, timeout);
    public EphemeralNestedBuilder<TInput, TState> Delay(TimeSpan duration) { TypedAuthoringBoundary.Operations.Delay<TInput, TState, NoAuthoringValue>(implementation, duration); return this; }
}

public sealed class DurableNestedBuilder<TInput, TState>
{
    private readonly AuthoringKernelHandle implementation;
    internal DurableNestedBuilder(AuthoringKernelHandle implementation) => this.implementation = implementation;

    public DurableNestedBuilder<TInput, TState> Then<TStep>() where TStep : IStep<TState> { TypedAuthoringBoundary.Operations.Then<TInput, TState, NoAuthoringValue, TStep>(implementation); return this; }
    public DurableNestedBuilder<TInput, TState> WithRetry(int maxAttempts, TimeSpan? fixedDelay = null) { TypedAuthoringBoundary.Operations.WithRetry<TInput, TState, NoAuthoringValue>(implementation, maxAttempts, fixedDelay); return this; }
    public DurableNestedBuilder<TInput, TState> WithStepTimeout(TimeSpan timeout) { TypedAuthoringBoundary.Operations.WithStepTimeout<TInput, TState, NoAuthoringValue>(implementation, timeout); return this; }

    public DurableNestedBuilder<TInput, TState> If(
        Func<ReadOnlyStateSnapshot<TState>, bool> condition,
        Action<DurableNestedBuilder<TInput, TState>> then,
        Action<DurableNestedBuilder<TInput, TState>>? otherwise = null)
    {
        TypedAuthoringBoundary.Operations.If<TInput, TState, NoAuthoringValue>(implementation, condition, nested => then(new(nested)), otherwise is null ? null : nested => otherwise(new(nested)));
        return this;
    }

    public DurableNestedBuilder<TInput, TState> Wait(WorkflowEventContract eventContract, Func<ReadOnlyStateSnapshot<TState>, CorrelationId> correlation) { TypedAuthoringBoundary.Operations.Wait<TInput, TState, NoAuthoringValue>(implementation, eventContract, correlation, null); return this; }
    public DurableNestedBuilder<TInput, TState> Wait(WorkflowEventContract eventContract, Func<ReadOnlyStateSnapshot<TState>, CorrelationId> correlation, TimeSpan timeout) { TypedAuthoringBoundary.Operations.Wait<TInput, TState, NoAuthoringValue>(implementation, eventContract, correlation, timeout); return this; }
    public DurableNestedBuilder<TInput, TState> Wait<TPayload>(WorkflowEventContract<TPayload> eventContract, Func<ReadOnlyStateSnapshot<TState>, CorrelationId> correlation) => Wait((WorkflowEventContract)eventContract, correlation);
    public DurableNestedBuilder<TInput, TState> Wait<TPayload>(WorkflowEventContract<TPayload> eventContract, Func<ReadOnlyStateSnapshot<TState>, CorrelationId> correlation, TimeSpan timeout) => Wait((WorkflowEventContract)eventContract, correlation, timeout);
    public DurableNestedBuilder<TInput, TState> Publish(WorkflowEventContract eventContract, Func<ReadOnlyStateSnapshot<TState>, CorrelationId> correlation) { TypedAuthoringBoundary.Operations.Publish<TInput, TState, NoAuthoringValue>(implementation, eventContract, correlation); return this; }
    public DurableNestedBuilder<TInput, TState> Publish<TPayload>(WorkflowEventContract<TPayload> eventContract, Func<ReadOnlyStateSnapshot<TState>, CorrelationId> correlation, Func<ReadOnlyStateSnapshot<TState>, TPayload> payload) { TypedAuthoringBoundary.Operations.Publish<TInput, TState, NoAuthoringValue, TPayload>(implementation, eventContract, correlation, payload); return this; }
    public DurableNestedBuilder<TInput, TState> Delay(TimeSpan duration) { TypedAuthoringBoundary.Operations.Delay<TInput, TState, NoAuthoringValue>(implementation, duration); return this; }

    public DurableNestedBuilder<TInput, TState> AcquireResources(ResourceLeaseRequest request, Action<DurableLeaseNestedBuilder<TInput, TState>> body)
    {
        TypedAuthoringBoundary.Operations.AcquireResources<TInput, TState, NoAuthoringValue>(implementation, request, nested => body(new(nested)));
        return this;
    }

    public DurableNestedBuilder<TInput, TState> AcquireResources(Func<ReadOnlyStateSnapshot<TState>, ResourceLeaseRequest> request, Action<DurableLeaseNestedBuilder<TInput, TState>> body)
    {
        TypedAuthoringBoundary.Operations.AcquireResources<TInput, TState, NoAuthoringValue>(implementation, request, nested => body(new(nested)));
        return this;
    }
}

public sealed class DurableLeaseWorkflowBuilder<TInput, TState>
{
    private readonly AuthoringKernelHandle implementation;
    internal DurableLeaseWorkflowBuilder(AuthoringKernelHandle implementation) => this.implementation = implementation;

    public DurableLeaseWorkflowBuilder<TInput, TState> Then<TStep>() where TStep : IStep<TState> { TypedAuthoringBoundary.Operations.Then<TInput, TState, NoAuthoringValue, TStep>(implementation); return this; }
    public DurableLeaseWorkflowBuilder<TInput, TState> WithRetry(int maxAttempts, TimeSpan? fixedDelay = null) { TypedAuthoringBoundary.Operations.WithRetry<TInput, TState, NoAuthoringValue>(implementation, maxAttempts, fixedDelay); return this; }
    public DurableLeaseWorkflowBuilder<TInput, TState> WithStepTimeout(TimeSpan timeout) { TypedAuthoringBoundary.Operations.WithStepTimeout<TInput, TState, NoAuthoringValue>(implementation, timeout); return this; }

    public DurableLeaseWorkflowBuilder<TInput, TState> If(
        Func<ReadOnlyStateSnapshot<TState>, bool> condition,
        Action<DurableLeaseNestedBuilder<TInput, TState>> then,
        Action<DurableLeaseNestedBuilder<TInput, TState>>? otherwise = null)
    {
        TypedAuthoringBoundary.Operations.If<TInput, TState, NoAuthoringValue>(implementation, condition, nested => then(new(nested)), otherwise is null ? null : nested => otherwise(new(nested)));
        return this;
    }

    public DurableLeaseWorkflowBuilder<TInput, TState> Wait(WorkflowEventContract eventContract, Func<ReadOnlyStateSnapshot<TState>, CorrelationId> correlation) { TypedAuthoringBoundary.Operations.Wait<TInput, TState, NoAuthoringValue>(implementation, eventContract, correlation, null); return this; }
    public DurableLeaseWorkflowBuilder<TInput, TState> Wait(WorkflowEventContract eventContract, Func<ReadOnlyStateSnapshot<TState>, CorrelationId> correlation, TimeSpan timeout) { TypedAuthoringBoundary.Operations.Wait<TInput, TState, NoAuthoringValue>(implementation, eventContract, correlation, timeout); return this; }
    public DurableLeaseWorkflowBuilder<TInput, TState> Wait<TPayload>(WorkflowEventContract<TPayload> eventContract, Func<ReadOnlyStateSnapshot<TState>, CorrelationId> correlation) => Wait((WorkflowEventContract)eventContract, correlation);
    public DurableLeaseWorkflowBuilder<TInput, TState> Wait<TPayload>(WorkflowEventContract<TPayload> eventContract, Func<ReadOnlyStateSnapshot<TState>, CorrelationId> correlation, TimeSpan timeout) => Wait((WorkflowEventContract)eventContract, correlation, timeout);
    public DurableLeaseWorkflowBuilder<TInput, TState> Publish(WorkflowEventContract eventContract, Func<ReadOnlyStateSnapshot<TState>, CorrelationId> correlation) { TypedAuthoringBoundary.Operations.Publish<TInput, TState, NoAuthoringValue>(implementation, eventContract, correlation); return this; }
    public DurableLeaseWorkflowBuilder<TInput, TState> Publish<TPayload>(WorkflowEventContract<TPayload> eventContract, Func<ReadOnlyStateSnapshot<TState>, CorrelationId> correlation, Func<ReadOnlyStateSnapshot<TState>, TPayload> payload) { TypedAuthoringBoundary.Operations.Publish<TInput, TState, NoAuthoringValue, TPayload>(implementation, eventContract, correlation, payload); return this; }
    public DurableLeaseWorkflowBuilder<TInput, TState> Delay(TimeSpan duration) { TypedAuthoringBoundary.Operations.Delay<TInput, TState, NoAuthoringValue>(implementation, duration); return this; }
}

public sealed class DurableLeaseNestedBuilder<TInput, TState>
{
    private readonly AuthoringKernelHandle implementation;
    internal DurableLeaseNestedBuilder(AuthoringKernelHandle implementation) => this.implementation = implementation;

    public DurableLeaseNestedBuilder<TInput, TState> Then<TStep>() where TStep : IStep<TState> { TypedAuthoringBoundary.Operations.Then<TInput, TState, NoAuthoringValue, TStep>(implementation); return this; }
    public DurableLeaseNestedBuilder<TInput, TState> WithRetry(int maxAttempts, TimeSpan? fixedDelay = null) { TypedAuthoringBoundary.Operations.WithRetry<TInput, TState, NoAuthoringValue>(implementation, maxAttempts, fixedDelay); return this; }
    public DurableLeaseNestedBuilder<TInput, TState> WithStepTimeout(TimeSpan timeout) { TypedAuthoringBoundary.Operations.WithStepTimeout<TInput, TState, NoAuthoringValue>(implementation, timeout); return this; }

    public DurableLeaseNestedBuilder<TInput, TState> If(
        Func<ReadOnlyStateSnapshot<TState>, bool> condition,
        Action<DurableLeaseNestedBuilder<TInput, TState>> then,
        Action<DurableLeaseNestedBuilder<TInput, TState>>? otherwise = null)
    {
        TypedAuthoringBoundary.Operations.If<TInput, TState, NoAuthoringValue>(implementation, condition, nested => then(new(nested)), otherwise is null ? null : nested => otherwise(new(nested)));
        return this;
    }

    public DurableLeaseNestedBuilder<TInput, TState> Wait(WorkflowEventContract eventContract, Func<ReadOnlyStateSnapshot<TState>, CorrelationId> correlation) { TypedAuthoringBoundary.Operations.Wait<TInput, TState, NoAuthoringValue>(implementation, eventContract, correlation, null); return this; }
    public DurableLeaseNestedBuilder<TInput, TState> Wait(WorkflowEventContract eventContract, Func<ReadOnlyStateSnapshot<TState>, CorrelationId> correlation, TimeSpan timeout) { TypedAuthoringBoundary.Operations.Wait<TInput, TState, NoAuthoringValue>(implementation, eventContract, correlation, timeout); return this; }
    public DurableLeaseNestedBuilder<TInput, TState> Wait<TPayload>(WorkflowEventContract<TPayload> eventContract, Func<ReadOnlyStateSnapshot<TState>, CorrelationId> correlation) => Wait((WorkflowEventContract)eventContract, correlation);
    public DurableLeaseNestedBuilder<TInput, TState> Wait<TPayload>(WorkflowEventContract<TPayload> eventContract, Func<ReadOnlyStateSnapshot<TState>, CorrelationId> correlation, TimeSpan timeout) => Wait((WorkflowEventContract)eventContract, correlation, timeout);
    public DurableLeaseNestedBuilder<TInput, TState> Publish(WorkflowEventContract eventContract, Func<ReadOnlyStateSnapshot<TState>, CorrelationId> correlation) { TypedAuthoringBoundary.Operations.Publish<TInput, TState, NoAuthoringValue>(implementation, eventContract, correlation); return this; }
    public DurableLeaseNestedBuilder<TInput, TState> Publish<TPayload>(WorkflowEventContract<TPayload> eventContract, Func<ReadOnlyStateSnapshot<TState>, CorrelationId> correlation, Func<ReadOnlyStateSnapshot<TState>, TPayload> payload) { TypedAuthoringBoundary.Operations.Publish<TInput, TState, NoAuthoringValue, TPayload>(implementation, eventContract, correlation, payload); return this; }
    public DurableLeaseNestedBuilder<TInput, TState> Delay(TimeSpan duration) { TypedAuthoringBoundary.Operations.Delay<TInput, TState, NoAuthoringValue>(implementation, duration); return this; }
}
