namespace OrcaCore.Providers.SqlServer.Internal;

internal sealed record SqlServerMigration(string Id, string Sql, string ContentDigest);
