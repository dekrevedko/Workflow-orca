using System.Reflection;
using System.Security.Cryptography;
using System.Text;

namespace OrcaCore.Providers.SqlServer.Internal;

internal static class EmbeddedSqlServerMigrations
{
    internal static IReadOnlyList<SqlServerMigration> Load()
    {
        var assembly = typeof(EmbeddedSqlServerMigrations).Assembly;
        return assembly.GetManifestResourceNames()
            .Where(name => name.Contains(".Migrations.", StringComparison.Ordinal) &&
                name.EndsWith(".sql", StringComparison.Ordinal))
            .OrderBy(name => name, StringComparer.Ordinal)
            .Select(name => Load(assembly, name))
            .ToArray();
    }

    private static SqlServerMigration Load(Assembly assembly, string resourceName)
    {
        using var stream = assembly.GetManifestResourceStream(resourceName) ??
            throw new InvalidOperationException($"Embedded migration '{resourceName}' was not found.");
        using var reader = new StreamReader(stream, Encoding.UTF8, detectEncodingFromByteOrderMarks: true);
        var sql = reader.ReadToEnd().Replace("\r\n", "\n", StringComparison.Ordinal);
        var id = resourceName[(resourceName.LastIndexOf(".Migrations.", StringComparison.Ordinal) + 12)..^4];
        var digest = Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(sql)));
        return new SqlServerMigration(id, sql, digest);
    }
}
