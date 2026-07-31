using OrcaCore;

namespace OrcaCore.Core.Authoring;

/// <summary>Authors one ephemeral fixed branch.</summary>
internal sealed class EphemeralBranchBuilder<TState, TResult>
{
    private readonly global::OrcaCore.Core.Building.BranchBuilder<TState, TResult> builder;
    internal EphemeralBranchBuilder(global::OrcaCore.Core.Building.BranchBuilder<TState, TResult> builder) => this.builder = builder;
    internal global::OrcaCore.Core.Building.BranchBuilder<TState, TResult> RuntimeBuilder => builder;

    public EphemeralBranchBuilder<TState, TResult> Then<TStep>() where TStep : IStep<TState>
    { builder.AddNamedStep<TStep>(); return this; }

    public EphemeralBranchBuilder<TState, TResult> Then(Func<StepContext<TState>, ValueTask> body)
    { ArgumentNullException.ThrowIfNull(body); builder.Then(() => new InlineEphemeralStep<TState>((context, _) => body(context))); return this; }

    public EphemeralBranchBuilder<TState, TResult> Then(Func<StepContext<TState>, CancellationToken, ValueTask> body)
    { ArgumentNullException.ThrowIfNull(body); builder.Then(() => new InlineEphemeralStep<TState>(body)); return this; }

    public EphemeralBranchBuilder<TState, TResult> WithRetry(int maxAttempts, TimeSpan? fixedDelay = null)
    { builder.DecoratePreviousWithRetry(maxAttempts, fixedDelay); return this; }

    public EphemeralBranchBuilder<TState, TResult> WithStepTimeout(TimeSpan timeout)
    { builder.DecoratePreviousWithTimeout(timeout); return this; }

    public EphemeralBranchBuilder<TState, TResult> WithTransientPool(TransientPoolName pool)
    { ArgumentNullException.ThrowIfNull(pool); builder.DecoratePreviousWithPool(pool.Value); return this; }

    public EphemeralBranchBuilder<TState, TResult> If(
        Func<ReadOnlyStateSnapshot<TState>, bool> condition,
        Action<EphemeralBranchBuilder<TState, TResult>> then,
        Action<EphemeralBranchBuilder<TState, TResult>>? otherwise = null)
    {
        ArgumentNullException.ThrowIfNull(condition);
        ArgumentNullException.ThrowIfNull(then);
        builder.AddIf(
            state => condition(PublicAuthoringContracts.Snapshot(state)),
            nested => then(new EphemeralBranchBuilder<TState, TResult>(nested)),
            otherwise is null
                ? null
                : nested => otherwise(new EphemeralBranchBuilder<TState, TResult>(nested)));
        return this;
    }

    public EphemeralBranchBuilder<TState, TResult> Wait(EventName eventName, Func<ReadOnlyStateSnapshot<TState>, CorrelationId> correlation)
    { ArgumentNullException.ThrowIfNull(eventName); ArgumentNullException.ThrowIfNull(correlation); builder.Wait(eventName.Value, state => correlation(PublicAuthoringContracts.Snapshot(state))); return this; }

    public EphemeralBranchBuilder<TState, TResult> Wait(EventName eventName, Func<ReadOnlyStateSnapshot<TState>, CorrelationId> correlation, TimeSpan timeout)
    { PublicAuthoringValidation.Positive(timeout, nameof(timeout)); ArgumentNullException.ThrowIfNull(eventName); ArgumentNullException.ThrowIfNull(correlation); builder.AddWait(eventName.Value, state => correlation(PublicAuthoringContracts.Snapshot(state)), global::OrcaCore.Abstractions.Instances.WaitMode.Resident, timeout); return this; }

    public EphemeralBranchBuilder<TState, TResult> Delay(TimeSpan duration)
    { builder.Delay(duration); return this; }

    public EphemeralBranchBuilder<TState, TResult> Return(Func<ReadOnlyStateSnapshot<TState>, TResult> result)
    { ArgumentNullException.ThrowIfNull(result); builder.Return(snapshot => result(PublicAuthoringContracts.Snapshot(snapshot.Value))); return this; }
}

/// <summary>Authors one ephemeral dynamic item.</summary>
internal sealed class EphemeralItemBuilder<TState, TResult>
{
    private readonly EphemeralBranchBuilder<TState, TResult> branch;
    internal EphemeralItemBuilder(global::OrcaCore.Core.Building.BranchBuilder<TState, TResult> builder) => branch = new(builder);

    public EphemeralItemBuilder<TState, TResult> Then<TStep>() where TStep : IStep<TState>
    { branch.Then<TStep>(); return this; }
    public EphemeralItemBuilder<TState, TResult> Then(Func<StepContext<TState>, ValueTask> body)
    { branch.Then(body); return this; }
    public EphemeralItemBuilder<TState, TResult> Then(Func<StepContext<TState>, CancellationToken, ValueTask> body)
    { branch.Then(body); return this; }
    public EphemeralItemBuilder<TState, TResult> WithRetry(int maxAttempts, TimeSpan? fixedDelay = null)
    { branch.WithRetry(maxAttempts, fixedDelay); return this; }
    public EphemeralItemBuilder<TState, TResult> WithStepTimeout(TimeSpan timeout)
    { branch.WithStepTimeout(timeout); return this; }
    public EphemeralItemBuilder<TState, TResult> WithTransientPool(TransientPoolName pool)
    { branch.WithTransientPool(pool); return this; }
    public EphemeralItemBuilder<TState, TResult> If(Func<ReadOnlyStateSnapshot<TState>, bool> condition, Action<EphemeralItemBuilder<TState, TResult>> then, Action<EphemeralItemBuilder<TState, TResult>>? otherwise = null)
    {
        ArgumentNullException.ThrowIfNull(condition);
        ArgumentNullException.ThrowIfNull(then);
        branch.RuntimeBuilder.AddIf(
            state => condition(PublicAuthoringContracts.Snapshot(state)),
            nested => then(new EphemeralItemBuilder<TState, TResult>(nested)),
            otherwise is null
                ? null
                : nested => otherwise(new EphemeralItemBuilder<TState, TResult>(nested)));
        return this;
    }
    public EphemeralItemBuilder<TState, TResult> Wait(EventName eventName, Func<ReadOnlyStateSnapshot<TState>, CorrelationId> correlation)
    { branch.Wait(eventName, correlation); return this; }
    public EphemeralItemBuilder<TState, TResult> Wait(EventName eventName, Func<ReadOnlyStateSnapshot<TState>, CorrelationId> correlation, TimeSpan timeout)
    { branch.Wait(eventName, correlation, timeout); return this; }
    public EphemeralItemBuilder<TState, TResult> Delay(TimeSpan duration)
    { branch.Delay(duration); return this; }
    public EphemeralItemBuilder<TState, TResult> Return(Func<ReadOnlyStateSnapshot<TState>, TResult> result)
    { branch.Return(result); return this; }
}

/// <summary>Authors one durable fixed branch.</summary>
internal sealed class DurableBranchBuilder<TState, TResult>
{
    private readonly global::OrcaCore.Core.Building.BranchBuilder<TState, TResult> builder;
    internal DurableBranchBuilder(global::OrcaCore.Core.Building.BranchBuilder<TState, TResult> builder) => this.builder = builder;
    internal global::OrcaCore.Core.Building.BranchBuilder<TState, TResult> RuntimeBuilder => builder;

    public DurableBranchBuilder<TState, TResult> Then<TStep>() where TStep : IStep<TState>
    { builder.AddNamedStep<TStep>(); return this; }
    public DurableBranchBuilder<TState, TResult> WithRetry(int maxAttempts, TimeSpan? fixedDelay = null)
    { builder.DecoratePreviousWithRetry(maxAttempts, fixedDelay); return this; }
    public DurableBranchBuilder<TState, TResult> WithStepTimeout(TimeSpan timeout)
    { builder.DecoratePreviousWithTimeout(timeout); return this; }
    public DurableBranchBuilder<TState, TResult> If(Func<ReadOnlyStateSnapshot<TState>, bool> condition, Action<DurableBranchBuilder<TState, TResult>> then, Action<DurableBranchBuilder<TState, TResult>>? otherwise = null)
    {
        ArgumentNullException.ThrowIfNull(condition);
        ArgumentNullException.ThrowIfNull(then);
        builder.AddIf(
            state => condition(PublicAuthoringContracts.Snapshot(state)),
            nested => then(new DurableBranchBuilder<TState, TResult>(nested)),
            otherwise is null
                ? null
                : nested => otherwise(new DurableBranchBuilder<TState, TResult>(nested)));
        return this;
    }
    public DurableBranchBuilder<TState, TResult> Wait(EventName eventName, Func<ReadOnlyStateSnapshot<TState>, CorrelationId> correlation)
    { ArgumentNullException.ThrowIfNull(eventName); ArgumentNullException.ThrowIfNull(correlation); builder.AddWait(eventName.Value, state => correlation(PublicAuthoringContracts.Snapshot(state)), global::OrcaCore.Abstractions.Instances.WaitMode.Cold); return this; }
    public DurableBranchBuilder<TState, TResult> Wait(EventName eventName, Func<ReadOnlyStateSnapshot<TState>, CorrelationId> correlation, TimeSpan timeout)
    { PublicAuthoringValidation.Positive(timeout, nameof(timeout)); ArgumentNullException.ThrowIfNull(eventName); ArgumentNullException.ThrowIfNull(correlation); builder.AddWait(eventName.Value, state => correlation(PublicAuthoringContracts.Snapshot(state)), global::OrcaCore.Abstractions.Instances.WaitMode.Cold, timeout); return this; }
    public DurableBranchBuilder<TState, TResult> Delay(TimeSpan duration)
    { builder.Delay(duration); return this; }
    public DurableBranchBuilder<TState, TResult> AcquireResources(ResourceLeaseRequest request, Action<DurableLeaseBranchBuilder<TState, TResult>> body)
    { ArgumentNullException.ThrowIfNull(request); ArgumentNullException.ThrowIfNull(body); builder.AddResourceLease(request, nested => body(new DurableLeaseBranchBuilder<TState, TResult>(nested))); return this; }
    public DurableBranchBuilder<TState, TResult> AcquireResources(Func<ReadOnlyStateSnapshot<TState>, ResourceLeaseRequest> request, Action<DurableLeaseBranchBuilder<TState, TResult>> body)
    { ArgumentNullException.ThrowIfNull(request); ArgumentNullException.ThrowIfNull(body); builder.AddResourceLease(state => request(PublicAuthoringContracts.Snapshot(state)), nested => body(new DurableLeaseBranchBuilder<TState, TResult>(nested))); return this; }
    public DurableBranchBuilder<TState, TResult> Return(Func<ReadOnlyStateSnapshot<TState>, TResult> result)
    { ArgumentNullException.ThrowIfNull(result); builder.Return(snapshot => result(PublicAuthoringContracts.Snapshot(snapshot.Value))); return this; }
}

/// <summary>Authors one durable dynamic item.</summary>
internal sealed class DurableItemBuilder<TState, TResult>
{
    private readonly DurableBranchBuilder<TState, TResult> branch;
    internal DurableItemBuilder(global::OrcaCore.Core.Building.BranchBuilder<TState, TResult> builder) => branch = new(builder);
    public DurableItemBuilder<TState, TResult> Then<TStep>() where TStep : IStep<TState> { branch.Then<TStep>(); return this; }
    public DurableItemBuilder<TState, TResult> WithRetry(int maxAttempts, TimeSpan? fixedDelay = null) { branch.WithRetry(maxAttempts, fixedDelay); return this; }
    public DurableItemBuilder<TState, TResult> WithStepTimeout(TimeSpan timeout) { branch.WithStepTimeout(timeout); return this; }
    public DurableItemBuilder<TState, TResult> If(Func<ReadOnlyStateSnapshot<TState>, bool> condition, Action<DurableItemBuilder<TState, TResult>> then, Action<DurableItemBuilder<TState, TResult>>? otherwise = null)
    {
        ArgumentNullException.ThrowIfNull(condition);
        ArgumentNullException.ThrowIfNull(then);
        branch.RuntimeBuilder.AddIf(
            state => condition(PublicAuthoringContracts.Snapshot(state)),
            nested => then(new DurableItemBuilder<TState, TResult>(nested)),
            otherwise is null
                ? null
                : nested => otherwise(new DurableItemBuilder<TState, TResult>(nested)));
        return this;
    }
    public DurableItemBuilder<TState, TResult> Wait(EventName eventName, Func<ReadOnlyStateSnapshot<TState>, CorrelationId> correlation) { branch.Wait(eventName, correlation); return this; }
    public DurableItemBuilder<TState, TResult> Wait(EventName eventName, Func<ReadOnlyStateSnapshot<TState>, CorrelationId> correlation, TimeSpan timeout) { branch.Wait(eventName, correlation, timeout); return this; }
    public DurableItemBuilder<TState, TResult> Delay(TimeSpan duration) { branch.Delay(duration); return this; }
    public DurableItemBuilder<TState, TResult> AcquireResources(ResourceLeaseRequest request, Action<DurableLeaseItemBuilder<TState, TResult>> body) { ArgumentNullException.ThrowIfNull(request); ArgumentNullException.ThrowIfNull(body); branch.RuntimeBuilder.AddResourceLease(request, nested => body(new DurableLeaseItemBuilder<TState, TResult>(new DurableBranchBuilder<TState, TResult>(nested)))); return this; }
    public DurableItemBuilder<TState, TResult> AcquireResources(Func<ReadOnlyStateSnapshot<TState>, ResourceLeaseRequest> request, Action<DurableLeaseItemBuilder<TState, TResult>> body) { ArgumentNullException.ThrowIfNull(request); ArgumentNullException.ThrowIfNull(body); branch.RuntimeBuilder.AddResourceLease(state => request(PublicAuthoringContracts.Snapshot(state)), nested => body(new DurableLeaseItemBuilder<TState, TResult>(new DurableBranchBuilder<TState, TResult>(nested)))); return this; }
    public DurableItemBuilder<TState, TResult> Return(Func<ReadOnlyStateSnapshot<TState>, TResult> result) { branch.Return(result); return this; }
}

/// <summary>Authors a fixed durable branch body inside an existing lease.</summary>
internal sealed class DurableLeaseBranchBuilder<TState, TResult>
{
    private readonly global::OrcaCore.Core.Building.BranchBuilder<TState, TResult> builder;
    internal DurableLeaseBranchBuilder(global::OrcaCore.Core.Building.BranchBuilder<TState, TResult> builder) => this.builder = builder;
    internal global::OrcaCore.Core.Building.BranchBuilder<TState, TResult> RuntimeBuilder => builder;
    public DurableLeaseBranchBuilder<TState, TResult> Then<TStep>() where TStep : IStep<TState> { builder.AddNamedStep<TStep>(); return this; }
    public DurableLeaseBranchBuilder<TState, TResult> WithRetry(int maxAttempts, TimeSpan? fixedDelay = null) { builder.DecoratePreviousWithRetry(maxAttempts, fixedDelay); return this; }
    public DurableLeaseBranchBuilder<TState, TResult> WithStepTimeout(TimeSpan timeout) { builder.DecoratePreviousWithTimeout(timeout); return this; }
    public DurableLeaseBranchBuilder<TState, TResult> If(Func<ReadOnlyStateSnapshot<TState>, bool> condition, Action<DurableLeaseBranchBuilder<TState, TResult>> then, Action<DurableLeaseBranchBuilder<TState, TResult>>? otherwise = null)
    {
        ArgumentNullException.ThrowIfNull(condition);
        ArgumentNullException.ThrowIfNull(then);
        builder.AddIf(
            state => condition(PublicAuthoringContracts.Snapshot(state)),
            nested => then(new DurableLeaseBranchBuilder<TState, TResult>(nested)),
            otherwise is null
                ? null
                : nested => otherwise(new DurableLeaseBranchBuilder<TState, TResult>(nested)));
        return this;
    }
    public DurableLeaseBranchBuilder<TState, TResult> Wait(EventName eventName, Func<ReadOnlyStateSnapshot<TState>, CorrelationId> correlation) { ArgumentNullException.ThrowIfNull(eventName); ArgumentNullException.ThrowIfNull(correlation); builder.AddWait(eventName.Value, state => correlation(PublicAuthoringContracts.Snapshot(state)), global::OrcaCore.Abstractions.Instances.WaitMode.Cold); return this; }
    public DurableLeaseBranchBuilder<TState, TResult> Wait(EventName eventName, Func<ReadOnlyStateSnapshot<TState>, CorrelationId> correlation, TimeSpan timeout) { PublicAuthoringValidation.Positive(timeout, nameof(timeout)); ArgumentNullException.ThrowIfNull(eventName); ArgumentNullException.ThrowIfNull(correlation); builder.AddWait(eventName.Value, state => correlation(PublicAuthoringContracts.Snapshot(state)), global::OrcaCore.Abstractions.Instances.WaitMode.Cold, timeout); return this; }
    public DurableLeaseBranchBuilder<TState, TResult> Delay(TimeSpan duration) { builder.Delay(duration); return this; }
    public DurableLeaseBranchBuilder<TState, TResult> Return(Func<ReadOnlyStateSnapshot<TState>, TResult> result) { ArgumentNullException.ThrowIfNull(result); builder.Return(snapshot => result(PublicAuthoringContracts.Snapshot(snapshot.Value))); return this; }
}

/// <summary>Authors a dynamic durable item body inside an existing lease.</summary>
internal sealed class DurableLeaseItemBuilder<TState, TResult>
{
    private readonly DurableBranchBuilder<TState, TResult> branch;
    internal DurableLeaseItemBuilder(DurableBranchBuilder<TState, TResult> branch) => this.branch = branch;
    public DurableLeaseItemBuilder<TState, TResult> Then<TStep>() where TStep : IStep<TState> { branch.Then<TStep>(); return this; }
    public DurableLeaseItemBuilder<TState, TResult> WithRetry(int maxAttempts, TimeSpan? fixedDelay = null) { branch.WithRetry(maxAttempts, fixedDelay); return this; }
    public DurableLeaseItemBuilder<TState, TResult> WithStepTimeout(TimeSpan timeout) { branch.WithStepTimeout(timeout); return this; }
    public DurableLeaseItemBuilder<TState, TResult> If(Func<ReadOnlyStateSnapshot<TState>, bool> condition, Action<DurableLeaseItemBuilder<TState, TResult>> then, Action<DurableLeaseItemBuilder<TState, TResult>>? otherwise = null)
    {
        ArgumentNullException.ThrowIfNull(condition);
        ArgumentNullException.ThrowIfNull(then);
        branch.RuntimeBuilder.AddIf(
            state => condition(PublicAuthoringContracts.Snapshot(state)),
            nested => then(new DurableLeaseItemBuilder<TState, TResult>(new DurableBranchBuilder<TState, TResult>(nested))),
            otherwise is null
                ? null
                : nested => otherwise(new DurableLeaseItemBuilder<TState, TResult>(new DurableBranchBuilder<TState, TResult>(nested))));
        return this;
    }
    public DurableLeaseItemBuilder<TState, TResult> Wait(EventName eventName, Func<ReadOnlyStateSnapshot<TState>, CorrelationId> correlation) { branch.Wait(eventName, correlation); return this; }
    public DurableLeaseItemBuilder<TState, TResult> Wait(EventName eventName, Func<ReadOnlyStateSnapshot<TState>, CorrelationId> correlation, TimeSpan timeout) { branch.Wait(eventName, correlation, timeout); return this; }
    public DurableLeaseItemBuilder<TState, TResult> Delay(TimeSpan duration) { branch.Delay(duration); return this; }
    public DurableLeaseItemBuilder<TState, TResult> Return(Func<ReadOnlyStateSnapshot<TState>, TResult> result) { branch.Return(result); return this; }
}

/// <summary>Authors fixed ephemeral root branches.</summary>
internal sealed class EphemeralWorkflowParallelBranchScopeBuilder<TInput, TState, TResult>
{
    private readonly global::OrcaCore.Core.Building.BranchScopeBuilder<TState, TResult> scope;
    internal EphemeralWorkflowParallelBranchScopeBuilder(global::OrcaCore.Core.Building.BranchScopeBuilder<TState, TResult> scope) => this.scope = scope;
    public EphemeralWorkflowParallelBranchScopeBuilder<TInput, TState, TResult> Branch<TBranchState>(AuthoredBranchId branchId, Func<ReadOnlyStateSnapshot<TState>, TBranchState> input, Action<EphemeralBranchBuilder<TBranchState, TResult>> body)
    { ArgumentNullException.ThrowIfNull(branchId); ArgumentNullException.ThrowIfNull(input); ArgumentNullException.ThrowIfNull(body); scope.Branch(branchId.Value, parent => input(PublicAuthoringContracts.Snapshot(parent.Value)), branch => body(new EphemeralBranchBuilder<TBranchState, TResult>(branch))); return this; }
}

/// <summary>Authors fixed durable root branches.</summary>
internal sealed class DurableWorkflowParallelBranchScopeBuilder<TInput, TState, TResult>
{
    private readonly global::OrcaCore.Core.Building.BranchScopeBuilder<TState, TResult> scope;
    internal DurableWorkflowParallelBranchScopeBuilder(global::OrcaCore.Core.Building.BranchScopeBuilder<TState, TResult> scope) => this.scope = scope;
    public DurableWorkflowParallelBranchScopeBuilder<TInput, TState, TResult> Branch<TBranchState>(AuthoredBranchId branchId, Func<ReadOnlyStateSnapshot<TState>, TBranchState> input, Action<DurableBranchBuilder<TBranchState, TResult>> body)
    { ArgumentNullException.ThrowIfNull(branchId); ArgumentNullException.ThrowIfNull(input); ArgumentNullException.ThrowIfNull(body); scope.Branch(branchId.Value, parent => input(PublicAuthoringContracts.Snapshot(parent.Value)), branch => body(new DurableBranchBuilder<TBranchState, TResult>(branch))); return this; }
}

/// <summary>Selects the required ephemeral root parallel join.</summary>
internal sealed class EphemeralWorkflowParallelJoinBuilder<TInput, TState, TResult>
{
    private readonly EphemeralWorkflowBuilder<TInput, TState> root;
    private readonly global::OrcaCore.Core.Building.AuthoringJoinToken join;
    private readonly Action<EphemeralWorkflowParallelBranchScopeBuilder<TInput, TState, TResult>> branches;
    internal EphemeralWorkflowParallelJoinBuilder(EphemeralWorkflowBuilder<TInput, TState> root, global::OrcaCore.Core.Building.AuthoringJoinToken join, Action<EphemeralWorkflowParallelBranchScopeBuilder<TInput, TState, TResult>> branches) { this.root = root; this.join = join; this.branches = branches; }
    public EphemeralWorkflowBuilder<TInput, TState> WhenAll(Func<ReadOnlyStateSnapshot<TState>, IReadOnlyList<BranchResult<TResult>>, TState> merge)
    { ArgumentNullException.ThrowIfNull(merge); var successor = root.RuntimeBuilder.CompleteRootParallel<TResult>(join, scope => branches(new(scope)), (parent, results) => merge(PublicAuthoringContracts.Snapshot(parent.Value), results.Select(result => new BranchResult<TResult>(AuthoredBranchId.Create(result.BranchId), result.Value)).ToArray())); return new(successor); }
    public EphemeralWorkflowBuilder<TInput, TState> WhenAllOutcomes(Func<ReadOnlyStateSnapshot<TState>, IReadOnlyList<BranchOutcome<TResult>>, TState> merge)
    { ArgumentNullException.ThrowIfNull(merge); var successor = root.RuntimeBuilder.CompleteRootParallelOutcomes<TResult>(join, scope => branches(new(scope)), (parent, outcomes) => merge(PublicAuthoringContracts.Snapshot(parent.Value), outcomes)); return new(successor); }
}

/// <summary>Selects the required durable root parallel join.</summary>
internal sealed class DurableWorkflowParallelJoinBuilder<TInput, TState, TResult>
{
    private readonly DurableWorkflowBuilder<TInput, TState> root;
    private readonly global::OrcaCore.Core.Building.AuthoringJoinToken join;
    private readonly Action<DurableWorkflowParallelBranchScopeBuilder<TInput, TState, TResult>> branches;
    internal DurableWorkflowParallelJoinBuilder(DurableWorkflowBuilder<TInput, TState> root, global::OrcaCore.Core.Building.AuthoringJoinToken join, Action<DurableWorkflowParallelBranchScopeBuilder<TInput, TState, TResult>> branches) { this.root = root; this.join = join; this.branches = branches; }
    public DurableWorkflowBuilder<TInput, TState> WhenAll(Func<ReadOnlyStateSnapshot<TState>, IReadOnlyList<BranchResult<TResult>>, TState> merge)
    { ArgumentNullException.ThrowIfNull(merge); var successor = root.RuntimeBuilder.CompleteRootParallel<TResult>(join, scope => branches(new(scope)), (parent, results) => merge(PublicAuthoringContracts.Snapshot(parent.Value), results.Select(result => new BranchResult<TResult>(AuthoredBranchId.Create(result.BranchId), result.Value)).ToArray())); return new(successor); }
    public DurableWorkflowBuilder<TInput, TState> WhenAllOutcomes(Func<ReadOnlyStateSnapshot<TState>, IReadOnlyList<BranchOutcome<TResult>>, TState> merge)
    { ArgumentNullException.ThrowIfNull(merge); var successor = root.RuntimeBuilder.CompleteRootParallelOutcomes<TResult>(join, scope => branches(new(scope)), (parent, outcomes) => merge(PublicAuthoringContracts.Snapshot(parent.Value), outcomes)); return new(successor); }
}

/// <summary>Selects the required ephemeral root item join.</summary>
internal sealed class EphemeralForEachJoinBuilder<TInput, TState, TResult>
{
    private readonly EphemeralWorkflowBuilder<TInput, TState> root;
    private readonly Func<Delegate, global::OrcaCore.Core.Building.EphemeralWorkflowBuilder<TState>> authorWhenAll;
    private readonly Func<Delegate, global::OrcaCore.Core.Building.EphemeralWorkflowBuilder<TState>> authorWhenAllOutcomes;
    internal EphemeralForEachJoinBuilder(EphemeralWorkflowBuilder<TInput, TState> root, Func<Delegate, global::OrcaCore.Core.Building.EphemeralWorkflowBuilder<TState>> authorWhenAll, Func<Delegate, global::OrcaCore.Core.Building.EphemeralWorkflowBuilder<TState>> authorWhenAllOutcomes) { this.root = root; this.authorWhenAll = authorWhenAll; this.authorWhenAllOutcomes = authorWhenAllOutcomes; }
    public EphemeralWorkflowBuilder<TInput, TState> WhenAll(Func<ReadOnlyStateSnapshot<TState>, IReadOnlyList<ForEachItemResult<TResult>>, TState> merge) { ArgumentNullException.ThrowIfNull(merge); return new(authorWhenAll(merge)); }
    public EphemeralWorkflowBuilder<TInput, TState> WhenAllOutcomes(Func<ReadOnlyStateSnapshot<TState>, IReadOnlyList<ForEachItemOutcome<TResult>>, TState> merge) { ArgumentNullException.ThrowIfNull(merge); return new(authorWhenAllOutcomes(merge)); }
}

/// <summary>Selects the required durable root item join.</summary>
internal sealed class DurableForEachJoinBuilder<TInput, TState, TResult>
{
    private readonly DurableWorkflowBuilder<TInput, TState> root;
    private readonly Func<Delegate, global::OrcaCore.Core.Building.DurableWorkflowBuilder<TState>> authorWhenAll;
    private readonly Func<Delegate, global::OrcaCore.Core.Building.DurableWorkflowBuilder<TState>> authorWhenAllOutcomes;
    internal DurableForEachJoinBuilder(DurableWorkflowBuilder<TInput, TState> root, Func<Delegate, global::OrcaCore.Core.Building.DurableWorkflowBuilder<TState>> authorWhenAll, Func<Delegate, global::OrcaCore.Core.Building.DurableWorkflowBuilder<TState>> authorWhenAllOutcomes) { this.root = root; this.authorWhenAll = authorWhenAll; this.authorWhenAllOutcomes = authorWhenAllOutcomes; }
    public DurableWorkflowBuilder<TInput, TState> WhenAll(Func<ReadOnlyStateSnapshot<TState>, IReadOnlyList<ForEachItemResult<TResult>>, TState> merge) { ArgumentNullException.ThrowIfNull(merge); return new(authorWhenAll(merge)); }
    public DurableWorkflowBuilder<TInput, TState> WhenAllOutcomes(Func<ReadOnlyStateSnapshot<TState>, IReadOnlyList<ForEachItemOutcome<TResult>>, TState> merge) { ArgumentNullException.ThrowIfNull(merge); return new(authorWhenAllOutcomes(merge)); }
}
