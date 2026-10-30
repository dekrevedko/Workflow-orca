using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using AwesomeAssertions;

namespace OrcaCore.DeveloperSurface.Guards;

[Trait(GuardTraits.Phase, GuardTraits.Phase0)]
[Trait(GuardTraits.Disposition, GuardTraits.Infrastructure)]
public sealed class CodecMemberCorrectionGuards
{
    private const string Base = "8566986850dc377f5a69e165dd58936be50ad722";
    private const string HarnessCheckpoint = "d8c55f32741f43523e528dd45ad49e6a0fbe7837";
    private const string Prefix = "openspec/changes/support-ephemeral-native-aot/";
    private const string Record = Prefix + "artifacts/codec-member-corrections-2026-10-28.json";
    private const string RecordSha = "e14e591cb641b21fb05fde9bba0369e04f7cd7c507d26fc04aee79b171e8a3d7";
    private const string Approval = Prefix + "artifacts/publication-codec-approval-binding-2026-10-28.json";
    private const string ApprovalSha = "bdc5db130b8e4d6f4c45cc5a63f9c3fe07fac56aed0040d78c1ee089359f1963";

    [Fact]
    public void CodecCorrections_HaveExactScopeAndNoInheritedApproval()
    {
        var root = FixtureDefinitions.RepositoryRoot();
        var bytes = File.ReadAllBytes(Path.Combine(root, Record));
        Hash(bytes).Should().Be(RecordSha);
        using var document = JsonDocument.Parse(bytes);
        var record = document.RootElement;
        record.GetProperty("baseCheckpoint").GetString().Should().Be(Base);
        foreach (var name in new[] { "sourceApprovalEvidencePath", "nativeQualificationEvidencePath" })
            record.GetProperty(name).ValueKind.Should().Be(JsonValueKind.Null);
        record.GetProperty("authorizationQuote").GetString().Should().Be("Fix them");
        var files = record.GetProperty("sourceFiles").EnumerateObject().ToArray();
        files.Select(file => file.Name).Should().Equal(
            "src/OrcaCore.Abstractions/Serialization/FixedWorkflowValueCodec.cs",
            "src/OrcaCore.Abstractions/Serialization/WorkflowJsonMetadataRegistry.cs",
            "src/OrcaCore.Abstractions/Serialization/WorkflowJsonTypeContracts.cs");
        Git(root, "diff", "--name-only", Base, "--", "src")
            .Split('\n', StringSplitOptions.RemoveEmptyEntries).Should().Equal(files.Select(file => file.Name));
        Git(root, "ls-files", "--others", "--exclude-standard", "--", "src").Trim().Should().BeEmpty();
        foreach (var file in files)
            Hash(File.ReadAllBytes(Path.Combine(root, file.Name))).Should().Be(file.Value.GetString());
        foreach (var file in record.GetProperty("harnessFiles").EnumerateObject())
            Hash(Encoding.UTF8.GetBytes(Git(root, "show", HarnessCheckpoint + ":" + file.Name)))
                .Should().Be(file.Value.GetString(), "this receipt describes the approved historical harness");
        using var registry = JsonDocument.Parse(File.ReadAllBytes(Path.Combine(root,
            "tests/OrcaCore.DeveloperSurface.Guards/Fixtures/post-gate-amendment-path.json")));
        var binding = registry.RootElement.GetProperty("nativeSupportContractEvidence").GetProperty("codecMemberCorrections");
        binding.GetProperty("artifactPath").GetString().Should().Be(Record);
        binding.GetProperty("artifactSha256").GetString().Should().Be(RecordSha);
        var guide = File.ReadAllText(Path.Combine(root, "docs/ephemeral-native-support.md"));
        ValidateApproval(root, registry.RootElement, binding, guide);
        record.GetProperty("stage").GetString().Should().Be("NativeRequalifiedPendingReview",
            "the checkpoint's pending-review record remains an immutable execution receipt");
        var matrix = record.GetProperty("nativeMatrix");
        var indexBytes = File.ReadAllBytes(Path.Combine(root, matrix.GetProperty("indexPath").GetString()!));
        Hash(indexBytes).Should().Be(matrix.GetProperty("indexSha256").GetString());
        using var index = JsonDocument.Parse(indexBytes);
        var cells = index.RootElement.GetProperty("cells").EnumerateArray().ToArray();
        cells.Should().HaveCount(6);
        cells.Select(cell => cell.GetProperty("host").GetString() + "/" + cell.GetProperty("flavor").GetString())
            .Should().BeEquivalentTo(new[] { "windows-2025/jit", "windows-2025/trimmed", "windows-2025/aot",
                "ubuntu-24.04/jit", "ubuntu-24.04/trimmed", "ubuntu-24.04/aot" });
        index.RootElement.GetProperty("totalCases").GetInt32().Should().Be(384);
        foreach (var cell in cells)
        {
            var reportBytes = File.ReadAllBytes(Path.Combine(root, cell.GetProperty("reportPath").GetString()!));
            Hash(reportBytes).Should().Be(cell.GetProperty("reportSha256").GetString());
            using var report = JsonDocument.Parse(reportBytes);
            var value = report.RootElement;
            value.GetProperty("protocol").GetInt32().Should().Be(3);
            value.GetProperty("sourceSha").GetString().Should().Be(index.RootElement.GetProperty("sourceCommit").GetString());
            value.GetProperty("host").GetProperty("qualified").GetBoolean().Should().BeTrue();
            value.GetProperty("sourceDirty").GetBoolean().Should().BeFalse();
            value.GetProperty("publishExitCode").GetInt32().Should().Be(0);
            value.GetProperty("liveEventDeliveryQualified").GetBoolean().Should().BeFalse();
            value.GetProperty("outgoingPublishingQualified").GetBoolean().Should().BeFalse();
            var flavor = value.GetProperty("flavor").GetString();
            var cases = value.GetProperty("cases").EnumerateArray().ToArray();
            cases.Should().HaveCount(flavor == "jit" ? 82 : 55);
            cases.Should().OnlyContain(test => test.GetProperty("outcome").GetString() == "Passed" &&
                test.GetProperty("exitCode").GetInt32() == 0);
            cases.Select(test => test.GetProperty("id").GetString()).Should().Contain(new[] {
                "write-only-generated", "write-only-default", "included-members-generated", "included-members-default" });
            if (flavor != "jit") value.GetProperty("jsonReflectionEnabled").GetBoolean().Should().BeFalse();
            if (flavor == "aot") value.GetProperty("dynamicCodeSupported").GetBoolean().Should().BeFalse();
            foreach (var table in new[] { "sourceFiles", "harnessFiles" })
                foreach (var file in value.GetProperty(table).EnumerateObject())
                {
                    var current = table == "sourceFiles" ? File.ReadAllBytes(Path.Combine(root, file.Name)) :
                        Encoding.UTF8.GetBytes(Git(root, "show", HarnessCheckpoint + ":" + file.Name));
                    if (file.Name == ".github/workflows/ephemeral-native-support.yml")
                        current = Encoding.UTF8.GetBytes(Encoding.UTF8.GetString(current).Replace(
                            "on:\n  workflow_dispatch:", "on:\n  push:\n    branches: [codex/issue-13-native-evidence]\n  workflow_dispatch:", StringComparison.Ordinal));
                    Hash(current).Should().Be(file.Value.GetString());
                }
        }
    }

    private static void ValidateApproval(string root, JsonElement registry, JsonElement binding, string guide)
    {
        var approvalBytes = File.ReadAllBytes(Path.Combine(root, Approval));
        Hash(approvalBytes).Should().Be(ApprovalSha);
        var extension = binding.GetProperty("approvalBinding");
        extension.GetProperty("artifactPath").GetString().Should().Be(Approval);
        extension.GetProperty("artifactSha256").GetString().Should().Be(ApprovalSha);
        binding.GetProperty("stage").GetString().Should().Be("SourceAndNativeApprovedPublicationPending");
        using var document = JsonDocument.Parse(approvalBytes);
        var approval = document.RootElement;
        approval.GetProperty("stage").GetString().Should().Be("SourceAndNativeApprovedPublicationPending");
        approval.GetProperty("approvalAuthority").GetString().Should().Be("DirectUserApproval");
        approval.GetProperty("approvalQuote").GetString().Should().Be("Continue");
        approval.GetProperty("reviewKind").GetString().Should().Be("AdversarialAuthorSelfReviewNotExternalApproval");
        foreach (var name in new[] { "sourceApproved", "nativeSupportApproved" })
            approval.GetProperty(name).GetBoolean().Should().BeTrue();
        foreach (var name in new[] { "liveEventDeliveryQualified", "outgoingPublishingQualified", "allPackagesQualified",
                     "publicationAuthorized", "mergeAuthorized", "issueClosureAuthorized", "releaseAuthorized",
                     "issueBranchPushed", "prOpened", "published" })
            approval.GetProperty(name).GetBoolean().Should().BeFalse();
        approval.GetProperty("releaseVersion").ValueKind.Should().Be(JsonValueKind.Null);

        var checkpoint = approval.GetProperty("checkpointCommit").GetString()!;
        var verdictCommit = approval.GetProperty("verdictCommit").GetString()!;
        var verdict = approval.GetProperty("verdictPath").GetString()!;
        Git(root, "show", "-s", "--format=%P", checkpoint).Trim()
            .Should().Be(approval.GetProperty("checkpointParent").GetString());
        Git(root, "show", "-s", "--format=%T", checkpoint).Trim()
            .Should().Be(approval.GetProperty("checkpointTree").GetString());
        Git(root, "show", "-s", "--format=%P", verdictCommit).Trim().Should().Be(checkpoint);
        Git(root, "diff-tree", "--no-commit-id", "--name-only", "-r", verdictCommit).Trim().Should().Be(verdict);
        var verdictBytes = File.ReadAllBytes(Path.Combine(root, verdict));
        verdictBytes.Length.Should().Be(approval.GetProperty("verdictBytes").GetInt32());
        Hash(verdictBytes).Should().Be(approval.GetProperty("verdictSha256").GetString());
        Encoding.UTF8.GetBytes(Git(root, "show", verdictCommit + ":" + verdict)).Should().Equal(verdictBytes);
        _ = Git(root, "merge-base", "--is-ancestor", verdictCommit, "HEAD");
        var manifest = approval.GetProperty("manifestPath").GetString()!;
        var manifestBytes = File.ReadAllBytes(Path.Combine(root, manifest));
        Hash(manifestBytes).Should().Be(approval.GetProperty("manifestSha256").GetString());
        Encoding.UTF8.GetBytes(Git(root, "show", checkpoint + ":" + manifest)).Should().Equal(manifestBytes);
        var paths = Encoding.UTF8.GetString(manifestBytes).Split('\n', StringSplitOptions.RemoveEmptyEntries);
        paths.Should().HaveCount(216).And.OnlyHaveUniqueItems();
        approval.GetProperty("approvedPathCount").GetInt32().Should().Be(paths.Length);
        Git(root, "diff-tree", "--no-commit-id", "--name-only", "-r", checkpoint)
            .Split('\n', StringSplitOptions.RemoveEmptyEntries).Should().Equal(paths);
        approval.GetProperty("executedTargetRecordPath").GetString().Should().Be(Record);
        approval.GetProperty("executedTargetRecordSha256").GetString().Should().Be(RecordSha);
        Hash(Encoding.UTF8.GetBytes(Git(root, "show", checkpoint + ":" + Record))).Should().Be(RecordSha);
        foreach (var table in new[] { "sourceFiles", "harnessFiles" })
            foreach (var file in approval.GetProperty(table).EnumerateObject())
            {
                if (table == "sourceFiles")
                    Hash(File.ReadAllBytes(Path.Combine(root, file.Name))).Should().Be(file.Value.GetString());
                Hash(Encoding.UTF8.GetBytes(Git(root, "show", checkpoint + ":" + file.Name)))
                    .Should().Be(file.Value.GetString());
            }
        var matrix = approval.GetProperty("nativeMatrix");
        var index = matrix.GetProperty("indexPath").GetString()!;
        Hash(File.ReadAllBytes(Path.Combine(root, index))).Should().Be(matrix.GetProperty("indexSha256").GetString());
        Hash(Encoding.UTF8.GetBytes(Git(root, "show", checkpoint + ":" + index)))
            .Should().Be(matrix.GetProperty("indexSha256").GetString());
        approval.GetProperty("publicBoundary").GetString()
            .Should().Be("PublicValuesTypedWaitParkingTimersRetryPoolCompositionOnly");

        foreach (var item in registry.GetProperty("nativeSupportPendingAmendments").EnumerateArray()
                     .Prepend(registry.GetProperty("nativeSupportContractEvidence")))
            foreach (var name in new[] { "sourceApprovalEvidencePath", "nativeQualificationEvidencePath" })
                item.GetProperty(name).GetString().Should().Be(Approval);
        guide.Should().Contain("source-qualified; release pending").And.Contain(checkpoint)
            .And.NotContain("Codec corrections are pending independent review");
    }

    private static string Hash(byte[] bytes) => Convert.ToHexStringLower(SHA256.HashData(bytes));

    private static string Git(string root, params string[] arguments)
    {
        var info = new ProcessStartInfo("git") { WorkingDirectory = root, RedirectStandardOutput = true,
            RedirectStandardError = true, UseShellExecute = false, CreateNoWindow = true };
        foreach (var argument in arguments) info.ArgumentList.Add(argument);
        using var process = Process.Start(info)!;
        var output = process.StandardOutput.ReadToEnd();
        var error = process.StandardError.ReadToEnd();
        process.WaitForExit();
        process.ExitCode.Should().Be(0, error);
        return output;
    }
}
