/// <summary>
/// Application-facing retention policy for durable artifact cleanup.
/// Prefer this over raw timestamp cutoffs in runtime code.
/// </summary>
namespace OrcaCore.Runtime.Durable.Management;

public sealed record DurableArtifactRetentionPolicy(
    TimeSpan? ProcessedInboxRetention = null,
    TimeSpan? TerminalOutboxRetention = null,
    TimeSpan? HistoryRetention = null)
{
    public static DurableArtifactRetentionPolicy Uniform(TimeSpan retention) =>
        new(retention, retention, retention);

    internal DurableArtifactRetentionCutoffs Resolve(DateTimeOffset now) =>
        new(
            ProcessedInboxRetention is null ? null : now - ProcessedInboxRetention.Value,
            TerminalOutboxRetention is null ? null : now - TerminalOutboxRetention.Value,
            HistoryRetention is null ? null : now - HistoryRetention.Value);
}
