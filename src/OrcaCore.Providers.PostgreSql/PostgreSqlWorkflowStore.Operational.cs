using System.Data;
using Npgsql;
using OrcaCore.Abstractions.Diagnostics;
using OrcaCore.Abstractions.Providers;

namespace OrcaCore.Providers.PostgreSql;

internal sealed partial class PostgreSqlWorkflowStore
{
    public async Task<WorkflowOperatorStatistics> GetOperatorStatisticsAsync(
        CancellationToken cancellationToken)
    {
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        await using var transaction = await connection
            .BeginTransactionAsync(IsolationLevel.RepeatableRead, cancellationToken)
            .ConfigureAwait(false);

        var groups = new List<WorkflowOperatorStatisticsGroup>();
        await using (var groupCommand = new NpgsqlCommand(
            """
            select definition_id, definition_version, status, count(*)
            from orcacore_instance_projections
            group by definition_id, definition_version, status
            order by definition_id, definition_version, status;
            """,
            connection,
            transaction))
        await using (var reader = await groupCommand.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false))
        {
            while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            {
                groups.Add(new WorkflowOperatorStatisticsGroup(
                    global::OrcaCore.DefinitionId.Parse(reader.GetGuid(0).ToString()),
                    new global::OrcaCore.DefinitionVersion(reader.GetInt32(1)),
                    Enum.Parse<global::OrcaCore.WorkflowInstanceStatus>(reader.GetString(2)),
                    reader.GetInt64(3)));
            }
        }

        var stuckGroups = new List<WorkflowOperatorStuckGroup>();
        await using (var stuckCommand = new NpgsqlCommand(
            """
            select definition_id, count(*)
            from orcacore_instance_projections
            where is_stuck or has_stuck_step
            group by definition_id
            order by definition_id;
            """,
            connection,
            transaction))
        await using (var reader = await stuckCommand.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false))
        {
            while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            {
                stuckGroups.Add(new WorkflowOperatorStuckGroup(
                    global::OrcaCore.DefinitionId.Parse(reader.GetGuid(0).ToString()),
                    reader.GetInt64(1)));
            }
        }

        var activeWaitGroups = new List<WorkflowOperatorActiveWaitGroup>();
        await using (var waitsCommand = new NpgsqlCommand(
            """
            select summary.definition_id, wait.event_name, count(*)
            from orcacore_active_wait_projections wait
            join orcacore_instance_projections summary on summary.instance_id = wait.instance_id
            group by summary.definition_id, wait.event_name
            order by summary.definition_id, wait.event_name;
            """,
            connection,
            transaction))
        await using (var reader = await waitsCommand.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false))
        {
            while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            {
                activeWaitGroups.Add(new WorkflowOperatorActiveWaitGroup(
                    global::OrcaCore.DefinitionId.Parse(reader.GetGuid(0).ToString()),
                    global::OrcaCore.EventName.Create(reader.GetString(1)),
                    reader.GetInt64(2)));
            }
        }

        await using var pressureCommand = new NpgsqlCommand(
            """
            select
                (select count(*) from orcacore_instance_projections
                    where status = any(@active_statuses)) as active_instances,
                (select count(*) from orcacore_instance_projections
                    where is_stuck or has_stuck_step) as stuck_instances,
                (select count(*) from orcacore_active_wait_projections) as active_waits,
                (select count(*) from orcacore_events) as stream_events,
                (select count(*) from orcacore_checkpoints) as checkpoints,
                (select coalesce(max(events.max_version - coalesce(checkpoints.stream_version, 0)), 0)
                    from (
                        select stream_id, max(version) as max_version
                        from orcacore_events
                        group by stream_id
                    ) events
                    left join orcacore_checkpoints checkpoints on checkpoints.instance_id = events.stream_id)
                    as checkpoint_lag,
                (select count(*) from orcacore_outbox where kind = @continue_kind and state = @pending)
                    as continuation_pending,
                (select count(*) from orcacore_outbox where kind = @continue_kind and state = @retryable)
                    as continuation_retryable,
                (select count(*) from orcacore_outbox where kind = @continue_kind and state = @claimed)
                    as continuation_claimed,
                (select count(*) from orcacore_outbox where kind = @continue_kind and state = @poisoned)
                    as continuation_poisoned,
                (select count(*) from orcacore_outbox where kind <> @continue_kind and state = @pending)
                    as external_pending,
                (select count(*) from orcacore_outbox where kind <> @continue_kind and state = @retryable)
                    as external_retryable,
                (select count(*) from orcacore_outbox where kind <> @continue_kind and state = @claimed)
                    as external_claimed,
                (select count(*) from orcacore_outbox where kind <> @continue_kind and state = @poisoned)
                    as external_poisoned;
            """,
            connection,
            transaction);
        pressureCommand.Parameters.AddWithValue(
            "active_statuses",
            new[]
            {
                global::OrcaCore.WorkflowInstanceStatus.Pending.ToString(),
                global::OrcaCore.WorkflowInstanceStatus.Running.ToString(),
                global::OrcaCore.WorkflowInstanceStatus.Waiting.ToString(),
                global::OrcaCore.WorkflowInstanceStatus.CancellationRequested.ToString()
            });
        pressureCommand.Parameters.AddWithValue("continue_kind", OutboxKinds.Continue);
        pressureCommand.Parameters.AddWithValue("pending", OutboxRecordState.Pending.ToString());
        pressureCommand.Parameters.AddWithValue("retryable", OutboxRecordState.Retryable.ToString());
        pressureCommand.Parameters.AddWithValue("claimed", OutboxRecordState.Claimed.ToString());
        pressureCommand.Parameters.AddWithValue("poisoned", OutboxRecordState.Poisoned.ToString());

        await using var pressureReader = await pressureCommand
            .ExecuteReaderAsync(cancellationToken)
            .ConfigureAwait(false);
        if (!await pressureReader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            throw new InvalidOperationException("The PostgreSQL operational projection query returned no row.");
        }

        var pressure = new WorkflowOperationalPressure
        {
            ActiveInstanceCount = pressureReader.GetInt64(0),
            StuckInstanceCount = pressureReader.GetInt64(1),
            ActiveWaitCount = pressureReader.GetInt64(2),
            StreamEventCount = pressureReader.GetInt64(3),
            CheckpointCount = pressureReader.GetInt64(4),
            CheckpointLag = pressureReader.GetInt64(5),
            ContinuationPendingCount = pressureReader.GetInt64(6),
            ContinuationRetryableCount = pressureReader.GetInt64(7),
            ContinuationClaimedCount = pressureReader.GetInt64(8),
            ContinuationPoisonedCount = pressureReader.GetInt64(9),
            ExternalOutboxPendingCount = pressureReader.GetInt64(10),
            ExternalOutboxRetryableCount = pressureReader.GetInt64(11),
            ExternalOutboxClaimedCount = pressureReader.GetInt64(12),
            ExternalOutboxPoisonedCount = pressureReader.GetInt64(13)
        };
        await pressureReader.DisposeAsync().ConfigureAwait(false);
        await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
        return new WorkflowOperatorStatistics
        {
            ProviderName = OrcaCoreDiagnostics.PostgreSqlProviderName,
            Groups = groups,
            StuckGroups = stuckGroups,
            ActiveWaitGroups = activeWaitGroups,
            Pressure = pressure
        };
    }
}
