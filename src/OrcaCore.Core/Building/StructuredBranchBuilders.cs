using OrcaCore.Abstractions.Ids;
using OrcaCore.Core.Authoring;
using OrcaCore.Core.Definitions;
using OrcaCore.Core.Execution;

namespace OrcaCore.Core.Building;

/// <summary>
/// Immutable view of parent state supplied to branch projection and merge delegates.
/// </summary>
internal sealed record ReadOnlyParentSnapshot<TState>(TState Value);

/// <summary>
/// Immutable view of branch-private state supplied to its result projector.
/// </summary>
internal sealed record ReadOnlyBranchSnapshot<TState>(TState Value);

/// <summary>
/// Typed result produced by one structured branch.
/// </summary>
internal sealed record BranchResult<TResult>(string BranchId, int Ordinal, TResult Value);

/// <summary>
/// Stable partition input used to initialize one ephemeral ForEach item fiber.
/// </summary>
internal sealed record ForEachItemInput<TItem>(int Index, IReadOnlyList<TItem> Items);

/// <summary>
/// Terminal state recorded for one ephemeral ForEach item fiber.
/// </summary>
internal enum ForEachItemTerminalStatus
{
    Succeeded,
    Failed,
    Cancelled
}

/// <summary>
/// Runtime-owned ordered outcome for one ephemeral ForEach item fiber.
/// </summary>
internal sealed record ForEachItemOutcome<TResult>(
    int Index,
    ForEachItemTerminalStatus Status,
    TResult? Result,
    FiberFailure? Failure);

/// <summary>
/// Authors branches with isolated state and one common result type.
/// </summary>
internal sealed class BranchScopeBuilder<TParentState, TResult>
{
    private readonly List<StructuredBranchAuthoring> branches = [];
    private readonly AuthoringLifecycleHandle lifecycle;

    internal BranchScopeBuilder(AuthoringLifecycleHandle lifecycle)
    {
        this.lifecycle = lifecycle;
    }

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

        using var operation = lifecycle.BeginMutation(
            $"{lifecycle.Location}/parallel:{branches.Count:D8}");
        var branchHandle = lifecycle.CreateLexical(
            $"{lifecycle.Location}/parallel:{branches.Count:D8}");
        var branch = new BranchBuilder<TBranchState, TResult>(branchHandle);
        try
        {
            build(branch);
        }
        finally
        {
            branchHandle.Expire();
        }

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
internal sealed class BranchBuilder<TBranchState, TResult>
{
    private readonly List<BranchAuthoringInstruction> instructions = [];
    private readonly AuthoringLifecycleHandle lifecycle;
    private WorkflowPolicySet pendingPolicies = WorkflowPolicySet.Empty;

    internal BranchBuilder(AuthoringLifecycleHandle lifecycle)
    {
        this.lifecycle = lifecycle;
    }

    /// <summary>
    /// Adds a parameterless business step to the branch-private state flow.
    /// </summary>
    public BranchBuilder<TBranchState, TResult> Then<TStep>()
        where TStep : IStep<TBranchState>
    {
        using var operation = Mutate();
        instructions.Add(new BranchStepAuthoringInstruction(
            null,
            () => Activator.CreateInstance<TStep>() ?? throw new InvalidOperationException(
                $"Step type '{typeof(TStep).FullName}' could not be created."),
            ConsumePendingPolicies()));
        return this;
    }

    /// <summary>
    /// Adds a business step factory to the branch-private state flow.
    /// </summary>
    public BranchBuilder<TBranchState, TResult> Then(Func<IStep<TBranchState>> stepFactory)
    {
        ArgumentNullException.ThrowIfNull(stepFactory);
        using var operation = Mutate();
        instructions.Add(new BranchStepAuthoringInstruction(null, stepFactory, ConsumePendingPolicies()));
        return this;
    }

    internal void AddNamedStep<TStep>() where TStep : IStep<TBranchState>
    {
        using var operation = Mutate();
        instructions.Add(new BranchStepAuthoringInstruction(
            typeof(TStep),
            null,
            ConsumePendingPolicies()));
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

        using var operation = Mutate();
        pendingPolicies = pendingPolicies.WithRetry(maxAttempts, backoff ?? TimeSpan.Zero);
        return this;
    }

    internal void DecoratePreviousWithRetry(int maxAttempts, TimeSpan? backoff)
    {
        if (maxAttempts <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(maxAttempts), maxAttempts, "Retry attempts must be positive.");
        }

        if (backoff < TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(backoff), backoff, "Retry backoff cannot be negative.");
        }

        using var operation = Mutate();
        var step = PreviousDecoratableStep("retry");
        if (step.Policies.Retry is not null)
        {
            throw AuthoringContractFactory.MisplacedDecorator("retry", instructions.Count - 1);
        }

        instructions[^1] = step with { Policies = step.Policies.WithRetry(maxAttempts, backoff ?? TimeSpan.Zero) };
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

        using var operation = Mutate();
        pendingPolicies = pendingPolicies.WithTimeout(duration);
        return this;
    }

    internal void DecoratePreviousWithTimeout(TimeSpan duration)
    {
        if (duration <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(duration), duration, "Timeout must be positive.");
        }

        using var operation = Mutate();
        var step = PreviousDecoratableStep("timeout");
        if (step.Policies.Timeout is not null)
        {
            throw AuthoringContractFactory.MisplacedDecorator("timeout", instructions.Count - 1);
        }

        instructions[^1] = step with { Policies = step.Policies.WithTimeout(duration) };
    }

    /// <summary>
    /// Allows operator cancellation to interrupt the next branch step.
    /// </summary>
    public BranchBuilder<TBranchState, TResult> WithCancellation()
    {
        using var operation = Mutate();
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
        using var operation = Mutate();
        pendingPolicies = pendingPolicies.WithPoolKey(poolKey);
        return this;
    }

    internal void DecoratePreviousWithPool(string poolKey)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(poolKey);
        using var operation = Mutate();
        var step = PreviousDecoratableStep("transient pool");
        if (step.Policies.PoolKey is not null)
        {
            throw AuthoringContractFactory.MisplacedDecorator("transient pool", instructions.Count - 1);
        }

        instructions[^1] = step with { Policies = step.Policies.WithPoolKey(poolKey) };
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
        using var operation = Mutate();
        instructions.Add(new BranchWaitAuthoringInstruction(
            eventName,
            correlationSelector,
            WaitMode.Resident,
            Timeout: null));
        return this;
    }

    internal void AddWait(
        string eventName,
        Func<TBranchState, CorrelationId> correlationSelector,
        WaitMode mode,
        TimeSpan? timeout = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(eventName);
        ArgumentNullException.ThrowIfNull(correlationSelector);
        using var operation = Mutate();
        instructions.Add(new BranchWaitAuthoringInstruction(
            eventName,
            correlationSelector,
            mode,
            timeout));
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

        using var operation = Mutate();
        instructions.Add(new BranchDelayAuthoringInstruction(duration));
        return this;
    }

    internal void AddIf(
        Func<TBranchState, bool> condition,
        Action<BranchBuilder<TBranchState, TResult>> then,
        Action<BranchBuilder<TBranchState, TResult>>? otherwise)
    {
        ArgumentNullException.ThrowIfNull(condition);
        ArgumentNullException.ThrowIfNull(then);

        using var operation = Mutate();
        var thenBuilder = InvokeNested(then, "if:true");
        var elseBuilder = otherwise is null
            ? EmptyNested("if:false")
            : InvokeNested(otherwise, "if:false");
        instructions.Add(new BranchIfAuthoringInstruction(
            condition,
            thenBuilder.Instructions,
            elseBuilder.Instructions));
    }

    internal void AddResourceLease(
        global::OrcaCore.ResourceLeaseRequest request,
        Action<BranchBuilder<TBranchState, TResult>> body)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(body);
        using var operation = Mutate();
        var nested = InvokeNested(body, "lease:body");
        instructions.Add(new BranchResourceLeaseAuthoringInstruction(
            request,
            null,
            nested.Instructions));
    }

    internal void AddResourceLease(
        Func<TBranchState, global::OrcaCore.ResourceLeaseRequest> request,
        Action<BranchBuilder<TBranchState, TResult>> body)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(body);
        using var operation = Mutate();
        var nested = InvokeNested(body, "lease:body");
        instructions.Add(new BranchResourceLeaseAuthoringInstruction(
            null,
            request,
            nested.Instructions));
    }

    /// <summary>
    /// Terminates the branch and projects its serializable result.
    /// </summary>
    public BranchBuilder<TBranchState, TResult> Return(
        Func<ReadOnlyBranchSnapshot<TBranchState>, TResult> resultProjector)
    {
        ArgumentNullException.ThrowIfNull(resultProjector);
        using var operation = Mutate();
        instructions.Add(new BranchReturnAuthoringInstruction(typeof(TResult), resultProjector));
        return this;
    }

    internal IReadOnlyList<BranchAuthoringInstruction> Instructions => instructions;

    private IDisposable Mutate() => lifecycle.BeginMutation(NextLocation);

    private string NextLocation => $"{lifecycle.Location}/n:{instructions.Count:D8}";

    private BranchBuilder<TBranchState, TResult> InvokeNested(
        Action<BranchBuilder<TBranchState, TResult>> callback,
        string segment)
    {
        var nestedHandle = lifecycle.CreateLexical($"{NextLocation}/{segment}");
        var nested = new BranchBuilder<TBranchState, TResult>(nestedHandle);
        try
        {
            callback(nested);
            return nested;
        }
        finally
        {
            nestedHandle.Expire();
        }
    }

    private BranchBuilder<TBranchState, TResult> EmptyNested(string segment)
    {
        var nestedHandle = lifecycle.CreateLexical($"{NextLocation}/{segment}");
        var nested = new BranchBuilder<TBranchState, TResult>(nestedHandle);
        nestedHandle.Expire();
        return nested;
    }

    private WorkflowPolicySet ConsumePendingPolicies()
    {
        var policies = pendingPolicies;
        pendingPolicies = WorkflowPolicySet.Empty;
        return policies;
    }

    private BranchStepAuthoringInstruction PreviousDecoratableStep(string decorator)
    {
        if (instructions.Count == 0 || instructions[^1] is not BranchStepAuthoringInstruction step)
        {
            throw AuthoringContractFactory.MisplacedDecorator(decorator, Math.Max(0, instructions.Count));
        }

        return step;
    }
}

internal sealed record StructuredBranchAuthoring(
    string BranchId,
    Type BranchStateType,
    Delegate InputProjector,
    IReadOnlyList<BranchAuthoringInstruction> Instructions);

internal abstract record BranchAuthoringInstruction;

internal sealed record BranchStepAuthoringInstruction(
    Type? StepType,
    Delegate? StepFactory,
    WorkflowPolicySet Policies) : BranchAuthoringInstruction;

internal sealed record BranchWaitAuthoringInstruction(
    string EventName,
    Delegate CorrelationSelector,
    WaitMode Mode,
    TimeSpan? Timeout) : BranchAuthoringInstruction;

internal sealed record BranchDelayAuthoringInstruction(TimeSpan Duration) : BranchAuthoringInstruction;

internal sealed record BranchIfAuthoringInstruction(
    Delegate Condition,
    IReadOnlyList<BranchAuthoringInstruction> Then,
    IReadOnlyList<BranchAuthoringInstruction> Else) : BranchAuthoringInstruction;

internal sealed record BranchResourceLeaseAuthoringInstruction(
    global::OrcaCore.ResourceLeaseRequest? StaticRequest,
    Delegate? RequestSelector,
    IReadOnlyList<BranchAuthoringInstruction> Body) : BranchAuthoringInstruction;

internal sealed record BranchStructuredScopeAuthoringInstruction(
    string ScopeKind,
    Type ParentStateType,
    Type ResultType,
    IReadOnlyList<StructuredBranchAuthoring> Branches,
    Delegate Merge) : BranchAuthoringInstruction;

internal sealed record BranchReturnAuthoringInstruction(
    Type ResultType,
    Delegate ResultProjector) : BranchAuthoringInstruction;
