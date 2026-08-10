using System.Runtime.CompilerServices;
using OrcaCore.Internal;

namespace OrcaCore.Core.Authoring;

internal sealed class TypedAuthoringOperations : ITypedAuthoringOperations
{
    internal static TypedAuthoringOperations Instance { get; } = new();

    private TypedAuthoringOperations()
    {
    }

    public AuthoringKernelHandle Ephemeral<TState>(DefinitionId definitionId, DefinitionVersion definitionVersion) =>
        Handle(Workflow.Ephemeral<TState>(definitionId, definitionVersion));

    public AuthoringKernelHandle Durable<TState>(DefinitionId definitionId, DefinitionVersion definitionVersion) =>
        Handle(Workflow.Durable<TState>(definitionId, definitionVersion));

    public AuthoringKernelHandle Init<TInput, TState>(
        AuthoringKernelHandle handle,
        Func<TInput, TState> createState) =>
        handle.Value switch
        {
            EphemeralWorkflowInitBuilder<TState> builder => Handle(builder.Init(createState)),
            DurableWorkflowInitBuilder<TState> builder => Handle(builder.Init(createState)),
            _ => Invalid<AuthoringKernelHandle>(handle, nameof(Init))
        };

    public void Then<TInput, TState, TResult, TStep>(AuthoringKernelHandle handle)
        where TStep : IStep<TState>
    {
        switch (handle.Value)
        {
            case EphemeralWorkflowBuilder<TInput, TState> builder: builder.Then<TStep>(); break;
            case DurableWorkflowBuilder<TInput, TState> builder: builder.Then<TStep>(); break;
            case EphemeralNestedBuilder<TInput, TState> builder: builder.Then<TStep>(); break;
            case DurableNestedBuilder<TInput, TState> builder: builder.Then<TStep>(); break;
            case DurableLeaseWorkflowBuilder<TInput, TState> builder: builder.Then<TStep>(); break;
            case DurableLeaseNestedBuilder<TInput, TState> builder: builder.Then<TStep>(); break;
            case EphemeralBranchBuilder<TState, TResult> builder: builder.Then<TStep>(); break;
            case EphemeralItemBuilder<TState, TResult> builder: builder.Then<TStep>(); break;
            case DurableBranchBuilder<TState, TResult> builder: builder.Then<TStep>(); break;
            case DurableItemBuilder<TState, TResult> builder: builder.Then<TStep>(); break;
            case DurableLeaseBranchBuilder<TState, TResult> builder: builder.Then<TStep>(); break;
            case DurableLeaseItemBuilder<TState, TResult> builder: builder.Then<TStep>(); break;
            default: Invalid(handle, nameof(Then)); break;
        }
    }

    public void Then<TInput, TState, TResult>(
        AuthoringKernelHandle handle,
        Func<StepContext<TState>, ValueTask> body)
    {
        switch (handle.Value)
        {
            case EphemeralWorkflowBuilder<TInput, TState> builder: builder.Then(body); break;
            case EphemeralNestedBuilder<TInput, TState> builder: builder.Then(body); break;
            case EphemeralBranchBuilder<TState, TResult> builder: builder.Then(body); break;
            case EphemeralItemBuilder<TState, TResult> builder: builder.Then(body); break;
            default: Invalid(handle, nameof(Then)); break;
        }
    }

    public void Then<TInput, TState, TResult>(
        AuthoringKernelHandle handle,
        Func<StepContext<TState>, CancellationToken, ValueTask> body)
    {
        switch (handle.Value)
        {
            case EphemeralWorkflowBuilder<TInput, TState> builder: builder.Then(body); break;
            case EphemeralNestedBuilder<TInput, TState> builder: builder.Then(body); break;
            case EphemeralBranchBuilder<TState, TResult> builder: builder.Then(body); break;
            case EphemeralItemBuilder<TState, TResult> builder: builder.Then(body); break;
            default: Invalid(handle, nameof(Then)); break;
        }
    }

    public void WithRetry<TInput, TState, TResult>(
        AuthoringKernelHandle handle,
        int maxAttempts,
        TimeSpan? fixedDelay)
    {
        switch (handle.Value)
        {
            case EphemeralWorkflowBuilder<TInput, TState> builder: builder.WithRetry(maxAttempts, fixedDelay); break;
            case DurableWorkflowBuilder<TInput, TState> builder: builder.WithRetry(maxAttempts, fixedDelay); break;
            case EphemeralNestedBuilder<TInput, TState> builder: builder.WithRetry(maxAttempts, fixedDelay); break;
            case DurableNestedBuilder<TInput, TState> builder: builder.WithRetry(maxAttempts, fixedDelay); break;
            case DurableLeaseWorkflowBuilder<TInput, TState> builder: builder.WithRetry(maxAttempts, fixedDelay); break;
            case DurableLeaseNestedBuilder<TInput, TState> builder: builder.WithRetry(maxAttempts, fixedDelay); break;
            case EphemeralBranchBuilder<TState, TResult> builder: builder.WithRetry(maxAttempts, fixedDelay); break;
            case EphemeralItemBuilder<TState, TResult> builder: builder.WithRetry(maxAttempts, fixedDelay); break;
            case DurableBranchBuilder<TState, TResult> builder: builder.WithRetry(maxAttempts, fixedDelay); break;
            case DurableItemBuilder<TState, TResult> builder: builder.WithRetry(maxAttempts, fixedDelay); break;
            case DurableLeaseBranchBuilder<TState, TResult> builder: builder.WithRetry(maxAttempts, fixedDelay); break;
            case DurableLeaseItemBuilder<TState, TResult> builder: builder.WithRetry(maxAttempts, fixedDelay); break;
            default: Invalid(handle, nameof(WithRetry)); break;
        }
    }

    public void WithStepTimeout<TInput, TState, TResult>(AuthoringKernelHandle handle, TimeSpan timeout)
    {
        switch (handle.Value)
        {
            case EphemeralWorkflowBuilder<TInput, TState> builder: builder.WithStepTimeout(timeout); break;
            case DurableWorkflowBuilder<TInput, TState> builder: builder.WithStepTimeout(timeout); break;
            case EphemeralNestedBuilder<TInput, TState> builder: builder.WithStepTimeout(timeout); break;
            case DurableNestedBuilder<TInput, TState> builder: builder.WithStepTimeout(timeout); break;
            case DurableLeaseWorkflowBuilder<TInput, TState> builder: builder.WithStepTimeout(timeout); break;
            case DurableLeaseNestedBuilder<TInput, TState> builder: builder.WithStepTimeout(timeout); break;
            case EphemeralBranchBuilder<TState, TResult> builder: builder.WithStepTimeout(timeout); break;
            case EphemeralItemBuilder<TState, TResult> builder: builder.WithStepTimeout(timeout); break;
            case DurableBranchBuilder<TState, TResult> builder: builder.WithStepTimeout(timeout); break;
            case DurableItemBuilder<TState, TResult> builder: builder.WithStepTimeout(timeout); break;
            case DurableLeaseBranchBuilder<TState, TResult> builder: builder.WithStepTimeout(timeout); break;
            case DurableLeaseItemBuilder<TState, TResult> builder: builder.WithStepTimeout(timeout); break;
            default: Invalid(handle, nameof(WithStepTimeout)); break;
        }
    }

    public void WithTransientPool<TInput, TState, TResult>(
        AuthoringKernelHandle handle,
        TransientPoolName pool)
    {
        switch (handle.Value)
        {
            case EphemeralWorkflowBuilder<TInput, TState> builder: builder.WithTransientPool(pool); break;
            case EphemeralNestedBuilder<TInput, TState> builder: builder.WithTransientPool(pool); break;
            case EphemeralBranchBuilder<TState, TResult> builder: builder.WithTransientPool(pool); break;
            case EphemeralItemBuilder<TState, TResult> builder: builder.WithTransientPool(pool); break;
            default: Invalid(handle, nameof(WithTransientPool)); break;
        }
    }

    public void CompleteWithin<TInput, TState>(AuthoringKernelHandle handle, TimeSpan timeout)
    {
        switch (handle.Value)
        {
            case EphemeralWorkflowBuilder<TInput, TState> builder: builder.CompleteWithin(timeout); break;
            case DurableWorkflowBuilder<TInput, TState> builder: builder.CompleteWithin(timeout); break;
            default: Invalid(handle, nameof(CompleteWithin)); break;
        }
    }

    public void If<TInput, TState, TResult>(
        AuthoringKernelHandle handle,
        Func<ReadOnlyStateSnapshot<TState>, bool> condition,
        Action<AuthoringKernelHandle> then,
        Action<AuthoringKernelHandle>? otherwise)
    {
        switch (handle.Value)
        {
            case EphemeralWorkflowBuilder<TInput, TState> builder:
                builder.If(condition, nested => then(Handle(nested)), Wrap<EphemeralNestedBuilder<TInput, TState>>(otherwise));
                break;
            case DurableWorkflowBuilder<TInput, TState> builder:
                builder.If(condition, nested => then(Handle(nested)), Wrap<DurableNestedBuilder<TInput, TState>>(otherwise));
                break;
            case EphemeralNestedBuilder<TInput, TState> builder:
                builder.If(condition, nested => then(Handle(nested)), Wrap<EphemeralNestedBuilder<TInput, TState>>(otherwise));
                break;
            case DurableNestedBuilder<TInput, TState> builder:
                builder.If(condition, nested => then(Handle(nested)), Wrap<DurableNestedBuilder<TInput, TState>>(otherwise));
                break;
            case DurableLeaseWorkflowBuilder<TInput, TState> builder:
                builder.If(condition, nested => then(Handle(nested)), Wrap<DurableLeaseNestedBuilder<TInput, TState>>(otherwise));
                break;
            case DurableLeaseNestedBuilder<TInput, TState> builder:
                builder.If(condition, nested => then(Handle(nested)), Wrap<DurableLeaseNestedBuilder<TInput, TState>>(otherwise));
                break;
            case EphemeralBranchBuilder<TState, TResult> builder:
                builder.If(condition, nested => then(Handle(nested)), Wrap<EphemeralBranchBuilder<TState, TResult>>(otherwise));
                break;
            case EphemeralItemBuilder<TState, TResult> builder:
                builder.If(condition, nested => then(Handle(nested)), Wrap<EphemeralItemBuilder<TState, TResult>>(otherwise));
                break;
            case DurableBranchBuilder<TState, TResult> builder:
                builder.If(condition, nested => then(Handle(nested)), Wrap<DurableBranchBuilder<TState, TResult>>(otherwise));
                break;
            case DurableItemBuilder<TState, TResult> builder:
                builder.If(condition, nested => then(Handle(nested)), Wrap<DurableItemBuilder<TState, TResult>>(otherwise));
                break;
            case DurableLeaseBranchBuilder<TState, TResult> builder:
                builder.If(condition, nested => then(Handle(nested)), Wrap<DurableLeaseBranchBuilder<TState, TResult>>(otherwise));
                break;
            case DurableLeaseItemBuilder<TState, TResult> builder:
                builder.If(condition, nested => then(Handle(nested)), Wrap<DurableLeaseItemBuilder<TState, TResult>>(otherwise));
                break;
            default: Invalid(handle, nameof(If)); break;
        }
    }

    public void While<TInput, TState>(
        AuthoringKernelHandle handle,
        Func<ReadOnlyStateSnapshot<TState>, bool> condition,
        Action<AuthoringKernelHandle> body)
    {
        switch (handle.Value)
        {
            case EphemeralWorkflowBuilder<TInput, TState> builder:
                builder.While(condition, nested => body(Handle(nested)));
                break;
            case DurableWorkflowBuilder<TInput, TState> builder:
                builder.While(condition, nested => body(Handle(nested)));
                break;
            default: Invalid(handle, nameof(While)); break;
        }
    }

    public void Wait<TInput, TState, TResult>(
        AuthoringKernelHandle handle,
        WorkflowEventContract eventContract,
        Func<ReadOnlyStateSnapshot<TState>, CorrelationId> correlation,
        TimeSpan? timeout)
    {
        switch (handle.Value)
        {
            case EphemeralWorkflowBuilder<TInput, TState> builder: WaitOn(builder, eventContract, correlation, timeout); break;
            case DurableWorkflowBuilder<TInput, TState> builder: WaitOn(builder, eventContract, correlation, timeout); break;
            case EphemeralNestedBuilder<TInput, TState> builder: WaitOn(builder, eventContract, correlation, timeout); break;
            case DurableNestedBuilder<TInput, TState> builder: WaitOn(builder, eventContract, correlation, timeout); break;
            case DurableLeaseWorkflowBuilder<TInput, TState> builder: WaitOn(builder, eventContract, correlation, timeout); break;
            case DurableLeaseNestedBuilder<TInput, TState> builder: WaitOn(builder, eventContract, correlation, timeout); break;
            case EphemeralBranchBuilder<TState, TResult> builder: WaitOn(builder, eventContract, correlation, timeout); break;
            case EphemeralItemBuilder<TState, TResult> builder: WaitOn(builder, eventContract, correlation, timeout); break;
            case DurableBranchBuilder<TState, TResult> builder: WaitOn(builder, eventContract, correlation, timeout); break;
            case DurableItemBuilder<TState, TResult> builder: WaitOn(builder, eventContract, correlation, timeout); break;
            case DurableLeaseBranchBuilder<TState, TResult> builder: WaitOn(builder, eventContract, correlation, timeout); break;
            case DurableLeaseItemBuilder<TState, TResult> builder: WaitOn(builder, eventContract, correlation, timeout); break;
            default: Invalid(handle, nameof(Wait)); break;
        }
    }

    public void Publish<TInput, TState, TResult>(
        AuthoringKernelHandle handle,
        WorkflowEventContract eventContract,
        Func<ReadOnlyStateSnapshot<TState>, CorrelationId> correlation)
    {
        switch (handle.Value)
        {
            case DurableWorkflowBuilder<TInput, TState> builder: builder.Publish(eventContract, correlation); break;
            case DurableNestedBuilder<TInput, TState> builder: builder.Publish(eventContract, correlation); break;
            case DurableLeaseWorkflowBuilder<TInput, TState> builder: builder.Publish(eventContract, correlation); break;
            case DurableLeaseNestedBuilder<TInput, TState> builder: builder.Publish(eventContract, correlation); break;
            case DurableBranchBuilder<TState, TResult> builder: builder.Publish(eventContract, correlation); break;
            case DurableItemBuilder<TState, TResult> builder: builder.Publish(eventContract, correlation); break;
            case DurableLeaseBranchBuilder<TState, TResult> builder: builder.Publish(eventContract, correlation); break;
            case DurableLeaseItemBuilder<TState, TResult> builder: builder.Publish(eventContract, correlation); break;
            default: Invalid(handle, nameof(Publish)); break;
        }
    }

    public void Publish<TInput, TState, TResult, TPayload>(
        AuthoringKernelHandle handle,
        WorkflowEventContract<TPayload> eventContract,
        Func<ReadOnlyStateSnapshot<TState>, CorrelationId> correlation,
        Func<ReadOnlyStateSnapshot<TState>, TPayload> payload)
    {
        switch (handle.Value)
        {
            case DurableWorkflowBuilder<TInput, TState> builder: builder.Publish(eventContract, correlation, payload); break;
            case DurableNestedBuilder<TInput, TState> builder: builder.Publish(eventContract, correlation, payload); break;
            case DurableLeaseWorkflowBuilder<TInput, TState> builder: builder.Publish(eventContract, correlation, payload); break;
            case DurableLeaseNestedBuilder<TInput, TState> builder: builder.Publish(eventContract, correlation, payload); break;
            case DurableBranchBuilder<TState, TResult> builder: builder.Publish(eventContract, correlation, payload); break;
            case DurableItemBuilder<TState, TResult> builder: builder.Publish(eventContract, correlation, payload); break;
            case DurableLeaseBranchBuilder<TState, TResult> builder: builder.Publish(eventContract, correlation, payload); break;
            case DurableLeaseItemBuilder<TState, TResult> builder: builder.Publish(eventContract, correlation, payload); break;
            default: Invalid(handle, nameof(Publish)); break;
        }
    }

    public void Delay<TInput, TState, TResult>(AuthoringKernelHandle handle, TimeSpan duration)
    {
        switch (handle.Value)
        {
            case EphemeralWorkflowBuilder<TInput, TState> builder: builder.Delay(duration); break;
            case DurableWorkflowBuilder<TInput, TState> builder: builder.Delay(duration); break;
            case EphemeralNestedBuilder<TInput, TState> builder: builder.Delay(duration); break;
            case DurableNestedBuilder<TInput, TState> builder: builder.Delay(duration); break;
            case DurableLeaseWorkflowBuilder<TInput, TState> builder: builder.Delay(duration); break;
            case DurableLeaseNestedBuilder<TInput, TState> builder: builder.Delay(duration); break;
            case EphemeralBranchBuilder<TState, TResult> builder: builder.Delay(duration); break;
            case EphemeralItemBuilder<TState, TResult> builder: builder.Delay(duration); break;
            case DurableBranchBuilder<TState, TResult> builder: builder.Delay(duration); break;
            case DurableItemBuilder<TState, TResult> builder: builder.Delay(duration); break;
            case DurableLeaseBranchBuilder<TState, TResult> builder: builder.Delay(duration); break;
            case DurableLeaseItemBuilder<TState, TResult> builder: builder.Delay(duration); break;
            default: Invalid(handle, nameof(Delay)); break;
        }
    }

    public void AcquireResources<TInput, TState, TResult>(
        AuthoringKernelHandle handle,
        ResourceLeaseRequest request,
        Action<AuthoringKernelHandle> body)
    {
        switch (handle.Value)
        {
            case DurableWorkflowBuilder<TInput, TState> builder: builder.AcquireResources(request, nested => body(Handle(nested))); break;
            case DurableNestedBuilder<TInput, TState> builder: builder.AcquireResources(request, nested => body(Handle(nested))); break;
            case DurableBranchBuilder<TState, TResult> builder: builder.AcquireResources(request, nested => body(Handle(nested))); break;
            case DurableItemBuilder<TState, TResult> builder: builder.AcquireResources(request, nested => body(Handle(nested))); break;
            default: Invalid(handle, nameof(AcquireResources)); break;
        }
    }

    public void AcquireResources<TInput, TState, TResult>(
        AuthoringKernelHandle handle,
        Func<ReadOnlyStateSnapshot<TState>, ResourceLeaseRequest> request,
        Action<AuthoringKernelHandle> body)
    {
        switch (handle.Value)
        {
            case DurableWorkflowBuilder<TInput, TState> builder: builder.AcquireResources(request, nested => body(Handle(nested))); break;
            case DurableNestedBuilder<TInput, TState> builder: builder.AcquireResources(request, nested => body(Handle(nested))); break;
            case DurableBranchBuilder<TState, TResult> builder: builder.AcquireResources(request, nested => body(Handle(nested))); break;
            case DurableItemBuilder<TState, TResult> builder: builder.AcquireResources(request, nested => body(Handle(nested))); break;
            default: Invalid(handle, nameof(AcquireResources)); break;
        }
    }

    public void Return<TState, TResult>(
        AuthoringKernelHandle handle,
        Func<ReadOnlyStateSnapshot<TState>, TResult> result)
    {
        switch (handle.Value)
        {
            case EphemeralBranchBuilder<TState, TResult> builder: builder.Return(result); break;
            case EphemeralItemBuilder<TState, TResult> builder: builder.Return(result); break;
            case DurableBranchBuilder<TState, TResult> builder: builder.Return(result); break;
            case DurableItemBuilder<TState, TResult> builder: builder.Return(result); break;
            case DurableLeaseBranchBuilder<TState, TResult> builder: builder.Return(result); break;
            case DurableLeaseItemBuilder<TState, TResult> builder: builder.Return(result); break;
            default: Invalid(handle, nameof(Return)); break;
        }
    }

    public AuthoringKernelHandle Parallel<TInput, TState, TResult>(
        AuthoringKernelHandle handle,
        Action<AuthoringKernelHandle> branches) =>
        handle.Value switch
        {
            EphemeralWorkflowBuilder<TInput, TState> builder =>
                Handle(builder.Parallel<TResult>(scope => branches(Handle(scope)))),
            DurableWorkflowBuilder<TInput, TState> builder =>
                Handle(builder.Parallel<TResult>(scope => branches(Handle(scope)))),
            _ => Invalid<AuthoringKernelHandle>(handle, nameof(Parallel))
        };

    public AuthoringKernelHandle ForEach<TInput, TState, TItem, TItemState, TResult>(
        AuthoringKernelHandle handle,
        Func<ReadOnlyStateSnapshot<TState>, IReadOnlyList<TItem>> items,
        ForEachOptions options,
        Func<ForEachItemInput<TItem>, TItemState> input,
        Action<AuthoringKernelHandle> body) =>
        handle.Value switch
        {
            EphemeralWorkflowBuilder<TInput, TState> builder =>
                Handle(builder.ForEach<TItem, TItemState, TResult>(items, options, input, nested => body(Handle(nested)))),
            DurableWorkflowBuilder<TInput, TState> builder =>
                Handle(builder.ForEach<TItem, TItemState, TResult>(items, options, input, nested => body(Handle(nested)))),
            _ => Invalid<AuthoringKernelHandle>(handle, nameof(ForEach))
        };

    public AuthoringKernelHandle End<TInput, TState>(AuthoringKernelHandle handle) =>
        handle.Value switch
        {
            EphemeralWorkflowBuilder<TInput, TState> builder => Handle(builder.End()),
            DurableWorkflowBuilder<TInput, TState> builder => Handle(builder.End()),
            _ => Invalid<AuthoringKernelHandle>(handle, nameof(End))
        };

    public AuthoringKernelHandle End<TInput, TState>(AuthoringKernelHandle handle, WorkflowOutcomeName outcome) =>
        handle.Value switch
        {
            EphemeralWorkflowBuilder<TInput, TState> builder => Handle(builder.End(outcome)),
            DurableWorkflowBuilder<TInput, TState> builder => Handle(builder.End(outcome)),
            _ => Invalid<AuthoringKernelHandle>(handle, nameof(End))
        };

    public AuthoringKernelHandle End<TInput, TState, TOutput>(
        AuthoringKernelHandle handle,
        Func<ReadOnlyStateSnapshot<TState>, TOutput> output) =>
        handle.Value switch
        {
            EphemeralWorkflowBuilder<TInput, TState> builder => Handle(builder.End(output)),
            DurableWorkflowBuilder<TInput, TState> builder => Handle(builder.End(output)),
            _ => Invalid<AuthoringKernelHandle>(handle, nameof(End))
        };

    public AuthoringKernelHandle End<TInput, TState, TOutput>(
        AuthoringKernelHandle handle,
        Func<ReadOnlyStateSnapshot<TState>, TOutput> output,
        WorkflowOutcomeName outcome) =>
        handle.Value switch
        {
            EphemeralWorkflowBuilder<TInput, TState> builder => Handle(builder.End(output, outcome)),
            DurableWorkflowBuilder<TInput, TState> builder => Handle(builder.End(output, outcome)),
            _ => Invalid<AuthoringKernelHandle>(handle, nameof(End))
        };

    public AuthoringKernelHandle ContinueAsNew<TInput, TState>(
        AuthoringKernelHandle handle,
        Func<ReadOnlyStateSnapshot<TState>, TState> replacementState) =>
        handle.Value is DurableWorkflowBuilder<TInput, TState> builder
            ? Handle(builder.ContinueAsNew(replacementState))
            : Invalid<AuthoringKernelHandle>(handle, nameof(ContinueAsNew));

    public void Branch<TInput, TState, TResult, TBranchState>(
        AuthoringKernelHandle handle,
        AuthoredBranchId branchId,
        Func<ReadOnlyStateSnapshot<TState>, TBranchState> input,
        Action<AuthoringKernelHandle> body)
    {
        switch (handle.Value)
        {
            case EphemeralWorkflowParallelBranchScopeBuilder<TInput, TState, TResult> builder:
                builder.Branch(branchId, input, nested => body(Handle(nested)));
                break;
            case DurableWorkflowParallelBranchScopeBuilder<TInput, TState, TResult> builder:
                builder.Branch(branchId, input, nested => body(Handle(nested)));
                break;
            default: Invalid(handle, nameof(Branch)); break;
        }
    }

    public AuthoringKernelHandle ParallelWhenAll<TInput, TState, TResult>(
        AuthoringKernelHandle handle,
        Func<ReadOnlyStateSnapshot<TState>, IReadOnlyList<BranchResult<TResult>>, TState> merge) =>
        handle.Value switch
        {
            EphemeralWorkflowParallelJoinBuilder<TInput, TState, TResult> builder => Handle(builder.WhenAll(merge)),
            DurableWorkflowParallelJoinBuilder<TInput, TState, TResult> builder => Handle(builder.WhenAll(merge)),
            _ => Invalid<AuthoringKernelHandle>(handle, nameof(ParallelWhenAll))
        };

    public AuthoringKernelHandle ParallelWhenAllOutcomes<TInput, TState, TResult>(
        AuthoringKernelHandle handle,
        Func<ReadOnlyStateSnapshot<TState>, IReadOnlyList<BranchOutcome<TResult>>, TState> merge) =>
        handle.Value switch
        {
            EphemeralWorkflowParallelJoinBuilder<TInput, TState, TResult> builder => Handle(builder.WhenAllOutcomes(merge)),
            DurableWorkflowParallelJoinBuilder<TInput, TState, TResult> builder => Handle(builder.WhenAllOutcomes(merge)),
            _ => Invalid<AuthoringKernelHandle>(handle, nameof(ParallelWhenAllOutcomes))
        };

    public AuthoringKernelHandle ForEachWhenAll<TInput, TState, TResult>(
        AuthoringKernelHandle handle,
        Func<ReadOnlyStateSnapshot<TState>, IReadOnlyList<ForEachItemResult<TResult>>, TState> merge) =>
        handle.Value switch
        {
            EphemeralForEachJoinBuilder<TInput, TState, TResult> builder => Handle(builder.WhenAll(merge)),
            DurableForEachJoinBuilder<TInput, TState, TResult> builder => Handle(builder.WhenAll(merge)),
            _ => Invalid<AuthoringKernelHandle>(handle, nameof(ForEachWhenAll))
        };

    public AuthoringKernelHandle ForEachWhenAllOutcomes<TInput, TState, TResult>(
        AuthoringKernelHandle handle,
        Func<ReadOnlyStateSnapshot<TState>, IReadOnlyList<ForEachItemOutcome<TResult>>, TState> merge) =>
        handle.Value switch
        {
            EphemeralForEachJoinBuilder<TInput, TState, TResult> builder => Handle(builder.WhenAllOutcomes(merge)),
            DurableForEachJoinBuilder<TInput, TState, TResult> builder => Handle(builder.WhenAllOutcomes(merge)),
            _ => Invalid<AuthoringKernelHandle>(handle, nameof(ForEachWhenAllOutcomes))
        };

    public EphemeralWorkflowDefinition<TInput> BuildEphemeral<TInput>(AuthoringKernelHandle handle) =>
        Require<EphemeralWorkflowCompletionBuilder<TInput>>(handle, nameof(BuildEphemeral)).Build();

    public Validation<EphemeralWorkflowDefinition<TInput>> TryBuildEphemeral<TInput>(AuthoringKernelHandle handle) =>
        Require<EphemeralWorkflowCompletionBuilder<TInput>>(handle, nameof(TryBuildEphemeral)).TryBuild();

    public EphemeralWorkflowDefinition<TInput, TOutput> BuildEphemeral<TInput, TOutput>(AuthoringKernelHandle handle) =>
        Require<EphemeralWorkflowCompletionBuilder<TInput, TOutput>>(handle, nameof(BuildEphemeral)).Build();

    public Validation<EphemeralWorkflowDefinition<TInput, TOutput>> TryBuildEphemeral<TInput, TOutput>(AuthoringKernelHandle handle) =>
        Require<EphemeralWorkflowCompletionBuilder<TInput, TOutput>>(handle, nameof(TryBuildEphemeral)).TryBuild();

    public DurableWorkflowDefinition<TInput> BuildDurable<TInput>(AuthoringKernelHandle handle) =>
        Require<DurableWorkflowCompletionBuilder<TInput>>(handle, nameof(BuildDurable)).Build();

    public Validation<DurableWorkflowDefinition<TInput>> TryBuildDurable<TInput>(AuthoringKernelHandle handle) =>
        Require<DurableWorkflowCompletionBuilder<TInput>>(handle, nameof(TryBuildDurable)).TryBuild();

    public DurableWorkflowDefinition<TInput, TOutput> BuildDurable<TInput, TOutput>(AuthoringKernelHandle handle) =>
        Require<DurableWorkflowCompletionBuilder<TInput, TOutput>>(handle, nameof(BuildDurable)).Build();

    public Validation<DurableWorkflowDefinition<TInput, TOutput>> TryBuildDurable<TInput, TOutput>(AuthoringKernelHandle handle) =>
        Require<DurableWorkflowCompletionBuilder<TInput, TOutput>>(handle, nameof(TryBuildDurable)).TryBuild();

    private static void WaitOn<TInput, TState>(EphemeralWorkflowBuilder<TInput, TState> value, WorkflowEventContract contract, Func<ReadOnlyStateSnapshot<TState>, CorrelationId> correlation, TimeSpan? timeout) { if (timeout is null) value.Wait(contract, correlation); else value.Wait(contract, correlation, timeout.Value); }
    private static void WaitOn<TInput, TState>(DurableWorkflowBuilder<TInput, TState> value, WorkflowEventContract contract, Func<ReadOnlyStateSnapshot<TState>, CorrelationId> correlation, TimeSpan? timeout) { if (timeout is null) value.Wait(contract, correlation); else value.Wait(contract, correlation, timeout.Value); }
    private static void WaitOn<TInput, TState>(EphemeralNestedBuilder<TInput, TState> value, WorkflowEventContract contract, Func<ReadOnlyStateSnapshot<TState>, CorrelationId> correlation, TimeSpan? timeout) { if (timeout is null) value.Wait(contract, correlation); else value.Wait(contract, correlation, timeout.Value); }
    private static void WaitOn<TInput, TState>(DurableNestedBuilder<TInput, TState> value, WorkflowEventContract contract, Func<ReadOnlyStateSnapshot<TState>, CorrelationId> correlation, TimeSpan? timeout) { if (timeout is null) value.Wait(contract, correlation); else value.Wait(contract, correlation, timeout.Value); }
    private static void WaitOn<TInput, TState>(DurableLeaseWorkflowBuilder<TInput, TState> value, WorkflowEventContract contract, Func<ReadOnlyStateSnapshot<TState>, CorrelationId> correlation, TimeSpan? timeout) { if (timeout is null) value.Wait(contract, correlation); else value.Wait(contract, correlation, timeout.Value); }
    private static void WaitOn<TInput, TState>(DurableLeaseNestedBuilder<TInput, TState> value, WorkflowEventContract contract, Func<ReadOnlyStateSnapshot<TState>, CorrelationId> correlation, TimeSpan? timeout) { if (timeout is null) value.Wait(contract, correlation); else value.Wait(contract, correlation, timeout.Value); }
    private static void WaitOn<TState, TResult>(EphemeralBranchBuilder<TState, TResult> value, WorkflowEventContract contract, Func<ReadOnlyStateSnapshot<TState>, CorrelationId> correlation, TimeSpan? timeout) { if (timeout is null) value.Wait(contract, correlation); else value.Wait(contract, correlation, timeout.Value); }
    private static void WaitOn<TState, TResult>(EphemeralItemBuilder<TState, TResult> value, WorkflowEventContract contract, Func<ReadOnlyStateSnapshot<TState>, CorrelationId> correlation, TimeSpan? timeout) { if (timeout is null) value.Wait(contract, correlation); else value.Wait(contract, correlation, timeout.Value); }
    private static void WaitOn<TState, TResult>(DurableBranchBuilder<TState, TResult> value, WorkflowEventContract contract, Func<ReadOnlyStateSnapshot<TState>, CorrelationId> correlation, TimeSpan? timeout) { if (timeout is null) value.Wait(contract, correlation); else value.Wait(contract, correlation, timeout.Value); }
    private static void WaitOn<TState, TResult>(DurableItemBuilder<TState, TResult> value, WorkflowEventContract contract, Func<ReadOnlyStateSnapshot<TState>, CorrelationId> correlation, TimeSpan? timeout) { if (timeout is null) value.Wait(contract, correlation); else value.Wait(contract, correlation, timeout.Value); }
    private static void WaitOn<TState, TResult>(DurableLeaseBranchBuilder<TState, TResult> value, WorkflowEventContract contract, Func<ReadOnlyStateSnapshot<TState>, CorrelationId> correlation, TimeSpan? timeout) { if (timeout is null) value.Wait(contract, correlation); else value.Wait(contract, correlation, timeout.Value); }
    private static void WaitOn<TState, TResult>(DurableLeaseItemBuilder<TState, TResult> value, WorkflowEventContract contract, Func<ReadOnlyStateSnapshot<TState>, CorrelationId> correlation, TimeSpan? timeout) { if (timeout is null) value.Wait(contract, correlation); else value.Wait(contract, correlation, timeout.Value); }

    private static Action<TBuilder>? Wrap<TBuilder>(Action<AuthoringKernelHandle>? callback) =>
        callback is null ? null : value => callback(Handle(value!));

    private static AuthoringKernelHandle Handle(object value) => new(value);

    private static T Require<T>(AuthoringKernelHandle handle, string operation) where T : class =>
        handle.Value as T ?? Invalid<T>(handle, operation);

    private static void Invalid(AuthoringKernelHandle handle, string operation) =>
        throw new InvalidOperationException(
            $"The typed authoring receiver '{handle.Value.GetType().FullName}' does not support '{operation}'.");

    private static T Invalid<T>(AuthoringKernelHandle handle, string operation)
    {
        Invalid(handle, operation);
        return default!;
    }
}

internal static class TypedAuthoringBootstrap
{
#pragma warning disable CA2255
    [ModuleInitializer]
    internal static void Initialize() => TypedAuthoringBoundary.Install(TypedAuthoringOperations.Instance);
#pragma warning restore CA2255
}
