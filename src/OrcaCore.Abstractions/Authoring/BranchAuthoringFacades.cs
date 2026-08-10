using OrcaCore.Internal;

namespace OrcaCore;

public sealed class EphemeralBranchBuilder<TState, TResult>
{
    private readonly AuthoringKernelHandle implementation;
    internal EphemeralBranchBuilder(AuthoringKernelHandle implementation) => this.implementation = implementation;
    public EphemeralBranchBuilder<TState, TResult> Then<TStep>() where TStep : IStep<TState> { TypedAuthoringBoundary.Operations.Then<NoAuthoringValue, TState, TResult, TStep>(implementation); return this; }
    public EphemeralBranchBuilder<TState, TResult> Then(Func<StepContext<TState>, ValueTask> body) { TypedAuthoringBoundary.Operations.Then<NoAuthoringValue, TState, TResult>(implementation, body); return this; }
    public EphemeralBranchBuilder<TState, TResult> Then(Func<StepContext<TState>, CancellationToken, ValueTask> body) { TypedAuthoringBoundary.Operations.Then<NoAuthoringValue, TState, TResult>(implementation, body); return this; }
    public EphemeralBranchBuilder<TState, TResult> WithRetry(int maxAttempts, TimeSpan? fixedDelay = null) { TypedAuthoringBoundary.Operations.WithRetry<NoAuthoringValue, TState, TResult>(implementation, maxAttempts, fixedDelay); return this; }
    public EphemeralBranchBuilder<TState, TResult> WithStepTimeout(TimeSpan timeout) { TypedAuthoringBoundary.Operations.WithStepTimeout<NoAuthoringValue, TState, TResult>(implementation, timeout); return this; }
    public EphemeralBranchBuilder<TState, TResult> WithTransientPool(TransientPoolName pool) { TypedAuthoringBoundary.Operations.WithTransientPool<NoAuthoringValue, TState, TResult>(implementation, pool); return this; }
    public EphemeralBranchBuilder<TState, TResult> If(Func<ReadOnlyStateSnapshot<TState>, bool> condition, Action<EphemeralBranchBuilder<TState, TResult>> then, Action<EphemeralBranchBuilder<TState, TResult>>? otherwise = null) { TypedAuthoringBoundary.Operations.If<NoAuthoringValue, TState, TResult>(implementation, condition, nested => then(new(nested)), otherwise is null ? null : nested => otherwise(new(nested))); return this; }
    public EphemeralBranchBuilder<TState, TResult> Wait(WorkflowEventContract eventContract, Func<ReadOnlyStateSnapshot<TState>, CorrelationId> correlation) { TypedAuthoringBoundary.Operations.Wait<NoAuthoringValue, TState, TResult>(implementation, eventContract, correlation, null); return this; }
    public EphemeralBranchBuilder<TState, TResult> Wait(WorkflowEventContract eventContract, Func<ReadOnlyStateSnapshot<TState>, CorrelationId> correlation, TimeSpan timeout) { TypedAuthoringBoundary.Operations.Wait<NoAuthoringValue, TState, TResult>(implementation, eventContract, correlation, timeout); return this; }
    public EphemeralBranchBuilder<TState, TResult> Wait<TPayload>(WorkflowEventContract<TPayload> eventContract, Func<ReadOnlyStateSnapshot<TState>, CorrelationId> correlation) => Wait((WorkflowEventContract)eventContract, correlation);
    public EphemeralBranchBuilder<TState, TResult> Wait<TPayload>(WorkflowEventContract<TPayload> eventContract, Func<ReadOnlyStateSnapshot<TState>, CorrelationId> correlation, TimeSpan timeout) => Wait((WorkflowEventContract)eventContract, correlation, timeout);
    public EphemeralBranchBuilder<TState, TResult> Delay(TimeSpan duration) { TypedAuthoringBoundary.Operations.Delay<NoAuthoringValue, TState, TResult>(implementation, duration); return this; }
    public EphemeralBranchBuilder<TState, TResult> Return(Func<ReadOnlyStateSnapshot<TState>, TResult> result) { TypedAuthoringBoundary.Operations.Return(implementation, result); return this; }
}

public sealed class EphemeralItemBuilder<TState, TResult>
{
    private readonly AuthoringKernelHandle implementation;
    internal EphemeralItemBuilder(AuthoringKernelHandle implementation) => this.implementation = implementation;
    public EphemeralItemBuilder<TState, TResult> Then<TStep>() where TStep : IStep<TState> { TypedAuthoringBoundary.Operations.Then<NoAuthoringValue, TState, TResult, TStep>(implementation); return this; }
    public EphemeralItemBuilder<TState, TResult> Then(Func<StepContext<TState>, ValueTask> body) { TypedAuthoringBoundary.Operations.Then<NoAuthoringValue, TState, TResult>(implementation, body); return this; }
    public EphemeralItemBuilder<TState, TResult> Then(Func<StepContext<TState>, CancellationToken, ValueTask> body) { TypedAuthoringBoundary.Operations.Then<NoAuthoringValue, TState, TResult>(implementation, body); return this; }
    public EphemeralItemBuilder<TState, TResult> WithRetry(int maxAttempts, TimeSpan? fixedDelay = null) { TypedAuthoringBoundary.Operations.WithRetry<NoAuthoringValue, TState, TResult>(implementation, maxAttempts, fixedDelay); return this; }
    public EphemeralItemBuilder<TState, TResult> WithStepTimeout(TimeSpan timeout) { TypedAuthoringBoundary.Operations.WithStepTimeout<NoAuthoringValue, TState, TResult>(implementation, timeout); return this; }
    public EphemeralItemBuilder<TState, TResult> WithTransientPool(TransientPoolName pool) { TypedAuthoringBoundary.Operations.WithTransientPool<NoAuthoringValue, TState, TResult>(implementation, pool); return this; }
    public EphemeralItemBuilder<TState, TResult> If(Func<ReadOnlyStateSnapshot<TState>, bool> condition, Action<EphemeralItemBuilder<TState, TResult>> then, Action<EphemeralItemBuilder<TState, TResult>>? otherwise = null) { TypedAuthoringBoundary.Operations.If<NoAuthoringValue, TState, TResult>(implementation, condition, nested => then(new(nested)), otherwise is null ? null : nested => otherwise(new(nested))); return this; }
    public EphemeralItemBuilder<TState, TResult> Wait(WorkflowEventContract eventContract, Func<ReadOnlyStateSnapshot<TState>, CorrelationId> correlation) { TypedAuthoringBoundary.Operations.Wait<NoAuthoringValue, TState, TResult>(implementation, eventContract, correlation, null); return this; }
    public EphemeralItemBuilder<TState, TResult> Wait(WorkflowEventContract eventContract, Func<ReadOnlyStateSnapshot<TState>, CorrelationId> correlation, TimeSpan timeout) { TypedAuthoringBoundary.Operations.Wait<NoAuthoringValue, TState, TResult>(implementation, eventContract, correlation, timeout); return this; }
    public EphemeralItemBuilder<TState, TResult> Wait<TPayload>(WorkflowEventContract<TPayload> eventContract, Func<ReadOnlyStateSnapshot<TState>, CorrelationId> correlation) => Wait((WorkflowEventContract)eventContract, correlation);
    public EphemeralItemBuilder<TState, TResult> Wait<TPayload>(WorkflowEventContract<TPayload> eventContract, Func<ReadOnlyStateSnapshot<TState>, CorrelationId> correlation, TimeSpan timeout) => Wait((WorkflowEventContract)eventContract, correlation, timeout);
    public EphemeralItemBuilder<TState, TResult> Delay(TimeSpan duration) { TypedAuthoringBoundary.Operations.Delay<NoAuthoringValue, TState, TResult>(implementation, duration); return this; }
    public EphemeralItemBuilder<TState, TResult> Return(Func<ReadOnlyStateSnapshot<TState>, TResult> result) { TypedAuthoringBoundary.Operations.Return(implementation, result); return this; }
}

public sealed class DurableBranchBuilder<TState, TResult>
{
    private readonly AuthoringKernelHandle implementation;
    internal DurableBranchBuilder(AuthoringKernelHandle implementation) => this.implementation = implementation;
    public DurableBranchBuilder<TState, TResult> Then<TStep>() where TStep : IStep<TState> { TypedAuthoringBoundary.Operations.Then<NoAuthoringValue, TState, TResult, TStep>(implementation); return this; }
    public DurableBranchBuilder<TState, TResult> WithRetry(int maxAttempts, TimeSpan? fixedDelay = null) { TypedAuthoringBoundary.Operations.WithRetry<NoAuthoringValue, TState, TResult>(implementation, maxAttempts, fixedDelay); return this; }
    public DurableBranchBuilder<TState, TResult> WithStepTimeout(TimeSpan timeout) { TypedAuthoringBoundary.Operations.WithStepTimeout<NoAuthoringValue, TState, TResult>(implementation, timeout); return this; }
    public DurableBranchBuilder<TState, TResult> If(Func<ReadOnlyStateSnapshot<TState>, bool> condition, Action<DurableBranchBuilder<TState, TResult>> then, Action<DurableBranchBuilder<TState, TResult>>? otherwise = null) { TypedAuthoringBoundary.Operations.If<NoAuthoringValue, TState, TResult>(implementation, condition, nested => then(new(nested)), otherwise is null ? null : nested => otherwise(new(nested))); return this; }
    public DurableBranchBuilder<TState, TResult> Wait(WorkflowEventContract eventContract, Func<ReadOnlyStateSnapshot<TState>, CorrelationId> correlation) { TypedAuthoringBoundary.Operations.Wait<NoAuthoringValue, TState, TResult>(implementation, eventContract, correlation, null); return this; }
    public DurableBranchBuilder<TState, TResult> Wait(WorkflowEventContract eventContract, Func<ReadOnlyStateSnapshot<TState>, CorrelationId> correlation, TimeSpan timeout) { TypedAuthoringBoundary.Operations.Wait<NoAuthoringValue, TState, TResult>(implementation, eventContract, correlation, timeout); return this; }
    public DurableBranchBuilder<TState, TResult> Wait<TPayload>(WorkflowEventContract<TPayload> eventContract, Func<ReadOnlyStateSnapshot<TState>, CorrelationId> correlation) => Wait((WorkflowEventContract)eventContract, correlation);
    public DurableBranchBuilder<TState, TResult> Wait<TPayload>(WorkflowEventContract<TPayload> eventContract, Func<ReadOnlyStateSnapshot<TState>, CorrelationId> correlation, TimeSpan timeout) => Wait((WorkflowEventContract)eventContract, correlation, timeout);
    public DurableBranchBuilder<TState, TResult> Publish(WorkflowEventContract eventContract, Func<ReadOnlyStateSnapshot<TState>, CorrelationId> correlation) { TypedAuthoringBoundary.Operations.Publish<NoAuthoringValue, TState, TResult>(implementation, eventContract, correlation); return this; }
    public DurableBranchBuilder<TState, TResult> Publish<TPayload>(WorkflowEventContract<TPayload> eventContract, Func<ReadOnlyStateSnapshot<TState>, CorrelationId> correlation, Func<ReadOnlyStateSnapshot<TState>, TPayload> payload) { TypedAuthoringBoundary.Operations.Publish<NoAuthoringValue, TState, TResult, TPayload>(implementation, eventContract, correlation, payload); return this; }
    public DurableBranchBuilder<TState, TResult> Delay(TimeSpan duration) { TypedAuthoringBoundary.Operations.Delay<NoAuthoringValue, TState, TResult>(implementation, duration); return this; }
    public DurableBranchBuilder<TState, TResult> AcquireResources(ResourceLeaseRequest request, Action<DurableLeaseBranchBuilder<TState, TResult>> body) { TypedAuthoringBoundary.Operations.AcquireResources<NoAuthoringValue, TState, TResult>(implementation, request, nested => body(new(nested))); return this; }
    public DurableBranchBuilder<TState, TResult> AcquireResources(Func<ReadOnlyStateSnapshot<TState>, ResourceLeaseRequest> request, Action<DurableLeaseBranchBuilder<TState, TResult>> body) { TypedAuthoringBoundary.Operations.AcquireResources<NoAuthoringValue, TState, TResult>(implementation, request, nested => body(new(nested))); return this; }
    public DurableBranchBuilder<TState, TResult> Return(Func<ReadOnlyStateSnapshot<TState>, TResult> result) { TypedAuthoringBoundary.Operations.Return(implementation, result); return this; }
}

public sealed class DurableItemBuilder<TState, TResult>
{
    private readonly AuthoringKernelHandle implementation;
    internal DurableItemBuilder(AuthoringKernelHandle implementation) => this.implementation = implementation;
    public DurableItemBuilder<TState, TResult> Then<TStep>() where TStep : IStep<TState> { TypedAuthoringBoundary.Operations.Then<NoAuthoringValue, TState, TResult, TStep>(implementation); return this; }
    public DurableItemBuilder<TState, TResult> WithRetry(int maxAttempts, TimeSpan? fixedDelay = null) { TypedAuthoringBoundary.Operations.WithRetry<NoAuthoringValue, TState, TResult>(implementation, maxAttempts, fixedDelay); return this; }
    public DurableItemBuilder<TState, TResult> WithStepTimeout(TimeSpan timeout) { TypedAuthoringBoundary.Operations.WithStepTimeout<NoAuthoringValue, TState, TResult>(implementation, timeout); return this; }
    public DurableItemBuilder<TState, TResult> If(Func<ReadOnlyStateSnapshot<TState>, bool> condition, Action<DurableItemBuilder<TState, TResult>> then, Action<DurableItemBuilder<TState, TResult>>? otherwise = null) { TypedAuthoringBoundary.Operations.If<NoAuthoringValue, TState, TResult>(implementation, condition, nested => then(new(nested)), otherwise is null ? null : nested => otherwise(new(nested))); return this; }
    public DurableItemBuilder<TState, TResult> Wait(WorkflowEventContract eventContract, Func<ReadOnlyStateSnapshot<TState>, CorrelationId> correlation) { TypedAuthoringBoundary.Operations.Wait<NoAuthoringValue, TState, TResult>(implementation, eventContract, correlation, null); return this; }
    public DurableItemBuilder<TState, TResult> Wait(WorkflowEventContract eventContract, Func<ReadOnlyStateSnapshot<TState>, CorrelationId> correlation, TimeSpan timeout) { TypedAuthoringBoundary.Operations.Wait<NoAuthoringValue, TState, TResult>(implementation, eventContract, correlation, timeout); return this; }
    public DurableItemBuilder<TState, TResult> Wait<TPayload>(WorkflowEventContract<TPayload> eventContract, Func<ReadOnlyStateSnapshot<TState>, CorrelationId> correlation) => Wait((WorkflowEventContract)eventContract, correlation);
    public DurableItemBuilder<TState, TResult> Wait<TPayload>(WorkflowEventContract<TPayload> eventContract, Func<ReadOnlyStateSnapshot<TState>, CorrelationId> correlation, TimeSpan timeout) => Wait((WorkflowEventContract)eventContract, correlation, timeout);
    public DurableItemBuilder<TState, TResult> Publish(WorkflowEventContract eventContract, Func<ReadOnlyStateSnapshot<TState>, CorrelationId> correlation) { TypedAuthoringBoundary.Operations.Publish<NoAuthoringValue, TState, TResult>(implementation, eventContract, correlation); return this; }
    public DurableItemBuilder<TState, TResult> Publish<TPayload>(WorkflowEventContract<TPayload> eventContract, Func<ReadOnlyStateSnapshot<TState>, CorrelationId> correlation, Func<ReadOnlyStateSnapshot<TState>, TPayload> payload) { TypedAuthoringBoundary.Operations.Publish<NoAuthoringValue, TState, TResult, TPayload>(implementation, eventContract, correlation, payload); return this; }
    public DurableItemBuilder<TState, TResult> Delay(TimeSpan duration) { TypedAuthoringBoundary.Operations.Delay<NoAuthoringValue, TState, TResult>(implementation, duration); return this; }
    public DurableItemBuilder<TState, TResult> AcquireResources(ResourceLeaseRequest request, Action<DurableLeaseItemBuilder<TState, TResult>> body) { TypedAuthoringBoundary.Operations.AcquireResources<NoAuthoringValue, TState, TResult>(implementation, request, nested => body(new(nested))); return this; }
    public DurableItemBuilder<TState, TResult> AcquireResources(Func<ReadOnlyStateSnapshot<TState>, ResourceLeaseRequest> request, Action<DurableLeaseItemBuilder<TState, TResult>> body) { TypedAuthoringBoundary.Operations.AcquireResources<NoAuthoringValue, TState, TResult>(implementation, request, nested => body(new(nested))); return this; }
    public DurableItemBuilder<TState, TResult> Return(Func<ReadOnlyStateSnapshot<TState>, TResult> result) { TypedAuthoringBoundary.Operations.Return(implementation, result); return this; }
}

public sealed class DurableLeaseBranchBuilder<TState, TResult>
{
    private readonly AuthoringKernelHandle implementation;
    internal DurableLeaseBranchBuilder(AuthoringKernelHandle implementation) => this.implementation = implementation;
    public DurableLeaseBranchBuilder<TState, TResult> Then<TStep>() where TStep : IStep<TState> { TypedAuthoringBoundary.Operations.Then<NoAuthoringValue, TState, TResult, TStep>(implementation); return this; }
    public DurableLeaseBranchBuilder<TState, TResult> WithRetry(int maxAttempts, TimeSpan? fixedDelay = null) { TypedAuthoringBoundary.Operations.WithRetry<NoAuthoringValue, TState, TResult>(implementation, maxAttempts, fixedDelay); return this; }
    public DurableLeaseBranchBuilder<TState, TResult> WithStepTimeout(TimeSpan timeout) { TypedAuthoringBoundary.Operations.WithStepTimeout<NoAuthoringValue, TState, TResult>(implementation, timeout); return this; }
    public DurableLeaseBranchBuilder<TState, TResult> If(Func<ReadOnlyStateSnapshot<TState>, bool> condition, Action<DurableLeaseBranchBuilder<TState, TResult>> then, Action<DurableLeaseBranchBuilder<TState, TResult>>? otherwise = null) { TypedAuthoringBoundary.Operations.If<NoAuthoringValue, TState, TResult>(implementation, condition, nested => then(new(nested)), otherwise is null ? null : nested => otherwise(new(nested))); return this; }
    public DurableLeaseBranchBuilder<TState, TResult> Wait(WorkflowEventContract eventContract, Func<ReadOnlyStateSnapshot<TState>, CorrelationId> correlation) { TypedAuthoringBoundary.Operations.Wait<NoAuthoringValue, TState, TResult>(implementation, eventContract, correlation, null); return this; }
    public DurableLeaseBranchBuilder<TState, TResult> Wait(WorkflowEventContract eventContract, Func<ReadOnlyStateSnapshot<TState>, CorrelationId> correlation, TimeSpan timeout) { TypedAuthoringBoundary.Operations.Wait<NoAuthoringValue, TState, TResult>(implementation, eventContract, correlation, timeout); return this; }
    public DurableLeaseBranchBuilder<TState, TResult> Wait<TPayload>(WorkflowEventContract<TPayload> eventContract, Func<ReadOnlyStateSnapshot<TState>, CorrelationId> correlation) => Wait((WorkflowEventContract)eventContract, correlation);
    public DurableLeaseBranchBuilder<TState, TResult> Wait<TPayload>(WorkflowEventContract<TPayload> eventContract, Func<ReadOnlyStateSnapshot<TState>, CorrelationId> correlation, TimeSpan timeout) => Wait((WorkflowEventContract)eventContract, correlation, timeout);
    public DurableLeaseBranchBuilder<TState, TResult> Publish(WorkflowEventContract eventContract, Func<ReadOnlyStateSnapshot<TState>, CorrelationId> correlation) { TypedAuthoringBoundary.Operations.Publish<NoAuthoringValue, TState, TResult>(implementation, eventContract, correlation); return this; }
    public DurableLeaseBranchBuilder<TState, TResult> Publish<TPayload>(WorkflowEventContract<TPayload> eventContract, Func<ReadOnlyStateSnapshot<TState>, CorrelationId> correlation, Func<ReadOnlyStateSnapshot<TState>, TPayload> payload) { TypedAuthoringBoundary.Operations.Publish<NoAuthoringValue, TState, TResult, TPayload>(implementation, eventContract, correlation, payload); return this; }
    public DurableLeaseBranchBuilder<TState, TResult> Delay(TimeSpan duration) { TypedAuthoringBoundary.Operations.Delay<NoAuthoringValue, TState, TResult>(implementation, duration); return this; }
    public DurableLeaseBranchBuilder<TState, TResult> Return(Func<ReadOnlyStateSnapshot<TState>, TResult> result) { TypedAuthoringBoundary.Operations.Return(implementation, result); return this; }
}

public sealed class DurableLeaseItemBuilder<TState, TResult>
{
    private readonly AuthoringKernelHandle implementation;
    internal DurableLeaseItemBuilder(AuthoringKernelHandle implementation) => this.implementation = implementation;
    public DurableLeaseItemBuilder<TState, TResult> Then<TStep>() where TStep : IStep<TState> { TypedAuthoringBoundary.Operations.Then<NoAuthoringValue, TState, TResult, TStep>(implementation); return this; }
    public DurableLeaseItemBuilder<TState, TResult> WithRetry(int maxAttempts, TimeSpan? fixedDelay = null) { TypedAuthoringBoundary.Operations.WithRetry<NoAuthoringValue, TState, TResult>(implementation, maxAttempts, fixedDelay); return this; }
    public DurableLeaseItemBuilder<TState, TResult> WithStepTimeout(TimeSpan timeout) { TypedAuthoringBoundary.Operations.WithStepTimeout<NoAuthoringValue, TState, TResult>(implementation, timeout); return this; }
    public DurableLeaseItemBuilder<TState, TResult> If(Func<ReadOnlyStateSnapshot<TState>, bool> condition, Action<DurableLeaseItemBuilder<TState, TResult>> then, Action<DurableLeaseItemBuilder<TState, TResult>>? otherwise = null) { TypedAuthoringBoundary.Operations.If<NoAuthoringValue, TState, TResult>(implementation, condition, nested => then(new(nested)), otherwise is null ? null : nested => otherwise(new(nested))); return this; }
    public DurableLeaseItemBuilder<TState, TResult> Wait(WorkflowEventContract eventContract, Func<ReadOnlyStateSnapshot<TState>, CorrelationId> correlation) { TypedAuthoringBoundary.Operations.Wait<NoAuthoringValue, TState, TResult>(implementation, eventContract, correlation, null); return this; }
    public DurableLeaseItemBuilder<TState, TResult> Wait(WorkflowEventContract eventContract, Func<ReadOnlyStateSnapshot<TState>, CorrelationId> correlation, TimeSpan timeout) { TypedAuthoringBoundary.Operations.Wait<NoAuthoringValue, TState, TResult>(implementation, eventContract, correlation, timeout); return this; }
    public DurableLeaseItemBuilder<TState, TResult> Wait<TPayload>(WorkflowEventContract<TPayload> eventContract, Func<ReadOnlyStateSnapshot<TState>, CorrelationId> correlation) => Wait((WorkflowEventContract)eventContract, correlation);
    public DurableLeaseItemBuilder<TState, TResult> Wait<TPayload>(WorkflowEventContract<TPayload> eventContract, Func<ReadOnlyStateSnapshot<TState>, CorrelationId> correlation, TimeSpan timeout) => Wait((WorkflowEventContract)eventContract, correlation, timeout);
    public DurableLeaseItemBuilder<TState, TResult> Publish(WorkflowEventContract eventContract, Func<ReadOnlyStateSnapshot<TState>, CorrelationId> correlation) { TypedAuthoringBoundary.Operations.Publish<NoAuthoringValue, TState, TResult>(implementation, eventContract, correlation); return this; }
    public DurableLeaseItemBuilder<TState, TResult> Publish<TPayload>(WorkflowEventContract<TPayload> eventContract, Func<ReadOnlyStateSnapshot<TState>, CorrelationId> correlation, Func<ReadOnlyStateSnapshot<TState>, TPayload> payload) { TypedAuthoringBoundary.Operations.Publish<NoAuthoringValue, TState, TResult, TPayload>(implementation, eventContract, correlation, payload); return this; }
    public DurableLeaseItemBuilder<TState, TResult> Delay(TimeSpan duration) { TypedAuthoringBoundary.Operations.Delay<NoAuthoringValue, TState, TResult>(implementation, duration); return this; }
    public DurableLeaseItemBuilder<TState, TResult> Return(Func<ReadOnlyStateSnapshot<TState>, TResult> result) { TypedAuthoringBoundary.Operations.Return(implementation, result); return this; }
}

public sealed class EphemeralWorkflowParallelBranchScopeBuilder<TInput, TState, TResult>
{
    private readonly AuthoringKernelHandle implementation;
    internal EphemeralWorkflowParallelBranchScopeBuilder(AuthoringKernelHandle implementation) => this.implementation = implementation;
    public EphemeralWorkflowParallelBranchScopeBuilder<TInput, TState, TResult> Branch<TBranchState>(AuthoredBranchId branchId, Func<ReadOnlyStateSnapshot<TState>, TBranchState> input, Action<EphemeralBranchBuilder<TBranchState, TResult>> body) { TypedAuthoringBoundary.Operations.Branch<TInput, TState, TResult, TBranchState>(implementation, branchId, input, nested => body(new(nested))); return this; }
}

public sealed class DurableWorkflowParallelBranchScopeBuilder<TInput, TState, TResult>
{
    private readonly AuthoringKernelHandle implementation;
    internal DurableWorkflowParallelBranchScopeBuilder(AuthoringKernelHandle implementation) => this.implementation = implementation;
    public DurableWorkflowParallelBranchScopeBuilder<TInput, TState, TResult> Branch<TBranchState>(AuthoredBranchId branchId, Func<ReadOnlyStateSnapshot<TState>, TBranchState> input, Action<DurableBranchBuilder<TBranchState, TResult>> body) { TypedAuthoringBoundary.Operations.Branch<TInput, TState, TResult, TBranchState>(implementation, branchId, input, nested => body(new(nested))); return this; }
}

public sealed class EphemeralWorkflowParallelJoinBuilder<TInput, TState, TResult>
{
    private readonly AuthoringKernelHandle implementation;
    internal EphemeralWorkflowParallelJoinBuilder(AuthoringKernelHandle implementation) => this.implementation = implementation;
    public EphemeralWorkflowBuilder<TInput, TState> WhenAll(Func<ReadOnlyStateSnapshot<TState>, IReadOnlyList<BranchResult<TResult>>, TState> merge) => new(TypedAuthoringBoundary.Operations.ParallelWhenAll<TInput, TState, TResult>(implementation, merge));
    public EphemeralWorkflowBuilder<TInput, TState> WhenAllOutcomes(Func<ReadOnlyStateSnapshot<TState>, IReadOnlyList<BranchOutcome<TResult>>, TState> merge) => new(TypedAuthoringBoundary.Operations.ParallelWhenAllOutcomes<TInput, TState, TResult>(implementation, merge));
}

public sealed class DurableWorkflowParallelJoinBuilder<TInput, TState, TResult>
{
    private readonly AuthoringKernelHandle implementation;
    internal DurableWorkflowParallelJoinBuilder(AuthoringKernelHandle implementation) => this.implementation = implementation;
    public DurableWorkflowBuilder<TInput, TState> WhenAll(Func<ReadOnlyStateSnapshot<TState>, IReadOnlyList<BranchResult<TResult>>, TState> merge) => new(TypedAuthoringBoundary.Operations.ParallelWhenAll<TInput, TState, TResult>(implementation, merge));
    public DurableWorkflowBuilder<TInput, TState> WhenAllOutcomes(Func<ReadOnlyStateSnapshot<TState>, IReadOnlyList<BranchOutcome<TResult>>, TState> merge) => new(TypedAuthoringBoundary.Operations.ParallelWhenAllOutcomes<TInput, TState, TResult>(implementation, merge));
}

public sealed class EphemeralForEachJoinBuilder<TInput, TState, TResult>
{
    private readonly AuthoringKernelHandle implementation;
    internal EphemeralForEachJoinBuilder(AuthoringKernelHandle implementation) => this.implementation = implementation;
    public EphemeralWorkflowBuilder<TInput, TState> WhenAll(Func<ReadOnlyStateSnapshot<TState>, IReadOnlyList<ForEachItemResult<TResult>>, TState> merge) => new(TypedAuthoringBoundary.Operations.ForEachWhenAll<TInput, TState, TResult>(implementation, merge));
    public EphemeralWorkflowBuilder<TInput, TState> WhenAllOutcomes(Func<ReadOnlyStateSnapshot<TState>, IReadOnlyList<ForEachItemOutcome<TResult>>, TState> merge) => new(TypedAuthoringBoundary.Operations.ForEachWhenAllOutcomes<TInput, TState, TResult>(implementation, merge));
}

public sealed class DurableForEachJoinBuilder<TInput, TState, TResult>
{
    private readonly AuthoringKernelHandle implementation;
    internal DurableForEachJoinBuilder(AuthoringKernelHandle implementation) => this.implementation = implementation;
    public DurableWorkflowBuilder<TInput, TState> WhenAll(Func<ReadOnlyStateSnapshot<TState>, IReadOnlyList<ForEachItemResult<TResult>>, TState> merge) => new(TypedAuthoringBoundary.Operations.ForEachWhenAll<TInput, TState, TResult>(implementation, merge));
    public DurableWorkflowBuilder<TInput, TState> WhenAllOutcomes(Func<ReadOnlyStateSnapshot<TState>, IReadOnlyList<ForEachItemOutcome<TResult>>, TState> merge) => new(TypedAuthoringBoundary.Operations.ForEachWhenAllOutcomes<TInput, TState, TResult>(implementation, merge));
}
