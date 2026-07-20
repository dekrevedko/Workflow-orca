// Normative companion to 17-selected-mode-capability-matrix.md.
//
// This is a declaration artifact, not product source. Throwing bodies exist only so every
// approved v1 workflow-authoring signature is valid C# with a concrete receiver and return
// type. Value, result, error, context, and option types referenced here are defined by document
// 17. No deferred or removed capability may be added to this file without first amending the
// matrix.

#nullable enable

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace OrcaCore;

public static class Workflow
{
    public static EphemeralWorkflowInitBuilder<TState> Ephemeral<TState>(
        DefinitionId definitionId,
        DefinitionVersion definitionVersion) => throw new NotSupportedException();

    public static DurableWorkflowInitBuilder<TState> Durable<TState>(
        DefinitionId definitionId,
        DefinitionVersion definitionVersion) => throw new NotSupportedException();
}

public sealed class EphemeralWorkflowInitBuilder<TState>
{
    internal EphemeralWorkflowInitBuilder() { }

    public EphemeralWorkflowBuilder<TInput, TState> Init<TInput>(
        Func<TInput, TState> createState) => throw new NotSupportedException();
}

public sealed class DurableWorkflowInitBuilder<TState>
{
    internal DurableWorkflowInitBuilder() { }

    public DurableWorkflowBuilder<TInput, TState> Init<TInput>(
        Func<TInput, TState> createState) => throw new NotSupportedException();
}

public sealed class EphemeralWorkflowCompletionBuilder<TInput>
{
    internal EphemeralWorkflowCompletionBuilder() { }

    public EphemeralWorkflowDefinition<TInput> Build() => throw new NotSupportedException();

    public Validation<EphemeralWorkflowDefinition<TInput>> TryBuild() =>
        throw new NotSupportedException();
}

public sealed class EphemeralWorkflowCompletionBuilder<TInput, TOutput>
{
    internal EphemeralWorkflowCompletionBuilder() { }

    public EphemeralWorkflowDefinition<TInput, TOutput> Build() =>
        throw new NotSupportedException();

    public Validation<EphemeralWorkflowDefinition<TInput, TOutput>> TryBuild() =>
        throw new NotSupportedException();
}

public sealed class DurableWorkflowCompletionBuilder<TInput>
{
    internal DurableWorkflowCompletionBuilder() { }

    public DurableWorkflowDefinition<TInput> Build() => throw new NotSupportedException();

    public Validation<DurableWorkflowDefinition<TInput>> TryBuild() =>
        throw new NotSupportedException();
}

public sealed class DurableWorkflowCompletionBuilder<TInput, TOutput>
{
    internal DurableWorkflowCompletionBuilder() { }

    public DurableWorkflowDefinition<TInput, TOutput> Build() =>
        throw new NotSupportedException();

    public Validation<DurableWorkflowDefinition<TInput, TOutput>> TryBuild() =>
        throw new NotSupportedException();
}

public sealed class EphemeralWorkflowDefinition<TInput>
{
    internal EphemeralWorkflowDefinition() { }

    public WorkflowMode Mode { get; }
    public DefinitionId DefinitionId { get; } = null!;
    public DefinitionVersion DefinitionVersion { get; } = null!;
    public DefinitionFingerprint DefinitionFingerprint { get; } = null!;
}

public sealed class EphemeralWorkflowDefinition<TInput, TOutput>
{
    internal EphemeralWorkflowDefinition() { }

    public WorkflowMode Mode { get; }
    public DefinitionId DefinitionId { get; } = null!;
    public DefinitionVersion DefinitionVersion { get; } = null!;
    public DefinitionFingerprint DefinitionFingerprint { get; } = null!;
}

public sealed class DurableWorkflowDefinition<TInput>
{
    internal DurableWorkflowDefinition() { }

    public WorkflowMode Mode { get; }
    public DefinitionId DefinitionId { get; } = null!;
    public DefinitionVersion DefinitionVersion { get; } = null!;
    public DefinitionFingerprint DefinitionFingerprint { get; } = null!;
    public DurableWorkflowRef<TInput> Reference { get; } = null!;
}

public sealed class DurableWorkflowDefinition<TInput, TOutput>
{
    internal DurableWorkflowDefinition() { }

    public WorkflowMode Mode { get; }
    public DefinitionId DefinitionId { get; } = null!;
    public DefinitionVersion DefinitionVersion { get; } = null!;
    public DefinitionFingerprint DefinitionFingerprint { get; } = null!;
    public DurableWorkflowRef<TInput, TOutput> Reference { get; } = null!;
}

public sealed class DurableWorkflowRef<TInput>
{
    internal DurableWorkflowRef() { }

    public WorkflowMode Mode { get; }
    public DefinitionId DefinitionId { get; } = null!;
    public DefinitionVersion DefinitionVersion { get; } = null!;
    public DefinitionFingerprint DefinitionFingerprint { get; } = null!;
}

public sealed class DurableWorkflowRef<TInput, TOutput>
{
    internal DurableWorkflowRef() { }

    public WorkflowMode Mode { get; }
    public DefinitionId DefinitionId { get; } = null!;
    public DefinitionVersion DefinitionVersion { get; } = null!;
    public DefinitionFingerprint DefinitionFingerprint { get; } = null!;
}

public sealed class EphemeralWorkflowBuilder<TInput, TState>
{
    internal EphemeralWorkflowBuilder() { }

    public EphemeralWorkflowBuilder<TInput, TState> Then<TStep>()
        where TStep : IStep<TState> => throw new NotSupportedException();

    public EphemeralWorkflowBuilder<TInput, TState> Then(
        Func<StepContext<TState>, ValueTask> body) => throw new NotSupportedException();

    public EphemeralWorkflowBuilder<TInput, TState> Then(
        Func<StepContext<TState>, CancellationToken, ValueTask> body) =>
        throw new NotSupportedException();

    public EphemeralWorkflowBuilder<TInput, TState> WithRetry(
        int maxAttempts,
        TimeSpan? fixedDelay = null) => throw new NotSupportedException();

    public EphemeralWorkflowBuilder<TInput, TState> WithStepTimeout(TimeSpan timeout) =>
        throw new NotSupportedException();

    public EphemeralWorkflowBuilder<TInput, TState> WithTransientPool(
        TransientPoolName pool) => throw new NotSupportedException();

    // A second call is rejected eagerly as SFE-AUTH-DEADLINE-001.
    public EphemeralWorkflowBuilder<TInput, TState> CompleteWithin(TimeSpan timeout) =>
        throw new NotSupportedException();

    public EphemeralWorkflowBuilder<TInput, TState> If(
        Func<ReadOnlyStateSnapshot<TState>, bool> condition,
        Action<EphemeralNestedBuilder<TInput, TState>> then,
        Action<EphemeralNestedBuilder<TInput, TState>>? otherwise = null) =>
        throw new NotSupportedException();

    public EphemeralWorkflowBuilder<TInput, TState> While(
        Func<ReadOnlyStateSnapshot<TState>, bool> condition,
        Action<EphemeralNestedBuilder<TInput, TState>> body) =>
        throw new NotSupportedException();

    public EphemeralWorkflowBuilder<TInput, TState> Wait(
        EventName eventName,
        Func<ReadOnlyStateSnapshot<TState>, CorrelationId> correlation) =>
        throw new NotSupportedException();

    public EphemeralWorkflowBuilder<TInput, TState> Wait(
        EventName eventName,
        Func<ReadOnlyStateSnapshot<TState>, CorrelationId> correlation,
        TimeSpan timeout) => throw new NotSupportedException();

    public EphemeralWorkflowBuilder<TInput, TState> Delay(TimeSpan duration) =>
        throw new NotSupportedException();

    // The action must add at least one branch; zero branches is SFE-AUTH-BRANCH-004 at build.
    public EphemeralWorkflowParallelJoinBuilder<TInput, TState, TResult> Parallel<TResult>(
        Action<EphemeralWorkflowParallelBranchScopeBuilder<TInput, TState, TResult>> branches) =>
        throw new NotSupportedException();

    public EphemeralForEachJoinBuilder<TInput, TState, TResult>
        ForEach<TItem, TItemState, TResult>(
            Func<ReadOnlyStateSnapshot<TState>, IReadOnlyList<TItem>> items,
            ForEachOptions options,
            Func<ForEachItemInput<TItem>, TItemState> input,
            Action<EphemeralItemBuilder<TItemState, TResult>> body) =>
        throw new NotSupportedException();

    public EphemeralWorkflowCompletionBuilder<TInput> End() =>
        throw new NotSupportedException();

    public EphemeralWorkflowCompletionBuilder<TInput> End(
        WorkflowOutcomeName outcome) => throw new NotSupportedException();

    public EphemeralWorkflowCompletionBuilder<TInput, TOutput> End<TOutput>(
        Func<ReadOnlyStateSnapshot<TState>, TOutput> output) =>
        throw new NotSupportedException();

    public EphemeralWorkflowCompletionBuilder<TInput, TOutput> End<TOutput>(
        Func<ReadOnlyStateSnapshot<TState>, TOutput> output,
        WorkflowOutcomeName outcome) => throw new NotSupportedException();
}

public sealed class DurableWorkflowBuilder<TInput, TState>
{
    internal DurableWorkflowBuilder() { }

    public DurableWorkflowBuilder<TInput, TState> Then<TStep>()
        where TStep : IStep<TState> => throw new NotSupportedException();

    public DurableWorkflowBuilder<TInput, TState> WithRetry(
        int maxAttempts,
        TimeSpan? fixedDelay = null) => throw new NotSupportedException();

    public DurableWorkflowBuilder<TInput, TState> WithStepTimeout(TimeSpan timeout) =>
        throw new NotSupportedException();

    // A second call is rejected eagerly as SFE-AUTH-DEADLINE-001.
    public DurableWorkflowBuilder<TInput, TState> CompleteWithin(TimeSpan timeout) =>
        throw new NotSupportedException();

    public DurableWorkflowBuilder<TInput, TState> If(
        Func<ReadOnlyStateSnapshot<TState>, bool> condition,
        Action<DurableNestedBuilder<TInput, TState>> then,
        Action<DurableNestedBuilder<TInput, TState>>? otherwise = null) =>
        throw new NotSupportedException();

    public DurableWorkflowBuilder<TInput, TState> While(
        Func<ReadOnlyStateSnapshot<TState>, bool> condition,
        Action<DurableNestedBuilder<TInput, TState>> body) => throw new NotSupportedException();

    public DurableWorkflowBuilder<TInput, TState> Wait(
        EventName eventName,
        Func<ReadOnlyStateSnapshot<TState>, CorrelationId> correlation) =>
        throw new NotSupportedException();

    public DurableWorkflowBuilder<TInput, TState> Wait(
        EventName eventName,
        Func<ReadOnlyStateSnapshot<TState>, CorrelationId> correlation,
        TimeSpan timeout) => throw new NotSupportedException();

    public DurableWorkflowBuilder<TInput, TState> Delay(TimeSpan duration) =>
        throw new NotSupportedException();

    // The action must add at least one branch; zero branches is SFE-AUTH-BRANCH-004 at build.
    public DurableWorkflowParallelJoinBuilder<TInput, TState, TResult> Parallel<TResult>(
        Action<DurableWorkflowParallelBranchScopeBuilder<TInput, TState, TResult>> branches) =>
        throw new NotSupportedException();

    public DurableForEachJoinBuilder<TInput, TState, TResult>
        ForEach<TItem, TItemState, TResult>(
            Func<ReadOnlyStateSnapshot<TState>, IReadOnlyList<TItem>> items,
            ForEachOptions options,
            Func<ForEachItemInput<TItem>, TItemState> input,
            Action<DurableItemBuilder<TItemState, TResult>> body) =>
        throw new NotSupportedException();

    public DurableWorkflowBuilder<TInput, TState> AcquireResources(
        ResourceLeaseRequest request,
        Action<DurableLeaseWorkflowBuilder<TInput, TState>> body) =>
        throw new NotSupportedException();

    public DurableWorkflowBuilder<TInput, TState> AcquireResources(
        Func<ReadOnlyStateSnapshot<TState>, ResourceLeaseRequest> request,
        Action<DurableLeaseWorkflowBuilder<TInput, TState>> body) =>
        throw new NotSupportedException();

    public DurableWorkflowCompletionBuilder<TInput> ContinueAsNew(
        Func<ReadOnlyStateSnapshot<TState>, TState> replacementState) =>
        throw new NotSupportedException();

    public DurableWorkflowCompletionBuilder<TInput> End() =>
        throw new NotSupportedException();

    public DurableWorkflowCompletionBuilder<TInput> End(
        WorkflowOutcomeName outcome) => throw new NotSupportedException();

    public DurableWorkflowCompletionBuilder<TInput, TOutput> End<TOutput>(
        Func<ReadOnlyStateSnapshot<TState>, TOutput> output) =>
        throw new NotSupportedException();

    public DurableWorkflowCompletionBuilder<TInput, TOutput> End<TOutput>(
        Func<ReadOnlyStateSnapshot<TState>, TOutput> output,
        WorkflowOutcomeName outcome) => throw new NotSupportedException();
}

public sealed class EphemeralNestedBuilder<TInput, TState>
{
    internal EphemeralNestedBuilder() { }

    public EphemeralNestedBuilder<TInput, TState> Then<TStep>()
        where TStep : IStep<TState> => throw new NotSupportedException();

    public EphemeralNestedBuilder<TInput, TState> Then(
        Func<StepContext<TState>, ValueTask> body) => throw new NotSupportedException();

    public EphemeralNestedBuilder<TInput, TState> Then(
        Func<StepContext<TState>, CancellationToken, ValueTask> body) =>
        throw new NotSupportedException();

    public EphemeralNestedBuilder<TInput, TState> WithRetry(
        int maxAttempts,
        TimeSpan? fixedDelay = null) => throw new NotSupportedException();

    public EphemeralNestedBuilder<TInput, TState> WithStepTimeout(TimeSpan timeout) =>
        throw new NotSupportedException();

    public EphemeralNestedBuilder<TInput, TState> WithTransientPool(TransientPoolName pool) =>
        throw new NotSupportedException();

    public EphemeralNestedBuilder<TInput, TState> If(
        Func<ReadOnlyStateSnapshot<TState>, bool> condition,
        Action<EphemeralNestedBuilder<TInput, TState>> then,
        Action<EphemeralNestedBuilder<TInput, TState>>? otherwise = null) =>
        throw new NotSupportedException();

    public EphemeralNestedBuilder<TInput, TState> Wait(
        EventName eventName,
        Func<ReadOnlyStateSnapshot<TState>, CorrelationId> correlation) =>
        throw new NotSupportedException();

    public EphemeralNestedBuilder<TInput, TState> Wait(
        EventName eventName,
        Func<ReadOnlyStateSnapshot<TState>, CorrelationId> correlation,
        TimeSpan timeout) => throw new NotSupportedException();

    public EphemeralNestedBuilder<TInput, TState> Delay(TimeSpan duration) =>
        throw new NotSupportedException();

}

public sealed class DurableNestedBuilder<TInput, TState>
{
    internal DurableNestedBuilder() { }

    public DurableNestedBuilder<TInput, TState> Then<TStep>()
        where TStep : IStep<TState> => throw new NotSupportedException();

    public DurableNestedBuilder<TInput, TState> WithRetry(
        int maxAttempts,
        TimeSpan? fixedDelay = null) => throw new NotSupportedException();

    public DurableNestedBuilder<TInput, TState> WithStepTimeout(TimeSpan timeout) =>
        throw new NotSupportedException();

    public DurableNestedBuilder<TInput, TState> If(
        Func<ReadOnlyStateSnapshot<TState>, bool> condition,
        Action<DurableNestedBuilder<TInput, TState>> then,
        Action<DurableNestedBuilder<TInput, TState>>? otherwise = null) =>
        throw new NotSupportedException();

    public DurableNestedBuilder<TInput, TState> Wait(
        EventName eventName,
        Func<ReadOnlyStateSnapshot<TState>, CorrelationId> correlation) =>
        throw new NotSupportedException();

    public DurableNestedBuilder<TInput, TState> Wait(
        EventName eventName,
        Func<ReadOnlyStateSnapshot<TState>, CorrelationId> correlation,
        TimeSpan timeout) => throw new NotSupportedException();

    public DurableNestedBuilder<TInput, TState> Delay(TimeSpan duration) =>
        throw new NotSupportedException();

    public DurableNestedBuilder<TInput, TState> AcquireResources(
        ResourceLeaseRequest request,
        Action<DurableLeaseNestedBuilder<TInput, TState>> body) =>
        throw new NotSupportedException();

    public DurableNestedBuilder<TInput, TState> AcquireResources(
        Func<ReadOnlyStateSnapshot<TState>, ResourceLeaseRequest> request,
        Action<DurableLeaseNestedBuilder<TInput, TState>> body) =>
        throw new NotSupportedException();
}

public sealed class EphemeralBranchBuilder<TState, TResult>
{
    internal EphemeralBranchBuilder() { }

    public EphemeralBranchBuilder<TState, TResult> Then<TStep>()
        where TStep : IStep<TState> => throw new NotSupportedException();

    public EphemeralBranchBuilder<TState, TResult> Then(
        Func<StepContext<TState>, ValueTask> body) => throw new NotSupportedException();

    public EphemeralBranchBuilder<TState, TResult> Then(
        Func<StepContext<TState>, CancellationToken, ValueTask> body) =>
        throw new NotSupportedException();

    public EphemeralBranchBuilder<TState, TResult> WithRetry(
        int maxAttempts,
        TimeSpan? fixedDelay = null) => throw new NotSupportedException();

    public EphemeralBranchBuilder<TState, TResult> WithStepTimeout(TimeSpan timeout) =>
        throw new NotSupportedException();

    public EphemeralBranchBuilder<TState, TResult> WithTransientPool(TransientPoolName pool) =>
        throw new NotSupportedException();

    public EphemeralBranchBuilder<TState, TResult> If(
        Func<ReadOnlyStateSnapshot<TState>, bool> condition,
        Action<EphemeralBranchBuilder<TState, TResult>> then,
        Action<EphemeralBranchBuilder<TState, TResult>>? otherwise = null) =>
        throw new NotSupportedException();

    public EphemeralBranchBuilder<TState, TResult> Wait(
        EventName eventName,
        Func<ReadOnlyStateSnapshot<TState>, CorrelationId> correlation) =>
        throw new NotSupportedException();

    public EphemeralBranchBuilder<TState, TResult> Wait(
        EventName eventName,
        Func<ReadOnlyStateSnapshot<TState>, CorrelationId> correlation,
        TimeSpan timeout) => throw new NotSupportedException();

    public EphemeralBranchBuilder<TState, TResult> Delay(TimeSpan duration) =>
        throw new NotSupportedException();

    public EphemeralBranchBuilder<TState, TResult> Return(
        Func<ReadOnlyStateSnapshot<TState>, TResult> result) =>
        throw new NotSupportedException();
}

public sealed class EphemeralItemBuilder<TState, TResult>
{
    internal EphemeralItemBuilder() { }

    public EphemeralItemBuilder<TState, TResult> Then<TStep>()
        where TStep : IStep<TState> => throw new NotSupportedException();

    public EphemeralItemBuilder<TState, TResult> Then(
        Func<StepContext<TState>, ValueTask> body) => throw new NotSupportedException();

    public EphemeralItemBuilder<TState, TResult> Then(
        Func<StepContext<TState>, CancellationToken, ValueTask> body) =>
        throw new NotSupportedException();

    public EphemeralItemBuilder<TState, TResult> WithRetry(
        int maxAttempts,
        TimeSpan? fixedDelay = null) => throw new NotSupportedException();

    public EphemeralItemBuilder<TState, TResult> WithStepTimeout(TimeSpan timeout) =>
        throw new NotSupportedException();

    public EphemeralItemBuilder<TState, TResult> WithTransientPool(TransientPoolName pool) =>
        throw new NotSupportedException();

    public EphemeralItemBuilder<TState, TResult> If(
        Func<ReadOnlyStateSnapshot<TState>, bool> condition,
        Action<EphemeralItemBuilder<TState, TResult>> then,
        Action<EphemeralItemBuilder<TState, TResult>>? otherwise = null) =>
        throw new NotSupportedException();

    public EphemeralItemBuilder<TState, TResult> Wait(
        EventName eventName,
        Func<ReadOnlyStateSnapshot<TState>, CorrelationId> correlation) =>
        throw new NotSupportedException();

    public EphemeralItemBuilder<TState, TResult> Wait(
        EventName eventName,
        Func<ReadOnlyStateSnapshot<TState>, CorrelationId> correlation,
        TimeSpan timeout) => throw new NotSupportedException();

    public EphemeralItemBuilder<TState, TResult> Delay(TimeSpan duration) =>
        throw new NotSupportedException();

    public EphemeralItemBuilder<TState, TResult> Return(
        Func<ReadOnlyStateSnapshot<TState>, TResult> result) =>
        throw new NotSupportedException();
}

public sealed class DurableBranchBuilder<TState, TResult>
{
    internal DurableBranchBuilder() { }

    public DurableBranchBuilder<TState, TResult> Then<TStep>()
        where TStep : IStep<TState> => throw new NotSupportedException();

    public DurableBranchBuilder<TState, TResult> WithRetry(
        int maxAttempts,
        TimeSpan? fixedDelay = null) => throw new NotSupportedException();

    public DurableBranchBuilder<TState, TResult> WithStepTimeout(TimeSpan timeout) =>
        throw new NotSupportedException();

    public DurableBranchBuilder<TState, TResult> If(
        Func<ReadOnlyStateSnapshot<TState>, bool> condition,
        Action<DurableBranchBuilder<TState, TResult>> then,
        Action<DurableBranchBuilder<TState, TResult>>? otherwise = null) =>
        throw new NotSupportedException();

    public DurableBranchBuilder<TState, TResult> Wait(
        EventName eventName,
        Func<ReadOnlyStateSnapshot<TState>, CorrelationId> correlation) =>
        throw new NotSupportedException();

    public DurableBranchBuilder<TState, TResult> Wait(
        EventName eventName,
        Func<ReadOnlyStateSnapshot<TState>, CorrelationId> correlation,
        TimeSpan timeout) => throw new NotSupportedException();

    public DurableBranchBuilder<TState, TResult> Delay(TimeSpan duration) =>
        throw new NotSupportedException();

    public DurableBranchBuilder<TState, TResult> AcquireResources(
        ResourceLeaseRequest request,
        Action<DurableLeaseBranchBuilder<TState, TResult>> body) =>
        throw new NotSupportedException();

    public DurableBranchBuilder<TState, TResult> AcquireResources(
        Func<ReadOnlyStateSnapshot<TState>, ResourceLeaseRequest> request,
        Action<DurableLeaseBranchBuilder<TState, TResult>> body) =>
        throw new NotSupportedException();

    public DurableBranchBuilder<TState, TResult> Return(
        Func<ReadOnlyStateSnapshot<TState>, TResult> result) =>
        throw new NotSupportedException();
}

public sealed class DurableItemBuilder<TState, TResult>
{
    internal DurableItemBuilder() { }

    public DurableItemBuilder<TState, TResult> Then<TStep>()
        where TStep : IStep<TState> => throw new NotSupportedException();

    public DurableItemBuilder<TState, TResult> WithRetry(
        int maxAttempts,
        TimeSpan? fixedDelay = null) => throw new NotSupportedException();

    public DurableItemBuilder<TState, TResult> WithStepTimeout(TimeSpan timeout) =>
        throw new NotSupportedException();

    public DurableItemBuilder<TState, TResult> If(
        Func<ReadOnlyStateSnapshot<TState>, bool> condition,
        Action<DurableItemBuilder<TState, TResult>> then,
        Action<DurableItemBuilder<TState, TResult>>? otherwise = null) =>
        throw new NotSupportedException();

    public DurableItemBuilder<TState, TResult> Wait(
        EventName eventName,
        Func<ReadOnlyStateSnapshot<TState>, CorrelationId> correlation) =>
        throw new NotSupportedException();

    public DurableItemBuilder<TState, TResult> Wait(
        EventName eventName,
        Func<ReadOnlyStateSnapshot<TState>, CorrelationId> correlation,
        TimeSpan timeout) => throw new NotSupportedException();

    public DurableItemBuilder<TState, TResult> Delay(TimeSpan duration) =>
        throw new NotSupportedException();

    public DurableItemBuilder<TState, TResult> AcquireResources(
        ResourceLeaseRequest request,
        Action<DurableLeaseItemBuilder<TState, TResult>> body) =>
        throw new NotSupportedException();

    public DurableItemBuilder<TState, TResult> AcquireResources(
        Func<ReadOnlyStateSnapshot<TState>, ResourceLeaseRequest> request,
        Action<DurableLeaseItemBuilder<TState, TResult>> body) =>
        throw new NotSupportedException();

    public DurableItemBuilder<TState, TResult> Return(
        Func<ReadOnlyStateSnapshot<TState>, TResult> result) =>
        throw new NotSupportedException();
}

public sealed class DurableLeaseWorkflowBuilder<TInput, TState>
{
    internal DurableLeaseWorkflowBuilder() { }

    public DurableLeaseWorkflowBuilder<TInput, TState> Then<TStep>()
        where TStep : IStep<TState> => throw new NotSupportedException();

    public DurableLeaseWorkflowBuilder<TInput, TState> WithRetry(
        int maxAttempts,
        TimeSpan? fixedDelay = null) => throw new NotSupportedException();

    public DurableLeaseWorkflowBuilder<TInput, TState> WithStepTimeout(TimeSpan timeout) =>
        throw new NotSupportedException();

    public DurableLeaseWorkflowBuilder<TInput, TState> If(
        Func<ReadOnlyStateSnapshot<TState>, bool> condition,
        Action<DurableLeaseNestedBuilder<TInput, TState>> then,
        Action<DurableLeaseNestedBuilder<TInput, TState>>? otherwise = null) =>
        throw new NotSupportedException();

    public DurableLeaseWorkflowBuilder<TInput, TState> Wait(
        EventName eventName,
        Func<ReadOnlyStateSnapshot<TState>, CorrelationId> correlation) =>
        throw new NotSupportedException();

    public DurableLeaseWorkflowBuilder<TInput, TState> Wait(
        EventName eventName,
        Func<ReadOnlyStateSnapshot<TState>, CorrelationId> correlation,
        TimeSpan timeout) => throw new NotSupportedException();

    public DurableLeaseWorkflowBuilder<TInput, TState> Delay(TimeSpan duration) =>
        throw new NotSupportedException();

}

public sealed class DurableLeaseNestedBuilder<TInput, TState>
{
    internal DurableLeaseNestedBuilder() { }

    public DurableLeaseNestedBuilder<TInput, TState> Then<TStep>()
        where TStep : IStep<TState> => throw new NotSupportedException();

    public DurableLeaseNestedBuilder<TInput, TState> WithRetry(
        int maxAttempts,
        TimeSpan? fixedDelay = null) => throw new NotSupportedException();

    public DurableLeaseNestedBuilder<TInput, TState> WithStepTimeout(TimeSpan timeout) =>
        throw new NotSupportedException();

    public DurableLeaseNestedBuilder<TInput, TState> If(
        Func<ReadOnlyStateSnapshot<TState>, bool> condition,
        Action<DurableLeaseNestedBuilder<TInput, TState>> then,
        Action<DurableLeaseNestedBuilder<TInput, TState>>? otherwise = null) =>
        throw new NotSupportedException();

    public DurableLeaseNestedBuilder<TInput, TState> Wait(
        EventName eventName,
        Func<ReadOnlyStateSnapshot<TState>, CorrelationId> correlation) =>
        throw new NotSupportedException();

    public DurableLeaseNestedBuilder<TInput, TState> Wait(
        EventName eventName,
        Func<ReadOnlyStateSnapshot<TState>, CorrelationId> correlation,
        TimeSpan timeout) => throw new NotSupportedException();

    public DurableLeaseNestedBuilder<TInput, TState> Delay(TimeSpan duration) =>
        throw new NotSupportedException();

}

public sealed class DurableLeaseBranchBuilder<TState, TResult>
{
    internal DurableLeaseBranchBuilder() { }

    public DurableLeaseBranchBuilder<TState, TResult> Then<TStep>()
        where TStep : IStep<TState> => throw new NotSupportedException();

    public DurableLeaseBranchBuilder<TState, TResult> WithRetry(
        int maxAttempts,
        TimeSpan? fixedDelay = null) => throw new NotSupportedException();

    public DurableLeaseBranchBuilder<TState, TResult> WithStepTimeout(TimeSpan timeout) =>
        throw new NotSupportedException();

    public DurableLeaseBranchBuilder<TState, TResult> If(
        Func<ReadOnlyStateSnapshot<TState>, bool> condition,
        Action<DurableLeaseBranchBuilder<TState, TResult>> then,
        Action<DurableLeaseBranchBuilder<TState, TResult>>? otherwise = null) =>
        throw new NotSupportedException();

    public DurableLeaseBranchBuilder<TState, TResult> Wait(
        EventName eventName,
        Func<ReadOnlyStateSnapshot<TState>, CorrelationId> correlation) =>
        throw new NotSupportedException();

    public DurableLeaseBranchBuilder<TState, TResult> Wait(
        EventName eventName,
        Func<ReadOnlyStateSnapshot<TState>, CorrelationId> correlation,
        TimeSpan timeout) => throw new NotSupportedException();

    public DurableLeaseBranchBuilder<TState, TResult> Delay(TimeSpan duration) =>
        throw new NotSupportedException();

    public DurableLeaseBranchBuilder<TState, TResult> Return(
        Func<ReadOnlyStateSnapshot<TState>, TResult> result) =>
        throw new NotSupportedException();
}

public sealed class DurableLeaseItemBuilder<TState, TResult>
{
    internal DurableLeaseItemBuilder() { }

    public DurableLeaseItemBuilder<TState, TResult> Then<TStep>()
        where TStep : IStep<TState> => throw new NotSupportedException();

    public DurableLeaseItemBuilder<TState, TResult> WithRetry(
        int maxAttempts,
        TimeSpan? fixedDelay = null) => throw new NotSupportedException();

    public DurableLeaseItemBuilder<TState, TResult> WithStepTimeout(TimeSpan timeout) =>
        throw new NotSupportedException();

    public DurableLeaseItemBuilder<TState, TResult> If(
        Func<ReadOnlyStateSnapshot<TState>, bool> condition,
        Action<DurableLeaseItemBuilder<TState, TResult>> then,
        Action<DurableLeaseItemBuilder<TState, TResult>>? otherwise = null) =>
        throw new NotSupportedException();

    public DurableLeaseItemBuilder<TState, TResult> Wait(
        EventName eventName,
        Func<ReadOnlyStateSnapshot<TState>, CorrelationId> correlation) =>
        throw new NotSupportedException();

    public DurableLeaseItemBuilder<TState, TResult> Wait(
        EventName eventName,
        Func<ReadOnlyStateSnapshot<TState>, CorrelationId> correlation,
        TimeSpan timeout) => throw new NotSupportedException();

    public DurableLeaseItemBuilder<TState, TResult> Delay(TimeSpan duration) =>
        throw new NotSupportedException();

    public DurableLeaseItemBuilder<TState, TResult> Return(
        Func<ReadOnlyStateSnapshot<TState>, TResult> result) =>
        throw new NotSupportedException();
}

// Root-only fixed-branch Parallel scopes and joins.

public sealed class EphemeralWorkflowParallelBranchScopeBuilder<TInput, TState, TResult>
{
    internal EphemeralWorkflowParallelBranchScopeBuilder() { }

    public EphemeralWorkflowParallelBranchScopeBuilder<TInput, TState, TResult>
        Branch<TBranchState>(
            AuthoredBranchId branchId,
            Func<ReadOnlyStateSnapshot<TState>, TBranchState> input,
            Action<EphemeralBranchBuilder<TBranchState, TResult>> body) =>
        throw new NotSupportedException();
}

public sealed class EphemeralWorkflowParallelJoinBuilder<TInput, TState, TResult>
{
    internal EphemeralWorkflowParallelJoinBuilder() { }

    public EphemeralWorkflowBuilder<TInput, TState> WhenAll(
        Func<ReadOnlyStateSnapshot<TState>,
            IReadOnlyList<BranchResult<TResult>>,
            TState> merge) => throw new NotSupportedException();

    public EphemeralWorkflowBuilder<TInput, TState> WhenAllOutcomes(
        Func<ReadOnlyStateSnapshot<TState>,
            IReadOnlyList<BranchOutcome<TResult>>,
            TState> merge) => throw new NotSupportedException();
}

public sealed class DurableWorkflowParallelBranchScopeBuilder<TInput, TState, TResult>
{
    internal DurableWorkflowParallelBranchScopeBuilder() { }

    public DurableWorkflowParallelBranchScopeBuilder<TInput, TState, TResult>
        Branch<TBranchState>(
            AuthoredBranchId branchId,
            Func<ReadOnlyStateSnapshot<TState>, TBranchState> input,
            Action<DurableBranchBuilder<TBranchState, TResult>> body) =>
        throw new NotSupportedException();
}

public sealed class DurableWorkflowParallelJoinBuilder<TInput, TState, TResult>
{
    internal DurableWorkflowParallelJoinBuilder() { }

    public DurableWorkflowBuilder<TInput, TState> WhenAll(
        Func<ReadOnlyStateSnapshot<TState>,
            IReadOnlyList<BranchResult<TResult>>,
            TState> merge) => throw new NotSupportedException();

    public DurableWorkflowBuilder<TInput, TState> WhenAllOutcomes(
        Func<ReadOnlyStateSnapshot<TState>,
            IReadOnlyList<BranchOutcome<TResult>>,
            TState> merge) => throw new NotSupportedException();
}

// Root-only bounded dynamic fan-out joins.

public sealed class EphemeralForEachJoinBuilder<TInput, TState, TResult>
{
    internal EphemeralForEachJoinBuilder() { }

    public EphemeralWorkflowBuilder<TInput, TState> WhenAll(
        Func<ReadOnlyStateSnapshot<TState>,
            IReadOnlyList<ForEachItemResult<TResult>>,
            TState> merge) => throw new NotSupportedException();

    public EphemeralWorkflowBuilder<TInput, TState> WhenAllOutcomes(
        Func<ReadOnlyStateSnapshot<TState>,
            IReadOnlyList<ForEachItemOutcome<TResult>>,
            TState> merge) => throw new NotSupportedException();
}

public sealed class DurableForEachJoinBuilder<TInput, TState, TResult>
{
    internal DurableForEachJoinBuilder() { }

    public DurableWorkflowBuilder<TInput, TState> WhenAll(
        Func<ReadOnlyStateSnapshot<TState>,
            IReadOnlyList<ForEachItemResult<TResult>>,
            TState> merge) => throw new NotSupportedException();

    public DurableWorkflowBuilder<TInput, TState> WhenAllOutcomes(
        Func<ReadOnlyStateSnapshot<TState>,
            IReadOnlyList<ForEachItemOutcome<TResult>>,
            TState> merge) => throw new NotSupportedException();
}
