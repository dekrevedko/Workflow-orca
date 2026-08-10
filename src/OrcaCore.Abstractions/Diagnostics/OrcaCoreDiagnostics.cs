namespace OrcaCore.Abstractions.Diagnostics;

/// <summary>
/// Defines stable telemetry source names and attribute keys.
/// </summary>
public static class OrcaCoreDiagnostics
{
    /// <summary>
    /// Gets the root OrcaCore source name.
    /// </summary>
    public const string SourceName = "OrcaCore";

    /// <summary>
    /// Gets the durable engine source and meter name.
    /// </summary>
    public const string DurableSourceName = "OrcaCore.Engine.Durable";

    /// <summary>
    /// Gets the ephemeral engine source and meter name.
    /// </summary>
    public const string EphemeralSourceName = "OrcaCore.Engine.Ephemeral";

    public const string EphemeralExecutionMode = "ephemeral";

    public const string InMemoryProviderName = "in-memory";
    public const string PostgreSqlProviderName = "postgresql";

    public const string StartOperation = "start";
    public const string RaiseEventOperation = "raise_event";
    public const string FireDueTimersOperation = "fire_due_timers";
    public const string RequestCancellationOperation = "request_cancellation";
    public const string RequestTerminationOperation = "request_termination";

    public const string CommandProcessActivity = "orca.command.process";
    public const string ProviderCommitActivity = "orca.provider.commit";
    public const string StepExecuteActivity = "orca.step.execute";
    public const string OutboxDispatchActivity = "orca.outbox.dispatch";
    public const string OutboxPumpCycleActivity = "orca.outbox.pump_cycle";
    public const string EventApplyActivity = "orca.event.apply";

    public const string PendingOutboxState = "pending";
    public const string RetryableOutboxState = "retryable";
    public const string ClaimedOutboxState = "claimed";
    public const string PoisonedOutboxState = "poisoned";
    public const string SuccessDispatchResult = "success";
    public const string RetryableDispatchResult = RetryableOutboxState;
    public const string PermanentDispatchResult = "permanent";

    public const string PendingCommitResourceState = "pending_commit";
    public const string HeldResourceState = "held";
    public const string ReviewMarkedResourceState = "review_marked";
    public const string AmbiguousHeldResourceState = "ambiguous_held";
    public const string QuarantinedResourceState = "quarantined";

    public const string InstanceStartedLifecycleEvent = "InstanceStarted";
    public const string MissingTransientPoolsError = "missing_transient_pools";

    public const string SecondsUnit = "s";

    public const string InMemoryProviderSourceName = "OrcaCore.Providers.InMemory";
    public const string PostgreSqlProviderSourceName = "OrcaCore.Providers.PostgreSql";
    public const string RabbitMqProviderSourceName = "OrcaCore.Providers.RabbitMq";
    public const string RedisProviderSourceName = "OrcaCore.Providers.Redis";
    public const string RelationalProviderSourceName = "OrcaCore.Providers.Relational";
    public const string SqlServerProviderSourceName = "OrcaCore.Providers.SqlServer";
    public const string ZeroMqProviderSourceName = "OrcaCore.Providers.ZeroMq";

    public static readonly string[] ProviderSourceNames =
    [
        InMemoryProviderSourceName,
        PostgreSqlProviderSourceName,
        RabbitMqProviderSourceName,
        RedisProviderSourceName,
        RelationalProviderSourceName,
        SqlServerProviderSourceName,
        ZeroMqProviderSourceName
    ];

    public static readonly string[] MeterNames =
    [
        SourceName,
        DurableSourceName,
        EphemeralSourceName,
        .. ProviderSourceNames
    ];

    public static readonly string[] ActivitySourceNames =
    [
        SourceName,
        DurableSourceName,
        EphemeralSourceName,
        .. ProviderSourceNames
    ];

    public const string ExecutionModeKey = "orca.execution.mode";
    public const string DurableExecutionMode = "durable";
    public const string ProviderNameKey = "orca.provider.name";
    public const string DefinitionIdKey = "orca.definition.id";
    public const string DefinitionVersionKey = "orca.definition.version";
    public const string StatusKey = "orca.status";
    public const string InstanceIdKey = "orca.instance.id";
    public const string CommandTypeKey = "orca.command.type";
    public const string CommandOutcomeKey = "outcome";
    public const string EventTypeKey = "orca.event.type";
    public const string StepPathKey = "orca.step.path";
    public const string ErrorKindKey = "error.kind";
    public const string LifecycleEventNameKey = "event.name";
    public const string DurableKey = "durable";
    public const string WaitEventNameKey = "orca.wait.event_name";
    public const string ResourcePoolNameKey = "pool.name";
    public const string ProviderOperationKey = "operation";
    public const string AppendProviderOperation = "append";
    public const string StreamVersionKey = "stream_version";
    public const string OutboxKindKey = "orca.outbox.kind";
    public const string OutboxRecordIdKey = "orca.outbox.record_id";
    public const string OutboxResultKey = "result";
    public const string StateKey = "state";
    public const string OutboxStateKey = StateKey;
    public const string ResourcePoolStateKey = StateKey;
    public const string QueueLaneKey = "orca.queue.lane";
    public const string ContinuationQueueLane = "continuation";
    public const string ExternalOutboxQueueLane = "external";
    public const string OutboxMaxCountKey = "orca.outbox.max_count";
    public const string OutboxClaimedCountKey = "orca.outbox.claimed_count";
    public const string OutboxDispatchedCountKey = "orca.outbox.dispatched_count";
    public const string DriverOutcomeKey = CommandOutcomeKey;
    public const string ParkReasonKey = "reason";
    public const string DriverFailureSourceKey = "source";
    public const string TraceIdKey = "trace_id";
    public const string SpanIdKey = "span_id";
    public const string TraceFlagsKey = "trace_flags";
}
