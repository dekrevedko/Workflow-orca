using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using AwesomeAssertions;

namespace OrcaCore.DeveloperSurface.Guards;

[Trait(GuardTraits.Phase, GuardTraits.Phase0)]
[Trait(GuardTraits.Disposition, GuardTraits.Infrastructure)]
public sealed class DagStreamIdentitySliceGuards
{
    private const string FingerprintAmendmentRegistrySha256 = "321b8fda534883310f1593d98c33fc28c9b39d529e6efaae9440438a345601bd";
    private const string CanonicalContentRecordSha256 = "f61d8b49b25095b31989639fb43a8ac6cae6fedc996ddfe1bbf4ec7233b4fae2";
    private const string ApprovedOrcaCoreApiSha256 = "a8bb583860fd8744b018273911fe65b1c899926d70e501efb773ccfbf5588f3e";
    private const string BaseCommit = "89e3b18aed62446583211478f1eb8545e4fb85ed";
    private static readonly TransitionEvidence ApprovedTransition = new(
        "1770b2b979ca8f8c4b385637ddabe5d28c45dbb2",
        "61b5900c0a6a87c34429e15c9cad7832d891c615",
        "18bf8656fdbcbf061d4f223afca6ed438efee3ea",
        "docs/review/developer-facing-interface-section-08-task-8-4-dag-run-atomic-canonical-transition-independent-review-verdict-2026-10-06.md",
        "e89e7066308bc906e6b94b073828471f8481a11b919ea711df1e89f55018cca6");

    [Fact]
    public void CanonicalFiles_AllTextIncludingUnownedRequirementsIsReviewPinned()
    {
        var root = FixtureDefinitions.RepositoryRoot();
        using var fixture = ReadFixture(root, "openspec-provenance-checkpoint.json");
        var expected = fixture.RootElement.GetProperty("canonicalContents").Deserialize<CanonicalFile[]>(JsonOptions)!;
        var actual = Directory.EnumerateDirectories(Path.Combine(root, "openspec", "specs"))
            .Select(directory => Path.GetRelativePath(root, Path.Combine(directory, "spec.md")).Replace('\\', '/'))
            .Order(StringComparer.Ordinal)
            .Select(path => Content(path, Normalize(File.ReadAllText(Path.Combine(root, path)))))
            .ToArray();
        RequireSameInventory(expected, actual);
        var record = string.Concat(actual.Select(row => $"{row.Path}\t{row.Bytes}\t{row.Sha256}\n"));
        var data = fixture.RootElement;
        data.GetProperty("canonicalContentRecordFormat").GetString().Should().Be(
            "path\\tLF-normalized UTF-8 bytes\\tSHA-256; ordinal path order, LF rows and one final LF");
        actual.Should().HaveCount(14);
        data.GetProperty("canonicalContentCount").GetInt32().Should().Be(actual.Length);
        data.GetProperty("canonicalContentBytes").GetInt32().Should().Be(Encoding.UTF8.GetByteCount(record));
        Hash(record).Should().Be(CanonicalContentRecordSha256,
            "whole canonical text may change only in an explicitly reviewed mechanical sync, not a routine fixture refresh");
        data.GetProperty("canonicalContentSha256").GetString().Should().Be(CanonicalContentRecordSha256);

        var unownedEdit = actual.ToArray();
        unownedEdit[0] = Content(actual[0].Path, "an unowned requirement was edited");
        Action drift = () => RequireSameInventory(expected, unownedEdit);
        drift.Should().Throw<InvalidDataException>().WithMessage($"*{actual[0].Path}*");
        Action missing = () => RequireSameInventory(expected, actual[1..]);
        missing.Should().Throw<InvalidDataException>().WithMessage($"*{actual[0].Path}*");
    }

    [Fact]
    public void CanonicalTransitionApproval_BindsTheActualCheckpointAndVerdictAddition()
    {
        var root = FixtureDefinitions.RepositoryRoot();
        using var registry = ReadFixture(root, "post-gate-amendment-path.json");
        var evidence = registry.RootElement.GetProperty("approvedDagRunTransitionEvidence")
            .Deserialize<TransitionEvidence>(JsonOptions)!;
        evidence.Should().Be(ApprovedTransition);
        ValidateReviewEvidence(root, evidence);
        ValidateProposedFingerprintAmendment(root, registry.RootElement);
    }

    private static void ValidateReviewEvidence(string root, TransitionEvidence evidence)
    {
        Git(root, "rev-parse", $"{evidence.CheckpointCommit}^{{tree}}").Trim().Should().Be(evidence.CheckpointTree);
        Git(root, "rev-list", "--parents", "-n", "1", evidence.EvidenceCommit).Trim()
            .Should().Be($"{evidence.EvidenceCommit} {evidence.CheckpointCommit}",
                "approval evidence must be a single-parent direct child, not any later commit containing the verdict");
        Git(root, "diff-tree", "--no-commit-id", "--name-only", "--diff-filter=A", "-r", evidence.EvidenceCommit)
            .Split('\n', StringSplitOptions.RemoveEmptyEntries).Should().Contain(evidence.VerdictPath);
        Git(root, "merge-base", "--is-ancestor", evidence.EvidenceCommit, "HEAD");
        var verdict = Normalize(File.ReadAllText(Path.Combine(root, evidence.VerdictPath)));
        Hash(verdict).Should().Be(evidence.VerdictSha256);
        Normalize(Git(root, "show", $"{evidence.EvidenceCommit}:{evidence.VerdictPath}"))
            .Should().Be(verdict, "the evidence commit must actually contain the registered immutable bytes");
        verdict.Split('\n').Count(line => line.StartsWith("**Verdict:**", StringComparison.Ordinal)).Should().Be(1);
        verdict.TrimEnd().Should().EndWith("**Verdict:** **APPROVE**");
    }

    [Fact]
    public void StreamIdentity_UsesOnlyTheSevenApprovedApiAdditions()
    {
        var root = FixtureDefinitions.RepositoryRoot();
        const string path = "tests/OrcaCore.DeveloperSurface.Guards/Fixtures/PublicApi/v1/OrcaCore.api.txt";
        var before = Normalize(Git(root, "show", $"{BaseCommit}:{path}"));
        Hash(before).Should().Be("ef13f660beecb09c88c0d22b09abae7b69332158d8fcea4b7da69ad5e0a32678");
        var after = Normalize(Git(root, "show", "10aec46e4a1f2f0771f6a5418b4dc6ef3ff3b5fd:" + path));
        Hash(after).Should().Be(ApprovedOrcaCoreApiSha256);
        var current = Normalize(File.ReadAllText(Path.Combine(root, path)));
        Hash(current).Should().Be("4a2e0af3420a8126f1a943a1510f8009b5da9371276a3a98e1e03092da432d3a");
        Hash(PublicApiBaseline.CaptureCurrent()["OrcaCore"]).Should().Be("4a2e0af3420a8126f1a943a1510f8009b5da9371276a3a98e1e03092da432d3a");
        // Normalize only the exact separately approved throttle-parameter annotation.
        // Both complete current/source hashes above remain pinned; the prior metadata API is exact too.
        var metadataApi = current.Replace("  method public static For<TStep>([System.Private.CoreLib]System.Int32 maxConcurrency) -> [OrcaCore]OrcaCore.Hosting.StepExecutionThrottle! where [TStep : attributes [[System.Private.CoreLib]System.Diagnostics.CodeAnalysis.DynamicallyAccessedMembersAttribute(8192), [System.Private.CoreLib]System.Runtime.CompilerServices.NullableAttribute(2)]]",
            "  method public static For<TStep>([System.Private.CoreLib]System.Int32 maxConcurrency) -> [OrcaCore]OrcaCore.Hosting.StepExecutionThrottle! where [TStep : attributes [[System.Private.CoreLib]System.Runtime.CompilerServices.NullableAttribute(2)]]", StringComparison.Ordinal);
        Hash(metadataApi).Should().Be("7de9a01886614526379247fe5cbeba8b3d102366e7037c432c542f2b8600c68a");
        ApiKeys(after).Except(ApiKeys(metadataApi)).Should().BeEmpty();
        ApiKeys(metadataApi).Except(ApiKeys(after)).Should().HaveCount(6,
            "only the approved metadata API plus exact product-factory catalogue extend the DAG-era baseline");
        var oldLines = ApiKeys(before);
        var newLines = ApiKeys(after);
        oldLines.Except(newLines).Should().BeEmpty("existing signatures and nullability metadata must stay exact");
        newLines.Except(oldLines).Should().HaveCount(7);
    }

    private static void ValidateProposedFingerprintAmendment(string root, JsonElement registry)
    {
        var amendment = registry.GetProperty("dagRunFingerprintContractAmendment");
        Hash(JsonSerializer.Serialize(amendment)).Should().Be(FingerprintAmendmentRegistrySha256,
            "the proposed fingerprint addendum and exact artifacts must remain source-pinned, not routine fixture data");
        amendment.GetProperty("stage").GetString().Should().Be("Proposed");
        amendment.GetProperty("approval").ValueKind.Should().Be(JsonValueKind.Null,
            "no implementation approval exists until the exact amendment is independently checkpointed and evidenced");
        var original = amendment.GetProperty("amendedContract");
        ValidateReviewEvidence(root, original.Deserialize<TransitionEvidence>(JsonOptions)!);
        foreach (var pair in new[] { ("contractPath", "contractSha256"), ("apiCatalogPath", "apiCatalogSha256") })
            Hash(Normalize(File.ReadAllText(Path.Combine(root, original.GetProperty(pair.Item1).GetString()!))))
                .Should().Be(original.GetProperty(pair.Item2).GetString(), "the approved RC-3 packet is immutable history");
        foreach (var artifact in amendment.GetProperty("artifacts").EnumerateArray())
            Hash(Normalize(File.ReadAllText(Path.Combine(root, artifact.GetProperty("path").GetString()!))))
                .Should().Be(artifact.GetProperty("sha256").GetString(), "the proposed amendment artifact must stay byte-exact after freeze clearance");
        const string artifacts = "openspec/changes/admit-durable-dag-run-progression/artifacts/";
        var oldSignatures = Normalize(File.ReadAllText(Path.Combine(root, artifacts + "task-1-1-standalone-signatures-rc-3-remediation-2026-10-05.cs")));
        var newSignatures = Normalize(File.ReadAllText(Path.Combine(root, artifacts + "task-2-1-fingerprint-amendment-signatures-2026-10-14.cs")));
        newSignatures.Should().Be(oldSignatures
            .Replace("byte[] Bytes, PayloadFingerprint Fingerprint);", "byte[] Bytes, string Fingerprint);", StringComparison.Ordinal)
            .Replace("DefinitionFingerprint Fingerprint);", "string Fingerprint);", StringComparison.Ordinal),
            "only the two storage-record parameter types change; the typed child receipt and other seams do not");
        var before = Normalize(File.ReadAllText(Path.Combine(root, original.GetProperty("apiCatalogPath").GetString()!)));
        var after = Normalize(File.ReadAllText(Path.Combine(root, artifacts + "task-2-1-exact-api-additions-fingerprint-amendment-2026-10-14.md")));
        var oldKeys = CatalogApiKeys(before);
        var newKeys = CatalogApiKeys(after);
        oldKeys.Except(newKeys).Should().HaveCount(6);
        newKeys.Except(oldKeys).Should().HaveCount(6);
        newKeys.Should().BeEquivalentTo(CatalogApiKeys(before
            .Replace("[OrcaCore]OrcaCore.DefinitionFingerprint! Fingerprint", "[System.Private.CoreLib]System.String! Fingerprint", StringComparison.Ordinal)
            .Replace("[OrcaCore]OrcaCore.PayloadFingerprint! Fingerprint", "[System.Private.CoreLib]System.String! Fingerprint", StringComparison.Ordinal)),
            "no seventh API key or direct provider-port signature may change in this contract refinement");
        var baseCommit = amendment.GetProperty("baseCommit").GetString()!;
        // This pending DAG amendment freezes its own implementations and contract surfaces.
        // Independent ephemeral ownership work is authorized by its separately carried 3.1 verdict.
        Git(root, "diff", "--name-only", baseCommit, "--",
            "src/OrcaCore.Dag", "src/OrcaCore.Dag.Hosting",
            "openspec/changes/admit-durable-dag-run-progression/specs",
            "tests/OrcaCore.DeveloperSurface.Guards/Fixtures/PublicApi/v1/OrcaCore.Dag.api.txt",
            "tests/OrcaCore.DeveloperSurface.Guards/Fixtures/PublicApi/v1/OrcaCore.Dag.Hosting.api.txt",
            "tests/OrcaCore.DeveloperSurface.Guards/Fixtures/PublicApi/v1/OrcaCore.Runtime.Protocol.api.txt",
            "tests/OrcaCore.DeveloperSurface.Guards/Fixtures/PublicApi/v1/OrcaCore.Provider.Abstractions.api.txt")
            .Should().BeEmpty("the proposed fingerprint amendment authorizes no DAG implementation or DAG contract edit");
        // Exact independently approved append-only layers; this grants no DAG source or broad canonical exemption.
        var layers = new Dictionary<string, string[]>(StringComparer.Ordinal)
        {
            ["developer-facing-surface"] = ["add-ephemeral-terminal-retention", "add-ephemeral-host-shutdown", "support-ephemeral-native-aot"],
            ["management-and-querying"] = ["add-ephemeral-terminal-retention", "add-ephemeral-host-shutdown"],
            ["quality-and-verification"] = ["assess-ephemeral-native-aot", "support-ephemeral-native-aot"],
            ["structured-fiber-execution"] = ["support-ephemeral-native-aot"],
            ["workflow-contracts"] = ["support-ephemeral-native-aot"]
        };
        Git(root, "diff", "--name-only", baseCommit, "--", "openspec/specs")
            .Split('\n', StringSplitOptions.RemoveEmptyEntries).Should().Equal(
                layers.Keys.Select(cap => "openspec/specs/" + cap + "/spec.md").Order(StringComparer.Ordinal));
        foreach (var (capability, changes) in layers)
        {
            var path = "openspec/specs/" + capability + "/spec.md";
            var expected = Normalize(Git(root, "show", $"{baseCommit}:{path}")).TrimEnd('\n') + "\n";
            foreach (var change in changes)
            {
                var delta = Normalize(File.ReadAllText(Path.Combine(
                    OpenSpecCorpusGuards.ResolveChangeRecord(root, change), "specs", capability, "spec.md")));
                delta.Should().StartWith("## ADDED Requirements\n\n");
                expected += "\n" + delta["## ADDED Requirements\n\n".Length..].TrimEnd('\n') + "\n";
            }
            Normalize(File.ReadAllText(Path.Combine(root, path))).Should().Be(expected,
                "only the exact reviewed layers extend this canonical target; native-support guards pin its Git-approved deltas");
        }
        Git(root, "ls-files", "--others", "--exclude-standard", "--",
            "src/OrcaCore.Dag", "src/OrcaCore.Dag.Hosting", "openspec/specs").Should().BeEmpty();
    }

    private static HashSet<string> CatalogApiKeys(string text)
    {
        var result = new HashSet<string>(StringComparer.Ordinal);
        foreach (Match block in Regex.Matches(text, @"```text\n([\s\S]*?)\n```"))
            result.UnionWith(ApiKeys(block.Groups[1].Value));
        return result;
    }

    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

    private static HashSet<string> ApiKeys(string text)
    {
        var keys = new HashSet<string>(StringComparer.Ordinal);
        var type = "";
        foreach (var line in text.Split('\n'))
        {
            if (line.StartsWith("type ", StringComparison.Ordinal)) type = line;
            if (type.Length != 0 && line.Length != 0) keys.Add(type + "\t" + line);
        }
        return keys;
    }

    private static JsonDocument ReadFixture(string root, string name) => JsonDocument.Parse(File.ReadAllText(
        Path.Combine(root, "tests", "OrcaCore.DeveloperSurface.Guards", "Fixtures", name)));

    private static string Normalize(string text) => text.Replace("\r\n", "\n", StringComparison.Ordinal).Replace('\r', '\n');
    private static string Hash(string text) => Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(text)));
    private static CanonicalFile Content(string path, string text) => new(path, Encoding.UTF8.GetByteCount(text), Hash(text));

    private static void RequireSameInventory(CanonicalFile[] expected, CanonicalFile[] actual)
    {
        if (expected.SequenceEqual(actual)) return;
        var paths = expected.Except(actual).Concat(actual.Except(expected)).Select(row => row.Path).Distinct();
        throw new InvalidDataException("Canonical content drift: " + string.Join(", ", paths));
    }

    private static string Git(string root, params string[] arguments)
    {
        var start = new ProcessStartInfo("git")
        {
            WorkingDirectory = root, RedirectStandardOutput = true, RedirectStandardError = true, UseShellExecute = false
        };
        foreach (var argument in arguments) start.ArgumentList.Add(argument);
        using var process = Process.Start(start) ?? throw new InvalidOperationException("Could not run Git.");
        var output = process.StandardOutput.ReadToEndAsync();
        var error = process.StandardError.ReadToEndAsync();
        if (!process.WaitForExit(30_000))
        {
            process.Kill(entireProcessTree: true);
            throw new TimeoutException("Review evidence Git query exceeded 30 seconds.");
        }
        if (process.ExitCode != 0) throw new InvalidDataException(error.GetAwaiter().GetResult());
        return output.GetAwaiter().GetResult();
    }

    private sealed record CanonicalFile(string Path, int Bytes, string Sha256);
    private sealed record TransitionEvidence(string CheckpointCommit, string CheckpointTree, string EvidenceCommit,
        string VerdictPath, string VerdictSha256);
}
