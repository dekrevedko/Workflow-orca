using System.Security.Cryptography;
using System.Text;

namespace OrcaCore.Providers.PostgreSql.Internal;

/// <summary>
/// Describes one provider-owned SQL migration.
/// </summary>
internal sealed record RelationalMigration
{
    public RelationalMigration(string migrationId, string sql)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(migrationId);
        ArgumentNullException.ThrowIfNull(sql);
        MigrationId = migrationId;
        Sql = sql;
        ContentHash = ComputeContentHash(sql);
    }

    public string MigrationId { get; }

    public string Sql { get; }

    public string ContentHash { get; }

    private static string ComputeContentHash(string sql)
    {
        var canonicalSql = sql
            .Replace("\r\n", "\n", StringComparison.Ordinal)
            .Replace('\r', '\n');
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(canonicalSql))).ToLowerInvariant();
    }
}

/// <summary>Identifies the canonical content of one applied provider migration.</summary>
internal sealed record AppliedRelationalMigration(string MigrationId, string ContentHash);

/// <summary>
/// Describes dialect-specific SQL used to track applied migrations.
/// </summary>
internal sealed record RelationalMigrationJournal(
    string EnsureJournalSql,
    string SelectAppliedMigrationsSql,
    string InsertAppliedMigrationSql);
