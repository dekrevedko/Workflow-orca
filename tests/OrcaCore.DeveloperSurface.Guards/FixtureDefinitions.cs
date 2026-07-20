using System.Text.Json;

namespace OrcaCore.DeveloperSurface.Guards;

public sealed record ConsumerFixture(
    string Id,
    string Audience,
    string CompileProject,
    string[] CurrentProjectReferences,
    string[] FuturePackages,
    string[] Assertions);

public sealed record ExpectedRedScenario(string Id, string Contract, string FuturePhase);

internal static class FixtureDefinitions
{
    internal static string RepositoryRoot()
    {
        var current = new DirectoryInfo(AppContext.BaseDirectory);
        while (current is not null && !File.Exists(Path.Combine(current.FullName, "OrcaCore.slnx")))
        {
            current = current.Parent;
        }

        return current?.FullName ?? throw new InvalidOperationException("Could not locate repository root.");
    }

    internal static T Read<T>(string relativePath)
    {
        var path = Path.Combine(RepositoryRoot(), relativePath.Replace('/', Path.DirectorySeparatorChar));
        return JsonSerializer.Deserialize<T>(File.ReadAllText(path), new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        }) ?? throw new InvalidOperationException($"Fixture '{relativePath}' is empty.");
    }
}
