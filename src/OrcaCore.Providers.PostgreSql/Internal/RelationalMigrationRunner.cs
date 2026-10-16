using System.Data;
using System.Data.Common;
using Dapper;

namespace OrcaCore.Providers.PostgreSql.Internal;

/// <summary>
/// Applies provider-owned SQL migrations with a small Dapper-based journal.
/// </summary>
internal static class RelationalMigrationRunner
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

        var applied = (await connection.QueryAsync<AppliedRelationalMigration>(new CommandDefinition(
                journal.SelectAppliedMigrationsSql,
                cancellationToken: cancellationToken)).ConfigureAwait(false))
            .ToDictionary(migration => migration.MigrationId, migration => migration.ContentHash, StringComparer.Ordinal);

        foreach (var migration in migrations)
        {
            if (applied.TryGetValue(migration.MigrationId, out var appliedContentHash))
            {
                if (!string.Equals(appliedContentHash, migration.ContentHash, StringComparison.Ordinal))
                {
                    throw new InvalidOperationException(
                        $"Applied migration '{migration.MigrationId}' content has changed; reset the unreleased provider database or add a new migration id.");
                }

                continue;
            }

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
                    migration.ContentHash,
                    AppliedAt = clock.GetUtcNow()
                },
                transaction,
                cancellationToken: cancellationToken)).ConfigureAwait(false);
            await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
        }
    }
}
