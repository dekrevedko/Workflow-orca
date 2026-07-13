namespace OrcaCore.Abstractions.Instances;

/// <summary>
/// Provides durable, queryable saga audit state for one workflow instance.
/// </summary>
public sealed record SagaAuditSnapshot
{
    /// <summary>
    /// Gets compensation scopes recorded for the saga instance.
    /// </summary>
    public IReadOnlyList<SagaAuditScopeSnapshot> Scopes { get; init; } = [];
}

/// <summary>
/// Provides durable audit state for one saga compensation scope.
/// </summary>
public sealed record SagaAuditScopeSnapshot
{
    /// <summary>
    /// Gets the logical compensation scope identity.
    /// </summary>
    public required string ScopeId { get; init; }

    /// <summary>
    /// Gets the current terminal outcome visible for this scope, when known.
    /// </summary>
    public WorkflowStatus? Outcome { get; init; }

    /// <summary>
    /// Gets completed forward actions eligible for compensation.
    /// </summary>
    public IReadOnlyList<SagaForwardActionSnapshot> ForwardActions { get; init; } = [];

    /// <summary>
    /// Gets compensating actions in deterministic execution order.
    /// </summary>
    public IReadOnlyList<SagaCompensationActionSnapshot> CompensationActions { get; init; } = [];

    /// <summary>
    /// Gets operator recovery interventions recorded for this scope.
    /// </summary>
    public IReadOnlyList<SagaRecoveryInterventionSnapshot> RecoveryInterventions { get; init; } = [];
}

/// <summary>
/// Describes one successfully completed saga forward action.
/// </summary>
public sealed record SagaForwardActionSnapshot
{
    /// <summary>
    /// Gets the logical compensation scope identity.
    /// </summary>
    public required string ScopeId { get; init; }

    /// <summary>
    /// Gets the stable forward action key.
    /// </summary>
    public required string ActionKey { get; init; }

    /// <summary>
    /// Gets the stable compensation action key.
    /// </summary>
    public required string CompensationKey { get; init; }

    /// <summary>
    /// Gets when the forward action completed.
    /// </summary>
    public required DateTimeOffset CompletedAt { get; init; }
}

/// <summary>
/// Describes the durable status of one compensating action.
/// </summary>
public enum SagaCompensationActionStatus
{
    /// <summary>
    /// The compensating action has started but no terminal outcome is recorded.
    /// </summary>
    Started,

    /// <summary>
    /// The compensating action completed successfully.
    /// </summary>
    Completed,

    /// <summary>
    /// The compensating action failed.
    /// </summary>
    Failed
}

/// <summary>
/// Describes one saga compensating action.
/// </summary>
public sealed record SagaCompensationActionSnapshot
{
    /// <summary>
    /// Gets the logical compensation scope identity.
    /// </summary>
    public required string ScopeId { get; init; }

    /// <summary>
    /// Gets the stable compensating action key.
    /// </summary>
    public required string ActionKey { get; init; }

    /// <summary>
    /// Gets the deterministic zero-based compensation order.
    /// </summary>
    public required int Order { get; init; }

    /// <summary>
    /// Gets when the compensating action started.
    /// </summary>
    public required DateTimeOffset StartedAt { get; init; }

    /// <summary>
    /// Gets when the compensating action completed, if successful.
    /// </summary>
    public DateTimeOffset? CompletedAt { get; init; }

    /// <summary>
    /// Gets when the compensating action failed, if failed.
    /// </summary>
    public DateTimeOffset? FailedAt { get; init; }

    /// <summary>
    /// Gets the failure summary when the action failed.
    /// </summary>
    public string? ErrorSummary { get; init; }

    /// <summary>
    /// Gets the durable compensation action status.
    /// </summary>
    public required SagaCompensationActionStatus Status { get; init; }
}

/// <summary>
/// Describes one operator recovery intervention recorded for a saga.
/// </summary>
public sealed record SagaRecoveryInterventionSnapshot
{
    /// <summary>
    /// Gets the logical compensation scope identity.
    /// </summary>
    public required string ScopeId { get; init; }

    /// <summary>
    /// Gets the compensating action key affected by the intervention.
    /// </summary>
    public required string ActionKey { get; init; }

    /// <summary>
    /// Gets the operator identity supplied by the caller.
    /// </summary>
    public required string OperatorId { get; init; }

    /// <summary>
    /// Gets the recovery action name selected by policy.
    /// </summary>
    public required string RecoveryAction { get; init; }

    /// <summary>
    /// Gets the operator-supplied reason.
    /// </summary>
    public string? Reason { get; init; }

    /// <summary>
    /// Gets when the intervention was recorded.
    /// </summary>
    public required DateTimeOffset RecordedAt { get; init; }

    /// <summary>
    /// Gets the workflow status produced by this intervention.
    /// </summary>
    public required WorkflowStatus TargetStatus { get; init; }
}
