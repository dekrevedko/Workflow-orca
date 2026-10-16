using System.Data;
using Microsoft.Data.SqlClient;

namespace OrcaCore.Providers.SqlServer.Internal;

internal sealed class SqlServerSchema(string connectionString, string schema)
{
    private const int ApplicationLockTimeoutMilliseconds = 60_000;
    private const int ApplicationLockResourceMaxLength = 255;
    private const int MigrationIdentifierMaxLength = 200;
    private const int MigrationCommandTimeoutSeconds = 120;
    private const int SqlServerIdentifierMaxLength = 128;
    private const string MigrationLockPrefix = "orcacore:migrations:";
    private const string MigrationJournalTable = "orcacore_migrations";
    private readonly string quotedSchema = QuoteSchema(schema);
    private readonly string schemaLiteral = schema.Replace("'", "''", StringComparison.Ordinal);

    internal async Task InitializeAsync(CancellationToken cancellationToken)
    {
        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
        await using var transaction = (SqlTransaction)await connection
            .BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken)
            .ConfigureAwait(false);

        await AcquireTransactionLockAsync(
            connection,
            transaction,
            MigrationLockPrefix + schema,
            cancellationToken).ConfigureAwait(false);
        await ExecuteAsync(
            connection,
            transaction,
            $"if schema_id(N'{schemaLiteral}') is null exec(N'create schema {quotedSchema}');",
            cancellationToken).ConfigureAwait(false);
        await ExecuteAsync(
            connection,
            transaction,
            $"""
            if object_id(N'{quotedSchema}.[{MigrationJournalTable}]', N'U') is null
            begin
                create table {quotedSchema}.[{MigrationJournalTable}]
                (
                    [migration_id] nvarchar(200) not null,
                    [content_digest] char(64) not null,
                    [applied_at] datetimeoffset(7) not null,
                    constraint [pk_orcacore_migrations] primary key ([migration_id])
                );
            end;
            """,
            cancellationToken).ConfigureAwait(false);

        foreach (var migration in EmbeddedSqlServerMigrations.Load())
        {
            var existingDigest = await ReadDigestAsync(
                connection,
                transaction,
                migration.Id,
                cancellationToken).ConfigureAwait(false);
            if (existingDigest is not null)
            {
                if (!string.Equals(existingDigest, migration.ContentDigest, StringComparison.Ordinal))
                {
                    throw new InvalidOperationException(
                        $"Applied SQL Server migration '{migration.Id}' has a different content digest.");
                }

                continue;
            }

            var sql = migration.Sql.Replace("{{schema}}", quotedSchema, StringComparison.Ordinal);
            await ExecuteAsync(connection, transaction, sql, cancellationToken).ConfigureAwait(false);
            await InsertDigestAsync(
                connection,
                transaction,
                migration,
                cancellationToken).ConfigureAwait(false);
        }

        await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
    }

    internal static async Task AcquireTransactionLockAsync(
        SqlConnection connection,
        SqlTransaction transaction,
        string resource,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            declare @result int;
            exec @result = sys.sp_getapplock
                @Resource = @resource,
                @LockMode = N'Exclusive',
                @LockOwner = N'Transaction',
                @LockTimeout = @lockTimeout;
            select @result;
            """;
        command.Parameters.Add(new SqlParameter("@resource", SqlDbType.NVarChar, ApplicationLockResourceMaxLength) { Value = resource });
        command.Parameters.Add(new SqlParameter("@lockTimeout", SqlDbType.Int) { Value = ApplicationLockTimeoutMilliseconds });
        var result = Convert.ToInt32(
            await command.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false),
            System.Globalization.CultureInfo.InvariantCulture);
        if (result < 0)
        {
            throw new InvalidOperationException(
                $"SQL Server application lock '{resource}' could not be acquired (result {result}).");
        }
    }

    internal static string QuoteSchema(string schema)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(schema);
        if (schema.Length > SqlServerIdentifierMaxLength)
        {
            throw new ArgumentOutOfRangeException(
                nameof(schema),
                schema,
                $"A SQL Server schema name cannot exceed {SqlServerIdentifierMaxLength} characters.");
        }

        using var builder = new SqlCommandBuilder();
        return builder.QuoteIdentifier(schema);
    }

    private async Task<string?> ReadDigestAsync(
        SqlConnection connection,
        SqlTransaction transaction,
        string migrationId,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = $"select [content_digest] from {quotedSchema}.[{MigrationJournalTable}] with (updlock, holdlock) where [migration_id] = @id;";
        command.Parameters.Add(new SqlParameter("@id", SqlDbType.NVarChar, MigrationIdentifierMaxLength) { Value = migrationId });
        return await command.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false) as string;
    }

    private async Task InsertDigestAsync(
        SqlConnection connection,
        SqlTransaction transaction,
        SqlServerMigration migration,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = $"insert into {quotedSchema}.[{MigrationJournalTable}] ([migration_id], [content_digest], [applied_at]) values (@id, @digest, sysdatetimeoffset());";
        command.Parameters.Add(new SqlParameter("@id", SqlDbType.NVarChar, MigrationIdentifierMaxLength) { Value = migration.Id });
        command.Parameters.Add(new SqlParameter("@digest", SqlDbType.Char, 64) { Value = migration.ContentDigest });
        _ = await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }

    private static async Task ExecuteAsync(
        SqlConnection connection,
        SqlTransaction transaction,
        string sql,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = sql;
        command.CommandTimeout = MigrationCommandTimeoutSeconds;
        _ = await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }
}
