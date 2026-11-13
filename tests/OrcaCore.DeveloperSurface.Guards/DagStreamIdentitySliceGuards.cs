using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Xml.Linq;
using System.Text.RegularExpressions;
using AwesomeAssertions;

namespace OrcaCore.DeveloperSurface.Guards;

[Trait(GuardTraits.Phase, GuardTraits.Phase0)]
[Trait(GuardTraits.Disposition, GuardTraits.Infrastructure)]
public sealed class DagStreamIdentitySliceGuards
{
    private const string FingerprintAmendmentRegistrySha256 = "88af7f6095c69698eea6a1162c8557df21fb3ef8898e29a230e45ec14d15d8ac";
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
        ValidateApprovedFingerprintAmendment(root, registry.RootElement);
    }

    [Fact]
    public void FingerprintAmendmentApproval_BindsTheActualCheckpointAndVerdictAddition()
    {
        var root = FixtureDefinitions.RepositoryRoot();
        using var registry = ReadFixture(root, "post-gate-amendment-path.json");
        var amendment = registry.RootElement.GetProperty("dagRunFingerprintContractAmendment");
        amendment.GetProperty("stage").GetString().Should().Be("Approved",
            "the next source target must consume the independently checkpointed fingerprint contract");
        var evidence = amendment.GetProperty("approval").Deserialize<TransitionEvidence>(JsonOptions)!;
        evidence.Should().Be(new TransitionEvidence(
            "a6f02fa5be9c31db232e3582d2f77d35acf86639",
            "dcf4337daffef0630f917f57d563011e43d330ec",
            "3c2cd9606db6e4380b148c5f8dd3503e367f255f",
            "docs/review/developer-facing-interface-section-08-task-8-4-dag-run-fingerprint-contract-amendment-independent-review-verdict-2026-10-15.md",
            "f8c0082e190d5656211caf5e76c318f5044b370524caad053287097aecfda3c2"));
        ValidateReviewEvidence(root, evidence);
    }

    [Fact]
    public void Task21ProtocolProviderCandidate_PreservesApprovedHistoryAndOnlyAddsReleased()
    {
        var root = FixtureDefinitions.RepositoryRoot();
        const string artifacts = "openspec/changes/admit-durable-dag-run-progression/artifacts/";
        Hash(Normalize(File.ReadAllText(Path.Combine(root, artifacts + "task-2-1-protocol-provider-errata-2026-10-29.md"))))
            .Should().Be("3a54776a91cd4a6ca5ef04dcd94f3e04af80ee25e4400485fcac009091739d98", "the combined source/errata review target must remain explicit");
        Hash(Normalize(File.ReadAllText(Path.Combine(root, artifacts + "task-2-1-protocol-provider-api-candidate-2026-10-29.md"))))
            .Should().Be("cbe6395a2fc19dcd43a40c365c1e7425aada685a6a6dd8a377ea2137d4694581", "the combined source/errata review target must remain explicit");
        Hash(Normalize(File.ReadAllText(Path.Combine(root, artifacts + "task-2-1-protocol-provider-signatures-2026-10-29.cs"))))
            .Should().Be("17576395ef39391c315c451d3ac80b3ead3ccac3d40639f6202e5a41bf7644df", "the combined source/errata review target must remain explicit");
        using var registry = ReadFixture(root, "post-gate-amendment-path.json");
        Hash(JsonSerializer.Serialize(registry.RootElement.GetProperty("dagRunProtocolProviderErrataCandidate")))
            .Should().Be("e69ddbf4add9e0f0353008717567a9b91fc0f733581d36710623bd1c520012f9", "the combined candidate is proposed, never self-approved by a green lane");
        var priorSignatures = Normalize(File.ReadAllText(Path.Combine(root, artifacts + "task-2-1-fingerprint-amendment-signatures-2026-10-14.cs")));
        var signatures = Normalize(File.ReadAllText(Path.Combine(root, artifacts + "task-2-1-protocol-provider-signatures-2026-10-29.cs")));
        signatures.Should().Be(priorSignatures.Replace(
            "public enum DagRunLeaseOutcomeV1 { Granted, Queued, OwnerBusy, StaleFence, NotFound, Terminal }",
            "public enum DagRunLeaseOutcomeV1 { Granted, Queued, OwnerBusy, StaleFence, NotFound, Terminal, Released }", StringComparison.Ordinal));
        var priorCatalog = Normalize(File.ReadAllText(Path.Combine(root, artifacts + "task-2-1-exact-api-additions-fingerprint-amendment-2026-10-14.md")));
        var candidate = Normalize(File.ReadAllText(Path.Combine(root, artifacts + "task-2-1-protocol-provider-api-candidate-2026-10-29.md")));
        var expected = CatalogApiKeys(priorCatalog[priorCatalog.IndexOf("## OrcaCore.Runtime.Protocol\n", StringComparison.Ordinal)..]);
        const string leaseType = "type public enum OrcaCore.Abstractions.Providers.Dag.DagRunLeaseOutcomeV1 : [System.Private.CoreLib]System.Int32";
        expected.Add(leaseType + "\t  field public const [OrcaCore.Provider.Abstractions]OrcaCore.Abstractions.Providers.Dag.DagRunLeaseOutcomeV1 Released = 6");
        CatalogApiKeys(candidate).Should().BeEquivalentTo(expected);
        foreach (var provider in new[] { "InMemory", "PostgreSql", "SqlServer" })
        {
            var project = XDocument.Load(Path.Combine(root,"src","OrcaCore.Providers."+provider,"OrcaCore.Providers."+provider+".csproj"));
            var includes = project.Descendants("ItemGroup").Single(g => (string?)g.Attribute("Label") == "DagRunPrivate")
                .Elements("Compile").Select(c => ((string)c.Attribute("Include")!).Replace('\\','/')).ToArray();
            var links = new List<string> { "../OrcaCore.Runtime.Protocol/Durable/Dag/DagRunStateReducer.cs", "../OrcaCore.Provider.Abstractions/Dag/Internal/DagRunStoreRules.cs" };
            if (provider != "InMemory") links.Add("../OrcaCore.Provider.Abstractions/Dag/Internal/DagRunSqlStore.cs");
            includes.Should().Equal(links,"private implementation sources must be compiled by their exact owning providers");
        }
        var contractProject = XDocument.Load(Path.Combine(root,"src","OrcaCore.Provider.Abstractions","OrcaCore.Provider.Abstractions.csproj"));
        contractProject.Descendants("Compile").Select(e => ((string?)e.Attribute("Remove"))?.Replace('\\','/'))
            .Where(s => s is not null).Should().Equal("Dag/Internal/**/*.cs");
        ValidateTask21SourceScope(root);
        foreach (var assembly in new[] { "OrcaCore.Runtime.Protocol", "OrcaCore.Provider.Abstractions" })
        {
            var path = "tests/OrcaCore.DeveloperSurface.Guards/Fixtures/PublicApi/v1/" + assembly + ".api.txt";
            var before = ApiKeys(Normalize(Git(root, "show", "82811cd41aae577d8a1ae13258790d3da0c30d72:" + path)));
            var actual = ApiKeys(Normalize(File.ReadAllText(Path.Combine(root, path))));
            before.Except(actual).Should().BeEmpty("this slice removes no existing public key");
            var allowed = CatalogApiKeys(candidate.Split("## " + assembly + "\n", StringSplitOptions.None)[1].Split("\n## ", StringSplitOptions.None)[0]);
            actual.Except(before).Should().BeEquivalentTo(allowed.Except(before));
        }
    }

    private static void ValidateTask21SourceScope(string root)
    {
        var changed = Git(root,"diff","--name-only","82811cd41aae577d8a1ae13258790d3da0c30d72","--","src")
            .Split('\n',StringSplitOptions.RemoveEmptyEntries)
            .Concat(Git(root,"ls-files","--others","--exclude-standard","src").Split('\n',StringSplitOptions.RemoveEmptyEntries))
            .Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal);
        changed.Should().Equal(new[]
        {
            "src/OrcaCore.Durable.Hosting/Services/OrcaCoreOutboxPumpHostedService.cs",
            "src/OrcaCore.Engine.Durable/Diagnostics/OrcaCoreDurableDiagnostics.cs",
            "src/OrcaCore.Engine.Durable/Outbox/DurableOutboxPump.cs",
            "src/OrcaCore.Provider.Abstractions/Dag/DagRunAppendV1.cs",
            "src/OrcaCore.Provider.Abstractions/Dag/DagRunDiscoveryRequestV1.cs",
            "src/OrcaCore.Provider.Abstractions/Dag/DagRunDiscoveryV1.cs",
            "src/OrcaCore.Provider.Abstractions/Dag/DagRunFenceV1.cs",
            "src/OrcaCore.Provider.Abstractions/Dag/DagRunLeaseOutcomeV1.cs",
            "src/OrcaCore.Provider.Abstractions/Dag/DagRunLeasePurposeV1.cs",
            "src/OrcaCore.Provider.Abstractions/Dag/DagRunLeaseRequestV1.cs",
            "src/OrcaCore.Provider.Abstractions/Dag/DagRunLeaseResultV1.cs",
            "src/OrcaCore.Provider.Abstractions/Dag/DagRunSubscribeOutcomeV1.cs",
            "src/OrcaCore.Provider.Abstractions/Dag/DagRunSubscribeResultV1.cs",
            "src/OrcaCore.Provider.Abstractions/Dag/DagRunSubscriptionV1.cs",
            "src/OrcaCore.Provider.Abstractions/Dag/Internal/DagRunSqlStore.cs",
            "src/OrcaCore.Provider.Abstractions/Dag/Internal/DagRunStoreRules.cs",
            "src/OrcaCore.Provider.Abstractions/OrcaCore.Provider.Abstractions.csproj",
            "src/OrcaCore.Provider.Abstractions/ProviderCommitContracts.cs",
            "src/OrcaCore.Provider.Abstractions/ProviderPorts.cs",
            "src/OrcaCore.Provider.Abstractions/WorkflowOperationalModels.cs",
            "src/OrcaCore.Providers.InMemory/InMemoryWorkflowProvider.Dag.cs",
            "src/OrcaCore.Providers.InMemory/InMemoryWorkflowProvider.cs",
            "src/OrcaCore.Providers.InMemory/OrcaCore.Providers.InMemory.csproj",
            "src/OrcaCore.Providers.PostgreSql/Migrations/001_initial.sql",
            "src/OrcaCore.Providers.PostgreSql/OrcaCore.Providers.PostgreSql.csproj",
            "src/OrcaCore.Providers.PostgreSql/PostgreSqlWorkflowStore.Dag.cs",
            "src/OrcaCore.Providers.PostgreSql/PostgreSqlWorkflowStore.Operational.cs",
            "src/OrcaCore.Providers.PostgreSql/PostgreSqlWorkflowStore.cs",
            "src/OrcaCore.Providers.SqlServer/Internal/SqlServerStateDocumentStore.cs",
            "src/OrcaCore.Providers.SqlServer/Internal/SqlServerWorkflowStateEngine.cs",
            "src/OrcaCore.Providers.SqlServer/Internal/SqlServerWorkflowStore.Dag.cs",
            "src/OrcaCore.Providers.SqlServer/Internal/SqlServerWorkflowStore.cs",
            "src/OrcaCore.Providers.SqlServer/Migrations/001_initial.sql",
            "src/OrcaCore.Providers.SqlServer/OrcaCore.Providers.SqlServer.csproj",
            "src/OrcaCore.Runtime.Protocol/Durable/Dag/DagNodeStateV1.cs",
            "src/OrcaCore.Runtime.Protocol/Durable/Dag/DagRunBindingV1.cs",
            "src/OrcaCore.Runtime.Protocol/Durable/Dag/DagRunCheckpointV1.cs",
            "src/OrcaCore.Runtime.Protocol/Durable/Dag/DagRunGraphV1.cs",
            "src/OrcaCore.Runtime.Protocol/Durable/Dag/DagRunNodeChangeV1.cs",
            "src/OrcaCore.Runtime.Protocol/Durable/Dag/DagRunNodeStateV1.cs",
            "src/OrcaCore.Runtime.Protocol/Durable/Dag/DagRunNodeV1.cs",
            "src/OrcaCore.Runtime.Protocol/Durable/Dag/DagRunRecordKindV1.cs",
            "src/OrcaCore.Runtime.Protocol/Durable/Dag/DagRunRecordV1.cs",
            "src/OrcaCore.Runtime.Protocol/Durable/Dag/DagRunSnapshotV1.cs",
            "src/OrcaCore.Runtime.Protocol/Durable/Dag/DagRunStartKeyV1.cs",
            "src/OrcaCore.Runtime.Protocol/Durable/Dag/DagRunStateReducer.cs",
            "src/OrcaCore.Runtime.Protocol/Durable/Dag/DagRunStateV1.cs",
            "src/OrcaCore.Runtime.Protocol/Durable/Dag/DagRunValidation.cs",
            "src/OrcaCore.Runtime.Protocol/Durable/Dag/DagRunValueV1.cs",
            "src/OrcaCore.Runtime.Protocol/Serialization/DagRunRecordCodecV1.cs",
        }, "this exact data-plane and external-lane safety target adds no new runtime pump, Hosting bridge or unrelated native/authoring source edit");
    }

    internal static void ProjectTask21RegistryExtension(JsonObject previous, JsonObject current)
    {
        // The DAG facts above validate the exact new candidate and the actual Git-bound
        // fingerprint approval. Older independent change guards still compare every
        // other registry value; they must not mistake this reviewed extension for drift
        // in their own historical approval records.
        current.Remove("dagRunProtocolProviderErrataCandidate");
        var old = previous["dagRunFingerprintContractAmendment"]!.AsObject();
        var actual = current["dagRunFingerprintContractAmendment"]!.AsObject();
        actual["stage"] = old["stage"]?.DeepClone();
        actual["approval"] = old["approval"]?.DeepClone();
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

    private static void ValidateApprovedFingerprintAmendment(string root, JsonElement registry)
    {
        var amendment = registry.GetProperty("dagRunFingerprintContractAmendment");
        Hash(JsonSerializer.Serialize(amendment)).Should().Be(FingerprintAmendmentRegistrySha256,
            "the proposed fingerprint addendum and exact artifacts must remain source-pinned, not routine fixture data");
        amendment.GetProperty("stage").GetString().Should().Be("Approved");
        ValidateReviewEvidence(root, amendment.GetProperty("approval").Deserialize<TransitionEvidence>(JsonOptions)!);
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
        // The fingerprint contract is now independently checkpointed and evidenced.
        // Task 2.1 may implement the protocol/provider data plane, not the later Hosting slices.
        Git(root, "diff", "--name-only", baseCommit, "--",
            "src/OrcaCore.Dag", "src/OrcaCore.Dag.Hosting",
            "openspec/changes/admit-durable-dag-run-progression/specs",
            "tests/OrcaCore.DeveloperSurface.Guards/Fixtures/PublicApi/v1/OrcaCore.Dag.api.txt",
            "tests/OrcaCore.DeveloperSurface.Guards/Fixtures/PublicApi/v1/OrcaCore.Dag.Hosting.api.txt")
            .Should().BeEmpty("Task 2.1 does not authorize Dag or Hosting source, their public APIs, or a delta change");
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
