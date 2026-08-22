using System.Diagnostics;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using AwesomeAssertions;

namespace OrcaCore.DeveloperSurface.Guards;

[Trait(GuardTraits.Phase, GuardTraits.Phase0)]
[Trait(GuardTraits.Disposition, GuardTraits.Infrastructure)]
public sealed class OpenSpecCorpusGuards
{
    private const string OpenTaskState = "Open";
    private const string CompleteTaskState = "Complete";
    private const string HistoricalCanonicalSourceCommit =
        "ba2478e995023b0712c44705174c2b0e3262f213";
    private const string HistoricalCanonicalRemovalCatalogSha256 =
        "81c06519ae95846b697df5e895e6bcbc9792c3e36afbe441529b3add15008ebd";
    private const int HistoricalCanonicalRemovalCatalogCount = 11;
    private const string ReviewManifestProvenanceFixture =
        "tests/OrcaCore.DeveloperSurface.Guards/Fixtures/review-manifest-provenance.json";
    private const string ReviewManifestCurrentMatchRefreshScript =
        "tests/OrcaCore.DeveloperSurface.Guards/refresh-review-manifest-current-matches.ps1";
    private const string MissingApprovalReviewState = "MissingApproval";
    private const string RejectedReviewState = "Rejected";
    private const string ApprovalAwaitingEvidenceCommitReviewState =
        "ApprovalAwaitingEvidenceCommit";
    private const string ApprovedReviewState = "Approved";
    private const string HarmonizationTaskLedgerPath =
        "openspec/changes/harmonize-downstream-capability-specs/tasks.md";
    private static readonly string[] Task52CanonicalSpecPaths =
    [
        "openspec/specs/management-and-querying/spec.md",
        "openspec/specs/quality-and-verification/spec.md",
        "openspec/specs/repository-foundation/spec.md"
    ];

    private static readonly string[] RequiredPostGateStages =
    [
        "amendment-approval",
        "canonical-open-spec",
        "numbered-requirements",
        "acceptance-criteria",
        "implementation-tasks",
        "executable-evidence",
        "refreeze",
        "independent-approval"
    ];

    [Fact]
    public void ReviewManifests_PreserveRawGitOrderOrDiscloseSetOnlyEvidence()
    {
        var root = FixtureDefinitions.RepositoryRoot();
        var checkpoint = FixtureDefinitions.Read<ReviewManifestProvenanceCheckpoint>(
            ReviewManifestProvenanceFixture);
        checkpoint.SchemaVersion.Should().Be(6);
        checkpoint.Entries.Select(entry => entry.Task).Should().OnlyHaveUniqueItems();
        File.Exists(Path.Combine(
            root,
            ReviewManifestCurrentMatchRefreshScript.Replace('/', Path.DirectorySeparatorChar)))
            .Should().BeTrue(
                "the freeze procedure must provide an executable current-match refresh before validation");

        ValidateCommitRealFreezeProjection(root);

        var reviewRoot = Path.Combine(root, "docs", "review");
        var discoveredManifests = Directory
            .EnumerateFiles(
                reviewRoot,
                "harmonize-downstream-capability-specs-task-*-dirty-manifest-*.txt",
                SearchOption.TopDirectoryOnly)
            .Select(path => RelativePath(root, path))
            .Order(StringComparer.Ordinal)
            .ToArray();
        checkpoint.Entries
            .Select(entry => entry.ManifestPath)
            .Order(StringComparer.Ordinal)
            .Should()
            .Equal(
                discoveredManifests,
                "every harmonization task review manifest must receive an explicit raw-order or set-only disposition");

        foreach (var entry in checkpoint.Entries)
        {
            ValidateReviewManifestProvenance(root, entry);
            ValidateReviewVerdictEvidence(root, entry);
            ValidateReviewStateEvidence(entry);
        }
    }

    [Fact]
    public void ReviewCheckpointProvenance_BlocksTask52CheckpointUntilTask51ApprovalExists()
    {
        var root = FixtureDefinitions.RepositoryRoot();
        var checkpoint = FixtureDefinitions.Read<ReviewManifestProvenanceCheckpoint>(
            ReviewManifestProvenanceFixture);
        var task51 = checkpoint.Entries.Single(entry => entry.Task == "5.1");
        var task52 = checkpoint.Entries.Single(entry => entry.Task == "5.2");
        ValidateReviewVerdictEvidence(root, task51);
        ValidateReviewStateEvidence(task51);
        ValidateReviewVerdictEvidence(root, task52);
        ValidateReviewStateEvidence(task52);

        task51.CheckpointCommit.Should().NotBeNullOrWhiteSpace();
        task51.CheckpointTree.Should().NotBeNullOrWhiteSpace();
        var commit = RunGit(root, "cat-file", "-e", $"{task51.CheckpointCommit}^{{commit}}");
        commit.ExitCode.Should().Be(0, "the Task 5.1 checkpoint under review must exist");
        var parent = RunGit(root, "rev-parse", $"{task51.CheckpointCommit}^");
        parent.ExitCode.Should().Be(0);
        parent.StandardOutput.Trim().Should().Be(task51.BaseCommit);
        var tree = RunGit(root, "rev-parse", $"{task51.CheckpointCommit}^{{tree}}");
        tree.ExitCode.Should().Be(0);
        tree.StandardOutput.Trim().Should().Be(task51.CheckpointTree);

        var originalManifest = ReadReviewManifest(root, task51.ManifestPath);
        var committedPaths = ReadCommittedPaths(root, task51.BaseCommit, task51.CheckpointCommit!);
        originalManifest.Lines
            .Select(ReviewManifestPath)
            .Order(StringComparer.Ordinal)
            .Should()
            .Equal(
                committedPaths,
                "the Task 5.1 checkpoint must contain exactly the files named by its frozen manifest");
        var blobContentRecord = BuildCommittedContentRecord(
            root,
            task51.CheckpointCommit!,
            originalManifest.Lines);
        Encoding.UTF8.GetByteCount(blobContentRecord).Should().Be(task51.CheckpointBlobContentRecordBytes);
        Sha256(blobContentRecord).Should().Be(task51.CheckpointBlobContentRecordSha256);
        var historicalContentRecord = BuildHistoricalContentRecord(
            task51.HistoricalDirtyContentRecordRows);
        historicalContentRecord.Should().Be(
            blobContentRecord,
            "Task 5.1's published dirty-worktree record is byte-identical to its committed blob projection");
        task51.HistoricalDirtyContentRecordBytes.Should().Be(
            task51.CheckpointBlobContentRecordBytes);
        task51.HistoricalDirtyContentRecordSha256.Should().Be(
            task51.CheckpointBlobContentRecordSha256);

        var task51Approvals = task51.VerdictEvidence
            .Where(evidence => evidence.Verdict == "APPROVE")
            .ToArray();
        var task51Rejections = task51.VerdictEvidence
            .Where(evidence => evidence.Verdict == "REJECT")
            .ToArray();
        ValidateTask52ContentDiscoverySemantics();
        var task52ContentMaterializationCommits =
            DiscoverTask52ContentMaterializationCommits(root, task51, task52);
        task52.PreApprovalContentCommits
            .Order(StringComparer.Ordinal)
            .Should()
            .Equal(
                task52ContentMaterializationCommits,
                "Task 5.2 content already landed in history must be discovered from the canonical " +
                "paths and checked ledger state, not inferred from a commit subject");
        task52.PreApprovalContentCommits.Should().OnlyHaveUniqueItems();
        task52.PreApprovalContentCommits.Should().OnlyContain(commitId =>
            Regex.IsMatch(commitId, "^[0-9a-f]{40}$", RegexOptions.CultureInvariant));

        switch (task51.ReviewState)
        {
            case MissingApprovalReviewState:
                task51.ApprovalEvidenceCommit.Should().BeNull(
                    "missing approval must not claim an approval-evidence checkpoint");
                task51.VerdictEvidence.Should().BeEmpty(
                    "a missing-review state may not conceal recorded review evidence");
                task52.CheckpointCommit.Should().BeNull(
                    "Task 5.2 cannot checkpoint on top of an unapproved Task 5.1 base");
                break;
            case RejectedReviewState:
                task51.ApprovalEvidenceCommit.Should().BeNull(
                    "rejected review evidence cannot be relabeled as an approval checkpoint");
                task51Approvals.Should().BeEmpty(
                    "Task 5.1 remains blocked until an immutable APPROVE verdict exists");
                task51Rejections.Should().NotBeEmpty(
                    "the rejected state must retain at least one immutable REJECT verdict");
                task52.CheckpointCommit.Should().BeNull(
                    "Task 5.2 cannot checkpoint on top of a rejected Task 5.1 provenance target");
                var rejectionPath = Path.Combine(
                    root,
                    task51.StateEvidencePath!.Replace('/', Path.DirectorySeparatorChar));
                var rejection = NormalizeLineEndings(File.ReadAllText(rejectionPath));
                rejection.Should().Contain("**Verdict:** **REJECT**");
                rejection.Should().Contain(task51.CheckpointCommit!);
                rejection.Should().Contain("Missing Task 5.1 independent-approval verdict");
                var taskLedger = NormalizeLineEndings(File.ReadAllText(Path.Combine(
                    root,
                    "openspec",
                    "changes",
                    "harmonize-downstream-capability-specs",
                    "tasks.md")));
                taskLedger.Should().Contain("- [ ] 5.2a **REMEDIATION REQUIRED:**");
                break;
            case ApprovalAwaitingEvidenceCommitReviewState:
                task51Approvals.Should().ContainSingle(
                    "the transition state must register exactly one external APPROVE verdict");
                task51Rejections.Should().NotBeEmpty(
                    "immutable rejection history remains part of the checkpoint provenance");
                task51.ApprovalEvidenceCommit.Should().BeNull(
                    "the evidence commit cannot be named until the verdict has first been committed");
                task52.CheckpointCommit.Should().BeNull(
                    "Task 5.2 remains blocked while approval evidence awaits its distinct commit");
                var awaitingLedger = NormalizeLineEndings(File.ReadAllText(Path.Combine(
                    root,
                    "openspec",
                    "changes",
                    "harmonize-downstream-capability-specs",
                    "tasks.md")));
                awaitingLedger.Should().Contain("- [ ] 5.2a **REMEDIATION REQUIRED:**");
                break;
            case ApprovedReviewState:
                task51Approvals.Should().ContainSingle(
                    "an approved Task 5.1 checkpoint must have exactly one immutable verdict");
                task51.ApprovalEvidenceCommit.Should().NotBeNullOrWhiteSpace(
                    "approval evidence must be preserved in a distinct repository checkpoint");
                task51.ReviewedTargetCommit.Should().NotBeNullOrWhiteSpace(
                    "approval must bind the exact independently reviewed Task 5.1 remediation target");
                var approvalCommit = RunGit(
                    root,
                    "cat-file",
                    "-e",
                    $"{task51.ApprovalEvidenceCommit}^{{commit}}");
                approvalCommit.ExitCode.Should().Be(0);
                var approvalAncestry = RunGit(
                    root,
                    "merge-base",
                    "--is-ancestor",
                    task51.ReviewedTargetCommit!,
                    task51.ApprovalEvidenceCommit!);
                approvalAncestry.ExitCode.Should().Be(0,
                    "Task 5.1 approval must be committed after its exact reviewed remediation target");
                var verdictRelativePath = task51Approvals[0].Path;
                var committedVerdict = RunGit(
                    root,
                    "show",
                    $"{task51.ApprovalEvidenceCommit}:{verdictRelativePath}");
                committedVerdict.ExitCode.Should().Be(0,
                    "the approval-evidence checkpoint must preserve the immutable Task 5.1 verdict");
                var verdict = NormalizeLineEndings(File.ReadAllText(Path.Combine(
                    root,
                    verdictRelativePath.Replace('/', Path.DirectorySeparatorChar))));
                verdict.Should().MatchRegex(
                    @"(?mi)^\*\*Verdict:\*\*[ \t]*(?:\*\*)?APPROVE(?:\*\*)?[ \t]*$");
                verdict.Should().Contain(task51.CheckpointCommit!);
                NormalizeLineEndings(committedVerdict.StandardOutput).TrimEnd('\n')
                    .Should().Be(verdict.TrimEnd('\n'),
                        "the working verdict must remain byte-content equivalent to its approval checkpoint");
                foreach (var preApprovalContentCommit in task52.PreApprovalContentCommits)
                {
                    var preApprovalAncestry = RunGit(
                        root,
                        "merge-base",
                        "--is-ancestor",
                        preApprovalContentCommit,
                        task51.ApprovalEvidenceCommit!);
                    preApprovalAncestry.ExitCode.Should().Be(0,
                        "pre-approval Task 5.2 content must be explicitly recorded as history before the approval checkpoint");
                }
                if (task52.CheckpointCommit is not null)
                {
                    var task52Ancestry = RunGit(
                        root,
                        "merge-base",
                        "--is-ancestor",
                        task51.ApprovalEvidenceCommit!,
                        task52.CheckpointCommit);
                    task52Ancestry.ExitCode.Should().Be(0,
                        "the approved Task 5.2 checkpoint must descend from committed Task 5.1 approval evidence");
                }
                break;
            default:
                throw new InvalidDataException(
                    $"Unsupported Task 5.1 review state '{task51.ReviewState}'.");
        }

        ValidateTask52ReviewTransition(root, task51, task52);
    }

    private static void ValidateTask52ReviewTransition(
        string root,
        ReviewManifestProvenanceEntry task51,
        ReviewManifestProvenanceEntry task52)
    {
        task51.ReviewState.Should().Be(
            ApprovedReviewState,
            "Task 5.2 review cannot advance until Task 5.1 approval evidence is activated");
        task51.ApprovalEvidenceCommit.Should().NotBeNullOrWhiteSpace();
        task52.ReviewedTargetCommit.Should().NotBeNullOrWhiteSpace(
            "Task 5.2 review state must bind its exact immutable target");
        var targetAncestry = RunGit(
            root,
            "merge-base",
            "--is-ancestor",
            task51.ApprovalEvidenceCommit!,
            task52.ReviewedTargetCommit!);
        targetAncestry.ExitCode.Should().Be(0,
            "the Task 5.2 reviewed target must descend from committed Task 5.1 approval evidence");

        var approvals = task52.VerdictEvidence
            .Where(evidence => evidence.Verdict == "APPROVE")
            .ToArray();
        var rejections = task52.VerdictEvidence
            .Where(evidence => evidence.Verdict == "REJECT")
            .ToArray();
        switch (task52.ReviewState)
        {
            case RejectedReviewState:
                approvals.Should().BeEmpty();
                rejections.Should().NotBeEmpty();
                task52.ApprovalEvidenceCommit.Should().BeNull();
                RequireOpenTask53(root);
                break;
            case ApprovalAwaitingEvidenceCommitReviewState:
                approvals.Should().ContainSingle();
                rejections.Should().NotBeEmpty();
                task52.ApprovalEvidenceCommit.Should().BeNull(
                    "the verdict checkpoint cannot name itself before it exists");
                RequireOpenTask53(root);
                break;
            case ApprovedReviewState:
                approvals.Should().ContainSingle();
                task52.ApprovalEvidenceCommit.Should().NotBeNullOrWhiteSpace();
                var evidenceCommit = RunGit(
                    root,
                    "cat-file",
                    "-e",
                    $"{task52.ApprovalEvidenceCommit}^{{commit}}");
                evidenceCommit.ExitCode.Should().Be(0);
                var evidenceAncestry = RunGit(
                    root,
                    "merge-base",
                    "--is-ancestor",
                    task52.ReviewedTargetCommit!,
                    task52.ApprovalEvidenceCommit!);
                evidenceAncestry.ExitCode.Should().Be(0,
                    "Task 5.2 approval evidence must follow its exact reviewed target");
                var approval = approvals.Single();
                var committedVerdict = RunGit(
                    root,
                    "show",
                    $"{task52.ApprovalEvidenceCommit}:{approval.Path}");
                committedVerdict.ExitCode.Should().Be(0,
                    "the Task 5.2 approval-evidence checkpoint must preserve the immutable verdict");
                var currentVerdict = NormalizeLineEndings(File.ReadAllText(Path.Combine(
                    root,
                    approval.Path.Replace('/', Path.DirectorySeparatorChar))));
                NormalizeLineEndings(committedVerdict.StandardOutput).TrimEnd('\n')
                    .Should().Be(currentVerdict.TrimEnd('\n'));
                break;
            default:
                throw new InvalidDataException(
                    $"Unsupported Task 5.2 review state '{task52.ReviewState}'.");
        }
    }

    private static void RequireOpenTask53(string root)
    {
        var ledger = NormalizeLineEndings(File.ReadAllText(Path.Combine(
            root,
            HarmonizationTaskLedgerPath.Replace('/', Path.DirectorySeparatorChar))));
        ledger.Should().Contain(
            "- [ ] 5.3 Verify reshape remains the sole active delta owner",
            "Task 5.3 must remain blocked until Task 5.2 approval evidence is activated");
    }

    [Fact]
    public void CanonicalSynchronizationGate_EnumeratesCapabilitiesDeltasAndRequirementOwners()
    {
        var root = FixtureDefinitions.RepositoryRoot();
        var openSpecRoot = Path.Combine(root, "openspec");
        var canonicalRoot = Path.Combine(openSpecRoot, "specs");
        var changesRoot = Path.Combine(openSpecRoot, "changes");

        var canonicalCapabilityDirectories = ReadCapabilityDirectories(canonicalRoot);
        canonicalCapabilityDirectories.Should().NotBeEmpty(
            "the canonical OpenSpec corpus must contain capability specifications");
        RequireSpecFiles(root, canonicalCapabilityDirectories, "canonical");
        var canonicalCapabilities = canonicalCapabilityDirectories
            .Select(GetDirectoryName)
            .ToHashSet(StringComparer.Ordinal);
        var canonicalRequirements = canonicalCapabilityDirectories.ToDictionary(
            GetDirectoryName,
            capabilityDirectory => ReadCanonicalRequirements(
                root,
                Path.Combine(capabilityDirectory, "spec.md")),
            StringComparer.Ordinal);

        var activeRequirementOwners = new List<RequirementOwner>();
        var activeDeltaRequirements = new List<DeltaRequirement>();
        var activeCapabilityDirectories = new List<string>();
        foreach (var changeRoot in Directory
                     .EnumerateDirectories(changesRoot)
                     .Where(path => !string.Equals(
                         GetDirectoryName(path),
                         "archive",
                         StringComparison.Ordinal))
                     .Order(StringComparer.Ordinal))
        {
            var proposalPath = Path.Combine(changeRoot, "proposal.md");
            if (!File.Exists(proposalPath))
            {
                throw new InvalidDataException(
                    $"Active change '{RelativePath(root, changeRoot)}' must contain proposal.md.");
            }

            var declaredCapabilities = ReadDeclaredCapabilities(root, proposalPath);
            var capabilityDirectories = ReadCapabilityDirectories(Path.Combine(changeRoot, "specs"));
            RequireSpecFiles(root, capabilityDirectories, "active");
            ReconcileDeclaredCapabilities(root, changeRoot, declaredCapabilities, capabilityDirectories);

            var unexplainedMissingCapabilities = declaredCapabilities
                .Where(capability =>
                    capability.Kind == CapabilityKind.Modified &&
                    !canonicalCapabilities.Contains(capability.Name))
                .Select(capability => capability.Name)
                .Order(StringComparer.Ordinal)
                .ToArray();
            if (unexplainedMissingCapabilities.Length > 0)
            {
                throw new InvalidDataException(
                    $"Active change '{RelativePath(root, changeRoot)}' modifies canonical capabilities " +
                    $"that do not exist: {string.Join(", ", unexplainedMissingCapabilities)}.");
            }

            activeCapabilityDirectories.AddRange(capabilityDirectories);
            foreach (var capabilityDirectory in capabilityDirectories)
            {
                var capability = GetDirectoryName(capabilityDirectory);
                var capabilityKind = declaredCapabilities
                    .Single(declared => string.Equals(
                        declared.Name,
                        capability,
                        StringComparison.Ordinal))
                    .Kind;
                var deltaRequirements = ReadDeltaRequirements(
                    root,
                    GetDirectoryName(changeRoot),
                    capability,
                    capabilityKind,
                    Path.Combine(capabilityDirectory, "spec.md"));
                activeDeltaRequirements.AddRange(deltaRequirements);
                activeRequirementOwners.AddRange(deltaRequirements.Select(requirement =>
                    new RequirementOwner(
                        requirement.Change,
                        requirement.Capability,
                        requirement.Requirement,
                        requirement.Operation.ToString().ToUpperInvariant())));
            }
        }

        activeCapabilityDirectories.Should().NotBeEmpty(
            "the active OpenSpec corpus must contain capability deltas");
        activeRequirementOwners.Should().NotBeEmpty(
            "every active delta must expose at least one requirement heading to the ownership scan");

        var duplicateOwners = activeRequirementOwners
            .GroupBy(
                owner => $"{owner.Capability}\0{owner.Requirement}",
                StringComparer.Ordinal)
            .Where(group => group.Count() > 1)
            .OrderBy(group => group.Key, StringComparer.Ordinal)
            .Select(group =>
                $"{group.First().Capability} :: {group.First().Requirement} => " +
                string.Join(", ", group
                    .Select(owner => $"{owner.Change} [{owner.Operation}]")
                    .Order(StringComparer.Ordinal)))
            .ToArray();
        duplicateOwners.Should().BeEmpty(
            "one active change may own each capability requirement heading; duplicate owners were: {0}",
            string.Join("; ", duplicateOwners));

        ValidateChangeToCanonicalProvenance(
            root,
            canonicalCapabilityDirectories,
            canonicalRequirements,
            activeDeltaRequirements,
            activeCapabilityDirectories);

        PreserveRuntimeConcurrencyStrayDisposition(root, changesRoot);
    }

    private static void PreserveRuntimeConcurrencyStrayDisposition(string root, string changesRoot)
    {
        const string changeName = "add-runtime-concurrency-limits";
        var activeRoot = Path.Combine(changesRoot, changeName);
        var archiveRoot = Path.Combine(changesRoot, "archive");
        var archivedRoots = Directory.Exists(archiveRoot)
            ? Directory
                .EnumerateDirectories(archiveRoot)
                .Where(path => GetDirectoryName(path).EndsWith(
                    $"-{changeName}",
                    StringComparison.Ordinal))
                .ToArray()
            : [];
        var recordedRoots = archivedRoots
            .Concat(Directory.Exists(activeRoot) ? [activeRoot] : [])
            .Order(StringComparer.Ordinal)
            .ToArray();
        if (recordedRoots.Length != 1)
        {
            throw new InvalidDataException(
                $"Expected exactly one active or archived record for '{changeName}', found " +
                $"{recordedRoots.Length}: {string.Join(", ", recordedRoots.Select(path => RelativePath(root, path)))}.");
        }

        var runtimeConcurrencyRoot = recordedRoots[0];
        var runtimeConcurrencyCapabilities = ReadCapabilityDirectories(
                Path.Combine(runtimeConcurrencyRoot, "specs"))
            .Select(GetDirectoryName)
            .ToArray();
        var declaredRuntimeConcurrencyCapabilities = ReadDeclaredCapabilities(
                root,
                Path.Combine(runtimeConcurrencyRoot, "proposal.md"))
            .Select(capability => capability.Name)
            .Order(StringComparer.Ordinal)
            .ToArray();

        runtimeConcurrencyCapabilities.Should().Equal("runtime-resource-governance");
        declaredRuntimeConcurrencyCapabilities.Should().Equal(runtimeConcurrencyCapabilities);
        Directory.Exists(Path.Combine(
                runtimeConcurrencyRoot,
                "specs",
                "state-driven-runtime"))
            .Should().BeFalse(
                "the former state-driven-runtime directory was a verified stray, not a declared delta");
    }

    private static string[] ReadCapabilityDirectories(string specsRoot) =>
        Directory.Exists(specsRoot)
            ? Directory.EnumerateDirectories(specsRoot).Order(StringComparer.Ordinal).ToArray()
            : [];

    private static void RequireSpecFiles(
        string root,
        IEnumerable<string> capabilityDirectories,
        string corpus)
    {
        var missingSpecs = capabilityDirectories
            .Where(path => !File.Exists(Path.Combine(path, "spec.md")))
            .Select(path => RelativePath(root, path))
            .Order(StringComparer.Ordinal)
            .ToArray();
        if (missingSpecs.Length > 0)
        {
            throw new InvalidDataException(
                $"Every {corpus} capability directory must contain spec.md; missing: " +
                string.Join(", ", missingSpecs));
        }
    }

    private static DeclaredCapability[] ReadDeclaredCapabilities(string root, string proposalPath)
    {
        if (!File.Exists(proposalPath))
        {
            throw new InvalidDataException(
                $"OpenSpec change record '{RelativePath(root, proposalPath)}' must contain proposal.md.");
        }

        var lines = File.ReadAllLines(proposalPath);
        var capabilitiesStart = Array.FindIndex(
            lines,
            line => string.Equals(line, "## Capabilities", StringComparison.Ordinal));
        if (capabilitiesStart < 0)
        {
            throw new InvalidDataException(
                $"Proposal '{RelativePath(root, proposalPath)}' must contain an exact '## Capabilities' heading.");
        }

        var impactStart = Array.FindIndex(
            lines,
            capabilitiesStart + 1,
            line => string.Equals(line, "## Impact", StringComparison.Ordinal));
        if (impactStart < 0)
        {
            throw new InvalidDataException(
                $"Proposal '{RelativePath(root, proposalPath)}' must contain '## Impact' after '## Capabilities'.");
        }

        var declaredCapabilities = new List<DeclaredCapability>();
        CapabilityKind? currentKind = null;
        for (var index = capabilitiesStart + 1; index < impactStart; index++)
        {
            currentKind = lines[index] switch
            {
                "### New Capabilities" => CapabilityKind.New,
                "### Modified Capabilities" => CapabilityKind.Modified,
                _ => currentKind
            };

            var match = Regex.Match(lines[index], @"^- `(?<name>[^`]+)`: ");
            if (!match.Success)
            {
                continue;
            }

            if (currentKind is null)
            {
                throw new InvalidDataException(
                    $"Capability '{match.Groups["name"].Value}' in '{RelativePath(root, proposalPath)}' " +
                    "must follow '### New Capabilities' or '### Modified Capabilities'.");
            }

            declaredCapabilities.Add(new DeclaredCapability(
                match.Groups["name"].Value,
                currentKind.Value));
        }

        var duplicates = declaredCapabilities
            .GroupBy(capability => capability.Name, StringComparer.Ordinal)
            .Where(group => group.Count() > 1)
            .Select(group => group.Key)
            .Order(StringComparer.Ordinal)
            .ToArray();
        if (duplicates.Length > 0)
        {
            throw new InvalidDataException(
                $"Proposal '{RelativePath(root, proposalPath)}' declares duplicate capabilities: " +
                string.Join(", ", duplicates));
        }

        return declaredCapabilities
            .OrderBy(capability => capability.Name, StringComparer.Ordinal)
            .ToArray();
    }

    private static void ReconcileDeclaredCapabilities(
        string root,
        string changeRoot,
        IReadOnlyCollection<DeclaredCapability> declaredCapabilities,
        IEnumerable<string> capabilityDirectories)
    {
        var declaredNames = declaredCapabilities
            .Select(capability => capability.Name)
            .ToHashSet(StringComparer.Ordinal);
        var actualNames = capabilityDirectories
            .Select(GetDirectoryName)
            .ToHashSet(StringComparer.Ordinal);
        var undeclaredDirectories = actualNames
            .Except(declaredNames, StringComparer.Ordinal)
            .Order(StringComparer.Ordinal)
            .ToArray();
        var missingDirectories = declaredNames
            .Except(actualNames, StringComparer.Ordinal)
            .Order(StringComparer.Ordinal)
            .ToArray();
        if (undeclaredDirectories.Length == 0 && missingDirectories.Length == 0)
        {
            return;
        }

        throw new InvalidDataException(
            $"Change '{RelativePath(root, changeRoot)}' proposal/capability-directory mismatch. " +
            $"Undeclared directories: [{string.Join(", ", undeclaredDirectories)}]. " +
            $"Declared capabilities without directories: [{string.Join(", ", missingDirectories)}].");
    }

    private static IReadOnlyDictionary<string, CanonicalRequirement> ReadCanonicalRequirements(
        string root,
        string specPath)
    {
        var requirements = ReadRequirementBlocks(root, specPath, isDelta: false);
        var duplicates = requirements
            .GroupBy(requirement => requirement.Requirement, StringComparer.Ordinal)
            .Where(group => group.Count() > 1)
            .Select(group => group.Key)
            .Order(StringComparer.Ordinal)
            .ToArray();
        if (duplicates.Length > 0)
        {
            throw new InvalidDataException(
                $"Canonical spec '{RelativePath(root, specPath)}' contains duplicate requirement headings: " +
                string.Join(", ", duplicates));
        }

        return requirements.ToDictionary(
            requirement => requirement.Requirement,
            requirement => new CanonicalRequirement(requirement.Requirement, requirement.Block),
            StringComparer.Ordinal);
    }

    private static string ReadCanonicalPreamble(string root, string specPath)
    {
        const string requirementPrefix = "### Requirement: ";
        var text = NormalizeLineEndings(File.ReadAllText(specPath));
        var requirementIndex = text.IndexOf(requirementPrefix, StringComparison.Ordinal);
        if (requirementIndex < 0 ||
            (requirementIndex > 0 && text[requirementIndex - 1] != '\n'))
        {
            throw new InvalidDataException(
                $"Canonical spec '{RelativePath(root, specPath)}' must contain a line-start requirement heading.");
        }

        return text[..requirementIndex];
    }

    private static DeltaRequirement[] ReadDeltaRequirements(
        string root,
        string change,
        string capability,
        CapabilityKind capabilityKind,
        string specPath)
    {
        var requirements = ReadRequirementBlocks(root, specPath, isDelta: true)
            .Select(requirement => new DeltaRequirement(
                change,
                capability,
                capabilityKind,
                requirement.Requirement,
                requirement.Operation ?? throw new InvalidDataException(
                    $"Delta requirement '{requirement.Requirement}' in '{RelativePath(root, specPath)}' " +
                    "has no operation."),
                requirement.Block))
            .ToArray();
        foreach (var removed in requirements.Where(requirement =>
                     requirement.Operation == RequirementOperation.Removed))
        {
            var blockLines = removed.Block.Split('\n');
            if (!blockLines.Any(line =>
                    line.StartsWith("**Reason**:", StringComparison.Ordinal) &&
                    line.Length > "**Reason**:".Length) ||
                !blockLines.Any(line =>
                    line.StartsWith("**Migration**:", StringComparison.Ordinal) &&
                    line.Length > "**Migration**:".Length))
            {
                throw new InvalidDataException(
                    $"Removed requirement '{removed.Capability} :: {removed.Requirement}' in " +
                    $"'{RelativePath(root, specPath)}' must contain non-empty **Reason** and **Migration** lines.");
            }
        }

        if (requirements.Length == 0)
        {
            throw new InvalidDataException(
                $"Active delta '{RelativePath(root, specPath)}' must contain at least one requirement heading.");
        }

        return requirements;
    }

    private static RequirementBlock[] ReadRequirementBlocks(
        string root,
        string specPath,
        bool isDelta)
    {
        return ReadRequirementBlocks(
            File.ReadAllLines(specPath),
            RelativePath(root, specPath),
            isDelta);
    }

    private static RequirementBlock[] ReadRequirementBlocks(
        string[] lines,
        string source,
        bool isDelta)
    {
        var requirements = new List<RequirementBlock>();
        RequirementOperation? operation = null;
        for (var index = 0; index < lines.Length; index++)
        {
            if (isDelta && lines[index].StartsWith("## ", StringComparison.Ordinal))
            {
                operation = lines[index] switch
                {
                    "## ADDED Requirements" => RequirementOperation.Added,
                    "## MODIFIED Requirements" => RequirementOperation.Modified,
                    "## REMOVED Requirements" => RequirementOperation.Removed,
                    _ => null
                };
            }

            const string requirementPrefix = "### Requirement: ";
            if (!lines[index].StartsWith(requirementPrefix, StringComparison.Ordinal))
            {
                continue;
            }

            if (isDelta && operation is null)
            {
                throw new InvalidDataException(
                    $"Requirement heading in '{source}' must follow an " +
                    "ADDED, MODIFIED, or REMOVED Requirements section.");
            }

            var end = index + 1;
            while (end < lines.Length &&
                   !lines[end].StartsWith(requirementPrefix, StringComparison.Ordinal) &&
                   !lines[end].StartsWith("## ", StringComparison.Ordinal))
            {
                end++;
            }

            var lastContentLine = end - 1;
            while (lastContentLine > index && lines[lastContentLine].Length == 0)
            {
                lastContentLine--;
            }

            requirements.Add(new RequirementBlock(
                lines[index][requirementPrefix.Length..],
                operation,
                string.Join('\n', lines[index..(lastContentLine + 1)])));
            index = end - 1;
        }

        return requirements.ToArray();
    }

    private static void ValidateChangeToCanonicalProvenance(
        string root,
        IEnumerable<string> canonicalCapabilityDirectories,
        IReadOnlyDictionary<string, IReadOnlyDictionary<string, CanonicalRequirement>> canonicalRequirements,
        IEnumerable<DeltaRequirement> activeDeltaRequirements,
        IEnumerable<string> activeCapabilityDirectories)
    {
        const string recordFormat =
            "change\\tcapability\\tcapability-kind\\toperation\\trequirement\\tstate\\t" +
            "delta-block-sha256\\tcanonical-block-sha256-or-dash";
        var rows = activeDeltaRequirements
            .Select(requirement => ClassifyProvenance(canonicalRequirements, requirement))
            .OrderBy(row => row.RecordLine, StringComparer.Ordinal)
            .ToArray();
        var record = string.Join('\n', rows.Select(row => row.RecordLine)) + "\n";
        var recordBytes = Encoding.UTF8.GetBytes(record);
        var checkpoint = FixtureDefinitions.Read<OpenSpecProvenanceCheckpoint>(
            "tests/OrcaCore.DeveloperSurface.Guards/Fixtures/openspec-provenance-checkpoint.json");

        checkpoint.SchemaVersion.Should().Be(6);
        checkpoint.RecordFormat.Should().Be(recordFormat);
        rows.Should().HaveCount(checkpoint.RecordCount);
        recordBytes.Should().HaveCount(checkpoint.RecordBytes);
        Sha256(record).Should().Be(checkpoint.RecordSha256);

        var pending = rows
            .Where(row => row.State is
                ProvenanceState.PendingAddition or
                ProvenanceState.PendingAddedCanonicalConflict or
                ProvenanceState.PendingModification or
                ProvenanceState.PendingRemoval)
            .ToArray();
        var actualPending = pending
            .GroupBy(row => row.Requirement.Capability, StringComparer.Ordinal)
            .OrderBy(group => group.Key, StringComparer.Ordinal)
            .Select(group => new ProvenanceCapabilityCount(group.Key, group.Count()))
            .ToArray();
        actualPending.Should().Equal(
            checkpoint.PendingCanonicalOperations
                .OrderBy(item => item.Capability, StringComparer.Ordinal)
                .Select(item => new ProvenanceCapabilityCount(item.Capability, item.Count)));

        var taskLedger = File.ReadAllText(Path.Combine(
            root,
            "openspec",
            "changes",
            "harmonize-downstream-capability-specs",
            "tasks.md"));
        var postGateCheckpoint = FixtureDefinitions.Read<PostGateAmendmentCheckpoint>(
            "tests/OrcaCore.DeveloperSurface.Guards/Fixtures/post-gate-amendment-path.json");
        ValidatePostGateAmendmentPath(root, canonicalRequirements, postGateCheckpoint);
        foreach (var operation in checkpoint.PendingCanonicalOperations)
        {
            var taskBlock = ReadOpenTaskBlock(taskLedger, operation.TurnsGreenTask);
            taskBlock.Should().Contain(
                $"`{operation.Capability}` ({operation.Count})",
                "the turns-green task must explicitly own the capability and exact pending count");
        }

        var actualNewCapabilities = rows
            .Where(row => row.State == ProvenanceState.NewCapabilityOutsideCanonical)
            .GroupBy(row => row.Requirement.Capability, StringComparer.Ordinal)
            .OrderBy(group => group.Key, StringComparer.Ordinal)
            .Select(group => new ProvenanceCapabilityCount(group.Key, group.Count()))
            .ToArray();
        actualNewCapabilities.Should().Equal(
            checkpoint.ActiveNewCapabilitiesOutsideCanonical
                .OrderBy(item => item.Capability, StringComparer.Ordinal)
                .Select(item => new ProvenanceCapabilityCount(item.Capability, item.Count)));
        foreach (var disposition in checkpoint.ActiveNewCapabilitiesOutsideCanonical)
        {
            disposition.Disposition.Should().NotBeNullOrWhiteSpace();
            File.Exists(Path.Combine(root, disposition.Evidence.Replace('/', Path.DirectorySeparatorChar)))
                .Should().BeTrue(
                    "every active new capability outside canonical must cite repository evidence");
        }

        var actualNewCapabilitiesAlreadyCanonical = rows
            .Where(row =>
                row.Requirement.CapabilityKind == CapabilityKind.New &&
                canonicalRequirements.ContainsKey(row.Requirement.Capability))
            .GroupBy(
                row => $"{row.Requirement.Change}\0{row.Requirement.Capability}",
                StringComparer.Ordinal)
            .OrderBy(group => group.Key, StringComparer.Ordinal)
            .Select(group => new ProvenanceDeclaredNewCapability(
                group.First().Requirement.Change,
                group.First().Requirement.Capability,
                group.Count()))
            .ToArray();
        actualNewCapabilitiesAlreadyCanonical.Should().Equal(
            checkpoint.ActiveNewCapabilitiesAlreadyCanonical
                .OrderBy(item => $"{item.Change}\0{item.Capability}", StringComparer.Ordinal)
                .Select(item => new ProvenanceDeclaredNewCapability(
                    item.Change,
                    item.Capability,
                    item.Count)));
        foreach (var disposition in checkpoint.ActiveNewCapabilitiesAlreadyCanonical)
        {
            disposition.Disposition.Should().NotBeNullOrWhiteSpace();
            File.Exists(Path.Combine(root, disposition.Evidence.Replace('/', Path.DirectorySeparatorChar)))
                .Should().BeTrue(
                    "every declared-new capability already in canonical must cite repository evidence");
            var amendment = postGateCheckpoint.Amendments.SingleOrDefault(item =>
                string.Equals(item.Id, disposition.PostGateAmendmentId, StringComparison.Ordinal));
            amendment.Should().NotBeNull(
                "every declared-new/already-canonical capability must link one permanent post-gate record");
            amendment!.OwningChange.Should().Be(disposition.Change);
            amendment.Capability.Should().Be(disposition.Capability);
            amendment.DeclaredRequirementCount.Should().Be(disposition.Count);
        }

        ValidateCanonicalCapabilityInventory(
            root,
            canonicalCapabilityDirectories,
            checkpoint);
        ValidateCapabilityDirectoryInventory(
            root,
            activeCapabilityDirectories,
            checkpoint);
        ValidateHistoricalCanonicalRemovals(root, rows, checkpoint);
        ValidateInfrastructureGuardCiLane(root);
        ValidateReviewArtifact(root, rows, checkpoint);

        checkpoint.SemanticApprovalEligible.Should().Be(pending.Length == 0);
        var semanticApproval = () => RequireSemanticApproval(pending);
        if (checkpoint.SemanticApprovalEligible)
        {
            semanticApproval.Should().NotThrow(
                "the checkpoint fixture declares complete change-to-canonical provenance");
        }
        else
        {
            semanticApproval.Should().Throw<InvalidDataException>()
                .WithMessage($"*{pending.Length} unresolved change-to-canonical operation(s)*")
                .WithMessage("*Structural OpenSpec validation is not semantic approval.*");
        }
    }

    private static void ValidateCapabilityDirectoryInventory(
        string root,
        IEnumerable<string> activeCapabilityDirectories,
        OpenSpecProvenanceCheckpoint checkpoint)
    {
        const string recordFormat =
            "repository-relative active openspec/changes/*/specs/*/ path with forward slashes and one trailing slash";
        var paths = activeCapabilityDirectories
            .Select(path => $"{RelativePath(root, path).TrimEnd('/')}/")
            .Order(StringComparer.Ordinal)
            .ToArray();
        var record = string.Join('\n', paths) + "\n";
        var bytes = Encoding.UTF8.GetBytes(record);

        checkpoint.CapabilityDirectoryRecordFormat.Should().Be(recordFormat);
        paths.Should().HaveCount(checkpoint.CapabilityDirectoryCount);
        bytes.Should().HaveCount(checkpoint.CapabilityDirectoryBytes);
        Sha256(record).Should().Be(checkpoint.CapabilityDirectorySha256);
    }

    private static void ValidateCanonicalCapabilityInventory(
        string root,
        IEnumerable<string> canonicalCapabilityDirectories,
        OpenSpecProvenanceCheckpoint checkpoint)
    {
        const string recordFormat =
            "repository-relative openspec/specs/*/ path with forward slashes and one trailing slash";
        var paths = canonicalCapabilityDirectories
            .Select(path => $"{RelativePath(root, path).TrimEnd('/')}/")
            .Order(StringComparer.Ordinal)
            .ToArray();
        var record = string.Join('\n', paths) + "\n";
        var bytes = Encoding.UTF8.GetBytes(record);

        checkpoint.CanonicalCapabilityRecordFormat.Should().Be(recordFormat);
        paths.Should().HaveCount(checkpoint.CanonicalCapabilityCount);
        bytes.Should().HaveCount(checkpoint.CanonicalCapabilityBytes);
        Sha256(record).Should().Be(checkpoint.CanonicalCapabilitySha256);

        const string preambleRecordFormat =
            "capability\\tnormalized canonical preamble SHA-256, sorted by capability with LF and one final LF";
        var preambles = canonicalCapabilityDirectories
            .Select(path => new CanonicalPreambleHash(
                GetDirectoryName(path),
                Sha256(ReadCanonicalPreamble(root, Path.Combine(path, "spec.md")))))
            .OrderBy(item => item.Capability, StringComparer.Ordinal)
            .ToArray();
        var preambleRecord = string.Join(
            '\n',
            preambles.Select(item => $"{item.Capability}\t{item.NormalizedSha256}")) + "\n";
        var preambleBytes = Encoding.UTF8.GetBytes(preambleRecord);

        checkpoint.CanonicalPreambleRecordFormat.Should().Be(preambleRecordFormat);
        preambles.Should().Equal(
            checkpoint.CanonicalPreambles.OrderBy(item => item.Capability, StringComparer.Ordinal),
            "every canonical preamble, including ## Purpose, must remain byte-stable after LF normalization");
        preambles.Should().HaveCount(checkpoint.CanonicalPreambleCount);
        preambleBytes.Should().HaveCount(checkpoint.CanonicalPreambleBytes);
        Sha256(preambleRecord).Should().Be(checkpoint.CanonicalPreambleSha256);
    }

    private static void ValidatePostGateAmendmentPath(
        string root,
        IReadOnlyDictionary<string, IReadOnlyDictionary<string, CanonicalRequirement>> canonicalRequirements,
        PostGateAmendmentCheckpoint checkpoint)
    {
        checkpoint.SchemaVersion.Should().Be(2);
        checkpoint.RequiredStages.Should().Equal(
            RequiredPostGateStages,
            "every late decision must re-enter each gate in the approved order");
        checkpoint.Amendments.Should().NotBeEmpty(
            "the known Section 7B post-gate decision must have a permanent record");
        checkpoint.Amendments.Select(item => item.Id).Should().OnlyHaveUniqueItems();
        checkpoint.Amendments
            .Select(item => $"{item.OwningChange}\0{item.Capability}")
            .Should().OnlyHaveUniqueItems();

        foreach (var amendment in checkpoint.Amendments)
        {
            amendment.Id.Should().NotBeNullOrWhiteSpace();
            amendment.OwningChange.Should().NotBeNullOrWhiteSpace();
            amendment.Capability.Should().NotBeNullOrWhiteSpace();

            var changeRoot = ResolveChangeRecord(root, amendment.OwningChange);
            var proposalPath = Path.Combine(changeRoot, "proposal.md");
            var capabilityKind = ReadDeclaredCapabilities(root, proposalPath)
                .Single(declared => string.Equals(
                    declared.Name,
                    amendment.Capability,
                    StringComparison.Ordinal))
                .Kind;
            var specPath = Path.Combine(changeRoot, "specs", amendment.Capability, "spec.md");
            var amendmentRequirements = ReadDeltaRequirements(
                root,
                amendment.OwningChange,
                amendment.Capability,
                capabilityKind,
                specPath);
            var requirementIdentities = amendmentRequirements
                .Select(requirement => requirement.Requirement)
                .Order(StringComparer.Ordinal)
                .ToArray();
            amendment.RequirementIdentities.Should().HaveCount(amendment.DeclaredRequirementCount);
            amendment.RequirementIdentities.Should().OnlyHaveUniqueItems();
            requirementIdentities.Should().Equal(
                amendment.RequirementIdentities.Order(StringComparer.Ordinal),
                "the permanent post-gate record must bind the exact requirement identities " +
                "from either the active or dated archived owning change");
            var amendmentRows = amendmentRequirements
                .Select(requirement => ClassifyProvenance(canonicalRequirements, requirement))
                .ToArray();
            amendmentRows.Count(row => row.State is
                    ProvenanceState.PendingAddition or
                    ProvenanceState.PendingAddedCanonicalConflict or
                    ProvenanceState.PendingModification or
                    ProvenanceState.PendingRemoval)
                .Should().Be(
                    amendment.PendingCanonicalOperationCount,
                    "the post-gate record must not conceal unresolved canonical operations");

            ValidateTaskReference(root, amendment.OriginalApprovalTask);
            ValidateTaskReference(root, amendment.OriginalCanonicalSyncTask);
            ValidateTaskReference(root, amendment.AmendmentApprovalTask);
            var canonicalTask = ValidateTaskReference(root, amendment.CanonicalReconciliationTask);
            var canonicalCountClaim =
                $"`{amendment.Capability}` ({amendment.PendingCanonicalOperationCount})";
            if (string.Equals(
                    amendment.CanonicalReconciliationTask.ExpectedState,
                    CompleteTaskState,
                    StringComparison.Ordinal))
            {
                canonicalTask.Should().MatchRegex(
                    $@"(?s)\*\*Completed:\*\*.*?{Regex.Escape(canonicalCountClaim)}",
                    "a completed canonical reconciliation must record its exact resulting pending count in the completion statement");
            }
            else
            {
                canonicalTask.Should().Contain(
                    canonicalCountClaim,
                    "an open canonical reconciliation must name the exact capability and pending count");
            }

            var numberedTask = ValidateTaskReference(root, amendment.NumberedRequirementTask);
            amendment.NumberedRequirementPaths.Should().NotBeEmpty();
            ValidateOwnedPaths(root, numberedTask, amendment.NumberedRequirementPaths);

            var acceptanceTask = ValidateTaskReference(root, amendment.AcceptanceCriteriaTask);
            amendment.AcceptanceCriteriaPaths.Should().NotBeEmpty();
            ValidateOwnedPaths(root, acceptanceTask, amendment.AcceptanceCriteriaPaths);

            amendment.ImplementationTaskCount.Should().BeGreaterThan(0);
            amendment.ImplementationTasks.Should().HaveCount(
                amendment.ImplementationTaskCount,
                "every implementation task in the approved amendment range must remain pinned");
            amendment.ImplementationTasks
                .Select(reference => $"{reference.Change}\0{reference.Task}")
                .Should().OnlyHaveUniqueItems();
            foreach (var implementationTask in amendment.ImplementationTasks)
            {
                ValidateTaskReference(root, implementationTask);
            }

            ValidateTaskReference(root, amendment.RefreezeTask);
            amendment.ExecutableEvidencePaths.Should().NotBeEmpty();
            amendment.ExecutableEvidencePaths.Should().OnlyHaveUniqueItems();
            foreach (var evidencePath in amendment.ExecutableEvidencePaths)
            {
                RequireNonEmptyFile(root, evidencePath, "post-gate executable evidence");
            }

            RequireApprovalEvidence(
                root,
                amendment.AmendmentApprovalEvidencePath,
                amendment.AmendmentApprovalTask.Task);
            RequireNonEmptyFile(root, amendment.RefreezeEvidencePath, "post-gate refreeze evidence");
            RequireApprovalEvidence(
                root,
                amendment.IndependentApprovalEvidencePath,
                amendment.RefreezeTask.Task);
        }

        var artifactPath = RequireNonEmptyFile(
            root,
            checkpoint.ArtifactPath,
            "post-gate amendment artifact");
        var normalizedArtifact = NormalizeLineEndings(File.ReadAllText(artifactPath));
        Sha256(normalizedArtifact).Should().Be(
            checkpoint.ArtifactNormalizedSha256,
            "the human-readable post-gate amendment path must remain byte-accountable");
        foreach (var stage in RequiredPostGateStages)
        {
            normalizedArtifact.Should().Contain($"`{stage}`");
        }

        foreach (var amendment in checkpoint.Amendments)
        {
            normalizedArtifact.Should().Contain($"`{amendment.Id}`");
            normalizedArtifact.Should().Contain(
                $"`{amendment.Capability}` ({amendment.DeclaredRequirementCount})");
            normalizedArtifact.Should().Contain(
                $"exactly {amendment.ImplementationTaskCount} reshape tasks",
                "the human-readable record must pin the implementation-task count independently of the task array");
        }
    }

    private static void ValidateOwnedPaths(
        string root,
        string taskBlock,
        IEnumerable<string> paths)
    {
        foreach (var path in paths)
        {
            RequireNonEmptyFile(root, path, "post-gate normative evidence");
            taskBlock.Should().Contain(
                $"`{path}`",
                "the assigned task must name every post-gate normative target literally");
        }
    }

    private static string ValidateTaskReference(string root, PostGateTaskReference reference)
    {
        var changeRoot = ResolveChangeRecord(root, reference.Change);
        var taskLedgerPath = Path.Combine(changeRoot, "tasks.md");
        var taskLedger = File.ReadAllText(taskLedgerPath);
        var matches = Regex.Matches(
            taskLedger,
            $@"(?ms)^- \[(?<state>[ x])\] {Regex.Escape(reference.Task)}\b.*?(?=^- \[[ x]\] \d+\.\d+\b|\z)",
            RegexOptions.CultureInvariant);
        if (matches.Count != 1)
        {
            throw new InvalidDataException(
                $"Post-gate task reference '{reference.Change}:{reference.Task}' must resolve exactly once; " +
                $"found {matches.Count} in '{RelativePath(root, taskLedgerPath)}'.");
        }

        var actualState = string.Equals(
            matches[0].Groups["state"].Value,
            "x",
            StringComparison.Ordinal)
            ? CompleteTaskState
            : OpenTaskState;
        reference.ExpectedState.Should().BeOneOf(OpenTaskState, CompleteTaskState);
        actualState.Should().Be(
            reference.ExpectedState,
            "post-gate task-state transitions require an explicit reviewed fixture refreeze");
        return matches[0].Value;
    }

    private static string ResolveChangeRecord(string root, string change)
    {
        var changesRoot = Path.Combine(root, "openspec", "changes");
        var activeRoot = Path.Combine(changesRoot, change);
        var archiveRoot = Path.Combine(changesRoot, "archive");
        var archivedRoots = Directory.Exists(archiveRoot)
            ? Directory
                .EnumerateDirectories(archiveRoot)
                .Where(path => GetDirectoryName(path).EndsWith(
                    $"-{change}",
                    StringComparison.Ordinal))
            : [];
        var records = archivedRoots
            .Concat(Directory.Exists(activeRoot) ? [activeRoot] : [])
            .Order(StringComparer.Ordinal)
            .ToArray();
        if (records.Length != 1)
        {
            throw new InvalidDataException(
                $"Post-gate change '{change}' must have exactly one active or dated archived record; " +
                $"found {records.Length}: {string.Join(", ", records.Select(path => RelativePath(root, path)))}.");
        }

        return records[0];
    }

    private static string RequireNonEmptyFile(string root, string relativePath, string description)
    {
        relativePath.Should().NotBeNullOrWhiteSpace();
        var path = Path.Combine(root, relativePath.Replace('/', Path.DirectorySeparatorChar));
        File.Exists(path).Should().BeTrue($"{description} must exist at '{relativePath}'");
        new FileInfo(path).Length.Should().BeGreaterThan(0, $"{description} must not be empty");
        return path;
    }

    private static void RequireApprovalEvidence(
        string root,
        string relativePath,
        string task)
    {
        var path = RequireNonEmptyFile(root, relativePath, "post-gate independent approval evidence");
        var evidence = NormalizeLineEndings(File.ReadAllText(path));
        evidence.Should().MatchRegex(
            $@"(?<![\d.]){Regex.Escape(task)}(?![\d.])",
            "post-gate approval evidence must identify its owning task");
        evidence.Should().MatchRegex(
            @"(?mi)^(?:\*\*Verdict:\*\*[ \t]*APPROVE|\*\*APPROVE\*\*)[ \t]*$",
            "post-gate approval evidence must carry an explicit approving verdict");
    }

    private static void ValidateHistoricalCanonicalRemovals(
        string root,
        IEnumerable<ProvenanceRow> rows,
        OpenSpecProvenanceCheckpoint checkpoint)
    {
        var synchronizedRemovals = rows
            .Where(row =>
                row.Requirement.Operation == RequirementOperation.Removed &&
                row.State == ProvenanceState.Synchronized)
            .Select(row => new HistoricalRemovalIdentity(
                row.Requirement.Capability,
                row.Requirement.Requirement))
            .OrderBy(item => $"{item.Capability}\0{item.Requirement}", StringComparer.Ordinal)
            .ToArray();
        var recordedRemovals = checkpoint.HistoricalCanonicalRemovals
            .Select(item => new HistoricalRemovalIdentity(item.Capability, item.Requirement))
            .OrderBy(item => $"{item.Capability}\0{item.Requirement}", StringComparer.Ordinal)
            .ToArray();
        ValidateHistoricalRemovalCatalogSemantics();
        RequireActiveRemovalsAreCataloged(
            new ActiveSynchronizedRemovalSet(synchronizedRemovals),
            new PermanentHistoricalRemovalCatalog(recordedRemovals));

        checkpoint.HistoricalCanonicalSourceCommit.Should().Be(
            HistoricalCanonicalSourceCommit,
            "the reviewed historical source anchor is part of the executable contract");
        checkpoint.HistoricalCanonicalRemovalCatalogSha256.Should().Be(
            HistoricalCanonicalRemovalCatalogSha256,
            "the embedded removal catalog must be pinned independently of its fixture values");
        var catalog = string.Join(
            '\n',
            checkpoint.HistoricalCanonicalRemovals
                .OrderBy(item => $"{item.Capability}\0{item.Requirement}", StringComparer.Ordinal)
                .Select(item => string.Join(
                    '\t',
                    checkpoint.HistoricalCanonicalSourceCommit,
                    item.SourcePath,
                    item.Capability,
                    item.Requirement,
                    item.CanonicalBlockSha256))) + "\n";
        Sha256(catalog).Should().Be(
            HistoricalCanonicalRemovalCatalogSha256,
            "a fixture-local block/hash pair must not be self-attesting");

        var historicalSources = TryReadHistoricalSources(
            root,
            checkpoint.HistoricalCanonicalSourceCommit,
            checkpoint.HistoricalCanonicalRemovals.Select(item => item.SourcePath));

        foreach (var evidence in checkpoint.HistoricalCanonicalRemovals)
        {
            evidence.SourcePath.Should().Be(
                $"openspec/specs/{evidence.Capability}/spec.md",
                "historical removal evidence must name its canonical capability source");
            evidence.CanonicalBlockSha256.Should().MatchRegex("^[0-9a-f]{64}$");
            evidence.CanonicalBlock.Should().NotBeNullOrWhiteSpace();
            var normalizedBlock = NormalizeLineEndings(evidence.CanonicalBlock);
            var historicalRequirements = ReadRequirementBlocks(
                normalizedBlock.Split('\n'),
                $"embedded historical evidence for {evidence.Capability}",
                isDelta: false);
            historicalRequirements.Should().ContainSingle(
                "each historical-removal fixture entry must embed exactly one canonical requirement block");
            var historicalBlock = historicalRequirements[0];
            historicalBlock.Requirement.Should().Be(
                evidence.Requirement,
                "the embedded canonical block heading must identify its removal record");
            historicalBlock.Block.Should().Be(
                normalizedBlock,
                "the embedded evidence must not contain unhashed preamble or trailing content");

            Sha256(historicalBlock.Block).Should().Be(
                evidence.CanonicalBlockSha256,
                "the fixture must retain the exact reviewed historical canonical requirement block even when Git history is rewritten");

            if (historicalSources is not null)
            {
                var sourceRequirements = ReadRequirementBlocks(
                    NormalizeLineEndings(historicalSources[evidence.SourcePath]).Split('\n'),
                    $"{checkpoint.HistoricalCanonicalSourceCommit}:{evidence.SourcePath}",
                    isDelta: false);
                var sourceRequirement = sourceRequirements.SingleOrDefault(requirement =>
                    string.Equals(
                        requirement.Requirement,
                        evidence.Requirement,
                        StringComparison.Ordinal));
                sourceRequirement.Should().NotBeNull(
                    "the recorded source commit must contain every historical canonical heading");
                sourceRequirement!.Block.Should().Be(
                    historicalBlock.Block,
                    "embedded removal evidence must reproduce the canonical block at its source commit");
            }
        }
    }

    private static void RequireActiveRemovalsAreCataloged(
        ActiveSynchronizedRemovalSet activeRemovals,
        PermanentHistoricalRemovalCatalog permanentCatalog)
    {
        permanentCatalog.Entries.Should().HaveCount(
            HistoricalCanonicalRemovalCatalogCount,
            "the permanent historical-removal catalog must retain reviewed entries after their owning deltas are archived");
        RequireActiveRemovalSubset(activeRemovals, permanentCatalog);
    }

    private static void RequireActiveRemovalSubset(
        ActiveSynchronizedRemovalSet activeRemovals,
        PermanentHistoricalRemovalCatalog permanentCatalog)
    {
        activeRemovals.Entries.Should().BeSubsetOf(
            permanentCatalog.Entries,
            "every currently synchronized removal must have exact immutable canonical-history evidence, while archived removal evidence remains permanently cataloged");
    }

    private static void ValidateHistoricalRemovalCatalogSemantics()
    {
        var active = new HistoricalRemovalIdentity("active-capability", "Active requirement");
        var archived = new HistoricalRemovalIdentity("archived-capability", "Archived requirement");
        RequireActiveRemovalSubset(
            new ActiveSynchronizedRemovalSet([active]),
            new PermanentHistoricalRemovalCatalog([active, archived]));
    }

    private static IReadOnlyDictionary<string, string>? TryReadHistoricalSources(
        string root,
        string commit,
        IEnumerable<string> sourcePaths)
    {
        commit.Should().MatchRegex("^[0-9a-f]{40}$");
        var commitProbe = RunGit(root, "cat-file", "-e", $"{commit}^{{commit}}");
        if (commitProbe.ExitCode != 0)
        {
            return null;
        }

        return sourcePaths
            .Distinct(StringComparer.Ordinal)
            .ToDictionary(
                sourcePath => sourcePath,
                sourcePath =>
                {
                    var result = RunGit(root, "show", $"{commit}:{sourcePath}");
                    result.ExitCode.Should().Be(
                        0,
                        "historical canonical source '{0}:{1}' must be readable; git reported {2}",
                        commit,
                        sourcePath,
                        result.StandardError);
                    return result.StandardOutput;
                },
                StringComparer.Ordinal);
    }

    private static GitResult RunGit(string root, params string[] arguments)
    {
        var startInfo = new ProcessStartInfo("git")
        {
            WorkingDirectory = root,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };
        foreach (var argument in arguments)
        {
            startInfo.ArgumentList.Add(argument);
        }

        using var process = Process.Start(startInfo) ??
            throw new InvalidOperationException("Unable to start git for OpenSpec provenance validation.");
        var standardOutput = process.StandardOutput.ReadToEndAsync();
        var standardError = process.StandardError.ReadToEndAsync();
        process.WaitForExit();
        return new GitResult(
            process.ExitCode,
            standardOutput.GetAwaiter().GetResult(),
            standardError.GetAwaiter().GetResult());
    }

    private static string[] DiscoverTask52ContentMaterializationCommits(
        string root,
        ReviewManifestProvenanceEntry task51,
        ReviewManifestProvenanceEntry task52)
    {
        var canonicalPaths = task52.HistoricalDirtyContentRecordRows
            .Select(row => row.Path)
            .Where(path => path.StartsWith("openspec/specs/", StringComparison.Ordinal))
            .Order(StringComparer.Ordinal)
            .ToArray();
        canonicalPaths.Should().Equal(Task52CanonicalSpecPaths);

        var upperBound = task51.ApprovalEvidenceCommit is null
            ? "HEAD"
            : $"{task51.ApprovalEvidenceCommit}^";
        var historyArguments = new List<string>
        {
            "log",
            "--format=%H",
            $"{task52.BaseCommit}..{upperBound}",
            "--"
        };
        historyArguments.AddRange(canonicalPaths);
        var history = RunGit(root, [.. historyArguments]);
        history.ExitCode.Should().Be(0);

        var candidates = new List<string>();
        foreach (var commitId in NormalizeLineEndings(history.StandardOutput)
                     .Split('\n', StringSplitOptions.RemoveEmptyEntries)
                     .Distinct(StringComparer.Ordinal))
        {
            var parent = RunGit(root, "rev-parse", $"{commitId}^");
            if (parent.ExitCode != 0)
            {
                continue;
            }

            var changedPaths = ReadCommittedPaths(root, parent.StandardOutput.Trim(), commitId);
            if (!TouchesAnyOwnedCanonicalPath(changedPaths, canonicalPaths))
            {
                continue;
            }

            candidates.Add(commitId);
        }

        return candidates
            .Order(StringComparer.Ordinal)
            .ToArray();
    }

    private static void ValidateTask52ContentDiscoverySemantics()
    {
        TouchesAnyOwnedCanonicalPath(
                ["openspec/specs/quality-and-verification/spec.md"],
                Task52CanonicalSpecPaths)
            .Should().BeTrue(
                "a split Task 5.2 landing that touches only one owned capability must be discovered");
        TouchesAnyOwnedCanonicalPath(
                ["openspec/specs/workflow-contracts/spec.md"],
                Task52CanonicalSpecPaths)
            .Should().BeFalse(
                "unowned canonical paths must not be classified as Task 5.2 content");
    }

    private static bool TouchesAnyOwnedCanonicalPath(
        IEnumerable<string> changedPaths,
        IEnumerable<string> canonicalPaths)
    {
        var changed = changedPaths.ToHashSet(StringComparer.Ordinal);
        return canonicalPaths.Any(changed.Contains);
    }

    private static void ValidateReviewManifestProvenance(
        string root,
        ReviewManifestProvenanceEntry entry)
    {
        var requestPath = Path.Combine(
            root,
            entry.RequestPath.Replace('/', Path.DirectorySeparatorChar));
        File.Exists(requestPath).Should().BeTrue(
            "the review request recorded for Task {0} must exist",
            entry.Task);
        var request = NormalizeLineEndings(File.ReadAllText(requestPath));
        request.Should().Contain(Path.GetFileName(entry.ManifestPath));
        entry.HistoricalDirtyContentRecordBytes.Should().BePositive();
        entry.HistoricalDirtyContentRecordSha256.Should().MatchRegex("^[0-9a-f]{64}$");
        if (entry.CheckpointCommit is null)
        {
            entry.CheckpointBlobContentRecordBytes.Should().BeNull();
            entry.CheckpointBlobContentRecordSha256.Should().BeNull();
        }
        else
        {
            entry.CheckpointBlobContentRecordBytes.Should().NotBeNull();
            entry.CheckpointBlobContentRecordSha256.Should().MatchRegex("^[0-9a-f]{64}$");
        }

        if (entry.ReviewedTargetCommit is null)
        {
            entry.ReviewedTargetTree.Should().BeNull();
        }
        else
        {
            entry.ReviewedTargetTree.Should().MatchRegex("^[0-9a-f]{40}$");
            var reviewedCommit = RunGit(
                root,
                "cat-file",
                "-e",
                $"{entry.ReviewedTargetCommit}^{{commit}}");
            reviewedCommit.ExitCode.Should().Be(0, "the exact reviewed target must exist");
            var reviewedTree = RunGit(root, "rev-parse", $"{entry.ReviewedTargetCommit}^{{tree}}");
            reviewedTree.ExitCode.Should().Be(0);
            reviewedTree.StandardOutput.Trim().Should().Be(
                entry.ReviewedTargetTree,
                "the exact reviewed target tree must remain immutable");
        }

        var manifest = ReadReviewManifest(root, entry.ManifestPath);
        manifest.Bytes.Should().HaveCount(entry.ManifestBytes);
        manifest.Lines.Should().HaveCount(entry.LineCount);
        Sha256(manifest.Bytes).Should().Be(entry.ManifestSha256);
        manifest.Lines.Should().OnlyContain(
            line => Regex.IsMatch(
                line,
                @"^[ MADRCU?!]{2} .+$",
                RegexOptions.CultureInvariant),
            "review manifests must contain exact two-character porcelain statuses and paths");
        manifest.Lines.Should().OnlyHaveUniqueItems();

        entry.HistoricalDirtyContentRecordRows.Should().HaveCount(
            entry.LineCount,
            "every frozen manifest entry must retain its exact historical byte evidence");
        entry.HistoricalDirtyContentRecordRows
            .Select(row => $"{row.Status} {row.Path}")
            .Should()
            .Equal(
                manifest.Lines,
                "historical content rows must bind one-to-one to the frozen manifest in its disclosed order");
        entry.HistoricalDirtyContentRecordRows.Should().OnlyHaveUniqueItems(
            row => $"{row.Status}\t{row.Path}");
        entry.HistoricalDirtyContentRecordRows.Should().OnlyContain(row =>
            Regex.IsMatch(row.Status, "^[ MADRCU?!]{2}$", RegexOptions.CultureInvariant) &&
            !string.IsNullOrWhiteSpace(row.Path) &&
            !row.Path.Contains('\\', StringComparison.Ordinal) &&
            row.Bytes > 0 &&
            Regex.IsMatch(row.Sha256, "^[0-9a-f]{64}$", RegexOptions.CultureInvariant));
        var historicalContentRecord = BuildHistoricalContentRecord(
            entry.HistoricalDirtyContentRecordRows);
        Encoding.UTF8.GetByteCount(historicalContentRecord).Should().Be(
            entry.HistoricalDirtyContentRecordBytes);
        Sha256(historicalContentRecord).Should().Be(
            entry.HistoricalDirtyContentRecordSha256,
            "the published historical content anchor must be recomputed from its exact recorded rows");

        var rowsByPath = entry.HistoricalDirtyContentRecordRows.ToDictionary(
            row => row.Path,
            StringComparer.Ordinal);
        entry.CurrentWorktreeMatchPaths.Should().OnlyHaveUniqueItems();
        var maximalCurrentWorktreeMatches = entry.HistoricalDirtyContentRecordRows
            .Where(row =>
            {
                var path = Path.Combine(
                    root,
                    row.Path.Replace('/', Path.DirectorySeparatorChar));
                if (!File.Exists(path))
                {
                    return false;
                }

                var bytes = File.ReadAllBytes(path);
                return bytes.Length == row.Bytes && Sha256(bytes) == row.Sha256;
            })
            .Select(row => row.Path)
            .ToArray();
        entry.CurrentWorktreeMatchPaths.Should().Equal(
            maximalCurrentWorktreeMatches,
            "current-worktree pins must be refreshed with {0} before every freeze and validation packet",
            ReviewManifestCurrentMatchRefreshScript);
        foreach (var currentPath in entry.CurrentWorktreeMatchPaths)
        {
            var currentBytes = File.ReadAllBytes(Path.Combine(
                root,
                currentPath.Replace('/', Path.DirectorySeparatorChar)));
            var historicalRow = rowsByPath[currentPath];
            currentBytes.Should().HaveCount(
                historicalRow.Bytes,
                "historical row '{0}' is still independently verifiable from the current worktree",
                currentPath);
            Sha256(currentBytes).Should().Be(
                historicalRow.Sha256,
                "historical row '{0}' is still independently verifiable from the current worktree",
                currentPath);
        }

        var pathSorted = manifest.Lines
            .OrderBy(ReviewManifestPath, StringComparer.Ordinal)
            .ToArray();
        var pathSortedBytes = Encoding.UTF8.GetBytes(string.Join('\n', pathSorted) + "\n");
        Sha256(pathSortedBytes).Should().Be(entry.PathSortedSha256);

        switch (entry.Disposition)
        {
            case "RawGitOrder":
                RequireRawReviewManifest(entry, manifest.Bytes);
                entry.ManifestSha256.Should().Be(entry.FrozenRawPorcelainSha256);
                entry.PathSortedSha256.Should().NotBe(
                    entry.FrozenRawPorcelainSha256,
                    "the checked-in regression must discriminate path sorting from Git-emitted raw order");
                Action validatePathSortedMutation = () =>
                    RequireRawReviewManifest(entry, pathSortedBytes);
                validatePathSortedMutation.Should().Throw<InvalidDataException>()
                    .WithMessage("*raw Git order*");
                break;
            case "SetOnlyPathSorted":
                entry.ManifestSha256.Should().Be(entry.PathSortedSha256);
                entry.ManifestSha256.Should().NotBe(
                    entry.FrozenRawPorcelainSha256,
                    "set-only evidence must never be presented as the raw porcelain anchor");
                break;
            default:
                throw new InvalidDataException(
                    $"Unsupported review manifest disposition '{entry.Disposition}'.");
        }
    }

    private static void RequireRawReviewManifest(
        ReviewManifestProvenanceEntry entry,
        byte[] candidateBytes)
    {
        var candidateSha256 = Sha256(candidateBytes);
        if (!string.Equals(
                candidateSha256,
                entry.FrozenRawPorcelainSha256,
                StringComparison.Ordinal))
        {
            throw new InvalidDataException(
                $"Task {entry.Task} review manifest must preserve raw Git order: expected " +
                $"{entry.FrozenRawPorcelainSha256}, found {candidateSha256}.");
        }
    }

    private static void ValidateCommitRealFreezeProjection(string root)
    {
        var syntheticStatus = new[]
        {
            " M real-change.md",
            " M index-only-phantom.md",
            "?? new-file.md"
        };
        ProjectCommitRealFreezeManifest(
                syntheticStatus,
                new HashSet<string>(
                    ["real-change.md", "new-file.md"],
                    StringComparer.Ordinal))
            .Should()
            .Equal(
                [" M real-change.md", "?? new-file.md"],
                "status-only entries whose bytes equal HEAD must never enter a frozen manifest");

        var status = RunGit(
            root,
            "status",
            "--porcelain=v1",
            "--untracked-files=all",
            "--no-renames");
        status.ExitCode.Should().Be(0);
        var statusLines = NormalizeLineEndings(status.StandardOutput)
            .Split('\n', StringSplitOptions.RemoveEmptyEntries);
        var tracked = RunGit(root, "diff", "--name-only", "--no-renames", "HEAD");
        tracked.ExitCode.Should().Be(0);
        var untracked = RunGit(root, "ls-files", "--others", "--exclude-standard");
        untracked.ExitCode.Should().Be(0);
        var commitRealPaths = NormalizeLineEndings(tracked.StandardOutput)
            .Split('\n', StringSplitOptions.RemoveEmptyEntries)
            .Concat(NormalizeLineEndings(untracked.StandardOutput)
                .Split('\n', StringSplitOptions.RemoveEmptyEntries))
            .ToHashSet(StringComparer.Ordinal);

        var projection = ProjectCommitRealFreezeManifest(statusLines, commitRealPaths);
        projection.Select(ReviewManifestPath)
            .Order(StringComparer.Ordinal)
            .Should()
            .Equal(
                commitRealPaths.Order(StringComparer.Ordinal),
                "the review freeze entry set must equal tracked content diffs plus untracked files");
    }

    private static string[] ProjectCommitRealFreezeManifest(
        IEnumerable<string> statusLines,
        IReadOnlySet<string> commitRealPaths) =>
        statusLines
            .Where(line => commitRealPaths.Contains(ReviewManifestPath(line)))
            .ToArray();

    private static void ValidateReviewVerdictEvidence(
        string root,
        ReviewManifestProvenanceEntry entry)
    {
        var reviewRoot = Path.Combine(root, "docs", "review");
        var discoveredPaths = Directory
            .EnumerateFiles(
                reviewRoot,
                $"harmonize-downstream-capability-specs-task-{entry.Task.Replace('.', '-')}*verdict-*.md",
                SearchOption.TopDirectoryOnly)
            .Select(path => RelativePath(root, path))
            .Order(StringComparer.Ordinal)
            .ToArray();
        entry.VerdictEvidence
            .Select(evidence => evidence.Path)
            .Order(StringComparer.Ordinal)
            .Should()
            .Equal(
                discoveredPaths,
                $"Task {entry.Task} must register every immutable verdict without overwriting rejection history");

        foreach (var evidence in entry.VerdictEvidence)
        {
            evidence.Verdict.Should().BeOneOf("APPROVE", "REJECT");
            var verdictPath = Path.Combine(
                root,
                evidence.Path.Replace('/', Path.DirectorySeparatorChar));
            File.Exists(verdictPath).Should().BeTrue("recorded review evidence must exist");
            var verdictBytes = File.ReadAllBytes(verdictPath);
            Sha256(verdictBytes).Should().Be(
                evidence.Sha256,
                "immutable review evidence must remain byte-exact");
            var matches = Regex.Matches(
                NormalizeLineEndings(Encoding.UTF8.GetString(verdictBytes)),
                @"(?mi)^\*\*Verdict(?::\*\*|:)[ \t]*(?:\*\*)?(APPROVE|REJECT)(?:\*\*)?[ \t]*$",
                RegexOptions.CultureInvariant);
            matches.Should().ContainSingle("every verdict file must carry exactly one terminal verdict");
            matches[0].Groups[1].Value.Should().Be(evidence.Verdict);
        }
    }

    private static void ValidateReviewStateEvidence(ReviewManifestProvenanceEntry entry)
    {
        var approvals = entry.VerdictEvidence.Count(evidence => evidence.Verdict == "APPROVE");
        var rejections = entry.VerdictEvidence.Count(evidence => evidence.Verdict == "REJECT");
        ReviewVerdictEvidence? stateEvidence = null;
        if (entry.StateEvidencePath is not null)
        {
            var matches = entry.VerdictEvidence
                .Where(evidence => evidence.Path == entry.StateEvidencePath)
                .ToArray();
            matches.Should().ContainSingle(
                "the state-evidence path must resolve to exactly one registered immutable verdict");
            stateEvidence = matches.Single();
        }

        switch (entry.ReviewState)
        {
            case MissingApprovalReviewState:
                entry.VerdictEvidence.Should().BeEmpty();
                entry.StateEvidencePath.Should().BeNull();
                break;
            case RejectedReviewState:
                approvals.Should().Be(0);
                rejections.Should().BeGreaterThan(0);
                stateEvidence.Should().NotBeNull();
                stateEvidence!.Verdict.Should().Be("REJECT");
                break;
            case ApprovalAwaitingEvidenceCommitReviewState:
                approvals.Should().Be(1);
                rejections.Should().BeGreaterThan(0);
                entry.ApprovalEvidenceCommit.Should().BeNull();
                stateEvidence.Should().NotBeNull();
                stateEvidence!.Verdict.Should().Be("APPROVE");
                break;
            case ApprovedReviewState:
                approvals.Should().Be(1);
                stateEvidence.Should().NotBeNull();
                stateEvidence!.Verdict.Should().Be("APPROVE");
                break;
            default:
                throw new InvalidDataException(
                    $"Unsupported review state '{entry.ReviewState}' for Task {entry.Task}.");
        }
    }

    private static ReviewManifest ReadReviewManifest(string root, string relativePath)
    {
        var path = Path.Combine(root, relativePath.Replace('/', Path.DirectorySeparatorChar));
        File.Exists(path).Should().BeTrue("the recorded review manifest must exist");
        var bytes = File.ReadAllBytes(path);
        (bytes.Length >= 3 && bytes[0] == 0xEF && bytes[1] == 0xBB && bytes[2] == 0xBF)
            .Should()
            .BeFalse("review manifests use UTF-8 without BOM");
        var text = Encoding.UTF8.GetString(bytes);
        text.Should().NotContain("\r");
        text.Should().EndWith("\n");
        var lines = text[..^1].Split('\n');
        return new ReviewManifest(bytes, lines);
    }

    private static string[] ReadCommittedPaths(
        string root,
        string baseCommit,
        string checkpointCommit)
    {
        var diff = RunGit(
            root,
            "diff",
            "--name-only",
            $"{baseCommit}..{checkpointCommit}");
        diff.ExitCode.Should().Be(0);
        return NormalizeLineEndings(diff.StandardOutput)
            .Split('\n', StringSplitOptions.RemoveEmptyEntries)
            .Order(StringComparer.Ordinal)
            .ToArray();
    }

    private static string BuildCommittedContentRecord(
        string root,
        string commit,
        IEnumerable<string> manifestLines) =>
        string.Join(
            '\n',
            manifestLines
                .Select(line =>
                {
                    var path = ReviewManifestPath(line);
                    var bytes = ReadGitBlob(root, commit, path);
                    return $"{line[..2]}\t{path}\t{bytes.Length}\t{Sha256(bytes)}";
                })
                .Order(StringComparer.Ordinal)) + "\n";

    private static string BuildHistoricalContentRecord(
        IEnumerable<HistoricalContentRecordRow> rows) =>
        string.Join(
            '\n',
            rows
                .Select(row => $"{row.Status}\t{row.Path}\t{row.Bytes}\t{row.Sha256}")
                .Order(StringComparer.Ordinal)) + "\n";

    private static byte[] ReadGitBlob(
        string root,
        string commit,
        string relativePath)
    {
        var startInfo = new ProcessStartInfo("git")
        {
            WorkingDirectory = root,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };
        startInfo.ArgumentList.Add("cat-file");
        startInfo.ArgumentList.Add("blob");
        startInfo.ArgumentList.Add($"{commit}:{relativePath}");

        using var process = Process.Start(startInfo) ??
            throw new InvalidOperationException("Unable to start git for review content validation.");
        var standardError = process.StandardError.ReadToEndAsync();
        using var output = new MemoryStream();
        process.StandardOutput.BaseStream.CopyTo(output);
        process.WaitForExit();
        if (process.ExitCode != 0)
        {
            throw new InvalidDataException(
                $"Unable to read '{relativePath}' from checkpoint {commit}: " +
                standardError.GetAwaiter().GetResult());
        }

        return output.ToArray();
    }

    private static string ReviewManifestPath(string line)
    {
        if (line.Length < 4 || line[2] != ' ')
        {
            throw new InvalidDataException($"Invalid review manifest record '{line}'.");
        }

        return line[3..];
    }

    private static void ValidateInfrastructureGuardCiLane(string root)
    {
        var workflowPath = Path.Combine(root, ".github", "workflows", "ci.yml");
        var workflow = NormalizeLineEndings(File.ReadAllText(workflowPath));
        var workflowSteps = Regex.Matches(
            workflow,
            @"(?ms)^[ \t]*-[ \t]+(?:name|uses):[^\n]*(?:\n.*?)(?=^[ \t]*-[ \t]+(?:name|uses):|\z)",
            RegexOptions.CultureInvariant)
            .Cast<Match>()
            .ToArray();
        var checkoutSteps = workflowSteps
            .Where(step => Regex.IsMatch(
                step.Value,
                @"(?m)^[ \t]*(?:-[ \t]+)?uses:[ \t]+actions/checkout@[^ \t\r\n]+[ \t]*$",
                RegexOptions.CultureInvariant))
            .ToArray();
        checkoutSteps.Should().NotBeEmpty("CI must check out repository source before running validation");
        checkoutSteps.Should().OnlyContain(
            step => Regex.IsMatch(
                step.Value,
                @"(?m)^[ \t]+fetch-depth:[ \t]+0[ \t]*$",
                RegexOptions.CultureInvariant),
            "every CI checkout must retain full history so available historical corroboration cannot silently degrade to an object-missing skip");

        var guardSteps = Regex.Matches(
            workflow,
            @"(?ms)^[ \t]*-[ \t]+name:[ \t]+Infrastructure contract guards[ \t]*$.*?(?=^[ \t]*-[ \t]+(?:name|uses):|\z)",
            RegexOptions.CultureInvariant);
        guardSteps.Should().ContainSingle(
            "CI must execute the must-be-green infrastructure guard disposition exactly once");
        var normalizedCommand = Regex.Replace(guardSteps[0].Value, @"\s+", " ");
        normalizedCommand.Should().Contain(
            "dotnet test tests/OrcaCore.DeveloperSurface.Guards/OrcaCore.DeveloperSurface.Guards.csproj");
        normalizedCommand.Should().Contain("--no-build --no-restore -c Release");
        normalizedCommand.Should().Contain("--filter \"Disposition=Infrastructure\"");
    }

    private static void ValidateReviewArtifact(
        string root,
        IReadOnlyCollection<ProvenanceRow> rows,
        OpenSpecProvenanceCheckpoint checkpoint)
    {
        var artifactPath = Path.Combine(
            root,
            checkpoint.ArtifactPath.Replace('/', Path.DirectorySeparatorChar));
        File.Exists(artifactPath).Should().BeTrue("the reviewed provenance artifact must exist");
        var normalizedArtifact = NormalizeLineEndings(File.ReadAllText(artifactPath));
        var pendingCount = rows.Count(row => row.State is not
            ProvenanceState.Synchronized and not
            ProvenanceState.NewCapabilityOutsideCanonical);
        RequireArtifactClaim(normalizedArtifact, $"- record rows: {checkpoint.RecordCount}");
        RequireArtifactClaim(
            normalizedArtifact,
            $"- record bytes: {FormatCount(checkpoint.RecordBytes)}");
        RequireArtifactClaim(
            normalizedArtifact,
            $"- record SHA-256: `{checkpoint.RecordSha256}`");
        RequireArtifactClaim(
            normalizedArtifact,
            $"- synchronized operations: {rows.Count(row => row.State == ProvenanceState.Synchronized)}");
        RequireArtifactClaim(
            normalizedArtifact,
            $"- pending canonical operations: {pendingCount}");
        RequireArtifactClaim(
            normalizedArtifact,
            "- declared new-capability requirements outside the canonical set: " +
            rows.Count(row => row.State == ProvenanceState.NewCapabilityOutsideCanonical));
        RequireArtifactClaim(
            normalizedArtifact,
            $"- semantic approval eligible: {(checkpoint.SemanticApprovalEligible ? "yes" : "no")}");
        foreach (var state in Enum.GetValues<ProvenanceState>())
        {
            RequireArtifactClaim(
                normalizedArtifact,
                $"| `{state}` | {rows.Count(row => row.State == state)} |");
        }

        RequireArtifactClaim(
            normalizedArtifact,
            $"- canonical capability directories: {checkpoint.CanonicalCapabilityCount}");
        RequireArtifactClaim(
            normalizedArtifact,
            $"- record bytes: {FormatCount(checkpoint.CanonicalCapabilityBytes)}");
        RequireArtifactClaim(
            normalizedArtifact,
            $"- SHA-256: `{checkpoint.CanonicalCapabilitySha256}`");
        RequireArtifactClaim(
            normalizedArtifact,
            $"- capability directories: {checkpoint.CapabilityDirectoryCount}");
        RequireArtifactClaim(
            normalizedArtifact,
            $"- record bytes: {FormatCount(checkpoint.CapabilityDirectoryBytes)}");
        RequireArtifactClaim(
            normalizedArtifact,
            $"- SHA-256: `{checkpoint.CapabilityDirectorySha256}`");
        foreach (var operation in checkpoint.PendingCanonicalOperations)
        {
            RequireArtifactClaim(
                normalizedArtifact,
                $"| `{operation.Capability}` | {operation.Count} | task {operation.TurnsGreenTask} |");
        }

        Sha256(normalizedArtifact).Should().Be(
            checkpoint.ArtifactNormalizedSha256,
            "the human-readable provenance artifact must remain byte-accountable after LF normalization");
    }

    private static void RequireArtifactClaim(string artifact, string claim) =>
        artifact.Should().Contain(
            claim,
            "the human-readable provenance artifact must agree with live executable evidence");

    private static string FormatCount(int value) =>
        value.ToString("N0", CultureInfo.InvariantCulture);

    private static string ReadOpenTaskBlock(string taskLedger, string task)
    {
        var match = Regex.Match(
            taskLedger,
            $@"(?ms)^- \[ \] {Regex.Escape(task)}\b.*?(?=^- \[[ x]\] \d+\.\d+\b|\z)",
            RegexOptions.CultureInvariant);
        if (!match.Success)
        {
            throw new InvalidDataException(
                $"Every unresolved canonical operation must have one open turns-green task '{task}'.");
        }

        return match.Value;
    }

    private static ProvenanceRow ClassifyProvenance(
        IReadOnlyDictionary<string, IReadOnlyDictionary<string, CanonicalRequirement>> canonicalRequirements,
        DeltaRequirement requirement)
    {
        if (!canonicalRequirements.TryGetValue(requirement.Capability, out var capabilityRequirements))
        {
            if (requirement.CapabilityKind != CapabilityKind.New)
            {
                throw new InvalidDataException(
                    $"Modified capability '{requirement.Capability}' has no canonical spec for " +
                    $"'{requirement.Change} :: {requirement.Requirement}'.");
            }

            return CreateProvenanceRow(
                requirement,
                ProvenanceState.NewCapabilityOutsideCanonical,
                canonicalBlock: null);
        }

        var canonicalExists = capabilityRequirements.TryGetValue(
            requirement.Requirement,
            out var canonicalRequirement);
        var canonicalBlock = canonicalRequirement?.Block;
        var blocksMatch = canonicalExists &&
                          string.Equals(requirement.Block, canonicalBlock, StringComparison.Ordinal);
        var state = requirement.Operation switch
        {
            RequirementOperation.Added when !canonicalExists => ProvenanceState.PendingAddition,
            RequirementOperation.Added when blocksMatch => ProvenanceState.Synchronized,
            RequirementOperation.Added => ProvenanceState.PendingAddedCanonicalConflict,
            RequirementOperation.Modified when !canonicalExists => throw new InvalidDataException(
                $"Modified requirement heading '{requirement.Capability} :: {requirement.Requirement}' " +
                $"from '{requirement.Change}' does not match canonical verbatim."),
            RequirementOperation.Modified when blocksMatch => ProvenanceState.Synchronized,
            RequirementOperation.Modified => ProvenanceState.PendingModification,
            RequirementOperation.Removed when canonicalExists => ProvenanceState.PendingRemoval,
            RequirementOperation.Removed => ProvenanceState.Synchronized,
            _ => throw new ArgumentOutOfRangeException(nameof(requirement.Operation))
        };

        return CreateProvenanceRow(requirement, state, canonicalBlock);
    }

    private static ProvenanceRow CreateProvenanceRow(
        DeltaRequirement requirement,
        ProvenanceState state,
        string? canonicalBlock)
    {
        var canonicalHash = canonicalBlock is null ? "-" : Sha256(canonicalBlock);
        var line = string.Join('\t',
            requirement.Change,
            requirement.Capability,
            requirement.CapabilityKind.ToString().ToUpperInvariant(),
            requirement.Operation.ToString().ToUpperInvariant(),
            requirement.Requirement,
            state.ToString(),
            Sha256(requirement.Block),
            canonicalHash);
        return new ProvenanceRow(requirement, state, line);
    }

    private static void RequireSemanticApproval(IReadOnlyCollection<ProvenanceRow> pending)
    {
        if (pending.Count == 0)
        {
            return;
        }

        var summary = pending
            .GroupBy(row => row.Requirement.Capability, StringComparer.Ordinal)
            .OrderBy(group => group.Key, StringComparer.Ordinal)
            .Select(group => $"{group.Key}={group.Count()}");
        throw new InvalidDataException(
            $"Checkpoint provenance has {pending.Count} unresolved change-to-canonical operation(s): " +
            $"{string.Join(", ", summary)}. Structural OpenSpec validation is not semantic approval.");
    }

    private static string Sha256(string content) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(content))).ToLowerInvariant();

    private static string Sha256(byte[] content) =>
        Convert.ToHexString(SHA256.HashData(content)).ToLowerInvariant();

    private static string NormalizeLineEndings(string content) =>
        content.Replace("\r\n", "\n", StringComparison.Ordinal).Replace('\r', '\n');

    private static string GetDirectoryName(string path) =>
        Path.GetFileName(path) ??
        throw new InvalidDataException($"Path '{path}' has no directory name.");

    private static string RelativePath(string root, string path) =>
        Path.GetRelativePath(root, path).Replace('\\', '/');

    private enum CapabilityKind
    {
        New,
        Modified
    }

    private enum RequirementOperation
    {
        Added,
        Modified,
        Removed
    }

    private enum ProvenanceState
    {
        Synchronized,
        PendingAddition,
        PendingAddedCanonicalConflict,
        PendingModification,
        PendingRemoval,
        NewCapabilityOutsideCanonical
    }

    private sealed record DeclaredCapability(string Name, CapabilityKind Kind);

    private sealed record RequirementBlock(
        string Requirement,
        RequirementOperation? Operation,
        string Block);

    private sealed record CanonicalRequirement(string Requirement, string Block);

    private sealed record DeltaRequirement(
        string Change,
        string Capability,
        CapabilityKind CapabilityKind,
        string Requirement,
        RequirementOperation Operation,
        string Block);

    private sealed record ProvenanceRow(
        DeltaRequirement Requirement,
        ProvenanceState State,
        string RecordLine);

    private sealed record ProvenanceCapabilityCount(string Capability, int Count);

    private sealed record OpenSpecProvenanceCheckpoint(
        int SchemaVersion,
        string RecordFormat,
        int RecordCount,
        int RecordBytes,
        string RecordSha256,
        bool SemanticApprovalEligible,
        ProvenancePendingCapability[] PendingCanonicalOperations,
        ProvenanceNewCapability[] ActiveNewCapabilitiesOutsideCanonical,
        ProvenanceNewCanonicalCapability[] ActiveNewCapabilitiesAlreadyCanonical,
        string HistoricalCanonicalSourceCommit,
        string HistoricalCanonicalRemovalCatalogSha256,
        HistoricalCanonicalRemoval[] HistoricalCanonicalRemovals,
        string CanonicalCapabilityRecordFormat,
        int CanonicalCapabilityCount,
        int CanonicalCapabilityBytes,
        string CanonicalCapabilitySha256,
        string CanonicalPreambleRecordFormat,
        int CanonicalPreambleCount,
        int CanonicalPreambleBytes,
        string CanonicalPreambleSha256,
        CanonicalPreambleHash[] CanonicalPreambles,
        string CapabilityDirectoryRecordFormat,
        int CapabilityDirectoryCount,
        int CapabilityDirectoryBytes,
        string CapabilityDirectorySha256,
        string ArtifactPath,
        string ArtifactNormalizedSha256);

    private sealed record CanonicalPreambleHash(
        string Capability,
        string NormalizedSha256);

    private sealed record ProvenancePendingCapability(
        string Capability,
        int Count,
        string TurnsGreenTask);

    private sealed record ProvenanceNewCapability(
        string Capability,
        int Count,
        string Disposition,
        string Evidence);

    private sealed record ProvenanceDeclaredNewCapability(
        string Change,
        string Capability,
        int Count);

    private sealed record ProvenanceNewCanonicalCapability(
        string Change,
        string Capability,
        int Count,
        string Disposition,
        string PostGateAmendmentId,
        string Evidence);

    private sealed record PostGateAmendmentCheckpoint(
        int SchemaVersion,
        string[] RequiredStages,
        PostGateAmendment[] Amendments,
        string ArtifactPath,
        string ArtifactNormalizedSha256);

    private sealed record PostGateAmendment(
        string Id,
        string OwningChange,
        string Capability,
        int DeclaredRequirementCount,
        int PendingCanonicalOperationCount,
        string[] RequirementIdentities,
        PostGateTaskReference OriginalApprovalTask,
        PostGateTaskReference OriginalCanonicalSyncTask,
        PostGateTaskReference AmendmentApprovalTask,
        PostGateTaskReference CanonicalReconciliationTask,
        PostGateTaskReference NumberedRequirementTask,
        PostGateTaskReference AcceptanceCriteriaTask,
        int ImplementationTaskCount,
        PostGateTaskReference[] ImplementationTasks,
        PostGateTaskReference RefreezeTask,
        string[] NumberedRequirementPaths,
        string[] AcceptanceCriteriaPaths,
        string[] ExecutableEvidencePaths,
        string AmendmentApprovalEvidencePath,
        string RefreezeEvidencePath,
        string IndependentApprovalEvidencePath);

    private sealed record PostGateTaskReference(
        string Change,
        string Task,
        string ExpectedState);

    private sealed record HistoricalCanonicalRemoval(
        string Capability,
        string Requirement,
        string SourcePath,
        string CanonicalBlockSha256,
        string CanonicalBlock);

    private sealed record GitResult(
        int ExitCode,
        string StandardOutput,
        string StandardError);

    private sealed record ReviewManifestProvenanceCheckpoint(
        int SchemaVersion,
        ReviewManifestProvenanceEntry[] Entries);

    private sealed record ReviewManifestProvenanceEntry(
        string Task,
        string RequestPath,
        string ManifestPath,
        string Disposition,
        int LineCount,
        int ManifestBytes,
        string ManifestSha256,
        string FrozenRawPorcelainSha256,
        string PathSortedSha256,
        string BaseCommit,
        string? CheckpointCommit,
        string? CheckpointTree,
        string? ApprovalEvidenceCommit,
        string? ReviewedTargetCommit,
        string? ReviewedTargetTree,
        string[] PreApprovalContentCommits,
        int HistoricalDirtyContentRecordBytes,
        string HistoricalDirtyContentRecordSha256,
        HistoricalContentRecordRow[] HistoricalDirtyContentRecordRows,
        string[] CurrentWorktreeMatchPaths,
        int? CheckpointBlobContentRecordBytes,
        string? CheckpointBlobContentRecordSha256,
        string ReviewState,
        string? StateEvidencePath,
        ReviewVerdictEvidence[] VerdictEvidence);

    private sealed record HistoricalContentRecordRow(
        string Status,
        string Path,
        int Bytes,
        string Sha256);

    private sealed record ReviewVerdictEvidence(
        string Path,
        string Verdict,
        string Sha256);

    private sealed record ReviewManifest(
        byte[] Bytes,
        string[] Lines);

    private sealed record HistoricalRemovalIdentity(
        string Capability,
        string Requirement);

    private sealed record ActiveSynchronizedRemovalSet(
        IReadOnlyCollection<HistoricalRemovalIdentity> Entries);

    private sealed record PermanentHistoricalRemovalCatalog(
        IReadOnlyCollection<HistoricalRemovalIdentity> Entries);

    private sealed record RequirementOwner(
        string Change,
        string Capability,
        string Requirement,
        string Operation);
}
