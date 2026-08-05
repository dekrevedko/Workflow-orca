using System.Data;
using System.Text.Json;
using Npgsql;
using NpgsqlTypes;
using OrcaCore.Abstractions.Durable;
using OrcaCore.Abstractions.Ids;
using OrcaCore.Abstractions.Primitives;
using OrcaCore.Abstractions.Providers;
using OrcaCore.Abstractions.Serialization;
using OrcaCore.Providers.Relational;

namespace OrcaCore.Providers.PostgreSql;

internal sealed class PostgreSqlTimerScheduler(NpgsqlDataSource dataSource)
{
    private static readonly TimeSpan DefaultLeaseDuration = TimeSpan.FromMinutes(5);

    public async Task ScheduleAsync(TimerScheduleRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        await using var command = new NpgsqlCommand(
            """
            insert into orcacore_timers (
                timer_id,
                instance_id,
                command_id,
                fire_at,
                wakeup_name,
                claimed,
                claimed_until)
            values (
                @timer_id,
                @instance_id,
                @command_id,
                @fire_at,
                @wakeup_name,
                false,
                null)
            on conflict (timer_id) do update set
                instance_id = excluded.instance_id,
                command_id = excluded.command_id,
                fire_at = excluded.fire_at,
                wakeup_name = excluded.wakeup_name,
                claimed = false,
                claimed_until = null;
            """,
            connection);
        command.Parameters.AddWithValue("timer_id", request.TimerId.Value);
        command.Parameters.AddWithValue("instance_id", request.InstanceId.Value);
        command.Parameters.AddWithValue("command_id", request.CommandId.Value);
        command.Parameters.AddWithValue("fire_at", request.FireAt);
        command.Parameters.AddWithValue("wakeup_name", request.WakeupName);

        await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<FireTimerCommand>> ClaimDueAsync(
        DateTimeOffset dueAtOrBefore,
        int maxCount,
        CancellationToken cancellationToken)
    {
        return await ClaimDueAsync(
            new TimerClaimRequest(dueAtOrBefore, maxCount, dueAtOrBefore, DefaultLeaseDuration),
            cancellationToken)
            .ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<FireTimerCommand>> ClaimDueAsync(
        TimerClaimRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentOutOfRangeException.ThrowIfNegative(request.MaxCount);
        ThrowIfInvalidLease(request.LeaseDuration);

        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        await using var transaction = await connection
            .BeginTransactionAsync(IsolationLevel.ReadCommitted, cancellationToken)
            .ConfigureAwait(false);
        await using var command = new NpgsqlCommand(
            """
            update orcacore_timers
            set
                claimed = true,
                claimed_until = @claimed_until
            where timer_id in (
                select timer_id
                from orcacore_timers
                where fire_at <= @due_at
                  and (claimed = false or claimed_until is null or claimed_until <= @claimed_at)
                order by fire_at, timer_id
                for update skip locked
                limit @max_count
            )
            returning timer_id, instance_id, command_id;
            """,
            connection,
            transaction);
        command.Parameters.AddWithValue("due_at", request.DueAtOrBefore);
        command.Parameters.AddWithValue("claimed_at", request.ClaimedAt);
        command.Parameters.AddWithValue("claimed_until", request.ClaimedAt.Add(request.LeaseDuration));
        command.Parameters.AddWithValue("max_count", request.MaxCount);

        var commands = new List<FireTimerCommand>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            commands.Add(new FireTimerCommand
            {
                TimerId = new TimerId(reader.GetGuid(0)),
                InstanceId = InstanceId.Parse(reader.GetGuid(1).ToString()),
                CommandId = new CommandId(reader.GetGuid(2)),
                RequestedAt = request.ClaimedAt
            });
        }

        await reader.DisposeAsync().ConfigureAwait(false);
        await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
        return commands;
    }

    /// <inheritdoc />
    public async Task CompleteAsync(TimerId timerId, CancellationToken cancellationToken)
    {
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        await using var command = new NpgsqlCommand(
            """
            delete from orcacore_timers
            where timer_id = @timer_id;
            """,
            connection);
        command.Parameters.AddWithValue("timer_id", timerId.Value);

        await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task ReleaseAsync(TimerId timerId, CancellationToken cancellationToken)
    {
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        await using var command = new NpgsqlCommand(
            """
            update orcacore_timers
            set
                claimed = false,
                claimed_until = null
            where timer_id = @timer_id;
            """,
            connection);
        command.Parameters.AddWithValue("timer_id", timerId.Value);

        await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />

    internal static async Task UpsertTimerSchedulesAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        IEnumerable<TimerScheduleRequest> requests,
        CancellationToken cancellationToken)
    {
        foreach (var request in requests)
        {
            await UpsertTimerScheduleAsync(connection, transaction, request, cancellationToken)
                .ConfigureAwait(false);
        }
    }

    private static async Task UpsertTimerScheduleAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        TimerScheduleRequest request,
        CancellationToken cancellationToken)
    {
        await using var command = new NpgsqlCommand(
            """
            insert into orcacore_timers (
                timer_id,
                instance_id,
                command_id,
                fire_at,
                wakeup_name,
                claimed,
                claimed_until)
            values (
                @timer_id,
                @instance_id,
                @command_id,
                @fire_at,
                @wakeup_name,
                false,
                null)
            on conflict (timer_id) do update set
                instance_id = excluded.instance_id,
                command_id = excluded.command_id,
                fire_at = excluded.fire_at,
                wakeup_name = excluded.wakeup_name,
                claimed = false,
                claimed_until = null;
            """,
            connection,
            transaction);
        command.Parameters.AddWithValue("timer_id", request.TimerId.Value);
        command.Parameters.AddWithValue("instance_id", request.InstanceId.Value);
        command.Parameters.AddWithValue("command_id", request.CommandId.Value);
        command.Parameters.AddWithValue("fire_at", request.FireAt);
        command.Parameters.AddWithValue("wakeup_name", request.WakeupName);

        await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }

    private static void ThrowIfInvalidLease(TimeSpan leaseDuration)
    {
        if (leaseDuration <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(leaseDuration), leaseDuration, "Lease duration must be positive.");
        }
    }
}
