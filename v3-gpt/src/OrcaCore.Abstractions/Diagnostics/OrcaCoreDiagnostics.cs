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

    public static readonly string[] MeterNames = [SourceName, DurableSourceName];

    public static readonly string[] ActivitySourceNames = [SourceName, DurableSourceName];

    public const string ExecutionModeKey = "orca.execution.mode";
    public const string DurableExecutionMode = "durable";
    public const string DefinitionIdKey = "orca.definition.id";
    public const string DefinitionVersionKey = "orca.definition.version";
    public const string StatusKey = "orca.status";
    public const string InstanceIdKey = "orca.instance.id";
    public const string CommandTypeKey = "orca.command.type";
    public const string CommandOutcomeKey = "outcome";
    public const string OutboxKindKey = "orca.outbox.kind";
    public const string OutboxRecordIdKey = "orca.outbox.record_id";
    public const string OutboxResultKey = "result";
    public const string OutboxMaxCountKey = "orca.outbox.max_count";
    public const string OutboxClaimedCountKey = "orca.outbox.claimed_count";
    public const string OutboxDispatchedCountKey = "orca.outbox.dispatched_count";
}
