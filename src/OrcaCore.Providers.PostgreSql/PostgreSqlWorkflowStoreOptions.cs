using OrcaCore.Abstractions.Ids;
using OrcaCore.Abstractions.Providers;

namespace OrcaCore.Providers.PostgreSql;

/// <summary>
/// Configures PostgreSQL workflow-store behavior.
/// </summary>
internal sealed class PostgreSqlWorkflowStoreOptions
{
    /// <summary>
    /// Optional hook invoked inside the append transaction after all writes are staged and before commit.
    /// </summary>
    public Func<PostgreSqlAppendContext, CancellationToken, ValueTask>? BeforeCommitAsync { get; set; }

    internal ValueTask InvokeBeforeCommitAsync(
        PostgreSqlAppendContext context,
        CancellationToken cancellationToken)
    {
        return BeforeCommitAsync?.Invoke(context, cancellationToken) ?? ValueTask.CompletedTask;
    }
}

/// <summary>
/// Describes a PostgreSQL append transaction at the pre-commit hook boundary.
/// </summary>
internal sealed record PostgreSqlAppendContext(
    WorkflowStreamId StreamId,
    StreamVersion ExpectedVersion,
    StreamVersion NewVersion,
    int EventCount,
    int InboxOperationCount,
    int OutboxRecordCount,
    int ProjectionOperationCount,
    int TimerScheduleCount);
