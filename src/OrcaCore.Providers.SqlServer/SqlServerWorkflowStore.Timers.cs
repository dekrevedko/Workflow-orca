using System.Data;
using System.Text.Json;
using Microsoft.Data.SqlClient;
using OrcaCore.Abstractions.Durable;
using OrcaCore.Abstractions.Errors;
using OrcaCore.Abstractions.Ids;
using OrcaCore.Abstractions.Instances;
using OrcaCore.Abstractions.Primitives;
using OrcaCore.Abstractions.Providers;
using OrcaCore.Abstractions.Serialization;
using OrcaCore.Providers.Relational;

namespace OrcaCore.Providers.SqlServer;

internal sealed class SqlServerTimerScheduler(string connectionString)
{
    private static readonly TimeSpan DefaultLeaseDuration = TimeSpan.FromMinutes(5);

    public async Task ScheduleAsync(TimerScheduleRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        await using var connection = await OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        await using var transaction = (SqlTransaction)await connection
            .BeginTransactionAsync(IsolationLevel.ReadCommitted, cancellationToken)
            .ConfigureAwait(false);
        await UpsertTimerScheduleAsync(connection, transaction, request, cancellationToken).ConfigureAwait(false);
        await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
    }

    public Task<IReadOnlyList<FireTimerCommand>> ClaimDueAsync(
        DateTimeOffset dueAtOrBefore,
        int maxCount,
        CancellationToken cancellationToken)
    {
        return ClaimDueAsync(
            new TimerClaimRequest(dueAtOrBefore, maxCount, dueAtOrBefore, DefaultLeaseDuration),
            cancellationToken);
    }

    public async Task<IReadOnlyList<FireTimerCommand>> ClaimDueAsync(
        TimerClaimRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentOutOfRangeException.ThrowIfNegative(request.MaxCount);
        ThrowIfInvalidLease(request.LeaseDuration);
        await using var connection = await OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        await using var transaction = (SqlTransaction)await connection
            .BeginTransactionAsync(IsolationLevel.ReadCommitted, cancellationToken)
            .ConfigureAwait(false);
        await using var command = new SqlCommand(
            """
            ;with due as (
                select top (@max_count) timer_id
                from dbo.orcacore_timers with (updlock, readpast, rowlock)
                where fire_at <= @due_at
                  and (claimed = 0 or claimed_until is null or claimed_until <= @claimed_at)
                order by fire_at, timer_id
            )
            update dbo.orcacore_timers
            set
                claimed = 1,
                claimed_until = @claimed_until
            output inserted.timer_id, inserted.instance_id, inserted.command_id
            where timer_id in (select timer_id from due);
            """,
            connection,
            transaction);
        command.Parameters.AddWithValue("@max_count", request.MaxCount);
        command.Parameters.AddWithValue("@due_at", request.DueAtOrBefore);
        command.Parameters.AddWithValue("@claimed_at", request.ClaimedAt);
        command.Parameters.AddWithValue("@claimed_until", request.ClaimedAt.Add(request.LeaseDuration));

        var commands = new List<FireTimerCommand>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            commands.Add(new FireTimerCommand
            {
                TimerId = new TimerId(reader.GetGuid(0)),
                InstanceId = new InstanceId(reader.GetGuid(1)),
                CommandId = new CommandId(reader.GetGuid(2)),
                RequestedAt = request.ClaimedAt
            });
        }

        await reader.DisposeAsync().ConfigureAwait(false);
        await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
        return commands;
    }

    public async Task CompleteAsync(TimerId timerId, CancellationToken cancellationToken)
    {
        await using var connection = await OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        await using var command = new SqlCommand(
            "delete from dbo.orcacore_timers where timer_id = @timer_id;",
            connection);
        command.Parameters.AddWithValue("@timer_id", timerId.Value);
        await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task ReleaseAsync(TimerId timerId, CancellationToken cancellationToken)
    {
        await using var connection = await OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        await using var command = new SqlCommand(
            """
            update dbo.orcacore_timers
            set
                claimed = 0,
                claimed_until = null
            where timer_id = @timer_id;
            """,
            connection);
        command.Parameters.AddWithValue("@timer_id", timerId.Value);
        await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }

    internal static async Task UpsertTimerSchedulesAsync(
        SqlConnection connection,
        SqlTransaction transaction,
        IEnumerable<TimerScheduleRequest> requests,
        CancellationToken cancellationToken)
    {
        foreach (var request in requests)
        {
            await UpsertTimerScheduleAsync(connection, transaction, request, cancellationToken).ConfigureAwait(false);
        }
    }

    private static async Task UpsertTimerScheduleAsync(
        SqlConnection connection,
        SqlTransaction transaction,
        TimerScheduleRequest request,
        CancellationToken cancellationToken)
    {
        await using var command = new SqlCommand(
            """
            update dbo.orcacore_timers
            set instance_id = @instance_id,
                command_id = @command_id,
                fire_at = @fire_at,
                wakeup_name = @wakeup_name,
                claimed = 0,
                claimed_until = null
            where timer_id = @timer_id;
            if @@rowcount = 0
            begin
                insert into dbo.orcacore_timers (
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
                    0,
                    null);
            end;
            """,
            connection,
            transaction);
        command.Parameters.AddWithValue("@timer_id", request.TimerId.Value);
        command.Parameters.AddWithValue("@instance_id", request.InstanceId.Value);
        command.Parameters.AddWithValue("@command_id", request.CommandId.Value);
        command.Parameters.AddWithValue("@fire_at", request.FireAt);
        command.Parameters.AddWithValue("@wakeup_name", request.WakeupName);
        await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }

    private async Task<SqlConnection> OpenConnectionAsync(CancellationToken cancellationToken)
    {
        var connection = new SqlConnection(connectionString);
        await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
        return connection;
    }

    private static void ThrowIfInvalidLease(TimeSpan leaseDuration)
    {
        if (leaseDuration <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(leaseDuration), leaseDuration, "Lease duration must be positive.");
        }
    }
}
