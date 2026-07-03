using System.Linq.Expressions;
using OrcaCore.Abstractions.Errors;
using OrcaCore.Abstractions.Ids;
using OrcaCore.Abstractions.Instances;
using OrcaCore.Core.Lifecycle;
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
    private readonly TerminateInstanceAsync terminateInstance;
    private readonly DefinitionId? definitionId;
    private readonly InstanceId? instanceId;
    private readonly Func<WorkflowInstanceSnapshot, bool>? predicate;
    private readonly bool isUnconstrainedAll;

    internal ManagementQueryScope(
        IInstanceRegistry instanceRegistry,
        TerminateInstanceAsync terminateInstance,
        DefinitionId? definitionId,
        InstanceId? instanceId,
        bool isUnconstrainedAll,
        Func<WorkflowInstanceSnapshot, bool>? predicate = null)
    {
        this.instanceRegistry = instanceRegistry;
        this.terminateInstance = terminateInstance;
        this.definitionId = definitionId;
        this.instanceId = instanceId;
        this.isUnconstrainedAll = isUnconstrainedAll;
        this.predicate = predicate;
    }

    /// <summary>
    /// Narrows the current scope with a constrained, translatable predicate over
    /// <see cref="WorkflowInstanceSnapshot"/> metadata (MG-002). Arbitrary delegates/external
    /// calls/non-translatable constructs are rejected by <see cref="SnapshotPredicateValidator"/>
    /// before the expression is ever compiled. Has no side effects (MG-001 stage 2). Narrowing
    /// with <c>Where(...)</c> clears the MG-004 "unconstrained All()" flag — a filtered selection
    /// is no longer the literal broad-destructive case <see cref="Terminate"/> guards against.
    /// </summary>
    public ManagementQueryScope Where(Expression<Func<WorkflowInstanceSnapshot, bool>> filter)
    {
        var compiled = SnapshotPredicateValidator.ValidateAndCompile(filter);
        var combined = predicate is null ? compiled : snapshot => predicate(snapshot) && compiled(snapshot);
        return new ManagementQueryScope(instanceRegistry, terminateInstance, definitionId, instanceId, isUnconstrainedAll: false, combined);
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

    /// <summary>
    /// Forced terminate (CR-031) over every instance currently matching this scope (MG-004): each
    /// instance still commits through its own execution lane (CR-040), independently, so one
    /// instance's outcome cannot block another's. An already-terminal instance in the selection is
    /// skipped, not reported as a failure — Terminate's contract is "every matching instance ends
    /// up terminal", which an already-terminal instance already satisfies.
    ///
    /// MG-004 safety gate: a literal unconstrained <c>All()</c> selection (never narrowed by
    /// <see cref="Where"/>, <c>ForDefinition</c>, or <c>Instance</c>) is a broad destructive
    /// operation and requires <paramref name="confirmBroadSelection"/> to be explicitly
    /// <see langword="true"/> — otherwise this throws <see cref="WorkflowDefinitionException"/>
    /// without terminating anything. A <see cref="Where"/>-filtered, definition-scoped, or
    /// instance-scoped selection does not need the gate.
    /// </summary>
    public async Task<IReadOnlyList<TerminalCommandOutcome>> Terminate(bool confirmBroadSelection = false, CancellationToken cancellationToken = default)
    {
        if (isUnconstrainedAll && !confirmBroadSelection)
        {
            throw new WorkflowDefinitionException(
                "Terminate() over an unconstrained All() selection affects every registered instance across every " +
                "definition (MG-004). Narrow the scope first (Where(...)/ForDefinition/Instance), or pass " +
                "confirmBroadSelection: true to explicitly confirm this broad destructive operation.");
        }

        var targets = MatchingInstances()
            .Where(instance => !LifecycleMachine.TerminalStatuses.Contains(instance.Status))
            .Select(instance => instance.InstanceId)
            .ToArray();

        var outcomes = await Task.WhenAll(
            targets.Select(id => terminateInstance(id, cancellationToken))).ConfigureAwait(false);

        return outcomes;
    }

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
