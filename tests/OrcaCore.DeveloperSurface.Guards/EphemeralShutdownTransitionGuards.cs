using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using AwesomeAssertions;

namespace OrcaCore.DeveloperSurface.Guards;

[Trait(GuardTraits.Phase, GuardTraits.Phase0)]
[Trait(GuardTraits.Disposition, GuardTraits.Infrastructure)]
public sealed class EphemeralShutdownTransitionGuards
{
    private const string Change = "add-ephemeral-host-shutdown";
    private const string Prefix = "openspec/changes/" + Change + "/";
    private const string Checkpoint = "3c8d33c1c89a3b7d4f1b0cd3a8c49ced5f3e4d21";
    private const string Parent = "fa88986aaac219c998b15e96748efdf2f245de51";
    private const string Tree = "b1611b09f2774bab0a84ae2a1492abd71a1e4459";
    private const string VerdictCommit = "4bd7c3e2f179a92bb5161edc16fbc0950d4b4126";
    private const string Verdict = Prefix + "artifacts/contract-r1-independent-review-verdict-2026-10-19.md";
    private const string VerdictSha = "fdac6ecd148ffa1ef936e1f9e27a5a80751ba554c5fb2aaaca7b7e3da94955ce";
    private const string Registry = "tests/OrcaCore.DeveloperSurface.Guards/Fixtures/post-gate-amendment-path.json";
    private const string Artifact = Prefix + "artifacts/task-2-1-amendment-registry-2026-10-19.md";
    private const string ArtifactSha = "064292fac110f493b540086143137e5d8ff4b57e45de077c05d3c4b2876d28d0";
    private const string Api = Prefix + "artifacts/task-2-1-public-api-2026-10-19.md";
    private const string ApiSha = "d1232fdfd991b19945b282f9d05ec888356f3daa65b2c57dabe584da0ad3232b";
    private const string Resolution = Prefix + "artifacts/task-2-1-occurrence-resolution-2026-10-19.tsv";
    private const string ResolutionSha = "ad0e5b9df73ee245ed849d501760e31d2eb30df882a834ab6d0f4edc074a9283";
    private const string CanonicalCheckpoint = "2e51fbdda5ebb6832c5ea93361b58acaf79b2b5e";
    private const string CanonicalTree = "0f4ad1fbb68b87fb3aee5af428b8da4d8dd60b18";
    private const string CanonicalVerdictCommit = "6253f6cd57f81afdde709ad705e9ba686a2818a2";
    private const string CanonicalVerdict = Prefix + "artifacts/task-2-1-transition-independent-review-verdict-2026-10-19.md";
    private const string CanonicalVerdictSha = "12f99ea87b68d9bb1a4f6b7abb6229c7be53a02a1a514e8635c493553ee952c5";
    private const string CanonicalBinding = Prefix + "artifacts/section-3-canonical-binding-2026-10-19.md";
    private const string CanonicalBindingSha = "f1dd54553676389876fe3846098675ed249e8d46ed467c531cb886ad63582ca0";
    private const string SourceCheckpoint = "789c1b74bb6a057bb39fd6191cc8a95140f39763";
    private const string SourceTree = "fac02f62a3ef0fdebb975f496001b3e27b00ed5f";
    private const string SourceVerdictCommit = "b3cd5affdaadcdaf9a9c6a91e8ea523ed8551770";
    private const string SourceVerdict = Prefix + "artifacts/section-3-independent-review-verdict-2026-10-19.md";
    private const string SourceVerdictSha = "d2ea311bdd255dcdf4171b237646eb8a845a9e019173e8407888db01ffe589ae";

    [Fact]
    public void ContractApproval_BindsExactCheckpointAndVerdictThroughGit()
    {
        using var fixture = Load();
        var evidence = fixture.RootElement.GetProperty("ephemeralShutdownContractEvidence");
        S(evidence, "checkpointCommit").Should().Be(Checkpoint);
        S(evidence, "checkpointParent").Should().Be(Parent);
        S(evidence, "checkpointTree").Should().Be(Tree);
        S(evidence, "approvalEvidenceCommit").Should().Be(VerdictCommit);
        S(evidence, "verdictPath").Should().Be(Verdict);
        S(evidence, "verdictSha256").Should().Be(VerdictSha);
        Git("show", "-s", "--format=%P", Checkpoint).Trim().Should().Be(Parent);
        Git("show", "-s", "--format=%T", Checkpoint).Trim().Should().Be(Tree);
        Git("show", "-s", "--format=%P", VerdictCommit).Trim().Should().Be(Checkpoint);
        Git("diff-tree", "--no-commit-id", "--name-only", "-r", VerdictCommit).Trim().Should().Be(Verdict);
        var bytes = File.ReadAllBytes(PathOf(Verdict));
        bytes.Should().HaveCount(4012);
        Hash(bytes).Should().Be(VerdictSha);
        Encoding.UTF8.GetBytes(Git("show", VerdictCommit + ":" + Verdict)).Should().Equal(bytes);
        Read(Verdict).Should().Contain("## Verdict: APPROVE").And.Contain("F2");
        S(evidence, "artifactPath").Should().Be(Artifact);
        S(evidence, "artifactNormalizedSha256").Should().Be(ArtifactSha);
        Hash(Encoding.UTF8.GetBytes(Read(Artifact))).Should().Be(ArtifactSha);
        S(evidence, "publicApiPath").Should().Be(Api);
        S(evidence, "publicApiNormalizedSha256").Should().Be(ApiSha);
        Hash(Encoding.UTF8.GetBytes(Read(Api))).Should().Be(ApiSha);
    }

    [Fact]
    public void SourceApproved_EarlierRecordsStayExact_AndApprovedSourceScopeIsPreserved()
    {
        using var fixture = Load();
        var current = fixture.RootElement;
        current.GetProperty("schemaVersion").GetInt32().Should().Be(24);
        var before = JsonNode.Parse(Git("show", SourceVerdictCommit + ":" + Registry))!.AsObject();
        var after = JsonNode.Parse(current.GetRawText())!.AsObject();
        // The native-support extension is independently validated, including unchanged prior records.
        after.Remove("nativeSupportContractEvidence");
        after.Remove("nativeSupportPendingAmendments");
        after.Remove("ephemeralShutdownSourceEvidence");
        foreach (var record in after["shutdownPendingAmendments"]!.AsArray())
        {
            record!["stage"] = "CanonicalApprovedSourcePending";
            record["sourceApprovalEvidencePath"] = null;
            record["ownershipApprovalEvidencePath"] = null;
        }
        after["schemaVersion"] = 18;
        JsonNode.DeepEquals(before, after).Should().BeTrue("all earlier approval records remain exact");
        var records = current.GetProperty("shutdownPendingAmendments").EnumerateArray().ToArray();
        records.Select(x => S(x, "capability")).Should().Equal("developer-facing-surface", "management-and-querying");
        foreach (var record in records)
        {
            var cap = S(record, "capability");
            S(record, "id").Should().Be("ephemeral-shutdown-" + cap);
            S(record, "owningChange").Should().Be(Change);
            S(record, "stage").Should().Be("SourceApproved");
            record.GetProperty("pendingCanonicalOperationCount").GetInt32().Should().Be(0);
            record.GetProperty("declaredRequirementCount").GetInt32().Should().Be(cap == "developer-facing-surface" ? 2 : 4);
            S(record, "contractApprovalTask").Should().Be("1.4");
            S(record, "contractCheckpointTask").Should().Be("1.4");
            S(record, "canonicalPreparationTask").Should().Be("2.1");
            S(record, "canonicalReviewTask").Should().Be("2.2");
            S(record, "authoringCompanionDisposition").Should().Be("ExcludedNoHostingDeclarations");
            S(record, "canonicalApprovalEvidencePath").Should().Be(CanonicalVerdict);
            foreach (var key in new[] { "sourceApprovalEvidencePath", "ownershipApprovalEvidencePath" })
                S(record, key).Should().Be(SourceVerdict);
            record.GetProperty("sourceTasks").EnumerateArray().Select(x => x.GetString())
                .Should().Equal("3.1", "3.2", "3.3", "3.4", "3.5");
            var tasks = Read(Prefix + "tasks.md");
            foreach (var key in new[] { "numberedRequirementPaths", "acceptanceCriteriaPaths" })
                foreach (var path in record.GetProperty(key).EnumerateArray())
                    tasks.Should().Contain("`" + path.GetString() + "`");
        }
        Regex.Matches(Read(Prefix + "tasks.md"), @"(?m)^- \[x\] (\d+\.\d+) ")
            .Select(x => x.Groups[1].Value).Should().Equal("1.1", "1.2", "1.3", "1.4", "2.1", "2.2", "3.1", "3.2", "3.3", "3.4", "3.5");
        Git("diff", "--name-only", CanonicalVerdictCommit, SourceCheckpoint, "--", "Directory.Packages.props", "global.json",
            "docs/specs/17-public-authoring-contract.cs").Trim().Should().BeEmpty();
        Git("diff", "--name-only", CanonicalVerdictCommit, SourceCheckpoint, "--", "src").Split('\n', StringSplitOptions.RemoveEmptyEntries)
            .Should().OnlyContain(path => path.StartsWith("src/OrcaCore.Engine.Ephemeral/", StringComparison.Ordinal),
                "the approved source checkpoint owns only the ephemeral runtime, with no new dependency or friend edge");
    }

    [Fact]
    public void CanonicalAndSourceApprovals_BindTheirCheckpointsAndSoleVerdictChildrenThroughGit()
    {
        using var fixture = Load();
        var evidence = fixture.RootElement.GetProperty("ephemeralShutdownCanonicalEvidence");
        S(evidence, "checkpointCommit").Should().Be(CanonicalCheckpoint);
        S(evidence, "checkpointParent").Should().Be(VerdictCommit);
        S(evidence, "checkpointTree").Should().Be(CanonicalTree);
        S(evidence, "approvalEvidenceCommit").Should().Be(CanonicalVerdictCommit);
        S(evidence, "verdictPath").Should().Be(CanonicalVerdict);
        S(evidence, "verdictSha256").Should().Be(CanonicalVerdictSha);
        S(evidence, "artifactPath").Should().Be(CanonicalBinding);
        S(evidence, "artifactNormalizedSha256").Should().Be(CanonicalBindingSha);
        Hash(Encoding.UTF8.GetBytes(Read(CanonicalBinding))).Should().Be(CanonicalBindingSha);
        Git("show", "-s", "--format=%P", CanonicalCheckpoint).Trim().Should().Be(VerdictCommit);
        Git("show", "-s", "--format=%T", CanonicalCheckpoint).Trim().Should().Be(CanonicalTree);
        Git("show", "-s", "--format=%P", CanonicalVerdictCommit).Trim().Should().Be(CanonicalCheckpoint);
        Git("diff-tree", "--no-commit-id", "--name-only", "-r", CanonicalVerdictCommit).Trim().Should().Be(CanonicalVerdict);
        var bytes = File.ReadAllBytes(PathOf(CanonicalVerdict));
        bytes.Should().HaveCount(2897);
        Hash(bytes).Should().Be(CanonicalVerdictSha);
        Encoding.UTF8.GetBytes(Git("show", CanonicalVerdictCommit + ":" + CanonicalVerdict)).Should().Equal(bytes);
        Read(CanonicalVerdict).Should().Contain("APPROVE");
        ValidateSourceApproval(fixture.RootElement.GetProperty("ephemeralShutdownSourceEvidence"));
    }

    private static void ValidateSourceApproval(JsonElement evidence)
    {
        S(evidence, "checkpointCommit").Should().Be(SourceCheckpoint);
        S(evidence, "checkpointParent").Should().Be(CanonicalVerdictCommit);
        S(evidence, "checkpointTree").Should().Be(SourceTree);
        S(evidence, "approvalEvidenceCommit").Should().Be(SourceVerdictCommit);
        S(evidence, "verdictPath").Should().Be(SourceVerdict);
        S(evidence, "verdictSha256").Should().Be(SourceVerdictSha);
        evidence.GetProperty("verdictBytes").GetInt32().Should().Be(6364);
        Git("show", "-s", "--format=%P", SourceCheckpoint).Trim().Should().Be(CanonicalVerdictCommit);
        Git("show", "-s", "--format=%T", SourceCheckpoint).Trim().Should().Be(SourceTree);
        Git("show", "-s", "--format=%P", SourceVerdictCommit).Trim().Should().Be(SourceCheckpoint);
        Git("diff-tree", "--no-commit-id", "--name-only", "-r", SourceVerdictCommit).Trim().Should().Be(SourceVerdict);
        var bytes = File.ReadAllBytes(PathOf(SourceVerdict));
        bytes.Should().HaveCount(6364);
        Hash(bytes).Should().Be(SourceVerdictSha);
        Encoding.UTF8.GetBytes(Git("show", SourceVerdictCommit + ":" + SourceVerdict)).Should().Equal(bytes);
        Read(SourceVerdict).Should().Contain("## Verdict: APPROVE");
    }

    [Fact]
    public void CorrectedBlocks_AppendExactlyAndPreserveTheSevenDeclarations()
    {
        var expected = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["developer-facing-surface\0Ephemeral host shutdown has one supported observation contract"] = "6dc7fce06a29742d54ce23bc63644852d62c8236e7b3506f03733c5f13271ce5",
            ["developer-facing-surface\0Ephemeral host shutdown preserves dependency ownership"] = "389568673e999728c85dd4bbf2e8984089ecb677d23b18786ce136ee920847e0",
            ["management-and-querying\0Ephemeral host shutdown closes admission atomically"] = "009db0deff82740ebecb0d5b92027d576b3686316d8a861493142d3eb8409fc9",
            ["management-and-querying\0Completed ephemeral host shutdown proves host-wide quiescence"] = "1cbcd063c33b69eb7c578338954b5f4cb6cddbb67bf0d84a6d802a745653fc21",
            ["management-and-querying\0Incomplete ephemeral host shutdown preserves truthful ownership"] = "5f735fd7e411a39157f7241ae5bef7641f0d54cf049724d3ea17e58b8e6829d3",
            ["management-and-querying\0Repeated ephemeral host shutdown observes one irreversible stop"] = "3ad75931371674f27e36afd2497ec9c4f1f22a0909a018da185535ef1227d4cb",
        };
        using var fixture = Load();
        foreach (var record in fixture.RootElement.GetProperty("shutdownPendingAmendments").EnumerateArray())
        {
            var cap = S(record, "capability");
            var delta = Read(Prefix + "specs/" + cap + "/spec.md");
            var blocks = Blocks(delta);
            blocks.Keys.Should().Equal(record.GetProperty("requirementIdentities").EnumerateArray().Select(x => x.GetString()));
            var canonicalPath = "openspec/specs/" + cap + "/spec.md";
            var before = Git("show", VerdictCommit + ":" + canonicalPath);
            // Pin the exact approved shutdown layer; native support may append only its separately guarded successor.
            var approvedCanonical = Git("show", CanonicalCheckpoint + ":" + canonicalPath);
            approvedCanonical.Should().Be(before + "\n" + delta["## ADDED Requirements\n\n".Length..],
                "earlier canonical text is a strict byte prefix and the approved shutdown blocks are exact copies");
            if (cap == "developer-facing-surface")
                Read(canonicalPath).Should().StartWith(approvedCanonical);
            else
                Read(canonicalPath).Should().Be(approvedCanonical);
            var canonical = Blocks(Read(canonicalPath));
            foreach (var (name, block) in blocks)
            {
                Hash(Encoding.UTF8.GetBytes(block)).Should().Be(expected[cap + "\0" + name]);
                canonical[name].Should().Be(block);
            }
        }
        var declaration = CSharp(Read(Api));
        declaration.Should().Be(CSharp(Git("show", Checkpoint + ":" + Prefix + "artifacts/contract-public-api-2026-10-18.md")));
        Read("docs/specs/17-selected-mode-capability-matrix.md").Should().Contain("```csharp\n" + declaration + "\n```");
        Regex.Matches(declaration, @"(?m)^\s*public (?:abstract class|sealed class|interface|enum) ").Should().HaveCount(7);
    }

    [Fact]
    public void EveryOriginalOccurrence_HasAnAccountableIndividualResolution()
    {
        var text = Read(Resolution);
        Hash(Encoding.UTF8.GetBytes(text)).Should().Be(ResolutionSha);
        var rows = text.TrimEnd('\n').Split('\n').Skip(1).Select(x => x.Split('\t')).ToArray();
        var old = Git("show", VerdictCommit + ":" + Prefix + "artifacts/contract-occurrence-decisions-2026-10-18.tsv")
            .TrimEnd('\n').Split('\n').Skip(1).Select(x => x.Split('\t')).ToArray();
        rows.Should().HaveCount(120);
        var approvedSources = rows.Where(row => row[4] != "Edit").Select(row => row[1]).Distinct(StringComparer.Ordinal)
            .ToDictionary(path => path, path => Git("show", CanonicalCheckpoint + ":" + path).Split('\n'), StringComparer.Ordinal);
        for (var i = 0; i < rows.Length; i++)
        {
            var row = rows[i];
            row.Should().HaveCount(7);
            row.Take(3).Should().Equal(old[i].Take(3));
            row[3].Should().Be(old[i][4]);
            row[4].Should().Be(old[i][5]);
            row[6].Should().NotBeNullOrWhiteSpace();
            if (row[4] == "Edit") row[5].Should().Be("-");
            else
            {
                var line = approvedSources[row[1]][int.Parse(row[5], System.Globalization.CultureInfo.InvariantCulture) - 1];
                Hash(Encoding.UTF8.GetBytes(line)).Should().Be(row[3]);
            }
        }
        rows.Count(x => x[4] == "Retain").Should().Be(81);
        rows.Count(x => x[4] == "Augment").Should().Be(37);
        rows.Where(x => x[4] == "Edit").Select(x => x[0]).Should().Equal("28", "41");
    }

    private static string PathOf(string path) => Path.Combine(FixtureDefinitions.RepositoryRoot(), path);
    private static string Read(string path) => File.ReadAllText(PathOf(path)).Replace("\r\n", "\n", StringComparison.Ordinal);
    private static JsonDocument Load() => JsonDocument.Parse(Read(Registry));
    private static string S(JsonElement e, string key) => e.GetProperty(key).GetString()!;
    private static string Hash(byte[] value) => Convert.ToHexString(SHA256.HashData(value)).ToLowerInvariant();
    private static string CSharp(string text) => Regex.Match(text, @"```csharp\n([\s\S]*?)\n```").Groups[1].Value;
    private static Dictionary<string, string> Blocks(string text) => Regex.Matches(text,
        @"(?ms)^### Requirement: (?<name>[^\n]+)\n.*?(?=^### Requirement: |^## |\z)")
        .ToDictionary(x => x.Groups["name"].Value, x => x.Value.TrimEnd('\n'), StringComparer.Ordinal);
    private static string Git(params string[] args)
    {
        var info = new ProcessStartInfo("git")
        {
            WorkingDirectory = FixtureDefinitions.RepositoryRoot(),
            UseShellExecute = false, RedirectStandardOutput = true, RedirectStandardError = true,
            StandardOutputEncoding = Encoding.UTF8, StandardErrorEncoding = Encoding.UTF8, CreateNoWindow = true
        };
        foreach (var arg in args) info.ArgumentList.Add(arg);
        using var process = Process.Start(info) ?? throw new InvalidOperationException("Git did not start");
        var output = process.StandardOutput.ReadToEndAsync();
        var error = process.StandardError.ReadToEndAsync();
        if (!process.WaitForExit(30000))
        {
            process.Kill(entireProcessTree: true);
            throw new TimeoutException("Required Git lookup exceeded 30 seconds");
        }
        if (process.ExitCode != 0)
            throw new InvalidOperationException("Required Git lookup failed: " + error.GetAwaiter().GetResult());
        return output.GetAwaiter().GetResult().Replace("\r\n", "\n", StringComparison.Ordinal);
    }
}
