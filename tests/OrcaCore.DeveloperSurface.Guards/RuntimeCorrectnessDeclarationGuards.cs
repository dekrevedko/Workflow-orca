using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using AwesomeAssertions;

namespace OrcaCore.DeveloperSurface.Guards;

[Trait(GuardTraits.Disposition, GuardTraits.Infrastructure)]
public sealed class RuntimeCorrectnessDeclarationGuards
{
    internal const string Change = "repair-runtime-correctness";
    internal const string RecordPath = "openspec/changes/" + Change + "/artifacts/amendment.json";

    [Fact]
    public void AdditiveContractAdmission_BindsExactDeltasNumberedTextAndApproval()
    {
        var root = FixtureDefinitions.RepositoryRoot();
        using var registry = Read(root, "tests/OrcaCore.DeveloperSurface.Guards/Fixtures/post-gate-amendment-path.json");
        registry.RootElement.GetProperty("runtimeCorrectnessAmendment").GetString().Should().Be(RecordPath);
        using var document = Read(root, RecordPath);
        var record = document.RootElement;
        record.GetProperty("change").GetString().Should().Be(Change);
        var stage = record.GetProperty("stage").GetString();
        new[] { "Proposed", "ContractApproved", "ApprovedPending", "Complete" }.Should().Contain(stage);
        var approved = stage != "Proposed";
        var synchronized = stage is "ApprovedPending" or "Complete";
        var contracts = record.GetProperty("contracts").EnumerateArray().ToArray();
        contracts.Select(row => row.GetProperty("capability").GetString()).Should().Equal(
            "durable-persistence-and-outbox", "durable-runtime", "quality-and-verification", "workflow-contracts");
        foreach (var row in contracts)
        {
            var capability = row.GetProperty("capability").GetString()!;
            var heading = row.GetProperty("requirement").GetString()!;
            row.GetProperty("operation").GetString().Should().Be("ADDED");
            var deltaPath = $"openspec/changes/{Change}/specs/{capability}/spec.md";
            var delta = Text(root, deltaPath);
            delta.Should().StartWith("## ADDED Requirements\n\n");
            Regex.Matches(delta, @"(?m)^### Requirement:").Should().ContainSingle();
            Hash(Block(delta, heading)).Should().Be(row.GetProperty("sha256").GetString());
            var canonical = Text(root, $"openspec/specs/{capability}/spec.md");
            if (synchronized) Block(canonical, heading).Should().Be(Block(delta, heading));
            else canonical.Should().NotContain("### Requirement: " + heading);
        }

        var additionsPath = record.GetProperty("numberedAdditions").GetString()!;
        Hash(Text(root, additionsPath)).Should().Be(record.GetProperty("numberedAdditionsSha256").GetString());
        using var additions = Read(root, additionsPath);
        var rows = additions.RootElement.GetProperty("documents").EnumerateArray().ToArray();
        rows.Select(row => row.GetProperty("path").GetString()).Should().Equal(
            "docs/specs/04-requirements-core-runtime.md", "docs/specs/06-requirements-durable-execution.md",
            "docs/specs/10-provider-model-and-extensibility.md", "docs/specs/12-acceptance-criteria.md",
            "docs/specs/16-requirements-durable-driver.md", "docs/specs/17-selected-mode-capability-matrix.md");
        foreach (var row in rows)
        {
            var addition = row.GetProperty("addition").GetString()!;
            Hash(addition).Should().Be(row.GetProperty("additionSha256").GetString());
            var path = row.GetProperty("path").GetString()!;
            var current = Text(root, path);
            if (synchronized) BeforeSynchronization(root, path, current).Should().NotBe(current);
            else current.Should().NotContain(addition);
        }

        var ledger = Text(root, $"openspec/changes/{Change}/tasks.md");
        ledger.Should().MatchRegex(@"(?m)^- \[" + (approved ? "x" : " ") + @"\] 1\.1\b");
        ledger.Should().MatchRegex(@"(?m)^- \[" + (synchronized ? "x" : " ") + @"\] 1\.2\b");
        if (!approved)
        {
            record.GetProperty("contractApproval").ValueKind.Should().Be(JsonValueKind.Null);
            record.GetProperty("synchronization").ValueKind.Should().Be(JsonValueKind.Null);
            return;
        }

        if (!synchronized) record.GetProperty("synchronization").ValueKind.Should().Be(JsonValueKind.Null);

        var approval = record.GetProperty("contractApproval");
        var verdict = Text(root, approval.GetProperty("verdictPath").GetString()!);
        Hash(verdict).Should().Be(approval.GetProperty("verdictSha256").GetString());
        var manifestPath = approval.GetProperty("manifestPath").GetString()!;
        var manifestHash = Convert.ToHexStringLower(SHA256.HashData(File.ReadAllBytes(Path.Combine(root, manifestPath))));
        manifestHash.Should().Be(approval.GetProperty("manifestSha256").GetString());
        ValidateVerdict(verdict, manifestHash);
        using var manifest = Read(root, manifestPath);
        var checkpoint = approval.GetProperty("checkpointCommit").GetString()!;
        checkpoint.Should().MatchRegex("^[0-9a-f]{40}$");
        var baseCommit = manifest.RootElement.GetProperty("base").GetString()!;
        Git(root, "rev-parse", checkpoint + "^").Trim().Should().Be(baseCommit);
        foreach (var file in manifest.RootElement.GetProperty("files").EnumerateArray())
        {
            var bytes = GitBytes(root, "show", checkpoint + ":" + file.GetProperty("path").GetString());
            Convert.ToHexStringLower(SHA256.HashData(bytes)).Should().Be(file.GetProperty("sha256").GetString(),
                "the checkpoint must contain the exact target named by the independent verdict");
        }
        foreach (var row in contracts)
        {
            var path = $"openspec/changes/{Change}/specs/{row.GetProperty("capability").GetString()}/spec.md";
            GitText(root, checkpoint, path).Should().Be(Text(root, path));
        }
        GitText(root, checkpoint, additionsPath).Should().Be(Text(root, additionsPath));
        if (stage == "Complete")
        {
            ledger.Should().MatchRegex(@"(?m)^- \[x\] 2\.1\b");
            ledger.Should().MatchRegex(@"(?m)^- \[x\] 2\.2\b");
            var qualification = record.GetProperty("providerQualification");
            var evidence = Text(root, qualification.GetProperty("path").GetString()!);
            Hash(evidence).Should().Be(qualification.GetProperty("sha256").GetString());
            evidence.Should().Contain("PostgreSQL").And.Contain("SQL Server");
        }
    }

    // Older approval snapshots predate this separately validated additive registry pointer.
    internal static void ProjectRegistryExtension(JsonObject current)
    {
        current["runtimeCorrectnessAmendment"]!.GetValue<string>().Should().Be(RecordPath);
        current.Remove("runtimeCorrectnessAmendment");
    }

    // Strip only this exact admitted addition before older, independently pinned projections.
    // The complete current document and its recovered predecessor are both hash checked.
    internal static string BeforeSynchronization(string root, string path, string current)
    {
        using var document = Read(root, RecordPath);
        var record = document.RootElement;
        if (record.GetProperty("stage").GetString() is "Proposed" or "ContractApproved") return current;
        using var additions = Read(root, record.GetProperty("numberedAdditions").GetString()!);
        var row = additions.RootElement.GetProperty("documents").EnumerateArray()
            .SingleOrDefault(item => item.GetProperty("path").GetString() == path);
        if (row.ValueKind == JsonValueKind.Undefined) return current;
        var projection = record.GetProperty("synchronization").GetProperty("documents").EnumerateArray()
            .Single(item => item.GetProperty("path").GetString() == path);
        Hash(current).Should().Be(projection.GetProperty("afterSha256").GetString());
        var addition = row.GetProperty("addition").GetString()!;
        current.Should().EndWith(addition);
        var prior = current[..^addition.Length];
        Hash(prior).Should().Be(projection.GetProperty("beforeSha256").GetString());
        return prior;
    }

    private static string Block(string text, string heading)
    {
        var start = text.IndexOf("### Requirement: " + heading + "\n", StringComparison.Ordinal);
        start.Should().BeGreaterThanOrEqualTo(0);
        var end = text.IndexOf("\n### Requirement: ", start + 1, StringComparison.Ordinal);
        return text[start..(end < 0 ? text.Length : end)].TrimEnd();
    }

    [Theory]
    [InlineData("rejected")]
    [InlineData("wrong-scope")]
    [InlineData("wrong-target")]
    public void ContractVerdictRejectsAnUnrelatedOrNegativeApproval(string fault)
    {
        var hash = new string('a', 64);
        var verdict = "Verdict: ACCEPT\nScope: repair-runtime-correctness contract\nReviewed manifest SHA-256: " + hash + "\n";
        verdict = fault switch
        {
            "rejected" => verdict.Replace("Verdict: ACCEPT", "Verdict: REQUEST CHANGES\nEarlier targets said ACCEPT."),
            "wrong-scope" => verdict.Replace("Scope: repair-runtime-correctness contract", "Scope: unrelated contract"),
            _ => verdict.Replace(hash, new string('b', 64))
        };
        Action validate = () => ValidateVerdict(verdict, hash);
        validate.Should().Throw<InvalidDataException>();
    }

    private static void ValidateVerdict(string verdict, string manifestHash)
    {
        if (!verdict.StartsWith("Verdict: ACCEPT\n", StringComparison.Ordinal) ||
            !Regex.IsMatch(verdict, @"(?m)^Scope: repair-runtime-correctness contract$") ||
            !Regex.IsMatch(verdict, "(?m)^Reviewed manifest SHA-256: " + Regex.Escape(manifestHash) + "$"))
            throw new InvalidDataException("An affirmative independent contract verdict must name this exact scope and reviewed manifest.");
    }

    private static string GitText(string root, string revision, string path) => Git(root, "show", revision + ":" + path);
    private static string Git(string root, params string[] args) => Encoding.UTF8.GetString(GitBytes(root, args)).Replace("\r\n", "\n");
    private static byte[] GitBytes(string root, params string[] args)
    {
        var start = new System.Diagnostics.ProcessStartInfo("git")
        {
            WorkingDirectory = root, RedirectStandardOutput = true, RedirectStandardError = true, UseShellExecute = false
        };
        foreach (var arg in args) start.ArgumentList.Add(arg);
        using var process = System.Diagnostics.Process.Start(start)!;
        using var bytes = new MemoryStream();
        process.StandardOutput.BaseStream.CopyTo(bytes);
        var error = process.StandardError.ReadToEnd();
        process.WaitForExit();
        process.ExitCode.Should().Be(0, error);
        return bytes.ToArray();
    }

    private static JsonDocument Read(string root, string path) => JsonDocument.Parse(Text(root, path));
    private static string Text(string root, string path) => File.ReadAllText(Path.Combine(root, path)).Replace("\r\n", "\n");
    private static string Hash(string text) => Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(text)));
}
