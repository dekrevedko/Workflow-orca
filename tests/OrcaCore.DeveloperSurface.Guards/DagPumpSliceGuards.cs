using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using AwesomeAssertions;

namespace OrcaCore.DeveloperSurface.Guards;

[Trait(GuardTraits.Phase, GuardTraits.Phase0)]
[Trait(GuardTraits.Disposition, GuardTraits.Infrastructure)]
public sealed class DagPumpSliceGuards
{
    private const string BaseCommit = "602844547539e0e5908d250c2a0d9560fc4b25dc";
    private const string Checkpoint = "2fd7a7d789203cb1b780abf5478d0e67c070e7f7";
    private static readonly DagStreamIdentitySliceGuards.TransitionEvidence Approval = new(
        Checkpoint,
        "42770df9ad12a5cbeaeefb53ae13ef8e3794da7e",
        "3dae4aff9e190af2fa4acbc93d3b8aa2d4d1383c",
        "docs/review/developer-facing-interface-section-08-task-8-4-dag-run-task-2-2-pumps-and-certification-independent-review-verdict-2026-11-01.md",
        "0107b814d79c36450fe1f2c6b866069114bd59adb7099763269d51a477889b7b");
    private const string CandidateSha256 = "127ba6f6f6df268438ccc0796fe8b0fbf2ee02fe11c55f5760eccdd486f34bf1";
    private const string ArtifactSha256 = "00b0e6e150718cba0d193b6a929d64ce419c67cfa7c0ccd552a2ee51ae581680";

    [Fact]
    public void Task21Approval_BindsTheActualCheckpointAndDirectChildVerdictAddition()
    {
        var root = FixtureDefinitions.RepositoryRoot();
        using var registry = JsonDocument.Parse(File.ReadAllText(Path.Combine(root,
            "tests/OrcaCore.DeveloperSurface.Guards/Fixtures/post-gate-amendment-path.json")));
        DagStreamIdentitySliceGuards.ValidateProtocolProviderApproval(root, registry.RootElement.GetProperty("dagRunTask21Approval"));
    }

    [Fact]
    public void Task22Candidate_BindsTheExactPumpSourceSetAndLeavesTheFullBridgeForTask23()
    {
        var root = FixtureDefinitions.RepositoryRoot();
        using var registry = JsonDocument.Parse(File.ReadAllText(Path.Combine(root,
            "tests/OrcaCore.DeveloperSurface.Guards/Fixtures/post-gate-amendment-path.json")));
        var candidate = registry.RootElement.GetProperty("dagRunTask22Candidate");
        Hash(JsonSerializer.Serialize(candidate)).Should().Be(CandidateSha256,
            "the exact pending scope is review-bound, never self-approved by green tests");
        candidate.GetProperty("stage").GetString().Should().Be("PreparedForIndependentReview");
        candidate.GetProperty("baseCommit").GetString().Should().Be(BaseCommit);
        var artifact = File.ReadAllText(Path.Combine(root, candidate.GetProperty("artifactPath").GetString()!));
        Hash(artifact.Replace("\r\n", "\n", StringComparison.Ordinal)).Should().Be(ArtifactSha256);
        _ = ApprovedDagSourcePaths(root);
        var changed = Git(root, "diff", "--name-only", BaseCommit, Checkpoint, "--", "src").Split('\n', StringSplitOptions.RemoveEmptyEntries)
            .Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal);
        changed.Should().Equal(candidate.GetProperty("sourcePaths").EnumerateArray().Select(p => p.GetString()),
            "Task 2.2 may add only this dedicated child/ready-claim slice, not the next mapping bridge or unrelated source");
        foreach (var path in candidate.GetProperty("unchangedPublicApiPaths").EnumerateArray().Select(p => p.GetString()!))
            GitBytes(root, Checkpoint, path).Should().Be(GitBytes(root, BaseCommit, path),
                "the approved pump checkpoint added no public API");
    }

    internal static string[] ApprovedDagSourcePaths(string root)
    {
        DagStreamIdentitySliceGuards.ValidateReviewEvidence(root, Approval);
        Git(root, "rev-list", "--parents", "-n", "1", Checkpoint).Trim().Should().Be(Checkpoint + " " + BaseCommit);
        var paths = Git(root, "diff", "--name-only", "0a8e375fbcf5005a2a2f491cf24dfbbd9d64c580", Checkpoint, "--", "src")
            .Split('\n', StringSplitOptions.RemoveEmptyEntries);
        foreach (var path in paths)
            Convert.ToBase64String(File.ReadAllBytes(Path.Combine(root, path))).Should().Be(GitBytes(root, Checkpoint, path),
                "only byte-exact independently approved DAG sources are excluded from earlier change scopes");
        return paths;
    }

    private static string Hash(string text) => Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(text)));

    private static string Git(string root, params string[] arguments)
    {
        var start = new ProcessStartInfo("git") { WorkingDirectory = root, RedirectStandardOutput = true,
            RedirectStandardError = true, UseShellExecute = false };
        foreach (var argument in arguments) start.ArgumentList.Add(argument);
        using var process = Process.Start(start)!;
        var output = process.StandardOutput.ReadToEnd();
        var error = process.StandardError.ReadToEnd();
        process.WaitForExit();
        process.ExitCode.Should().Be(0, error);
        return output.Replace("\r\n", "\n", StringComparison.Ordinal);
    }

    private static string GitBytes(string root, string commit, string path)
    {
        var start = new ProcessStartInfo("git") { WorkingDirectory = root, RedirectStandardOutput = true,
            RedirectStandardError = true, UseShellExecute = false };
        start.ArgumentList.Add("show"); start.ArgumentList.Add(commit + ":" + path);
        using var process = Process.Start(start)!;
        using var stream = new MemoryStream();
        process.StandardOutput.BaseStream.CopyTo(stream);
        var error = process.StandardError.ReadToEnd(); process.WaitForExit();
        process.ExitCode.Should().Be(0, error);
        return Convert.ToBase64String(stream.ToArray());
    }
}
