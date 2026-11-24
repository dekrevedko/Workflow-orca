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

        return ValidateAmendmentOwners(root, active, contract, RecoveryChange, RecoverySynchronized,
            (path, current) => BeforeScopedGrantAdmission(root, path, current));
    }

    internal static string BeforeRecoverySynchronization(string root, string path, string current)
    {
        current = BeforeScopedGrantAdmission(root, path, current);
        if (!RecoverySynchronized) return current;
        var contract = ReadRecoveryContract(root);
        var row = contract.GetProperty("documents").EnumerateArray().SingleOrDefault(item => item.GetProperty("path").GetString() == path);
        if (row.ValueKind != JsonValueKind.Undefined)
            return ProjectAmendmentDocument(root, path, current, contract, row, RecoverySynchronized);
        return BeforeAmendmentCanonicalFile(root, path, current, contract, RecoveryProposal);
    }

    // The grant edit list includes insertions with an empty predecessor. Preserve their exact
    // positions by applying the recovery edits to the reviewed grant files.
    internal static string BeforeScopedGrantAdmission(string root, string path, string current)
    {
        const string grant = "openspec/changes/archive/2026-11-23-document-scoped-grant-admission/";
        if (path == "openspec/specs/durable-runtime/spec.md")
        {
            var delta = NormalizeLineEndings(File.ReadAllText(Path.Combine(root, grant, "specs/durable-runtime/spec.md")));
            Sha256(delta).Should().Be("3496ce14cd2100eb985f25c9cb56b3a06954e4c9c602753a5b4fb3302395575b");
            var suffix = "\n" + delta["## ADDED Requirements\n\n".Length..];
            var canonicalPrior = DagFacadeApprovedHistory.TextAt(root, RecoveryProposal, path);
            Sha256(canonicalPrior).Should().Be("9bdd2093e6c1687687f43fbbaf1b9e7d15753c0e02c0736ea4c4974c4aef198d");
            Sha256(canonicalPrior + suffix).Should().Be("f507ac1d9e0122cf856621878c4ed08b175110ca67e944d52448817fbc172a73");
            var recovery = DagFacadeApprovedHistory.TextAt(root, RecoveryImplementation, path);
            current.Should().Be(recovery + suffix, "the scoped grant requirement is appended verbatim to the reviewed recovery text");
            return recovery;
        }

        if (path is not ("docs/implementation/00-stack-decisions.md" or "docs/specs/10-provider-model-and-extensibility.md" or
            "docs/specs/12-acceptance-criteria.md" or "docs/specs/16-requirements-durable-driver.md" or
            "docs/specs/17-selected-mode-capability-matrix.md")) return current;
        var json = NormalizeLineEndings(File.ReadAllText(Path.Combine(root, grant, "numbered-owner-edits.json")));
        Sha256(json).Should().Be("b4ea7f6cae17a8bed02474ee0cbbd87a9cc28d07230da2bfda5d4ff6d8f839b1");
        using var edits = JsonDocument.Parse(json);
        var owner = edits.RootElement.EnumerateArray().Single(row => row.GetProperty("path").GetString() == path);
        var grantOnly = DagFacadeApprovedHistory.TextAt(root, "a0cc173085774b71fd121094e0a6e0016842cf86", path);
        Sha256(grantOnly).Should().Be(owner.GetProperty("afterSha256").GetString());
        var prior = grantOnly;
        foreach (var edit in owner.GetProperty("edits").EnumerateArray().Reverse())
        {
            var after = edit.GetProperty("after").GetString()!;
            prior.Split(after, StringSplitOptions.None).Should().HaveCount(2, "each approved grant edit must match exactly once");
            prior = prior.Replace(after, edit.GetProperty("before").GetString(), StringComparison.Ordinal);
        }
        Sha256(prior).Should().Be(owner.GetProperty("beforeSha256").GetString());
        var recoveryRow = ReadRecoveryContract(root).GetProperty("documents").EnumerateArray()
            .SingleOrDefault(row => row.GetProperty("path").GetString() == path);
        if (recoveryRow.ValueKind == JsonValueKind.Undefined)
        {
            current.Should().Be(grantOnly);
            return prior;
        }
        current.Should().Be(ApplyAmendmentDocumentEdits(grantOnly, recoveryRow), "both reviewed edits must retain their exact positions");
        return ApplyAmendmentDocumentEdits(prior, recoveryRow);
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
