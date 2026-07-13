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
    public const string StreamVersionKey = "stream_version";
    public const string OutboxKindKey = "orca.outbox.kind";
    public const string OutboxRecordIdKey = "orca.outbox.record_id";
    public const string OutboxResultKey = "result";
    public const string OutboxStateKey = "state";
    public const string OutboxMaxCountKey = "orca.outbox.max_count";
    public const string OutboxClaimedCountKey = "orca.outbox.claimed_count";
    public const string OutboxDispatchedCountKey = "orca.outbox.dispatched_count";
    public const string DriverOutcomeKey = "outcome";
    public const string ParkReasonKey = "reason";
    public const string DriverFailureSourceKey = "source";
}
