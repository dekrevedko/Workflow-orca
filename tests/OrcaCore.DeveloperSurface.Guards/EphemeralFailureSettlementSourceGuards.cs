using System.Diagnostics;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using AwesomeAssertions;

namespace OrcaCore.DeveloperSurface.Guards;

[Trait(GuardTraits.Disposition, GuardTraits.Infrastructure)]
public sealed class EphemeralFailureSettlementSourceGuards
{
    [Fact]
    public void PublicationCompletion_BindsTheApprovedChainAndMerge()
    {
        var root = FixtureDefinitions.RepositoryRoot();
        _ = EphemeralFailureSettlementSourceReview.AuthorizedPaths(root);
        EphemeralFailureSettlementSourceReview.ValidatePublicationBinding(root);
        EphemeralFailureSettlementSourceReview.ValidateMergeCompletion(root);
    }
}

internal static class EphemeralFailureSettlementSourceReview
{
    private const string Prefix = "openspec/changes/expose-ephemeral-failures-and-settlement/";
    private const string TaskLedgerPath = Prefix + "tasks.md";
    internal const string Base = "5960f0b5e920e2a53b34e38290d20c6e545fbf2a";
    private const string RegistryHash = "d62cd0d28712b18550216f5b487a42a85b557726a7fe06eccb7d1c7a53e2e282";
    private const string SourceCheckpoint = "5a9147721a0b8a0e7ffecacd07572a6ddebcd0ef";
    private const string SourceVerdict = "8667ea5e7894e7e64859070d7556594e559a4fe6";
    private const string PublicationPath = Prefix + "artifacts/task-4-1-publication-binding-2026-11-08.json";
    private const string PublicationHash = "279f76d6c2335ca634770098d891954aef16d59c7b0d1a91b77d7061620c1d95";

    internal static string[] AuthorizedPaths(string root)
    {
        var bytes = File.ReadAllBytes(Path.Combine(root, Prefix + "artifacts/task-3-source-authorization-2026-11-02.json"));
        Hash(bytes).Should().Be(RegistryHash);
        using var document = JsonDocument.Parse(bytes);
        var record = document.RootElement;
        record.GetProperty("stage").GetString().Should().Be("CanonicalApprovedSourceInProgress");
        record.GetProperty("approvalAuthority").GetString().Should().Be("DirectUserApproval");
        record.GetProperty("sourceImplementationAuthorized").GetBoolean().Should().BeTrue();
        record.GetProperty("sourceApprovalEvidencePath").ValueKind.Should().Be(JsonValueKind.Null);
        record.GetProperty("sourceReviewPending").GetBoolean().Should().BeTrue();
        record.GetProperty("nativeQualificationComplete").GetBoolean().Should().BeFalse();
        record.GetProperty("pushAuthorized").GetBoolean().Should().BeFalse();
        const string checkpoint = "ecd188a4b4ee4d45ecdaddc7112523e20f0cc1b1";
        record.GetProperty("canonicalCheckpoint").GetString().Should().Be(checkpoint);
        record.GetProperty("canonicalParent").GetString().Should().Be("7697c7981c02d03408cbccf9a2e0b9bade33ca14");
        record.GetProperty("canonicalTree").GetString().Should().Be("ae97c26e5c088c3c5430d9fd6644379984d30922");
        record.GetProperty("canonicalVerdictCommit").GetString().Should().Be(Base);
        Git(root, "rev-parse", checkpoint + "^").Trim().Should().Be("7697c7981c02d03408cbccf9a2e0b9bade33ca14");
        Git(root, "rev-parse", checkpoint + "^{tree}").Trim().Should().Be("ae97c26e5c088c3c5430d9fd6644379984d30922");
        Git(root, "rev-parse", Base + "^").Trim().Should().Be(checkpoint);
        var verdictPath = Prefix + "artifacts/task-2-1-independent-review-verdict-2026-11-01.md";
        record.GetProperty("canonicalVerdictPath").GetString().Should().Be(verdictPath);
        Git(root, "diff-tree", "--no-commit-id", "--name-only", "-r", Base).Trim().Should().Be(verdictPath);
        var verdict = File.ReadAllBytes(Path.Combine(root, verdictPath));
        Hash(verdict).Should().Be("96ce82184702d169620da0810dff1c093970375148c4cc531c4daf5259ce0f22");
        Hash(verdict).Should().Be(record.GetProperty("canonicalVerdictSha256").GetString());
        Encoding.UTF8.GetBytes(Git(root, "show", Base + ":" + verdictPath)).Should().Equal(verdict);
        var files = record.GetProperty("sourceFiles").EnumerateObject().ToArray();
        files.Select(file => file.Name).Should().OnlyContain(path => path.StartsWith("src/OrcaCore.Abstractions/", StringComparison.Ordinal) ||
            path.StartsWith("src/OrcaCore.Core/", StringComparison.Ordinal) || path.StartsWith("src/OrcaCore.Engine.Ephemeral/", StringComparison.Ordinal));
        foreach (var file in files)
        {
            Hash(Encoding.UTF8.GetBytes(Git(root, "show", Base + ":" + file.Name))).Should().Be(file.Value.GetProperty("beforeSha256").GetString());
            Hash(File.ReadAllBytes(Path.Combine(root, file.Name))).Should().Be(file.Value.GetProperty("afterSha256").GetString());
        }
        Git(root, "diff", "--name-only", Base, "--", "src").Split('\n', StringSplitOptions.RemoveEmptyEntries).Should().Equal(files.Select(file => file.Name));
        Git(root, "ls-files", "--others", "--exclude-standard", "--", "src").Should().BeEmpty();
        var tasks = File.ReadAllText(Path.Combine(root, TaskLedgerPath));
        foreach (var task in new[] { "2.1", "2.2" }) tasks.Should().Contain("- [x] " + task + " ");
        var checkpointTasks = CheckpointTaskLedger(root);
        foreach (var task in new[] { "3.1", "3.2", "3.3", "3.4", "3.5", "3.6" })
        {
            checkpointTasks.Should().Contain("- [ ] " + task + " ");
            tasks.Should().Contain("- [x] " + task + " ");
        }

        return files.Select(file => file.Name).ToArray();
    }

    internal static string CheckpointTaskLedger(string root) =>
        Git(root, "show", SourceCheckpoint + ":" + TaskLedgerPath);

    internal static string PublicationDocumentationHash(string root, string path, string priorHash)
    {
        using var document = ReadPublicationBinding(root);
        if (!document.RootElement.GetProperty("documentationFiles").TryGetProperty(path, out var pin))
            return priorHash;

        pin.GetProperty("beforeSha256").GetString().Should().Be(priorHash);
        DocumentationHash(Git(root, "show", SourceCheckpoint + ":" + path)).Should().Be(priorHash);
        return pin.GetProperty("afterSha256").GetString()!;
    }

    internal static void ValidatePublicationBinding(string root)
    {
        using var document = ReadPublicationBinding(root);
        var binding = document.RootElement;
        binding.GetProperty("schemaVersion").GetInt32().Should().Be(1);
        binding.GetProperty("stage").GetString().Should().Be("Task3ApprovedPublicationReviewPending");
        binding.GetProperty("owningIssue").GetInt32().Should().Be(16);
        binding.GetProperty("approvalAuthority").GetString().Should().Be("DirectUserApproval");
        foreach (var field in new[] { "sourceApproved", "nativeQualificationComplete", "publicationReviewPending" })
            binding.GetProperty(field).GetBoolean().Should().BeTrue();
        foreach (var field in new[] { "pushAuthorized", "pullRequestAuthorized", "mergeAuthorized",
                     "issueClosureAuthorized", "releaseAuthorized" })
            binding.GetProperty(field).GetBoolean().Should().BeFalse();
        binding.GetProperty("futureReleaseVersion").ValueKind.Should().Be(JsonValueKind.Null);

        binding.GetProperty("checkpointCommit").GetString().Should().Be(SourceCheckpoint);
        binding.GetProperty("checkpointParent").GetString().Should().Be(Base);
        binding.GetProperty("checkpointTree").GetString().Should().Be("ed06e4888769fc4fceae8cf08122f25c722d0586");
        binding.GetProperty("approvedPathCount").GetInt32().Should().Be(69);
        Git(root, "rev-parse", SourceCheckpoint + "^").Trim().Should().Be(Base);
        Git(root, "rev-parse", SourceCheckpoint + "^{tree}").Trim().Should().Be(binding.GetProperty("checkpointTree").GetString());
        Git(root, "diff-tree", "--no-commit-id", "--name-only", "-r", SourceCheckpoint)
            .Split('\n', StringSplitOptions.RemoveEmptyEntries).Should().HaveCount(69);

        binding.GetProperty("verdictCommit").GetString().Should().Be(SourceVerdict);
        var verdictPath = binding.GetProperty("verdictPath").GetString()!;
        Git(root, "rev-parse", SourceVerdict + "^").Trim().Should().Be(SourceCheckpoint);
        Git(root, "diff-tree", "--no-commit-id", "--name-only", "-r", SourceVerdict).Trim().Should().Be(verdictPath);
        _ = Git(root, "merge-base", "--is-ancestor", SourceVerdict, "HEAD");
        var verdict = File.ReadAllBytes(Path.Combine(root, verdictPath));
        Hash(verdict).Should().Be(binding.GetProperty("verdictSha256").GetString());
        Encoding.UTF8.GetBytes(Git(root, "show", SourceVerdict + ":" + verdictPath)).Should().Equal(verdict);

        var docs = binding.GetProperty("documentationFiles").EnumerateObject().ToArray();
        docs.Select(file => file.Name).Should().Equal("README.md", "docs/ephemeral-engine-developer-guide.md",
            "docs/ephemeral-native-support.md", "docs/packages.md");
        foreach (var file in docs)
        {
            DocumentationHash(Git(root, "show", SourceCheckpoint + ":" + file.Name))
                .Should().Be(file.Value.GetProperty("beforeSha256").GetString());
            DocumentationHash(File.ReadAllText(Path.Combine(root, file.Name)))
                .Should().Be(file.Value.GetProperty("afterSha256").GetString());
        }

        var pr = binding.GetProperty("pullRequest");
        pr.GetProperty("baseBranch").GetString().Should().Be("master");
        pr.GetProperty("headBranch").GetString().Should().Be("codex/issue-16-ephemeral-failure-settlement");
        var body = File.ReadAllBytes(Path.Combine(root, pr.GetProperty("bodyPath").GetString()!));
        Hash(body).Should().Be(pr.GetProperty("bodySha256").GetString());
        Encoding.UTF8.GetString(body).Should().Contain("Closes #16");

        ValidateNativeEvidence(root, binding.GetProperty("nativeQualification"));
    }

    internal static void ValidateMergeCompletion(string root)
    {
        const string receiptPath = Prefix + "artifacts/task-4-2-merge-completion-2026-11-08.json";
        const string receiptHash = "87d42417487aab18a34a8a33db4c805dc0c8223cc297bcc50e03558f595fba56";
        const string checkpoint = "884a753cb24848ca1de1f4156e488f4f3ee5dc45";
        const string verdict = "184ce3e940f5c0f033a2ab173256e4758141d81b";
        const string merge = "893ca34e205e182c97e08cfda353c6765ab6208d";
        var bytes = File.ReadAllBytes(Path.Combine(root, receiptPath));
        Hash(bytes).Should().Be(receiptHash);
        using var document = JsonDocument.Parse(bytes);
        var record = document.RootElement;
        record.GetProperty("schemaVersion").GetInt32().Should().Be(1);
        record.GetProperty("stage").GetString().Should().Be("MergedIssueClosed");
        record.GetProperty("releaseAuthorized").GetBoolean().Should().BeFalse();
        record.GetProperty("syncedMaster").GetString().Should().Be(merge);

        var pr = record.GetProperty("pr");
        pr.GetProperty("number").GetInt32().Should().Be(18);
        pr.GetProperty("state").GetString().Should().Be("MERGED");
        pr.GetProperty("headRefOid").GetString().Should().Be(verdict);
        pr.GetProperty("mergeCommit").GetProperty("oid").GetString().Should().Be(merge);
        var issue = record.GetProperty("issue");
        issue.GetProperty("number").GetInt32().Should().Be(16);
        issue.GetProperty("state").GetString().Should().Be("CLOSED");
        issue.GetProperty("stateReason").GetString().Should().Be("COMPLETED");
        var ci = record.GetProperty("prCi");
        ci.GetProperty("headSha").GetString().Should().Be(verdict);
        ci.GetProperty("conclusion").GetString().Should().Be("success");
        ci.GetProperty("jobs").EnumerateArray().Should().HaveCount(3).And
            .OnlyContain(job => job.GetProperty("conclusion").GetString() == "success");

        record.GetProperty("publicationCheckpoint").GetString().Should().Be(checkpoint);
        record.GetProperty("publicationCheckpointParent").GetString().Should().Be(SourceVerdict);
        Git(root, "show", "-s", "--format=%P %T", checkpoint).Trim().Should().Be(
            SourceVerdict + " " + record.GetProperty("publicationCheckpointTree").GetString());
        Hash(Encoding.UTF8.GetBytes(Git(root, "show", checkpoint + ":" + PublicationPath))).Should().Be(PublicationHash);

        record.GetProperty("publicationVerdictCommit").GetString().Should().Be(verdict);
        Git(root, "show", "-s", "--format=%P", verdict).Trim().Should().Be(checkpoint);
        var verdictPath = record.GetProperty("publicationVerdictPath").GetString()!;
        Git(root, "diff", "--name-status", checkpoint, verdict).TrimEnd('\r', '\n').Should().Be("A\t" + verdictPath);
        var verdictBytes = File.ReadAllBytes(Path.Combine(root, verdictPath));
        Hash(verdictBytes).Should().Be(record.GetProperty("publicationVerdictSha256").GetString());
        Encoding.UTF8.GetBytes(Git(root, "show", verdict + ":" + verdictPath)).Should().Equal(verdictBytes);

        record.GetProperty("mergeCommit").GetString().Should().Be(merge);
        var parents = record.GetProperty("mergeParents").EnumerateArray().Select(parent => parent.GetString()).ToArray();
        parents.Should().Equal("0a8e375fbcf5005a2a2f491cf24dfbbd9d64c580", verdict);
        Git(root, "show", "-s", "--format=%P", merge).Trim().Should().Be(string.Join(" ", parents));
        var mergeTree = record.GetProperty("mergeTree").GetString();
        Git(root, "rev-parse", merge + "^{tree}").Trim().Should().Be(mergeTree);
        Git(root, "rev-parse", verdict + "^{tree}").Trim().Should().Be(mergeTree);
        _ = Git(root, "merge-base", "--is-ancestor", merge, "HEAD");

        var ledger = Git(root, "show", checkpoint + ":" + TaskLedgerPath);
        foreach (var task in new[] { "4.1", "4.2" })
        {
            var open = "- [ ] " + task + " ";
            ledger.Split(open).Should().HaveCount(2);
            ledger = ledger.Replace(open, "- [x] " + task + " ", StringComparison.Ordinal);
        }

        File.ReadAllText(Path.Combine(root, TaskLedgerPath)).Replace("\r\n", "\n", StringComparison.Ordinal)
            .Should().Be(ledger, "completion only ticks the two publication checkboxes");
    }

    private static void ValidateNativeEvidence(string root, JsonElement qualification)
    {
        var evidencePath = qualification.GetProperty("evidencePath").GetString()!;
        var fullPath = Path.Combine(root, evidencePath);
        Hash(File.ReadAllBytes(fullPath)).Should().Be(qualification.GetProperty("evidenceSha256").GetString());
        Git(root, "hash-object", "--no-filters", fullPath).Trim()
            .Should().Be(Git(root, "rev-parse", SourceCheckpoint + ":" + evidencePath).Trim());

        using var archive = ZipFile.OpenRead(fullPath);
        using var receiptStream = archive.GetEntry("receipt.json")!.Open();
        using var receipt = JsonDocument.Parse(receiptStream);
        var run = receipt.RootElement;
        run.GetProperty("qualified").GetBoolean().Should().BeTrue();
        run.GetProperty("runConclusion").GetString().Should().Be("success");
        run.GetProperty("matrixReplayError").ValueKind.Should().Be(JsonValueKind.Null);
        run.GetProperty("runId").GetInt64().Should().Be(qualification.GetProperty("runId").GetInt64()).And.Be(42554435708);
        run.GetProperty("validationCommit").GetString().Should().Be(qualification.GetProperty("sourceCommit").GetString());
        run.GetProperty("sourceTree").GetString().Should().Be(qualification.GetProperty("sourceTree").GetString());
        run.GetProperty("packageVersion").GetString().Should().Be("0.0.0-phase0").And.Be(qualification.GetProperty("packageVersion").GetString());
        run.GetProperty("packages").EnumerateObject().Should().HaveCount(12);
        var aggregate = run.GetProperty("aggregateJob");
        aggregate.GetProperty("id").GetInt64().Should().Be(qualification.GetProperty("aggregateJobId").GetInt64());
        aggregate.GetProperty("status").GetString().Should().Be("completed");
        aggregate.GetProperty("conclusion").GetString().Should().Be("success");
        aggregate.GetProperty("head_sha").GetString().Should().Be(qualification.GetProperty("sourceCommit").GetString());
        var cells = run.GetProperty("cells").EnumerateArray().ToArray();
        cells.Select(cell => cell.GetProperty("host").GetString() + "/" + cell.GetProperty("profile").GetString())
            .Should().BeEquivalentTo(new[] { "windows-2025/jit", "windows-2025/trimmed", "windows-2025/aot",
                "ubuntu-24.04/jit", "ubuntu-24.04/trimmed", "ubuntu-24.04/aot" });
        cells.Should().HaveCount(qualification.GetProperty("cells").GetInt32());
        cells.Sum(cell => cell.GetProperty("passed").GetInt32())
            .Should().Be(qualification.GetProperty("totalCases").GetInt32()).And.Be(392);
        cells.Sum(cell => cell.GetProperty("diagnostics").GetArrayLength())
            .Should().Be(qualification.GetProperty("diagnosticPins").GetInt32()).And.Be(24);
        foreach (var cell in cells)
        {
            var profile = cell.GetProperty("profile").GetString();
            cell.GetProperty("conclusion").GetString().Should().Be("success");
            cell.GetProperty("caseCount").GetInt32().Should().Be(profile == "jit" ? 84 : 56);
            cell.GetProperty("passed").GetInt32().Should().Be(cell.GetProperty("caseCount").GetInt32());
            if (profile != "jit") cell.GetProperty("reflection").GetBoolean().Should().BeFalse();
            if (profile == "aot") cell.GetProperty("dynamicCode").GetBoolean().Should().BeFalse();
        }

        // The aggregate verifies one set of source and harness hashes across all cells.
        using var cellArchive = new ZipArchive(archive.GetEntry("raw/issue-13-windows-2025-jit.zip")!.Open());
        using var reportStream = cellArchive.GetEntry("support-report.json")!.Open();
        using var report = JsonDocument.Parse(reportStream);
        var representative = report.RootElement;
        representative.GetProperty("protocol").GetInt32().Should().Be(qualification.GetProperty("protocol").GetInt32()).And.Be(4);
        representative.GetProperty("sourceSha").GetString().Should().Be(qualification.GetProperty("sourceCommit").GetString());
        representative.GetProperty("sourceTree").GetString().Should().Be(qualification.GetProperty("sourceTree").GetString());
        foreach (var table in new[] { "sourceFiles", "harnessFiles" })
        {
            var files = representative.GetProperty(table).EnumerateObject().ToArray();
            files.Should().HaveCount(table == "sourceFiles" ? 360 : 23);
            foreach (var file in files)
                Hash(File.ReadAllBytes(Path.Combine(root, file.Name))).Should().Be(file.Value.GetString());
        }
    }

    private static JsonDocument ReadPublicationBinding(string root)
    {
        var bytes = File.ReadAllBytes(Path.Combine(root, PublicationPath));
        Hash(bytes).Should().Be(PublicationHash);
        return JsonDocument.Parse(bytes);
    }

    private static string DocumentationHash(string text) =>
        Hash(Encoding.UTF8.GetBytes(text.Replace("\r\n", "\n", StringComparison.Ordinal)));

    internal static string PriorApi(string value)
    {
        const string property = "  property [System.Private.CoreLib]System.String? ExceptionType { get:public } attributes [[System.Private.CoreLib]System.Runtime.CompilerServices.NullableAttribute(2)]\n";
        const string method = "  method public WaitForSettlementAsync([System.Private.CoreLib]System.Threading.CancellationToken cancellationToken = null) -> [System.Private.CoreLib]System.Threading.Tasks.ValueTask\n";
        value.Split(property).Should().HaveCount(2);
        value.Split(method).Should().HaveCount(2);
        return value.Replace(property, "", StringComparison.Ordinal).Replace(method, "", StringComparison.Ordinal);
    }

    internal static byte[] HistoricalBytes(string root, string path)
    {
        var bytes = File.ReadAllBytes(Path.Combine(root, Prefix + "artifacts/task-3-source-authorization-2026-11-02.json"));
        Hash(bytes).Should().Be(RegistryHash);
        using var document = JsonDocument.Parse(bytes);
        var current = File.ReadAllBytes(Path.Combine(root, path));
        if (!document.RootElement.GetProperty("sourceFiles").TryGetProperty(path, out var pin)) return current;
        Hash(current).Should().Be(pin.GetProperty("afterSha256").GetString());
        var before = Encoding.UTF8.GetBytes(Git(root, "show", Base + ":" + path));
        Hash(before).Should().Be(pin.GetProperty("beforeSha256").GetString());
        return before;
    }

    private static string Hash(byte[] bytes) => Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();
    private static string Git(string root, params string[] arguments)
    {
        var info = new ProcessStartInfo("git") { WorkingDirectory = root, RedirectStandardOutput = true, RedirectStandardError = true,
            StandardOutputEncoding = Encoding.UTF8, StandardErrorEncoding = Encoding.UTF8, UseShellExecute = false, CreateNoWindow = true };
        foreach (var argument in arguments) info.ArgumentList.Add(argument);
        using var process = Process.Start(info) ?? throw new InvalidOperationException("Cannot verify source authority through Git.");
        var output = process.StandardOutput.ReadToEndAsync(); var error = process.StandardError.ReadToEndAsync();
        process.WaitForExit();
        process.ExitCode.Should().Be(0, error.GetAwaiter().GetResult());
        return output.GetAwaiter().GetResult();
    }
}
