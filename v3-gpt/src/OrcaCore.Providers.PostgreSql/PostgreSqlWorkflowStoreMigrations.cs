using System.Reflection;
using OrcaCore.Providers.Relational;

namespace OrcaCore.Providers.PostgreSql;

internal static class PostgreSqlWorkflowStoreMigrations
{
    public static RelationalMigrationJournal Journal { get; } = new(
        """
        create table if not exists orcacore_schema_migrations (
            migration_id text primary key,
            applied_at timestamp with time zone not null
        );
        """,
        "select migration_id from orcacore_schema_migrations order by migration_id;",
        """
        insert into orcacore_schema_migrations (migration_id, applied_at)
        values (@MigrationId, @AppliedAt);
        """);

    public static IReadOnlyList<RelationalMigration> All { get; } =
        EmbeddedSqlMigrations.Load(
            Assembly.GetExecutingAssembly(),
            "OrcaCore.Providers.PostgreSql.Migrations.");
}
