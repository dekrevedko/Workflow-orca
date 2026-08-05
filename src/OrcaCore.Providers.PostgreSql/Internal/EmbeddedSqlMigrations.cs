using System.Reflection;

namespace OrcaCore.Providers.Relational;

/// <summary>
/// Loads SQL migrations embedded in a provider assembly.
/// </summary>
internal static class EmbeddedSqlMigrations
{
    public static IReadOnlyList<RelationalMigration> Load(Assembly assembly, string resourcePrefix)
    {
        ArgumentNullException.ThrowIfNull(assembly);
        ArgumentException.ThrowIfNullOrWhiteSpace(resourcePrefix);

        return assembly
            .GetManifestResourceNames()
            .Where(name => name.StartsWith(resourcePrefix, StringComparison.Ordinal) &&
                name.EndsWith(".sql", StringComparison.Ordinal))
            .Order(StringComparer.Ordinal)
            .Select(name => new RelationalMigration(MigrationId(name, resourcePrefix), ReadResource(assembly, name)))
            .ToArray();
    }

    private static string MigrationId(string resourceName, string resourcePrefix)
    {
        return resourceName[resourcePrefix.Length..^".sql".Length];
    }

    private static string ReadResource(Assembly assembly, string resourceName)
    {
        using var stream = assembly.GetManifestResourceStream(resourceName)
            ?? throw new InvalidOperationException($"Embedded SQL migration '{resourceName}' was not found.");
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }
}
