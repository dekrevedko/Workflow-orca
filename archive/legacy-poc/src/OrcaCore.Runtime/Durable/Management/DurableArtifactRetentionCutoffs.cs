/// <summary>
/// Provider-facing resolved retention cutoffs used by <see cref="IWorkflowStore"/>.
/// Runtime/application code should normally use <see cref="DurableArtifactRetentionPolicy"/>.
/// </summary>
namespace OrcaCore.Runtime.Durable.Management;

public sealed record DurableArtifactRetentionCutoffs(
    DateTimeOffset? ProcessedInboxOlderThan,
    DateTimeOffset? TerminalOutboxOlderThan,
    DateTimeOffset? HistoryOlderThan);
