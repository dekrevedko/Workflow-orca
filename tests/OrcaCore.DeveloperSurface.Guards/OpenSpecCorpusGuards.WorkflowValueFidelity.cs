using System.Text.Json;
using System.Text.Json.Nodes;
using AwesomeAssertions;

namespace OrcaCore.DeveloperSurface.Guards;

public sealed partial class OpenSpecCorpusGuards
{
    private const string ValueFidelityChange = "enforce-lossless-workflow-values";
    private const string ValueFidelityContractPath = "openspec/changes/" + ValueFidelityChange + "/artifacts/contract.json";
    private const string ValueFidelityProposal = "c05bdad7af594febd3b7c487d42dbccf65224a59";
    private const string ValueFidelityVerdict = "openspec/changes/enforce-lossless-workflow-values/artifacts/contract-approval.md";
    private const string ValueFidelityVerdictHash = "66c8fefd9869217ef20e0efa59f34d953a47a71ad01b7bf7e3b7e6928868111e";
    private const string ValueFidelityStage = "Complete";
    private const string ValueFidelitySource = "200d13a854677743b644e9bb1dbde2edb2318e05";
    private const string ValueFidelityQualification = "openspec/changes/" + ValueFidelityChange + "/artifacts/qualification.md";
    private const string ValueFidelityQualificationHash = "e74443200893868427982ea936c957c25453825531cd3697e40529eb4e7ed39f";
    private const string ValueFidelityContractHash = "18406e9549dad916e955ea9acd8baa02f0f7049464789a32bfe7a085dfdc8bc9";

    internal static void ProjectValueFidelityRegistry(JsonObject current)
    {
        ProjectObservationRegistry(current);
        var row = current["workflowValueFidelity"]!.AsObject();
        row.Count.Should().Be(10);
        row["implementationCheckpoint"]!.GetValue<string>().Should().Be(ValueFidelitySource);
        row["qualificationPath"]!.GetValue<string>().Should().Be(ValueFidelityQualification);
        row["qualificationSha256"]!.GetValue<string>().Should().Be(ValueFidelityQualificationHash);
        row["reviewedCheckpoint"]!.GetValue<string>().Should().Be(ValueFidelityProposal);
        row["verdictPath"]!.GetValue<string>().Should().Be(ValueFidelityVerdict);
        row["verdictSha256"]!.GetValue<string>().Should().Be(ValueFidelityVerdictHash);
        row["contract"]!.GetValue<string>().Should().Be(ValueFidelityContractPath);
        row["stage"]!.GetValue<string>().Should().Be(ValueFidelityStage);
        row["reviewTask"]!.GetValue<string>().Should().Be("1.1");
        row["canonicalSyncTask"]!.GetValue<string>().Should().Be("1.2");
        current.Remove("workflowValueFidelity");
    }

    private static JsonElement ReadFidelityContract(string root)
    {
        var path = Path.Combine(root, ValueFidelityContractPath);
        var text = NormalizeLineEndings(File.ReadAllText(path));
        Sha256(text).Should().Be(ValueFidelityContractHash);
        using var document = JsonDocument.Parse(text);
        return document.RootElement.Clone();
    }

    private static HashSet<string> ValidateValueFidelityOwners(string root, List<DeltaRequirement> active,
        IReadOnlyDictionary<string, IReadOnlyDictionary<string, CanonicalRequirement>> canonical, JsonElement contract)
    {
        var registry = JsonNode.Parse(File.ReadAllText(Path.Combine(root,
            "tests/OrcaCore.DeveloperSurface.Guards/Fixtures/post-gate-amendment-path.json")))!.AsObject();
        ProjectValueFidelityRegistry(registry);
        var baseCommit = contract.GetProperty("base").GetString()!;
        DagFacadeApprovedHistory.TextAt(root, ValueFidelityProposal, ValueFidelityContractPath)
            .Should().Be(NormalizeLineEndings(File.ReadAllText(Path.Combine(root, ValueFidelityContractPath))));
        RunGit(root, "rev-parse", ValueFidelityProposal + "^2").ExitCode.Should().NotBe(0);
        RunGit(root, "rev-parse", ValueFidelityProposal + "^^").StandardOutput.Trim().Should().Be(baseCommit);
        var verdict = NormalizeLineEndings(File.ReadAllText(Path.Combine(root, ValueFidelityVerdict)));
        Sha256(verdict).Should().Be(ValueFidelityVerdictHash);
        verdict.Split('\n').Should().Contain("Verdict: ACCEPT").And.Contain("Scope: enforce-lossless-workflow-values contract and owner routing")
            .And.Contain($"Reviewed proposal checkpoint: `{ValueFidelityProposal}`")
            .And.Contain($"Reviewed normalized contract SHA-256: `{ValueFidelityContractHash}`");
        ValidateTaskReference(root, new PostGateTaskReference(ValueFidelityChange, "1.1", CompleteTaskState));
        ValidateTaskReference(root, new PostGateTaskReference(ValueFidelityChange, "1.2", CompleteTaskState));
        ValidateTaskReference(root, new PostGateTaskReference(ValueFidelityChange, "2.1", CompleteTaskState));
        ValidateTaskReference(root, new PostGateTaskReference(ValueFidelityChange, "2.2", CompleteTaskState));
        var qualification = NormalizeLineEndings(File.ReadAllText(Path.Combine(root, ValueFidelityQualification)));
        Sha256(qualification).Should().Be(ValueFidelityQualificationHash);
        qualification.Should().Contain(ValueFidelitySource);
        var keys = new HashSet<string>(StringComparer.Ordinal);
        var expectedCount = 0;
        foreach (var row in contract.GetProperty("successors").EnumerateArray())
        {
            var cap = row.GetProperty("capability").GetString()!;
            var title = row.GetProperty("requirement").GetString()!;
            var predecessor = row.GetProperty("predecessor").GetString()!;
            var owners = predecessor == DagFacadeSeamChange
                ? new[] { "reshape-developer-facing-interfaces", "admit-dag-authoring-friend-boundary",
                    RuntimeViewChange, DagFacadeSeamChange, ValueFidelityChange }
                : [predecessor, ValueFidelityChange];
            ValidateResolvedPostGateOwnerChain(root, active, cap, title, owners);
            var change = ReadResolvedPostGateOwner(root, ValueFidelityChange, cap, title);
            change.Operation.Should().Be(RequirementOperation.Modified);
            Sha256(change.Block).Should().Be(row.GetProperty("afterSha256").GetString());
            keys.Add($"{cap}\0{title}").Should().BeTrue();
            expectedCount++;
        }
        foreach (var row in contract.GetProperty("additions").EnumerateArray())
        {
            var cap = row.GetProperty("capability").GetString()!;
            var title = row.GetProperty("requirement").GetString()!;
            var change = ReadResolvedPostGateOwner(root, ValueFidelityChange, cap, title);
            change.Operation.Should().Be(RequirementOperation.Added);
            Sha256(change.Block).Should().Be(row.GetProperty("sha256").GetString());
            canonical[cap][title].Block.Should().Be(change.Block);
            expectedCount++;
        }
        active.Count(item => item.Change == ValueFidelityChange).Should().Be(expectedCount);
        foreach (var row in contract.GetProperty("documents").EnumerateArray())
        {
            var path = row.GetProperty("path").GetString()!;
            _ = ProjectValueFidelityDocument(root, path,
                BeforeObservationSynchronization(root, path, NormalizeLineEndings(File.ReadAllText(Path.Combine(root, path)))), contract, row);
        }
        return keys;
    }

    private static IReadOnlyDictionary<string, IReadOnlyDictionary<string, CanonicalRequirement>>
        PriorValueFidelityCanonical(string root,
            IReadOnlyDictionary<string, IReadOnlyDictionary<string, CanonicalRequirement>> canonical, JsonElement contract)
    {
        var result = canonical.ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.Ordinal);
        foreach (var row in contract.GetProperty("successors").EnumerateArray())
        {
            var cap = row.GetProperty("capability").GetString()!;
            var title = row.GetProperty("requirement").GetString()!;
            var predecessor = ReadResolvedPostGateOwner(root, row.GetProperty("predecessor").GetString()!, cap, title);
            Sha256(predecessor.Block).Should().Be(row.GetProperty("beforeSha256").GetString());
            var expected = row.GetProperty("afterSha256").GetString();
            Sha256(canonical[cap][title].Block).Should().Be(expected);
            var historical = new Dictionary<string, CanonicalRequirement>(result[cap], StringComparer.Ordinal);
            historical[title] = new(title, predecessor.Block);
            result[cap] = historical;
        }
        return result;
    }

    private static ProvenanceRow ClassifyValueFidelityProvenance(string root,
        IReadOnlyDictionary<string, IReadOnlyDictionary<string, CanonicalRequirement>> current,
        IReadOnlyDictionary<string, IReadOnlyDictionary<string, CanonicalRequirement>> previous,
        DeltaRequirement requirement, PostGateAmendmentCheckpoint checkpoint)
    {
        if (requirement.Change == ValueFidelityChange) return ClassifyProvenance(current, requirement);
        var historical = ClassifyPostGateProvenance(root, previous, requirement, checkpoint);
        if (!current.TryGetValue(requirement.Capability, out var present) ||
            !present.TryGetValue(requirement.Requirement, out var block) ||
            previous[requirement.Capability][requirement.Requirement] == block) return historical;
        historical.State.Should().BeOneOf(ProvenanceState.Synchronized, ProvenanceState.SupersededByApprovedSuccessor);
        return CreateProvenanceRow(requirement, ProvenanceState.SupersededByApprovedSuccessor, block.Block);
    }

    internal static string BeforeValueFidelitySynchronization(string root, string path, string current)
    {
        if (path is "README.md" or "docs/production-readiness.md")
        {
            // Validate the reviewed status refresh before checking its parent against earlier approvals.
            const string statusRefresh = "80495420c75e762f2841c450e55f4b2eac73c9f1";
            current.Should().Be(DagFacadeApprovedHistory.TextAt(root, statusRefresh, path));
            current = DagFacadeApprovedHistory.TextAt(root, statusRefresh + "^", path);
        }

        current = BeforeObservationSynchronization(root, path, current);
        // Only these two guide updates are projected before older documentation checks.
        (string After, string Before)? guide = path switch
        {
            "docs/ephemeral-engine-developer-guide.md" => ("c44bc8e489bf2afc3bad3d814cd073348ca37eeb8931d905589bfcdce0165363",
                "091ac6a1fe608e65433c3f7355d4b327f0a2ed049b9ef7b09781da1fa8c78786"),
            "docs/ephemeral-native-support.md" => ("e448a88ad1f365e0138b2360479ab9bc450e58c9d9718616e354c81f03f7c57b",
                "9e5a9506ae93cdd2d752385e8b929d601f7436c895e578936c0dd1346a3ddb1e"),
            _ => null
        };
        if (guide is { } hashes)
        {
            Sha256(current).Should().Be(hashes.After);
            var prior = DagFacadeApprovedHistory.TextAt(root, ValueFidelityProposal, path);
            Sha256(prior).Should().Be(hashes.Before);
            return prior;
        }
        var contract = ReadFidelityContract(root);
        foreach (var row in contract.GetProperty("documents").EnumerateArray())
            if (row.GetProperty("path").GetString() == path)
                return ProjectValueFidelityDocument(root, path, current, contract, row);
        return current;
    }

    private static string ProjectValueFidelityDocument(string root, string path, string current,
        JsonElement contract, JsonElement row)
    {
        var hash = row.GetProperty("afterSha256").GetString();
        Sha256(current).Should().Be(hash, "only the reviewed document may be projected");
        var prior = DagFacadeApprovedHistory.TextAt(root, contract.GetProperty("base").GetString()!, path);
        Sha256(prior).Should().Be(row.GetProperty("beforeSha256").GetString());
        return prior;
    }
}
