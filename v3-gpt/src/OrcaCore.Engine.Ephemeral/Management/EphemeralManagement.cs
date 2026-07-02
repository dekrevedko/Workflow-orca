using System.Linq.Expressions;
using System.Text.Json;
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
        return new EphemeralManagementQuery(engine, () => registry.List());
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
        return new EphemeralInstanceManagement(registry, instanceId);
    }

    /// <summary>
    /// Selects a known set of instance ids through one bulk registry lookup.
    /// </summary>
    public EphemeralManagementQuery Instances(IEnumerable<InstanceId> instanceIds)
    {
        ArgumentNullException.ThrowIfNull(instanceIds);

        var ids = instanceIds.ToArray();
        return new EphemeralManagementQuery(engine, () => registry.GetMany(ids));
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

    internal EphemeralManagementQuery(
        EphemeralWorkflowEngine engine,
        Func<IReadOnlyCollection<object>> loadInstances)
        : this(engine, loadInstances, [])
    {
    }

    private EphemeralManagementQuery(
        EphemeralWorkflowEngine engine,
        Func<IReadOnlyCollection<object>> loadInstances,
        IReadOnlyList<Expression<Func<WorkflowInstanceQueryModel, bool>>> filters)
    {
        this.engine = engine;
        this.loadInstances = loadInstances;
        this.filters = filters;
    }

    /// <summary>
    /// Adds a constrained metadata predicate to the current selection.
    /// </summary>
    public EphemeralManagementQuery Where(Expression<Func<WorkflowInstanceQueryModel, bool>> predicate)
    {
        ArgumentNullException.ThrowIfNull(predicate);
        QueryPredicateValidator.Validate(predicate);

        return new EphemeralManagementQuery(engine, loadInstances, [.. filters, predicate]);
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
    /// Returns grouped status statistics for instances in the current selection.
    /// </summary>
    public WorkflowStatistics Statistics()
    {
        var groups = List()
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

        return new WorkflowStatistics { Groups = groups };
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
                wait.CorrelationId == envelope.CorrelationId))
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
            .Select(instance => instance.ToSnapshot())
            .ToArray();
    }
}

/// <summary>
/// Represents one instance-scoped management selection.
/// </summary>
public sealed class EphemeralInstanceManagement
{
    private readonly InstanceId instanceId;
    private readonly IInstanceRegistry registry;

    internal EphemeralInstanceManagement(IInstanceRegistry registry, InstanceId instanceId)
    {
        this.registry = registry;
        this.instanceId = instanceId;
    }

    /// <summary>
    /// Gets an immutable snapshot of the selected instance.
    /// </summary>
    public WorkflowInstanceSnapshot Get()
    {
        return GetInstance().ToSnapshot();
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

        return CopyState((TState)instance.StateObject);
    }

    /// <summary>
    /// Gets immutable snapshots of currently active waits for the selected instance.
    /// </summary>
    public IReadOnlyList<ActiveWaitSnapshot> GetActiveWaits()
    {
        return Get().ActiveWaits.ToArray();
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

    private static TState CopyState<TState>(TState state)
    {
        var serialized = JsonSerializer.Serialize(state);
        return JsonSerializer.Deserialize<TState>(serialized)
            ?? throw new WorkflowDefinitionException(
                $"Workflow state type '{typeof(TState).Name}' could not be copied.");
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
            EndOutcomeName = snapshot.EndOutcomeName
        };
    }
}

/// <summary>
/// Grouped management statistics for an instance selection.
/// </summary>
public sealed record WorkflowStatistics
{
    public required IReadOnlyList<WorkflowStatisticsGroup> Groups { get; init; }
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

internal sealed class QueryPredicateValidator : ExpressionVisitor
{
    private QueryPredicateValidator()
    {
    }

    internal static void Validate(Expression expression)
    {
        new QueryPredicateValidator().Visit(expression);
    }

    protected override Expression VisitInvocation(InvocationExpression node)
    {
        throw new NotSupportedException("Management predicates cannot invoke delegates.");
    }

    protected override Expression VisitMethodCall(MethodCallExpression node)
    {
        throw new NotSupportedException("Management predicates cannot call methods.");
    }

    protected override Expression VisitNew(NewExpression node)
    {
        throw new NotSupportedException("Management predicates cannot construct objects.");
    }
}
