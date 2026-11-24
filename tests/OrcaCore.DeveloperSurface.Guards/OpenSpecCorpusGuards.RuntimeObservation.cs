using System.Text.Json;
using System.Text.Json.Nodes;
using AwesomeAssertions;

namespace OrcaCore.DeveloperSurface.Guards;

public sealed partial class OpenSpecCorpusGuards
{
    private const string ObservationChange = "bound-runtime-observation";
    private const string ObservationStage = "Complete";
    private const string ObservationImplementation = "3e96c8cfe652f03d4f482b2515f59354033d22f3";
    private const string ObservationImplementationTree = "31bb381b5a13e0d789f21e442cba7c5774515b69";
    private const string ObservationQualification = "openspec/changes/" + ObservationChange + "/artifacts/qualification.md";
    private const string ObservationQualificationHash = "5a379a66992c5ba43065ce8a161077a950385bd2a88e0803ab20f3f265f0116f";
    internal const string ObservationProposal = "b857386dd7d43bbe1d0a51e9ce8e331fa4b17b82";
    private const string ObservationVerdict = "openspec/changes/bound-runtime-observation/artifacts/contract-approval.md";
    private const string ObservationVerdictHash = "a090616a8f89cb7cb5394d2f51589f0d0b43de7f373f0a784bb96e0455ffa380";
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
        var grant = current["scopedGrantAdmission"]!.AsObject();
        Sha256(grant.ToJsonString()).Should().Be("d94d0d26de484bad99e6ea4bd8c4eb3f9bf262d4e99e6bb4caa73ee72dd7d795", "only the reviewed scoped grant amendment may be projected");
        current.Remove("scopedGrantAdmission");

        var row = current["durableOutputObservation"]!.AsObject();
        row.Count.Should().Be(10);
        row["implementationCheckpoint"]!.GetValue<string>().Should().Be(ObservationImplementation);
        row["qualificationPath"]!.GetValue<string>().Should().Be(ObservationQualification);
        row["qualificationSha256"]!.GetValue<string>().Should().Be(ObservationQualificationHash);
        row["reviewedCheckpoint"]!.GetValue<string>().Should().Be(ObservationProposal);
        row["verdictPath"]!.GetValue<string>().Should().Be(ObservationVerdict);
        row["verdictSha256"]!.GetValue<string>().Should().Be(ObservationVerdictHash);
        row["stage"]!.GetValue<string>().Should().Be(ObservationStage);
        row["contract"]!.GetValue<string>().Should().Be(ObservationContractPath);
        row["reviewTask"]!.GetValue<string>().Should().Be("1.1");
        row["canonicalSyncTask"]!.GetValue<string>().Should().Be("1.2");
        current.Remove("durableOutputObservation");
    }

    private static HashSet<string> ValidateObservationOwners(string root, List<DeltaRequirement> active, JsonElement contract)
    {
        DagFacadeApprovedHistory.TextAt(root, ObservationProposal, ObservationContractPath)
            .Should().Be(NormalizeLineEndings(File.ReadAllText(Path.Combine(root, ObservationContractPath))));
        RunGit(root, "merge-base", "--is-ancestor", contract.GetProperty("base").GetString()!, ObservationProposal).ExitCode.Should().Be(0);
        RunGit(root, "merge-base", "--is-ancestor", ObservationProposal, "HEAD").ExitCode.Should().Be(0);
        var verdict = NormalizeLineEndings(File.ReadAllText(Path.Combine(root, ObservationVerdict)));
        Sha256(verdict).Should().Be(ObservationVerdictHash);
        verdict.Split('\n').Should().Contain("Verdict: ACCEPT").And.Contain("Scope: bound-runtime-observation contract and owner routing")
            .And.Contain($"Reviewed proposal checkpoint: `{ObservationProposal}`")
            .And.Contain($"Reviewed normalized contract SHA-256: `{ObservationContractHash}`");
        ValidateTaskReference(root, new PostGateTaskReference(ObservationChange, "1.1", CompleteTaskState));
        ValidateTaskReference(root, new PostGateTaskReference(ObservationChange, "1.2", CompleteTaskState));
        ValidateTaskReference(root, new PostGateTaskReference(ObservationChange, "2.1", CompleteTaskState));
        ValidateTaskReference(root, new PostGateTaskReference(ObservationChange, "2.2", CompleteTaskState));
        RunGit(root, "merge-base", "--is-ancestor", ObservationProposal, ObservationImplementation).ExitCode.Should().Be(0);
        RunGit(root, "merge-base", "--is-ancestor", ObservationImplementation, "HEAD").ExitCode.Should().Be(0);
        RunGit(root, "show", "-s", "--format=%T", ObservationImplementation).StandardOutput.Trim().Should().Be(ObservationImplementationTree);
        var qualification = NormalizeLineEndings(File.ReadAllText(Path.Combine(root, ObservationQualification)));
        Sha256(qualification).Should().Be(ObservationQualificationHash);
        qualification.Should().Contain(ObservationImplementation).And.Contain(ObservationImplementationTree);

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
            ProjectObservationDocument(root, path, BeforeScopedGrantDocumentation(root, path, current), contract, row);
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

    // Later grant documentation amended owners that earlier contracts pin in full.
    // Reconstruct each historical document from only its reviewed changes before checking those pins.
    private static string BeforeScopedGrantDocumentation(string root, string path, string current)
    {
        if (path is not ("docs/implementation/00-stack-decisions.md" or "docs/specs/10-provider-model-and-extensibility.md" or
            "docs/specs/12-acceptance-criteria.md" or "docs/specs/16-requirements-durable-driver.md" or
            "docs/specs/17-selected-mode-capability-matrix.md")) return current;
        const string editsPath = "openspec/changes/archive/2026-11-23-document-scoped-grant-admission/numbered-owner-edits.json";
        var json = NormalizeLineEndings(File.ReadAllText(Path.Combine(root, editsPath)));
        Sha256(json).Should().Be("b4ea7f6cae17a8bed02474ee0cbbd87a9cc28d07230da2bfda5d4ff6d8f839b1");
        using var edits = JsonDocument.Parse(json);
        var owner = edits.RootElement.EnumerateArray().Single(row => row.GetProperty("path").GetString() == path);
        Sha256(current).Should().Be(owner.GetProperty("afterSha256").GetString());
        foreach (var edit in owner.GetProperty("edits").EnumerateArray().Reverse())
        {
            var after = edit.GetProperty("after").GetString()!;
            current.Split(after, StringSplitOptions.None).Should().HaveCount(2);
            current = current.Replace(after, edit.GetProperty("before").GetString(), StringComparison.Ordinal);
        }
        Sha256(current).Should().Be(owner.GetProperty("beforeSha256").GetString());
        return current;
    }

    private static string BeforeObservationSynchronization(string root, string path, string current)
    {
        current = BeforeScopedGrantDocumentation(root, path, current);
        var contract = ReadObservationContract(root);
        var row = contract.GetProperty("documents").EnumerateArray().SingleOrDefault(item => item.GetProperty("path").GetString() == path);
        if (row.ValueKind != JsonValueKind.Undefined)
            return ProjectObservationDocument(root, path, current, contract, row);
        return BeforeObservationCanonicalFile(root, path, current, contract);
    }

    internal static string BeforeObservationCanonicalFile(string root, string path, string current)
        => BeforeObservationCanonicalFile(root, path, current, ReadObservationContract(root));

    private static string BeforeObservationCanonicalFile(string root, string path, string current, JsonElement contract)
    {
        if (path == "openspec/specs/durable-runtime/spec.md")
        {
            const string deltaPath = "openspec/changes/archive/2026-11-23-document-scoped-grant-admission/specs/durable-runtime/spec.md";
            var delta = NormalizeLineEndings(File.ReadAllText(Path.Combine(root, deltaPath)));
            Sha256(delta).Should().Be("3496ce14cd2100eb985f25c9cb56b3a06954e4c9c602753a5b4fb3302395575b");
            var suffix = "\n" + delta["## ADDED Requirements\n\n".Length..];
            current.Should().EndWith(suffix, "the reviewed grant requirement is appended verbatim");
            current = current[..^suffix.Length];
            Sha256(current).Should().Be("9bdd2093e6c1687687f43fbbaf1b9e7d15753c0e02c0736ea4c4974c4aef198d", "prior canonical requirements retain their reviewed bytes");
        }

        var replacements = contract.GetProperty("successors").EnumerateArray()
            .Where(row => path == $"openspec/specs/{row.GetProperty("capability").GetString()}/spec.md").ToArray();
        if (replacements.Length == 0) return current;
        var prior = DagFacadeApprovedHistory.TextAt(root, ObservationProposal, path);
        var expected = prior;
        foreach (var row in replacements)
        {
            var cap = row.GetProperty("capability").GetString()!;
            var heading = row.GetProperty("requirement").GetString()!;
            var chain = row.GetProperty("ownerChain").Deserialize<string[]>()!;
            var before = ReadResolvedPostGateOwner(root, chain[^2], cap, heading);
            var after = ReadResolvedPostGateOwner(root, chain[^1], cap, heading);
            Sha256(before.Block).Should().Be(row.GetProperty("beforeSha256").GetString());
            Sha256(after.Block).Should().Be(row.GetProperty("afterSha256").GetString());
            expected.Split(before.Block, StringSplitOptions.None).Should().HaveCount(2);
            expected = expected.Replace(before.Block, after.Block, StringComparison.Ordinal);
        }
        current.Should().Be(expected, "the five successor blocks are the only approved canonical edits");
        return prior;
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
