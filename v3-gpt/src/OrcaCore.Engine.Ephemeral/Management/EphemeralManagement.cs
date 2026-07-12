using System.Linq.Expressions;
using OrcaCore.Abstractions.Events;
using OrcaCore.Abstractions.Errors;
using OrcaCore.Abstractions.Ids;
using OrcaCore.Abstractions.Instances;
using OrcaCore.Engine.Ephemeral.Execution;

namespace OrcaCore.Engine.Ephemeral;

/// <summary>
/// Provides the root management surface for ephemeral workflow instances.
/// </summary>
public sealed class EphemeralManagement
{
    private readonly EphemeralWorkflowEngine engine;
    private readonly IInstanceRegistry registry;

    internal EphemeralManagement(EphemeralWorkflowEngine engine, IInstanceRegistry registry)
    {
        ArgumentNullException.ThrowIfNull(engine);
        ArgumentNullException.ThrowIfNull(registry);

        this.engine = engine;
        this.registry = registry;
    }

    /// <summary>
    /// Selects all instances.
    /// </summary>
    public EphemeralManagementQuery All()
    {
        return new EphemeralManagementQuery(engine, () => registry.List(), requiresDestructiveSafety: true);
    }

    /// <summary>
    /// Selects all instances for one definition.
    /// </summary>
    public EphemeralManagementQuery ForDefinition(DefinitionId definitionId)
    {
        return All().Where(instance => instance.DefinitionId == definitionId);
    }

    /// <summary>
    /// Selects one instance by id.
    /// </summary>
    public EphemeralInstanceManagement Instance(InstanceId instanceId)
    {
        return new EphemeralInstanceManagement(engine, registry, instanceId);
    }

    /// <summary>
    /// Selects a known set of instance ids through one bulk registry lookup.
    /// </summary>
    public EphemeralManagementQuery Instances(IEnumerable<InstanceId> instanceIds)
    {
        ArgumentNullException.ThrowIfNull(instanceIds);

        var ids = instanceIds.ToArray();
        return new EphemeralManagementQuery(engine, () => registry.GetMany(ids), requiresDestructiveSafety: false);
    }

    /// <summary>
    /// Evicts one terminal instance from process memory, ending its queryability. Returns false when
    /// the instance is unknown; throws <see cref="WorkflowLifecycleException"/> while it is still active.
    /// Eviction is a memory-retention operation, not durable retention purge — nothing durable exists here.
    /// </summary>
    public bool Evict(InstanceId instanceId)
    {
        return engine.EvictInstance(instanceId);
    }

    /// <summary>
    /// Evicts every terminal instance from process memory and returns how many were evicted.
    /// Long-lived hosts should call this (or <see cref="Evict"/>) periodically: the ephemeral engine
    /// otherwise retains completed instances for querying until the process exits.
    /// </summary>
    public int EvictTerminal()
    {
        return engine.EvictTerminalInstances();
    }
}

/// <summary>
/// Represents one composable ephemeral management selection.
/// </summary>
public sealed class EphemeralManagementQuery
{
    private readonly EphemeralWorkflowEngine engine;
    private readonly IReadOnlyList<Expression<Func<WorkflowInstanceQueryModel, bool>>> filters;
    private readonly Func<IReadOnlyCollection<object>> loadInstances;
    private readonly bool requiresDestructiveSafety;

    internal EphemeralManagementQuery(
        EphemeralWorkflowEngine engine,
        Func<IReadOnlyCollection<object>> loadInstances,
        bool requiresDestructiveSafety)
        : this(engine, loadInstances, requiresDestructiveSafety, [])
    {
    }

    private EphemeralManagementQuery(
        EphemeralWorkflowEngine engine,
        Func<IReadOnlyCollection<object>> loadInstances,
        bool requiresDestructiveSafety,
        IReadOnlyList<Expression<Func<WorkflowInstanceQueryModel, bool>>> filters)
    {
        this.engine = engine;
        this.loadInstances = loadInstances;
        this.requiresDestructiveSafety = requiresDestructiveSafety;
        this.filters = filters;
    }

    /// <summary>
    /// Adds a constrained metadata predicate to the current selection.
    /// </summary>
    public EphemeralManagementQuery Where(Expression<Func<WorkflowInstanceQueryModel, bool>> predicate)
    {
        ArgumentNullException.ThrowIfNull(predicate);
        QueryPredicateValidator.Validate(predicate);

        return new EphemeralManagementQuery(engine, loadInstances, requiresDestructiveSafety, [.. filters, predicate]);
    }

    /// <summary>
    /// Lists immutable instance snapshots in the current selection.
    /// </summary>
    public IReadOnlyList<WorkflowInstanceSnapshot> List()
    {
        return ApplyFilters(LoadSnapshots()).ToArray();
    }

    /// <summary>
    /// Counts instance snapshots in the current selection.
    /// </summary>
    public int Count()
    {
        return ApplyFilters(LoadSnapshots()).Count();
    }

    /// <summary>
    /// Gets the single snapshot in the current selection.
    /// </summary>
    public WorkflowInstanceSnapshot Get()
    {
        return List().Single();
    }

    /// <summary>
    /// Gets active waits for instances in the current selection.
    /// </summary>
    public IReadOnlyList<ActiveWaitSnapshot> GetActiveWaits()
    {
        return List()
            .SelectMany(snapshot => snapshot.ActiveWaits)
            .ToArray();
    }

    /// <summary>
    /// Gets lifecycle events for instances in the current selection.
    /// </summary>
    public IReadOnlyList<LifecycleEventSnapshot> GetLifecycleEvents()
    {
        return List()
            .SelectMany(snapshot => snapshot.LifecycleEvents)
            .ToArray();
    }

    /// <summary>
    /// Marks selected non-terminal instances as stuck when they have made no progress beyond the threshold.
    /// </summary>
    public IReadOnlyList<WorkflowInstanceSnapshot> DetectStuck(TimeSpan threshold)
    {
        if (threshold <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(threshold), threshold, "Stuck threshold must be positive.");
        }

        var now = engine.GetUtcNow();
        var compiledFilters = filters.Select(filter => filter.Compile()).ToArray();
        var marked = new List<WorkflowInstanceSnapshot>();
        foreach (var instance in loadInstances().OfType<IWorkflowInstance>())
        {
            var snapshot = instance.GetPublishedSnapshot();
            if (compiledFilters.Any(filter => !filter(WorkflowInstanceQueryModel.From(snapshot))))
            {
                continue;
            }

            var updated = instance.MarkStuckIfNoProgress(now, threshold);
            if (updated.IsStuck)
            {
                marked.Add(updated);
            }
        }

        return marked;
    }

    /// <summary>
    /// Returns grouped status statistics for instances in the current selection.
    /// </summary>
    public WorkflowStatistics Statistics()
    {
        var snapshots = List();
        var groups = snapshots
            .GroupBy(snapshot => new
            {
                snapshot.DefinitionId,
                snapshot.DefinitionVersion,
                snapshot.Status
            })
            .Select(group => new WorkflowStatisticsGroup
            {
                DefinitionId = group.Key.DefinitionId,
                DefinitionVersion = group.Key.DefinitionVersion,
                Status = group.Key.Status,
                Count = group.Count()
            })
            .OrderBy(group => group.DefinitionId.ToString(), StringComparer.Ordinal)
            .ThenBy(group => group.DefinitionVersion.Value)
            .ThenBy(group => group.Status)
            .ToArray();
        var activeWaitsByEventName = snapshots
            .SelectMany(snapshot => snapshot.ActiveWaits)
            .GroupBy(wait => wait.EventName, StringComparer.Ordinal)
            .Select(group => new ActiveWaitStatistics
            {
                EventName = group.Key,
                Count = group.Count()
            })
            .OrderBy(group => group.EventName, StringComparer.Ordinal)
            .ToArray();
        var nonTerminal = snapshots
            .Where(snapshot => !IsTerminal(snapshot.Status))
            .ToArray();
        var now = engine.GetUtcNow();
        var oldestActiveAge = nonTerminal.Length == 0
            ? (TimeSpan?)null
            : nonTerminal.Max(snapshot => now - snapshot.CreatedAt);
        var stuckCount = snapshots.Count(snapshot => snapshot.IsStuck || snapshot.HasStuckStep);

        return new WorkflowStatistics
        {
            Groups = groups,
            ActiveWaitsByEventName = activeWaitsByEventName,
            OldestActiveInstanceAge = oldestActiveAge,
            StuckCount = stuckCount
        };
    }

    /// <summary>
    /// Delivers an event to selected instances that currently expose a matching active wait.
    /// </summary>
    public async Task<IReadOnlyList<WorkflowInstanceSnapshot>> RaiseEventAsync<TState>(
        EventEnvelope envelope,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(envelope);

        var snapshots = List()
            .Where(snapshot => snapshot.ActiveWaits.Any(wait =>
                string.Equals(wait.EventName, envelope.EventName, StringComparison.Ordinal) &&
                wait.CorrelationId == envelope.CorrelationId &&
                (string.IsNullOrWhiteSpace(envelope.BranchId) ||
                    string.Equals(wait.BranchId, envelope.BranchId, StringComparison.Ordinal))))
            .ToArray();
        var results = new List<WorkflowInstanceSnapshot>(snapshots.Length);

        foreach (var snapshot in snapshots)
        {
            var result = await engine.RaiseEventAsync<TState>(
                snapshot.InstanceId,
                envelope,
                cancellationToken).ConfigureAwait(false);
            results.Add(result);
        }

        return results;
    }

    /// <summary>
    /// Cancels every non-terminal instance in the current selection.
    /// </summary>
    public async Task<TerminalCommandReport> CancelAsync(CancellationToken cancellationToken)
    {
        var results = new List<WorkflowInstanceSnapshot>();
        foreach (var snapshot in List().Where(snapshot => !IsTerminal(snapshot.Status)))
        {
            results.Add(await engine.CancelInstanceAsync(snapshot.InstanceId, cancellationToken).ConfigureAwait(false));
        }

        return new TerminalCommandReport { Results = results };
    }

    /// <summary>
    /// Rejects broad destructive termination unless explicit safety is supplied.
    /// </summary>
    public Task<TerminalCommandReport> TerminateAsync(CancellationToken cancellationToken)
    {
        if (requiresDestructiveSafety)
        {
            throw new WorkflowLifecycleException(
                "Broad destructive Terminate requires explicit safety confirmation.");
        }

        return TerminateAsync(DestructiveCommandSafety.Confirmed, cancellationToken);
    }

    /// <summary>
    /// Terminates every non-terminal instance in the current selection.
    /// </summary>
    public async Task<TerminalCommandReport> TerminateAsync(
        DestructiveCommandSafety safety,
        CancellationToken cancellationToken)
    {
        if (safety != DestructiveCommandSafety.Confirmed)
        {
            throw new WorkflowLifecycleException(
                "Terminate requires explicit safety confirmation.");
        }

        var results = new List<WorkflowInstanceSnapshot>();
        foreach (var snapshot in List().Where(snapshot => !IsTerminal(snapshot.Status)))
        {
            results.Add(await engine.TerminateInstanceAsync(snapshot.InstanceId, cancellationToken).ConfigureAwait(false));
        }

        return new TerminalCommandReport { Results = results };
    }

    private IEnumerable<WorkflowInstanceSnapshot> ApplyFilters(IEnumerable<WorkflowInstanceSnapshot> snapshots)
    {
        var filtered = snapshots;
        foreach (var filter in filters)
        {
            var compiled = filter.Compile();
            filtered = filtered.Where(snapshot => compiled(WorkflowInstanceQueryModel.From(snapshot)));
        }

        return filtered;
    }

    private IReadOnlyList<WorkflowInstanceSnapshot> LoadSnapshots()
    {
        return loadInstances()
            .OfType<IWorkflowInstance>()
            .Select(instance => instance.GetPublishedSnapshot())
            .ToArray();
    }

    private static bool IsTerminal(WorkflowStatus status)
    {
        return status is WorkflowStatus.Completed or
            WorkflowStatus.Failed or
            WorkflowStatus.Cancelled or
            WorkflowStatus.Terminated or
            WorkflowStatus.Compensated or
            WorkflowStatus.CompensationFailed;
    }
}

/// <summary>
/// Represents one instance-scoped management selection.
/// </summary>
public sealed class EphemeralInstanceManagement
{
    private readonly EphemeralWorkflowEngine engine;
    private readonly InstanceId instanceId;
    private readonly IInstanceRegistry registry;

    internal EphemeralInstanceManagement(
        EphemeralWorkflowEngine engine,
        IInstanceRegistry registry,
        InstanceId instanceId)
    {
        this.engine = engine;
        this.registry = registry;
        this.instanceId = instanceId;
    }

    /// <summary>
    /// Gets an immutable snapshot of the selected instance.
    /// </summary>
    public WorkflowInstanceSnapshot Get()
    {
        return GetInstance().GetPublishedSnapshot();
    }

    /// <summary>
    /// Gets a detached copy of the selected instance state.
    /// </summary>
    public TState GetState<TState>()
    {
        var instance = GetInstance();
        if (instance.StateType != typeof(TState))
        {
            throw new WorkflowDefinitionException(
                $"Workflow instance '{instanceId}' state type is '{instance.StateType.Name}', not requested state type '{typeof(TState).Name}'.");
        }

        if (!instance.HasPublishedState && instance.GetPublishedSnapshot().ActiveStep is not null)
        {
            throw new WorkflowLifecycleException(
                $"Workflow instance '{instanceId}' state cannot be copied while user step code is running.");
        }

        return (TState)instance.CopyState(engine.StateSnapshotter);
    }

    /// <summary>
    /// Gets immutable snapshots of currently active waits for the selected instance.
    /// </summary>
    public IReadOnlyList<ActiveWaitSnapshot> GetActiveWaits()
    {
        return Get().ActiveWaits.ToArray();
    }

    /// <summary>
    /// Gets lifecycle events for the selected instance.
    /// </summary>
    public IReadOnlyList<LifecycleEventSnapshot> GetLifecycleEvents()
    {
        return Get().LifecycleEvents.ToArray();
    }

    /// <summary>
    /// Selects lifecycle and execution metadata for one step path on this instance.
    /// </summary>
    public EphemeralStepManagement Step(string stepPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(stepPath);

        return new EphemeralStepManagement(this, stepPath);
    }

    /// <summary>
    /// Selects saga metadata for this instance.
    /// </summary>
    public EphemeralSagaManagement Saga()
    {
        return new EphemeralSagaManagement(this);
    }

    /// <summary>
    /// Cooperatively cancels the selected instance.
    /// </summary>
    public Task<WorkflowInstanceSnapshot> CancelAsync(CancellationToken cancellationToken)
    {
        return engine.CancelInstanceAsync(instanceId, cancellationToken);
    }

    /// <summary>
    /// Cooperatively interrupts in-flight user code and terminates the selected instance.
    /// </summary>
    public Task<WorkflowInstanceSnapshot> TerminateAsync(CancellationToken cancellationToken)
    {
        return engine.TerminateInstanceAsync(instanceId, cancellationToken);
    }

    private IWorkflowInstance GetInstance()
    {
        if (!registry.TryGet(instanceId, out var instance) ||
            instance is not IWorkflowInstance workflowInstance)
        {
            throw new WorkflowRoutingException(
                $"No workflow instance exists for instance id '{instanceId}'.");
        }

        return workflowInstance;
    }

}

/// <summary>
/// Metadata-only model accepted by management query predicates.
/// </summary>
public sealed record WorkflowInstanceQueryModel
{
    public required InstanceId InstanceId { get; init; }

    public required DefinitionId DefinitionId { get; init; }

    public required DefinitionVersion DefinitionVersion { get; init; }

    public required WorkflowStatus Status { get; init; }

    public required DateTimeOffset CreatedAt { get; init; }

    public required DateTimeOffset UpdatedAt { get; init; }

    public string? ErrorSummary { get; init; }

    public string? EndOutcomeName { get; init; }

    public bool IsStuck { get; init; }

    public bool HasStuckStep { get; init; }

    internal static WorkflowInstanceQueryModel From(WorkflowInstanceSnapshot snapshot)
    {
        return new WorkflowInstanceQueryModel
        {
            InstanceId = snapshot.InstanceId,
            DefinitionId = snapshot.DefinitionId,
            DefinitionVersion = snapshot.DefinitionVersion,
            Status = snapshot.Status,
            CreatedAt = snapshot.CreatedAt,
            UpdatedAt = snapshot.UpdatedAt,
            ErrorSummary = snapshot.ErrorSummary,
            EndOutcomeName = snapshot.EndOutcomeName,
            IsStuck = snapshot.IsStuck,
            HasStuckStep = snapshot.HasStuckStep
        };
    }
}

/// <summary>
/// Grouped management statistics for an instance selection.
/// </summary>
public sealed record WorkflowStatistics
{
    public required IReadOnlyList<WorkflowStatisticsGroup> Groups { get; init; }

    public IReadOnlyList<ActiveWaitStatistics> ActiveWaitsByEventName { get; init; } = [];

    public TimeSpan? OldestActiveInstanceAge { get; init; }

    public int StuckCount { get; init; }
}

/// <summary>
/// Count for active waits grouped by event name.
/// </summary>
public sealed record ActiveWaitStatistics
{
    public required string EventName { get; init; }

    public required int Count { get; init; }
}

/// <summary>
/// Count for one definition-version-status group.
/// </summary>
public sealed record WorkflowStatisticsGroup
{
    public required DefinitionId DefinitionId { get; init; }

    public required DefinitionVersion DefinitionVersion { get; init; }

    public required WorkflowStatus Status { get; init; }

    public required int Count { get; init; }
}

/// <summary>
/// Explicit confirmation token for broad destructive commands.
/// </summary>
public enum DestructiveCommandSafety
{
    /// <summary>
    /// Caller explicitly confirmed the destructive breadth.
    /// </summary>
    Confirmed
}

/// <summary>
/// Result summary for terminal management commands.
/// </summary>
public sealed record TerminalCommandReport
{
    public required IReadOnlyList<WorkflowInstanceSnapshot> Results { get; init; }

    public int AffectedCount => Results.Count;
}

/// <summary>
/// Instance-scoped query surface for one step path.
/// </summary>
public sealed class EphemeralStepManagement
{
    private readonly EphemeralInstanceManagement instance;
    private readonly string stepPath;

    internal EphemeralStepManagement(EphemeralInstanceManagement instance, string stepPath)
    {
        this.instance = instance;
        this.stepPath = stepPath;
    }

    public ActiveStepSnapshot? GetActiveStep()
    {
        var activeStep = instance.Get().ActiveStep;
        return activeStep is not null && string.Equals(activeStep.StepPath, stepPath, StringComparison.Ordinal)
            ? activeStep
            : null;
    }

    public IReadOnlyList<LifecycleEventSnapshot> GetLifecycleEvents()
    {
        return instance.GetLifecycleEvents()
            .Where(lifecycleEvent => string.Equals(lifecycleEvent.StepPath, stepPath, StringComparison.Ordinal))
            .ToArray();
    }
}

/// <summary>
/// Instance-scoped query surface for saga metadata.
/// </summary>
public sealed class EphemeralSagaManagement
{
    private readonly EphemeralInstanceManagement instance;

    internal EphemeralSagaManagement(EphemeralInstanceManagement instance)
    {
        this.instance = instance;
    }

    public IReadOnlyList<SagaAuditScopeSnapshot> GetAudits()
    {
        return instance.Get().SagaAudits.ToArray();
    }
}

internal sealed class QueryPredicateValidator : ExpressionVisitor
{
    private static readonly HashSet<ExpressionType> AllowedBinaryNodeTypes =
    [
        ExpressionType.AndAlso,
        ExpressionType.OrElse,
        ExpressionType.Equal,
        ExpressionType.NotEqual,
        ExpressionType.GreaterThan,
        ExpressionType.GreaterThanOrEqual,
        ExpressionType.LessThan,
        ExpressionType.LessThanOrEqual
    ];

    private QueryPredicateValidator()
    {
    }

    internal static void Validate(Expression expression)
    {
        new QueryPredicateValidator().Visit(expression);
    }

    protected override Expression VisitBinary(BinaryExpression node)
    {
        if (!AllowedBinaryNodeTypes.Contains(node.NodeType))
        {
            throw new NotSupportedException(
                $"Management predicates cannot use binary operator '{node.NodeType}'.");
        }

        return base.VisitBinary(node);
    }

    protected override Expression VisitConditional(ConditionalExpression node)
    {
        throw new NotSupportedException("Management predicates cannot use conditional expressions.");
    }

    protected override Expression VisitIndex(IndexExpression node)
    {
        throw new NotSupportedException("Management predicates cannot use indexer access.");
    }

    protected override Expression VisitInvocation(InvocationExpression node)
    {
        throw new NotSupportedException("Management predicates cannot invoke delegates.");
    }

    protected override Expression VisitMethodCall(MethodCallExpression node)
    {
        if (node.Method.IsSpecialName && string.Equals(node.Method.Name, "get_Chars", StringComparison.Ordinal))
        {
            throw new NotSupportedException("Management predicates cannot use indexer access.");
        }

        throw new NotSupportedException("Management predicates cannot call methods.");
    }

    protected override Expression VisitNew(NewExpression node)
    {
        throw new NotSupportedException("Management predicates cannot construct objects.");
    }
}
