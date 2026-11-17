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
    private const string RecordSha256 = "003dfd97665358213a6d6dd517f9cd271f62eea617f95642c80fec3ebc961bb6";

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

    internal const string IntegrationTip = "ba2b057d7898f516845e90323b4288fb0c1744d8";

    internal static string IntegrationAncestry(string root)
    {
        var mergePath = Git(root, "rev-parse", "--path-format=absolute", "--git-path", "MERGE_HEAD").Trim();
        if (File.Exists(mergePath))
        {
            File.ReadAllText(mergePath).Trim().Should().Be(IntegrationTip,
                "only the named mainline parent may supply pending-merge ancestry");
            return IntegrationTip;
        }
        _ = Git(root, "merge-base", "--is-ancestor", IntegrationTip, "HEAD");
        return "HEAD";
    }

    private static void VerifyImplementationReview(string root)
    {
        const string checkpoint = "b9dcdef7e611da45d7dc4f75946ee192fc83c37e";
        const string evidence = "598c6d4fb61f7883e25f79f03001aa620dda0bf7";
        const string verdict = "docs/review/developer-facing-interface-section-08-task-8-6-dag-public-handle-seam-implementation-independent-review-verdict-2026-11-16.md";
        Hash(File.ReadAllBytes(Path.Combine(root,
            "openspec/changes/admit-dag-public-handle-seam/artifacts/implementation-review-record-2026-11-16.json")))
            .Should().Be("8c70ceb10212907da0f08fe106b0075d5acfc1e477fdc38fd1de1635d23ff2c5");
        Git(root, "rev-parse", checkpoint + "^{tree}").Trim().Should().Be("8ef5842bb8c7952f30f0f4184e97baf45ba986c0");
        Git(root, "rev-list", "--parents", "-n", "1", checkpoint).Trim().Should().Be(checkpoint + " " + Parent);
        Git(root, "rev-list", "--parents", "-n", "1", evidence).Trim().Should().Be(evidence + " " + checkpoint);
        _ = Git(root, "merge-base", "--is-ancestor", evidence, "HEAD");
        Git(root, "diff-tree", "--no-commit-id", "--name-only", "-r", evidence)
            .Split('\n', StringSplitOptions.RemoveEmptyEntries).Should().Equal(verdict,
                "tests/OrcaCore.DeveloperSurface.Guards/Fixtures/immutable-document-history.json",
                "tests/OrcaCore.DeveloperSurface.Guards/Fixtures/review-manifest-provenance.json");
        Hash(File.ReadAllBytes(Path.Combine(root, verdict))).Should().Be("c64747716a1ddb35757919525cefb5aee509cde948c41d63c1f7fea899884600");
        Normalize(File.ReadAllText(Path.Combine(root, verdict))).Should().Be(Git(root, "show", evidence + ":" + verdict));
    }

    internal static string[] IntegratedSourcePaths(string root)
    {
        _ = IntegrationAncestry(root);
        var paths = Git(root, "diff", "--name-only", Parent, IntegrationTip, "--", "src")
            .Split('\n', StringSplitOptions.RemoveEmptyEntries);
        paths.Should().HaveCount(9);
        paths.Should().OnlyContain(path => path.StartsWith("src/OrcaCore.Engine.Ephemeral/", StringComparison.Ordinal));
        foreach (var path in paths)
            Normalize(File.ReadAllText(Path.Combine(root, path))).Should().Be(Git(root, "show", IntegrationTip + ":" + path),
                "only the exact integrated PR #23/#25 source is excluded from the DAG candidate");
        return paths;
    }

    internal static string IntegratedDocumentBeforeFacade(string root, string path, string current)
    {
        var row = Read(root).Documents.Single(item => item.Path == path);
        VerifyCurrent(root, row);
        current.Should().Be(Normalize(File.ReadAllText(Path.Combine(root, path))));
        return Git(root, "show", IntegrationTip + ":" + path);
    }

    internal static string[] SourcePaths(string root)
    {
        VerifyImplementationReview(root);
        var rows = Read(root).Sources;
        var actual = Git(root, "diff", "--name-only", Parent, "--", "src").Split('\n', StringSplitOptions.RemoveEmptyEntries)
            .Concat(Git(root, "ls-files", "--others", "--exclude-standard", "--", "src").Split('\n', StringSplitOptions.RemoveEmptyEntries))
            .Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToArray();
        rows.Select(row => row.Path).Should().Equal(actual.Except(IntegratedSourcePaths(root), StringComparer.Ordinal), "the owner-authorized facade candidate has an exact source scope");
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
        var priorHash = Hash(Encoding.UTF8.GetBytes(Normalize(Encoding.UTF8.GetString(PriorBytes(root, row)))));
        if (priorHash != historicalHash)
        {
            _ = IntegrationAncestry(root);
            Hash(Encoding.UTF8.GetBytes(Git(root, "show", IntegrationTip + ":" + path))).Should().Be(historicalHash,
                "a mainline documentation successor must match the exact integrated parent");
        }
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
