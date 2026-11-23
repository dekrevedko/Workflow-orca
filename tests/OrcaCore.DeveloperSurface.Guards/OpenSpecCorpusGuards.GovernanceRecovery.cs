using System.Text.Json;
using System.Text.Json.Nodes;
using AwesomeAssertions;

namespace OrcaCore.DeveloperSurface.Guards;

public sealed partial class OpenSpecCorpusGuards
{
    private const string RecoveryChange = "recover-latest-governance-state";
    private const string RecoveryStage = "Complete";
    private const string RecoveryProposal = "8c083547edaf80e5de39468e2f0c53387f263fa8";
    private const string RecoveryImplementation = "6d7cf078dea9b1fd2df412f33c0a8a3c3a133f30";
    private const string RecoveryImplementationTree = "fd31147335de13c9a9d09f192799fe9750d7b80b";
    private const string RecoveryArtifacts = "openspec/changes/" + RecoveryChange + "/artifacts/";
    private const string RecoveryQualification = RecoveryArtifacts + "qualification.md";
    private const string RecoveryQualificationHash = "ee33dcc046a6c320b0a1d68cc73d47fd38df497be0f8a3a7493720e56e81e327";
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
        row.Count.Should().Be(10);
        row["implementationCheckpoint"]!.GetValue<string>().Should().Be(RecoveryImplementation);
        row["qualificationPath"]!.GetValue<string>().Should().Be(RecoveryQualification);
        row["qualificationSha256"]!.GetValue<string>().Should().Be(RecoveryQualificationHash);
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
        ValidateTaskReference(root, new PostGateTaskReference(RecoveryChange, "2.1", CompleteTaskState));
        ValidateTaskReference(root, new PostGateTaskReference(RecoveryChange, "2.2", CompleteTaskState));
        RunGit(root, "merge-base", "--is-ancestor", RecoveryProposal, RecoveryImplementation).ExitCode.Should().Be(0);
        RunGit(root, "merge-base", "--is-ancestor", RecoveryImplementation, "HEAD").ExitCode.Should().Be(0);
        RunGit(root, "show", "-s", "--format=%T", RecoveryImplementation).StandardOutput.Trim().Should().Be(RecoveryImplementationTree);
        var qualification = NormalizeLineEndings(File.ReadAllText(Path.Combine(root, RecoveryQualification)));
        Sha256(qualification).Should().Be(RecoveryQualificationHash);
        qualification.Should().Contain(RecoveryImplementation).And.Contain(RecoveryImplementationTree);
        foreach (var (file, expectedDigest) in new[]
        {
            ("recovery-measurements.md", "3f6fd8b3bc59b39f3ea13956390c6533f12db16ed7f0b995822e5cd0002f9dce"),
            ("recovery-samples.json", "c59b253ec57af7e68b7fd187cc9c8b4bc7bccbc1723522cb66e91b54a272cdf0")
        })
        {
            var evidence = NormalizeLineEndings(File.ReadAllText(Path.Combine(root, RecoveryArtifacts, file)));
            Sha256(evidence).Should().Be(expectedDigest, "{0} is the reviewed evidence file", file);
            qualification.Should().Contain($"]({file})").And.Contain($"`{expectedDigest}`");
        }

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
