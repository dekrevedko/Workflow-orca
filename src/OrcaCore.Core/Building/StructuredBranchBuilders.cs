using OrcaCore.Abstractions.Ids;
using OrcaCore.Abstractions.Instances;
using OrcaCore.Abstractions.Steps;
using OrcaCore.Core.Definitions;

namespace OrcaCore.Core.Building;

/// <summary>
/// Immutable view of parent state supplied to branch projection and merge delegates.
/// </summary>
public sealed record ReadOnlyParentSnapshot<TState>(TState Value);

/// <summary>
/// Immutable view of branch-private state supplied to its result projector.
/// </summary>
public sealed record ReadOnlyBranchSnapshot<TState>(TState Value);

/// <summary>
/// Typed result produced by one structured branch.
/// </summary>
public sealed record BranchResult<TResult>(string BranchId, int Ordinal, TResult Value);

/// <summary>
/// Stable partition input used to initialize one ephemeral ForEach item fiber.
/// </summary>
public sealed record ForEachItemInput<TItem>(int Index, IReadOnlyList<TItem> Items);

/// <summary>
/// Terminal state recorded for one ephemeral ForEach item fiber.
/// </summary>
public enum ForEachItemTerminalStatus
{
    Succeeded,
    Failed,
    Cancelled
}

/// <summary>
/// Runtime-owned ordered outcome for one ephemeral ForEach item fiber.
/// </summary>
public sealed record ForEachItemOutcome<TResult>(
    int Index,
    ForEachItemTerminalStatus Status,
    TResult? Result,
    string? Failure);

/// <summary>
/// Authors branches with isolated state and one common result type.
/// </summary>
public sealed class BranchScopeBuilder<TParentState, TResult>
{
    private readonly List<StructuredBranchAuthoring> branches = [];

    /// <summary>
    /// Adds one named branch with a parent-to-private-state projection and an explicit return.
    /// </summary>
    public BranchScopeBuilder<TParentState, TResult> Branch<TBranchState>(
        string branchId,
        Func<ReadOnlyParentSnapshot<TParentState>, TBranchState> inputProjector,
        Action<BranchBuilder<TBranchState, TResult>> build)
    {
        ArgumentNullException.ThrowIfNull(inputProjector);
        ArgumentNullException.ThrowIfNull(build);

        var branch = new BranchBuilder<TBranchState, TResult>();
        build(branch);
        branches.Add(new StructuredBranchAuthoring(
            branchId,
            typeof(TBranchState),
            inputProjector,
            branch.Instructions));
        return this;
    }

    internal IReadOnlyList<StructuredBranchAuthoring> Branches => branches;
}

/// <summary>
/// Authors one isolated branch body and its typed terminal result.
/// </summary>
public sealed class BranchBuilder<TBranchState, TResult>
{
    private readonly List<BranchAuthoringInstruction> instructions = [];
    private WorkflowPolicySet pendingPolicies = WorkflowPolicySet.Empty;

    /// <summary>
    /// Adds a parameterless business step to the branch-private state flow.
    /// </summary>
    public BranchBuilder<TBranchState, TResult> Then<TStep>()
        where TStep : IStep<TBranchState>, new()
    {
        instructions.Add(new BranchStepAuthoringInstruction(() => new TStep(), ConsumePendingPolicies()));
        return this;
    }

    /// <summary>
    /// Adds a business step factory to the branch-private state flow.
    /// </summary>
    public BranchBuilder<TBranchState, TResult> Then(Func<IStep<TBranchState>> stepFactory)
    {
        ArgumentNullException.ThrowIfNull(stepFactory);
        instructions.Add(new BranchStepAuthoringInstruction(stepFactory, ConsumePendingPolicies()));
        return this;
    }

    /// <summary>
    /// Applies a retry policy to the next branch step.
    /// </summary>
    public BranchBuilder<TBranchState, TResult> WithRetry(int maxAttempts, TimeSpan? backoff = null)
    {
        if (maxAttempts <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(maxAttempts), maxAttempts, "Retry attempts must be positive.");
        }

        if (backoff < TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(backoff), backoff, "Retry backoff cannot be negative.");
        }

        pendingPolicies = pendingPolicies.WithRetry(maxAttempts, backoff ?? TimeSpan.Zero);
        return this;
    }

    /// <summary>
    /// Applies a timeout to the next branch step.
    /// </summary>
    public BranchBuilder<TBranchState, TResult> WithTimeout(TimeSpan duration)
    {
        if (duration <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(duration), duration, "Timeout must be positive.");
        }

        pendingPolicies = pendingPolicies.WithTimeout(duration);
        return this;
    }

    /// <summary>
    /// Allows operator cancellation to interrupt the next branch step.
    /// </summary>
    public BranchBuilder<TBranchState, TResult> WithCancellation()
    {
        pendingPolicies = pendingPolicies.WithCancellation();
        return this;
    }

    /// <summary>
    /// Applies an ephemeral in-process governance pool key to the next branch step.
    /// Durable compilation rejects this transient policy.
    /// </summary>
    public BranchBuilder<TBranchState, TResult> WithPoolKey(string poolKey)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(poolKey);
        pendingPolicies = pendingPolicies.WithPoolKey(poolKey);
        return this;
    }

    /// <summary>
    /// Adds a structural resident wait owned by this branch fiber.
    /// </summary>
    public BranchBuilder<TBranchState, TResult> Wait(
        string eventName,
        Func<TBranchState, CorrelationId> correlationSelector)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(eventName);
        ArgumentNullException.ThrowIfNull(correlationSelector);
        instructions.Add(new BranchWaitAuthoringInstruction(
            eventName,
            correlationSelector,
            WaitMode.Resident));
        return this;
    }

    /// <summary>
    /// Adds a structural delay owned by this branch fiber.
    /// </summary>
    public BranchBuilder<TBranchState, TResult> Delay(TimeSpan duration)
    {
        if (duration <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(duration), duration, "Delay must be positive.");
        }

        instructions.Add(new BranchDelayAuthoringInstruction(duration));
        return this;
    }

    /// <summary>
    /// Adds nested cooperative branches whose merge replaces this branch's private state.
    /// </summary>
    public BranchBuilder<TBranchState, TResult> Parallel<TNestedResult>(
        Action<BranchScopeBuilder<TBranchState, TNestedResult>> branches,
        Func<ReadOnlyParentSnapshot<TBranchState>, IReadOnlyList<BranchResult<TNestedResult>>, TBranchState> merge)
    {
        ArgumentNullException.ThrowIfNull(branches);
        ArgumentNullException.ThrowIfNull(merge);

        var scope = new BranchScopeBuilder<TBranchState, TNestedResult>();
        branches(scope);
        instructions.Add(new BranchStructuredScopeAuthoringInstruction(
            "Parallel",
            typeof(TBranchState),
            typeof(TNestedResult),
            scope.Branches,
            merge));
        return this;
    }

    /// <summary>
    /// Adds a nested first-terminal race whose merge replaces this branch's private state.
    /// </summary>
    public BranchBuilder<TBranchState, TResult> WhenFirst<TNestedResult>(
        Action<BranchScopeBuilder<TBranchState, TNestedResult>> branches,
        Func<ReadOnlyParentSnapshot<TBranchState>, BranchResult<TNestedResult>, TBranchState> merge)
    {
        ArgumentNullException.ThrowIfNull(branches);
        ArgumentNullException.ThrowIfNull(merge);

        var scope = new BranchScopeBuilder<TBranchState, TNestedResult>();
        branches(scope);
        instructions.Add(new BranchStructuredScopeAuthoringInstruction(
            "WhenFirst",
            typeof(TBranchState),
            typeof(TNestedResult),
            scope.Branches,
            merge));
        return this;
    }

    /// <summary>
    /// Terminates the branch and projects its serializable result.
    /// </summary>
    public BranchBuilder<TBranchState, TResult> Return(
        Func<ReadOnlyBranchSnapshot<TBranchState>, TResult> resultProjector)
    {
        ArgumentNullException.ThrowIfNull(resultProjector);
        instructions.Add(new BranchReturnAuthoringInstruction(typeof(TResult), resultProjector));
        return this;
    }

    internal IReadOnlyList<BranchAuthoringInstruction> Instructions => instructions;

    private WorkflowPolicySet ConsumePendingPolicies()
    {
        var policies = pendingPolicies;
        pendingPolicies = WorkflowPolicySet.Empty;
        return policies;
    }
}

internal sealed record StructuredBranchAuthoring(
    string BranchId,
    Type BranchStateType,
    Delegate InputProjector,
    IReadOnlyList<BranchAuthoringInstruction> Instructions);

internal abstract record BranchAuthoringInstruction;

internal sealed record BranchStepAuthoringInstruction(
    Delegate StepFactory,
    WorkflowPolicySet Policies) : BranchAuthoringInstruction;

internal sealed record BranchWaitAuthoringInstruction(
    string EventName,
    Delegate CorrelationSelector,
    WaitMode Mode) : BranchAuthoringInstruction;

internal sealed record BranchDelayAuthoringInstruction(TimeSpan Duration) : BranchAuthoringInstruction;

internal sealed record BranchStructuredScopeAuthoringInstruction(
    string ScopeKind,
    Type ParentStateType,
    Type ResultType,
    IReadOnlyList<StructuredBranchAuthoring> Branches,
    Delegate Merge) : BranchAuthoringInstruction;

internal sealed record BranchReturnAuthoringInstruction(
    Type ResultType,
    Delegate ResultProjector) : BranchAuthoringInstruction;
