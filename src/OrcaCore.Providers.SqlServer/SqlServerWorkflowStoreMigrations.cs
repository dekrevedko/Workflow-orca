using System.Reflection;
using OrcaCore.Providers.Relational;

namespace OrcaCore.Providers.SqlServer;

internal static class SqlServerWorkflowStoreMigrations
{
    public static RelationalMigrationJournal Journal { get; } = new(
        """
        if object_id('dbo.orcacore_schema_migrations', 'U') is null
        begin
            create table dbo.orcacore_schema_migrations (
                migration_id nvarchar(200) not null primary key,
                applied_at datetimeoffset not null
            );
        end;
        """,
        "select migration_id from dbo.orcacore_schema_migrations order by migration_id;",
        """
        insert into dbo.orcacore_schema_migrations (migration_id, applied_at)
        values (@MigrationId, @AppliedAt);
        """);

    public static IReadOnlyList<RelationalMigration> All { get; } =
        EmbeddedSqlMigrations.Load(
            Assembly.GetExecutingAssembly(),
            "OrcaCore.Providers.SqlServer.Migrations.");
}
