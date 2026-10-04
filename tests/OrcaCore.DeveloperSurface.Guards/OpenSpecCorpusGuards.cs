using System.Diagnostics;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using AwesomeAssertions;

namespace OrcaCore.DeveloperSurface.Guards;

[Trait(GuardTraits.Phase, GuardTraits.Phase0)]
[Trait(GuardTraits.Disposition, GuardTraits.Infrastructure)]
public sealed class OpenSpecCorpusGuards
{
    private const string OpenTaskState = "Open";
    private const string CompleteTaskState = "Complete";
    private const string ProposedPostGateStage = "Proposed";
    private const string ApprovedPendingPostGateStage = "ApprovedPending";
    private const string CompletePostGateStage = "Complete";
    private const string DagFriendCloseoutArtifact =
        "openspec/changes/admit-dag-authoring-friend-boundary/artifacts/" +
        "task-3-2-authoring-friend-closeout-2026-09-29.md";
    private const string DagFriendCloseoutArtifactSha256 =
        "00c1fd7e1e3add9531cd1dc46d8103f023825ef6965a1a7482cf8e435671385c";
    private static readonly PostGateProposedSuccessor[] ExactProposedDagSuccessors =
    [
        new(
            "admit-dag-authoring-friend-boundary",
            "reshape-developer-facing-interfaces",
            "developer-facing-surface",
            "Implementation package boundaries use exact internal friends",
            "ADDED",
            "MODIFIED",
            ProposedPostGateStage,
            "1.3"),
        new(
            "admit-dag-authoring-friend-boundary",
            "reshape-developer-facing-interfaces",
            "repository-foundation",
            "Dependency direction remains one-way",
            "MODIFIED",
            "MODIFIED",
            ProposedPostGateStage,
            "1.3")
    ];
    private static readonly PostGateApprovedPendingSuccessor[] ExactApprovedPendingDagSuccessors =
    [
        new(
            "developer-facing-surface",
            "Implementation package boundaries use exact internal friends",
            ApprovedPendingPostGateStage,
            "e12c77e5d3a8eaf30dbe31a68ffe4baec3b105023dd5f1512d3f062917a312d0",
            "bed102a2e4c98598b30cbb741c956a236f2050d27d88e6fc567b915f5b260793"),
        new(
            "repository-foundation",
            "Dependency direction remains one-way",
            ApprovedPendingPostGateStage,
            "d0d512ea59ea8da595770b5437d7faa7565773c6cd8845850d2dea6010b54b29",
            "bbae0c226c6824570650d1f6f980e3b74e6c35cb196784581eef20e6e3563ce9")
    ];
    private static readonly PostGateApprovedPendingEvidence ExactDagFriendApprovalEvidence = new(
        ApprovedPendingPostGateStage,
        "89a2b475ebaeed82de2fd2edaf1f31b371bd71a4",
        "bbac0977bffce25881a7f27f48b075cda7dc206f",
        "docs/review/developer-facing-interface-section-08-task-8-2-dag-friend-contract-yyy-1-remediation-independent-review-verdict-2026-09-27.md",
        "398031734b5b98d3244e19e85f519c464aae80f2faf843e0c34d3b0c180cdf2f",
        "1.2",
        "1.3",
        "1.4",
        "2.1");
    private static readonly PostGateCompleteEvidence ExactDagFriendCompleteEvidence = new(
        CompletePostGateStage,
        "a9f835f939d683500ca231c7ba491ab8eae2aaae",
        "cf11b3f6732f11250ba3a0255114bcdc4ae00060",
        "055e7b8e71e8dfe79e76f267f8782b7f6f79f7b8",
        "docs/review/developer-facing-interface-section-08-task-8-2-dag-authoring-source-zzz-remediation-independent-review-verdict-2026-09-28.md",
        "513871b82c87f5949d4801108f4f1c901b8f8cfe7084121217359cf96a9ccd4a",
        "docs/review/developer-facing-interface-section-08-task-8-2-dag-authoring-source-zzz-remediation-dirty-manifest-2026-09-28.txt",
        "c9462505515394deb34865b8fd9e5c4dd94c6427733498c89cae586f98ec020a",
        "docs/review/developer-facing-interface-section-08-task-8-2-dag-authoring-source-zzz-remediation-independent-review-request-2026-09-28.md",
        "c2e58b564b92bacd8ab32ee9f6ada943a0ffb28bbbcdf1fd8a9e837805b444bf",
        5,
        ["2.1", "2.2", "2.3", "2.4", "2.5"],
        [
            "tests/OrcaCore.DeveloperSurface.Guards/DagInternalMemberReferenceGuards.cs",
            "tests/OrcaCore.DeveloperSurface.Guards/DagAuthoringBehaviorGuards.cs",
            "tests/OrcaCore.Core.Tests/Compilation/PublicDefinitionCompilerContractTests.cs"
        ]);
    private const string RuntimeViewChange = "admit-dag-hosting-runtime-view";
    private static readonly PostGateRuntimeViewSuccessor[] ExactProposedRuntimeViewSuccessors =
    [
        new("developer-facing-surface", "Implementation package boundaries use exact internal friends",
            RuntimeViewChange, "admit-dag-authoring-friend-boundary", "MODIFIED", "MODIFIED",
            ProposedPostGateStage, "1.3",
            "bed102a2e4c98598b30cbb741c956a236f2050d27d88e6fc567b915f5b260793",
            "3a848931f75b44c6cf2edf97b564c6e213b23d90720701a31a4a45394cbf93ab"),
        new("repository-foundation", "Dependency direction remains one-way",
            RuntimeViewChange, "admit-dag-authoring-friend-boundary", "MODIFIED", "MODIFIED",
            ProposedPostGateStage, "1.3",
            "bbae0c226c6824570650d1f6f980e3b74e6c35cb196784581eef20e6e3563ce9",
            "f0dbc156d044536a83d73934dfcb6eaac5f974069b1a5c555433f2a4b56a1ab8")
    ];
    private static readonly PostGateApprovedPendingSuccessor[] ExactApprovedRuntimeViewSuccessors =
    [
        new("developer-facing-surface", "Implementation package boundaries use exact internal friends", ApprovedPendingPostGateStage,
            "bed102a2e4c98598b30cbb741c956a236f2050d27d88e6fc567b915f5b260793", "3a848931f75b44c6cf2edf97b564c6e213b23d90720701a31a4a45394cbf93ab"),
        new("repository-foundation", "Dependency direction remains one-way", ApprovedPendingPostGateStage,
            "bbae0c226c6824570650d1f6f980e3b74e6c35cb196784581eef20e6e3563ce9", "f0dbc156d044536a83d73934dfcb6eaac5f974069b1a5c555433f2a4b56a1ab8")
    ];
    private static readonly PostGateSupersededPredecessor[] ExactRuntimeViewSupersededPredecessors =
    [
        new("reshape-developer-facing-interfaces", "developer-facing-surface", "Implementation package boundaries use exact internal friends", "ADDED",
            "e12c77e5d3a8eaf30dbe31a68ffe4baec3b105023dd5f1512d3f062917a312d0", "3a848931f75b44c6cf2edf97b564c6e213b23d90720701a31a4a45394cbf93ab"),
        new("admit-dag-authoring-friend-boundary", "developer-facing-surface", "Implementation package boundaries use exact internal friends", "MODIFIED",
            "bed102a2e4c98598b30cbb741c956a236f2050d27d88e6fc567b915f5b260793", "3a848931f75b44c6cf2edf97b564c6e213b23d90720701a31a4a45394cbf93ab"),
        new("reshape-developer-facing-interfaces", "repository-foundation", "Dependency direction remains one-way", "MODIFIED",
            "d0d512ea59ea8da595770b5437d7faa7565773c6cd8845850d2dea6010b54b29", "f0dbc156d044536a83d73934dfcb6eaac5f974069b1a5c555433f2a4b56a1ab8"),
        new("admit-dag-authoring-friend-boundary", "repository-foundation", "Dependency direction remains one-way", "MODIFIED",
            "bbae0c226c6824570650d1f6f980e3b74e6c35cb196784581eef20e6e3563ce9", "f0dbc156d044536a83d73934dfcb6eaac5f974069b1a5c555433f2a4b56a1ab8")
    ];
    private static readonly PostGateApprovedPendingEvidence ExactRuntimeViewApprovalEvidence = new(
        ApprovedPendingPostGateStage,
        "43d869fc29e7daa3ec567d4602960f458eb98492",
        "1ea7f44b5a32d058e04b913f387317c21747add5",
        "docs/review/developer-facing-interface-section-08-task-8-3-runtime-view-contract-rv-remediation-independent-review-verdict-2026-10-02.md",
        "5ff6b3322d3071149fefe583fd74fba35afa5d28ea6a81ca50637bcdb029b457",
        "1.2", "1.3", "1.4", "2.1");
    private const string RuntimeViewAtomicTransitionArtifact =
        "openspec/changes/admit-dag-hosting-runtime-view/artifacts/task-1-3-1-4-atomic-runtime-view-transition-2026-10-03.md";
    private const string RuntimeViewAtomicTransitionSha256 = "a2a261de206ec8ca6e54509d467431cb20b688d2bb2540520b42fc7a40ae1b9d";
    private static readonly PostGateHistoricalArtifact[] ExactHistoricalRuntimeViewContracts =
    [
        new(RuntimeViewChange, "artifacts/task-1-1-runtime-view-contract-2026-10-02.md", "5527189563e0f39eccbb9e56bc902d2e1e4cc9e2695e0db929cc5e4b03a7dc12"),
        new(RuntimeViewChange, "artifacts/task-1-1-runtime-view-contract-rv-remediation-2026-10-02.md", "2dec007df0e06740373fdc4c4d0cd69066d0d08cda417f141a3897a4b3824398")
    ];
    private const string RuntimeViewContractArtifact =
        "openspec/changes/admit-dag-hosting-runtime-view/artifacts/task-1-1-runtime-view-contract-rv-remediation-2026-10-02.md";
    private const string RuntimeViewContractArtifactSha256 =
        "2dec007df0e06740373fdc4c4d0cd69066d0d08cda417f141a3897a4b3824398";
    private const string RuntimeViewSignatureContractSha256 =
        "93e6490d8c132784600c3c9a59f8d27be2e22662236f6f5bf0ea12d0dd21dda9";
    private const string RuntimeViewNumberedProposalSha256 =
        "4f77f455d64b761d8cca9f62e0fad82559eb314bbeaf64743ae3f74cc7a13775";
    private const string RuntimeViewNumberedBoundarySha256 =
        "4e82831901243a978767412a71a3255a514e695c04ac6832495dc141d45255ae";
    private const string RuntimeViewCompositionProposalSha256 =
        "76fbb969379eedf4ce915f9c058dbab955c70222054ad63c10fc79d3ce086071";
    private const string RuntimeViewReshapeHandoffSha256 =
        "ec0492e7f4ce742122932c3a870cd41f03e58bd7fb9bf280aec740a6b3b33814";
    private const string CurrentOpenSpecProvenanceSha256 =
        "612d4b9e3ac08278c2e700139cad9780bb6d9d6230a240b9e57ba7627732969b";
    private const string HistoricalCanonicalSourceCommit =
        "ba2478e995023b0712c44705174c2b0e3262f213";
    private const string HistoricalCanonicalRemovalCatalogSha256 =
        "81c06519ae95846b697df5e895e6bcbc9792c3e36afbe441529b3add15008ebd";
    private const int HistoricalCanonicalRemovalCatalogCount = 11;
    private const string ReviewManifestProvenanceFixture =
        "tests/OrcaCore.DeveloperSurface.Guards/Fixtures/review-manifest-provenance.json";
    private const string ReviewManifestCurrentMatchRefreshScript =
        "tests/OrcaCore.DeveloperSurface.Guards/refresh-review-manifest-current-matches.ps1";
    private const string Task81AuditArtifact =
        "openspec/changes/harmonize-downstream-capability-specs/artifacts/" +
        "task-8-1-final-harmonization-audit-2026-09-23.md";
    private const string Task81AuditSha256 =
        "0618ab997def24d79d58648f2adf29d65d619063cabfd4f279086d76a54a3a52";
    private const string FinalHarmonizationLedgerSectionSha256 =
        "03a52a610ab207696ca51458f37ac3b6568a1797b8562dfb9702b653a32d6ff1";
    private const string Task80RequirementGateArtifact =
        "openspec/changes/reshape-developer-facing-interfaces/artifacts/" +
        "task-8-0-section-8-requirement-gate-2026-09-26.md";
    private const string Task80RequirementGateSha256 =
        "deef826ad585cf976dc8dd666059e431da1b52bfe14eefd4314a8cf0e2fb391c";
    private const string MissingApprovalReviewState = "MissingApproval";
    private const string RejectedReviewState = "Rejected";
    private const string ApprovalAwaitingEvidenceCommitReviewState =
        "ApprovalAwaitingEvidenceCommit";
    private const string ApprovedReviewState = "Approved";
    private const string OwnerAuthorizationAwaitingEvidenceCommitReviewState =
        "OwnerAuthorizationAwaitingEvidenceCommit";
    private const string OwnerAuthorizedReviewState = "OwnerAuthorized";
    private const string IndependentReviewAuthority = "IndependentReview";
    private const string OwnerAuthorizationAuthority = "OwnerAuthorization";
    private const string RetroactiveOwnerAuthorizationTask = "5.3";
    private const string ApprovalHistoryDecision =
        "When a later remediation round is also approved, every immutable approval remains registered and the " +
        "newest approval verdict governs the transition.";
    private const string SyntheticGitObjectId = "0123456789012345678901234567890123456789";
    private const string RetiredMaxActiveFibersName = "MaxActiveFibers";
    private const string DeliberatelyExcludedClaimsHeading = "## Deliberately excluded claims";
    private const string DeliberatelyExcludedClaimsSha256 =
        "a989ad5ea0773cdd22eb13b65194651b61035eef9da469369134921b730c56b1";
    private const string SemanticAppendixSourceSha256 =
        "131d22bea736b6c7c4ac8a310ef1db72c992dcc867776b664c01fe2988d57be6";
    private const string PublicAuthoringCompanionSha256 =
        "41f6472c2774363d2ab922c608922e787ec241333e1d1c0b76b0c6d529ab8ec3";
    private const int SupersededOpenSpecProvenanceArtifactCatalogCount = 7;
    private const string SupersededOpenSpecProvenanceArtifactCatalogSha256 =
        "a4c8f08e730b3f0b9482dbf528af02dd33914f8986cdd509963899b169564739";
    private static readonly (string Path, string NormalizedSha256)[]
        SupersededOpenSpecProvenanceArtifacts =
        [
            (
                "openspec/changes/harmonize-downstream-capability-specs/artifacts/" +
                "task-4-2-openspec-provenance-record-2026-08-18.md",
                "9e8709096f8f3efcb8ea1ee040d1ec6f13e997a320eb6fbb7f724ec708ae2958"),
            (
                "openspec/changes/harmonize-downstream-capability-specs/artifacts/" +
                "task-6-6-openspec-provenance-refresh-2026-09-13.md",
                "01bbb6f1ed8e78b680e5bebc2157347ff80eed880e847a0375e1e9e9e2f65834"),
            (
                "openspec/changes/admit-dag-authoring-friend-boundary/artifacts/" +
                "process-openspec-provenance-2026-09-27.md",
                "85cf55f19ce0b504e03d7f4deb038e993483b7d3ccfe547172d2e17cc673b7cf"),
            (
                "openspec/changes/admit-dag-authoring-friend-boundary/artifacts/" +
                "approved-pending-openspec-provenance-2026-09-27.md",
                "f174df2b0a8a352a0601eea879b7f3260fa49ab064451ad8da50c50550c9ff82"),
            (
                "openspec/changes/admit-dag-hosting-runtime-view/artifacts/process-openspec-provenance-2026-09-30.md",
                "91c0cba8e867c03f41d8ec26c0aec7a194fdf36e5302b37d9541492ad58ae0c1"),
            (
                "openspec/changes/admit-dag-hosting-runtime-view/artifacts/contract-openspec-provenance-2026-10-02.md",
                "524053f5cf11e078aa2d6221c8105c51e040b9ba7cbcaedd85f3a9d90580e23e"),
            (
                "openspec/changes/admit-dag-hosting-runtime-view/artifacts/contract-rv-remediation-openspec-provenance-2026-10-02.md",
                "515a968b5027f8120b6a6ff4e87525542329fed42b6d5e6fa0eea8ad1f055125")
        ];
    private const string EmptyCorePublicApiBaseline =
        "# orcacore-public-api-v1\nassembly OrcaCore.Core\n";
    private static readonly string[] AuthoringLifecycleImplementationTypeNames =
    [
        "AuthoringSessionState",
        "AuthoringLifecycleSession",
        "AuthoringLifecycleHandle",
        "AuthoringLexicalToken",
        "AuthoringJoinToken",
        "WorkflowAuthoringSession"
    ];
    private static readonly string[] ActiveDocumentationExtensions = [".cs", ".md"];
    private static readonly string[] ImmutableDocumentationPrefixes = ["docs/archive/", "docs/review/"];
    private const string ImmutableDocumentHistoryFixturePath =
        "tests/OrcaCore.DeveloperSurface.Guards/Fixtures/immutable-document-history.json";
    private const string ImmutableDocumentHistoryRecordFormat =
        "ordinal path<TAB>LF-normalized byte length<TAB>lowercase SHA-256, LF-joined with one final LF";
    private const string ImmutableDocumentHistoryNormalization =
        "replace CRLF and lone CR bytes with LF before byte length and SHA-256";
    private const string ImmutableDocumentHistoryBaselineCommit =
        "261d578b11a4b2d09e033aa44c8123cbc4ec653e";
    private const int ImmutableDocumentHistoryBaselineCount = 427;
    private const int ImmutableDocumentHistoryBaselineBytes = 66_459;
    private const string ImmutableDocumentHistoryBaselineSha256 =
        "5be2e4560b568c6022015624420ae0a2b11966676ff499e4bff0095e5526e180";
    private static readonly (string Path, string Purpose)[] MutableHistoricalDocumentPaths =
    [
        (ImmutableDocumentationPrefixes[0] + "README.md", "active archive index"),
        (
            "docs/review/developer-facing-interface-phase-review-template.md",
            "active review template")
    ];
    private const string Task72CompletionDecision =
        "      **Completed:** `immutable-document-history.json` classifies every file under both historical\n" +
        "      roots as either part of the LF-normalized, committed-blob baseline, a universal append-only\n" +
        "      post-baseline record, or one of exactly two mutable surfaces: the active archive index and\n" +
        "      reusable review template. A guard-source count and digest pin only the fixed Task 7.2 baseline.\n" +
        "      Every later archive or review record belongs to one permanent append-only set. While uncommitted,\n" +
        "      the catalog's active freeze manifest must name the record; after checkpoint, every Git\n" +
        "      addition of that path must reproduce the catalogued normalized bytes. Re-adding identical\n" +
        "      content is allowed; any differing addition fails. The infrastructure guard derives every\n" +
        "      post-baseline addition from Git history and rejects missing, moved, modified, unclassified,\n" +
        "      or broadened records. Deletion or relocation first requires a separately reviewed\n" +
        "      tombstone mechanism, which the current contract does not provide.\n" +
        "      **Validation remediation:** the leased-retry fixtures now race `SecondStarted` against the\n" +
        "      real instance terminal status, not the inline `StartOrGetAsync` operation that can complete\n" +
        "      before scheduler notification; the source guard pins both call sites and snapshot boundary.\n" +
        "      The first body remains deliberately cancellation-ignoring so late-return fencing stays covered;\n" +
        "      terminal observation uses a delayed, bounded loop instead of tight or unbounded polling.\n" +
        "      The same remediation makes durable metric-catalog capture thread-safe under concurrent\n" +
        "      `MeterListener` publication and source-pins the `ConcurrentDictionary` boundary.\n" +
        "      **Review remediation:** Round 60 findings PP-1 and RR-1 are closed by the committed, normalized\n" +
        "      baseline and bounded terminal observation; all three Task 7.2 `REJECT` verdicts remain immutable\n" +
        "      and registered. SS-1 and TT-1 are closed by the family-independent append-only rule for both\n" +
        "      historical roots; UU-1 and VV-1 are closed by line-ending-stable source/manifest checks and\n" +
        "      exact deadline and baseline-count pins. **Second review remediation:** WW-1 is closed by\n" +
        "      reconciling every post-baseline Git addition back to the permanent catalog and current path;\n" +
        "      deletion or relocation now fails before and after commit. XX-1 is closed by stating precisely\n" +
        "      that the active manifest names each uncommitted record. **Post-approval hardening:** YY-1 is\n" +
        "      closed by using `--full-history` for both addition-history queries, including merged side-branch\n" +
        "      additions; ZZ-1 is closed by making Task 7.7 require immutable predecessor evidence and a reviewed\n" +
        "      tombstone mechanism before any relocation. **Second post-approval hardening:** AAA-1 is closed by\n" +
        "      accepting repeated additions only when every addition commit reproduces the catalogued normalized bytes.\n" +
        "      **Review carry-forward:** Task 7.1 finding OO-1 is closed by naming the two exact harmonization\n" +
        "      planning paths whose pre-finalization TSV rows intentionally predate their final text.";
    private const string Task72DesignDecision =
        "Task 7.2 makes immutable-history classification executable. A machine-readable catalog preserves an\n" +
        "LF-normalized baseline derived from the exact committed blobs at the Task 7.2 base. Guard source owns\n" +
        "that baseline's count and digest, so checkout line-ending transforms cannot redefine it. Every later\n" +
        "record under either historical root belongs to one family-independent permanent append-only set. While\n" +
        "uncommitted, the catalog's active freeze manifest must name the record. After checkpoint, every Git\n" +
        "addition of that path must reproduce the catalogued normalized bytes. Re-adding identical content is\n" +
        "allowed; any differing addition fails. The guard also derives every post-baseline addition from Git history\n" +
        "and requires that path to remain present and cataloged; deletion or relocation requires a separately reviewed\n" +
        "tombstone mechanism, which the current contract does not provide. Both Git addition-history queries use\n" +
        "`--full-history`, so a record added and later deleted on a merged side branch remains visible. The exact\n" +
        "active archive index and reusable review template remain the only mutable surfaces.";
    private const string Task72ValidationRemediationDecision =
        "Validation also replaces the leased-retry fixtures' second-attempt race against the inline start\n" +
        "operation with observation of the real workflow instance terminal state; the start operation can\n" +
        "complete before the scheduler publishes `SecondStarted`, while the persisted instance is the\n" +
        "authoritative completion boundary.\n" +
        "The first protected body remains cancellation-ignoring after physical release, retaining coverage of\n" +
        "late-return fencing without changing the runtime's select-once deadline arbitration. Terminal-status\n" +
        "observation is delayed and bounded so the test neither spins nor hangs when neither side progresses.\n" +
        "The same validation pass makes the durable metric-catalog listener use thread-safe collection\n" +
        "semantics because `MeterListener` may publish instruments concurrently.";
    private const string Task72ArchiveIndexDecision =
        "5. **Correct historical conclusions with a new dated superseding record.** Existing files under\n" +
        "   both historical-document roots are immutable; only this active archive index and the reusable\n" +
        "   review template are mutable. Add every new review or archive record to `appendOnlyRecords`. During\n" +
        "   its reviewed freeze, set `activeFreezeManifestPath` to the manifest that names every uncommitted\n" +
        "   record. The guard binds each record to that active freeze and, after checkpoint, requires every Git\n" +
        "   addition of that path to reproduce the catalogued normalized bytes. Re-adding identical content is\n" +
        "   allowed; any differing addition fails. Every committed post-baseline path must remain present\n" +
        "   and cataloged; deletion or relocation first requires an explicit reviewed tombstone mechanism. Update this index to route\n" +
        "   readers to the superseding record instead of rewriting the historical file.";
    private const string Task77ArchiveProvenanceDecision =
        "- [x] 7.7 Repair the Phase-0 kickoff prompt archive move by adding an explicit immutable archive\n" +
        "      provenance record that names the exact predecessor and commit; do not rename, delete, or edit\n" +
        "      an existing protected path. Any future history-preserving relocation first requires a separately\n" +
        "      reviewed tombstone mechanism, which the current Task 7.2 contract does not provide.";
    private const string Task77ArchiveRecord =
        "docs/archive/plans/developer-facing-interface-phase-00-kickoff-archive-provenance-2026-09-23.md";
    private const string Task77ArchiveRecordSha256 =
        "20be3dcd960c50feb7226e105366807ff2ad0058cbee10b5c02dd45b81eae983";
    private const string Task77Predecessor =
        "docs/implementation/developer-facing-interface-phase-00-kickoff-prompt-2026-07-15.md";
    private const string Task77ArchivedPrompt =
        "docs/archive/plans/developer-facing-interface-phase-00-kickoff-prompt-2026-07-15.md";
    private const string Task77PredecessorCommit =
        "ac46d99543daf85c0fa3234272997ba40f47f96b";
    private const string Task77MoveCommit =
        "ad9414088f1843dae09ef8a5d10caa8aca413561";
    private const string Task73DocumentationArtifact =
        "openspec/changes/harmonize-downstream-capability-specs/artifacts/" +
        "task-7-3-active-documentation-reconciliation-2026-09-18.md";
    private const string Task73DocumentationArtifactSha256 =
        "caa9681fa204b9bd231dd29d2139cdcf3304c2032618488289d945d65fda79b6";
    private const string Task73PinRefreshDecision =
        "Only the owner of a reviewed change that intentionally edits one of these 22 sources may refresh its " +
        "recorded hash. Task 7.4 or Section 8 may refresh a row only in the same frozen target that intentionally " +
        "edits the source and updates the artifact row, guard-source artifact digest, and review evidence; neither " +
        "may perform a mechanical follow-up refresh for an earlier unreviewed edit.";
    private const string Task73SourceSweepArtifact =
        "openspec/changes/harmonize-downstream-capability-specs/artifacts/" +
        "task-3-1-active-corpus-vocabulary-sweep-2026-08-18.md";
    private const string Task73CompletionDecision =
        "**Completed:** reconciled exactly 22 Task 3.1 sources to the approved Section 7B contract; " +
        "Task 7.4 retains the separate Orleans note and Task 7.5 retains recurring active-tree enforcement. " +
        "**Review remediation:** corrected caller-created inbound identity and application-registered dispatcher ownership; " +
        "restored unrelated timeout, collation, statistics, dynamic-wait, and dispatch-hook obligations; moved " +
        "AC-108/116/118/119/120 evidence to the real provider/product/hosting/engine tests; and pinned the " +
        "LF-normalized SHA-256 of every reconciled source through the guard-source-owned artifact digest.";
    private const string Task73VocabularyRemediationDecision =
        "**Task 7.5 review remediation:** DU-055 and AC-005 now use the exact current event-acceptance " +
        "and direct-terminal-rejection vocabulary; only rows 16 and 19 plus the guard-source artifact " +
        "digest were refreshed.";
    private static readonly string[] Task73ActiveDocumentationPaths =
    [
        "CLAUDE.md",
        "docs/eks-scheduler-handoff.md",
        "docs/end-to-end-plan.md",
        "docs/ephemeral-engine-developer-guide.md",
        "docs/ephemeral-engine-diagrams.md",
        "docs/implementation/00-stack-decisions.md",
        "docs/implementation/01-solution-architecture.md",
        "docs/implementation/02-engineering-conventions.md",
        "docs/implementation/developer-facing-interface-refactor-phased-plan-2026-07-14.md",
        "docs/normative-source-map.md",
        "docs/production-readiness.md",
        "docs/project-technical-overview.md",
        "docs/specs/01-concept-and-goals.md",
        "docs/specs/03-domain-model-and-glossary.md",
        "docs/specs/05-requirements-events-waits-timers.md",
        "docs/specs/06-requirements-durable-execution.md",
        "docs/specs/09-requirements-management-operations.md",
        "docs/specs/10-provider-model-and-extensibility.md",
        "docs/specs/12-acceptance-criteria.md",
        "docs/specs/13-phasing-and-open-questions.md",
        "docs/specs/14-driving-scenario-eks-job-scheduler.md",
        "docs/specs/16-requirements-durable-driver.md"
    ];
    private const string Task74ActiveBoundaryPath = "docs/orleans-engine/README.md";
    private const string Task74ActiveBoundarySha256 =
        "e4bf37e2b53b6c66d29a1028276a73237b16f0f665c353c21fb877a808270d8e";
    private const string Task74ArchiveRelativeRoot = "plans/orleans-engine-pre-v1/";
    private const int Task74ArchiveFileCount = 25;
    private const int Task74ArchiveRecordBytes = 2_497;
    private const string Task74ArchiveRecordSha256 =
        "c8a39515f7c8a8af49674e3d03f6733804b1455d2dc7f7dddee950b9b4a18da2";
    private const string Task74DocumentationArtifact =
        "openspec/changes/harmonize-downstream-capability-specs/artifacts/" +
        "task-7-4-orleans-boundary-reconciliation-2026-09-21.md";
    private const string Task74DocumentationArtifactSha256 =
        "a71dd9498c6f6763bc9e756e2f620edfecec18ea3f4015458de74f72669281fa";
    private const string Task74ReviewRemediationDecision =
        "**Review remediation:** the Task 7.4 artifact is whitespace-clean; the guard semantically " +
        "pins the numbered new-change prerequisite and Orleans-only adapter boundary, derives the " +
        "archived inventory from disk as well as the immutable fixture, and rejects any additional " +
        "active Orleans task-ledger block.";
    private const string Task74DesignReviewRemediationDecision =
        "Task 7.4 review remediation makes that boundary independent of a whole-document hash: the guard " +
        "pins the numbered new-change prerequisite and Orleans-only adapter ownership semantically, derives " +
        "the 25-file archive from disk as well as the immutable-history fixture, and permits no additional " +
        "active task-ledger block that mentions Orleans implementation work.";
    private const string Task75InitialFixtureCommit =
        "89e3ed55357e849852c1a0f6fefa2124433d7e30";
    private const string Task75InitialFixtureSha256 =
        "49a674d09d66d1bbc835e853b0d3368b135293106a239030c33b9e3ff65ccb2c";
    private const string Task75DocumentationArtifact =
        "openspec/changes/harmonize-downstream-capability-specs/artifacts/" +
        "task-7-5-active-tree-documentation-guard-2026-09-21.md";
    private const string Task75DocumentationArtifactSha256 =
        "c65a670e52804739ea5b2413b2883be38d457fdafd4c63298cd2304f1c0c17d8";
    private const string Task75ClassifierCatalogSha256 =
        "fa5cb2d5828f453a54311be5c8b77934bc7ca5fbf68c06de6571e334fea55c03";
    private const string Task75NaturalLanguageRegressionCatalogSha256 =
        "df9180ed2858bbe28e3d3e71326efd7ecfb86fb6b9157558922fe1b0c2218549";
    private const string Task75PostReviewHardeningArtifact =
        "openspec/changes/harmonize-downstream-capability-specs/artifacts/" +
        "task-7-5-post-review-hardening-2026-09-22.md";
    private const string Task75PostReviewHardeningArtifactSha256 =
        "1d07e76675cc6fea5e95c4d27e1f40c326fe8216c849f3231b8cee45ede5177f";
    private const string Task7475ReviewRemediationArtifact =
        "openspec/changes/harmonize-downstream-capability-specs/artifacts/" +
        "task-7-4-and-7-5-review-remediation-2026-09-22.md";
    private const string Task7475ReviewRemediationArtifactSha256 =
        "eb33c6dcc17259cee2957938d30bc77fd844af2981f4a23b96660cec56a59ffa";
    private static readonly string Task75CompletionDecision =
        "**Completed:** the recurring guard enumerates the evolving active contract corpus while excluding " +
        $"`{ImmutableDocumentationPrefixes[0]}` and `{ImmutableDocumentationPrefixes[1]}`, reuses the Task 3.1 artifact as the exact 23-source initial " +
        "stale-negative fixture, proves every initial source trips the classifier at the pre-reconciliation " +
        "commit, and requires zero positive removed/deferred calls or stale Section 7B claims now.";
    private static readonly string Task75DesignDecision =
        "Task 7.5 turns the Task 3.1 classifications into a recurring active-tree gate. The gate reuses the\n" +
        $"same evolving corpus and positive-call expressions as Task 7.1, excludes immutable `{ImmutableDocumentationPrefixes[0]}`\n" +
        $"and `{ImmutableDocumentationPrefixes[1]}` evidence, and pins the complete 23-source stale-negative fixture plus its historical\n" +
        "pre-reconciliation commit. The replay must equal the reviewed 56-result path/line/classifier record\n" +
        "and exercise every classifier, while the current active tree must produce no finding. Explicitly\n" +
        "superseded task/proposal sentences remain searchable history rather than being mistaken for current\n" +
        "guidance.";
    private const string Task75ReviewRemediationDecision =
        "**Review remediation:** the historical replay now pins all 56 exact path/line/classifier " +
        "findings, every classifier must appear, legacy event-client/method/result/status and natural " +
        "routing, publish, and pre-wait rewordings are covered, and the artifact's 86-source figure is " +
        "snapshot evidence rather than a live cardinality invariant.";
    private const string Task75DesignReviewRemediationDecision =
        "Task 7.5 review remediation broadens legacy event-surface, terminal-result, routing, publish, and " +
        "pre-wait phrase coverage. The artifact's 86-source value remains evidence of the reviewed snapshot, " +
        "not a live equality: benign additions and normal archival are accepted when the re-enumerated corpus " +
        "contains no positive removed/deferred call or stale Section 7B claim.";
    private const string Task75PostReviewHardeningDecision =
        "**Post-review hardening:** all eight OOO-1 natural-language regressions are executable probes, " +
        "and guard-source SHA-256 values pin both the complete classifier name/expression catalog and the " +
        "exact phrase/expected-classifier catalog so removing a tuple or probe cannot pass as a routine " +
        "refresh.";
    private const string Task75DesignPostReviewHardeningDecision =
        "Task 7.5 post-review hardening adds the eight OOO-1 natural-language forms as synthetic regressions\n" +
        "and pins both the ordered classifier name/expression catalog and the exact phrase/expected-classifier\n" +
        "catalog in guard source. Removing a classifier or synthetic regression and coherently refreshing its\n" +
        "historical rows and mutable artifact digest therefore remains red unless the same reviewed edit\n" +
        "explicitly changes the corresponding guard-owned digest.";
    private static readonly string[] Task75InitialStaleNegativePaths =
    [
        "CLAUDE.md",
        "docs/end-to-end-plan.md",
        "docs/eks-scheduler-handoff.md",
        "docs/ephemeral-engine-developer-guide.md",
        "docs/ephemeral-engine-diagrams.md",
        "docs/implementation/00-stack-decisions.md",
        "docs/implementation/01-solution-architecture.md",
        "docs/implementation/02-engineering-conventions.md",
        "docs/implementation/developer-facing-interface-refactor-phased-plan-2026-07-14.md",
        "docs/normative-source-map.md",
        "docs/orleans-engine/README.md",
        "docs/production-readiness.md",
        "docs/project-technical-overview.md",
        "docs/specs/01-concept-and-goals.md",
        "docs/specs/03-domain-model-and-glossary.md",
        "docs/specs/05-requirements-events-waits-timers.md",
        "docs/specs/06-requirements-durable-execution.md",
        "docs/specs/09-requirements-management-operations.md",
        "docs/specs/10-provider-model-and-extensibility.md",
        "docs/specs/12-acceptance-criteria.md",
        "docs/specs/13-phasing-and-open-questions.md",
        "docs/specs/14-driving-scenario-eks-job-scheduler.md",
        "docs/specs/16-requirements-durable-driver.md"
    ];
    private static readonly (string Name, string Expression)[] StaleSection7BClaimPatterns =
    [
        (
            "legacy event client",
            @"(?i)IWorkflowEventClient|DeliverToInstanceAsync"),
        (
            "non-buffering pre-wait delivery",
            @"(?i)(?:non-consuming|non-buffering)[^\n]{0,80}`?NoActiveWait`?|" +
            @"(?<!never\s)(?:returns?|yields?)\b[^\n]{0,120}`?NoActiveWait`?|" +
            @"`?NoActiveWait`?[^\n]{0,160}(?:writes?\s+no|does\s+not\s+consume|no\s+(?:pending|mailbox|inbox))|" +
            @"v1[ \t]+(?:does[ \t]+not|doesn't)[ \t]+buffer[ \t]+events|no[ \t]+pre-wait[ \t]+mailbox|" +
            @"(?:events?[^\n]{0,80}(?:arrive|arriving)[^\n]{0,80}before[^\n]{0,40}(?:a[ \t]+)?wait[^\n]{0,80}" +
            @"(?:drop|discard|redeliver)|does[ \t]+not[ \t]+buffer[ \t]+events?[ \t]+before[ \t]+a[ \t]+wait[ \t]+exists)"),
        (
            "deferred definition fanout",
            @"(?i)(?:definition(?:-targeted)?(?:[ \t]+event)?[ \t]+fanout|" +
            @"fanout[^.\n]{0,80}(?:instances?[^.\n]{0,40})?definition)[^.\n]{0,100}" +
            @"(?:(?:is|remains)[ \t]+(?:deferred|absent)|not[ \t]+(?:a[ \t]+)?" +
            @"(?:v1|delivery[ \t]+route|supported|available))"),
        (
            "superseded deferred list",
            @"(?i)(?:workflow-authored\s+`Publish`/`Cancel`|authored\s+`Publish`/`Cancel`|" +
            @"workflow-authored\s+`Publish`\s+or\s+self-`Cancel`)[\s\S]{0,120}" +
            @"definition-targeted\s+event\s+fanout"),
        (
            "deferred durable publish",
            @"(?i)does[ \t]+not[ \t]+approve[ \t]+workflow-authored[ \t]+`?Publish`?|" +
            @"(?:(?:workflow-authored|durable|authored)[ \t]+)?`?Publish`?[ \t]+(?:is|remains|stays)[ \t]+" +
            @"(?:deferred|absent|not[ \t]+(?:a[ \t]+)?(?:v1|supported|available))|" +
            @"publishing[ \t]+events?[ \t]+from[ \t]+a[ \t]+workflow[ \t]+is[ \t]+not[ \t]+" +
            @"(?:supported|available)(?:[ \t]+in[ \t]+v1)?"),
        (
            "two-route ingress",
            @"(?i)(?:exactly[ \t]+)?two(?:[ \t]+first-release)?(?:[ \t]+event|[ \t]+delivery)?" +
            @"[ \t]+routes?|two-route(?:/redelivery)?[ \t]+contract|" +
            @"(?:only|limited[ \t]+to)[^\n]{0,60}(?:instance|direct)[^\n]{0,60}" +
            @"(?:and|or)[^\n]{0,40}correlation[^\n]{0,40}rout(?:e|es|ing)|" +
            @"supports?[^\n]{0,60}instance[^\n]{0,60}(?:and|or)[^\n]{0,40}correlation[^\n]{0,40}" +
            @"(?:delivery|routing)[ \t]+only"),
        (
            "superseded delivery status",
            @"(?i)EventDeliveryStatus|EventDeliveryResult|" +
            @"event[ \t]+delivery[ \t]+(?:returns?|yields?)[^\n]{0,80}`?InstanceTerminal`?"),
        (
            "unapproved Section 7B",
            @"(?i)(?:pending[ \t]+Section[ \t]+7B|Section[ \t]+7B[ \t]+(?:is|remains)[ \t]+" +
            @"(?:a[ \t]+)?(?:proposal|pending|unapproved)|Section[ \t]+7B[ \t]+(?:is[ \t]+)?" +
            @"(?:not[ \t]+yet|never)[ \t]+approved|Section[ \t]+7B[ \t]+has[ \t]+not[ \t]+been[ \t]+" +
            @"approved[ \t]+yet)")
    ];
    private static readonly string[] Task75HistoricalContextExpressions =
    [
        @"(?i)\*\*BREAKING\*\*[ \t]+Replace[ \t]+`IWorkflowEventClient`",
        @"(?i)Replace[ \t]+`IWorkflowEventClient`[ \t]+with",
        @"(?i)Historical guard target superseded",
        @"(?i)Historical behavior target superseded",
        @"(?i)Completed against the superseded pre-7B contract"
    ];

    private const string HarmonizationTaskLedgerPath =
        "openspec/changes/harmonize-downstream-capability-specs/tasks.md";
    private const string HarmonizationDesignPath =
        "openspec/changes/harmonize-downstream-capability-specs/design.md";
    private const string Task65PostReviewHardeningDecision =
        "**Post-review hardening:** the companion's reviewed SHA-256 is now a guard-source constant,\n" +
        "      and the mutable public-contract fixture must equal that constant before the companion bytes are\n" +
        "      checked, closing review finding Y-1's coherent companion-plus-fixture re-pin path.";
    private const string Task65SecondReviewHardeningDecision =
        "**Second-review hardening:** the Task 6.5 corpus guard binds both this ledger decision and\n" +
        "      the design's guard-source ownership paragraph, so review finding Z-1 cannot recur after archival.";
    private const string Task65CompleteDesignDecision =
        "Task 6.5 resolves that decision by keeping the companion byte-unchanged. `AuthoringSessionState`,\n" +
        "the lifecycle session, lifecycle/join handles, lexical token, and shared workflow-authoring session\n" +
        "remain internal implementation types in `OrcaCore.Core`; that assembly's exact v1 API baseline has\n" +
        "no exported declarations. The existing twelve-assembly public API baseline independently rejects\n" +
        "any future visibility leak, while the companion continues to describe only application-authored\n" +
        "types and signatures. The companion's reviewed SHA-256 is owned by guard source; the mutable public-\n" +
        "contract fixture must reproduce that pin and cannot authorize coherent documentation drift by\n" +
        "re-pinning itself.";
    private const string FutureCapabilityRegistryDocumentPath =
        "docs/specs/13-phasing-and-open-questions.md";
    private const string FutureCapabilityRegistrySourceMapTarget =
        "specs/13-phasing-and-open-questions.md";
    private const string FutureCapabilityRegistryHeading =
        "## 13.4 Future-capability registry";
    private const string FutureCapabilityRegistrySectionIdentity =
        "§13.4 \"Future-capability registry\"";
    private const string DeferredCapabilitiesHeading = "### Deferred capabilities";
    private const string RemovedConceptsHeading = "### Removed concepts";
    private const string FutureCapabilityRegistryTableHeading =
        "| Capability | Future amendment must close |";
    private const string FutureCapabilityRegistryCrossReference =
        "future-capability registry at `docs/specs/13-phasing-and-open-questions.md` " +
        "§13.4 (\"Future-capability registry\")";
    private const string LegacyFutureCapabilityRegistryHeading =
        "Explicitly deferred or removed capabilities";
    private const string LegacyFutureCapabilityRegistryAnchor =
        "#134-explicitly-deferred-or-removed-capabilities";
    private const string FutureCapabilityRegistryAnchor =
        "#134-future-capability-registry";
    private const string LegacyFutureCapabilityRegistryMismatchClaim =
        "nothing links the two terms";
    private const string DeferredCapabilitiesRequirement =
        "Deferred capabilities are documented without public placeholders";
    private const string SagaDeferredCapabilityRequirement =
        "Saga remains an explicit deferred capability";
    private const string Task66CompletionDecision =
        "**Completed:**\n" +
        "      `docs/specs/13-phasing-and-open-questions.md` §13.4 now uses the exact \"Future-capability\n" +
        "      registry\" name shared by canonical OpenSpec and its active owning deltas; the registry has a\n" +
        "      deferred-capability table and a separate removed-concepts subsection, and active guide links\n" +
        "      use the exact section anchor. Reshape task 9.6 retains ownership of final registry membership.";
    private const string Task66ReviewCarryForwardDecision =
        "**Review carry-forward:** Task 6.5 finding AA-1 is closed by pinning the complete design\n" +
        "      decision rather than only its final guard-source-ownership sentence.";
    private const string Task66PostReviewRemediationDecision =
        "**Post-review\n" +
        "      remediation:** a permanent guard-source catalog retains every superseded OpenSpec provenance\n" +
        "      artifact and its normalized hash; identifier-boundary checks keep `WaitLong` and `Yield` out\n" +
        "      of the deferred table regardless of Markdown spelling; and the dated Task 6.6 remediation\n" +
        "      record preserves the exact citation-only canonical/delta sync plus its actual approval order.";
    private const string Task66DesignDecision =
        "Task 6.6 gives that registry one exact cross-tree identity: `docs/specs/13-phasing-and-open-questions.md`\n" +
        "§13.4, \"Future-capability registry\". Its deferred-capability table records future promises and their\n" +
        "re-entry gates, while its separate removed-concepts subsection keeps retired names searchable\n" +
        "without treating them as future work. Canonical OpenSpec and the active deltas that still own those\n" +
        "requirements cite the same path and section name. Harmonization owns this name and cross-reference;\n" +
        "reshape task 9.6 remains the owner of final registry membership.";
    private const string Task66PostReviewDesignDecision =
        "Task 6.6 post-review remediation makes provenance refreshes append-only in effect: guard source\n" +
        "permanently catalogs every superseded provenance artifact path and normalized hash before the\n" +
        "mutable fixture points at its successor. Removed-concept classification uses identifier boundaries\n" +
        "rather than Markdown spelling. The dated remediation record also discloses that Task 6.6 authority\n" +
        "predated the canonical gate, while its citation-only canonical/delta sync was prepared before the\n" +
        "exact 22-path target received independent approval; it preserves the exact sync diff and subsequent\n" +
        "checkpoint, approval-evidence, and activation sequence without recasting it as approval-first work.";
    private const string Task66PostReviewRemediationArtifact =
        "openspec/changes/harmonize-downstream-capability-specs/artifacts/" +
        "task-6-6-post-review-remediation-2026-09-13.md";
    private const string Task66PostReviewRemediationArtifactSha256 =
        "913c6d286f89b3b3b7f52cae6776764392be7bd722d80a0eb15e77869a1e0b65";
    private const string Task66SecondPostReviewHardeningDecision =
        "**Second post-review hardening:** a separately pinned dated addendum preserves the literal\n" +
        "      backticks in both exact synchronization fragments; exhaustive provenance-artifact discovery\n" +
        "      requires every refresh record to be current or permanently catalogued; and removed-token\n" +
        "      checks cover the complete §13.4 future-work region before the removed-concepts subsection.";
    private const string Task66SecondPostReviewDesignDecision =
        "Task 6.6 second post-review hardening makes those controls exhaustive and byte-explicit. A dated\n" +
        "addendum preserves the literal backticks in the two citation-only synchronization fragments without\n" +
        "rewriting the approved remediation record. The provenance guard enumerates every top-level\n" +
        "`*openspec-provenance-*.md` artifact and requires each to be either the fixture's current record or a\n" +
        "permanently catalogued predecessor. Removed-concept tokens are rejected across the complete §13.4\n" +
        "future-work region before the removed-concepts subsection, including its preamble.";
    private const string Task66SecondPostReviewHardeningArtifact =
        "openspec/changes/harmonize-downstream-capability-specs/artifacts/" +
        "task-6-6-second-post-review-hardening-2026-09-14.md";
    private const string Task66SecondPostReviewHardeningArtifactSha256 =
        "caa1151583da9503a70993b20a769342040bcc162c1788719dbb44488e8d3096";
    private const string DocumentationRoot = "docs";
    private const string OpenSpecChangesRoot = "openspec/changes";
    private const string OpenSpecChangesPathPrefix = "openspec/changes/";
    private const string CanonicalOpenSpecSpecsRoot = "openspec/specs";
    private const string CanonicalOpenSpecSpecsPathPrefix = "openspec/specs/";
    private const string OpenSpecSpecFileName = "spec.md";
    private const string ActiveChangeSpecsDirectoryName = "specs";
    private const string ArchivedChangesDirectoryName = "archive";
    private const string OpenSpecProvenanceArtifactNameFragment = "openspec-provenance-";
    private const string Task71PositiveCallScanArtifact =
        "openspec/changes/harmonize-downstream-capability-specs/artifacts/" +
        "task-7-1-active-guide-positive-call-scan-2026-09-14.md";
    private const string Task71PositiveCallScanArtifactSha256 =
        "55bda56ed62a6b0cf7664a26f0331d35d37b0e207d1352f27fb37808e9b954c4";
    private const string Task71PositiveCallSourceRecord =
        "openspec/changes/harmonize-downstream-capability-specs/artifacts/" +
        "task-7-1-active-guide-positive-call-source-record-2026-09-15.tsv";
    private const int Task71PositiveCallSourceRecordRows = 86;
    private const int Task71PositiveCallSourceRecordBytes = 10_655;
    private const string Task71PositiveCallSourceRecordSha256 =
        "56b6d27ece05d0ff536d9ca5114ba3e1856f0f3288f4bf5226bab341e36963f2";
    private const string Task71CompletionDecision =
        "**Completed:** the 2026-09-14 rerun enumerates 86 active contract sources and reports zero\n" +
        "      positive removed/deferred API call forms. The executable guard rescans the evolving active\n" +
        "      corpus, requires the three active guide notes to link to §13.4, and closes review findings\n" +
        "      HH-1 and II-1 through recursive provenance discovery and exact removed-subsection boundaries.";
    private const string Task71DesignDecision =
        "Task 7.1 re-runs the original positive-call classification over root guidance, active documentation,\n" +
        "canonical OpenSpec, and active change proposals, designs, ledgers, and deltas. The dated checkpoint\n" +
        "record reports zero positive call forms while preserving concise deferred-capability notes and exact\n" +
        "§13.4 re-entry links in the ephemeral, Kubernetes scheduler, and Orleans guides. The same slice closes\n" +
        "Task 6.6 findings HH-1 and II-1: provenance-artifact discovery is recursive across all of\n" +
        "`openspec/changes/**`, and removed identifiers are prohibited everywhere outside the exact removed-\n" +
        "concepts subsection rather than treating every later subsection as removed territory.";
    private const string Task71ReviewRemediationDecision =
        "      **Review remediation:** both rejected Task 7.1 freezes retain separate raw-order manifests and\n" +
        "      byte-pinned requests and verdicts. The final companion record is ordinal-path sorted, LF-only,\n" +
        "      10,655 bytes, 86 rows, and SHA-256 `56b6d27ece05d0ff536d9ca5114ba3e1856f0f3288f4bf5226bab341e36963f2`;\n" +
        "      the guard validates that order and the scan artifact's exact real companion path.";
    private const string Task71ReviewRemediationDesignDecision =
        "The Task 7.1 review remediation preserves each rejected target under its own immutable raw-order\n" +
        "manifest while byte-pinning the request and `REJECT` verdict that produced it. The final source-record\n" +
        "companion uses ordinal path comparison and the executable guard enforces its 86-row order, LF-only\n" +
        "encoding, exact digest, and the scan artifact's exact real companion path. The companion is a\n" +
        "pre-finalization scan snapshot: its\n" +
        "`openspec/changes/harmonize-downstream-capability-specs/design.md` and\n" +
        "`openspec/changes/harmonize-downstream-capability-specs/tasks.md` rows intentionally predate their\n" +
        "final self-describing remediation text and therefore do not represent the checkpoint-tree digest.";
    private static readonly string[] ActiveCorpusRootDocumentPaths = ["CLAUDE.md", "README.md"];
    private static readonly string[] ActiveChangePlanningFileNames = ["proposal.md", "design.md", "tasks.md"];
    private static readonly (string Name, string Expression)[] PositiveRemovedOrDeferredCallPatterns =
    [
        ("WaitLong", @"(?<![\p{L}\p{Nd}_])WaitLong\s*\("),
        ("Yield", @"(?<![\p{L}\p{Nd}_])Yield\s*\("),
        ("WhenFirst", @"(?<![\p{L}\p{Nd}_])WhenFirst\s*\("),
        ("Saga", @"(?<![\p{L}\p{Nd}_])Saga\s*\("),
        ("RunExternalJob", @"(?<![\p{L}\p{Nd}_])RunExternalJob\s*\("),
        ("RunChild", @"(?<![\p{L}\p{Nd}_])RunChild\s*\("),
        ("RunChildren", @"(?<![\p{L}\p{Nd}_])RunChildren\s*\("),
        ("management operation", @"\.(?:Pause|Resume|Archive|Purge|Cancel)\s*\(")
    ];
    private static readonly string[] RemovedConceptRegistryNames = ["WaitLong", "Yield"];
    private static readonly string[] FutureCapabilityRegistryGuidePaths =
    [
        "docs/eks-scheduler-handoff.md",
        "docs/ephemeral-engine-developer-guide.md",
        "docs/orleans-engine/README.md"
    ];
    private static readonly (string CanonicalPath, string DeltaPath, string Requirement)[]
        FutureCapabilityRegistryRequirementBindings =
        [
            (
                "openspec/specs/developer-facing-surface/spec.md",
                "openspec/changes/reshape-developer-facing-interfaces/specs/developer-facing-surface/spec.md",
                DeferredCapabilitiesRequirement),
            (
                "openspec/specs/saga-orchestration/spec.md",
                "openspec/changes/reshape-developer-facing-interfaces/specs/saga-orchestration/spec.md",
                SagaDeferredCapabilityRequirement)
        ];

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
        checkpoint.SchemaVersion.Should().Be(11);
        checkpoint.Entries.Select(entry => entry.Task).Should().OnlyHaveUniqueItems();
        checkpoint.Entries.Select(entry => entry.ManifestPath).Should().OnlyHaveUniqueItems();
        checkpoint.RejectedFreezes.Select(freeze => freeze.Id).Should().OnlyHaveUniqueItems();
        checkpoint.RejectedFreezes.Select(freeze => freeze.ManifestPath).Should().OnlyHaveUniqueItems();
        checkpoint.RejectedFreezes.Select(freeze => freeze.RequestPath).Should().OnlyHaveUniqueItems();
        checkpoint.RejectedFreezes.Select(freeze => freeze.VerdictPath).Should().OnlyHaveUniqueItems();
        checkpoint.ArchivedFreezes.Select(freeze => freeze.Id).Should().OnlyHaveUniqueItems();
        checkpoint.ArchivedFreezes.Select(freeze => freeze.ManifestPath).Should().OnlyHaveUniqueItems();
        File.Exists(Path.Combine(
            root,
            ReviewManifestCurrentMatchRefreshScript.Replace('/', Path.DirectorySeparatorChar)))
            .Should().BeTrue(
                "the freeze procedure must provide an executable current-match refresh before validation");

        ValidateCommitRealFreezeProjection(root);
        ValidateActiveFreezeContentRecordSemantics();
        ValidateActiveFreezeContentRecordBuilders(root);
        ValidateMissingApprovalStateSemantics();
        ValidateReviewAuthoritySemantics();

        var reviewRoot = Path.Combine(root, "docs", "review");
        var discoveredManifests = Directory
            .EnumerateFiles(
                reviewRoot,
                "harmonize-downstream-capability-specs-task-*-dirty-manifest-*.txt",
                SearchOption.TopDirectoryOnly)
            .Select(path => RelativePath(root, path))
            .Order(StringComparer.Ordinal)
            .ToArray();
        var recordedManifestPaths = checkpoint.Entries
            .Select(entry => entry.ManifestPath)
            .Concat(checkpoint.RejectedFreezes.Select(freeze => freeze.ManifestPath))
            .Concat(checkpoint.ArchivedFreezes.Select(freeze => freeze.ManifestPath))
            .Concat(checkpoint.ActiveFreeze is null
                ? []
                : [checkpoint.ActiveFreeze.ManifestPath])
            .Where(path => Path.GetFileName(path).StartsWith(
                "harmonize-downstream-capability-specs-task-", StringComparison.Ordinal))
            .Distinct(StringComparer.Ordinal)
            .Order(StringComparer.Ordinal)
            .ToArray();
        recordedManifestPaths.Should()
            .Equal(
                discoveredManifests,
                "every harmonization task review manifest must receive an explicit raw-order or set-only disposition");

        foreach (var entry in checkpoint.Entries)
        {
            ValidateReviewManifestProvenance(root, entry);
            ValidateReviewVerdictEvidence(root, entry);
            ValidateReviewStateEvidence(root, entry);
        }

        var verdictEvidence = checkpoint.Entries
            .SelectMany(entry => entry.VerdictEvidence)
            .ToArray();
        foreach (var rejectedFreeze in checkpoint.RejectedFreezes)
        {
            ValidateRejectedReviewFreeze(root, rejectedFreeze);
        }

        foreach (var archivedFreeze in checkpoint.ArchivedFreezes)
        {
            ValidateArchivedReviewFreeze(root, archivedFreeze, verdictEvidence);
        }

        if (checkpoint.ActiveFreeze is not null)
        {
            ValidateActiveReviewFreeze(root, checkpoint.ActiveFreeze, checkpoint.Entries);
        }

        ValidateFinalHarmonizationCloseout(
            root,
            checkpoint.Entries.Single(entry => entry.Task == "8.1"));
        ValidateReshapeTask80RequirementGate(root);
    }

    private static void ValidateReshapeTask80RequirementGate(string root)
    {
        var mapping = NormalizeLineEndings(File.ReadAllText(Path.Combine(
            root,
            Task80RequirementGateArtifact.Replace('/', Path.DirectorySeparatorChar))));
        Sha256(mapping).Should().Be(
            Task80RequirementGateSha256,
            "the reviewed Task 8.0 cross-tree mapping must not drift before Section 8 source work");

        var ledger = NormalizeLineEndings(File.ReadAllText(Path.Combine(
            root,
            "openspec",
            "changes",
            "reshape-developer-facing-interfaces",
            "tasks.md")));
        Regex.Matches(ledger, @"(?m)^- \[x\] 8\.0\b")
            .Should().HaveCount(1, "the mapping gate must be recorded once in reshape");
        ledger.Should().Contain("`artifacts/task-8-0-section-8-requirement-gate-2026-09-26.md`");
        ledger.Should().Contain(
            "No 8.1–8.10 source work may start until this exact 8.0 target receives independent approval and its checkpoint.");
        ledger.Should().Contain(
            "Task 8.10 must replace physical-file acceptance credit with compiled-test evidence for DR-AC-008, AC-321, AC-528, AC-606–AC-618 and JS-AC-001–JS-AC-018.");

        var jsRequirements = Regex.Matches(mapping, @"(?m)^\| JS-\d{3} \|")
            .Select(match => match.Value[2..^2]);
        jsRequirements.Should().Equal(Enumerable.Range(1, 10).Select(index => $"JS-{index:000}"));
        var jsCriteria = Regex.Matches(mapping, @"(?m)^\| JS-AC-\d{3} \|")
            .Select(match => match.Value[2..^2]);
        jsCriteria.Should().Equal(Enumerable.Range(1, 18).Select(index => $"JS-AC-{index:000}"));
        mapping.Should().Contain("DU-033 partitions public events from internal continuations");
        foreach (var criterion in new[] { "DR-AC-008", "AC-321", "AC-528", "AC-317" })
        {
            Regex.Matches(mapping, $@"(?m)^\| {Regex.Escape(criterion)} \|")
                .Should().HaveCount(1, $"criterion {criterion} must have one explicit owner decision");
        }
        mapping.Should().Contain("AC-317's stale 8.4 attribution is removed");
        mapping.Should().Contain("DU-031 names internal DAG child-start commands");
        mapping.Should().Contain("DR-037 gives the internal dispatcher the disjoint claim");
        mapping.Should().Contain("| JS-003 | 8.2, 8.3, 8.5, 8.6 |");
        mapping.Should().Contain("| JS-AC-005 | 8.3, 8.6, 8.10 |");
        mapping.Should().Contain("| JS-AC-018 | 6.7, 8.6, 8.9, 8.10 |");

        var waivers = File.ReadAllText(Path.Combine(root, "tests", "OrcaCore.Core.Tests",
            "RepositoryGuardTests.cs"));
        foreach (var (criterion, fragment) in new[]
        {
            ("AC-317", "task 9.1"), ("AC-321", "tasks 8.9 and 8.10"),
            ("AC-528", "tasks 8.7 and 8.10"), ("DR-AC-008", "tasks 8.4-8.6 and 8.10")
        })
        {
            var line = waivers.Split('\n').Single(value => value.Contains($"[\"{criterion}\"]", StringComparison.Ordinal));
            line.Should().Contain(fragment, $"the {criterion} waiver must name its real owner");
        }
        var section8WaiverIds = waivers.Split('\n')
            .Where(line => Regex.IsMatch(line, @"(?<!\d)8\.\d+", RegexOptions.CultureInvariant))
            .Select(line => Regex.Match(line,
                @"\[""((?:AC|DR-AC|JS-AC)-\d{3})""\]",
                RegexOptions.CultureInvariant))
            .Where(match => match.Success)
            .Select(match => match.Groups[1].Value)
            .Order(StringComparer.Ordinal)
            .ToArray();
        section8WaiverIds.Should().Equal(new[]
            {
                "AC-321", "AC-528", "AC-617", "AC-618", "DR-AC-008",
                "JS-AC-014", "JS-AC-015", "JS-AC-016", "JS-AC-017", "JS-AC-018"
            }.Order(StringComparer.Ordinal),
            "every waiver naming a Section 8 task must be explicitly reviewed in the handoff");

        ValidateSection8CompiledAcceptanceBaseline(root, ledger);
    }

    private static void ValidateSection8CompiledAcceptanceBaseline(string root, string ledger)
    {
        ReadCompiledAcceptanceTraitIds(
            "[Trait(\"AC\", \"AC-606\")]\n" +
            "// [Trait(\"AC\", \"AC-607\")]\n" +
            "/* [Trait(\"AC\", \"AC-608\")] */\n" +
            "var decoy = \"[Trait(\\\"AC\\\", \\\"AC-609\\\")]\";\n" +
            "var raw = \"\"\"[Trait(\"AC\", \"AC-610\")]\"\"\";")
            .Should().Equal(new[] { "AC-606" },
                "comment and string decoys are not compiled attributes");
        var required = Enumerable.Range(606, 13).Select(index => $"AC-{index:000}")
            .Concat(Enumerable.Range(1, 18).Select(index => $"JS-AC-{index:000}"))
            .Concat(["DR-AC-008", "AC-321", "AC-528"])
            .ToHashSet(StringComparer.Ordinal);
        var active = new HashSet<string>(StringComparer.Ordinal);
        var excluded = new HashSet<string>(StringComparer.Ordinal);
        var inventoryPath = Path.Combine(root, "tests", "OrcaCore.DeveloperSurface.Guards",
            "Fixtures", "section-07-r-declaration-crosswalk.json");
        using var inventory = JsonDocument.Parse(File.ReadAllText(inventoryPath));
        foreach (var source in inventory.RootElement.GetProperty("sourceInventory").EnumerateArray())
        {
            var status = source.GetProperty("status").GetString();
            if (status is not ("active" or "compile-excluded"))
            {
                continue;
            }

            var path = source.GetProperty("source").GetString()!;
            var contents = File.ReadAllText(Path.Combine(root, path.Replace('/', Path.DirectorySeparatorChar)));
            var destination = status == "active" ? active : excluded;
            foreach (var trait in ReadCompiledAcceptanceTraitIds(contents))
            {
                if (required.Contains(trait))
                {
                    destination.Add(trait);
                }
            }
        }

        var missing = required.Except(active, StringComparer.Ordinal).Order(StringComparer.Ordinal).ToArray();
        foreach (var completed in new[]
        {
            "- [x] 8.10", "- [X] 8.10", "- [x]  8.10", "  - [x] 8.10"
        })
        {
            IsTask810Complete(completed).Should().BeTrue(
                $"ledger-equivalent completion syntax {completed} must close the gate");
        }
        IsTask810Complete("- [ ] 8.10").Should().BeFalse();
        if (IsTask810Complete(ledger))
        {
            if (missing.Length != 0)
            {
                throw new InvalidDataException(
                    "Task 8.10 cannot close while these Section 8 criteria lack compiled trait-bearing tests: " +
                    string.Join(", ", missing));
            }
            return;
        }

        active.Should().BeEmpty("the Task 8.0 baseline has no genuine compiled Section 8 acceptance credit");
        excluded.Except(active, StringComparer.Ordinal).Should().BeEquivalentTo(
            Enumerable.Range(606, 11).Select(index => $"AC-{index:000}")
                .Concat(Enumerable.Range(1, 13).Select(index => $"JS-AC-{index:000}")),
            "compile-removed legacy files are recorded as debt, never executable credit");
        missing.Except(excluded, StringComparer.Ordinal).Should().BeEquivalentTo(
            new[] { "AC-321", "AC-528", "AC-617", "AC-618", "DR-AC-008",
                "JS-AC-014", "JS-AC-015", "JS-AC-016", "JS-AC-017", "JS-AC-018" });
    }

    private static bool IsTask810Complete(string ledger)
    {
        var completion = Regex.Matches(ledger,
            @"(?m)^[ \t]*-[ \t]+\[([ xX])\][ \t]+8\.10\b",
            RegexOptions.CultureInvariant);
        completion.Should().HaveCount(1, "reshape Task 8.10 must have exactly one ledger row");
        return completion[0].Groups[1].Value.Equals("x", StringComparison.OrdinalIgnoreCase);
    }

    private static IReadOnlyList<string> ReadCompiledAcceptanceTraitIds(string source)
    {
        var visible = source.ToCharArray();
        var code = new bool[source.Length];
        for (var index = 0; index < source.Length;)
        {
            if (source[index] == '/' && index + 1 < source.Length && source[index + 1] is '/' or '*')
            {
                var line = source[index + 1] == '/';
                visible[index++] = ' ';
                visible[index++] = ' ';
                while (index < source.Length)
                {
                    if (line && source[index] is '\r' or '\n')
                    {
                        break;
                    }
                    if (!line && source[index] == '*' && index + 1 < source.Length && source[index + 1] == '/')
                    {
                        visible[index++] = ' ';
                        visible[index++] = ' ';
                        break;
                    }
                    if (source[index] is not ('\r' or '\n'))
                    {
                        visible[index] = ' ';
                    }
                    index++;
                }
                continue;
            }

            if (source[index] is '"' or '\'')
            {
                var quote = source[index];
                var rawWidth = 0;
                if (quote == '"')
                {
                    while (index + rawWidth < source.Length && source[index + rawWidth] == '"')
                    {
                        rawWidth++;
                    }
                }
                if (rawWidth >= 3)
                {
                    index += rawWidth;
                    while (index < source.Length)
                    {
                        var closingWidth = 0;
                        while (index + closingWidth < source.Length && source[index + closingWidth] == '"')
                        {
                            closingWidth++;
                        }
                        if (closingWidth >= rawWidth)
                        {
                            index += closingWidth;
                            break;
                        }
                        index++;
                    }
                    continue;
                }

                var verbatim = quote == '"' && index > 0 &&
                    (source[index - 1] == '@' || index > 1 && source[index - 1] == '$' && source[index - 2] == '@');
                index++;
                while (index < source.Length)
                {
                    if (!verbatim && source[index] == '\\')
                    {
                        index = Math.Min(index + 2, source.Length);
                        continue;
                    }
                    if (source[index] == quote)
                    {
                        index++;
                        if (verbatim && index < source.Length && source[index] == '"')
                        {
                            index++;
                            continue;
                        }
                        break;
                    }
                    index++;
                }
                continue;
            }

            code[index++] = true;
        }

        return Regex.Matches(new string(visible),
                @"\[[ \t\r\n]*Trait[ \t\r\n]*\([ \t\r\n]*""AC""[ \t\r\n]*,[ \t\r\n]*""((?:AC|DR-AC|JS-AC)-\d{3})""[ \t\r\n]*\)[ \t\r\n]*\]",
                RegexOptions.CultureInvariant)
            .Where(match => code[match.Index])
            .Select(match => match.Groups[1].Value)
            .ToArray();
    }

    private static void ValidateFinalHarmonizationCloseout(
        string root,
        ReviewManifestProvenanceEntry exitReview)
    {
        var audit = NormalizeLineEndings(File.ReadAllText(Path.Combine(
            root,
            Task81AuditArtifact.Replace('/', Path.DirectorySeparatorChar))));
        Sha256(audit).Should().Be(
            Task81AuditSha256,
            "the approved Task 8.1 audit must remain byte-stable after its active freeze is archived");

        var ledger = NormalizeLineEndings(File.ReadAllText(Path.Combine(
            root,
            "openspec",
            "changes",
            "harmonize-downstream-capability-specs",
            "tasks.md")));
        const string heading = "## 8. Final harmonization gate";
        var start = ledger.IndexOf(heading, StringComparison.Ordinal);
        start.Should().BeGreaterThanOrEqualTo(0);
        ledger.IndexOf(heading, start + heading.Length, StringComparison.Ordinal)
            .Should().Be(-1, "the final harmonization gate must have one ledger section");
        var closeout = ledger[start..];
        Sha256(closeout).Should().Be(
            FinalHarmonizationLedgerSectionSha256,
            "the approved audit and post-approval checkpoint accounting must remain reviewed evidence");
        foreach (var task in new[] { "8.1", "8.2", "8.3" })
        {
            Regex.Matches(closeout, $@"(?m)^- \[x\] {Regex.Escape(task)}\b")
                .Should().HaveCount(1, $"harmonization task {task} must be recorded complete exactly once");
        }

        exitReview.ReviewState.Should().Be(ApprovedReviewState);
        closeout.Should().Contain("`" + exitReview.CheckpointCommit + "`");
        closeout.Should().Contain("`" + exitReview.ApprovalEvidenceCommit + "`");
        closeout.Should().Contain(
            "it does not authorize reshape task 8.0 or",
            "the harmonization exit verdict does not grant Section 8 implementation authority");
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
        ValidateReviewStateEvidence(root, task51);
        ValidateReviewVerdictEvidence(root, task52);
        ValidateReviewStateEvidence(root, task52);

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

        var postGateCheckpoint = FixtureDefinitions.Read<PostGateAmendmentCheckpoint>(
            "tests/OrcaCore.DeveloperSurface.Guards/Fixtures/post-gate-amendment-path.json");
        var registeredSuccessorKeys = ValidateProposedRequirementSuccessors(
            root,
            canonicalRequirements,
            activeDeltaRequirements,
            postGateCheckpoint);

        var duplicateOwners = activeRequirementOwners
            .GroupBy(
                owner => $"{owner.Capability}\0{owner.Requirement}",
                StringComparer.Ordinal)
            .Where(group => group.Count() > 1 && !registeredSuccessorKeys.Contains(group.Key))
            .OrderBy(group => group.Key, StringComparer.Ordinal)
            .Select(group =>
                $"{group.First().Capability} :: {group.First().Requirement} => " +
                string.Join(", ", group
                    .Select(owner => $"{owner.Change} [{owner.Operation}]")
                    .Order(StringComparer.Ordinal)))
            .ToArray();
        duplicateOwners.Should().BeEmpty(
            "only a source-pinned proposed post-gate successor may share an active heading; " +
            "unregistered duplicate owners were: {0}",
            string.Join("; ", duplicateOwners));

        ValidateChangeToCanonicalProvenance(
            root,
            canonicalCapabilityDirectories,
            canonicalRequirements,
            activeDeltaRequirements,
            activeCapabilityDirectories);

        PreserveRuntimeConcurrencyStrayDisposition(root, changesRoot);
    }

    private static HashSet<string> ValidateProposedRequirementSuccessors(
        string root,
        IReadOnlyDictionary<string, IReadOnlyDictionary<string, CanonicalRequirement>> canonicalRequirements,
        IReadOnlyCollection<DeltaRequirement> activeRequirements,
        PostGateAmendmentCheckpoint checkpoint)
    {
        checkpoint.SchemaVersion.Should().Be(8);
        checkpoint.ProposedRequirementSuccessors.Should().Equal(
            ExactProposedDagSuccessors,
            "only the exact reviewed predecessor/successor headings may bypass sole active ownership");
        checkpoint.ApprovedPendingRequirementSuccessors.Should().Equal(
            ExactApprovedPendingDagSuccessors,
            "the approved-pending canonical targets must remain source-pinned independently of fixture edits");
        checkpoint.ApprovedPendingEvidence.Should().Be(
            ExactDagFriendApprovalEvidence,
            "the approved-pending transition must cite the real reviewed checkpoint and verdict commit");

        var evidence = checkpoint.ApprovedPendingEvidence;
        ValidateDagApprovalEvidence(root, evidence.ReviewedCheckpointCommit,
            evidence.ApprovalEvidenceCommit, evidence.ApprovalVerdictPath,
            evidence.ApprovalVerdictSha256, evidence.ApprovalTask);

        const string change = "admit-dag-authoring-friend-boundary";
        ValidateTaskReference(root, new PostGateTaskReference(change, evidence.ApprovalTask, CompleteTaskState));
        ValidateTaskReference(root, new PostGateTaskReference(change, evidence.CanonicalSyncTask, CompleteTaskState));
        ValidateTaskReference(root, new PostGateTaskReference(change, evidence.RegistryTransitionTask, CompleteTaskState));
        // ApprovedPending is retained history. Complete is admitted only through the
        // separately reviewed implementation checkpoint and its direct-child verdict.
        ValidateCompletedDagFriend(root, checkpoint.CompleteEvidence);

        var keys = new HashSet<string>(StringComparer.Ordinal);
        foreach (var successor in checkpoint.ProposedRequirementSuccessors)
        {
            var key = $"{successor.Capability}\0{successor.Requirement}";
            keys.Add(key).Should().BeTrue("proposed successor identities must be unique");
            successor.Stage.Should().Be(ProposedPostGateStage,
                "proposal-stage ownership does not imply amendment approval or source authority");

            ValidateActivePostGateOwners(root, activeRequirements, successor.Capability, successor.Requirement);
            var predecessor = ReadResolvedPostGateOwner(root, successor.PredecessorChange,
                successor.Capability, successor.Requirement);
            var amendment = ReadResolvedPostGateOwner(root, successor.Change,
                successor.Capability, successor.Requirement);
            predecessor.Operation.ToString().ToUpperInvariant().Should().Be(successor.PredecessorOperation);
            amendment.Operation.ToString().ToUpperInvariant().Should().Be(successor.SuccessorOperation);
            var approved = checkpoint.ApprovedPendingRequirementSuccessors.Single(item =>
                item.Capability == successor.Capability && item.Requirement == successor.Requirement);
            Sha256(predecessor.Block).Should().Be(approved.HistoricalCanonicalBlockSha256);
            Sha256(amendment.Block).Should().Be(approved.CanonicalBlockSha256);
            ClassifyPostGateProvenance(canonicalRequirements, predecessor, checkpoint).State.Should().Be(
                ProvenanceState.SupersededByApprovedSuccessor);
            ClassifyPostGateProvenance(canonicalRequirements, amendment, checkpoint).State.Should().Be(
                ProvenanceState.SupersededByApprovedSuccessor);

            var task = ValidateTaskReference(
                root,
                new PostGateTaskReference(successor.Change, successor.TurnsGreenTask, CompleteTaskState));
            task.Should().Contain($"`{successor.Capability}` (1)",
                "each approved requirement must retain its exact canonical-sync owner");
        }

        ValidateProposedRuntimeViewSuccessors(root, canonicalRequirements, activeRequirements, checkpoint);
        return keys;
    }

    private static void ValidateProposedRuntimeViewSuccessors(
        string root,
        IReadOnlyDictionary<string, IReadOnlyDictionary<string, CanonicalRequirement>> canonicalRequirements,
        IReadOnlyCollection<DeltaRequirement> activeRequirements,
        PostGateAmendmentCheckpoint checkpoint)
    {
        checkpoint.ProposedRuntimeViewSuccessors.Should().Equal(ExactProposedRuntimeViewSuccessors,
            "the proposed rows are retained as immutable process history");
        checkpoint.ApprovedRuntimeViewRequirementSuccessors.Should().Equal(ExactApprovedRuntimeViewSuccessors);
        checkpoint.SupersededRuntimeViewPredecessors.Should().Equal(ExactRuntimeViewSupersededPredecessors,
            "only four exact old blocks may be superseded by the two runtime-view requirements");
        checkpoint.ApprovedRuntimeViewEvidence.Should().Be(ExactRuntimeViewApprovalEvidence);
        checkpoint.HistoricalRuntimeViewContractArtifacts.Should().Equal(ExactHistoricalRuntimeViewContracts);
        foreach (var artifact in checkpoint.HistoricalRuntimeViewContractArtifacts)
        {
            var path = Path.Combine(ResolveChangeRecord(root, artifact.Change),
                artifact.RelativePath.Replace('/', Path.DirectorySeparatorChar));
            File.Exists(path).Should().BeTrue("a permanent historical contract must remain available");
            Sha256(NormalizeLineEndings(File.ReadAllText(path))).Should().Be(artifact.NormalizedSha256);
        }
        Sha256(NormalizeLineEndings(File.ReadAllText(RequireNonEmptyFile(root,
            RuntimeViewAtomicTransitionArtifact, "atomic runtime-view transition"))))
            .Should().Be(RuntimeViewAtomicTransitionSha256);
        var runtimeOwner = ReadCanonicalRequirements(root, Path.Combine(root, "openspec", "specs", "durable-runtime", "spec.md"));
        Sha256(runtimeOwner["Durable DAG progression is runtime owned"].Block).Should().Be("11741edaf2f26fd43846948d5395a8fe5be92ce61c016e3dc232d11dc810cd40");
        var authoringOwner = ReadCanonicalRequirements(root, Path.Combine(root, "openspec", "specs", "workflow-authoring", "spec.md"));
        Sha256(authoringOwner["Built definitions are immutable"].Block).Should().Be("d1a5dc99ac3f9c8b2ecf9285d3ff7df0bcb3a547240b4c6b0d2621243b37a240");
        var evidence = checkpoint.ApprovedRuntimeViewEvidence;
        ValidateDagApprovalEvidence(root, evidence.ReviewedCheckpointCommit, evidence.ApprovalEvidenceCommit,
            evidence.ApprovalVerdictPath, evidence.ApprovalVerdictSha256, evidence.ApprovalTask);
        var reviewedTree = RunGit(root, "rev-parse", $"{evidence.ReviewedCheckpointCommit}^{{tree}}");
        reviewedTree.ExitCode.Should().Be(0);
        reviewedTree.StandardOutput.Trim().Should().Be("5c9d63fe0b30ea4465054ac01c986a8b80707e62");
        ValidateDagApprovalEvidence(root,
            "dc8095c5536316cb772c985641e45179fa3c93b5",
            "0f4fafa3c3b3acfdb2c39227bba53f094782bd13",
            "docs/review/developer-facing-interface-section-08-task-8-3-runtime-view-successor-process-independent-review-verdict-2026-09-30.md",
            "bc72cd0eca94e77a1c09744e18aea6bb0133cfe5429ddf5a50fdfb4e0c70cd4c", "0.1");
        foreach (var task in new[] { "0.1", "0.2", "1.1", "1.2", "1.3", "1.4", "2.1", "2.2", "2.3" })
            ValidateTaskReference(root, new PostGateTaskReference(RuntimeViewChange, task, CompleteTaskState));
        // The source candidate requires the actual atomic-transition approval, not a checkbox.
        ValidateDagApprovalEvidence(root,
            "a0da21ba9597e3864a3d4134120fbb0138417bd7",
            "6c8bcd02bf6c747800c17dbc27afce53d24efbc1",
            "docs/review/developer-facing-interface-section-08-task-8-3-runtime-view-atomic-canonical-transition-independent-review-verdict-2026-10-03.md",
            "cce8c5195f559f1624a002ef4870c32ccdb445163aec9f552dcbfc5245de7e35", "1.4");
        var atomicTree = RunGit(root, "rev-parse", "a0da21ba9597e3864a3d4134120fbb0138417bd7^{tree}");
        atomicTree.ExitCode.Should().Be(0);
        atomicTree.StandardOutput.Trim().Should().Be("572d86f6801e36a0611227386939e4dcbb1ab885");
        // 2.4 includes independent review/checkpoint; leave only its checkbox activation unpinned.
        // 3.2 must bind the later actual source APPROVE/checkpoint before Complete promotion.
        foreach (var task in new[] { "3.1", "3.2" })
            ValidateTaskReference(root, new PostGateTaskReference(RuntimeViewChange, task, OpenTaskState));
        ValidateDagApprovalEvidence(root,
            "b5fb28e65dbf3fea102ddec1d5fe1cf9d794c659", "dbc3086da206bde20cc8624816e3f45f1d13f9b8",
            "docs/review/developer-facing-interface-section-08-task-8-2-authoring-friend-closeout-independent-review-verdict-2026-09-29.md",
            "221c60c34f1db12c747eb2da20a7107bbe40baba227a9b8e0c223db1e913e531", "3.2");
        ValidateTaskReference(root, new PostGateTaskReference("admit-dag-authoring-friend-boundary", "3.2", CompleteTaskState));
        ValidateRuntimeViewContractProposal(root);
        foreach (var successor in checkpoint.ApprovedRuntimeViewRequirementSuccessors)
        {
            successor.Stage.Should().Be(ApprovedPendingPostGateStage);
            ValidateActivePostGateOwners(root, activeRequirements, successor.Capability, successor.Requirement);
            var current = ReadResolvedPostGateOwner(root, RuntimeViewChange, successor.Capability, successor.Requirement);
            current.Operation.Should().Be(RequirementOperation.Modified);
            Sha256(current.Block).Should().Be(successor.CanonicalBlockSha256);
            ClassifyProvenance(canonicalRequirements, current).State.Should().Be(ProvenanceState.Synchronized);
        }
    }

    private static DeltaRequirement ReadResolvedPostGateOwner(
        string root, string change, string capability, string heading)
    {
        var record = ResolveChangeRecord(root, change);
        var declared = ReadDeclaredCapabilities(root, Path.Combine(record, "proposal.md"))
            .Single(item => item.Name == capability);
        return ReadDeltaRequirements(root, change, capability, declared.Kind,
                Path.Combine(record, "specs", capability, "spec.md"))
            .Single(item => item.Requirement == heading);
    }

    private static void ValidateActivePostGateOwners(
        string root, IReadOnlyCollection<DeltaRequirement> active, string capability, string heading)
    {
        string[] chain = ["reshape-developer-facing-interfaces", "admit-dag-authoring-friend-boundary", RuntimeViewChange];
        foreach (var change in chain) _ = ResolveChangeRecord(root, change);
        var expectedActive = chain.Where(change => Directory.Exists(Path.Combine(root, "openspec", "changes", change)))
            .Order(StringComparer.Ordinal);
        active.Where(item => item.Capability == capability && item.Requirement == heading)
            .Select(item => item.Change).Order(StringComparer.Ordinal).Should().Equal(expectedActive,
                "only the exact chain may own this heading, whether predecessors are active or dated archived");
    }

    private static void ValidateRuntimeViewContractProposal(string root)
    {
        var supersededContract = NormalizeLineEndings(File.ReadAllText(
            RequireNonEmptyFile(root, $"openspec/changes/{RuntimeViewChange}/artifacts/task-1-1-runtime-view-contract-2026-10-02.md",
                "permanent superseded rejected runtime-view contract")));
        Sha256(supersededContract).Should().Be("5527189563e0f39eccbb9e56bc902d2e1e4cc9e2695e0db929cc5e4b03a7dc12",
            "the superseded rejected contract must remain byte-exact after approval and archival");
        var artifact = NormalizeLineEndings(File.ReadAllText(
            RequireNonEmptyFile(root, RuntimeViewContractArtifact, "proposed runtime-view contract")));
        Sha256(artifact).Should().Be(RuntimeViewContractArtifactSha256,
            "the exact proposed signatures, null policy and full disposition must stay review-bound");
        var matrix = NormalizeLineEndings(File.ReadAllText(
            RequireNonEmptyFile(root, "docs/specs/17-selected-mode-capability-matrix.md",
                "proposed numbered runtime-view contract")));
        var numbered = Regex.Matches(matrix,
            @"<!-- runtime-view-contract:start -->\n[\s\S]*?<!-- runtime-view-contract:end -->");
        numbered.Should().ContainSingle("the complete proposed contract belongs in typed DAG planning");
        var dagSection = matrix.IndexOf("### 17.2.6 Typed DAG planning", StringComparison.Ordinal);
        dagSection.Should().BeGreaterThanOrEqualTo(0);
        var dagBodyStart = matrix.IndexOf('\n', dagSection) + 1;
        var followingHeading = Regex.Match(matrix[dagBodyStart..], @"(?m)^#{2,3} 17\.");
        var nextSection = followingHeading.Success ? dagBodyStart + followingHeading.Index : matrix.Length;
        numbered[0].Index.Should().BeGreaterThan(dagSection);
        (numbered[0].Index + numbered[0].Length).Should().BeLessThan(nextSection);
        Sha256(numbered[0].Value).Should().Be(RuntimeViewNumberedProposalSha256,
            "proposal status, signatures, successful-null and failure policy must remain durable after freeze");
        var boundary = Regex.Matches(matrix,
            @"Implementation package boundaries use exact type-safe internal friends[\s\S]*?(?=Phase 0 packs)");
        boundary.Should().ContainSingle("doc 17 section 17.5 explicitly disposes of both friend grants");
        Sha256(boundary[0].Value.TrimEnd()).Should().Be(RuntimeViewNumberedBoundarySha256);
        var signatureTexts = new List<string>
        {
            artifact,
            NormalizeLineEndings(File.ReadAllText(
                RequireNonEmptyFile(root, $"openspec/changes/{RuntimeViewChange}/design.md", "runtime-view design"))),
            numbered[0].Value
        };
        foreach (var capability in new[] { "developer-facing-surface", "repository-foundation" })
        {
            signatureTexts.Add(NormalizeLineEndings(File.ReadAllText(
                RequireNonEmptyFile(root, $"openspec/changes/{RuntimeViewChange}/specs/{capability}/spec.md",
                    "self-contained normative runtime-view signature contract"))));
        }
        foreach (var text in signatureTexts)
        {
            var signature = Regex.Matches(text, @"\x60\x60\x60csharp\n(.*?)\n\x60\x60\x60", RegexOptions.Singleline);
            signature.Should().ContainSingle("every normative owner defines all fifteen exact runtime-view signatures");
            Sha256(signature[0].Groups[1].Value).Should().Be(RuntimeViewSignatureContractSha256);
        }
        var composition = NormalizeLineEndings(File.ReadAllText(
            RequireNonEmptyFile(root, "docs/specs/08-requirements-composition.md", "proposed composition contract")));
        var compositionBlocks = new List<string>();
        foreach (var heading in new[]
                 { "CP-020 Separate package and dependency direction", "CP-022 Direct-dependency input mapping" })
        {
            var matches = Regex.Matches(composition,
                $@"(?m)^### {Regex.Escape(heading)}\n[\s\S]*?(?=^### |\z)");
            matches.Should().ContainSingle();
            compositionBlocks.Add(matches[0].Value.TrimEnd());
        }
        Sha256(string.Join('\n', compositionBlocks)).Should().Be(RuntimeViewCompositionProposalSha256,
            "CP-020/CP-022 proposal status and exact doc 17 link cannot silently flip after checkpoint");
        var reshape = NormalizeLineEndings(File.ReadAllText(
            RequireNonEmptyFile(root, "openspec/changes/reshape-developer-facing-interfaces/tasks.md",
                "proposed mapping and durable bridge handoff")));
        var handoff = Regex.Matches(reshape, @"(?m)^- \[ \] 8\.[345] .+$")
            .Select(match => match.Value).ToArray();
        handoff.Should().HaveCount(3);
        Sha256(string.Join('\n', handoff)).Should().Be(RuntimeViewReshapeHandoffSha256,
            "the proposed open 8.3–8.5 mapping/codec ownership remains durable without pinning Task 1.2");
    }

    private static void ValidateCompletedDagFriend(string root, PostGateCompleteEvidence evidence)
    {
        evidence.Should().BeEquivalentTo(ExactDagFriendCompleteEvidence,
            options => options.WithStrictOrdering(),
            "the Complete transition must retain the exact reviewed Task 8.2 implementation and evidence");
        evidence.Stage.Should().Be(CompletePostGateStage);
        evidence.ImplementationTasks.Should().HaveCount(evidence.ImplementationTaskCount);
        var artifact = NormalizeLineEndings(File.ReadAllText(
            RequireNonEmptyFile(root, DagFriendCloseoutArtifact, "permanent DAG friend closeout")));
        Sha256(artifact).Should().Be(DagFriendCloseoutArtifactSha256,
            "the permanent closeout decision must remain pinned after refreeze");
        const string change = "admit-dag-authoring-friend-boundary";
        foreach (var task in evidence.ImplementationTasks)
        {
            ValidateTaskReference(root, new PostGateTaskReference(change, task, CompleteTaskState));
        }
        ValidateTaskReference(root, new PostGateTaskReference(change, "3.1", CompleteTaskState));
        ValidateTaskReference(root, new PostGateTaskReference(
            "reshape-developer-facing-interfaces", "8.2", CompleteTaskState));
        ValidateDagApprovalEvidence(root, evidence.ReviewedCheckpointCommit,
            evidence.ApprovalEvidenceCommit, evidence.ApprovalVerdictPath,
            evidence.ApprovalVerdictSha256, "8.2");

        var tree = RunGit(root, "show", "-s", "--format=%T", evidence.ReviewedCheckpointCommit);
        tree.ExitCode.Should().Be(0);
        tree.StandardOutput.Trim().Should().Be(evidence.ReviewedCheckpointTree);
        foreach (var (path, hash) in new[]
                 {
                     (evidence.RefreezeManifestPath, evidence.RefreezeManifestSha256),
                     (evidence.ReviewRequestPath, evidence.ReviewRequestSha256)
                 })
        {
            var bytes = File.ReadAllBytes(RequireNonEmptyFile(root, path, "DAG source refreeze evidence"));
            Sha256(bytes).Should().Be(hash);
            ReadGitBlob(root, evidence.ReviewedCheckpointCommit, path).Should().Equal(bytes,
                "the completed source record must bind the immutable packet in its reviewed checkpoint");
        }
        foreach (var path in evidence.ExecutableEvidencePaths)
        {
            RequireNonEmptyFile(root, path, "DAG friend executable evidence");
            ReadGitBlob(root, evidence.ReviewedCheckpointCommit, path).Should().NotBeEmpty();
        }
    }

    private static void ValidateDagApprovalEvidence(string root, string checkpoint, string commit,
        string path, string hash, string task)
    {
        var bytes = File.ReadAllBytes(RequireNonEmptyFile(root, path, "DAG independent approval verdict"));
        Sha256(bytes).Should().Be(hash);
        RequireApprovalEvidence(root, path, task);
        var text = NormalizeLineEndings(Encoding.UTF8.GetString(bytes)).TrimEnd();
        var verdicts = Regex.Matches(text, @"(?m)^\*\*Verdict:\*\* \*\*APPROVE\*\*$");
        verdicts.Should().ContainSingle("DAG approval must have one terminal approving verdict line");
        (verdicts[0].Index + verdicts[0].Length).Should().Be(text.Length,
            "the approving verdict must be the final nonblank line");
        ReadGitBlob(root, commit, path).Should().Equal(bytes);
        var parents = RunGit(root, "rev-list", "--parents", "-n", "1", commit);
        parents.ExitCode.Should().Be(0);
        NormalizeLineEndings(parents.StandardOutput)
            .Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries)
            .Should().Equal([commit, checkpoint],
                "DAG evidence must be a single-parent direct child of the reviewed checkpoint");
        var addition = RunGit(root, "diff-tree", "--root", "--no-commit-id", "--name-status",
            "-r", "--no-renames", commit, "--", path);
        addition.ExitCode.Should().Be(0);
        NormalizeLineEndings(addition.StandardOutput).Trim().Should().Be($"A\t{path}",
            "the evidence commit must add the verdict, rather than only contain an earlier copy");
    }

    [Fact]
    public void Task53_ReshapeSolelyOwnsFriendTopologyAndDurableGovernanceAggregate()
    {
        const string expectedOwner = "reshape-developer-facing-interfaces";
        (string Capability, string Requirement, RequirementOperation Operation)[] targets =
        [
            (
                "repository-foundation",
                "Dependency direction remains one-way",
                RequirementOperation.Modified),
            (
                "durable-runtime",
                "Durable resource governance is one serialized provider aggregate",
                RequirementOperation.Added)
        ];

        var root = FixtureDefinitions.RepositoryRoot();
        var requirements = ReadTask53ActiveDeltaRequirements(root);
        var proposedSuccessors = FixtureDefinitions.Read<PostGateAmendmentCheckpoint>(
                "tests/OrcaCore.DeveloperSurface.Guards/Fixtures/post-gate-amendment-path.json")
            .ProposedRequirementSuccessors;
        proposedSuccessors.Should().Equal(ExactProposedDagSuccessors);
        foreach (var target in targets)
        {
            var owners = requirements
                .Where(requirement =>
                    requirement.Capability == target.Capability &&
                    requirement.Requirement == target.Requirement)
                .ToArray();
            var proposedSuccessor = proposedSuccessors.SingleOrDefault(successor =>
                successor.Capability == target.Capability &&
                successor.Requirement == target.Requirement);
            if (proposedSuccessor is null)
            {
                owners.Select(item => item.Change).Should().Equal(
                    Directory.Exists(Path.Combine(root, "openspec", "changes", expectedOwner))
                        ? new[] { expectedOwner }
                        : Array.Empty<string>());
            }
            else
            {
                ValidateActivePostGateOwners(root, requirements, target.Capability, target.Requirement);
            }
            var owner = ReadResolvedPostGateOwner(root, expectedOwner, target.Capability, target.Requirement);
            owner.Change.Should().Be(
                expectedOwner,
                "reshape remains the approved predecessor owner of {0} :: {1}",
                target.Capability,
                target.Requirement);
            owner.Operation.Should().Be(
                target.Operation,
                "Task 5.3 must retain each aggregate's approved delta disposition");
            if (proposedSuccessor is not null)
            {
                var successorOwner = ReadResolvedPostGateOwner(root, proposedSuccessor.Change,
                    target.Capability, target.Requirement);
                successorOwner.Operation.Should().Be(RequirementOperation.Modified);
                proposedSuccessor.PredecessorChange.Should().Be(expectedOwner);
                proposedSuccessor.Stage.Should().Be(ProposedPostGateStage);
            }

            var ownerBody = RequirementBody(owner.Block);
            var copiedBodies = requirements
                .Where(requirement =>
                    !(requirement.Change == owner.Change &&
                      requirement.Capability == owner.Capability &&
                      requirement.Requirement == owner.Requirement) &&
                    string.Equals(
                        RequirementBody(requirement.Block),
                        ownerBody,
                        StringComparison.Ordinal))
                .Select(requirement =>
                    $"{requirement.Change} :: {requirement.Capability} :: {requirement.Requirement}")
                .Order(StringComparer.Ordinal)
                .ToArray();
            copiedBodies.Should().BeEmpty(
                "byte-identical normative content under another heading or capability is still a competing " +
                "active owner for {0} :: {1}; copies were: {2}",
                target.Capability,
                target.Requirement,
                string.Join(", ", copiedBodies));
        }
    }

    [Fact]
    public void Task61_CoreRuntimeDocumentsAuthoringSessionLifecycleAndExecutableEvidence()
    {
        const string requirementId = "CR-009a";
        const string canonicalRequirement =
            "Authoring handles are phase-bound and definitions are frozen";
        const string callbackLocalScopeClause =
            "A nested, branch, item, leased, or callback-local scope handle SHALL expire";
        string[] lifecycleStates = ["Open", "JoinPending", "Frozen"];
        string[] lifecycleCodes =
        [
            "SFE-AUTH-LIFECYCLE-001",
            "SFE-AUTH-LIFECYCLE-002",
            "SFE-AUTH-LIFECYCLE-003",
            "SFE-AUTH-LIFECYCLE-004",
            "SFE-AUTH-LIFECYCLE-005"
        ];
        string[] evidenceMembers =
        [
            "EphemeralParallelJoin_SupersedesRootAndReturnsSuccessorEpochFacade",
            "DurableJoinSelection_IsSingleUseAndRejectedSelectionDoesNotMutateGraph",
            "EphemeralRootTerminal_FreezesSnapshotAndRepeatedBuildsAreStable",
            "CallbackHandle_ExpiresWhenCallbackReturns",
            "DurableConcurrentAuthoring_AdmitsOneAtomicWinnerAndRejectsLoserWithoutGraphMutation"
        ];

        var root = FixtureDefinitions.RepositoryRoot();
        var numberedPath = Path.Combine(root, "docs", "specs", "04-requirements-core-runtime.md");
        var numbered = NormalizeLineEndings(File.ReadAllText(numberedPath));
        var heading = Regex.Match(
            numbered,
            $@"(?m)^### {Regex.Escape(requirementId)} Authoring sessions have one explicit lifecycle$");
        heading.Success.Should().BeTrue("Task 6.1 must add one stable numbered lifecycle requirement");
        Regex.Matches(numbered, $@"(?m)^### {Regex.Escape(requirementId)}(?:\s|$)")
            .Should().ContainSingle("the stable requirement ID must have exactly one owner");
        var nextHeading = numbered.IndexOf("\n##", heading.Index + heading.Length, StringComparison.Ordinal);
        var sectionEnd = nextHeading < 0 ? numbered.Length : nextHeading;
        var numberedBlock = numbered[heading.Index..sectionEnd];

        lifecycleStates.Should().OnlyContain(state => numberedBlock.Contains($"`{state}`", StringComparison.Ordinal));
        lifecycleCodes.Should().OnlyContain(code => numberedBlock.Contains($"`{code}`", StringComparison.Ordinal));
        numberedBlock.Should().Contain("Its state SHALL be exactly `Open`, `JoinPending`, or `Frozen`");
        numberedBlock.Should().Contain("session, authoring epoch, and lexical-scope token");
        numberedBlock.Should().Contain("atomically move the session from `Open` to `JoinPending`");
        numberedBlock.Should().Contain("capture one immutable authored-graph snapshot");
        numberedBlock.Should().Contain("before mutating the graph");

        var canonicalPath = Path.Combine(root, "openspec", "specs", "workflow-authoring", "spec.md");
        var canonical = ReadRequirementBlocks(root, canonicalPath, isDelta: false)
            .Single(requirement => requirement.Requirement == canonicalRequirement);
        var reshapePath = Path.Combine(
            root,
            "openspec",
            "changes",
            "reshape-developer-facing-interfaces",
            "specs",
            "workflow-authoring",
            "spec.md");
        var reshape = ReadRequirementBlocks(root, reshapePath, isDelta: true)
            .Single(requirement => requirement.Requirement == canonicalRequirement);
        canonical.Block.Should().Be(
            reshape.Block,
            "the canonical lifecycle requirement must remain byte-equivalent to its active owning delta");
        foreach (var block in new[] { canonical.Block, reshape.Block })
        {
            Regex.Replace(block, @"\s+", " ")
                .Should().Contain(
                    callbackLocalScopeClause,
                    "both normative owners must keep callback-local scope handles inside the lifecycle contract");
        }

        lifecycleStates.Should().OnlyContain(state => canonical.Block.Contains($"`{state}`", StringComparison.Ordinal));
        canonical.Block.Should().Contain("Every builder handle SHALL be valid only for the session epoch and lexical scope");
        canonical.Block.Should().Contain("SHALL leave the authored graph unchanged");

        var evidencePath = Path.Combine(
            root,
            "tests",
            "OrcaCore.Core.Tests",
            "Building",
            "AuthoringLifecycleTests.cs");
        var evidence = NormalizeLineEndings(File.ReadAllText(evidencePath));
        evidence.Should().Contain($"[Trait(\"Requirement\", \"{requirementId}\")]");
        evidence.Should().NotContain(
            "[Trait(\"AC\", \"AC-021\")]",
            "the lifecycle suite must not reclaim the unrelated lambda-mode acceptance criterion");
        foreach (var member in evidenceMembers)
        {
            var declaration = Regex.Match(
                evidence,
                $@"(?ms)(?<attributes>(?:^[ \t]*\[[^\n]+\][ \t]*\n)+)[ \t]*public[ \t]+(?:async[ \t]+)?(?:void|Task)[ \t]+{Regex.Escape(member)}[ \t]*\(",
                RegexOptions.CultureInvariant);
            declaration.Success.Should().BeTrue("{0} must remain a discovered lifecycle test", member);
            var attributes = declaration.Groups["attributes"].Value;
            attributes.Should().MatchRegex(@"(?m)^[ \t]*\[(?:Fact|Theory)(?:\]|\()", "{0} must be executable by xUnit", member);
            attributes.Should().NotContain("Skip", "{0} must not be disabled", member);
        }

        var lambdaEvidence = NormalizeLineEndings(File.ReadAllText(Path.Combine(
            root,
            "tests",
            "OrcaCore.Core.Tests",
            "Building",
            "StagedWorkflowBuilderTests.cs")));
        lambdaEvidence.Should().Contain(
            "[Trait(\"AC\", \"AC-021\")]",
            "removing AC-021 from the lifecycle suite must not orphan its actual lambda-mode evidence");

        var ci = NormalizeLineEndings(File.ReadAllText(Path.Combine(
            root,
            ".github",
            "workflows",
            "ci.yml")));
        var lifecycleSteps = ReadCiWorkflowSteps(ci)
            .Where(step => Regex.IsMatch(
                step.Value,
                @"(?m)^[ \t]*-[ \t]+name:[ \t]+Authoring lifecycle requirement evidence[ \t]*$",
                RegexOptions.CultureInvariant))
            .ToArray();
        lifecycleSteps.Should().ContainSingle(
            "CI must contain one dedicated authoring-lifecycle requirement evidence step");
        var lifecycleCommand = NormalizeCiWorkflowStep(lifecycleSteps[0]).Trim();
        lifecycleCommand.Should().Be(
            "- name: Authoring lifecycle requirement evidence run: > dotnet test " +
            "tests/OrcaCore.Core.Tests/OrcaCore.Core.Tests.csproj --no-build --no-restore " +
            "-c Release --filter \"Requirement=CR-009a\" --logger trx --results-directory TestResults");
    }

    [Fact]
    public void Task62_CoreRuntimeDocumentsWorkflowFailureProvenanceAndExecutableEvidence()
    {
        const string requirementId = "CR-014a";
        const string authoritativeRequirement =
            "Authoring lifecycle, fingerprint coverage, and failure provenance are executable";
        string[] numberedClauses =
        [
            "exactly one immutable `AuthoredLocation`",
            "exactly one runtime-created `FailureOccurrence`",
            "closed to `Root`, `Branch(AuthoredBranchId)`, and `Item(index)`",
            "attach when the failure is created",
            "Propagating one failure SHALL preserve",
            "fixed branches ordered by authored branch order",
            "dynamic items ordered by item index",
            "versioned closed discriminator allowlist `root`/`branch`/`item`",
            "Unknown discriminator kinds or versions"
        ];

        var root = FixtureDefinitions.RepositoryRoot();
        var numberedPath = Path.Combine(root, "docs", "specs", "04-requirements-core-runtime.md");
        var numbered = NormalizeLineEndings(File.ReadAllText(numberedPath));
        var heading = Regex.Match(
            numbered,
            $@"(?m)^### {Regex.Escape(requirementId)} Workflow failures retain authored and runtime occurrence provenance$");
        heading.Success.Should().BeTrue("Task 6.2 must add one stable numbered failure-provenance requirement");
        Regex.Matches(numbered, $@"(?m)^### {Regex.Escape(requirementId)}(?:\s|$)")
            .Should().ContainSingle("the stable requirement ID must have exactly one owner");
        var nextHeading = numbered.IndexOf("\n##", heading.Index + heading.Length, StringComparison.Ordinal);
        var sectionEnd = nextHeading < 0 ? numbered.Length : nextHeading;
        var numberedBlock = numbered[heading.Index..sectionEnd];
        Sha256(Encoding.UTF8.GetBytes(numberedBlock)).Should().Be(
            "27f38ec13f066af5b2715172b11d2d0db7ad342d0649418e25fc6143a03f25d3",
            "the complete CR-014a block is immutable reviewed text, so appended contradictions or " +
            "unreviewed clause movement must fail rather than coexist with required fragments");
        numberedClauses.Should().OnlyContain(clause =>
            numberedBlock.Contains(clause, StringComparison.Ordinal));

        var taskLedger = NormalizeLineEndings(File.ReadAllText(Path.Combine(
            root,
            "openspec",
            "changes",
            "harmonize-downstream-capability-specs",
            "tasks.md")));
        const string task63Heading =
            "- [x] 6.3 Add acceptance criteria in `docs/specs/12-acceptance-criteria.md`";
        var task63Start = taskLedger.IndexOf(task63Heading, StringComparison.Ordinal);
        task63Start.Should().BeGreaterThanOrEqualTo(0);
        var task64Boundary = Regex.Match(
            taskLedger[task63Start..],
            @"(?m)^- \[[ xX]\] 6\.4 ",
            RegexOptions.CultureInvariant);
        task64Boundary.Success.Should().BeTrue(
            "Task 6.3 evidence must terminate at Task 6.4 regardless of the successor task's state");
        var task63End = task63Start + task64Boundary.Index;
        var task63Block = Regex.Replace(taskLedger[task63Start..task63End], @"\s+", " ");
        string[] task63Clauses =
        [
            "`quality-and-verification` executable-evidence requirement",
            "`structured-fiber-execution` ordering contract",
            "public-contract companion",
            "one owning join failure",
            "authored-branch and dynamic-item ordering keys",
            "non-negative item indexes",
            "creation-time attachment",
            "unchanged one-failure propagation",
            "ordered per-cause provenance",
            "rejection of unknown, missing, or malformed fixed-codec occurrence data",
            "must not reuse `AC-022`"
        ];
        task63Clauses.Should().OnlyContain(clause =>
            task63Block.Contains(clause, StringComparison.Ordinal));

        var canonicalPath = Path.Combine(root, "openspec", "specs", "quality-and-verification", "spec.md");
        var canonical = ReadRequirementBlocks(root, canonicalPath, isDelta: false)
            .Single(requirement => requirement.Requirement == authoritativeRequirement);
        var reshapePath = Path.Combine(
            root,
            "openspec",
            "changes",
            "reshape-developer-facing-interfaces",
            "specs",
            "quality-and-verification",
            "spec.md");
        var reshape = ReadRequirementBlocks(root, reshapePath, isDelta: true)
            .Single(requirement => requirement.Requirement == authoritativeRequirement);
        canonical.Block.Should().Be(
            reshape.Block,
            "the approved executable failure-provenance requirement must remain synchronized");
        var authoritativeBlock = Regex.Replace(canonical.Block, @"\s+", " ");
        string[] authoritativeClauses =
        [
            "failure provenance attaches at failure creation",
            "root/branch/item occurrence constructors are runtime-only",
            "one-failure propagation is unchanged",
            "multiple causes retain ordered individual provenance",
            "closed `root`/`branch`/`item` discriminator allowlist round-trips through `orcacore-json-v1`"
        ];
        authoritativeClauses.Should().OnlyContain(clause =>
            authoritativeBlock.Contains(clause, StringComparison.Ordinal));

        var coreEvidencePath = Path.Combine(
            root,
            "tests",
            "OrcaCore.Core.Tests",
            "Execution",
            "FailureProvenanceTests.cs");
        var coreEvidence = NormalizeLineEndings(File.ReadAllText(coreEvidencePath));
        coreEvidence.Should().Contain($"[Trait(\"Requirement\", \"{requirementId}\")]");
        coreEvidence.Should().NotContain(
            "[Trait(\"AC\", \"AC-022\")]",
            "failure provenance must not claim the structural-fingerprint acceptance criterion");
        var fingerprintEvidence = NormalizeLineEndings(File.ReadAllText(Path.Combine(
            root,
            "tests",
            "OrcaCore.Core.Tests",
            "Compilation",
            "PublicDefinitionCompilerContractTests.cs")));
        string[] fingerprintMembers =
        [
            "Fingerprint_IsDeterministicAndChangesWithStructureAndOutcome",
            "Fingerprint_IgnoresCapturedOpaqueSelectorConfiguration"
        ];
        foreach (var member in fingerprintMembers)
        {
            var declaration = Regex.Match(
                fingerprintEvidence,
                $@"(?ms)(?<attributes>(?:^[ \t]*\[[^\n]+\][ \t]*\n)+)[ \t]*public[ \t]+void[ \t]+{Regex.Escape(member)}[ \t]*\(",
                RegexOptions.CultureInvariant);
            declaration.Success.Should().BeTrue("{0} must remain discovered AC-022 evidence", member);
            declaration.Groups["attributes"].Value.Should().Contain("[Trait(\"AC\", \"AC-022\")]");
        }

        string[] coreMembers =
        [
            "FailureOccurrence_IsTheExactClosedRuntimeCreatedUnion",
            "DetachedFailureGraph_PreservesProvenanceByValueWithoutSharingReferences",
            "AggregateFailures_PreservesSingleIdentityAndUsesOwningProvenanceForMany",
            "FixedCodec_RoundTripsTheVersionedClosedOccurrenceAllowlist",
            "FixedCodec_RejectsUnknownOrMalformedOccurrence"
        ];
        foreach (var member in coreMembers)
        {
            var declaration = Regex.Match(
                coreEvidence,
                $@"(?ms)(?<attributes>(?:^[ \t]*\[[^\n]+\][ \t]*\n)+)[ \t]*public[ \t]+void[ \t]+{Regex.Escape(member)}[ \t]*\(",
                RegexOptions.CultureInvariant);
            declaration.Success.Should().BeTrue("{0} must remain discovered requirement evidence", member);
            var attributes = declaration.Groups["attributes"].Value;
            attributes.Should().MatchRegex(@"(?m)^[ \t]*\[(?:Fact|Theory)(?:\]|\()", "{0} must be executable by xUnit", member);
            attributes.Should().NotContain("Skip", "{0} must not be disabled", member);
        }

        (string Path, string Member, string[] Fragments)[] runtimeEvidence =
        [
            (
                "tests/OrcaCore.Engine.Ephemeral.Tests/Execution/StructuredFiberExecutionPublicTests.cs",
                "SelectedParallel_WhenAllOutcomesMergesOrderedSuccessAndFailureData",
                [
                    "workflow:$/n:00000001/parallel:00000000/n:00000000",
                    "FailureOccurrence.Branch"
                ]),
            (
                "tests/OrcaCore.Engine.Durable.Tests/Driver/DurableFailureProvenanceTests.cs",
                "SelectedParallel_WhenAllOutcomesPreservesOrderedFailureProvenance",
                [
                    "failing:failure:WF-LEGACY-LIFECYCLE",
                    "workflow:$/n:00000001/parallel:00000000/n:00000000",
                    "FailureOccurrence.Branch"
                ])
        ];
        foreach (var evidence in runtimeEvidence)
        {
            var source = NormalizeLineEndings(File.ReadAllText(Path.Combine(
                root,
                evidence.Path.Replace('/', Path.DirectorySeparatorChar))));
            var projectPath = evidence.Path.Contains("Engine.Ephemeral.Tests", StringComparison.Ordinal)
                ? "tests/OrcaCore.Engine.Ephemeral.Tests/OrcaCore.Engine.Ephemeral.Tests.csproj"
                : "tests/OrcaCore.Engine.Durable.Tests/OrcaCore.Engine.Durable.Tests.csproj";
            var project = NormalizeLineEndings(File.ReadAllText(Path.Combine(
                root,
                projectPath.Replace('/', Path.DirectorySeparatorChar))));
            var projectDirectory = projectPath[..projectPath.LastIndexOf('/')];
            var compilePath = evidence.Path[(projectDirectory.Length + 1)..].Replace('/', '\\');
            project.Should().NotContain(
                $"<Compile Remove=\"{compilePath}\"",
                "{0} must be compile-included rather than source-only evidence",
                evidence.Path);
            var declaration = Regex.Match(
                source,
                $@"(?ms)(?<attributes>(?:^[ \t]*\[[^\n]+\][ \t]*\n)+)[ \t]*public[ \t]+(?:async[ \t]+Task|void)[ \t]+{Regex.Escape(evidence.Member)}[ \t]*\(",
                RegexOptions.CultureInvariant);
            declaration.Success.Should().BeTrue("{0} must remain discovered runtime evidence", evidence.Member);
            var attributes = declaration.Groups["attributes"].Value;
            attributes.Should().Contain($"[Trait(\"Requirement\", \"{requirementId}\")]");
            attributes.Should().MatchRegex(@"(?m)^[ \t]*\[Fact\]", "{0} must be executable by xUnit", evidence.Member);
            attributes.Should().NotContain("Skip", "{0} must not be disabled", evidence.Member);
            evidence.Fragments.Should().OnlyContain(fragment =>
                source.Contains(fragment, StringComparison.Ordinal));
        }

        var ci = NormalizeLineEndings(File.ReadAllText(Path.Combine(
            root,
            ".github",
            "workflows",
            "ci.yml")));
        var provenanceSteps = ReadCiWorkflowSteps(ci)
            .Where(step => Regex.IsMatch(
                step.Value,
                @"(?m)^[ \t]*-[ \t]+name:[ \t]+Failure provenance requirement evidence[ \t]*$",
                RegexOptions.CultureInvariant))
            .ToArray();
        provenanceSteps.Should().ContainSingle(
            "CI must contain one dedicated failure-provenance requirement evidence step");
        var provenanceCommand = NormalizeCiWorkflowStep(provenanceSteps[0]).Trim();
        provenanceCommand.Should().Be(
            "- name: Failure provenance requirement evidence run: > dotnet test " +
            "tests/OrcaCore.Core.Tests/OrcaCore.Core.Tests.csproj --no-build --no-restore " +
            "-c Release --filter \"Requirement=CR-014a\" --logger trx --results-directory TestResults && " +
            "dotnet test tests/OrcaCore.Engine.Ephemeral.Tests/OrcaCore.Engine.Ephemeral.Tests.csproj " +
            "--no-build --no-restore -c Release --filter \"Requirement=CR-014a\" --logger trx " +
            "--results-directory TestResults && dotnet test " +
            "tests/OrcaCore.Engine.Durable.Tests/OrcaCore.Engine.Durable.Tests.csproj --no-build " +
            "--no-restore -c Release --filter \"Requirement=CR-014a\" --logger trx " +
            "--results-directory TestResults");
    }

    [Fact]
    public void Task63_AcceptanceCriteriaMapLifecycleAndFailureProvenanceBidirectionally()
    {
        var root = FixtureDefinitions.RepositoryRoot();
        var requirements = NormalizeLineEndings(File.ReadAllText(Path.Combine(
            root, "docs", "specs", "04-requirements-core-runtime.md")));
        var acceptance = NormalizeLineEndings(File.ReadAllText(Path.Combine(
            root, "docs", "specs", "12-acceptance-criteria.md")));

        var lifecycleRequirement = ReadNumberedRequirementBlock(
            requirements, "CR-009a Authoring sessions have one explicit lifecycle");
        var failureRequirement = ReadNumberedRequirementBlock(
            requirements, "CR-014a Workflow failures retain authored and runtime occurrence provenance");
        Sha256(Encoding.UTF8.GetBytes(lifecycleRequirement)).Should().Be(
            "085fb9753a97838f8798f5443c1b6e444eda392ef8a06f3c9531cd56726856f8",
            "the complete CR-009a block is immutable reviewed text, so appended contradictions or " +
            "unreviewed clause movement must fail rather than coexist with the acceptance backlink");
        lifecycleRequirement.Should().Contain("Acceptance criterion: `AC-028`.");
        failureRequirement.Should().Contain("Acceptance criterion: `AC-029`.");
        lifecycleRequirement.Should().NotContain("AC-029");
        failureRequirement.Should().NotContain("AC-028");
        failureRequirement.Should().NotContain("AC-022");

        var lifecycleCriterion = ReadAcceptanceCriterionBlock(acceptance, "AC-028");
        var failureCriterion = ReadAcceptanceCriterionBlock(acceptance, "AC-029");
        Regex.Replace(lifecycleCriterion, @"\s+", " ").Should().ContainAll(
            "`Open` to `JoinPending`",
            "successor `Open` epoch",
            "root terminal freezes one immutable snapshot",
            "before graph mutation",
            "[CR-009a]");
        Regex.Replace(failureCriterion, @"\s+", " ").Should().ContainAll(
            "runtime-created occurrence at creation",
            "One-failure propagation preserves the failure unchanged",
            "one owning failure",
            "authored branch order",
            "dynamic item index order",
            "non-negative item indexes",
            "rejects unknown versions or discriminators",
            "missing variant data",
            "malformed payloads rather than coercing them",
            "[CR-014a]",
            "`quality-and-verification` executable evidence",
            "`structured-fiber-execution` join ordering",
            "public-contract companion");
        RequireMarkdownLinkTarget(
            root,
            "docs/specs/12-acceptance-criteria.md",
            failureCriterion,
            "`quality-and-verification` executable evidence",
            "../../openspec/specs/quality-and-verification/spec.md",
            "requirement-authoring-lifecycle-fingerprint-coverage-and-failure-provenance-are-executable",
            "### Requirement: Authoring lifecycle, fingerprint coverage, and failure provenance are executable");
        RequireMarkdownLinkTarget(
            root,
            "docs/specs/12-acceptance-criteria.md",
            failureCriterion,
            "`structured-fiber-execution` join ordering",
            "../../openspec/specs/structured-fiber-execution/spec.md",
            "requirement-join-policies-define-one-scope-outcome",
            "### Requirement: Join policies define one scope outcome");
        RequireMarkdownLinkTarget(
            root,
            "docs/specs/12-acceptance-criteria.md",
            failureCriterion,
            "public-contract companion",
            "17-selected-mode-capability-matrix.md",
            expectedAnchor: null,
            expectedHeading: null);
        lifecycleCriterion.Should().NotContain("CR-014a");
        failureCriterion.Should().NotContain("CR-009a");
        failureCriterion.Should().NotContain("AC-022");

        var lifecycleEvidence = NormalizeLineEndings(File.ReadAllText(Path.Combine(
            root, "tests", "OrcaCore.Core.Tests", "Building", "AuthoringLifecycleTests.cs")));
        var lifecycleClass = Regex.Match(
            lifecycleEvidence,
            @"(?ms)(?<attributes>(?:^[ \t]*\[[^\n]+\][ \t]*\n)+)[ \t]*public[ \t]+sealed[ \t]+class[ \t]+AuthoringLifecycleTests(?:\s|$)",
            RegexOptions.CultureInvariant);
        lifecycleClass.Success.Should().BeTrue("AuthoringLifecycleTests must remain discovered AC-028 evidence");
        lifecycleClass.Groups["attributes"].Value.Should().ContainAll(
            "[Trait(\"Requirement\", \"CR-009a\")]",
            "[Trait(\"AC\", \"AC-028\")]");
        var coreFailureEvidence = NormalizeLineEndings(File.ReadAllText(Path.Combine(
            root, "tests", "OrcaCore.Core.Tests", "Execution", "FailureProvenanceTests.cs")));
        var failureClass = Regex.Match(
            coreFailureEvidence,
            @"(?ms)(?<attributes>(?:^[ \t]*\[[^\n]+\][ \t]*\n)+)[ \t]*public[ \t]+sealed[ \t]+class[ \t]+FailureProvenanceTests(?:\s|$)",
            RegexOptions.CultureInvariant);
        failureClass.Success.Should().BeTrue("FailureProvenanceTests must remain discovered AC-029 evidence");
        failureClass.Groups["attributes"].Value.Should().ContainAll(
            "[Trait(\"Requirement\", \"CR-014a\")]",
            "[Trait(\"AC\", \"AC-029\")]");
        coreFailureEvidence.Should().NotContain("[Trait(\"AC\", \"AC-022\")]");

        (string Path, string Member)[] runtimeEvidence =
        [
            ("tests/OrcaCore.Engine.Ephemeral.Tests/Execution/StructuredFiberExecutionPublicTests.cs",
                "SelectedParallel_WhenAllOutcomesMergesOrderedSuccessAndFailureData"),
            ("tests/OrcaCore.Engine.Durable.Tests/Driver/DurableFailureProvenanceTests.cs",
                "SelectedParallel_WhenAllOutcomesPreservesOrderedFailureProvenance")
        ];
        foreach (var evidence in runtimeEvidence)
        {
            var source = NormalizeLineEndings(File.ReadAllText(Path.Combine(
                root, evidence.Path.Replace('/', Path.DirectorySeparatorChar))));
            var declaration = Regex.Match(
                source,
                $@"(?ms)(?<attributes>(?:^[ \t]*\[[^\n]+\][ \t]*\n)+)[ \t]*public[ \t]+async[ \t]+Task[ \t]+{Regex.Escape(evidence.Member)}[ \t]*\(",
                RegexOptions.CultureInvariant);
            declaration.Success.Should().BeTrue("{0} must remain executable AC-029 evidence", evidence.Member);
            declaration.Groups["attributes"].Value.Should().ContainAll(
                "[Trait(\"Requirement\", \"CR-014a\")]",
                "[Trait(\"AC\", \"AC-029\")]");
        }

        var repositoryGuard = NormalizeLineEndings(File.ReadAllText(Path.Combine(
            root, "tests", "OrcaCore.Core.Tests", "RepositoryGuardTests.cs")));
        repositoryGuard.Should().Contain(
            "AcceptanceCriterionCatalog_HasTraitCoverageOrExplicitWaiver");
        repositoryGuard.Should().Contain(
            "AcceptanceCriterionWaivers_AreCatalogedReasonedAndNotAlreadyCovered");

        var ci = NormalizeLineEndings(File.ReadAllText(Path.Combine(root, ".github", "workflows", "ci.yml")));
        var acceptanceSteps = ReadCiWorkflowSteps(ci)
            .Where(step => Regex.IsMatch(
                step.Value,
                @"(?m)^[ \t]*-[ \t]+name:[ \t]+Acceptance harmonization evidence[ \t]*$",
                RegexOptions.CultureInvariant))
            .ToArray();
        acceptanceSteps.Should().ContainSingle();
        NormalizeCiWorkflowStep(acceptanceSteps[0]).Trim().Should().Be(
            "- name: Acceptance harmonization evidence run: > dotnet test " +
            "tests/OrcaCore.Core.Tests/OrcaCore.Core.Tests.csproj --no-build --no-restore " +
            "-c Release --filter \"AC=AC-028|AC=AC-029\" --logger trx --results-directory " +
            "TestResults && dotnet test tests/OrcaCore.Engine.Ephemeral.Tests/" +
            "OrcaCore.Engine.Ephemeral.Tests.csproj --no-build --no-restore -c Release " +
            "--filter \"AC=AC-029\" --logger trx --results-directory TestResults && dotnet test " +
            "tests/OrcaCore.Engine.Durable.Tests/OrcaCore.Engine.Durable.Tests.csproj --no-build " +
            "--no-restore -c Release --filter \"AC=AC-029\" --logger trx --results-directory TestResults");
    }

    [Fact]
    public void Task64_MaxActiveFibersMentionsAreHistoricalOrExplicitlyNegative()
    {
        var root = FixtureDefinitions.RepositoryRoot();
        var productMentions = Directory
            .EnumerateFiles(Path.Combine(root, "src"), "*.cs", SearchOption.AllDirectories)
            .Where(path => !IsGeneratedBuildPath(path))
            .Where(path => File.ReadAllText(path).Contains(RetiredMaxActiveFibersName, StringComparison.Ordinal))
            .Select(path => Path.GetRelativePath(root, path).Replace('\\', '/'))
            .Order(StringComparer.Ordinal)
            .ToArray();
        productMentions.Should().BeEmpty(
            "the retired live-fiber quantity must not return as a current product-source claim");

        var activeDocuments = new[] { Path.Combine(root, "docs"), Path.Combine(root, "openspec") }
            .SelectMany(directory => Directory.EnumerateFiles(directory, "*", SearchOption.AllDirectories))
            .Where(path => ActiveDocumentationExtensions.Contains(
                Path.GetExtension(path),
                StringComparer.OrdinalIgnoreCase))
            .Select(path => new
            {
                Path = Path.GetRelativePath(root, path).Replace('\\', '/'),
                Content = NormalizeLineEndings(File.ReadAllText(path))
            })
            .Where(document => !IsImmutableDocumentationPath(document.Path))
            .Where(document => document.Content.Contains(RetiredMaxActiveFibersName, StringComparison.Ordinal))
            .Select(document =>
                $"{document.Path}\t{Regex.Matches(document.Content, Regex.Escape(RetiredMaxActiveFibersName), RegexOptions.CultureInvariant).Count}")
            .Order(StringComparer.Ordinal)
            .ToArray();
        activeDocuments.Should().Equal(
        [
            "docs/specs/18-semantic-appendix.md\t1",
            "openspec/changes/harmonize-downstream-capability-specs/artifacts/task-6-4-max-active-fibers-disposition-2026-09-01.md\t4",
            "openspec/changes/harmonize-downstream-capability-specs/artifacts/task-6-4-rejection-remediation-2026-09-02.md\t1",
            "openspec/changes/harmonize-downstream-capability-specs/tasks.md\t1",
            "openspec/changes/reshape-developer-facing-interfaces/AMENDMENT-2026-07-28-root-only-fanout-and-authoring-lifecycle.md\t13",
            "openspec/changes/reshape-developer-facing-interfaces/design.md\t1",
            "openspec/changes/reshape-developer-facing-interfaces/proposal.md\t1",
            "openspec/changes/reshape-developer-facing-interfaces/tasks.md\t1"
        ],
            "every non-review mention must remain in its reviewed negative or dated-history owner");

        var semanticAppendix = NormalizeLineEndings(File.ReadAllText(Path.Combine(
            root, "docs", "specs", "18-semantic-appendix.md")));
        var excludedClaimsStart = semanticAppendix.IndexOf(
            DeliberatelyExcludedClaimsHeading, StringComparison.Ordinal);
        excludedClaimsStart.Should().BeGreaterThanOrEqualTo(0);
        semanticAppendix[excludedClaimsStart..].Should().Contain(
            "`MaxActiveFibers` or another third live-fiber admission quantity exists in the current implementation.\n" +
            "  Task 5.13 removed that quantity",
            "the former current-source claim must be retained only as an explicitly excluded claim");

        var excludedClaimsEnd = semanticAppendix.IndexOf(
            "\n## ",
            excludedClaimsStart + DeliberatelyExcludedClaimsHeading.Length,
            StringComparison.Ordinal);
        var excludedClaimsBlock = semanticAppendix[
            excludedClaimsStart..(excludedClaimsEnd < 0 ? semanticAppendix.Length : excludedClaimsEnd)];
        Sha256(Encoding.UTF8.GetBytes(excludedClaimsBlock)).Should().Be(
            DeliberatelyExcludedClaimsSha256,
            "the complete deliberately-excluded claim set must remain immutable, including the independent fan-out-rank exclusion");

        var semanticAppendixSourcePath = Path.Combine(
            root, "openspec", "changes", "reshape-developer-facing-interfaces", "artifacts",
            "semantic-appendix.md");
        var semanticAppendixSourceBytes = File.ReadAllBytes(semanticAppendixSourcePath);
        Sha256(semanticAppendixSourceBytes).Should().Be(
            SemanticAppendixSourceSha256,
            "the immutable semantic-appendix source must not drift coherently with its published projection");
        var semanticAppendixSource = NormalizeLineEndings(
            Encoding.UTF8.GetString(semanticAppendixSourceBytes));
        BuildPublishedSemanticAppendix(semanticAppendixSource).Should().Be(
            semanticAppendix,
            "the complete canonical appendix must remain the exact published projection of its immutable source artifact");

        var reshapeProposal = NormalizeLineEndings(File.ReadAllText(Path.Combine(
            root, "openspec", "changes", "reshape-developer-facing-interfaces", "proposal.md")));
        reshapeProposal.Should().Contain("Remove implementation-only `MaxActiveFibers`");
        var reshapeDesign = NormalizeLineEndings(File.ReadAllText(Path.Combine(
            root, "openspec", "changes", "reshape-developer-facing-interfaces", "design.md")));
        reshapeDesign.Should().Contain("`MaxActiveFibers` quantity has no normative owner and is removed");
        var reshapeTasks = NormalizeLineEndings(File.ReadAllText(Path.Combine(
            root, "openspec", "changes", "reshape-developer-facing-interfaces", "tasks.md")));
        reshapeTasks.Should().Contain("- [x] 5.13 Remove `MaxActiveFibers`");

        var datedAmendment = NormalizeLineEndings(File.ReadAllText(Path.Combine(
            root, "openspec", "changes", "reshape-developer-facing-interfaces",
            "AMENDMENT-2026-07-28-root-only-fanout-and-authoring-lifecycle.md")));
        datedAmendment.Should().StartWith("# Amendment 2026-07-28");
        datedAmendment.Should().Contain("## Revision history");

        var harmonizationTasks = NormalizeLineEndings(File.ReadAllText(Path.Combine(
            root, HarmonizationTaskLedgerPath.Replace('/', Path.DirectorySeparatorChar))));
        var task = Regex.Match(
            harmonizationTasks,
            @"(?ms)^- \[x\] 6\.4 .*?(?=^- \[[ xX]\] 6\.5 )",
            RegexOptions.CultureInvariant);
        task.Success.Should().BeTrue("Task 6.4 must remain completed with its explicit disposition");
        task.Value.Should().Contain(RetiredMaxActiveFibersName);
        task.Value.Should().Contain("**Completed:**");
    }

    [Fact]
    public void Task65_PublicAuthoringCompanionRemainsUnchangedAndLifecycleInternalsStayNonPublic()
    {
        var root = FixtureDefinitions.RepositoryRoot();
        var contract = FixtureDefinitions.Read<V1PublicContract>(
            "tests/OrcaCore.DeveloperSurface.Guards/Fixtures/v1-public-contract.json");
        var companionBytes = File.ReadAllBytes(Path.Combine(
            root, "docs", "specs", "17-public-authoring-contract.cs"));
        contract.CompanionSha256.ToLowerInvariant().Should().Be(
            PublicAuthoringCompanionSha256,
            "the mutable contract fixture must reproduce the source-owned reviewed companion pin");
        Sha256(companionBytes).Should().Be(
            PublicAuthoringCompanionSha256,
            "Task 6.5 deliberately preserves the already-reviewed compile-shaped public companion");

        var companion = NormalizeLineEndings(Encoding.UTF8.GetString(companionBytes));
        companion.Should().NotContainAny(
            AuthoringLifecycleImplementationTypeNames,
            "authoring-session state, handles, and tokens are implementation details rather than public declarations");

        var lifecycleSources = NormalizeLineEndings(File.ReadAllText(Path.Combine(
                root, "src", "OrcaCore.Core", "Building", "AuthoringLifecycle.cs"))) +
            NormalizeLineEndings(File.ReadAllText(Path.Combine(
                root, "src", "OrcaCore.Core", "Building", "SelectedWorkflowBuilder.cs")));
        lifecycleSources.Should().Contain("internal enum AuthoringSessionState");
        lifecycleSources.Should().Contain("internal sealed class AuthoringLifecycleSession");
        lifecycleSources.Should().Contain("internal abstract class WorkflowAuthoringSession");

        var approved = PublicApiBaseline.ReadApproved();
        approved["OrcaCore.Core"].Should().Be(
            EmptyCorePublicApiBaseline,
            "the assembly that owns authoring-session internals intentionally exports no public API");
        var actual = PublicApiBaseline.CaptureCurrent();
        PublicApiBaseline.Diff(approved, actual).Should().BeEmpty(
            "the exhaustive twelve-assembly baseline must independently reject any leaked lifecycle type");
        var completePublicSurface = string.Join('\n', actual.Values);
        completePublicSurface.Should().NotContainAny(AuthoringLifecycleImplementationTypeNames);

        var taskLedger = NormalizeLineEndings(File.ReadAllText(Path.Combine(
            root, HarmonizationTaskLedgerPath.Replace('/', Path.DirectorySeparatorChar))));
        var task = Regex.Match(
            taskLedger,
            @"(?ms)^- \[x\] 6\.5 .*?(?=^- \[[ xX]\] 6\.6 )",
            RegexOptions.CultureInvariant);
        task.Success.Should().BeTrue("Task 6.5 must retain its explicit reviewed decision");
        task.Value.Should().Contain("**Completed:**");
        task.Value.Should().Contain("deliberately byte-unchanged");
        task.Value.Should().Contain("exhaustive twelve-assembly public API baseline");
        task.Value.Should().Contain(Task65PostReviewHardeningDecision);
        task.Value.Should().Contain(Task65SecondReviewHardeningDecision);

        var design = NormalizeLineEndings(File.ReadAllText(Path.Combine(
            root, HarmonizationDesignPath.Replace('/', Path.DirectorySeparatorChar))));
        design.Should().Contain(
            Task65CompleteDesignDecision,
            "Task 6.5's complete design decision must survive active-freeze archival");
    }

    [Fact]
    public void Task66_FutureCapabilityRegistryUsesOneCrossTreeNameAndSeparatesRemovedConcepts()
    {
        var root = FixtureDefinitions.RepositoryRoot();
        var registry = NormalizeLineEndings(File.ReadAllText(Path.Combine(
            root, FutureCapabilityRegistryDocumentPath.Replace('/', Path.DirectorySeparatorChar))));
        var registrySection = ReadExactMarkdownSection(registry, FutureCapabilityRegistryHeading);
        var deferredMarker = $"\n{DeferredCapabilitiesHeading}\n";
        var removedMarker = $"\n{RemovedConceptsHeading}\n";
        var deferredStart = registrySection.IndexOf(deferredMarker, StringComparison.Ordinal);
        var removedStart = registrySection.IndexOf(removedMarker, StringComparison.Ordinal);
        deferredStart.Should().BeGreaterThanOrEqualTo(
            0,
            "the future-capability registry must expose a named deferred-capability subsection");
        removedStart.Should().BeGreaterThan(
            deferredStart,
            "removed concepts must remain searchable without being classified as deferred work");

        var nextSubsectionStart = registrySection.IndexOf(
            "\n### ",
            removedStart + removedMarker.Length,
            StringComparison.Ordinal);
        var removedEnd = nextSubsectionStart < 0 ? registrySection.Length : nextSubsectionStart;
        var deferredSection = registrySection[deferredStart..removedStart];
        var removedSection = registrySection[removedStart..removedEnd];
        var nonRemovedSection = registrySection[..removedStart] + registrySection[removedEnd..];
        deferredSection.Should().Contain(FutureCapabilityRegistryTableHeading);
        foreach (var removedConcept in RemovedConceptRegistryNames)
        {
            ContainsIdentifierToken(nonRemovedSection, removedConcept).Should().BeFalse(
                $"removed concept {removedConcept} must not be a future promise outside the exact removed subsection");
            ContainsIdentifierToken(removedSection, removedConcept).Should().BeTrue(
                $"removed concept {removedConcept} must remain explicitly searchable");
        }

        foreach (var binding in FutureCapabilityRegistryRequirementBindings)
        {
            var canonicalPath = Path.Combine(root, binding.CanonicalPath.Replace('/', Path.DirectorySeparatorChar));
            var deltaPath = Path.Combine(root, binding.DeltaPath.Replace('/', Path.DirectorySeparatorChar));
            var canonical = ReadRequirementBlocks(root, canonicalPath, isDelta: false)
                .Single(requirement => requirement.Requirement == binding.Requirement);
            var delta = ReadRequirementBlocks(root, deltaPath, isDelta: true)
                .Single(requirement => requirement.Requirement == binding.Requirement);
            canonical.Block.Should().Contain(
                FutureCapabilityRegistryCrossReference,
                $"canonical requirement {binding.Requirement} must resolve the registry path and exact name");
            delta.Block.Should().Be(
                canonical.Block,
                $"the active reshape delta still owns and must match canonical requirement {binding.Requirement}");
        }

        var activeDocumentation = Directory
            .EnumerateFiles(Path.Combine(root, "docs"), "*", SearchOption.AllDirectories)
            .Where(path => ActiveDocumentationExtensions.Contains(
                Path.GetExtension(path),
                StringComparer.OrdinalIgnoreCase))
            .Select(path => new
            {
                Path = RelativePath(root, path),
                Content = NormalizeLineEndings(File.ReadAllText(path))
            })
            .Where(document => !IsImmutableDocumentationPath(document.Path))
            .ToArray();
        foreach (var document in activeDocumentation)
        {
            document.Content.Should().NotContain(
                LegacyFutureCapabilityRegistryHeading,
                $"active document {document.Path} must use the exact cross-tree registry name");
            document.Content.Should().NotContain(
                LegacyFutureCapabilityRegistryAnchor,
                $"active document {document.Path} must not link the retired registry anchor");
        }

        foreach (var guidePath in FutureCapabilityRegistryGuidePaths)
        {
            activeDocumentation
                .Single(document => document.Path == guidePath)
                .Content.Should().Contain(
                    FutureCapabilityRegistryAnchor,
                    $"active guide {guidePath} must link the exact future-capability registry anchor");
        }

        var sourceMap = activeDocumentation
            .Single(document => document.Path == "docs/normative-source-map.md")
            .Content;
        sourceMap.Should().Contain(FutureCapabilityRegistrySourceMapTarget);
        sourceMap.Should().Contain(FutureCapabilityRegistrySectionIdentity);
        sourceMap.Should().NotContain(LegacyFutureCapabilityRegistryMismatchClaim);

        var taskLedger = NormalizeLineEndings(File.ReadAllText(Path.Combine(
            root, HarmonizationTaskLedgerPath.Replace('/', Path.DirectorySeparatorChar))));
        var task = Regex.Match(
            taskLedger,
            @"(?ms)^- \[x\] 6\.6 .*?(?=^## 7\.)",
            RegexOptions.CultureInvariant);
        task.Success.Should().BeTrue("Task 6.6 must retain its completed cross-tree disposition");
        task.Value.Should().Contain(Task66CompletionDecision);
        task.Value.Should().Contain(Task66ReviewCarryForwardDecision);
        task.Value.Should().Contain(Task66PostReviewRemediationDecision);
        task.Value.Should().Contain(Task66SecondPostReviewHardeningDecision);

        var design = NormalizeLineEndings(File.ReadAllText(Path.Combine(
            root, HarmonizationDesignPath.Replace('/', Path.DirectorySeparatorChar))));
        design.Should().Contain(
            Task66DesignDecision,
            "the one-name, split-classification decision must survive active-freeze archival");
        design.Should().Contain(
            Task66PostReviewDesignDecision,
            "the Task 6.6 review remediation decision must survive active-freeze archival");
        design.Should().Contain(
            Task66SecondPostReviewDesignDecision,
            "the Task 6.6 second post-review hardening decision must survive active-freeze archival");

        var remediationArtifact = NormalizeLineEndings(File.ReadAllText(Path.Combine(
            root,
            Task66PostReviewRemediationArtifact.Replace('/', Path.DirectorySeparatorChar))));
        Sha256(remediationArtifact).Should().Be(
            Task66PostReviewRemediationArtifactSha256,
            "the exact Task 6.6 approval and synchronization disclosure must remain immutable");

        var hardeningArtifact = NormalizeLineEndings(File.ReadAllText(Path.Combine(
            root,
            Task66SecondPostReviewHardeningArtifact.Replace('/', Path.DirectorySeparatorChar))));
        Sha256(hardeningArtifact).Should().Be(
            Task66SecondPostReviewHardeningArtifactSha256,
            "the Task 6.6 exact-byte correction and exhaustive hardening record must remain immutable");
    }

    [Fact]
    public void Task71_ActiveGuidesContainNoPositiveRemovedOrDeferredApiCalls()
    {
        var root = FixtureDefinitions.RepositoryRoot();
        var sourcePaths = EnumerateTask71ActiveCorpusSourcePaths(root);
        sourcePaths.Should().Contain(ActiveCorpusRootDocumentPaths);
        sourcePaths.Should().Contain(FutureCapabilityRegistryGuidePaths);
        sourcePaths.Should().Contain(path => path.StartsWith(CanonicalOpenSpecSpecsPathPrefix, StringComparison.Ordinal));
        sourcePaths.Should().Contain(path => path.StartsWith(OpenSpecChangesPathPrefix, StringComparison.Ordinal));

        var sourceRecord = File.ReadAllBytes(Path.Combine(
            root,
            Task71PositiveCallSourceRecord.Replace('/', Path.DirectorySeparatorChar)));
        sourceRecord.Should().HaveCount(Task71PositiveCallSourceRecordBytes);
        sourceRecord.Should().NotContain((byte)'\r');
        sourceRecord.Should().EndWith((byte)'\n');
        sourceRecord.Count(value => value == (byte)'\n').Should().Be(Task71PositiveCallSourceRecordRows);
        var sourceRecordPaths = Encoding.UTF8.GetString(sourceRecord)
            .Split('\n', StringSplitOptions.RemoveEmptyEntries)
            .Select(line => line[..line.IndexOf('\t')])
            .ToArray();
        sourceRecordPaths.Should().OnlyHaveUniqueItems();
        sourceRecordPaths.Should().Equal(
            sourceRecordPaths.Order(StringComparer.Ordinal),
            "the source record must use the documented ordinal path order");
        Sha256(sourceRecord).Should().Be(
            Task71PositiveCallSourceRecordSha256,
            "the exact Task 7.1 source-record rows must reproduce the published aggregate digest");

        var findings = sourcePaths
            .Select(path => new
            {
                Path = path,
                Content = NormalizeLineEndings(File.ReadAllText(Path.Combine(
                    root,
                    path.Replace('/', Path.DirectorySeparatorChar))))
            })
            .SelectMany(document => PositiveRemovedOrDeferredCallPatterns.SelectMany(pattern =>
                Regex.Matches(
                        document.Content,
                        pattern.Expression,
                        RegexOptions.CultureInvariant)
                    .Select(match =>
                        $"{document.Path}:{GetLineNumber(document.Content, match.Index)}:{pattern.Name}")))
            .Order(StringComparer.Ordinal)
            .ToArray();
        findings.Should().BeEmpty(
            "active guides and contract sources must not teach removed or deferred APIs as callable v1 members");

        foreach (var guidePath in FutureCapabilityRegistryGuidePaths)
        {
            var guide = NormalizeLineEndings(File.ReadAllText(Path.Combine(
                root,
                guidePath.Replace('/', Path.DirectorySeparatorChar))));
            guide.Should().Contain(
                FutureCapabilityRegistryAnchor,
                $"active guide {guidePath} must retain an exact re-entry link rather than a positive how-to");
        }

        var taskLedger = NormalizeLineEndings(File.ReadAllText(Path.Combine(
            root,
            HarmonizationTaskLedgerPath.Replace('/', Path.DirectorySeparatorChar))));
        var task = Regex.Match(
            taskLedger,
            @"(?ms)^- \[x\] 7\.1 .*?(?=^- \[[ xX]\] 7\.2 )",
            RegexOptions.CultureInvariant);
        task.Success.Should().BeTrue("Task 7.1 must retain its completed zero-finding disposition");
        task.Value.Should().Contain(Task71CompletionDecision);
        task.Value.Should().Contain(Task71ReviewRemediationDecision);

        var design = NormalizeLineEndings(File.ReadAllText(Path.Combine(
            root,
            HarmonizationDesignPath.Replace('/', Path.DirectorySeparatorChar))));
        design.Should().Contain(
            Task71DesignDecision,
            "Task 7.1's active-guide and carried review-hardening decision must survive archival");
        design.Should().Contain(
            Task71ReviewRemediationDesignDecision,
            "Task 7.1's rejected-freeze and ordinal-source-record remediation must survive archival");

        var artifact = NormalizeLineEndings(File.ReadAllText(Path.Combine(
            root,
            Task71PositiveCallScanArtifact.Replace('/', Path.DirectorySeparatorChar))));
        Sha256(artifact).Should().Be(
            Task71PositiveCallScanArtifactSha256,
            "the dated Task 7.1 zero-finding rerun must remain immutable");
        artifact.Should().Contain(
            $"`{Task71PositiveCallSourceRecord}`",
            "the dated scan must name the exact companion record it publishes");
    }

    [Fact]
    public void Task72_ArchivedAndReviewRecordsAreImmutableAndNewRecordsAreClassified()
    {
        var root = FixtureDefinitions.RepositoryRoot();
        var fixture = FixtureDefinitions.Read<ImmutableDocumentHistoryFixture>(
            ImmutableDocumentHistoryFixturePath);
        fixture.SchemaVersion.Should().Be(3);
        fixture.RecordFormat.Should().Be(ImmutableDocumentHistoryRecordFormat);
        fixture.Normalization.Should().Be(ImmutableDocumentHistoryNormalization);
        fixture.BaselineCommit.Should().Be(
            ImmutableDocumentHistoryBaselineCommit,
            "the immutable baseline must remain tied to the reviewed Task 7.2 base");
        fixture.MutablePaths
            .Select(entry => (entry.Path, entry.Purpose))
            .Should().Equal(MutableHistoricalDocumentPaths,
                "only the exact active archive index and reusable review template may remain mutable");
        fixture.MutablePaths.Select(entry => entry.Path).Should().OnlyHaveUniqueItems();
        fixture.ActiveFreezeManifestPath.Should().StartWith(
            ImmutableDocumentationPrefixes[1],
            "post-baseline admission must work for any review family through a review-root manifest");

        fixture.BaselineCount.Should().Be(ImmutableDocumentHistoryBaselineCount);
        fixture.Records.Should().HaveCount(fixture.BaselineCount);
        fixture.Records.Select(entry => entry.Path).Should().OnlyHaveUniqueItems();
        fixture.Records.Select(entry => entry.Path).Should().Equal(
            fixture.Records.Select(entry => entry.Path).Order(StringComparer.Ordinal),
            "immutable baseline records must remain in ordinal path order");
        fixture.Records.Should().OnlyContain(entry =>
            ImmutableDocumentationPrefixes.Any(prefix =>
                entry.Path.StartsWith(prefix, StringComparison.Ordinal)));
        fixture.Records.Select(entry => entry.Path).Should().NotIntersectWith(
            fixture.MutablePaths.Select(entry => entry.Path));
        fixture.AppendOnlyRecords.Select(entry => entry.Path).Should().OnlyHaveUniqueItems();
        fixture.AppendOnlyRecords.Select(entry => entry.Path).Should().Equal(
            fixture.AppendOnlyRecords.Select(entry => entry.Path).Order(StringComparer.Ordinal),
            "append-only post-baseline records must remain in ordinal path order");
        fixture.AppendOnlyRecords.Should().OnlyContain(entry =>
            ImmutableDocumentationPrefixes.Any(prefix =>
                entry.Path.StartsWith(prefix, StringComparison.Ordinal)));
        fixture.AppendOnlyRecords.Select(entry => entry.Path).Should().NotIntersectWith(
            fixture.Records.Select(entry => entry.Path));
        fixture.AppendOnlyRecords.Select(entry => entry.Path).Should().NotIntersectWith(
            fixture.MutablePaths.Select(entry => entry.Path));

        var record = string.Join(
            '\n',
            fixture.Records.Select(entry => $"{entry.Path}\t{entry.Bytes}\t{entry.Sha256}")) + "\n";
        var recordBytes = Encoding.UTF8.GetBytes(record);
        recordBytes.Should().HaveCount(fixture.BaselineBytes);
        recordBytes.Should().HaveCount(ImmutableDocumentHistoryBaselineBytes);
        Sha256(recordBytes).Should().Be(fixture.BaselineSha256.ToLowerInvariant());
        Sha256(recordBytes).Should().Be(
            ImmutableDocumentHistoryBaselineSha256,
            "fixture-only rehashing must not rewrite the immutable Task 7.2 baseline");

        var committedBaselinePaths = ReadHistoricalDocumentPaths(root, fixture.BaselineCommit);
        fixture.Records.Select(entry => entry.Path).Should().Equal(
            committedBaselinePaths,
            "the baseline must enumerate the exact historical files committed at the reviewed base");

        foreach (var entry in fixture.Records)
        {
            var committedBytes = NormalizeHistoricalDocumentBytes(ReadGitBlob(
                root,
                fixture.BaselineCommit,
                entry.Path));
            committedBytes.Should().HaveCount(
                entry.Bytes,
                $"{entry.Path} must retain its LF-normalized committed baseline length");
            Sha256(committedBytes).Should().Be(
                entry.Sha256.ToLowerInvariant(),
                $"{entry.Path} must reproduce from the committed Task 7.2 baseline");

            var worktreeBytes = NormalizeHistoricalDocumentBytes(File.ReadAllBytes(Path.Combine(
                root,
                entry.Path.Replace('/', Path.DirectorySeparatorChar))));
            worktreeBytes.Should().HaveCount(
                entry.Bytes,
                $"{entry.Path} must retain its checkout-independent normalized length");
            Sha256(worktreeBytes).Should().Be(
                entry.Sha256.ToLowerInvariant(),
                $"{entry.Path} is immutable; line-ending transforms are ignored, content changes are not");
        }

        ValidateHistoricalAdditionSemantics();
        ValidateAppendOnlyHistoricalRecords(root, fixture);

        var discoveredPaths = ImmutableDocumentationPrefixes
            .SelectMany(prefix => Directory.EnumerateFiles(
                Path.Combine(root, prefix.TrimEnd('/').Replace('/', Path.DirectorySeparatorChar)),
                "*",
                SearchOption.AllDirectories))
            .Select(path => RelativePath(root, path))
            .Order(StringComparer.Ordinal)
            .ToArray();
        var classifiedPaths = fixture.Records.Select(entry => entry.Path)
            .Concat(fixture.AppendOnlyRecords.Select(entry => entry.Path))
            .Concat(fixture.MutablePaths.Select(entry => entry.Path))
            .Distinct(StringComparer.Ordinal)
            .Order(StringComparer.Ordinal)
            .ToArray();
        classifiedPaths.Should().Equal(
            discoveredPaths,
            "every historical document must be baseline, a universal append-only post-baseline record, or one exact mutable surface");

        fixture.MutablePaths.Should().OnlyContain(entry => File.Exists(Path.Combine(
            root,
            entry.Path.Replace('/', Path.DirectorySeparatorChar))));

        var taskLedger = NormalizeLineEndings(File.ReadAllText(Path.Combine(
            root,
            HarmonizationTaskLedgerPath.Replace('/', Path.DirectorySeparatorChar))));
        var task = Regex.Match(
            taskLedger,
            @"(?ms)^- \[x\] 7\.2 .*?(?=^- \[[ xX]\] 7\.3 )",
            RegexOptions.CultureInvariant);
        task.Success.Should().BeTrue("Task 7.2 must retain its completed immutable-history decision");
        task.Value.Should().Contain(Task72CompletionDecision);
        taskLedger.Should().Contain(
            Task77ArchiveProvenanceDecision,
            "Task 7.7 must not authorize relocation before a reviewed tombstone mechanism exists");

        var design = NormalizeLineEndings(File.ReadAllText(Path.Combine(
            root,
            HarmonizationDesignPath.Replace('/', Path.DirectorySeparatorChar))));
        design.Should().Contain(
            Task72DesignDecision,
            "Task 7.2's immutable-history classification decision must survive archival");
        design.Should().Contain(
            Task72ValidationRemediationDecision,
            "Task 7.2's validation-detected terminal-signal correction must survive archival");

        var archiveIndexPath = fixture.MutablePaths.Single(entry =>
            entry.Purpose == "active archive index").Path;
        var archiveIndex = NormalizeLineEndings(File.ReadAllText(Path.Combine(
            root,
            archiveIndexPath.Replace('/', Path.DirectorySeparatorChar))));
        archiveIndex.Should().Contain(
            Task72ArchiveIndexDecision,
            "the mutable archive index must direct corrections into new dated superseding records");
        ValidateTask77ArchiveProvenance(root, fixture, taskLedger, design, archiveIndex);
    }

    private static void ValidateTask77ArchiveProvenance(
        string root,
        ImmutableDocumentHistoryFixture fixture,
        string taskLedger,
        string design,
        string archiveIndex)
    {
        var recordPath = Path.Combine(root, Task77ArchiveRecord.Replace('/', Path.DirectorySeparatorChar));
        var recordBytes = NormalizeHistoricalDocumentBytes(File.ReadAllBytes(recordPath));
        Sha256(recordBytes).Should().Be(Task77ArchiveRecordSha256,
            "the dated Task 7.7 predecessor record must remain byte-exact");
        var cataloguedRecord = fixture.AppendOnlyRecords.Single(entry => entry.Path == Task77ArchiveRecord);
        cataloguedRecord.Bytes.Should().Be(recordBytes.Length);
        cataloguedRecord.Sha256.Should().Be(Task77ArchiveRecordSha256);
        var record = NormalizeLineEndings(Encoding.UTF8.GetString(recordBytes));
        record.Should().ContainAll(
            Task77Predecessor,
            Task77PredecessorCommit,
            Task77MoveCommit,
            Task77ArchivedPrompt,
            "R097",
            "The four changed lines");

        var parent = RunGit(root, "rev-parse", $"{Task77MoveCommit}^");
        parent.ExitCode.Should().Be(0);
        parent.StandardOutput.Trim().Should().Be(Task77PredecessorCommit);
        var predecessorBlob = RunGit(root, "rev-parse", $"{Task77PredecessorCommit}:{Task77Predecessor}");
        predecessorBlob.ExitCode.Should().Be(0);
        predecessorBlob.StandardOutput.Trim().Should().Be("b60942bef28dcd5b58c5027ab92d821d2ebd6d5c");
        var archivedBlob = RunGit(root, "rev-parse", $"{Task77MoveCommit}:{Task77ArchivedPrompt}");
        archivedBlob.ExitCode.Should().Be(0);
        archivedBlob.StandardOutput.Trim().Should().Be("d9d509cf8ca8ae17e8850d057638f7fa1de07820");
        var rename = RunGit(root, "diff", "--find-renames=50%", "--name-status",
            Task77PredecessorCommit, Task77MoveCommit, "--", Task77Predecessor, Task77ArchivedPrompt);
        rename.ExitCode.Should().Be(0);
        NormalizeLineEndings(rename.StandardOutput).TrimEnd().Should().Be(
            $"R097\t{Task77Predecessor}\t{Task77ArchivedPrompt}");
        var predecessorBytes = ReadGitBlob(root, Task77PredecessorCommit, Task77Predecessor);
        predecessorBytes.Length.Should().Be(14526);
        Sha256(predecessorBytes).Should().Be(
            "14c07da02a867247ac04204f9bd3ef137771d70b9196a769b9562d186a0ff556");
        var archivedBytes = ReadGitBlob(root, Task77MoveCommit, Task77ArchivedPrompt);
        archivedBytes.Length.Should().Be(14538);
        Sha256(archivedBytes).Should().Be(
            "80ea80cf3ca951bd7481f1066103472216a8a644787929714570f733eb0db0aa");
        File.Exists(Path.Combine(root, Task77Predecessor.Replace('/', Path.DirectorySeparatorChar)))
            .Should().BeFalse("the old prompt path was moved at the recorded commit");
        var currentArchivedBlob = RunGit(root, "rev-parse", $"HEAD:{Task77ArchivedPrompt}");
        currentArchivedBlob.ExitCode.Should().Be(0);
        currentArchivedBlob.StandardOutput.Trim().Should().Be(archivedBlob.StandardOutput.Trim(),
            "the committed archived prompt must remain byte-identical to the move-commit blob");
        NormalizeHistoricalDocumentBytes(File.ReadAllBytes(Path.Combine(
                root, Task77ArchivedPrompt.Replace('/', Path.DirectorySeparatorChar))))
            .Should().Equal(NormalizeHistoricalDocumentBytes(archivedBytes),
                "checkout line-ending conversion must not change the protected archived prompt's content");

        var task = Regex.Match(taskLedger, @"(?ms)^- \[x\] 7\.7 .*?(?=^## 8\.)",
            RegexOptions.CultureInvariant);
        task.Success.Should().BeTrue("Task 7.7 must be completed only with exact move evidence");
        task.Value.Should().Contain(Task77PredecessorCommit).And.Contain(Task77MoveCommit)
            .And.Contain("No existing protected path was changed or relocated.");
        design.Should().Contain("Task 7.7 resolves the Phase-0 kickoff prompt move with a new immutable archive provenance record.");
        archiveIndex.Should().Contain(
            $"| `{Task77Predecessor}` | [Archived kickoff prompt]" +
            "(plans/developer-facing-interface-phase-00-kickoff-prompt-2026-07-15.md) and its " +
            "[exact move provenance]" +
            "(plans/developer-facing-interface-phase-00-kickoff-archive-provenance-2026-09-23.md) |",
            "the old-path routing key and both exact targets must remain bound together");
    }

    [Fact]
    public void Task73_ActiveDocumentationMatchesTheApprovedSection7BContract()
    {
        var root = FixtureDefinitions.RepositoryRoot();
        Task73ActiveDocumentationPaths.Should().HaveCount(22);
        Task73ActiveDocumentationPaths.Should().OnlyHaveUniqueItems();
        Task73ActiveDocumentationPaths.Should().Equal(
            Task73ActiveDocumentationPaths.Order(StringComparer.Ordinal));

        var sweep = NormalizeLineEndings(File.ReadAllText(Path.Combine(
            root,
            Task73SourceSweepArtifact.Replace('/', Path.DirectorySeparatorChar))));
        foreach (var path in Task73ActiveDocumentationPaths)
        {
            File.Exists(Path.Combine(root, path.Replace('/', Path.DirectorySeparatorChar)))
                .Should().BeTrue($"Task 7.3 source '{path}' must remain present");
            sweep.Should().Contain($"`{path}`",
                $"Task 7.3 source '{path}' must derive from the Task 3.1 sweep");
        }

        Task73ActiveDocumentationPaths.Should().NotContain("docs/orleans-engine/README.md");
        sweep.Should().Contain("`docs/orleans-engine/README.md`",
            "Task 7.4 must retain ownership of the separate Orleans finding");

        var documents = Task73ActiveDocumentationPaths.ToDictionary(
            path => path,
            path => NormalizeLineEndings(File.ReadAllText(Path.Combine(
                root,
                path.Replace('/', Path.DirectorySeparatorChar)))),
            StringComparer.Ordinal);
        var corpusGuardSource = NormalizeLineEndings(File.ReadAllText(Path.Combine(
            root,
            "tests",
            "OrcaCore.DeveloperSurface.Guards",
            "OpenSpecCorpusGuards.cs")));
        Regex.Matches(
                corpusGuardSource,
                @"Trait\(""AC"",\s*""(?:AC|JS-AC|DR-AC)-\d{3}""\)",
                RegexOptions.CultureInvariant)
            .Should().BeEmpty(
                "the Markdown corpus guard must not satisfy product acceptance coverage with AC traits");
        var staleClaims = new[]
        {
            "IWorkflowEventClient",
            "NoActiveWait",
            "Definition-targeted fanout is deferred",
            "Definition fanout is deferred",
            "Workflow-authored `Publish` is deferred",
            "two event routes",
            "two first-release routing",
            "EventDeliveryStatus",
            "EventDeliveryResult",
            "DeliverToInstanceAsync",
            "event delivery returns `InstanceTerminal`",
            "pending Section 7B",
            "Section 7B is a proposal"
        };
        foreach (var (path, content) in documents)
        {
            foreach (var staleClaim in staleClaims)
            {
                content.Should().NotContain(staleClaim,
                    $"Task 7.3 source '{path}' must not retain stale Section 7B claim '{staleClaim}'");
            }
        }

        var eventRequirements = documents["docs/specs/05-requirements-events-waits-timers.md"];
        eventRequirements.Should().ContainAll(
            "a globally unique caller-created `EventId`, required `CorrelationId`",
            "### EV-010 Four self-routing durable routes",
            "### EV-030 Durable pre-wait acceptance is retained",
            "### EV-031 Global identity before routing, per-target fanout ownership",
            "leaves the wait `Active` and the accepted record re-matchable",
            "### EV-045 Static and dynamic wait authoring",
            "### EV-060 Publish uses the transactional outbox",
            "IWorkflowEventDispatcher.DispatchAsync(WorkflowOutboundEvent, CancellationToken)");

        var durableRequirements = Regex.Replace(
            documents["docs/specs/06-requirements-durable-execution.md"],
            @"\s+",
            " ");
        durableRequirements.Should().Contain(
            "The application receives the exact `WorkflowEventAcceptanceResult` from EV-012");
        durableRequirements.Should().NotContain("EventDeliveryResult");

        var acceptance = Regex.Replace(
            documents["docs/specs/12-acceptance-criteria.md"],
            @"\s+",
            " ");
        acceptance.Should().ContainAll(
            "AC-104",
            "AC-108",
            "StepAttemptTimeoutException",
            "leaves the wait `Active` and the accepted record re-matchable",
            "AC-116",
            "`DirectInstanceTerminal`, `StartConflict`",
            "dynamic `StepResult.WaitForEvent`",
            "regardless of its default collation",
            "AC-118",
            "AC-119",
            "AC-120",
            "direct, correlation, definition-fanout, and start-or-deliver",
            "direct durable event ingress returns `Rejected(DirectInstanceTerminal)`");
        acceptance.Should().NotContain("event delivery returns `InstanceTerminal`");

        var management = Regex.Replace(
            documents["docs/specs/09-requirements-management-operations.md"],
            @"\s+",
            " ");
        management.Should().Contain(
            "active waits by definition/event name, stuck instances by definition, and provider-authoritative pressure");
        management.Should().NotContain(
            "age groups",
            "MG-030 must describe only the grouped and pressure values returned by IWorkflowOperationalStore");

        var providerModel = Regex.Replace(
            documents["docs/specs/10-provider-model-and-extensibility.md"],
            @"\s+",
            " ");
        providerModel.Should().ContainAll(
            "`OrcaCore.Engine.Ephemeral` owns `OrcaCore.Hosting.OrcaCoreEphemeralEngineServiceCollectionExtensions`",
            "`OrcaCore.Durable.Hosting` owns `OrcaCore.Hosting.OrcaCoreDurableEngineServiceCollectionExtensions`, " +
            "the durable builder, `IWorkflowEventIngress`, and `IWorkflowEventDispatcher`",
            "Provider and DAG extensions remain in their owning package-specific classes; " +
            "no public extension class is split across assemblies.",
            "the application registers its implementation, and the durable engine consumes it");
        Regex.Replace(documents["docs/specs/16-requirements-durable-driver.md"], @"\s+", " ").Should().Contain(
            "the application registers its implementation and the engine consumes it");
        Regex.Replace(documents["docs/production-readiness.md"], @"\s+", " ").Should().Contain(
            "the application registers an `IWorkflowEventDispatcher` implementation");

        var joined = string.Join('\n', documents.Values);
        joined.Should().ContainAll(
            "IWorkflowEventIngress",
            "WorkflowEventAcceptanceResult",
            "IWorkflowEventDispatcher",
            "orcacore-json-v1",
            "IWorkflowOperationalStore",
            "IWorkflowProviderMaintenanceStore",
            "AddOrcaCoreSqlServerDurableProvider");

        var artifact = NormalizeLineEndings(File.ReadAllText(Path.Combine(
            root,
            Task73DocumentationArtifact.Replace('/', Path.DirectorySeparatorChar))));
        Sha256(artifact).Should().Be(Task73DocumentationArtifactSha256);
        Regex.Replace(artifact, @"\s+", " ").Should().Contain(Task73PinRefreshDecision);
        var artifactRows = Regex.Matches(
                artifact,
                @"(?m)^\| (\d+) \| `([^`]+)` \| `([0-9a-f]{64})` \|$",
                RegexOptions.CultureInvariant)
            .Select(match => new
            {
                Number = int.Parse(match.Groups[1].Value, CultureInfo.InvariantCulture),
                Path = match.Groups[2].Value,
                Sha256 = match.Groups[3].Value
            })
            .ToArray();
        artifactRows.Select(row => row.Number).Should().Equal(Enumerable.Range(1, 22));
        artifactRows.Select(row => row.Path).Should().Equal(Task73ActiveDocumentationPaths);
        foreach (var row in artifactRows)
        {
            Sha256(documents[row.Path]).Should().Be(
                row.Sha256,
                $"Task 7.3 source '{row.Path}' must remain byte-equivalent after LF normalization");
        }

        var ledger = NormalizeLineEndings(File.ReadAllText(Path.Combine(
            root,
            HarmonizationTaskLedgerPath.Replace('/', Path.DirectorySeparatorChar))));
        var task = Regex.Match(
            ledger,
            @"(?ms)^- \[x\] 7\.3 .*?(?=^- \[[ xX]\] 7\.4 )",
            RegexOptions.CultureInvariant);
        task.Success.Should().BeTrue("Task 7.3 must retain its completed 22-source disposition");
        Regex.Replace(task.Value, @"\s+", " ").Should().Contain(Task73CompletionDecision);
        Regex.Replace(task.Value, @"\s+", " ").Should().Contain(Task73VocabularyRemediationDecision);
        Regex.Replace(task.Value, @"\s+", " ").Should().Contain(Task73PinRefreshDecision);
        task.Value.Should().Contain($"`{Task73DocumentationArtifact}`");

        var design = NormalizeLineEndings(File.ReadAllText(Path.Combine(
            root,
            HarmonizationDesignPath.Replace('/', Path.DirectorySeparatorChar))));
        Regex.Replace(design, @"\s+", " ").Should().Contain(Task73PinRefreshDecision);
        Regex.Replace(design, @"\s+", " ").Should().Contain(
            "Task 7.5 review remediation corrects the two remaining legacy event-result statements in DU-055 and " +
            "AC-005 and refreshes only their Task 7.3 source rows plus the guard-owned artifact digest.");
        Regex.Replace(design, @"\s+", " ").Should().Contain(ApprovalHistoryDecision);
    }

    [Fact]
    public void Task74_OrleansFutureBoundaryIsSingularCurrentAndArchivePinned()
    {
        var root = FixtureDefinitions.RepositoryRoot();
        var activeRoot = Path.Combine(root, "docs", "orleans-engine");
        var activePaths = Directory
            .EnumerateFiles(activeRoot, "*", SearchOption.AllDirectories)
            .Select(path => Path.GetRelativePath(root, path).Replace('\\', '/'))
            .Order(StringComparer.Ordinal)
            .ToArray();
        activePaths.Should().Equal(
            [Task74ActiveBoundaryPath],
            "the repository keeps one active Orleans future-hosting boundary note");

        var activeBoundary = NormalizeLineEndings(File.ReadAllText(Path.Combine(
            root,
            Task74ActiveBoundaryPath.Replace('/', Path.DirectorySeparatorChar))));
        Sha256(activeBoundary).Should().Be(Task74ActiveBoundarySha256);
        Regex.Replace(activeBoundary, @"\s+", " ").Should().ContainAll(
            "new OpenSpec change and independent approval",
            "single ordinary `Wait` remains cold-capable",
            "caller-created globally unique `EventId` values",
            "direct, correlation, definition-fanout, and start-or-deliver routes",
            "retained pre-wait acceptance",
            "source acknowledgement only after `Accepted` or `Duplicate`",
            "authored `Publish` commits a workflow-event outbox record atomically",
            "application-registered `IWorkflowEventDispatcher`",
            "continuation records remain runtime-internal",
            "fixed `orcacore-json-v1` codec",
            "`OrcaCore.Engine.Durable` retains interpreter/runtime ownership",
            "`OrcaCore.Durable.Hosting` retains durable hosting, ingress, and dispatcher-port ownership",
            "each durable provider package retains its own storage registration",
            "an Orleans adapter may host reviewed durable seams and own only Orleans-specific activation, transport, and lifecycle integration",
            "must not expose internal aggregate, interpreter, command-processor, compiled-plan, or provider implementation types as public seams",
            "mutually exclusive with another engine owner",
            "Create and independently approve a new OpenSpec change before adding projects, package references, migrations, or implementation tasks",
            "future-capability registry",
            "../archive/plans/orleans-engine-pre-v1/README.md");
        activeBoundary.Should().NotContain(
            "NoActiveWait",
            "the active Orleans note must not revive the superseded non-buffering delivery claim");
        activeBoundary.Should().NotContain(
            "exactly the instance and correlation routes",
            "the active Orleans note must retain the complete four-route ingress union");

        var immutableFixture = FixtureDefinitions.Read<ImmutableDocumentHistoryFixture>(
            ImmutableDocumentHistoryFixturePath);
        var archivePrefix = ImmutableDocumentationPrefixes[0] + Task74ArchiveRelativeRoot;
        var archiveRows = immutableFixture.Records
            .Where(entry => entry.Path.StartsWith(archivePrefix, StringComparison.Ordinal))
            .Select(entry => new
            {
                Path = entry.Path[archivePrefix.Length..],
                entry.Bytes,
                Sha256 = entry.Sha256.ToLowerInvariant()
            })
            .OrderBy(row => row.Path, StringComparer.Ordinal)
            .ToArray();
        archiveRows.Should().HaveCount(Task74ArchiveFileCount);
        var archiveDiskRoot = Path.Combine(
            root,
            archivePrefix.Replace('/', Path.DirectorySeparatorChar));
        var archiveDiskRows = Directory
            .EnumerateFiles(archiveDiskRoot, "*", SearchOption.AllDirectories)
            .Select(path =>
            {
                var bytes = NormalizeHistoricalDocumentBytes(File.ReadAllBytes(path));
                return new
                {
                    Path = Path.GetRelativePath(archiveDiskRoot, path).Replace('\\', '/'),
                    Bytes = bytes.Length,
                    Sha256 = Sha256(bytes)
                };
            })
            .OrderBy(row => row.Path, StringComparer.Ordinal)
            .ToArray();
        archiveDiskRows
            .Select(row => $"{row.Path}\t{row.Bytes}\t{row.Sha256}")
            .Should()
            .Equal(
                archiveRows.Select(row => $"{row.Path}\t{row.Bytes}\t{row.Sha256}"),
                "Task 7.4 must derive the immutable Orleans inventory from disk as well as the history fixture");
        var archiveRecord = string.Join(
            '\n',
            archiveRows.Select(row => $"{row.Path}\t{row.Bytes}\t{row.Sha256}")) + "\n";
        Encoding.UTF8.GetByteCount(archiveRecord).Should().Be(Task74ArchiveRecordBytes);
        Sha256(archiveRecord).Should().Be(
            Task74ArchiveRecordSha256,
            "the immutable-history baseline must retain the complete superseded Orleans plan");

        var artifact = NormalizeLineEndings(File.ReadAllText(Path.Combine(
            root,
            Task74DocumentationArtifact.Replace('/', Path.DirectorySeparatorChar))));
        Sha256(artifact).Should().Be(
            Task74DocumentationArtifactSha256,
            "Task 7.4's reconciliation evidence must remain immutable");
        var artifactRows = Regex.Matches(
                artifact,
                @"(?m)^\| `([^`]+)` \| ([0-9,]+) \| `([0-9a-f]{64})` \|$",
                RegexOptions.CultureInvariant)
            .Select(match => new
            {
                Path = match.Groups[1].Value,
                Bytes = int.Parse(
                    match.Groups[2].Value.Replace(",", string.Empty, StringComparison.Ordinal),
                    CultureInfo.InvariantCulture),
                Sha256 = match.Groups[3].Value
            })
            .ToArray();
        artifactRows.Should().HaveCount(Task74ArchiveFileCount);
        artifactRows
            .Select(row => $"{row.Path}\t{row.Bytes}\t{row.Sha256}")
            .Should()
            .Equal(archiveRows.Select(row => $"{row.Path}\t{row.Bytes}\t{row.Sha256}"));
        artifact.Should().Contain(Task74ActiveBoundarySha256);
        artifact.Should().Contain(Task74ArchiveRecordSha256);

        var sweep = NormalizeLineEndings(File.ReadAllText(Path.Combine(
            root,
            Task73SourceSweepArtifact.Replace('/', Path.DirectorySeparatorChar))));
        sweep.Should().Contain(
            $"`{Task74ActiveBoundaryPath}`",
            "Task 7.4 must close the Orleans finding recorded by Task 3.1");

        var ledger = NormalizeLineEndings(File.ReadAllText(Path.Combine(
            root,
            HarmonizationTaskLedgerPath.Replace('/', Path.DirectorySeparatorChar))));
        var task = Regex.Match(
            ledger,
            @"(?ms)^- \[x\] 7\.4 .*?(?=^- \[[ xX]\] 7\.5 )",
            RegexOptions.CultureInvariant);
        task.Success.Should().BeTrue("Task 7.4 must retain its completed disposition");
        task.Value.Should().Contain($"`{Task74DocumentationArtifact}`");
        Regex.Replace(task.Value, @"\s+", " ").Should().ContainAll(
            "single active future-hosting boundary note",
            "25-file superseded plan remains byte-unchanged",
            "new independently approved OpenSpec change");
        Regex.Replace(task.Value, @"\s+", " ").Should().Contain(Task74ReviewRemediationDecision);

        var orleansTaskOwners = Directory
            .EnumerateFiles(
                Path.Combine(root, "openspec", "changes"),
                "tasks.md",
                SearchOption.AllDirectories)
            .SelectMany(path => Regex.Matches(
                    NormalizeLineEndings(File.ReadAllText(path)),
                    @"(?ms)^- \[[ xX]\] ([0-9]+(?:\.[0-9a-z]+)?) .*?(?=^- \[[ xX]\] |\z)",
                    RegexOptions.CultureInvariant)
                .Where(match => match.Value.Contains("Orleans", StringComparison.OrdinalIgnoreCase))
                .Select(match =>
                    $"{Path.GetFileName(Path.GetDirectoryName(path))}:{match.Groups[1].Value}"))
            .Order(StringComparer.Ordinal)
            .ToArray();
        orleansTaskOwners.Should().Equal(
            [
                "harmonize-downstream-capability-specs:7.3",
                "harmonize-downstream-capability-specs:7.4"
            ],
            "no active task ledger may add Orleans implementation work without a new approved change");

        var design = Regex.Replace(
            NormalizeLineEndings(File.ReadAllText(Path.Combine(
                root,
                HarmonizationDesignPath.Replace('/', Path.DirectorySeparatorChar)))),
            @"\s+",
            " ");
        design.Should().ContainAll(
            "Task 7.4 retains one active Orleans future-hosting boundary",
            "25-file superseded plan remains immutable",
            "new independently approved OpenSpec change");
        design.Should().Contain(Task74DesignReviewRemediationDecision);
    }

    [Fact]
    public void Task75_ActiveTreeRejectsRemovedDeferredCallsAndStaleSection7BClaims()
    {
        var root = FixtureDefinitions.RepositoryRoot();
        var sourcePaths = EnumerateTask71ActiveCorpusSourcePaths(root);
        sourcePaths.Should().NotContain(path => IsImmutableDocumentationPath(path));
        sourcePaths.Should().Contain(Task75InitialStaleNegativePaths);

        var initialFixture = NormalizeLineEndings(File.ReadAllText(Path.Combine(
            root,
            Task73SourceSweepArtifact.Replace('/', Path.DirectorySeparatorChar))));
        Sha256(initialFixture).Should().Be(
            Task75InitialFixtureSha256,
            "Task 7.5's initial complete classification fixture must not be coherently rewritten");
        var fixturePaths = Regex.Matches(
                initialFixture,
                @"(?m)^\d+\. `([^`]+)`$",
                RegexOptions.CultureInvariant)
            .Select(match => match.Groups[1].Value)
            .ToArray();
        fixturePaths.Should().Equal(Task75InitialStaleNegativePaths);

        var historicalFindings = new List<string>();
        foreach (var path in Task75InitialStaleNegativePaths)
        {
            var historical = NormalizeLineEndings(Encoding.UTF8.GetString(ReadGitBlob(
                root,
                Task75InitialFixtureCommit,
                path)));
            var pathFindings = FindTask75StaleNegativeClaims(path, historical);
            pathFindings.Should().NotBeEmpty(
                $"the Task 3.1 fixture path '{path}' must prove the recurring classifier covers its original stale claim");
            historicalFindings.AddRange(pathFindings);
        }

        var documents = sourcePaths.ToDictionary(
            path => path,
            path => NormalizeLineEndings(File.ReadAllText(Path.Combine(
                root,
                path.Replace('/', Path.DirectorySeparatorChar)))),
            StringComparer.Ordinal);
        var positiveFindings = documents
            .SelectMany(document => PositiveRemovedOrDeferredCallPatterns.SelectMany(pattern =>
                Regex.Matches(
                        document.Value,
                        pattern.Expression,
                        RegexOptions.CultureInvariant)
                    .Select(match =>
                        $"{document.Key}:{GetLineNumber(document.Value, match.Index)}:{pattern.Name}")))
            .Order(StringComparer.Ordinal)
            .ToArray();
        positiveFindings.Should().BeEmpty(
            "the recurring active-tree gate must retain Task 7.1's zero positive removed/deferred calls");

        var staleFindings = documents
            .SelectMany(document => FindTask75StaleNegativeClaims(document.Key, document.Value))
            .Order(StringComparer.Ordinal)
            .ToArray();
        staleFindings.Should().BeEmpty(
            "approved Section 7B ingress, buffering, fanout, publish, dispatcher, and status behavior must not regress to stale guidance");

        var artifact = NormalizeLineEndings(File.ReadAllText(Path.Combine(
            root,
            Task75DocumentationArtifact.Replace('/', Path.DirectorySeparatorChar))));
        Sha256(artifact).Should().Be(
            Task75DocumentationArtifactSha256,
            "Task 7.5's zero-finding evidence must remain guard-source pinned");
        var recordedHistoricalFindings = Regex.Matches(
                artifact,
                @"(?m)^HISTORICAL\t([^\t]+)\t([0-9]+)\t([^\r\n]+)$",
                RegexOptions.CultureInvariant)
            .Select(match =>
                $"{match.Groups[1].Value}:{match.Groups[2].Value}:{match.Groups[3].Value}")
            .Order(StringComparer.Ordinal)
            .ToArray();
        recordedHistoricalFindings.Should().Equal(
            historicalFindings.Order(StringComparer.Ordinal),
            "the Task 3.1 replay must pin every historical path, line, and classifier rather than one finding per file");
        var classifierCatalog = string.Join(
            '\n',
            StaleSection7BClaimPatterns.Select(pattern => $"{pattern.Name}\t{pattern.Expression}")) + "\n";
        Sha256(classifierCatalog).Should().Be(
            Task75ClassifierCatalogSha256,
            "retiring a stale-claim classifier requires an explicit guard-source catalog decision");
        recordedHistoricalFindings
            .Select(finding => finding[(finding.LastIndexOf(':') + 1)..])
            .Distinct(StringComparer.Ordinal)
            .Order(StringComparer.Ordinal)
            .Should()
            .Equal(StaleSection7BClaimPatterns
                .Select(pattern => pattern.Name)
                .Order(StringComparer.Ordinal));
        var naturalLanguageRegressions = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["Fanout to every instance of a definition is not supported in v1."] = "deferred definition fanout",
            ["Definition fanout is not supported."] = "deferred definition fanout",
            ["Section 7B is not yet approved."] = "unapproved Section 7B",
            ["Section 7B has not been approved yet."] = "unapproved Section 7B",
            ["Events that arrive before a wait exists are dropped and must be redelivered."] =
                "non-buffering pre-wait delivery",
            ["The durable engine does not buffer events before a wait exists."] =
                "non-buffering pre-wait delivery",
            ["Publishing events from a workflow is not available in v1."] = "deferred durable publish",
            ["The engine supports instance and correlation delivery only."] = "two-route ingress"
        };
        var naturalLanguageRegressionCatalog = string.Join(
            '\n',
            naturalLanguageRegressions
                .OrderBy(regression => regression.Key, StringComparer.Ordinal)
                .Select(regression => $"{regression.Key}\t{regression.Value}")) + "\n";
        Sha256(naturalLanguageRegressionCatalog).Should().Be(
            Task75NaturalLanguageRegressionCatalogSha256,
            "all eight reviewed natural-language regressions and their expected classifiers must remain exact");
        foreach (var regression in naturalLanguageRegressions)
        {
            FindTask75StaleNegativeClaims("synthetic.md", regression.Key)
                .Should().ContainSingle(finding => finding.EndsWith($":{regression.Value}", StringComparison.Ordinal));
        }
        artifact.Should().ContainAll(
            $"`{Task73SourceSweepArtifact}`",
            $"`{Task75InitialFixtureCommit}`",
            "23 initial stale-negative sources",
            "Checkpoint snapshot: 86 evolving active contract sources",
            "0 positive removed/deferred calls",
            "0 stale Section 7B claims");

        var remediationArtifact = NormalizeLineEndings(File.ReadAllText(Path.Combine(
            root,
            Task7475ReviewRemediationArtifact.Replace('/', Path.DirectorySeparatorChar))));
        Sha256(remediationArtifact).Should().Be(Task7475ReviewRemediationArtifactSha256);
        remediationArtifact.Should().ContainAll(
            "LLL-1 — exact historical classifier replay",
            "MMM-1 — current event vocabulary",
            "NNN-1 — review observations",
            "does not authorize a checkpoint");
        var postReviewHardeningArtifact = NormalizeLineEndings(File.ReadAllText(Path.Combine(
            root,
            Task75PostReviewHardeningArtifact.Replace('/', Path.DirectorySeparatorChar))));
        Sha256(postReviewHardeningArtifact).Should().Be(Task75PostReviewHardeningArtifactSha256);
        postReviewHardeningArtifact.Should().ContainAll(
            "OOO-1",
            Task75ClassifierCatalogSha256,
            Task75NaturalLanguageRegressionCatalogSha256,
            "all eight reviewed forms");

        var ledger = NormalizeLineEndings(File.ReadAllText(Path.Combine(
            root,
            HarmonizationTaskLedgerPath.Replace('/', Path.DirectorySeparatorChar))));
        var task = Regex.Match(
            ledger,
            @"(?ms)^- \[x\] 7\.5 .*?(?=^- \[[ xX]\] 7\.6 )",
            RegexOptions.CultureInvariant);
        task.Success.Should().BeTrue("Task 7.5 must retain its completed recurring-gate disposition");
        Regex.Replace(task.Value, @"\s+", " ").Should().Contain(Task75CompletionDecision);
        Regex.Replace(task.Value, @"\s+", " ").Should().Contain(Task75ReviewRemediationDecision);
        Regex.Replace(task.Value, @"\s+", " ").Should().Contain(Task75PostReviewHardeningDecision);
        task.Value.Should().Contain($"`{Task75DocumentationArtifact}`");
        task.Value.Should().Contain($"`{Task7475ReviewRemediationArtifact}`");
        task.Value.Should().Contain($"`{Task75PostReviewHardeningArtifact}`");

        var design = NormalizeLineEndings(File.ReadAllText(Path.Combine(
            root,
            HarmonizationDesignPath.Replace('/', Path.DirectorySeparatorChar))));
        design.Should().Contain(
            Task75DesignDecision,
            "Task 7.5's evolving-scope, immutable-exclusion, and historical-fixture decision must survive archival");
        Regex.Replace(design, @"\s+", " ").Should().Contain(Task75DesignReviewRemediationDecision);
        design.Should().Contain(Task75DesignPostReviewHardeningDecision);
        design.Should().Contain(Path.GetFileName(Task7475ReviewRemediationArtifact));
    }

    private static string[] FindTask75StaleNegativeClaims(string path, string content)
    {
        var findings = new List<string>();
        foreach (var pattern in StaleSection7BClaimPatterns)
        {
            foreach (Match match in Regex.Matches(
                         content,
                         pattern.Expression,
                         RegexOptions.CultureInvariant))
            {
                var lineStart = content.LastIndexOf('\n', Math.Max(0, match.Index - 1)) + 1;
                var lineEnd = content.IndexOf('\n', match.Index + match.Length);
                var evidence = content[lineStart..(lineEnd < 0 ? content.Length : lineEnd)];
                if (Task75HistoricalContextExpressions.Any(expression =>
                        Regex.IsMatch(evidence, expression, RegexOptions.CultureInvariant)))
                {
                    continue;
                }

                findings.Add($"{path}:{GetLineNumber(content, match.Index)}:{pattern.Name}");
            }
        }

        return findings
            .Distinct(StringComparer.Ordinal)
            .Order(StringComparer.Ordinal)
            .ToArray();
    }

    private static string[] ReadHistoricalDocumentPaths(string root, string commit)
    {
        var result = RunGit(
            root,
            "ls-tree",
            "-r",
            "--name-only",
            commit,
            "--",
            ImmutableDocumentationPrefixes[0].TrimEnd('/'),
            ImmutableDocumentationPrefixes[1].TrimEnd('/'));
        result.ExitCode.Should().Be(0, result.StandardError);
        return NormalizeLineEndings(result.StandardOutput)
            .Split('\n', StringSplitOptions.RemoveEmptyEntries)
            .Except(MutableHistoricalDocumentPaths.Select(entry => entry.Path), StringComparer.Ordinal)
            .Order(StringComparer.Ordinal)
            .ToArray();
    }

    private static byte[] NormalizeHistoricalDocumentBytes(byte[] content)
    {
        using var normalized = new MemoryStream(content.Length);
        for (var index = 0; index < content.Length; index++)
        {
            if (content[index] != (byte)'\r')
            {
                normalized.WriteByte(content[index]);
                continue;
            }

            if (index + 1 < content.Length && content[index + 1] == (byte)'\n')
            {
                index++;
            }

            normalized.WriteByte((byte)'\n');
        }

        return normalized.ToArray();
    }

    private static void ValidateHistoricalAdditionSemantics()
    {
        var normalized = Encoding.UTF8.GetBytes("same\n");
        var entry = new ImmutableHistoricalDocumentRecord(
            "docs/review/synthetic.md",
            normalized.Length,
            Sha256(normalized));

        Action identicalReAdditions = () => ValidateHistoricalAdditions(
            entry,
            [
                ("first-addition", Encoding.UTF8.GetBytes("same\r\n")),
                ("second-addition", Encoding.UTF8.GetBytes("same\n"))
            ]);
        identicalReAdditions.Should().NotThrow(
            "re-adding byte-identical normalized history must remain valid");

        Action divergentReAddition = () => ValidateHistoricalAdditions(
            entry,
            [
                ("first-addition", Encoding.UTF8.GetBytes("same\n")),
                ("divergent-addition", Encoding.UTF8.GetBytes("different\n"))
            ]);
        divergentReAddition.Should().Throw<InvalidDataException>()
            .WithMessage("*divergent-addition*");
    }

    private static void ValidateHistoricalAdditions(
        ImmutableHistoricalDocumentRecord entry,
        IEnumerable<(string Commit, byte[] Content)> additions)
    {
        foreach (var (commit, content) in additions)
        {
            var normalized = NormalizeHistoricalDocumentBytes(content);
            if (normalized.Length != entry.Bytes ||
                !string.Equals(Sha256(normalized), entry.Sha256, StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidDataException(
                    $"Historical record '{entry.Path}' does not reproduce its catalogued normalized bytes " +
                    $"at Git addition {commit}.");
            }
        }
    }

    private static void ValidateAppendOnlyHistoricalRecords(
        string root,
        ImmutableDocumentHistoryFixture fixture)
    {
        var appendOnlyPaths = fixture.AppendOnlyRecords
            .Select(entry => entry.Path)
            .ToHashSet(StringComparer.Ordinal);
        var additionHistory = RunGit(
            root,
            "log",
            "--full-history",
            "--diff-filter=A",
            "--name-only",
            "--format=",
            $"{fixture.BaselineCommit}..HEAD",
            "--",
            ImmutableDocumentationPrefixes[0].TrimEnd('/'),
            ImmutableDocumentationPrefixes[1].TrimEnd('/'));
        additionHistory.ExitCode.Should().Be(0, additionHistory.StandardError);
        var committedPostBaselinePaths = NormalizeLineEndings(additionHistory.StandardOutput)
            .Split('\n', StringSplitOptions.RemoveEmptyEntries)
            .Distinct(StringComparer.Ordinal)
            .Order(StringComparer.Ordinal)
            .ToArray();
        committedPostBaselinePaths.Should().OnlyContain(
            path => appendOnlyPaths.Contains(path),
            "every historical record first added after the fixed baseline must remain cataloged");
        committedPostBaselinePaths.Should().OnlyContain(
            path => File.Exists(Path.Combine(root, path.Replace('/', Path.DirectorySeparatorChar))),
            "a committed historical record cannot be deleted or moved without an explicit reviewed tombstone mechanism");

        HashSet<string>? activeFreezePaths = null;

        foreach (var entry in fixture.AppendOnlyRecords)
        {
            var path = Path.Combine(root, entry.Path.Replace('/', Path.DirectorySeparatorChar));
            File.Exists(path).Should().BeTrue($"append-only post-baseline record {entry.Path} must exist");
            var worktreeBytes = NormalizeHistoricalDocumentBytes(File.ReadAllBytes(path));
            worktreeBytes.Should().HaveCount(entry.Bytes);
            Sha256(worktreeBytes).Should().Be(entry.Sha256.ToLowerInvariant());

            var history = RunGit(
                root,
                "log",
                "--full-history",
                "--diff-filter=A",
                "--format=%H",
                "--reverse",
                "HEAD",
                "--",
                entry.Path);
            history.ExitCode.Should().Be(0, history.StandardError);
            var introductionCommits = NormalizeLineEndings(history.StandardOutput)
                .Split('\n', StringSplitOptions.RemoveEmptyEntries);
            if (introductionCommits.Length == 0)
            {
                activeFreezePaths ??= ReadNormalizedReviewManifestPaths(
                    root,
                    fixture.ActiveFreezeManifestPath);
                activeFreezePaths.Should().Contain(
                    entry.Path,
                    "an uncommitted post-baseline record must be byte-pinned by the active reviewed freeze");
                continue;
            }

            ValidateHistoricalAdditions(
                entry,
                introductionCommits.Select(commit => (
                    commit,
                    ReadGitBlob(root, commit, entry.Path))));
        }
    }

    private static string BuildPublishedSemanticAppendix(string sourceArtifact)
    {
        const string temporaryLeasePublicationNote =
            "\nThe lease requirement remains in the active change delta until Section 6 performs its canonical\n" +
            "promotion. This citation records the approved normative target; it does not claim current product\n" +
            "conformance or authorize Section 6 implementation.\n";
        const string sourceAdmissionExclusion =
            "- A third live-fiber admission quantity exists in the current implementation. Task 5.13 removed it;\n" +
            "  the v1 execution model has only host execution-path capacity and node-local `ForEach` admission.";
        const string publishedAdmissionExclusion =
            "- `MaxActiveFibers` or another third live-fiber admission quantity exists in the current implementation.\n" +
            "  Task 5.13 removed that quantity; the v1 execution model has only host execution-path capacity and\n" +
            "  node-local `ForEach` admission.";

        return sourceArtifact
            .Replace("(../specs/", "(../../openspec/specs/", StringComparison.Ordinal)
            .Replace("(../../../specs/", "(../../openspec/specs/", StringComparison.Ordinal)
            .Replace("[reshape durable-runtime delta:", "[durable-runtime:", StringComparison.Ordinal)
            .Replace(temporaryLeasePublicationNote, string.Empty, StringComparison.Ordinal)
            .Replace(sourceAdmissionExclusion, publishedAdmissionExclusion, StringComparison.Ordinal);
    }

    private static string[] EnumerateTask71ActiveCorpusSourcePaths(string root)
    {
        var paths = new List<string>(ActiveCorpusRootDocumentPaths);
        paths.AddRange(Directory
            .EnumerateFiles(Path.Combine(root, DocumentationRoot), "*", SearchOption.AllDirectories)
            .Where(path => ActiveDocumentationExtensions.Contains(
                Path.GetExtension(path),
                StringComparer.OrdinalIgnoreCase))
            .Select(path => RelativePath(root, path))
            .Where(path => !IsImmutableDocumentationPath(path)));
        paths.AddRange(Directory
            .EnumerateFiles(
                Path.Combine(root, CanonicalOpenSpecSpecsRoot.Replace('/', Path.DirectorySeparatorChar)),
                OpenSpecSpecFileName,
                SearchOption.AllDirectories)
            .Select(path => RelativePath(root, path)));

        var changesRoot = Path.Combine(root, OpenSpecChangesRoot.Replace('/', Path.DirectorySeparatorChar));
        foreach (var changeDirectory in Directory
                     .EnumerateDirectories(changesRoot)
                     .Where(path => !string.Equals(
                         Path.GetFileName(path),
                         ArchivedChangesDirectoryName,
                         StringComparison.Ordinal)))
        {
            paths.AddRange(ActiveChangePlanningFileNames
                .Select(fileName => Path.Combine(changeDirectory, fileName))
                .Where(File.Exists)
                .Select(path => RelativePath(root, path)));
            var specsDirectory = Path.Combine(changeDirectory, ActiveChangeSpecsDirectoryName);
            if (Directory.Exists(specsDirectory))
            {
                paths.AddRange(Directory
                    .EnumerateFiles(specsDirectory, OpenSpecSpecFileName, SearchOption.AllDirectories)
                    .Select(path => RelativePath(root, path)));
            }
        }

        return paths
            .Distinct(StringComparer.Ordinal)
            .Order(StringComparer.Ordinal)
            .ToArray();
    }

    private static int GetLineNumber(string content, int characterIndex)
    {
        var lineNumber = 1;
        for (var index = 0; index < characterIndex; index++)
        {
            if (content[index] == '\n')
            {
                lineNumber++;
            }
        }

        return lineNumber;
    }

    private static bool IsImmutableDocumentationPath(string path) =>
        ImmutableDocumentationPrefixes.Any(prefix => path.StartsWith(prefix, StringComparison.Ordinal));

    private static string ReadExactMarkdownSection(string document, string heading)
    {
        var matches = Regex.Matches(
            document,
            $"(?m)^{Regex.Escape(heading)}$",
            RegexOptions.CultureInvariant);
        if (matches.Count != 1)
        {
            throw new InvalidDataException(
                $"Markdown heading '{heading}' must appear exactly once, found {matches.Count}.");
        }

        var start = matches[0].Index;
        var end = document.IndexOf("\n## ", start + heading.Length, StringComparison.Ordinal);
        return document[start..(end < 0 ? document.Length : end)];
    }

    private static string ReadNumberedRequirementBlock(string document, string identity)
    {
        var heading = $"### {identity}";
        var start = document.IndexOf(heading, StringComparison.Ordinal);
        if (start < 0)
        {
            throw new InvalidDataException($"Numbered requirement {identity} was not found.");
        }

        var end = document.IndexOf("\n##", start + heading.Length, StringComparison.Ordinal);
        return document[start..(end < 0 ? document.Length : end)];
    }

    private static string ReadAcceptanceCriterionBlock(string catalog, string criterionId)
    {
        var pattern = $@"(?ms)^- \*\*{Regex.Escape(criterionId)}\*\*.*?(?=^- \*\*AC-|^## |\z)";
        var matches = Regex.Matches(catalog, pattern, RegexOptions.CultureInvariant);
        if (matches.Count != 1)
        {
            throw new InvalidDataException(
                $"Acceptance criterion {criterionId} must appear exactly once, found {matches.Count}.");
        }

        return matches[0].Value.TrimEnd();
    }

    private static DeltaRequirement[] ReadTask53ActiveDeltaRequirements(string root)
    {
        var changesRoot = Path.Combine(root, "openspec", "changes");
        var requirements = new List<DeltaRequirement>();
        foreach (var changeRoot in Directory
                     .EnumerateDirectories(changesRoot)
                     .Where(path => !string.Equals(
                         GetDirectoryName(path),
                         "archive",
                         StringComparison.Ordinal))
                     .Order(StringComparer.Ordinal))
        {
            var change = GetDirectoryName(changeRoot);
            var declaredCapabilities = ReadDeclaredCapabilities(
                root,
                Path.Combine(changeRoot, "proposal.md"));
            foreach (var capabilityDirectory in ReadCapabilityDirectories(
                         Path.Combine(changeRoot, "specs")))
            {
                var capability = GetDirectoryName(capabilityDirectory);
                var capabilityKind = declaredCapabilities
                    .Single(declared => string.Equals(
                        declared.Name,
                        capability,
                        StringComparison.Ordinal))
                    .Kind;
                requirements.AddRange(ReadDeltaRequirements(
                    root,
                    change,
                    capability,
                    capabilityKind,
                    Path.Combine(capabilityDirectory, "spec.md")));
            }
        }

        return requirements.ToArray();
    }

    private static string RequirementBody(string block)
    {
        var headingTerminator = block.IndexOf('\n');
        return headingTerminator < 0 ? string.Empty : block[(headingTerminator + 1)..];
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
        var checkpoint = FixtureDefinitions.Read<OpenSpecProvenanceCheckpoint>(
            "tests/OrcaCore.DeveloperSurface.Guards/Fixtures/openspec-provenance-checkpoint.json");
        var postGateCheckpoint = FixtureDefinitions.Read<PostGateAmendmentCheckpoint>(
            "tests/OrcaCore.DeveloperSurface.Guards/Fixtures/post-gate-amendment-path.json");
        var rows = activeDeltaRequirements
            .Select(requirement => ClassifyPostGateProvenance(
                canonicalRequirements,
                requirement,
                postGateCheckpoint))
            .OrderBy(row => row.RecordLine, StringComparer.Ordinal)
            .ToArray();
        var record = string.Join('\n', rows.Select(row => row.RecordLine)) + "\n";
        var recordBytes = Encoding.UTF8.GetBytes(record);

        checkpoint.SchemaVersion.Should().Be(6);
        checkpoint.RecordFormat.Should().Be(recordFormat);
        rows.Should().HaveCount(checkpoint.RecordCount);
        recordBytes.Should().HaveCount(checkpoint.RecordBytes);
        var recordSha256 = Sha256(record);
        recordSha256.Should().Be(
            checkpoint.RecordSha256,
            "the recomputed provenance record SHA-256 is {0}",
            recordSha256);

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

        ValidatePostGateAmendmentPath(root, canonicalRequirements, postGateCheckpoint);
        foreach (var operation in checkpoint.PendingCanonicalOperations)
        {
            var taskLedger = File.ReadAllText(Path.Combine(
                ResolveChangeRecord(root, operation.TurnsGreenChange),
                "tasks.md"));
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
        checkpoint.SchemaVersion.Should().Be(8);
        checkpoint.ProposedRequirementSuccessors.Should().Equal(
            ExactProposedDagSuccessors,
            "a proposed post-gate successor is a named exception, not a general duplicate-owner waiver");
        checkpoint.ApprovedPendingRequirementSuccessors.Should().Equal(
            ExactApprovedPendingDagSuccessors);
        checkpoint.ProposedRuntimeViewSuccessors.Should().Equal(ExactProposedRuntimeViewSuccessors);
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
                .Select(requirement => ClassifyPostGateProvenance(
                    canonicalRequirements,
                    requirement,
                    checkpoint))
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
            @"(?mi)^(?:\*\*Verdict:\*\*[ \t]*(?:APPROVE|\*\*APPROVE\*\*)|\*\*APPROVE\*\*)[ \t]*$",
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

    private static void ValidateRejectedReviewFreeze(
        string root,
        RejectedReviewFreeze freeze)
    {
        freeze.Disposition.Should().Be("RawGitOrder");
        freeze.BaseCommit.Should().MatchRegex("^[0-9a-f]{40}$");

        var requestPath = Path.Combine(
            root,
            freeze.RequestPath.Replace('/', Path.DirectorySeparatorChar));
        File.Exists(requestPath).Should().BeTrue();
        var requestBytes = File.ReadAllBytes(requestPath);
        requestBytes.Length.Should().Be(freeze.RequestBytes);
        Sha256(requestBytes).Should().Be(
            freeze.RequestSha256,
            "a rejected review request must remain byte-exact");
        NormalizeLineEndings(Encoding.UTF8.GetString(requestBytes)).Should().Contain(
            Path.GetFileName(freeze.RequestManifestPath),
            "the rejected review request must name the manifest that actually froze its target");

        var manifest = ReadReviewManifest(root, freeze.ManifestPath);
        manifest.Lines.Should().HaveCount(freeze.LineCount);
        manifest.Lines.Should().NotBeEmpty();
        manifest.Lines.Should().OnlyHaveUniqueItems();
        manifest.Lines.Should().OnlyContain(line => Regex.IsMatch(
            line,
            @"^[ MADRCU?!]{2} .+$",
            RegexOptions.CultureInvariant));
        manifest.Bytes.Length.Should().Be(freeze.ManifestBytes);
        Sha256(manifest.Bytes).Should().Be(
            freeze.ManifestSha256,
            "a rejected raw-order manifest must remain byte-exact");
        manifest.Bytes.Should().Equal(
            Encoding.UTF8.GetBytes(string.Join('\n', manifest.Lines) + "\n"),
            "a rejected raw-order manifest must remain LF-only with one final newline");

        var verdictPath = Path.Combine(
            root,
            freeze.VerdictPath.Replace('/', Path.DirectorySeparatorChar));
        File.Exists(verdictPath).Should().BeTrue();
        var verdictBytes = File.ReadAllBytes(verdictPath);
        verdictBytes.Length.Should().Be(freeze.VerdictBytes);
        Sha256(verdictBytes).Should().Be(
            freeze.VerdictSha256,
            "a rejected verdict must remain byte-exact");
        var verdictMatches = Regex.Matches(
            NormalizeLineEndings(Encoding.UTF8.GetString(verdictBytes)),
            @"(?mi)^\*\*Verdict(?::\*\*|:)[ \t]*(?:\*\*)?(APPROVE|REJECT)(?:\*\*)?[ \t]*$",
            RegexOptions.CultureInvariant);
        verdictMatches.Should().ContainSingle(
            "every rejected freeze must bind exactly one terminal verdict");
        verdictMatches[0].Groups[1].Value.Should().Be(
            "REJECT",
            "a rejected freeze cannot be relabeled as approved evidence");
    }
    private static void ValidateArchivedReviewFreeze(
        string root,
        ArchivedReviewFreeze freeze,
        IReadOnlyCollection<ReviewVerdictEvidence> verdictEvidence)
    {
        freeze.Disposition.Should().Be("RawGitOrder");
        freeze.Authority.Should().BeOneOf(IndependentReviewAuthority, OwnerAuthorizationAuthority);
        var authorityEvidence = verdictEvidence
            .Where(evidence => evidence.Path == freeze.AuthorityEvidencePath)
            .ToArray();
        authorityEvidence.Should().ContainSingle(
            "every archived freeze must bind one registered immutable authority verdict");
        authorityEvidence.Single().Verdict.Should().Be("APPROVE");
        if (freeze.Authority == IndependentReviewAuthority)
        {
            RequireIndependentReviewAuthority(freeze.Id, freeze.AuthorityEvidencePath);
        }
        else
        {
            RequireOwnerAuthorizationAuthority(freeze.Id, freeze.AuthorityEvidencePath);
        }
        freeze.BaseCommit.Should().MatchRegex("^[0-9a-f]{40}$");
        freeze.CheckpointCommit.Should().MatchRegex("^[0-9a-f]{40}$");
        freeze.CheckpointTree.Should().MatchRegex("^[0-9a-f]{40}$");

        var requestPath = Path.Combine(
            root,
            freeze.RequestPath.Replace('/', Path.DirectorySeparatorChar));
        File.Exists(requestPath).Should().BeTrue();
        NormalizeLineEndings(File.ReadAllText(requestPath)).Should().Contain(
            Path.GetFileName(freeze.ManifestPath));

        var manifest = ReadReviewManifest(root, freeze.ManifestPath);
        manifest.Lines.Should().HaveCount(freeze.LineCount);
        manifest.Lines.Should().NotBeEmpty();
        manifest.Lines.Should().OnlyHaveUniqueItems();
        manifest.Lines.Should().OnlyContain(line => Regex.IsMatch(
            line,
            @"^[ MADRCU?!]{2} .+$",
            RegexOptions.CultureInvariant));
        manifest.Bytes.Length.Should().Be(freeze.ManifestBytes);
        Sha256(manifest.Bytes).Should().Be(freeze.ManifestSha256);
        freeze.ManifestSha256.Should().Be(
            freeze.FrozenRawPorcelainSha256,
            "an archived raw-order freeze must preserve its original porcelain anchor");
        manifest.Bytes.Should().Equal(
            Encoding.UTF8.GetBytes(string.Join('\n', manifest.Lines) + "\n"),
            "an archived raw-order manifest must remain LF-only with one final newline");

        var pathSorted = manifest.Lines
            .OrderBy(ReviewManifestPath, StringComparer.Ordinal)
            .ToArray();
        var pathSortedBytes = Encoding.UTF8.GetBytes(string.Join('\n', pathSorted) + "\n");
        Sha256(pathSortedBytes).Should().Be(freeze.PathSortedSha256);
        freeze.PathSortedSha256.Should().NotBe(
            freeze.FrozenRawPorcelainSha256,
            "the archived regression must discriminate path sorting from Git-emitted raw order");

        var commit = RunGit(root, "cat-file", "-e", $"{freeze.CheckpointCommit}^{{commit}}");
        commit.ExitCode.Should().Be(0, "the archived reviewed checkpoint must exist");
        var parent = RunGit(root, "rev-parse", $"{freeze.CheckpointCommit}^");
        parent.ExitCode.Should().Be(0);
        parent.StandardOutput.Trim().Should().Be(freeze.BaseCommit);
        var tree = RunGit(root, "rev-parse", $"{freeze.CheckpointCommit}^{{tree}}");
        tree.ExitCode.Should().Be(0);
        tree.StandardOutput.Trim().Should().Be(freeze.CheckpointTree);
        ReadCommittedPaths(root, freeze.BaseCommit, freeze.CheckpointCommit)
            .Should()
            .Equal(
                manifest.Lines
                    .Select(ReviewManifestPath)
                    .Order(StringComparer.Ordinal),
                "the archived checkpoint must contain exactly its frozen manifest paths");

        freeze.ContentRecordExcludedPaths.Should().Equal(
            [ReviewManifestProvenanceFixture],
            "only the self-referential provenance fixture may be excluded from an archived content record");
        manifest.Lines.Select(ReviewManifestPath).Should().Contain(ReviewManifestProvenanceFixture);
        var contentRecordLines = manifest.Lines
            .Where(line => !freeze.ContentRecordExcludedPaths.Contains(
                ReviewManifestPath(line),
                StringComparer.Ordinal))
            .ToArray();
        var contentRecord = BuildCommittedContentRecord(
            root,
            freeze.CheckpointCommit,
            contentRecordLines);
        Encoding.UTF8.GetByteCount(contentRecord).Should().Be(freeze.ContentRecordBytes);
        Sha256(contentRecord).Should().Be(
            freeze.ContentRecordSha256,
            "the archived checkpoint must preserve its scoped content anchor");
    }

    private static void ValidateActiveReviewFreeze(
        string root,
        ActiveReviewFreeze freeze,
        IEnumerable<ReviewManifestProvenanceEntry> historicalEntries)
    {
        freeze.Disposition.Should().Be("RawGitOrder");
        historicalEntries.Select(entry => entry.Task).Should().NotContain(
            freeze.Task,
            "an active freeze becomes a historical entry only after its checkpoint commit exists");
        freeze.BaseCommit.Should().MatchRegex("^[0-9a-f]{40}$");

        var requestPath = Path.Combine(
            root,
            freeze.RequestPath.Replace('/', Path.DirectorySeparatorChar));
        File.Exists(requestPath).Should().BeTrue();
        NormalizeLineEndings(File.ReadAllText(requestPath)).Should().Contain(
            Path.GetFileName(freeze.ManifestPath));
        var manifest = ReadReviewManifest(root, freeze.ManifestPath);
        manifest.Lines.Should().NotBeEmpty();
        manifest.Lines.Should().OnlyHaveUniqueItems();
        manifest.Lines.Should().OnlyContain(line => Regex.IsMatch(
            line,
            @"^[ MADRCU?!]{2} .+$",
            RegexOptions.CultureInvariant));
        manifest.Bytes.Should().Equal(
            Encoding.UTF8.GetBytes(string.Join('\n', manifest.Lines) + "\n"),
            "an active raw-order manifest must be LF-only with one final newline");
        freeze.ContentRecordExcludedPaths.Should().Equal(
            [ReviewManifestProvenanceFixture],
            "only the self-referential provenance fixture may be excluded from the active content record");
        var manifestPaths = manifest.Lines.Select(ReviewManifestPath).ToArray();
        manifestPaths.Should().Contain(ReviewManifestProvenanceFixture);
        var contentRecordLines = manifest.Lines
            .Where(line => !freeze.ContentRecordExcludedPaths.Contains(
                ReviewManifestPath(line),
                StringComparer.Ordinal))
            .ToArray();
        contentRecordLines.Should().HaveCount(manifest.Lines.Length - 1);
        freeze.ContentRecordBytes.Should().BeGreaterThan(0);
        freeze.ContentRecordSha256.Should().MatchRegex("^[0-9a-f]{64}$");

        var head = RunGit(root, "rev-parse", "HEAD");
        head.ExitCode.Should().Be(0);
        var currentProjection = ReadCurrentCommitRealFreezeManifest(root);
        if (currentProjection.Length > 0)
        {
            head.StandardOutput.Trim().Should().Be(
                freeze.BaseCommit,
                "the dirty active freeze must remain on its declared immutable base");
            manifest.Lines.Should().Equal(
                currentProjection,
                "the active manifest must preserve the current commit-real porcelain in raw Git order");
            var worktreeContentRecord = BuildWorktreeContentRecord(root, contentRecordLines);
            Encoding.UTF8.GetByteCount(worktreeContentRecord).Should().Be(freeze.ContentRecordBytes);
            Sha256(worktreeContentRecord).Should().Be(
                freeze.ContentRecordSha256,
                "the active freeze must content-pin every target path except its self-referential fixture");
            return;
        }

        var parent = RunGit(root, "rev-parse", "HEAD^");
        parent.ExitCode.Should().Be(0);
        parent.StandardOutput.Trim().Should().Be(
            freeze.BaseCommit,
            "a clean active freeze must be the immediately committed reviewed checkpoint");
        var committedPaths = ReadCommittedPaths(root, freeze.BaseCommit, head.StandardOutput.Trim());
        manifest.Lines.Select(ReviewManifestPath)
            .Order(StringComparer.Ordinal)
            .Should()
            .Equal(
                committedPaths,
                "the checkpoint must contain exactly the active frozen manifest paths");
        var committedContentRecord = BuildCommittedContentRecord(
            root,
            head.StandardOutput.Trim(),
            contentRecordLines);
        Encoding.UTF8.GetByteCount(committedContentRecord).Should().Be(freeze.ContentRecordBytes);
        Sha256(committedContentRecord).Should().Be(
            freeze.ContentRecordSha256,
            "the committed checkpoint must preserve the active freeze's scoped content anchor");
    }

    private static void ValidateActiveFreezeContentRecordSemantics()
    {
        var ownedBytes = Encoding.UTF8.GetBytes("owned");
        var fixtureBytes = Encoding.UTF8.GetBytes("fixture");
        HistoricalContentRecordRow[] baseline =
        [
            new(" M", "docs/owned.md", ownedBytes.Length, Sha256(ownedBytes)),
            new(" M", ReviewManifestProvenanceFixture, fixtureBytes.Length, Sha256(fixtureBytes))
        ];
        var contentRecord = BuildHistoricalContentRecord(
            baseline.Where(row => row.Path != ReviewManifestProvenanceFixture));
        var includedMutation = BuildHistoricalContentRecord(
            baseline
                .Select(row => row.Path == "docs/owned.md"
                    ? row with { Sha256 = Sha256(Encoding.UTF8.GetBytes("drift")) }
                    : row)
                .Where(row => row.Path != ReviewManifestProvenanceFixture));
        var excludedMutation = BuildHistoricalContentRecord(
            baseline
                .Select(row => row.Path == ReviewManifestProvenanceFixture
                    ? row with { Sha256 = Sha256(Encoding.UTF8.GetBytes("changed")) }
                    : row)
                .Where(row => row.Path != ReviewManifestProvenanceFixture));

        includedMutation.Should().NotBe(
            contentRecord,
            "drift in every non-self-referential target file must invalidate the active content record");
        excludedMutation.Should().Be(
            contentRecord,
            "the sole excluded fixture may change only because it stores the content record itself");
    }

    private static void ValidateActiveFreezeContentRecordBuilders(string root)
    {
        const string probePath = "CLAUDE.md";
        var manifestLine = $" M {probePath}";
        var worktreeBytes = File.ReadAllBytes(Path.Combine(root, probePath));
        var expectedWorktreeRecord = BuildHistoricalContentRecord(
        [
            new(" M", probePath, worktreeBytes.Length, Sha256(worktreeBytes))
        ]);
        BuildWorktreeContentRecord(root, [manifestLine]).Should().Be(
            expectedWorktreeRecord,
            "the active-freeze worktree builder must read and render the declared file bytes");

        var head = RunGit(root, "rev-parse", "HEAD");
        head.ExitCode.Should().Be(0);
        var commit = head.StandardOutput.Trim();
        var committedBytes = ReadGitBlob(root, commit, probePath);
        var expectedCommittedRecord = BuildHistoricalContentRecord(
        [
            new(" M", probePath, committedBytes.Length, Sha256(committedBytes))
        ]);
        BuildCommittedContentRecord(root, commit, [manifestLine]).Should().Be(
            expectedCommittedRecord,
            "the active-freeze committed builder must read and render the declared Git blob bytes");
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

        var projection = ReadCurrentCommitRealFreezeManifest(root);
        var tracked = RunGit(root, "diff", "--name-only", "--no-renames", "HEAD");
        tracked.ExitCode.Should().Be(0);
        var untracked = RunGit(root, "ls-files", "--others", "--exclude-standard");
        untracked.ExitCode.Should().Be(0);
        var commitRealPaths = NormalizeLineEndings(tracked.StandardOutput)
            .Split('\n', StringSplitOptions.RemoveEmptyEntries)
            .Concat(NormalizeLineEndings(untracked.StandardOutput)
                .Split('\n', StringSplitOptions.RemoveEmptyEntries))
            .ToHashSet(StringComparer.Ordinal);
        projection.Select(ReviewManifestPath)
            .Order(StringComparer.Ordinal)
            .Should()
            .Equal(
                commitRealPaths.Order(StringComparer.Ordinal),
                "the review freeze entry set must equal tracked content diffs plus untracked files");
    }

    private static string[] ReadCurrentCommitRealFreezeManifest(string root)
    {
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

        return ProjectCommitRealFreezeManifest(statusLines, commitRealPaths);
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

    private static void ValidateReviewStateEvidence(
        string root,
        ReviewManifestProvenanceEntry entry)
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
                RequireMissingApprovalCheckpointAbsent(
                    entry.Task,
                    entry.CheckpointCommit,
                    entry.CheckpointTree);
                break;
            case RejectedReviewState:
                approvals.Should().Be(0);
                rejections.Should().BeGreaterThan(0);
                stateEvidence.Should().NotBeNull();
                stateEvidence!.Verdict.Should().Be("REJECT");
                break;
            case ApprovalAwaitingEvidenceCommitReviewState:
                approvals.Should().BeGreaterThan(0,
                    "first-pass approval and later approved remediation rounds need no prior rejection");
                entry.ApprovalEvidenceCommit.Should().BeNull();
                stateEvidence.Should().NotBeNull();
                stateEvidence!.Verdict.Should().Be("APPROVE");
                RequireIndependentReviewAuthority(entry.Task, stateEvidence.Path);
                break;
            case ApprovedReviewState:
                approvals.Should().BeGreaterThan(0,
                    "an approved task may retain more than one immutable approval round");
                stateEvidence.Should().NotBeNull();
                stateEvidence!.Verdict.Should().Be("APPROVE");
                RequireIndependentReviewAuthority(entry.Task, stateEvidence.Path);
                ValidateCommittedApprovalEvidence(
                    root,
                    entry,
                    stateEvidence,
                    requireReviewedTargetAsDirectParent: true);
                break;
            case OwnerAuthorizationAwaitingEvidenceCommitReviewState:
                RequireRetroactiveOwnerAuthorizationTask(entry.Task);
                approvals.Should().Be(1);
                entry.ApprovalEvidenceCommit.Should().BeNull();
                entry.CheckpointCommit.Should().NotBeNullOrWhiteSpace();
                entry.CheckpointTree.Should().NotBeNullOrWhiteSpace();
                stateEvidence.Should().NotBeNull();
                stateEvidence!.Verdict.Should().Be("APPROVE");
                RequireOwnerAuthorizationAuthority(entry.Task, stateEvidence.Path);
                break;
            case OwnerAuthorizedReviewState:
                RequireRetroactiveOwnerAuthorizationTask(entry.Task);
                approvals.Should().Be(1);
                entry.CheckpointCommit.Should().NotBeNullOrWhiteSpace();
                entry.CheckpointTree.Should().NotBeNullOrWhiteSpace();
                stateEvidence.Should().NotBeNull();
                stateEvidence!.Verdict.Should().Be("APPROVE");
                RequireOwnerAuthorizationAuthority(entry.Task, stateEvidence.Path);
                ValidateCommittedApprovalEvidence(
                    root,
                    entry,
                    stateEvidence,
                    requireReviewedTargetAsDirectParent: false);
                break;
            default:
                throw new InvalidDataException(
                    $"Unsupported review state '{entry.ReviewState}' for Task {entry.Task}.");
        }
    }

    private static void ValidateCommittedApprovalEvidence(
        string root,
        ReviewManifestProvenanceEntry entry,
        ReviewVerdictEvidence stateEvidence,
        bool requireReviewedTargetAsDirectParent)
    {
        entry.ApprovalEvidenceCommit.Should().NotBeNullOrWhiteSpace(
            "approved states must name the distinct commit that preserved their approval evidence");

        var commit = RunGit(
            root,
            "rev-parse",
            "--verify",
            $"{entry.ApprovalEvidenceCommit}^{{commit}}");
        commit.ExitCode.Should().Be(
            0,
            $"Task {entry.Task} approval-evidence commit must resolve: {commit.StandardError}");
        NormalizeLineEndings(commit.StandardOutput).Trim().Should().Be(
            entry.ApprovalEvidenceCommit,
            "approval-evidence commits must use the exact full object id rather than an ambiguous prefix");

        entry.ReviewedTargetCommit.Should().NotBeNullOrWhiteSpace(
            "approved states must name the exact reviewed target that directly precedes their evidence commit");
        var commitLine = RunGit(
            root,
            "rev-list",
            "--parents",
            "-n",
            "1",
            entry.ApprovalEvidenceCommit!);
        commitLine.ExitCode.Should().Be(
            0,
            $"Task {entry.Task} approval-evidence parent must resolve: {commitLine.StandardError}");
        var commitAndParents = NormalizeLineEndings(commitLine.StandardOutput)
            .Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
        commitAndParents.Should().HaveCount(
            2,
            $"Task {entry.Task} approval evidence must be one distinct non-merge commit directly after the reviewed target");
        commitAndParents[0].Should().Be(
            entry.ApprovalEvidenceCommit,
            $"Task {entry.Task} approval evidence must resolve to its registered commit");
        if (requireReviewedTargetAsDirectParent)
        {
            commitAndParents[1].Should().Be(
                entry.ReviewedTargetCommit,
                $"Task {entry.Task} independent approval-evidence commit parent must be the exact reviewed target");
        }
        else
        {
            var targetAncestry = RunGit(
                root,
                "merge-base",
                "--is-ancestor",
                entry.ReviewedTargetCommit!,
                entry.ApprovalEvidenceCommit!);
            targetAncestry.ExitCode.Should().Be(
                0,
                $"Task {entry.Task} retroactively recorded owner authorization must still descend from its exact reviewed target");
        }

        var ancestry = RunGit(
            root,
            "merge-base",
            "--is-ancestor",
            entry.ApprovalEvidenceCommit!,
            "HEAD");
        ancestry.ExitCode.Should().Be(
            0,
            $"Task {entry.Task} approval evidence must remain in the current checkpoint lineage");

        var committedVerdict = RunGit(
            root,
            "show",
            $"{entry.ApprovalEvidenceCommit}:{stateEvidence.Path}");
        committedVerdict.ExitCode.Should().Be(
            0,
            $"Task {entry.Task} approval-evidence commit must contain {stateEvidence.Path}");
        NormalizeLineEndings(committedVerdict.StandardOutput).Should().Be(
            NormalizeLineEndings(File.ReadAllText(Path.Combine(
                root,
                stateEvidence.Path.Replace('/', Path.DirectorySeparatorChar)))),
            "the evidence commit must preserve the registered governing verdict byte content");

        var verdictAddition = RunGit(
            root,
            "diff-tree",
            "--root",
            "--no-commit-id",
            "--name-status",
            "-r",
            "--no-renames",
            entry.ApprovalEvidenceCommit!,
            "--",
            stateEvidence.Path);
        verdictAddition.ExitCode.Should().Be(
            0,
            $"Task {entry.Task} approval-evidence addition must resolve: {verdictAddition.StandardError}");
        NormalizeLineEndings(verdictAddition.StandardOutput).Trim().Should().Be(
            $"A\t{stateEvidence.Path}",
            "the approval-evidence commit must add the governing verdict rather than merely contain it");
    }

    private static void RequireIndependentReviewAuthority(string owner, string evidencePath)
    {
        if (evidencePath.Contains("-owner-approval-verdict-", StringComparison.Ordinal))
        {
            throw new InvalidDataException(
                $"Independent review state {owner} cannot use owner-approval evidence {evidencePath}.");
        }
    }

    private static void RequireOwnerAuthorizationAuthority(string owner, string evidencePath)
    {
        if (!evidencePath.Contains("-owner-approval-verdict-", StringComparison.Ordinal))
        {
            throw new InvalidDataException(
                $"Owner-authorized state {owner} must use an owner-approval verdict, found {evidencePath}.");
        }
    }

    private static void RequireRetroactiveOwnerAuthorizationTask(string task)
    {
        if (!string.Equals(task, RetroactiveOwnerAuthorizationTask, StringComparison.Ordinal))
        {
            throw new InvalidDataException(
                $"Only historical Task {RetroactiveOwnerAuthorizationTask} may use the retroactive owner-authorization lineage rule, found Task {task}.");
        }
    }

    private static void ValidateReviewAuthoritySemantics()
    {
        RequireIndependentReviewAuthority("synthetic-independent", "docs/review/independent-review-verdict.md");
        RequireOwnerAuthorizationAuthority(
            RetroactiveOwnerAuthorizationTask,
            "docs/review/synthetic-owner-approval-verdict-2026-08-31.md");
        RequireRetroactiveOwnerAuthorizationTask(RetroactiveOwnerAuthorizationTask);

        Action ownerEvidenceAsIndependent = () => RequireIndependentReviewAuthority(
            "synthetic-independent",
            "docs/review/synthetic-owner-approval-verdict-2026-08-31.md");
        ownerEvidenceAsIndependent.Should().Throw<InvalidDataException>()
            .WithMessage("*Independent review state*owner-approval evidence*");

        Action independentEvidenceAsOwner = () => RequireOwnerAuthorizationAuthority(
            "synthetic-owner",
            "docs/review/independent-review-verdict.md");
        independentEvidenceAsOwner.Should().Throw<InvalidDataException>()
            .WithMessage("*Owner-authorized state*owner-approval verdict*");

        Action unrelatedOwnerAuthorization = () => RequireRetroactiveOwnerAuthorizationTask("7.4");
        unrelatedOwnerAuthorization.Should().Throw<InvalidDataException>()
            .WithMessage("*Only historical Task 5.3*found Task 7.4*");
    }

    private static void ValidateMissingApprovalStateSemantics()
    {
        Action invalidCommit = () => RequireMissingApprovalCheckpointAbsent(
            "synthetic-commit",
            SyntheticGitObjectId,
            null);
        Action invalidTree = () => RequireMissingApprovalCheckpointAbsent(
            "synthetic-tree",
            null,
            SyntheticGitObjectId);

        invalidCommit.Should().Throw<InvalidDataException>().WithMessage("*synthetic-commit*checkpoint*");
        invalidTree.Should().Throw<InvalidDataException>().WithMessage("*synthetic-tree*checkpoint*");
        RequireMissingApprovalCheckpointAbsent("synthetic-clean", null, null);
    }

    private static void RequireMissingApprovalCheckpointAbsent(
        string task,
        string? checkpointCommit,
        string? checkpointTree)
    {
        if (checkpointCommit is not null || checkpointTree is not null)
        {
            throw new InvalidDataException(
                $"Task {task} cannot record a checkpoint while review state is MissingApproval; " +
                "use independent approval or an explicit owner-authorization state.");
        }
    }

    private static HashSet<string> ReadNormalizedReviewManifestPaths(string root, string relativePath)
    {
        var path = Path.Combine(root, relativePath.Replace('/', Path.DirectorySeparatorChar));
        File.Exists(path).Should().BeTrue("the active review manifest must exist");
        var bytes = File.ReadAllBytes(path);
        (bytes.Length >= 3 && bytes[0] == 0xEF && bytes[1] == 0xBB && bytes[2] == 0xBF)
            .Should()
            .BeFalse("review manifests use UTF-8 without BOM");
        var text = NormalizeLineEndings(Encoding.UTF8.GetString(bytes));
        text.Should().EndWith("\n");
        return text[..^1]
            .Split('\n')
            .Select(ReviewManifestPath)
            .ToHashSet(StringComparer.Ordinal);
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

    private static string BuildWorktreeContentRecord(
        string root,
        IEnumerable<string> manifestLines) =>
        string.Join(
            '\n',
            manifestLines
                .Select(line =>
                {
                    var path = ReviewManifestPath(line);
                    var bytes = File.ReadAllBytes(Path.Combine(
                        root,
                        path.Replace('/', Path.DirectorySeparatorChar)));
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

    private static Match[] ReadCiWorkflowSteps(string workflow) =>
        Regex.Matches(
            workflow,
            @"(?ms)^[ \t]*-[ \t]+(?:name|uses):[^\n]*(?:\n.*?)(?=^[ \t]*-[ \t]+(?:name|uses):|\z)",
            RegexOptions.CultureInvariant)
        .Cast<Match>()
        .ToArray();

    private static bool IsGeneratedBuildPath(string path) =>
        path.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
            .Any(segment =>
                segment.Equals("bin", StringComparison.OrdinalIgnoreCase) ||
                segment.Equals("obj", StringComparison.OrdinalIgnoreCase));

    private static void RequireMarkdownLinkTarget(
        string repositoryRoot,
        string sourceDocumentPath,
        string block,
        string label,
        string expectedRelativePath,
        string? expectedAnchor,
        string? expectedHeading)
    {
        var matches = Regex.Matches(
            block,
            $@"\[{Regex.Escape(label)}\]\((?<target>[^)]+)\)",
            RegexOptions.CultureInvariant);
        matches.Should().ContainSingle("the normative companion link {0} must occur exactly once", label);

        var expectedTarget = expectedAnchor is null
            ? expectedRelativePath
            : $"{expectedRelativePath}#{expectedAnchor}";
        matches[0].Groups["target"].Value.Should().Be(
            expectedTarget,
            "the normative companion link {0} must continue to address its approved target",
            label);

        var sourcePath = Path.Combine(
            repositoryRoot,
            sourceDocumentPath.Replace('/', Path.DirectorySeparatorChar));
        var targetPath = Path.GetFullPath(Path.Combine(
            Path.GetDirectoryName(sourcePath)!,
            expectedRelativePath.Replace('/', Path.DirectorySeparatorChar)));
        File.Exists(targetPath).Should().BeTrue(
            "the normative companion link {0} must resolve to an existing document",
            label);
        if (expectedHeading is not null)
        {
            var target = NormalizeLineEndings(File.ReadAllText(targetPath));
            target.Should().Contain(
                expectedHeading,
                "the normative companion anchor {0} must resolve to its approved heading",
                expectedAnchor);
        }
    }

    private static string NormalizeCiWorkflowStep(Match step) =>
        Regex.Replace(step.Value, @"\s+", " ");

    private static void ValidateInfrastructureGuardCiLane(string root)
    {
        var workflowPath = Path.Combine(root, ".github", "workflows", "ci.yml");
        var workflow = NormalizeLineEndings(File.ReadAllText(workflowPath));
        var workflowSteps = ReadCiWorkflowSteps(workflow);
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
        var normalizedCommand = NormalizeCiWorkflowStep(guardSteps[0]);
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
        SupersededOpenSpecProvenanceArtifacts.Should().HaveCount(
            SupersededOpenSpecProvenanceArtifactCatalogCount);
        var supersededArtifactCatalog = string.Join(
            '\n',
            SupersededOpenSpecProvenanceArtifacts
                .Select(artifact => $"{artifact.Path}\t{artifact.NormalizedSha256}")
                .Order(StringComparer.Ordinal)) + "\n";
        Sha256(supersededArtifactCatalog).Should().Be(
            SupersededOpenSpecProvenanceArtifactCatalogSha256,
            "superseded provenance artifacts must remain permanently pinned by guard source");
        foreach (var artifact in SupersededOpenSpecProvenanceArtifacts)
        {
            artifact.Path.Should().NotBe(
                checkpoint.ArtifactPath,
                "the current provenance artifact must not also be catalogued as superseded");
            var supersededPath = Path.Combine(
                root,
                artifact.Path.Replace('/', Path.DirectorySeparatorChar));
            File.Exists(supersededPath).Should().BeTrue(
                $"superseded provenance artifact {artifact.Path} must remain available");
            Sha256(NormalizeLineEndings(File.ReadAllText(supersededPath))).Should().Be(
                artifact.NormalizedSha256,
                $"superseded provenance artifact {artifact.Path} must retain its reviewed bytes");
        }

        var openSpecChangesPath = Path.Combine(
            root,
            OpenSpecChangesRoot.Replace('/', Path.DirectorySeparatorChar));
        var discoveredProvenanceArtifacts = Directory
            .EnumerateFiles(openSpecChangesPath, "*.md", SearchOption.AllDirectories)
            .Where(path => Path.GetFileName(path).Contains(
                OpenSpecProvenanceArtifactNameFragment,
                StringComparison.Ordinal))
            .Select(path => RelativePath(root, path))
            .Order(StringComparer.Ordinal)
            .ToArray();
        var governedProvenanceArtifacts = SupersededOpenSpecProvenanceArtifacts
            .Select(artifact => artifact.Path)
            .Append(checkpoint.ArtifactPath)
            .Distinct(StringComparer.Ordinal)
            .Order(StringComparer.Ordinal)
            .ToArray();
        discoveredProvenanceArtifacts.Should().Equal(
            governedProvenanceArtifacts,
            "every OpenSpec provenance artifact must be the current record or a permanently catalogued predecessor");

        var artifactPath = Path.Combine(
            root,
            checkpoint.ArtifactPath.Replace('/', Path.DirectorySeparatorChar));
        File.Exists(artifactPath).Should().BeTrue("the reviewed provenance artifact must exist");
        var normalizedArtifact = NormalizeLineEndings(File.ReadAllText(artifactPath));
        checkpoint.ArtifactNormalizedSha256.Should().Be(
            CurrentOpenSpecProvenanceSha256,
            "the current human record must not be self-attesting through fixture edits");
        var pendingCount = rows.Count(row => row.State is
            ProvenanceState.PendingAddition or
            ProvenanceState.PendingAddedCanonicalConflict or
            ProvenanceState.PendingModification or
            ProvenanceState.PendingRemoval);
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
                $"| `{operation.Capability}` | {operation.Count} | " +
                $"{operation.TurnsGreenChange} task {operation.TurnsGreenTask} |");
        }

        Sha256(normalizedArtifact).Should().Be(
            checkpoint.ArtifactNormalizedSha256,
            "the human-readable provenance artifact must remain byte-accountable after LF normalization");
    }

    private static void RequireArtifactClaim(string artifact, string claim) =>
        artifact.Should().Contain(
            claim,
            "the human-readable provenance artifact must agree with live executable evidence");

    private static bool ContainsIdentifierToken(string text, string identifier) =>
        Regex.IsMatch(
            text,
            @"(?<![\p{L}\p{Nd}_])" + Regex.Escape(identifier) + @"(?![\p{L}\p{Nd}_])",
            RegexOptions.CultureInvariant);

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

    private static ProvenanceRow ClassifyPostGateProvenance(
        IReadOnlyDictionary<string, IReadOnlyDictionary<string, CanonicalRequirement>> canonicalRequirements,
        DeltaRequirement requirement,
        PostGateAmendmentCheckpoint checkpoint)
    {
        var predecessor = checkpoint.SupersededRuntimeViewPredecessors.SingleOrDefault(item =>
            item.Change == requirement.Change && item.Capability == requirement.Capability &&
            item.Requirement == requirement.Requirement);
        if (predecessor is null) return ClassifyProvenance(canonicalRequirements, requirement);
        requirement.Operation.ToString().ToUpperInvariant().Should().Be(predecessor.Operation);
        Sha256(requirement.Block).Should().Be(predecessor.DeltaBlockSha256,
            "an approved successor must never erase or reword its historical predecessor delta");
        var canonicalBlock = canonicalRequirements[requirement.Capability][requirement.Requirement].Block;
        Sha256(canonicalBlock).Should().Be(predecessor.CanonicalBlockSha256,
            "supersession is bound to the newest approved canonical block, not a general drift waiver");
        return CreateProvenanceRow(requirement, ProvenanceState.SupersededByApprovedSuccessor, canonicalBlock);
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
        SupersededByApprovedSuccessor,
        PendingAddition,
        PendingAddedCanonicalConflict,
        PendingModification,
        PendingRemoval,
        NewCapabilityOutsideCanonical
    }

    private sealed record ImmutableDocumentHistoryFixture(
        int SchemaVersion,
        string RecordFormat,
        string Normalization,
        string BaselineCommit,
        int BaselineCount,
        int BaselineBytes,
        string BaselineSha256,
        MutableHistoricalDocumentPath[] MutablePaths,
        string ActiveFreezeManifestPath,
        ImmutableHistoricalDocumentRecord[] Records,
        ImmutableHistoricalDocumentRecord[] AppendOnlyRecords);

    private sealed record MutableHistoricalDocumentPath(string Path, string Purpose);

    private sealed record ImmutableHistoricalDocumentRecord(string Path, int Bytes, string Sha256);
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
        string TurnsGreenChange,
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
        PostGateProposedSuccessor[] ProposedRequirementSuccessors,
        PostGateApprovedPendingEvidence ApprovedPendingEvidence,
        PostGateApprovedPendingSuccessor[] ApprovedPendingRequirementSuccessors,
        PostGateCompleteEvidence CompleteEvidence,
        PostGateRuntimeViewSuccessor[] ProposedRuntimeViewSuccessors,
        PostGateApprovedPendingEvidence ApprovedRuntimeViewEvidence,
        PostGateApprovedPendingSuccessor[] ApprovedRuntimeViewRequirementSuccessors,
        PostGateSupersededPredecessor[] SupersededRuntimeViewPredecessors,
        PostGateHistoricalArtifact[] HistoricalRuntimeViewContractArtifacts,
        PostGateAmendment[] Amendments,
        string ArtifactPath,
        string ArtifactNormalizedSha256);

    private sealed record PostGateHistoricalArtifact(
        string Change, string RelativePath, string NormalizedSha256);

    private sealed record PostGateSupersededPredecessor(
        string Change, string Capability, string Requirement, string Operation,
        string DeltaBlockSha256, string CanonicalBlockSha256);

    private sealed record PostGateRuntimeViewSuccessor(
        string Capability,
        string Requirement,
        string Change,
        string PredecessorChange,
        string PredecessorOperation,
        string SuccessorOperation,
        string Stage,
        string TurnsGreenTask,
        string PredecessorBlockSha256,
        string ProposedBlockSha256);

    private sealed record PostGateCompleteEvidence(
        string Stage,
        string ReviewedCheckpointCommit,
        string ReviewedCheckpointTree,
        string ApprovalEvidenceCommit,
        string ApprovalVerdictPath,
        string ApprovalVerdictSha256,
        string RefreezeManifestPath,
        string RefreezeManifestSha256,
        string ReviewRequestPath,
        string ReviewRequestSha256,
        int ImplementationTaskCount,
        string[] ImplementationTasks,
        string[] ExecutableEvidencePaths);

    private sealed record PostGateProposedSuccessor(
        string Change,
        string PredecessorChange,
        string Capability,
        string Requirement,
        string PredecessorOperation,
        string SuccessorOperation,
        string Stage,
        string TurnsGreenTask);

    private sealed record PostGateApprovedPendingEvidence(
        string Stage,
        string ReviewedCheckpointCommit,
        string ApprovalEvidenceCommit,
        string ApprovalVerdictPath,
        string ApprovalVerdictSha256,
        string ApprovalTask,
        string CanonicalSyncTask,
        string RegistryTransitionTask,
        string ImplementationTask);

    private sealed record PostGateApprovedPendingSuccessor(
        string Capability,
        string Requirement,
        string Stage,
        string HistoricalCanonicalBlockSha256,
        string CanonicalBlockSha256);

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
        ActiveReviewFreeze? ActiveFreeze,
        RejectedReviewFreeze[] RejectedFreezes,
        ArchivedReviewFreeze[] ArchivedFreezes,
        ReviewManifestProvenanceEntry[] Entries);

    private sealed record ActiveReviewFreeze(
        string Task,
        string RequestPath,
        string ManifestPath,
        string Disposition,
        string BaseCommit,
        string[] ContentRecordExcludedPaths,
        int ContentRecordBytes,
        string ContentRecordSha256);

    private sealed record RejectedReviewFreeze(
        string Id,
        string RequestPath,
        int RequestBytes,
        string RequestSha256,
        string RequestManifestPath,
        string ManifestPath,
        string Disposition,
        int LineCount,
        int ManifestBytes,
        string ManifestSha256,
        string BaseCommit,
        string VerdictPath,
        int VerdictBytes,
        string VerdictSha256);
    private sealed record ArchivedReviewFreeze(
        string Id,
        string Authority,
        string AuthorityEvidencePath,
        string RequestPath,
        string ManifestPath,
        string Disposition,
        int LineCount,
        int ManifestBytes,
        string ManifestSha256,
        string FrozenRawPorcelainSha256,
        string PathSortedSha256,
        string BaseCommit,
        string CheckpointCommit,
        string CheckpointTree,
        string[] ContentRecordExcludedPaths,
        int ContentRecordBytes,
        string ContentRecordSha256);

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
