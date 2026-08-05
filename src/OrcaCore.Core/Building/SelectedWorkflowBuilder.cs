using OrcaCore.Abstractions.Errors;
using OrcaCore.Abstractions.Ids;
using OrcaCore.Abstractions.Primitives;
using OrcaCore.Core.Authoring;
using OrcaCore.Core.Compilation;
using OrcaCore.Core.Definitions;

namespace OrcaCore.Core.Building;

/// <summary>
/// Internal authored-graph session shared by the mode-specific implementation adapters.
/// </summary>
internal abstract class WorkflowAuthoringSession<TState, TSelf>
    where TSelf : WorkflowAuthoringSession<TState, TSelf>
{
    private readonly SelectedWorkflowAuthoring<TState> authoring;
    private readonly List<SelectedAuthoringNode<TState>> nodes;
    private readonly bool isRoot;
    private readonly AuthoringLifecycleHandle lifecycle;
    private WorkflowPolicySet pendingPolicies = WorkflowPolicySet.Empty;

    internal WorkflowAuthoringSession(
        SelectedWorkflowAuthoring<TState> authoring,
        List<SelectedAuthoringNode<TState>> nodes,
        bool isRoot,
        AuthoringLifecycleHandle? lifecycle = null)
    {
        this.authoring = authoring;
        this.nodes = nodes;
        this.isRoot = isRoot;
        this.lifecycle = lifecycle ?? new AuthoringLifecycleSession().CreateRootHandle();
    }

    internal AuthoringJoinToken BeginRootJoin()
    {
        EnsureRoot();
        return lifecycle.Session.BeginJoin(lifecycle, NextLocation);
    }

    internal TSelf CompleteRootParallel<TResult>(
        AuthoringJoinToken join,
        Action<BranchScopeBuilder<TState, TResult>> branches,
        Func<ReadOnlyParentSnapshot<TState>, IReadOnlyList<BranchResult<TResult>>, TState> merge)
    {
        var successor = lifecycle.Session.CompleteJoin(
            join,
            () => AuthorParallel(branches, merge));
        return CreateSuccessorCore(authoring, nodes, successor);
    }

    internal TSelf CompleteRootParallelOutcomes<TResult>(
        AuthoringJoinToken join,
        Action<BranchScopeBuilder<TState, TResult>> branches,
        Func<
            ReadOnlyParentSnapshot<TState>,
            IReadOnlyList<global::OrcaCore.BranchOutcome<TResult>>,
            TState> merge)
    {
        var successor = lifecycle.Session.CompleteJoin(
            join,
            () => AuthorParallel(branches, merge));
        return CreateSuccessorCore(authoring, nodes, successor);
    }

    internal TSelf CompleteRootForEach<TItem, TItemState, TResult>(
        AuthoringJoinToken join,
        Func<ReadOnlyParentSnapshot<TState>, IReadOnlyList<TItem>> itemSelector,
        WorkflowPartitioner<TItem> partitioner,
        Func<ForEachItemInput<TItem>, TItemState> itemStateProjector,
        Action<BranchBuilder<TItemState, TResult>> body,
        ForEachJoinPolicy joinPolicy,
        ForEachFailurePolicy failurePolicy,
        int? maxItems,
        int? maxConcurrency,
        Func<ReadOnlyParentSnapshot<TState>, IReadOnlyList<ForEachItemOutcome<TResult>>, TState>? merge)
    {
        var successor = lifecycle.Session.CompleteJoin(
            join,
            () => AuthorForEach(
                itemSelector,
                partitioner,
                itemStateProjector,
                body,
                joinPolicy,
                failurePolicy,
                maxItems,
                maxConcurrency,
                merge));
        return CreateSuccessorCore(authoring, nodes, successor);
    }

    internal void SelectDeadline(TimeSpan timeout)
    {
        lifecycle.Session.SelectDeadline(lifecycle, NextLocation, timeout);
        authoring.WorkflowTimeout = timeout;
    }

    /// <summary>
    /// Adds the sole root input-to-state initializer.
    /// </summary>
    internal TSelf Init<TInput>(Func<TInput, TState> createState)
    {
        ArgumentNullException.ThrowIfNull(createState);
        using var operation = Mutate();
        nodes.Add(new SelectedInitAuthoringNode<TState>(
            typeof(TInput),
            input => createState((TInput)input!)));
        return Self;
    }

    /// <summary>
    /// Adds a parameterless business step.
    /// </summary>
    internal TSelf Then<TStep>()
        where TStep : IStep<TState>
    {
        using var operation = Mutate();
        nodes.Add(new SelectedStepAuthoringNode<TState>(
            null,
            () => Activator.CreateInstance<TStep>() ?? throw new InvalidOperationException(
                $"Step type '{typeof(TStep).FullName}' could not be created."),
            ConsumePendingPolicies()));
        return Self;
    }

    /// <summary>
    /// Adds a configured business step factory.
    /// </summary>
    internal TSelf Then(Func<IStep<TState>> stepFactory)
    {
        ArgumentNullException.ThrowIfNull(stepFactory);
        using var operation = Mutate();
        nodes.Add(new SelectedStepAuthoringNode<TState>(null, stepFactory, ConsumePendingPolicies()));
        return Self;
    }

    internal void AddNamedStep<TStep>() where TStep : IStep<TState>
    {
        using var operation = Mutate();
        nodes.Add(new SelectedStepAuthoringNode<TState>(
            typeof(TStep),
            null,
            ConsumePendingPolicies()));
    }

    /// <summary>
    /// Applies a retry policy to the next authored step.
    /// </summary>
    internal TSelf WithRetry(int maxAttempts, TimeSpan? backoff = null)
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
        return Self;
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
            throw AuthoringContractFactory.MisplacedDecorator("retry", nodes.Count - 1);
        }

        nodes[^1] = step with { Policies = step.Policies.WithRetry(maxAttempts, backoff ?? TimeSpan.Zero) };
    }

    /// <summary>
    /// Applies an execution timeout to the next authored step.
    /// </summary>
    internal TSelf WithTimeout(TimeSpan duration)
    {
        if (duration <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(duration), duration, "Timeout must be positive.");
        }

        using var operation = Mutate();
        pendingPolicies = pendingPolicies.WithTimeout(duration);
        return Self;
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
            throw AuthoringContractFactory.MisplacedDecorator("timeout", nodes.Count - 1);
        }

        nodes[^1] = step with { Policies = step.Policies.WithTimeout(duration) };
    }

    /// <summary>
    /// Allows operator cancellation to interrupt the next authored step.
    /// </summary>
    internal TSelf WithCancellation()
    {
        using var operation = Mutate();
        pendingPolicies = pendingPolicies.WithCancellation();
        return Self;
    }

    protected TSelf ApplyPoolKey(string poolKey)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(poolKey);
        using var operation = Mutate();
        pendingPolicies = pendingPolicies.WithPoolKey(poolKey);
        return Self;
    }

    internal void DecoratePreviousWithPool(string poolKey)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(poolKey);
        using var operation = Mutate();
        var step = PreviousDecoratableStep("transient pool");
        if (step.Policies.PoolKey is not null)
        {
            throw AuthoringContractFactory.MisplacedDecorator("transient pool", nodes.Count - 1);
        }

        nodes[^1] = step with { Policies = step.Policies.WithPoolKey(poolKey) };
    }

    /// <summary>
    /// Adds a structural resident wait for a statically known event name.
    /// </summary>
    internal TSelf Wait(string eventName, Func<TState, CorrelationId> correlationSelector)
    {
        AddWait(eventName, correlationSelector, WaitMode.Resident, timeout: null);
        return Self;
    }

    /// <summary>
    /// Adds a structural delay.
    /// </summary>
    internal TSelf Delay(TimeSpan duration)
    {
        if (duration <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(duration), duration, "Delay must be positive.");
        }

        using var operation = Mutate();
        nodes.Add(new SelectedDelayAuthoringNode<TState>(duration));
        return Self;
    }

    /// <summary>
    /// Adds a structured conditional without an authored closing node.
    /// </summary>
    internal TSelf If(
        Func<TState, bool> condition,
        Action<TSelf> then,
        Action<TSelf>? otherwise = null)
    {
        ArgumentNullException.ThrowIfNull(condition);
        ArgumentNullException.ThrowIfNull(then);

        using var operation = Mutate();
        var thenNodes = new List<SelectedAuthoringNode<TState>>();
        InvokeNested(thenNodes, then, "if:true");
        var elseNodes = new List<SelectedAuthoringNode<TState>>();
        if (otherwise is not null)
        {
            InvokeNested(elseNodes, otherwise, "if:false");
        }

        nodes.Add(new SelectedIfAuthoringNode<TState>(condition, thenNodes, elseNodes));
        return Self;
    }

    /// <summary>
    /// Adds a structured loop without an authored closing node.
    /// </summary>
    internal TSelf While(Func<TState, bool> condition, Action<TSelf> body)
    {
        ArgumentNullException.ThrowIfNull(condition);
        ArgumentNullException.ThrowIfNull(body);

        using var operation = Mutate();
        var bodyNodes = new List<SelectedAuthoringNode<TState>>();
        InvokeNested(bodyNodes, body, "while:body");
        nodes.Add(new SelectedWhileAuthoringNode<TState>(condition, bodyNodes));
        return Self;
    }

    /// <summary>
    /// Adds cooperative branches and a deterministic authored-order merge.
    /// </summary>
    internal TSelf Parallel<TResult>(
        Action<BranchScopeBuilder<TState, TResult>> branches,
        Func<ReadOnlyParentSnapshot<TState>, IReadOnlyList<BranchResult<TResult>>, TState> merge)
    {
        ArgumentNullException.ThrowIfNull(branches);
        ArgumentNullException.ThrowIfNull(merge);
        using var operation = Mutate();
        AuthorParallel(branches, merge);
        return Self;
    }

    internal TSelf ParallelOutcomes<TResult>(
        Action<BranchScopeBuilder<TState, TResult>> branches,
        Func<
            ReadOnlyParentSnapshot<TState>,
            IReadOnlyList<global::OrcaCore.BranchOutcome<TResult>>,
            TState> merge)
    {
        ArgumentNullException.ThrowIfNull(branches);
        ArgumentNullException.ThrowIfNull(merge);
        using var operation = Mutate();
        AuthorParallel(branches, merge);
        return Self;
    }

    /// <summary>
    /// Adds the sole root completion node.
    /// </summary>
    internal TSelf End(string? outcomeName = null)
    {
        lifecycle.Session.Freeze(
            lifecycle,
            NextLocation,
            () => nodes.Add(new SelectedEndAuthoringNode<TState>(outcomeName, null)));
        return Self;
    }

    /// <summary>
    /// Adds the sole root completion node with a deterministic outcome selector.
    /// </summary>
    internal TSelf End(Func<TState, string?> outcomeSelector)
    {
        ArgumentNullException.ThrowIfNull(outcomeSelector);
        lifecycle.Session.Freeze(
            lifecycle,
            NextLocation,
            () => nodes.Add(new SelectedEndAuthoringNode<TState>(null, outcomeSelector)));
        return Self;
    }

    internal TSelf AddTypedEnd<TOutput>(Func<TState, TOutput> outputSelector, string? outcomeName = null)
    {
        ArgumentNullException.ThrowIfNull(outputSelector);
        lifecycle.Session.Freeze(
            lifecycle,
            NextLocation,
            () => nodes.Add(new SelectedEndAuthoringNode<TState>(
                outcomeName,
                OutcomeSelector: null,
                typeof(TOutput),
                outputSelector)));
        return Self;
    }

    /// <summary>
    /// Builds a compiled-plan-backed definition or throws all compiler diagnostics.
    /// </summary>
    internal WorkflowDefinition<TState> Build()
    {
        var validation = TryBuild();
        if (validation.IsValid)
        {
            return validation.Value;
        }

        var message = string.Join(
            Environment.NewLine,
            validation.Errors.Select(error => $"{error.Code}: {error.Message} ({error.Path})"));
        throw global::OrcaCore.Core.Authoring.AuthoringContractFactory.DefinitionException(message);
    }

    /// <summary>
    /// Compiles the graph and returns every discoverable graph-wide diagnostic.
    /// </summary>
    internal OrcaCore.Abstractions.Primitives.Validation<WorkflowDefinition<TState>> TryBuild()
    {
        if (!isRoot)
        {
            throw new InvalidOperationException("A nested workflow builder cannot build a definition.");
        }

        return DefinitionCompiler.Compile(authoring);
    }

    internal void UseDetachedAttemptState()
    {
        using var operation = Mutate();
        authoring.DetachedAttemptState = true;
    }

    /// <summary>
    /// Replaces compiler limits for this definition.
    /// </summary>
    internal TSelf WithCompilerOptions(DefinitionCompilerOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        using var operation = Mutate();
        authoring.CompilerOptions = options;
        return Self;
    }

    /// <summary>
    /// Replaces type-to-schema resolution for persisted and copied workflow data.
    /// </summary>
    internal TSelf WithTypeSerializerRegistry(IWorkflowTypeSerializerRegistry registry)
    {
        ArgumentNullException.ThrowIfNull(registry);
        using var operation = Mutate();
        authoring.TypeSerializerRegistry = registry;
        return Self;
    }

    internal void AddContinueAsNew(Func<TState, TState> stateSelector)
    {
        ArgumentNullException.ThrowIfNull(stateSelector);
        lifecycle.Session.Freeze(
            lifecycle,
            NextLocation,
            () => nodes.Add(new SelectedContinueAsNewAuthoringNode<TState>(stateSelector)));
    }

    internal void AddResourceLease(
        global::OrcaCore.ResourceLeaseRequest request,
        Action<TSelf> body)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(body);
        using var operation = Mutate();
        var bodyNodes = new List<SelectedAuthoringNode<TState>>();
        InvokeNested(bodyNodes, body, "lease:body");
        nodes.Add(new SelectedResourceLeaseAuthoringNode<TState>(request, null, bodyNodes));
    }

    internal void AddResourceLease(
        Func<TState, global::OrcaCore.ResourceLeaseRequest> request,
        Action<TSelf> body)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(body);
        using var operation = Mutate();
        var bodyNodes = new List<SelectedAuthoringNode<TState>>();
        InvokeNested(bodyNodes, body, "lease:body");
        nodes.Add(new SelectedResourceLeaseAuthoringNode<TState>(null, request, bodyNodes));
    }

    internal void AddWait(
        string eventName,
        Func<TState, CorrelationId> correlationSelector,
        WaitMode mode,
        TimeSpan? timeout = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(eventName);
        ArgumentNullException.ThrowIfNull(correlationSelector);
        using var operation = Mutate();
        nodes.Add(new SelectedWaitAuthoringNode<TState>(eventName, correlationSelector, mode, timeout));
    }

    internal void AddDurableNode(SelectedAuthoringNode<TState> node)
    {
        ArgumentNullException.ThrowIfNull(node);
        using var operation = Mutate();
        nodes.Add(node);
    }

    internal void AddForEach<TItem, TItemState, TResult>(
        Func<ReadOnlyParentSnapshot<TState>, IReadOnlyList<TItem>> itemSelector,
        WorkflowPartitioner<TItem> partitioner,
        Func<ForEachItemInput<TItem>, TItemState> itemStateProjector,
        Action<BranchBuilder<TItemState, TResult>> body,
        ForEachJoinPolicy joinPolicy,
        ForEachFailurePolicy failurePolicy,
        int? maxConcurrency,
        Func<ReadOnlyParentSnapshot<TState>, IReadOnlyList<ForEachItemOutcome<TResult>>, TState>? merge)
    {
        ArgumentNullException.ThrowIfNull(itemSelector);
        ArgumentNullException.ThrowIfNull(partitioner);
        ArgumentNullException.ThrowIfNull(itemStateProjector);
        ArgumentNullException.ThrowIfNull(body);
        using var operation = Mutate();
        AuthorForEach(
            itemSelector,
            partitioner,
            itemStateProjector,
            body,
            joinPolicy,
            failurePolicy,
            maxItems: null,
            maxConcurrency,
            merge);
    }

    private TSelf Self => (TSelf)this;

    private void InvokeNested(
        List<SelectedAuthoringNode<TState>> nestedNodes,
        Action<TSelf> callback,
        string segment)
    {
        var nestedHandle = lifecycle.CreateLexical($"{NextLocation}/{segment}");
        try
        {
            callback(CreateNestedCore(authoring, nestedNodes, nestedHandle));
        }
        finally
        {
            nestedHandle.Expire();
        }
    }

    internal abstract TSelf CreateNestedCore(
        SelectedWorkflowAuthoring<TState> sharedAuthoring,
        List<SelectedAuthoringNode<TState>> nestedNodes,
        AuthoringLifecycleHandle lifecycle);

    internal abstract TSelf CreateSuccessorCore(
        SelectedWorkflowAuthoring<TState> sharedAuthoring,
        List<SelectedAuthoringNode<TState>> rootNodes,
        AuthoringLifecycleHandle lifecycle);

    private IDisposable Mutate() => lifecycle.BeginMutation(NextLocation);

    private string NextLocation => $"{lifecycle.Location}/n:{nodes.Count:D8}";

    private void EnsureRoot()
    {
        if (!isRoot)
        {
            throw new InvalidOperationException("A nested workflow builder cannot start a root join.");
        }
    }

    private void AuthorParallel<TResult>(
        Action<BranchScopeBuilder<TState, TResult>> branches,
        Func<ReadOnlyParentSnapshot<TState>, IReadOnlyList<BranchResult<TResult>>, TState> merge)
    {
        var scopeHandle = lifecycle.CreateLexical(NextLocation);
        var scope = new BranchScopeBuilder<TState, TResult>(scopeHandle);
        try
        {
            branches(scope);
        }
        finally
        {
            scopeHandle.Expire();
        }

        nodes.Add(new SelectedStructuredScopeAuthoringNode<TState>(
            "Parallel",
            typeof(TResult),
            scope.Branches,
            merge));
    }

    private void AuthorParallel<TResult>(
        Action<BranchScopeBuilder<TState, TResult>> branches,
        Func<
            ReadOnlyParentSnapshot<TState>,
            IReadOnlyList<global::OrcaCore.BranchOutcome<TResult>>,
            TState> merge)
    {
        var scopeHandle = lifecycle.CreateLexical(NextLocation);
        var scope = new BranchScopeBuilder<TState, TResult>(scopeHandle);
        try
        {
            branches(scope);
        }
        finally
        {
            scopeHandle.Expire();
        }

        nodes.Add(new SelectedStructuredScopeAuthoringNode<TState>(
            "ParallelOutcomes",
            typeof(TResult),
            scope.Branches,
            merge));
    }

    private void AuthorForEach<TItem, TItemState, TResult>(
        Func<ReadOnlyParentSnapshot<TState>, IReadOnlyList<TItem>> itemSelector,
        WorkflowPartitioner<TItem> partitioner,
        Func<ForEachItemInput<TItem>, TItemState> itemStateProjector,
        Action<BranchBuilder<TItemState, TResult>> body,
        ForEachJoinPolicy joinPolicy,
        ForEachFailurePolicy failurePolicy,
        int? maxItems,
        int? maxConcurrency,
        Func<ReadOnlyParentSnapshot<TState>, IReadOnlyList<ForEachItemOutcome<TResult>>, TState>? merge)
    {
        var bodyHandle = lifecycle.CreateLexical($"{NextLocation}/foreach:body");
        var itemBody = new BranchBuilder<TItemState, TResult>(bodyHandle);
        try
        {
            body(itemBody);
        }
        finally
        {
            bodyHandle.Expire();
        }

        nodes.Add(new SelectedForEachAuthoringNode<TState>(
            typeof(TItem),
            typeof(TItemState),
            typeof(TResult),
            itemSelector,
            partitioner,
            itemStateProjector,
            itemBody.Instructions,
            joinPolicy,
            failurePolicy,
            maxItems,
            maxConcurrency,
            merge));
    }

    private WorkflowPolicySet ConsumePendingPolicies()
    {
        var policies = pendingPolicies;
        pendingPolicies = WorkflowPolicySet.Empty;
        return policies;
    }

    private SelectedStepAuthoringNode<TState> PreviousDecoratableStep(string decorator)
    {
        if (nodes.Count == 0 || nodes[^1] is not SelectedStepAuthoringNode<TState> step)
        {
            throw AuthoringContractFactory.MisplacedDecorator(decorator, Math.Max(0, nodes.Count));
        }

        return step;
    }
}

/// <summary>
/// Authors capabilities implemented by the ephemeral engine.
/// </summary>
internal sealed class EphemeralWorkflowBuilder<TState>
    : WorkflowAuthoringSession<TState, EphemeralWorkflowBuilder<TState>>
{
    internal EphemeralWorkflowBuilder(DefinitionId definitionId, DefinitionVersion definitionVersion)
        : this(
            new SelectedWorkflowAuthoring<TState>(
                definitionId,
                definitionVersion,
                WorkflowExecutionMode.Ephemeral),
            null,
            isRoot: true)
    {
    }

    /// <summary>
    /// Applies an in-process governance pool key to the next authored step.
    /// </summary>
    public EphemeralWorkflowBuilder<TState> WithPoolKey(string poolKey)
    {
        return ApplyPoolKey(poolKey);
    }

    /// <summary>
    /// Adds dynamic in-instance fanout as isolated item fibers.
    /// </summary>
    public EphemeralWorkflowBuilder<TState> ForEach<TItem, TItemState, TResult>(
        Func<ReadOnlyParentSnapshot<TState>, IReadOnlyList<TItem>> itemSelector,
        WorkflowPartitioner<TItem> partitioner,
        Func<ForEachItemInput<TItem>, TItemState> itemStateProjector,
        Action<BranchBuilder<TItemState, TResult>> body,
        ForEachJoinPolicy joinPolicy,
        ForEachFailurePolicy failurePolicy,
        int? maxConcurrency = null,
        Func<ReadOnlyParentSnapshot<TState>, IReadOnlyList<ForEachItemOutcome<TResult>>, TState>? merge = null)
    {
        AddForEach(
            itemSelector,
            partitioner,
            itemStateProjector,
            body,
            joinPolicy,
            failurePolicy,
            maxConcurrency,
            merge);
        return this;
    }

    private EphemeralWorkflowBuilder(
        SelectedWorkflowAuthoring<TState> authoring,
        List<SelectedAuthoringNode<TState>>? nodes,
        bool isRoot,
        AuthoringLifecycleHandle? lifecycle = null)
        : base(authoring, nodes ?? authoring.RootNodes, isRoot, lifecycle)
    {
    }

    internal override EphemeralWorkflowBuilder<TState> CreateNestedCore(
        SelectedWorkflowAuthoring<TState> sharedAuthoring,
        List<SelectedAuthoringNode<TState>> nestedNodes,
        AuthoringLifecycleHandle lifecycle)
    {
        return new EphemeralWorkflowBuilder<TState>(
            sharedAuthoring,
            nestedNodes,
            isRoot: false,
            lifecycle);
    }

    internal override EphemeralWorkflowBuilder<TState> CreateSuccessorCore(
        SelectedWorkflowAuthoring<TState> sharedAuthoring,
        List<SelectedAuthoringNode<TState>> rootNodes,
        AuthoringLifecycleHandle lifecycle)
    {
        return new EphemeralWorkflowBuilder<TState>(
            sharedAuthoring,
            rootNodes,
            isRoot: true,
            lifecycle);
    }
}

/// <summary>
/// Authors capabilities implemented by the durable engine.
/// </summary>
internal sealed class DurableWorkflowBuilder<TState>
    : WorkflowAuthoringSession<TState, DurableWorkflowBuilder<TState>>
{
    internal DurableWorkflowBuilder(DefinitionId definitionId, DefinitionVersion definitionVersion)
        : this(
            new SelectedWorkflowAuthoring<TState>(
                definitionId,
                definitionVersion,
                WorkflowExecutionMode.Durable),
            null,
            isRoot: true)
    {
    }

    private DurableWorkflowBuilder(
        SelectedWorkflowAuthoring<TState> authoring,
        List<SelectedAuthoringNode<TState>>? nodes,
        bool isRoot,
        AuthoringLifecycleHandle? lifecycle = null)
        : base(authoring, nodes ?? authoring.RootNodes, isRoot, lifecycle)
    {
    }

    /// <summary>
    /// Adds a structural history rollover on the root fiber.
    /// </summary>
    public DurableWorkflowBuilder<TState> ContinueAsNew(Func<TState, TState> stateSelector)
    {
        AddContinueAsNew(stateSelector);
        return this;
    }

    /// <summary>
    /// Adds a cold durable wait for legacy internal execution fixtures.
    /// </summary>
    internal DurableWorkflowBuilder<TState> AddColdWait(
        string eventName,
        Func<TState, CorrelationId> correlationSelector)
    {
        AddWait(eventName, correlationSelector, WaitMode.Cold);
        return this;
    }

    internal override DurableWorkflowBuilder<TState> CreateNestedCore(
        SelectedWorkflowAuthoring<TState> sharedAuthoring,
        List<SelectedAuthoringNode<TState>> nestedNodes,
        AuthoringLifecycleHandle lifecycle)
    {
        return new DurableWorkflowBuilder<TState>(
            sharedAuthoring,
            nestedNodes,
            isRoot: false,
            lifecycle);
    }

    internal override DurableWorkflowBuilder<TState> CreateSuccessorCore(
        SelectedWorkflowAuthoring<TState> sharedAuthoring,
        List<SelectedAuthoringNode<TState>> rootNodes,
        AuthoringLifecycleHandle lifecycle)
    {
        return new DurableWorkflowBuilder<TState>(
            sharedAuthoring,
            rootNodes,
            isRoot: true,
            lifecycle);
    }

    private void AddNode(SelectedAuthoringNode<TState> node)
    {
        AddDurableNode(node);
    }
}
