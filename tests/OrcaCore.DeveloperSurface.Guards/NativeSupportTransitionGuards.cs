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
public sealed class NativeSupportTransitionGuards
{
    private const string Prefix = "openspec/changes/support-ephemeral-native-aot/";
    private const string Registry = "tests/OrcaCore.DeveloperSurface.Guards/Fixtures/post-gate-amendment-path.json";
    private const string Checkpoint = "6122025828c7e8e3525ad8f2e24048024cbbb2d5";
    private const string Parent = "3b7f98aa0748a4d9c3f54d4604e566ed61f21bad";
    private const string Tree = "2b981a667a0c08aca7215118e73f0e40b91db8ac";
    private const string VerdictCommit = "10aec46e4a1f2f0771f6a5418b4dc6ef3ff3b5fd";
    private const string Verdict = Prefix + "artifacts/contract-r1-independent-review-verdict-2026-10-23.md";
    private const string VerdictSha = "4389efe8e8d58366e6064ba6cbd6d5d55a5fcf77d111d21759cdbedf3b61a554";
    private const string Binding = Prefix + "artifacts/task-2-1-contract-binding-2026-10-24.md";
    private const string BindingSha = "0b7b6fda7ad1c52e0420261bd48b7c3f0b9e6719a7179132829c11fb2873b4d6";
    private const string ApprovedStage = "CanonicalSourceAndNativeApprovedPublicationReviewPending";
    private const string Stage = "CanonicalApprovedCodecCorrectionsPendingReview";
    private const string SourceApproval = "openspec/changes/support-ephemeral-native-aot/artifacts/task-2-6-user-approval-verdict-2026-10-27.md";
    private const string CatalogueBinding = "openspec/changes/support-ephemeral-native-aot/artifacts/task-2-2-catalogue-approval-binding-2026-10-25.json";
    private const string CatalogueBindingSha = "efc13e6b51aa50bdeb41997471177a94a23bcdd6e1fe4bdb85be42af5be92181";
    private static readonly Dictionary<string, string> CatalogueDeltaHashes = new(StringComparer.Ordinal)
    {
        ["developer-facing-surface"] = "76662f6bff9137bfbc865d2c44bfb456ed6e7a895f0d14b2e5b8960ae3d68413",
        ["quality-and-verification"] = "2b984062dedc9be864f8620e9be53521620d64343ea0ed61b69fffad3997644b",
        ["workflow-contracts"] = "e0d4f9322935d73b645384eb04f25514feed9ca8300a90388bac92e4882f225a",
    };
    private const string ThrottleBinding = "openspec/changes/support-ephemeral-native-aot/artifacts/task-2-3-throttle-approval-binding-2026-10-26.json";
    private const string ThrottleBindingSha = "39cc08fd67953977a309cfa3d1aa662a59bdafa04e365147225c861cef5138a3";
    private static readonly Dictionary<string, string> ThrottleDeltaHashes = new(StringComparer.Ordinal)
    {
        ["developer-facing-surface"] = "a448d9b9489c2bf0bb952c3ed04670a6eb44712ff6f5d1526b05329f7ce23d03",
        ["structured-fiber-execution"] = "12e3a971faab7d5368f4f61234f379ef4d5d0f27500fce84631df38081df8970",
        ["quality-and-verification"] = "bf2e37f53409b22ea1fa2f178112bd8141b3f8a318341bf9a3d5d79b08b2eab0",
    };
    private const string BoundaryBinding = "openspec/changes/support-ephemeral-native-aot/artifacts/task-2-4-boundary-approval-binding-2026-10-27.json";
    private const string BoundaryBindingSha = "fecfe2efeb5c0d1c675f01ad8a13131891a6631b3647f27d519a2271a50e9512";
    private static readonly Dictionary<string, string> BoundaryDeltaHashes = new(StringComparer.Ordinal)
    {
        ["structured-fiber-execution"] = "0c5ef301edd14633c98204031026b1175bf997bee0fbbc93e4b55881a7d0dca3",
        ["quality-and-verification"] = "82089bd834be4348ed7aab632cde1e37cdff67e9967e9e0d0217e56de88ca4bc",
    };
    private static string? CurrentDeltaHash(string capability) =>
        BoundaryDeltaHashes.TryGetValue(capability, out var boundary) ? boundary :
        ThrottleDeltaHashes.TryGetValue(capability, out var current) ? current : CatalogueDeltaHashes.GetValueOrDefault(capability);
    private static readonly string[] Capabilities =
        ["developer-facing-surface", "quality-and-verification", "structured-fiber-execution", "workflow-contracts"];

    [Fact]
    public void ContractBinding_ResolvesTheApprovedTreeAndSoleVerdictChildThroughGit()
    {
        using var document = Load();
        var evidence = document.RootElement.GetProperty("nativeSupportContractEvidence");
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
        Hash(bytes).Should().Be(VerdictSha);
        Encoding.UTF8.GetBytes(Git("show", VerdictCommit + ":" + Verdict)).Should().Equal(bytes);
        Read(Verdict).Should().Contain("## Verdict: APPROVE (contract only)");
        var manifest = Prefix + "artifacts/contract-r1-manifest-2026-10-23.txt";
        S(evidence, "manifestPath").Should().Be(manifest);
        var paths = Read(manifest).Split('\n', StringSplitOptions.RemoveEmptyEntries);
        paths.Should().HaveCount(29).And.OnlyHaveUniqueItems();
        Git("diff-tree", "--no-commit-id", "--name-only", "-r", Checkpoint)
            .Split('\n', StringSplitOptions.RemoveEmptyEntries).Should().Equal(paths);
        S(evidence, "artifactPath").Should().Be(Binding);
        S(evidence, "artifactNormalizedSha256").Should().Be(BindingSha);
        Hash(Encoding.UTF8.GetBytes(Read(Binding))).Should().Be(BindingSha);
    }

    [Fact]
    public void RegistryExtension_PreservesEveryPriorRecordAndBindsSourceApproval()
    {
        using var document = Load();
        var root = document.RootElement;
        root.GetProperty("schemaVersion").GetInt32().Should().Be(22);
        var old = JsonNode.Parse(Git("show", VerdictCommit + ":" + Registry));
        var current = JsonNode.Parse(root.GetRawText())!.AsObject();
        current.Remove("nativeSupportContractEvidence");
        current.Remove("nativeSupportPendingAmendments");
        current["schemaVersion"] = 19;
        JsonNode.DeepEquals(old, current).Should().BeTrue("only the two specifically validated native-support keys extend the prior registry");
        var evidence = root.GetProperty("nativeSupportContractEvidence");
        S(evidence, "stage").Should().Be(Stage);
        S(evidence, "canonicalPreparationTask").Should().Be("2.1");
        S(evidence, "implementationReviewTask").Should().Be("2.5");
        ValidateImplementationApproval(root);
        foreach (var key in new[] { "sourceApprovalEvidencePath", "nativeQualificationEvidencePath" })
            evidence.GetProperty(key).ValueKind.Should().Be(JsonValueKind.Null,
                "the base checkpoint approval does not approve the codec corrections");
        var records = root.GetProperty("nativeSupportPendingAmendments").EnumerateArray().ToArray();
        records.Select(x => S(x, "capability")).Should().Equal(Capabilities);
        foreach (var record in records)
        {
            var capability = S(record, "capability");
            S(record, "id").Should().Be("ephemeral-native-support-" + capability);
            S(record, "owningChange").Should().Be("support-ephemeral-native-aot");
            S(record, "stage").Should().Be(Stage);
            record.GetProperty("pendingCanonicalOperationCount").GetInt32().Should().Be(0);
            var approved = Blocks(Git("show", Checkpoint + ":" + Prefix + "specs/" + capability + "/spec.md"));
            record.GetProperty("declaredRequirementCount").GetInt32().Should().Be(approved.Length);
            record.GetProperty("requirementIdentities").EnumerateArray().Select(x => x.GetString()).Should()
                .Equal(approved.Select(x => x.Split('\n')[0]["### Requirement: ".Length..]));
            S(record, "contractApprovalEvidencePath").Should().Be(Verdict);
            S(record, "canonicalPreparationTask").Should().Be("2.1");
            S(record, "canonicalReviewTask").Should().Be("2.5");
            record.GetProperty("sourceTasks").EnumerateArray().Select(x => x.GetString())
                .Should().Equal("2.2", "2.3", "2.4", "2.5", "2.6");
            S(record, "canonicalApprovalEvidencePath").Should().Be(SourceApproval);
            foreach (var key in new[] { "sourceApprovalEvidencePath", "nativeQualificationEvidencePath" })
                record.GetProperty(key).ValueKind.Should().Be(JsonValueKind.Null);
        }
        var tasks = Read(Prefix + "tasks.md");
        foreach (var task in new[] { "1.1", "1.2", "1.3", "1.4" })
            tasks.Should().Contain("- [x] " + task + " ");
        tasks.Should().Contain("- [x] 2.6 ");
        foreach (var task in new[] { "3.1", "3.2" })
            tasks.Should().Contain("- [ ] " + task + " ");
    }

    [Fact]
    public void SevenCanonicalAdditions_AreTheExactApprovedDeltasAndPreserveEveryPriorLine()
    {
        using var registry = Load();
        var boundary = registry.RootElement.GetProperty("nativeSupportContractEvidence").GetProperty("publicConsumerBoundaryAmendment");
        S(boundary, "artifactPath").Should().Be(BoundaryBinding);
        S(boundary, "artifactSha256").Should().Be(BoundaryBindingSha);
        Hash(File.ReadAllBytes(PathOf(BoundaryBinding))).Should().Be(BoundaryBindingSha);
        using var approval = JsonDocument.Parse(Read(BoundaryBinding));
        S(approval.RootElement, "approvalAuthority").Should().Be("DirectUserApproval");
        S(approval.RootElement, "approvalQuote").Should().Be("Approve the bounded public recipe");
        S(approval.RootElement, "authorizedRemoteBranch").Should().Be("codex/issue-13-native-evidence");
        Hash(File.ReadAllBytes(PathOf(S(approval.RootElement, "amendmentPath")))).Should().Be(S(approval.RootElement, "amendmentSha256"));
        foreach (var delta in BoundaryDeltaHashes)
        {
            S(approval.RootElement.GetProperty("currentDeltaSha256"), delta.Key).Should().Be(delta.Value);
            Hash(File.ReadAllBytes(PathOf(Prefix + "specs/" + delta.Key + "/spec.md"))).Should().Be(delta.Value);
        }
        approval.RootElement.GetProperty("sourceApprovalEvidencePath").ValueKind.Should().Be(JsonValueKind.Null);
        approval.RootElement.GetProperty("nativeQualificationEvidencePath").ValueKind.Should().Be(JsonValueKind.Null);

        var count = 0;
        foreach (var capability in Capabilities)
        {
            var canonicalPath = "openspec/specs/" + capability + "/spec.md";
            var deltaPath = Prefix + "specs/" + capability + "/spec.md";
            var prior = Normalize(Git("show", VerdictCommit + ":" + canonicalPath));
            var canonical = Read(canonicalPath);
            canonical.Should().StartWith(prior);
            var approved = Blocks(Git("show", Checkpoint + ":" + deltaPath));
            if (CurrentDeltaHash(capability) is { } amendedHash)
            {
                Hash(File.ReadAllBytes(PathOf(deltaPath))).Should().Be(amendedHash,
                    "only the exact explicitly approved catalogue amendment can replace this R1 layer");
                approved = Blocks(Read(deltaPath));
            }
            else Blocks(Read(deltaPath)).Should().Equal(approved);
            Blocks(canonical[prior.Length..]).Should().Equal(approved);
            foreach (var block in approved)
                Blocks(canonical).Count(x => x == block).Should().Be(1);
            count += approved.Length;
        }
        count.Should().Be(7);
    }

    [Fact]
    public void NumberedOwnersAndMatrix_PreserveTheExactReviewedApiAndF1P3Obligations()
    {
        Read("docs/specs/11-non-functional-requirements.md").Should().Contain("NF-034");
        var acceptance = Read("docs/specs/12-acceptance-criteria.md");
        for (var id = 563; id <= 574; id++)
            Regex.Matches(acceptance, @"(?m)^- \*\*AC-" + id + @"\*\*").Should().ContainSingle();
        var matrix = Read("docs/specs/17-selected-mode-capability-matrix.md");
        const string api = "public static class WorkflowJsonMetadata\n{\n    public static void Register<TContext>()\n        where TContext : System.Text.Json.Serialization.JsonSerializerContext, new();\n}";
        matrix.Should().Contain("```csharp\n" + api + "\n```");
        matrix.Should().Contain("source-qualified; release pending").And.Contain("IsMemberInitializer=true")
            .And.Contain("SFE-TYPE-002").And.Contain("SFE-TYPE-003");
        matrix.Should().Contain("TerminalRetention", "the existing host policy signature remains part of document 17");
        var protocol = Read(Prefix + "artifacts/contract-r1-consumer-protocol-2026-10-23.md");
        foreach (var suffix in new[] { "02a", "02b", "02c", "06a", "06b", "06c", "06d", "06e", "06f" })
            protocol.Should().Contain("| NS-" + suffix + " |");
        protocol.Should().Contain("UseStringEnumConverter=true").And.Contain("handwritten")
            .And.Contain("IsMemberInitializer is true AND")
            .And.Contain("both submitted 0 (omitted) and 7 (present)");
    }

    [Fact]
    public void CatalogueAmendment_BindsTheExplicitUserApprovalAndImmutableReview()
    {
        using var matrixRegistry = Load();
        var matrix = matrixRegistry.RootElement.GetProperty("nativeSupportContractEvidence").GetProperty("nativeMatrixExecution");
        S(matrix, "stage").Should().Be("ExecutionAndWholeTargetApproved");
        const string indexPath = "openspec/changes/support-ephemeral-native-aot/artifacts/native-task-2-4-2026-10-27/index.json";
        S(matrix, "artifactPath").Should().Be(indexPath);
        S(matrix, "artifactSha256").Should().Be("4d5ad4ef7480daa14296347ef06ba57922ceed46d9f648ee0fcc60e90f99daeb");
        Hash(File.ReadAllBytes(PathOf(indexPath))).Should().Be(S(matrix, "artifactSha256"));
        S(matrix, "reviewPath").Should().Be("openspec/changes/support-ephemeral-native-aot/artifacts/task-2-4-native-matrix-independent-review-verdict-2026-10-27.md");
        S(matrix, "reviewSha256").Should().Be("5f90fb364e10a080c068cf4941244552a34f2f53bbdc8f220679751822cdcd85");
        Hash(File.ReadAllBytes(PathOf(S(matrix, "reviewPath")))).Should().Be(S(matrix, "reviewSha256"));
        using var matrixIndex = JsonDocument.Parse(Read(indexPath));
        var index = matrixIndex.RootElement;
        index.GetProperty("runId").GetInt64().Should().Be(40629471843);
        S(index, "sourceCommit").Should().Be("609f1045db0f863b43be58843654f03cecb47d56");
        index.GetProperty("totalCases").GetInt32().Should().Be(356);
        index.GetProperty("globalSourceApproval").GetBoolean().Should().BeFalse();
        index.GetProperty("globalNativeSupportApproval").GetBoolean().Should().BeFalse();
        index.GetProperty("liveEventDeliveryQualified").GetBoolean().Should().BeFalse();
        index.GetProperty("outgoingPublishingQualified").GetBoolean().Should().BeFalse();
        foreach (var file in index.GetProperty("files").EnumerateObject())
            Hash(File.ReadAllBytes(PathOf(Prefix + "artifacts/native-task-2-4-2026-10-27/" + file.Name)))
                .Should().Be(S(file.Value, "sha256"));
        var cells = index.GetProperty("cells").EnumerateArray().ToArray();
        cells.Should().HaveCount(6);
        cells.Select(cell => S(cell, "host") + "/" + S(cell, "flavor")).Should().OnlyHaveUniqueItems();
        foreach (var cell in cells)
        {
            using var report = JsonDocument.Parse(Read(S(cell, "reportPath")));
            S(report.RootElement, "sourceSha").Should().Be(S(index, "sourceCommit"));
            foreach (var table in new[] { "sourceFiles", "harnessFiles" })
                foreach (var file in report.RootElement.GetProperty(table).EnumerateObject())
                {
                    if (file.Name == ".github/workflows/ephemeral-native-support.yml")
                    {
                        var tested = Git("show", "8566986850dc377f5a69e165dd58936be50ad722:" + file.Name);
                        Hash(Encoding.UTF8.GetBytes(tested)).Should().Be(file.Value.GetString());
                        Read(file.Name).Should().Be(Normalize(tested).Replace(
                            "  push:\n    branches: [codex/issue-13-native-evidence]\n", "", StringComparison.Ordinal),
                            "publication removes only the temporary trigger; every tested job/harness byte is preserved");
                    }
                    else Hash(Encoding.UTF8.GetBytes(Git("show", "8566986850dc377f5a69e165dd58936be50ad722:" + file.Name)))
                        .Should().Be(file.Value.GetString(), "protocol-2 evidence remains bound to its approved historical source");
                }
        }

        using var registry = Load();
        var record = registry.RootElement.GetProperty("nativeSupportContractEvidence").GetProperty("catalogueAmendment");
        S(record, "artifactPath").Should().Be(CatalogueBinding);
        S(record, "artifactSha256").Should().Be(CatalogueBindingSha);
        Hash(File.ReadAllBytes(PathOf(CatalogueBinding))).Should().Be(CatalogueBindingSha);
        using var document = JsonDocument.Parse(Read(CatalogueBinding));
        var binding = document.RootElement;
        S(binding, "approvalAuthority").Should().Be("DirectUserApproval");
        S(binding, "approvedContentSha256").Should().Be("477b88fde7bc4a1320cac82cdfa4fecab4e7c09c852fa3e517dd6f8a202cd8b5");
        foreach (var kind in new[] { "amendment", "verdict" })
            Hash(File.ReadAllBytes(PathOf(S(binding, kind + "Path")))).Should().Be(S(binding, kind + "Sha256"));
        foreach (var key in new[] { "sourceApprovalEvidencePath", "nativeQualificationEvidencePath" })
            binding.GetProperty(key).ValueKind.Should().Be(JsonValueKind.Null);
        foreach (var amended in CatalogueDeltaHashes)
            S(binding.GetProperty("approvedFileSha256"), Prefix + "specs/" + amended.Key + "/spec.md").Should().Be(amended.Value);
        foreach (var row in registry.RootElement.GetProperty("nativeSupportPendingAmendments").EnumerateArray())
            S(row, "authoringCompanionDisposition").Should().Be("MetadataConverterAndTypedDispatchSourceAndNativeApproved");
        ValidateWholeTargetReview(registry.RootElement);
        Read(Prefix + "tasks.md").Should().Contain("- [x] 2.4 ").And.Contain("- [x] 2.5 ");
    }

    private static void ValidateImplementationApproval(JsonElement root)
    {
        var evidence = root.GetProperty("nativeSupportContractEvidence");
        var extension = evidence.GetProperty("sourceApprovalBinding");
        const string path = "openspec/changes/support-ephemeral-native-aot/artifacts/task-3-1-implementation-approval-binding-2026-10-27.json";
        S(extension, "artifactPath").Should().Be(path);
        S(extension, "artifactSha256").Should().Be("6abf023553a1546f52d331d01bfb82aff1f41634b8235e7e3f76713b89f614c5");
        S(extension, "stage").Should().Be(ApprovedStage);
        Hash(File.ReadAllBytes(PathOf(path))).Should().Be(S(extension, "artifactSha256"));
        using var document = JsonDocument.Parse(Read(path));
        var binding = document.RootElement;
        S(binding, "stage").Should().Be(ApprovedStage);
        S(binding, "approvalAuthority").Should().Be("DirectUserApproval");
        const string checkpoint = "8566986850dc377f5a69e165dd58936be50ad722";
        const string child = "1ad2a5946b2874fd709571376a67799f5c7f9cd1";
        S(binding, "checkpointCommit").Should().Be(checkpoint);
        S(binding, "checkpointParent").Should().Be(VerdictCommit);
        S(binding, "checkpointTree").Should().Be("6f697b1374800a2b5d7b4196f9c841f0330d6247");
        S(binding, "approvalEvidenceCommit").Should().Be(child);
        S(binding, "verdictPath").Should().Be(SourceApproval);
        S(binding, "verdictSha256").Should().Be("657a03a70b3d8aea73a0fc5e21e866561130d36d9cdd7067c2ad8f8930ded588");
        Git("show", "-s", "--format=%P", checkpoint).Trim().Should().Be(VerdictCommit);
        Git("show", "-s", "--format=%T", checkpoint).Trim().Should().Be(S(binding, "checkpointTree"));
        Git("show", "-s", "--format=%P", child).Trim().Should().Be(checkpoint);
        Git("diff-tree", "--no-commit-id", "--name-only", "-r", child).Trim().Should().Be(SourceApproval);
        var bytes = File.ReadAllBytes(PathOf(SourceApproval));
        bytes.Length.Should().Be(binding.GetProperty("verdictBytes").GetInt32());
        Hash(bytes).Should().Be(S(binding, "verdictSha256"));
        Encoding.UTF8.GetBytes(Git("show", child + ":" + SourceApproval)).Should().Equal(bytes);
        var manifest = S(binding, "manifestPath");
        Read(manifest).Should().Be(Normalize(Git("show", checkpoint + ":" + manifest)));
        var paths = Read(manifest).Split('\n', StringSplitOptions.RemoveEmptyEntries);
        paths.Should().HaveCount(327).And.OnlyHaveUniqueItems();
        Git("diff-tree", "--no-commit-id", "--name-only", "-r", checkpoint)
            .Split('\n', StringSplitOptions.RemoveEmptyEntries).Should().Equal(paths);
        _ = Git("merge-base", "--is-ancestor", child, "HEAD");
        foreach (var key in new[] { "liveEventDeliveryQualified", "outgoingPublishingQualified", "allPackagesQualified",
                     "published", "issueBranchPushed", "prOpened" })
            binding.GetProperty(key).GetBoolean().Should().BeFalse();
        binding.GetProperty("releaseVersion").ValueKind.Should().Be(JsonValueKind.Null);
        foreach (var key in new[] { "catalogueAmendment", "throttleMetadataAmendment" })
            S(evidence.GetProperty(key), "stage").Should().Be("CatalogueAndImplementationApproved");
        var workflow = binding.GetProperty("workflowChange");
        Hash(File.ReadAllBytes(PathOf(S(workflow, "path")))).Should().Be(S(workflow, "preparedSha256"));
        Read("docs/ephemeral-native-support.md").Should().Contain("source-qualified; release pending")
            .And.Contain("Live event delivery/resumption and outgoing publishing receive no native credit");
    }

    private static void ValidateWholeTargetReview(JsonElement root)
    {
        var binding = root.GetProperty("nativeSupportContractEvidence").GetProperty("wholeTargetValidation");
        S(binding, "stage").Should().Be("SelfReviewedAndUserApproved");
        S(binding, "task").Should().Be("2.5");
        const string recordPath = "openspec/changes/support-ephemeral-native-aot/artifacts/task-2-5-validation-record-2026-10-27.json";
        const string reviewPath = "openspec/changes/support-ephemeral-native-aot/artifacts/task-2-5-independent-review-verdict-2026-10-27.md";
        S(binding, "artifactPath").Should().Be(recordPath);
        S(binding, "artifactSha256").Should().Be("c26c101f802fc6f39fecbfb17c88f26b978e94e1a6e5337304c0a8df0df50c0b");
        S(binding, "reviewPath").Should().Be(reviewPath);
        S(binding, "reviewSha256").Should().Be("bbcee0355982247289a28828fa617f77071a3f4e7894dc2a176f6d6b3a65acc7");
        Hash(File.ReadAllBytes(PathOf(recordPath))).Should().Be(S(binding, "artifactSha256"));
        Hash(File.ReadAllBytes(PathOf(reviewPath))).Should().Be(S(binding, "reviewSha256"));
        using var document = JsonDocument.Parse(Read(recordPath));
        var record = document.RootElement;
        S(record, "stage").Should().Be("FullValidationCompleteApprovalPending");
        S(record, "base").Should().Be(VerdictCommit);
        foreach (var key in new[] { "sourceApprovalEvidencePath", "nativeQualificationEvidencePath" })
            record.GetProperty(key).ValueKind.Should().Be(JsonValueKind.Null);
        foreach (var file in record.GetProperty("sourceFiles").EnumerateObject())
            Hash(Encoding.UTF8.GetBytes(Git("show", "8566986850dc377f5a69e165dd58936be50ad722:" + file.Name)))
                .Should().Be(file.Value.GetString(), "the prior whole-target review remains immutable historical evidence");
        foreach (var file in record.GetProperty("artifacts").EnumerateObject())
            Hash(File.ReadAllBytes(PathOf(Prefix + "artifacts/validation-task-2-5-2026-10-27/" + file.Name)))
                .Should().Be(S(file.Value, "sha256"));
        var runs = record.GetProperty("runs").EnumerateArray().ToArray();
        runs.Should().OnlyContain(run => run.GetProperty("failed").GetInt32() == 0 &&
            run.GetProperty("skipped").GetInt32() == 0 && run.GetProperty("passed").GetInt32() == run.GetProperty("total").GetInt32());
        foreach (var expected in new[] { (Lane: "unit", Count: 969), (Lane: "provider", Count: 188),
                     (Lane: "requirements", Count: 60), (Lane: "infrastructure", Count: 302), (Lane: "stress", Count: 17600) })
            runs.Where(run => S(run, "lane") == expected.Lane).Sum(run => run.GetProperty("passed").GetInt32())
                .Should().Be(expected.Count);
        runs.Count(run => S(run, "lane") == "stress").Should().Be(50);
        record.GetProperty("coverage").GetProperty("lineRate").GetDouble().Should().BeGreaterThanOrEqualTo(0.75);
        record.GetProperty("resharper").GetProperty("errors").GetInt32().Should().Be(0);
        record.GetProperty("native").GetProperty("totalCases").GetInt32().Should().Be(356);
        Read(reviewPath).Should().Contain("Verdict: APPROVE").And.Contain("Keep task 2 uncommitted.");
    }

    [Fact]
    public void ThrottleMetadata_ChangesOnlyTheApprovedGenericParameterAnnotation()
    {
        using var registry = Load();
        var record = registry.RootElement.GetProperty("nativeSupportContractEvidence").GetProperty("throttleMetadataAmendment");
        S(record, "artifactPath").Should().Be(ThrottleBinding);
        S(record, "artifactSha256").Should().Be(ThrottleBindingSha);
        Hash(File.ReadAllBytes(PathOf(ThrottleBinding))).Should().Be(ThrottleBindingSha);
        using var approved = JsonDocument.Parse(Read(ThrottleBinding));
        S(approved.RootElement, "approvalAuthority").Should().Be("DirectUserApproval");
        S(approved.RootElement, "approvedTree").Should().Be("ceef1c99341a39a70c77f13f530ca6ecc3d244f8");
        foreach (var kind in new[] { "amendment", "review", "previousCatalogueBinding" })
            Hash(File.ReadAllBytes(PathOf(S(approved.RootElement, kind + "Path")))).Should().Be(S(approved.RootElement, kind + "Sha256"));
        foreach (var delta in ThrottleDeltaHashes)
        {
            S(approved.RootElement.GetProperty("currentDeltaSha256"), delta.Key).Should().Be(delta.Value);
            Hash(File.ReadAllBytes(PathOf(Prefix + "specs/" + delta.Key + "/spec.md"))).Should().Be(CurrentDeltaHash(delta.Key));
        }
        foreach (var key in new[] { "sourceApprovalEvidencePath", "nativeQualificationEvidencePath" })
            approved.RootElement.GetProperty(key).ValueKind.Should().Be(JsonValueKind.Null);
        var parameter = typeof(global::OrcaCore.Hosting.StepExecutionThrottle)
            .GetMethod(nameof(global::OrcaCore.Hosting.StepExecutionThrottle.For))!
            .GetGenericArguments().Single();
        var attributes = parameter.GetCustomAttributes(
            typeof(System.Diagnostics.CodeAnalysis.DynamicallyAccessedMembersAttribute), false);
        attributes.Should().ContainSingle("the approved throttle parameter must preserve interface metadata");
        ((System.Diagnostics.CodeAnalysis.DynamicallyAccessedMembersAttribute)attributes[0]).MemberTypes
            .Should().Be(System.Diagnostics.CodeAnalysis.DynamicallyAccessedMemberTypes.Interfaces);
        const string path = "src/OrcaCore.Abstractions/Hosting/StructuredExecutionHostOptions.cs";
        const string signature = "For<\n        [System.Diagnostics.CodeAnalysis.DynamicallyAccessedMembers(\n            System.Diagnostics.CodeAnalysis.DynamicallyAccessedMemberTypes.Interfaces)]\n        TStep>";
        Read(path).Should().Be(Git("show", VerdictCommit + ":" + path)
            .Replace("For<TStep>", signature, StringComparison.Ordinal),
            "the amendment changes only this annotation, with the original validation body intact");
    }

    [Fact]
    public void PublicFactory_ChangesOnlyTheApprovedAccessibilityToken()
    {
        const string factory = "src/OrcaCore.Abstractions/Ids/StrongStringValueJsonConverterFactory.cs";
        Read(factory).Should().Be(Git("show", VerdictCommit + ":" + factory)
            .Replace("internal sealed class StrongStringValueJsonConverterFactory", "public sealed class StrongStringValueJsonConverterFactory", StringComparison.Ordinal));
        const string values = "src/OrcaCore.Abstractions/Ids/StrongValues.cs";
        Read(values).Should().Be(Git("show", VerdictCommit + ":" + values));
        var type = typeof(global::OrcaCore.Abstractions.Ids.StrongStringValueJsonConverterFactory);
        type.IsPublic.Should().BeTrue(); type.IsSealed.Should().BeTrue();
        type.GetConstructors().Should().ContainSingle().Which.GetParameters().Should().BeEmpty();
    }

    [Fact]
    public void TypedDispatchSlice_SourceEditsStayWithinTheExplicitlyReviewedFileScope()
    {
        var changed = Git("diff", "--name-only", VerdictCommit, "--", "src")
            .Split('\n', StringSplitOptions.RemoveEmptyEntries)
            .Concat(Git("ls-files", "--others", "--exclude-standard", "src").Split('\n', StringSplitOptions.RemoveEmptyEntries))
            .Order(StringComparer.Ordinal).ToArray();
        changed.Should().Equal(new[]
        {
            "src/OrcaCore.Abstractions/Hosting/StructuredExecutionHostOptions.cs",
            "src/OrcaCore.Abstractions/Ids/StrongStringValueJsonConverterFactory.cs",
            "src/OrcaCore.Abstractions/Instances/WorkflowDiagnosticCatalog.cs",
            "src/OrcaCore.Abstractions/Internal/TypedAuthoringBoundary.cs",
            "src/OrcaCore.Abstractions/Serialization/FixedWorkflowValueCodec.cs",
            "src/OrcaCore.Abstractions/Serialization/WorkflowFailureJsonConverters.cs",
            "src/OrcaCore.Abstractions/Serialization/WorkflowJsonMetadata.cs",
            "src/OrcaCore.Abstractions/Serialization/WorkflowJsonMetadataRegistry.cs",
            "src/OrcaCore.Abstractions/Serialization/WorkflowJsonProductContext.cs",
            "src/OrcaCore.Abstractions/Serialization/WorkflowJsonTypeContracts.cs",
            "src/OrcaCore.Abstractions/Steps/StepResult.cs",
            "src/OrcaCore.Core/Building/PublicBranchAuthoring.cs",
            "src/OrcaCore.Core/Building/PublicNestedAuthoring.cs",
            "src/OrcaCore.Core/Building/PublicStagedAuthoring.cs",
            "src/OrcaCore.Core/Building/SelectedWorkflowAuthoring.cs",
            "src/OrcaCore.Core/Building/SelectedWorkflowBuilder.cs",
            "src/OrcaCore.Core/Building/StructuredBranchBuilders.cs",
            "src/OrcaCore.Core/Compilation/DefinitionCompiler.Fingerprint.cs",
            "src/OrcaCore.Core/Compilation/DefinitionCompiler.TypeContracts.cs",
            "src/OrcaCore.Core/Compilation/DefinitionCompiler.cs",
            "src/OrcaCore.Core/Compilation/DefinitionCompilerOptions.cs",
            "src/OrcaCore.Core/Definitions/WorkflowDefinition.cs",
            "src/OrcaCore.Core/Definitions/WorkflowPartitioner.cs",
            "src/OrcaCore.Core/Execution/ClosedForEachBindings.cs",
            "src/OrcaCore.Core/Execution/ClosedResultBindings.cs",
            "src/OrcaCore.Core/Execution/FailureProvenance.cs",
            "src/OrcaCore.Core/Execution/ForEachSnapshotMaterializer.cs",
            "src/OrcaCore.Core/Execution/ScopeMergeAdapter.cs",
            "src/OrcaCore.Core/Execution/StepResultWaitAccessor.cs",
            "src/OrcaCore.Core/Execution/StructuredInvocationCache.cs",
            "src/OrcaCore.Engine.Ephemeral/EphemeralWorkflowEngine.cs",
            "src/OrcaCore.Engine.Ephemeral/Execution/EphemeralProjectionModels.cs",
            "src/OrcaCore.Engine.Ephemeral/Execution/InMemoryExecutionStateAdapter.Failures.cs",
            "src/OrcaCore.Engine.Ephemeral/Execution/InMemoryExecutionStateAdapter.ForEach.cs",
            "src/OrcaCore.Engine.Ephemeral/Execution/InMemoryExecutionStateAdapter.cs",
            "src/OrcaCore.Engine.Ephemeral/Execution/WorkflowErrorDetails.cs",
            "src/OrcaCore.Engine.Ephemeral/Execution/WorkflowInstance.cs",
            "src/OrcaCore.Engine.Ephemeral/Facade/EphemeralDefinitionRuntime.cs",
            "src/OrcaCore.Engine.Ephemeral/Facade/EphemeralWorkflowCatalog.cs",
            "src/OrcaCore.Engine.Ephemeral/Facade/EphemeralWorkflowEventRouter.cs",
            "src/OrcaCore.Engine.Ephemeral/Facade/EphemeralWorkflowFacade.cs",
            "src/OrcaCore.Engine.Ephemeral/Hosting/OrcaCoreEphemeralEngineServiceCollectionExtensions.cs",
        }, "the typed dispatch slice grants no blanket Core/Abstractions/Ephemeral source exemption");
    }

    private static JsonDocument Load() => JsonDocument.Parse(Read(Registry));
    private static string S(JsonElement value, string property) => value.GetProperty(property).GetString()!;
    private static string PathOf(string relative) => Path.Combine(FixtureDefinitions.RepositoryRoot(), relative);
    private static string Read(string relative) => Normalize(File.ReadAllText(PathOf(relative)));
    private static string Normalize(string text) => text.Replace("\r\n", "\n", StringComparison.Ordinal);
    private static string[] Blocks(string text) => Regex.Matches(Normalize(text), @"(?ms)^### Requirement: .*?(?=^### Requirement: |^## |\z)")
        .Select(x => x.Value.TrimEnd('\n')).ToArray();
    private static string Hash(byte[] bytes) => Convert.ToHexStringLower(SHA256.HashData(bytes));
    private static string Git(params string[] arguments)
    {
        var start = new ProcessStartInfo("git")
        {
            WorkingDirectory = FixtureDefinitions.RepositoryRoot(),
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            StandardOutputEncoding = Encoding.UTF8
        };
        foreach (var argument in arguments) start.ArgumentList.Add(argument);
        using var process = Process.Start(start) ?? throw new InvalidOperationException("Git authority lookup could not start.");
        var output = process.StandardOutput.ReadToEnd();
        var error = process.StandardError.ReadToEnd();
        process.WaitForExit();
        process.ExitCode.Should().Be(0, "Git authority lookups fail closed: {0}", error);
        return output;
    }
}
