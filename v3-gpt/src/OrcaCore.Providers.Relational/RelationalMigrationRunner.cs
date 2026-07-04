using System.Data;
using System.Data.Common;
using Dapper;

namespace OrcaCore.Providers.Relational;

/// <summary>
/// Applies provider-owned SQL migrations with a small Dapper-based journal.
/// </summary>
public static class RelationalMigrationRunner
{
    public static async Task ApplyAsync(
        DbConnection connection,
        RelationalMigrationJournal journal,
        IReadOnlyList<RelationalMigration> migrations,
        CancellationToken cancellationToken,
        TimeProvider? timeProvider = null)
    {
        ArgumentNullException.ThrowIfNull(connection);
        ArgumentNullException.ThrowIfNull(journal);
        ArgumentNullException.ThrowIfNull(migrations);
        var clock = timeProvider ?? TimeProvider.System;

        if (connection.State != ConnectionState.Open)
        {
            await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
        }

        await connection.ExecuteAsync(new CommandDefinition(
            journal.EnsureJournalSql,
            cancellationToken: cancellationToken)).ConfigureAwait(false);

        var applied = (await connection.QueryAsync<string>(new CommandDefinition(
                journal.SelectAppliedMigrationsSql,
                cancellationToken: cancellationToken)).ConfigureAwait(false))
            .ToHashSet(StringComparer.Ordinal);

        foreach (var migration in migrations.Where(migration => !applied.Contains(migration.MigrationId)))
        {
            await using var transaction = await connection
                .BeginTransactionAsync(cancellationToken)
                .ConfigureAwait(false);
            await connection.ExecuteAsync(new CommandDefinition(
                migration.Sql,
                transaction: transaction,
                cancellationToken: cancellationToken)).ConfigureAwait(false);
            await connection.ExecuteAsync(new CommandDefinition(
                journal.InsertAppliedMigrationSql,
                new
                {
                    migration.MigrationId,
                    AppliedAt = clock.GetUtcNow()
                },
                transaction,
                cancellationToken: cancellationToken)).ConfigureAwait(false);
            await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
        }
    }
}
