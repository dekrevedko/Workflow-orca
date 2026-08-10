namespace OrcaCore.Providers.PostgreSql.Internal;

/// <summary>
/// Describes one provider-owned SQL migration.
/// </summary>
internal sealed record RelationalMigration(string MigrationId, string Sql);

/// <summary>
/// Describes dialect-specific SQL used to track applied migrations.
/// </summary>
internal sealed record RelationalMigrationJournal(
    string EnsureJournalSql,
    string SelectAppliedMigrationsSql,
    string InsertAppliedMigrationSql);
