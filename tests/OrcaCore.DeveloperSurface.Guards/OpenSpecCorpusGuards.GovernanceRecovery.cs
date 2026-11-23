using System.Text.Json;
using System.Text.Json.Nodes;
using AwesomeAssertions;

namespace OrcaCore.DeveloperSurface.Guards;

public sealed partial class OpenSpecCorpusGuards
{
    private const string RecoveryChange = "recover-latest-governance-state";
    private const string RecoveryStage = "ApprovedPending";
    private const string RecoveryProposal = "8c083547edaf80e5de39468e2f0c53387f263fa8";
    private const string RecoveryVerdict = "openspec/changes/recover-latest-governance-state/artifacts/contract-approval.md";
    private const string RecoveryVerdictHash = "aed93edec75a90355e105c41db4b0174911be7ab3bae3c76597880bd681d7628";
    private const string RecoveryContractPath = "openspec/changes/" + RecoveryChange + "/artifacts/contract.json";
    private const string RecoveryContractHash = "3105db233df92cb890631f6d5207b0a1bd62e598031764d4e373ac1bc3f7b095";
    private static bool RecoverySynchronized => RecoveryStage != "Proposed";

    private static JsonElement ReadRecoveryContract(string root)
    {
        var text = NormalizeLineEndings(File.ReadAllText(Path.Combine(root, RecoveryContractPath)));
        Sha256(text).Should().Be(RecoveryContractHash);
        using var contract = JsonDocument.Parse(text);
        return contract.RootElement.Clone();
    }

    private static void ProjectRecoveryRegistry(JsonObject current)
    {
        var row = current["latestGovernanceRecovery"]!.AsObject();
        row.Count.Should().Be(7);
        row["reviewedCheckpoint"]!.GetValue<string>().Should().Be(RecoveryProposal);
        row["verdictPath"]!.GetValue<string>().Should().Be(RecoveryVerdict);
        row["verdictSha256"]!.GetValue<string>().Should().Be(RecoveryVerdictHash);
        row["stage"]!.GetValue<string>().Should().Be(RecoveryStage);
        row["contract"]!.GetValue<string>().Should().Be(RecoveryContractPath);
        row["reviewTask"]!.GetValue<string>().Should().Be("1.1");
        row["canonicalSyncTask"]!.GetValue<string>().Should().Be("1.2");
        current.Remove("latestGovernanceRecovery");
    }

    private static HashSet<string> ValidateRecoveryOwners(string root, List<DeltaRequirement> active, JsonElement contract)
    {
        DagFacadeApprovedHistory.TextAt(root, RecoveryProposal, RecoveryContractPath)
            .Should().Be(NormalizeLineEndings(File.ReadAllText(Path.Combine(root, RecoveryContractPath))));
        RunGit(root, "merge-base", "--is-ancestor", contract.GetProperty("base").GetString()!, RecoveryProposal).ExitCode.Should().Be(0);
        RunGit(root, "merge-base", "--is-ancestor", RecoveryProposal, "HEAD").ExitCode.Should().Be(0);
        var verdict = NormalizeLineEndings(File.ReadAllText(Path.Combine(root, RecoveryVerdict)));
        Sha256(verdict).Should().Be(RecoveryVerdictHash);
        verdict.Split('\n').Should().Contain("Verdict: ACCEPT").And.Contain("Scope: recover-latest-governance-state contract and owner routing")
            .And.Contain($"Reviewed proposal checkpoint: `{RecoveryProposal}`")
            .And.Contain($"Reviewed normalized contract SHA-256: `{RecoveryContractHash}`");
        ValidateTaskReference(root, new PostGateTaskReference(RecoveryChange, "1.1", CompleteTaskState));
        ValidateTaskReference(root, new PostGateTaskReference(RecoveryChange, "1.2", CompleteTaskState));
        return ValidateAmendmentOwners(root, active, contract, RecoveryChange, RecoverySynchronized, (_, current) => current);
    }

    internal static string BeforeRecoverySynchronization(string root, string path, string current)
    {
        if (!RecoverySynchronized) return current;
        var contract = ReadRecoveryContract(root);
        var row = contract.GetProperty("documents").EnumerateArray().SingleOrDefault(item => item.GetProperty("path").GetString() == path);
        if (row.ValueKind != JsonValueKind.Undefined)
            return ProjectAmendmentDocument(root, path, current, contract, row, RecoverySynchronized);
        return BeforeAmendmentCanonicalFile(root, path, current, contract, RecoveryProposal);
    }

    private static ProvenanceRow ClassifyRecoveryProvenance(string root,
        IReadOnlyDictionary<string, IReadOnlyDictionary<string, CanonicalRequirement>> current,
        IReadOnlyDictionary<string, IReadOnlyDictionary<string, CanonicalRequirement>> beforeRecovery,
        IReadOnlyDictionary<string, IReadOnlyDictionary<string, CanonicalRequirement>> beforeObservation,
        IReadOnlyDictionary<string, IReadOnlyDictionary<string, CanonicalRequirement>> beforeFidelity,
        DeltaRequirement requirement, PostGateAmendmentCheckpoint checkpoint)
    {
        if (requirement.Change == RecoveryChange) return ClassifyProvenance(current, requirement);
        var previous = ClassifyObservationProvenance(root, beforeRecovery, beforeObservation, beforeFidelity, requirement, checkpoint);
        return AfterAmendment(current, beforeRecovery, requirement, previous);
    }
}
