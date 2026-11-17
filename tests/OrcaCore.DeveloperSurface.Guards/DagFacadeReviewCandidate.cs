using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using AwesomeAssertions;

namespace OrcaCore.DeveloperSurface.Guards;

// A bounded owner-authorized candidate, not inherited independent approval. Historical
// guards see their original bytes only after verifying the exact current source record.
internal static class DagFacadeReviewCandidate
{
    internal const string Parent = "da1a4729ea2cdb9474eb219eb507760574f83dcb";
    private const string RecordPath = "tests/OrcaCore.DeveloperSurface.Guards/Fixtures/dag-task-8-6-review-candidate.json";
    private const string RecordSha256 = "db2b6805233c0ee65c156ab7583a17c6ee8a0f2ab44ae063896be3089a783d12";

    private sealed record FileRow(string Path, string? BeforeSha256, string AfterSha256);
    private sealed record Candidate(string Stage, string Parent, string OwnerDirectionPath, string OwnerDirectionSha256,
        FileRow[] Sources, FileRow[] Documents, FileRow[] PublicApi);

    private static Candidate Read(string root)
    {
        var bytes = File.ReadAllBytes(Path.Combine(root, RecordPath));
        Hash(bytes).Should().Be(RecordSha256);
        var record = JsonSerializer.Deserialize<Candidate>(bytes, new JsonSerializerOptions { PropertyNameCaseInsensitive = true })!;
        record.Stage.Should().Be("OwnerAuthorizedReviewCandidate");
        record.Parent.Should().Be(Parent);
        Hash(File.ReadAllBytes(Path.Combine(root, record.OwnerDirectionPath))).Should().Be(record.OwnerDirectionSha256);
        return record;
    }

    internal static string[] SourcePaths(string root)
    {
        var rows = Read(root).Sources;
        var actual = Git(root, "diff", "--name-only", Parent, "--", "src").Split('\n', StringSplitOptions.RemoveEmptyEntries)
            .Concat(Git(root, "ls-files", "--others", "--exclude-standard", "--", "src").Split('\n', StringSplitOptions.RemoveEmptyEntries))
            .Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToArray();
        rows.Select(row => row.Path).Should().Equal(actual, "the owner-authorized facade candidate has an exact source scope");
        foreach (var row in rows) VerifyCurrent(root, row);
        return actual;
    }

    internal static string[] PublicApiPaths(string root)
    {
        var rows = Read(root).PublicApi;
        rows.Select(row => row.Path).Should().Equal(
            "tests/OrcaCore.DeveloperSurface.Guards/Fixtures/PublicApi/v1/OrcaCore.Dag.api.txt");
        foreach (var row in rows) { VerifyCurrent(root, row); _ = PriorBytes(root, row); }
        return rows.Select(row => row.Path).ToArray();
    }

    internal static byte[] PriorSourceBytes(string root, string path)
    {
        var row = Read(root).Sources.SingleOrDefault(item => item.Path == path);
        if (row is null) return File.ReadAllBytes(Path.Combine(root, path));
        VerifyCurrent(root, row);
        row.BeforeSha256.Should().NotBeNull("only existing source can be projected into a historical source record");
        return PriorBytes(root, row);
    }

    internal static string PriorDocument(string root, string path)
    {
        var row = Read(root).Documents.SingleOrDefault(item => item.Path == path);
        if (row is null) return Normalize(File.ReadAllText(Path.Combine(root, path)));
        VerifyCurrent(root, row);
        return Normalize(Encoding.UTF8.GetString(PriorBytes(root, row)));
    }

    internal static string CurrentDocumentationHash(string root, string path, string historicalHash)
    {
        var row = Read(root).Documents.SingleOrDefault(item => item.Path == path);
        if (row is null) return historicalHash;
        VerifyCurrent(root, row);
        Hash(Encoding.UTF8.GetBytes(Normalize(Encoding.UTF8.GetString(PriorBytes(root, row))))).Should().Be(historicalHash,
            "the candidate must extend the exact prior current documentation, not bypass its pin");
        return Hash(Encoding.UTF8.GetBytes(Normalize(File.ReadAllText(Path.Combine(root, path)))));
    }

    private static void VerifyCurrent(string root, FileRow row) =>
        Hash(File.ReadAllBytes(Path.Combine(root, row.Path))).Should().Be(row.AfterSha256, "the facade candidate pins {0}", row.Path);

    private static byte[] PriorBytes(string root, FileRow row)
    {
        var start = Start(root, "show", Parent + ":" + row.Path);
        using var process = Process.Start(start)!;
        using var bytes = new MemoryStream();
        process.StandardOutput.BaseStream.CopyTo(bytes);
        var error = process.StandardError.ReadToEnd(); process.WaitForExit();
        process.ExitCode.Should().Be(0, error);
        var result = bytes.ToArray(); Hash(result).Should().Be(row.BeforeSha256);
        return result;
    }

    private static string Git(string root, params string[] args)
    {
        using var process = Process.Start(Start(root, args))!;
        var result = process.StandardOutput.ReadToEnd(); var error = process.StandardError.ReadToEnd(); process.WaitForExit();
        process.ExitCode.Should().Be(0, error); return Normalize(result);
    }
    private static ProcessStartInfo Start(string root, params string[] args)
    {
        var start = new ProcessStartInfo("git") { WorkingDirectory = root, UseShellExecute = false,
            RedirectStandardOutput = true, RedirectStandardError = true, CreateNoWindow = true };
        foreach (var arg in args) start.ArgumentList.Add(arg); return start;
    }
    private static string Normalize(string text) => text.Replace("\r\n", "\n", StringComparison.Ordinal).Replace('\r', '\n');
    private static string Hash(byte[] bytes) => Convert.ToHexStringLower(SHA256.HashData(bytes));
}
