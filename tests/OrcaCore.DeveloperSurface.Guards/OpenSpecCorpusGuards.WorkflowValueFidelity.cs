using System.Text.Json;
using System.Text.Json.Nodes;
using AwesomeAssertions;

namespace OrcaCore.DeveloperSurface.Guards;

public sealed partial class OpenSpecCorpusGuards
{
    private const string ValueFidelityChange = "enforce-lossless-workflow-values";
    private const string ValueFidelityContractPath = "openspec/changes/" + ValueFidelityChange + "/artifacts/contract.json";
    private const string ValueFidelityStage = "Proposed";
    private const string ValueFidelityContractHash = "bbc6ad64c55ee313d9e1b002bf954e674275bb3a0981f1081fb0dfd856a35dc0";

    internal static void ProjectValueFidelityRegistry(JsonObject current)
    {
        var row = current["workflowValueFidelity"]!.AsObject();
        row.Count.Should().Be(4);
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
            if (FidelitySynchronized) canonical[cap][title].Block.Should().Be(change.Block);
            else canonical[cap].Should().NotContainKey(title);
            expectedCount++;
        }
        active.Count(item => item.Change == ValueFidelityChange).Should().Be(expectedCount);
        foreach (var row in contract.GetProperty("documents").EnumerateArray())
        {
            var path = row.GetProperty("path").GetString()!;
            _ = ProjectValueFidelityDocument(root, path,
                NormalizeLineEndings(File.ReadAllText(Path.Combine(root, path))), contract, row);
        }
        return keys;
    }

    private static bool FidelitySynchronized => ValueFidelityStage == "ApprovedPending";

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
            var expected = row.GetProperty(FidelitySynchronized ? "afterSha256" : "beforeSha256").GetString();
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
        var contract = ReadFidelityContract(root);
        foreach (var row in contract.GetProperty("documents").EnumerateArray())
            if (row.GetProperty("path").GetString() == path)
                return ProjectValueFidelityDocument(root, path, current, contract, row);
        return current;
    }

    private static string ProjectValueFidelityDocument(string root, string path, string current,
        JsonElement contract, JsonElement row)
    {
        var hash = row.GetProperty(FidelitySynchronized ? "afterSha256" : "beforeSha256").GetString();
        Sha256(current).Should().Be(hash, "only the reviewed document may be projected");
        if (!FidelitySynchronized) return current;
        var prior = DagFacadeApprovedHistory.TextAt(root, contract.GetProperty("base").GetString()!, path);
        Sha256(prior).Should().Be(row.GetProperty("beforeSha256").GetString());
        return prior;
    }
}
