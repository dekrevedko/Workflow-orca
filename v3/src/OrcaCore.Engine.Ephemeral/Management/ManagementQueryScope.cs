using System.Linq.Expressions;
using OrcaCore.Abstractions.Errors;
using OrcaCore.Abstractions.Ids;
using OrcaCore.Abstractions.Instances;
using OrcaCore.Engine.Ephemeral.Execution;

namespace OrcaCore.Engine.Ephemeral.Management;

/// <summary>
/// A selected set of instances (a scope, optionally narrowed by <see cref="Where"/>) with
/// terminal query operations (MG-001 stage 3). See <see cref="ManagementQueryRoot"/> for how a
/// scope is reached.
/// </summary>
public sealed class ManagementQueryScope
{
    private readonly IInstanceRegistry instanceRegistry;
    private readonly DefinitionId? definitionId;
    private readonly InstanceId? instanceId;
    private readonly Func<WorkflowInstanceSnapshot, bool>? predicate;

    internal ManagementQueryScope(
        IInstanceRegistry instanceRegistry,
        DefinitionId? definitionId,
        InstanceId? instanceId,
        Func<WorkflowInstanceSnapshot, bool>? predicate = null)
    {
        this.instanceRegistry = instanceRegistry;
        this.definitionId = definitionId;
        this.instanceId = instanceId;
        this.predicate = predicate;
    }

    /// <summary>
    /// Narrows the current scope with a constrained, translatable predicate over
    /// <see cref="WorkflowInstanceSnapshot"/> metadata (MG-002). Arbitrary delegates/external
    /// calls/non-translatable constructs are rejected by <see cref="SnapshotPredicateValidator"/>
    /// before the expression is ever compiled. Has no side effects (MG-001 stage 2).
    /// </summary>
    public ManagementQueryScope Where(Expression<Func<WorkflowInstanceSnapshot, bool>> filter)
    {
        var compiled = SnapshotPredicateValidator.ValidateAndCompile(filter);
        var combined = predicate is null ? compiled : snapshot => predicate(snapshot) && compiled(snapshot);
        return new ManagementQueryScope(instanceRegistry, definitionId, instanceId, combined);
    }

    /// <summary>Returns every snapshot currently matching this scope (MG-001 terminal query, MG-005 snapshot-only, EV-013/AC-115 bulk read).</summary>
    public IReadOnlyList<WorkflowInstanceSnapshot> List() => [.. MatchingSnapshots()];

    /// <summary>Returns the count of instances currently matching this scope, using the same selection as <see cref="List"/> (MG-003).</summary>
    public int Count() => MatchingSnapshots().Count();

    /// <summary>
    /// Returns the single matching snapshot, or <c>null</c> if none match. Throws
    /// <see cref="WorkflowDefinitionException"/> if more than one instance matches - a single
    /// result is the only sensible outcome for a "get one" query.
    /// </summary>
    public WorkflowInstanceSnapshot? Get()
    {
        var instance = SingleMatchingInstance();
        return instance is null ? null : ToSnapshot(instance);
    }

    /// <summary>
    /// Returns a copy of the single matching instance's business state as <typeparamref name="TState"/>
    /// (CR-021: never the live reference), or <c>null</c> if no instance matches. Throws
    /// <see cref="WorkflowDefinitionException"/> if more than one instance matches, or if the
    /// matching instance's actual state type is not <typeparamref name="TState"/>.
    /// </summary>
    public TState? GetState<TState>()
    {
        var instance = SingleMatchingInstance();
        if (instance is null)
        {
            return default;
        }

        object copy;
        try
        {
            copy = instance.GetStateCopy();
        }
        catch (Exception exception) when (exception is not WorkflowDefinitionException)
        {
            throw new WorkflowDefinitionException(
                $"Failed to copy business state for instance '{instance.InstanceId}': {exception.Message}", exception);
        }

        if (copy is not TState typed)
        {
            throw new WorkflowDefinitionException(
                $"GetState<{typeof(TState).Name}>() does not match instance '{instance.InstanceId}''s actual " +
                $"business state type '{copy.GetType().Name}'. Call GetState with the definition's real state type.");
        }

        return typed;
    }

    /// <summary>Returns active-wait snapshots (metadata only, never live wait references) for every instance matching this scope (MG-030).</summary>
    public IReadOnlyList<ActiveWaitSnapshot> GetActiveWaits() =>
        [.. MatchingInstances().SelectMany(instance =>
            instance.ActiveWaitSources().Select(source =>
                new ActiveWaitSnapshot(instance.InstanceId, source.WaitId, source.EventName, source.CorrelationId, source.RegisteredAt)))];

    /// <summary>Grouped instance counts by definition, version, and status for this scope (MG-030/AC-503).</summary>
    public IReadOnlyList<InstanceStatisticsGroup> Statistics() =>
        [.. MatchingInstances()
            .GroupBy(instance => (instance.DefinitionId, instance.DefinitionVersion, instance.Status))
            .Select(group => new InstanceStatisticsGroup(group.Key.DefinitionId, group.Key.DefinitionVersion, group.Key.Status, group.Count()))];

    private IEnumerable<IWorkflowInstance> MatchingInstances()
    {
        var all = instanceRegistry.GetAll();

        foreach (var instance in all)
        {
            if (instanceId is { } targetInstanceId && instance.InstanceId != targetInstanceId)
            {
                continue;
            }

            if (definitionId is { } targetDefinitionId && instance.DefinitionId != targetDefinitionId)
            {
                continue;
            }

            if (predicate is not null && !predicate(ToSnapshot(instance)))
            {
                continue;
            }

            yield return instance;
        }
    }

    private IEnumerable<WorkflowInstanceSnapshot> MatchingSnapshots() => MatchingInstances().Select(ToSnapshot);

    private IWorkflowInstance? SingleMatchingInstance()
    {
        using var enumerator = MatchingInstances().GetEnumerator();
        if (!enumerator.MoveNext())
        {
            return null;
        }

        var first = enumerator.Current;
        if (enumerator.MoveNext())
        {
            throw new WorkflowDefinitionException(
                "This scope matches more than one instance; narrow it (e.g. Instance(id)) before requesting a single result.");
        }

        return first;
    }

    private static WorkflowInstanceSnapshot ToSnapshot(IWorkflowInstance instance) =>
        new(
            instance.InstanceId,
            instance.DefinitionId,
            instance.DefinitionVersion,
            instance.Status,
            instance.CreatedAt,
            instance.UpdatedAt,
            instance.ErrorSummary,
            instance.EndOutcomeName);
}
