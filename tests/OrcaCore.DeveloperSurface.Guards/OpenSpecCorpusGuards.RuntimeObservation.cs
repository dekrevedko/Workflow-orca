using System.Text.Json;
using System.Text.Json.Nodes;
using AwesomeAssertions;

namespace OrcaCore.DeveloperSurface.Guards;

public sealed partial class OpenSpecCorpusGuards
{
    private const string ObservationChange = "bound-runtime-observation";
    private const string ObservationStage = "Proposed";
    private const string ObservationContractPath = "openspec/changes/" + ObservationChange + "/artifacts/contract.json";
    private const string ObservationContractHash = "f6c715c0f33770d198885a10d09116fc45287a257590fee2aa4eaeb908b34594";
    private static bool ObservationSynchronized => ObservationStage != "Proposed";

    private static JsonElement ReadObservationContract(string root)
    {
        var text = NormalizeLineEndings(File.ReadAllText(Path.Combine(root, ObservationContractPath)));
        Sha256(text).Should().Be(ObservationContractHash);
        using var contract = JsonDocument.Parse(text);
        return contract.RootElement.Clone();
    }

    private static void ProjectObservationRegistry(JsonObject current)
    {
        var row = current["durableOutputObservation"]!.AsObject();
        row.Count.Should().Be(4);
        row["stage"]!.GetValue<string>().Should().Be(ObservationStage);
        row["contract"]!.GetValue<string>().Should().Be(ObservationContractPath);
        row["reviewTask"]!.GetValue<string>().Should().Be("1.1");
        row["canonicalSyncTask"]!.GetValue<string>().Should().Be("1.2");
        current.Remove("durableOutputObservation");
    }

    private static HashSet<string> ValidateObservationOwners(string root, List<DeltaRequirement> active, JsonElement contract)
    {
        ValidateTaskReference(root, new PostGateTaskReference(ObservationChange, "1.1", OpenTaskState));
        ValidateTaskReference(root, new PostGateTaskReference(ObservationChange, "1.2", OpenTaskState));
        var keys = new HashSet<string>(StringComparer.Ordinal);
        foreach (var row in contract.GetProperty("successors").EnumerateArray())
        {
            var cap = row.GetProperty("capability").GetString()!;
            var heading = row.GetProperty("requirement").GetString()!;
            var chain = row.GetProperty("ownerChain").Deserialize<string[]>()!;
            chain[^1].Should().Be(ObservationChange);
            ValidateResolvedPostGateOwnerChain(root, active, cap, heading, chain);
            var successor = ReadResolvedPostGateOwner(root, ObservationChange, cap, heading);
            successor.Operation.Should().Be(RequirementOperation.Modified);
            Sha256(successor.Block).Should().Be(row.GetProperty("afterSha256").GetString());
            keys.Add($"{cap}\0{heading}").Should().BeTrue();
        }
        active.Count(r => r.Change == ObservationChange).Should().Be(keys.Count);
        foreach (var row in contract.GetProperty("documents").EnumerateArray())
        {
            var path = row.GetProperty("path").GetString()!;
            var current = NormalizeLineEndings(File.ReadAllText(Path.Combine(root, path)));
            ProjectObservationDocument(root, path, current, contract, row);
        }
        return keys;
    }

    private static IReadOnlyDictionary<string, IReadOnlyDictionary<string, CanonicalRequirement>>
        PriorObservationCanonical(string root, IReadOnlyDictionary<string, IReadOnlyDictionary<string, CanonicalRequirement>> current,
            JsonElement contract)
    {
        var prior = current.ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.Ordinal);
        foreach (var row in contract.GetProperty("successors").EnumerateArray())
        {
            var cap = row.GetProperty("capability").GetString()!;
            var heading = row.GetProperty("requirement").GetString()!;
            var chain = row.GetProperty("ownerChain").Deserialize<string[]>()!;
            var predecessor = ReadResolvedPostGateOwner(root, chain[^2], cap, heading);
            Sha256(predecessor.Block).Should().Be(row.GetProperty("beforeSha256").GetString());
            Sha256(current[cap][heading].Block).Should().Be(row.GetProperty(ObservationSynchronized ? "afterSha256" : "beforeSha256").GetString());
            var headings = new Dictionary<string, CanonicalRequirement>(prior[cap], StringComparer.Ordinal);
            headings[heading] = new(heading, predecessor.Block);
            prior[cap] = headings;
        }
        return prior;
    }

    private static ProvenanceRow ClassifyObservationProvenance(string root,
        IReadOnlyDictionary<string, IReadOnlyDictionary<string, CanonicalRequirement>> current,
        IReadOnlyDictionary<string, IReadOnlyDictionary<string, CanonicalRequirement>> beforeObservation,
        IReadOnlyDictionary<string, IReadOnlyDictionary<string, CanonicalRequirement>> beforeFidelity,
        DeltaRequirement requirement, PostGateAmendmentCheckpoint checkpoint)
    {
        if (requirement.Change == ObservationChange) return ClassifyProvenance(current, requirement);
        var historical = ClassifyValueFidelityProvenance(root, beforeObservation, beforeFidelity, requirement, checkpoint);
        if (!current.TryGetValue(requirement.Capability, out var cap) || !cap.TryGetValue(requirement.Requirement, out var present) ||
            beforeObservation[requirement.Capability][requirement.Requirement] == present) return historical;
        historical.State.Should().BeOneOf(ProvenanceState.Synchronized, ProvenanceState.SupersededByApprovedSuccessor);
        return CreateProvenanceRow(requirement, ProvenanceState.SupersededByApprovedSuccessor, present.Block);
    }

    private static string BeforeObservationSynchronization(string root, string path, string current)
    {
        var contract = ReadObservationContract(root);
        var row = contract.GetProperty("documents").EnumerateArray().SingleOrDefault(item => item.GetProperty("path").GetString() == path);
        return row.ValueKind == JsonValueKind.Undefined ? current : ProjectObservationDocument(root, path, current, contract, row);
    }

    private static string ProjectObservationDocument(string root, string path, string current, JsonElement contract, JsonElement row)
    {
        var prior = DagFacadeApprovedHistory.TextAt(root, contract.GetProperty("base").GetString()!, path);
        Sha256(prior).Should().Be(row.GetProperty("beforeSha256").GetString());
        var amended = prior;
        foreach (var edit in row.GetProperty("edits").EnumerateArray())
        {
            var oldText = edit.GetProperty("before").GetString()!;
            var newText = edit.GetProperty("after").GetString()!;
            oldText.Should().NotBeEmpty();
            amended.Split(oldText, StringSplitOptions.None).Should().HaveCount(2, "each approved replacement must match once");
            amended = amended.Replace(oldText, newText, StringComparison.Ordinal);
        }
        Sha256(amended).Should().Be(row.GetProperty("afterSha256").GetString());
        current.Should().Be(ObservationSynchronized ? amended : prior, "only a reviewed whole document may be projected");
        return prior;
    }
}
