using OrcaCore.Abstractions.Errors;
using OrcaCore.Abstractions.Durable;
using OrcaCore.Abstractions.Ids;
using OrcaCore.Abstractions.Instances;
using OrcaCore.Abstractions.Primitives;
using OrcaCore.Abstractions.Steps;
using OrcaCore.Core.Compilation;
using OrcaCore.Core.Definitions;

namespace OrcaCore.Core.Building;

/// <summary>
/// Shared portable authoring operations for a builder whose execution mode is already fixed.
/// </summary>
public abstract class SelectedWorkflowBuilder<TState, TSelf>
    where TSelf : SelectedWorkflowBuilder<TState, TSelf>
{
    private readonly SelectedWorkflowAuthoring<TState> authoring;
    private readonly List<SelectedAuthoringNode<TState>> nodes;
    private readonly bool isRoot;
    private WorkflowPolicySet pendingPolicies = WorkflowPolicySet.Empty;

    internal SelectedWorkflowBuilder(
        SelectedWorkflowAuthoring<TState> authoring,
        List<SelectedAuthoringNode<TState>> nodes,
        bool isRoot)
    {
        this.authoring = authoring;
        this.nodes = nodes;
        this.isRoot = isRoot;
    }

    /// <summary>
    /// Adds the sole root input-to-state initializer.
    /// </summary>
    public TSelf Init<TInput>(Func<TInput, TState> createState)
    {
        ArgumentNullException.ThrowIfNull(createState);
        nodes.Add(new SelectedInitAuthoringNode<TState>(
            input => createState((TInput)input!),
            (payload, serializer) => serializer.Deserialize<TInput>(payload)));
        return Self;
    }

    /// <summary>
    /// Adds a parameterless business step.
    /// </summary>
    public TSelf Then<TStep>()
        where TStep : IStep<TState>, new()
    {
        nodes.Add(new SelectedStepAuthoringNode<TState>(() => new TStep(), ConsumePendingPolicies()));
        return Self;
    }

    /// <summary>
    /// Adds a configured business step factory.
    /// </summary>
    public TSelf Then(Func<IStep<TState>> stepFactory)
    {
        ArgumentNullException.ThrowIfNull(stepFactory);
        nodes.Add(new SelectedStepAuthoringNode<TState>(stepFactory, ConsumePendingPolicies()));
        return Self;
    }

    /// <summary>
    /// Applies a retry policy to the next authored step.
    /// </summary>
    public TSelf WithRetry(int maxAttempts, TimeSpan? backoff = null)
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
        return Self;
    }

    /// <summary>
    /// Applies an execution timeout to the next authored step.
    /// </summary>
    public TSelf WithTimeout(TimeSpan duration)
    {
        if (duration <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(duration), duration, "Timeout must be positive.");
        }

        pendingPolicies = pendingPolicies.WithTimeout(duration);
        return Self;
    }

    /// <summary>
    /// Allows operator cancellation to interrupt the next authored step.
    /// </summary>
    public TSelf WithCancellation()
    {
        pendingPolicies = pendingPolicies.WithCancellation();
        return Self;
    }

    protected TSelf ApplyPoolKey(string poolKey)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(poolKey);
        pendingPolicies = pendingPolicies.WithPoolKey(poolKey);
        return Self;
    }

    /// <summary>
    /// Adds a structural resident wait for a statically known event name.
    /// </summary>
    public TSelf Wait(string eventName, Func<TState, CorrelationId> correlationSelector)
    {
        AddWait(eventName, correlationSelector, WaitMode.Resident);
        return Self;
    }

    /// <summary>
    /// Adds a structural delay.
    /// </summary>
    public TSelf Delay(TimeSpan duration)
    {
        if (duration <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(duration), duration, "Delay must be positive.");
        }

        nodes.Add(new SelectedDelayAuthoringNode<TState>(duration));
        return Self;
    }

    /// <summary>
    /// Adds a structured conditional without an authored closing node.
    /// </summary>
    public TSelf If(
        Func<TState, bool> condition,
        Action<TSelf> then,
        Action<TSelf>? otherwise = null)
    {
        ArgumentNullException.ThrowIfNull(condition);
        ArgumentNullException.ThrowIfNull(then);

        var thenNodes = new List<SelectedAuthoringNode<TState>>();
        then(CreateNested(thenNodes));
        var elseNodes = new List<SelectedAuthoringNode<TState>>();
        otherwise?.Invoke(CreateNested(elseNodes));
        nodes.Add(new SelectedIfAuthoringNode<TState>(condition, thenNodes, elseNodes));
        return Self;
    }

    /// <summary>
    /// Adds a structured loop without an authored closing node.
    /// </summary>
    public TSelf While(Func<TState, bool> condition, Action<TSelf> body)
    {
        ArgumentNullException.ThrowIfNull(condition);
        ArgumentNullException.ThrowIfNull(body);

        var bodyNodes = new List<SelectedAuthoringNode<TState>>();
        body(CreateNested(bodyNodes));
        nodes.Add(new SelectedWhileAuthoringNode<TState>(condition, bodyNodes));
        return Self;
    }

    /// <summary>
    /// Adds cooperative branches and a deterministic authored-order merge.
    /// </summary>
    public TSelf Parallel<TResult>(
        Action<BranchScopeBuilder<TState, TResult>> branches,
        Func<ReadOnlyParentSnapshot<TState>, IReadOnlyList<BranchResult<TResult>>, TState> merge)
    {
        ArgumentNullException.ThrowIfNull(branches);
        ArgumentNullException.ThrowIfNull(merge);

        var scope = new BranchScopeBuilder<TState, TResult>();
        branches(scope);
        nodes.Add(new SelectedStructuredScopeAuthoringNode<TState>(
            "Parallel",
            typeof(TResult),
            scope.Branches,
            merge));
        return Self;
    }

    /// <summary>
    /// Adds a first-terminal branch race and a deterministic winner merge.
    /// </summary>
    public TSelf WhenFirst<TResult>(
        Action<BranchScopeBuilder<TState, TResult>> branches,
        Func<ReadOnlyParentSnapshot<TState>, BranchResult<TResult>, TState> merge)
    {
        ArgumentNullException.ThrowIfNull(branches);
        ArgumentNullException.ThrowIfNull(merge);

        var scope = new BranchScopeBuilder<TState, TResult>();
        branches(scope);
        nodes.Add(new SelectedStructuredScopeAuthoringNode<TState>(
            "WhenFirst",
            typeof(TResult),
            scope.Branches,
            merge));
        return Self;
    }

    /// <summary>
    /// Adds the sole root completion node.
    /// </summary>
    public TSelf End(string? outcomeName = null)
    {
        nodes.Add(new SelectedEndAuthoringNode<TState>(outcomeName, null));
        return Self;
    }

    /// <summary>
    /// Adds the sole root completion node with a deterministic outcome selector.
    /// </summary>
    public TSelf End(Func<TState, string?> outcomeSelector)
    {
        ArgumentNullException.ThrowIfNull(outcomeSelector);
        nodes.Add(new SelectedEndAuthoringNode<TState>(null, outcomeSelector));
        return Self;
    }

    /// <summary>
    /// Builds a compiled-plan-backed definition or throws all compiler diagnostics.
    /// </summary>
    public WorkflowDefinition<TState> Build()
    {
        var validation = TryBuild();
        if (validation.IsValid)
        {
            return validation.Value;
        }

        var message = string.Join(
            Environment.NewLine,
            validation.Errors.Select(error => $"{error.Code}: {error.Message} ({error.Path})"));
        throw new WorkflowDefinitionException(message);
    }

    /// <summary>
    /// Compiles the graph and returns every discoverable graph-wide diagnostic.
    /// </summary>
    public Validation<WorkflowDefinition<TState>> TryBuild()
    {
        if (!isRoot)
        {
            throw new InvalidOperationException("A nested workflow builder cannot build a definition.");
        }

        return DefinitionCompiler.Compile(authoring);
    }

    /// <summary>
    /// Replaces compiler limits for this definition.
    /// </summary>
    public TSelf WithCompilerOptions(DefinitionCompilerOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        authoring.CompilerOptions = options;
        return Self;
    }

    /// <summary>
    /// Replaces type-to-schema resolution for persisted and copied workflow data.
    /// </summary>
    public TSelf WithTypeSerializerRegistry(IWorkflowTypeSerializerRegistry registry)
    {
        ArgumentNullException.ThrowIfNull(registry);
        authoring.TypeSerializerRegistry = registry;
        return Self;
    }

    internal void AddContinueAsNew(Func<TState, TState> stateSelector)
    {
        ArgumentNullException.ThrowIfNull(stateSelector);
        nodes.Add(new SelectedContinueAsNewAuthoringNode<TState>(stateSelector));
    }

    internal void AddWait(
        string eventName,
        Func<TState, CorrelationId> correlationSelector,
        WaitMode mode)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(eventName);
        ArgumentNullException.ThrowIfNull(correlationSelector);
        nodes.Add(new SelectedWaitAuthoringNode<TState>(eventName, correlationSelector, mode));
    }

    internal void AddDurableNode(SelectedAuthoringNode<TState> node)
    {
        ArgumentNullException.ThrowIfNull(node);
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

        var itemBody = new BranchBuilder<TItemState, TResult>();
        body(itemBody);
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
            maxConcurrency,
            merge));
    }

    private TSelf Self => (TSelf)this;

    private TSelf CreateNested(List<SelectedAuthoringNode<TState>> nestedNodes)
    {
        return CreateNestedCore(authoring, nestedNodes);
    }

    internal abstract TSelf CreateNestedCore(
        SelectedWorkflowAuthoring<TState> sharedAuthoring,
        List<SelectedAuthoringNode<TState>> nestedNodes);

    private WorkflowPolicySet ConsumePendingPolicies()
    {
        var policies = pendingPolicies;
        pendingPolicies = WorkflowPolicySet.Empty;
        return policies;
    }
}

/// <summary>
/// Authors capabilities implemented by the ephemeral engine.
/// </summary>
public sealed class EphemeralWorkflowBuilder<TState>
    : SelectedWorkflowBuilder<TState, EphemeralWorkflowBuilder<TState>>
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
        bool isRoot)
        : base(authoring, nodes ?? authoring.RootNodes, isRoot)
    {
    }

    internal override EphemeralWorkflowBuilder<TState> CreateNestedCore(
        SelectedWorkflowAuthoring<TState> sharedAuthoring,
        List<SelectedAuthoringNode<TState>> nestedNodes)
    {
        return new EphemeralWorkflowBuilder<TState>(sharedAuthoring, nestedNodes, isRoot: false);
    }
}

/// <summary>
/// Authors capabilities implemented by the durable engine.
/// </summary>
public sealed class DurableWorkflowBuilder<TState>
    : SelectedWorkflowBuilder<TState, DurableWorkflowBuilder<TState>>
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
        bool isRoot)
        : base(authoring, nodes ?? authoring.RootNodes, isRoot)
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
    /// Adds a cold durable wait for a statically known event name.
    /// </summary>
    public DurableWorkflowBuilder<TState> WaitLong(
        string eventName,
        Func<TState, CorrelationId> correlationSelector)
    {
        AddWait(eventName, correlationSelector, WaitMode.Cold);
        return this;
    }

    /// <summary>
    /// Dispatches one durable child instance and suspends the owning fiber until it completes.
    /// </summary>
    public DurableWorkflowBuilder<TState> RunChild(
        DefinitionId childDefinitionId,
        DefinitionVersion childDefinitionVersion,
        RunChildFailurePolicy failurePolicy = RunChildFailurePolicy.PropagateFailure)
    {
        AddNode(new SelectedRunChildAuthoringNode<TState>(
            childDefinitionId,
            childDefinitionVersion,
            failurePolicy));
        return this;
    }

    /// <summary>
    /// Dispatches a durable child-instance group as one owned suspension.
    /// </summary>
    public DurableWorkflowBuilder<TState> RunChildren(
        DefinitionId childDefinitionId,
        DefinitionVersion childDefinitionVersion,
        Func<TState, IReadOnlyList<string>> itemSnapshotSelector,
        int? maxConcurrency = null,
        RunChildFailurePolicy failurePolicy = RunChildFailurePolicy.PropagateFailure,
        RunChildrenJoinPolicy joinPolicy = RunChildrenJoinPolicy.WhenAll,
        RunChildrenResidualPolicy residualPolicy = RunChildrenResidualPolicy.CancelRemaining)
    {
        ArgumentNullException.ThrowIfNull(itemSnapshotSelector);
        if (maxConcurrency <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(maxConcurrency),
                maxConcurrency,
                "Child-group max concurrency must be positive.");
        }

        AddNode(new SelectedRunChildrenAuthoringNode<TState>(
            childDefinitionId,
            childDefinitionVersion,
            itemSnapshotSelector,
            maxConcurrency,
            failurePolicy,
            joinPolicy,
            residualPolicy));
        return this;
    }

    internal override DurableWorkflowBuilder<TState> CreateNestedCore(
        SelectedWorkflowAuthoring<TState> sharedAuthoring,
        List<SelectedAuthoringNode<TState>> nestedNodes)
    {
        return new DurableWorkflowBuilder<TState>(sharedAuthoring, nestedNodes, isRoot: false);
    }

    private void AddNode(SelectedAuthoringNode<TState> node)
    {
        AddDurableNode(node);
    }
}
