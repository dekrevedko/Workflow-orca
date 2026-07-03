namespace OrcaCore.Providers.Relational;

/// <summary>
/// Describes one provider-owned SQL migration.
/// </summary>
public sealed record RelationalMigration(string MigrationId, string Sql);

/// <summary>
/// Describes dialect-specific SQL used to track applied migrations.
/// </summary>
public sealed record RelationalMigrationJournal(
    string EnsureJournalSql,
    string SelectAppliedMigrationsSql,
    string InsertAppliedMigrationSql);
