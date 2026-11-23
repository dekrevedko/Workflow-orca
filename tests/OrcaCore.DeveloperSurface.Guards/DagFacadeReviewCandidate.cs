using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using AwesomeAssertions;

namespace OrcaCore.DeveloperSurface.Guards;

// Immutable approval history, not a pin on the current product or documentation.
// Current behavior/API/canonical guards remain responsible for the live tree.
internal static class DagFacadeApprovedHistory
{
    internal const string Parent = "da1a4729ea2cdb9474eb219eb507760574f83dcb";
    internal const string Implementation = "b9dcdef7e611da45d7dc4f75946ee192fc83c37e";
    internal const string ImplementationEvidence = "598c6d4fb61f7883e25f79f03001aa620dda0bf7";
    internal const string IntegrationTip = "ba2b057d7898f516845e90323b4288fb0c1744d8";
    internal const string Merge = "1e960fc4bf67ad6d1a8c053602d4a58964b47d79";
    internal const string MergeEvidence = "94e9aca5632bd77d0c6ecde9d7fe689bcdee18d1";
    private const string RecordPath = "tests/OrcaCore.DeveloperSurface.Guards/Fixtures/dag-task-8-6-review-candidate.json";
    private const string RecordSha256 = "003dfd97665358213a6d6dd517f9cd271f62eea617f95642c80fec3ebc961bb6";
    private sealed record FileRow(string Path, string? BeforeSha256, string AfterSha256);
    private sealed record Candidate(string Stage, string Parent, string OwnerDirectionPath, string OwnerDirectionSha256,
        FileRow[] Sources, FileRow[] Documents, FileRow[] PublicApi);

    private static Candidate Read(string root)
    {
        var bytes = Blob(root, Merge, RecordPath);
        Hash(bytes).Should().Be(RecordSha256);
        File.ReadAllBytes(Path.Combine(root, RecordPath)).Should().Equal(bytes, "the retired draft record remains immutable history");
        var record = JsonSerializer.Deserialize<Candidate>(bytes, new JsonSerializerOptions { PropertyNameCaseInsensitive = true })!;
        record.Stage.Should().Be("OwnerAuthorizedReviewCandidate");
        record.Parent.Should().Be(Parent);
        Hash(Blob(root, Merge, record.OwnerDirectionPath)).Should().Be(record.OwnerDirectionSha256);
        return record;
    }

    internal static void ValidateReviewedChains(string root)
    {
        Git(root, "rev-list", "--parents", "-n", "1", Implementation).Trim().Should().Be(Implementation + " " + Parent);
        Git(root, "rev-parse", Implementation + "^{tree}").Trim().Should().Be("8ef5842bb8c7952f30f0f4184e97baf45ba986c0");
        Git(root, "rev-list", "--parents", "-n", "1", Merge).Trim().Should().Be(Merge + " " + ImplementationEvidence + " " + IntegrationTip);
        Git(root, "rev-parse", Merge + "^{tree}").Trim().Should().Be("ff4711dca44f7a56abd53bf0854c10332831e814");
        Evidence(Implementation, ImplementationEvidence, "implementation", "c64747716a1ddb35757919525cefb5aee509cde948c41d63c1f7fea899884600");
        Evidence(Merge, MergeEvidence, "integration-remediation", "e9c5feddb651cbb570d289027b6c43b27eebc08b92dec2cd907a784468366074");
        _ = Git(root, "merge-base", "--is-ancestor", MergeEvidence, "HEAD");
        void Evidence(string checkpoint, string evidence, string kind, string sha)
        {
            var verdict = "docs/review/developer-facing-interface-section-08-task-8-6-dag-public-handle-seam-" + kind + "-independent-review-verdict-2026-11-16.md";
            Git(root, "rev-list", "--parents", "-n", "1", evidence).Trim().Should().Be(evidence + " " + checkpoint);
            Git(root, "diff-tree", "--no-commit-id", "--name-only", "-r", evidence).Split('\n', StringSplitOptions.RemoveEmptyEntries)
                .Should().Equal(verdict, "tests/OrcaCore.DeveloperSurface.Guards/Fixtures/immutable-document-history.json",
                    "tests/OrcaCore.DeveloperSurface.Guards/Fixtures/review-manifest-provenance.json");
            var bytes = Blob(root, evidence, verdict);
            Hash(bytes).Should().Be(sha);
            File.ReadAllBytes(Path.Combine(root, verdict)).Should().Equal(bytes);
        }
    }

    internal static string[] IntegratedSourcePaths(string root)
    {
        var paths = Git(root, "diff", "--name-only", Parent, IntegrationTip, "--", "src").Split('\n', StringSplitOptions.RemoveEmptyEntries);
        paths.Should().HaveCount(9);
        paths.Should().OnlyContain(path => path.StartsWith("src/OrcaCore.Engine.Ephemeral/", StringComparison.Ordinal));
        foreach (var path in paths) Blob(root, Merge, path).Should().Equal(Blob(root, IntegrationTip, path));
        return paths;
    }

    internal static string[] SourcePaths(string root)
    {
        ValidateReviewedChains(root);
        var rows = Read(root).Sources;
        var approved = Git(root, "diff", "--name-only", Parent, Merge, "--", "src").Split('\n', StringSplitOptions.RemoveEmptyEntries);
        rows.Select(row => row.Path).Should().Equal(approved.Except(IntegratedSourcePaths(root), StringComparer.Ordinal));
        foreach (var row in rows) VerifyApproved(root, row);
        return approved;
    }

    internal static string[] PublicApiPaths(string root)
    {
        var rows = Read(root).PublicApi;
        rows.Select(row => row.Path).Should().Equal("tests/OrcaCore.DeveloperSurface.Guards/Fixtures/PublicApi/v1/OrcaCore.Dag.api.txt");
        foreach (var row in rows) { VerifyApproved(root, row); _ = PriorBytes(root, row); }
        return rows.Select(row => row.Path).ToArray();
    }

    internal static byte[] PriorSourceBytes(string root, string path)
    {
        var row = Read(root).Sources.SingleOrDefault(item => item.Path == path);
        if (row is null) return Blob(root, Parent, path);
        VerifyApproved(root, row);
        return PriorBytes(root, row);
    }

    internal static string PriorDocument(string root, string path)
    {
        var row = Read(root).Documents.SingleOrDefault(item => item.Path == path);
        if (row is null) return TextAt(root, Merge, path);
        VerifyApproved(root, row);
        return Normalize(Encoding.UTF8.GetString(PriorBytes(root, row)));
    }

    internal static string TextAt(string root, string commit, string path) => Normalize(Encoding.UTF8.GetString(Blob(root, commit, path)));
    private static void VerifyApproved(string root, FileRow row) => Hash(Blob(root, Merge, row.Path)).Should().Be(row.AfterSha256);
    private static byte[] PriorBytes(string root, FileRow row)
    {
        var bytes = Blob(root, Parent, row.Path); Hash(bytes).Should().Be(row.BeforeSha256); return bytes;
    }
    private static byte[] Blob(string root, string commit, string path)
    {
        using var process = Process.Start(Start(root, "show", commit + ":" + path))!;
        using var bytes = new MemoryStream(); process.StandardOutput.BaseStream.CopyTo(bytes);
        var error = process.StandardError.ReadToEnd(); process.WaitForExit();
        process.ExitCode.Should().Be(0, error); return bytes.ToArray();
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
            RedirectStandardOutput = true, RedirectStandardError = true, CreateNoWindow = true,
            StandardOutputEncoding = Encoding.UTF8, StandardErrorEncoding = Encoding.UTF8 };
        foreach (var arg in args) start.ArgumentList.Add(arg); return start;
    }
    private static string Normalize(string text) => text.Replace("\r\n", "\n", StringComparison.Ordinal).Replace('\r', '\n');
    private static string Hash(byte[] bytes) => Convert.ToHexStringLower(SHA256.HashData(bytes));
}
