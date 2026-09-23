## Context

The repository has two normative trees (`docs/specs/` and `openspec/specs/`) plus active delta
changes. Earlier synchronization applied only the deltas named by one change and ran before later
approved amendments. That allowed canonical requirements to remain stale and allowed two active
changes to modify the same requirement headings without structural validation reporting a conflict.

`reshape-developer-facing-interfaces` is the authoritative product-contract change. It now owns the
complete event-routing/wait, durable-persistence/outbox, repository friend-graph, and durable
resource-governance deltas. This change retains only two unique capability deltas:
`event-driven-prototype` and `state-driven-runtime`. Its remaining work is cross-tree provenance,
numbered-requirement/acceptance coverage, documentation classification, and synchronization process.

The repository is greenfield: there is no released package or persisted-schema compatibility
contract to preserve. Historical plans and verdicts remain immutable evidence, but they do not
override current normative sources.

## Goals / Non-Goals

**Goals:**

- Give each canonical requirement heading one active change owner.
- Synchronize only independently approved deltas and prove their unchanged canonical baseline.
- Detect missing, duplicated, stale, and post-gate amendments across both normative trees.
- Preserve exact provenance for historical documents and review verdicts.
- Make freeze manifests reproducible across reviewers, including rename records.
- Add missing numbered requirements and acceptance criteria for already-approved authoring-session
  and failure-provenance semantics.

**Non-Goals:**

- Define or implement event routing, persistence/outbox, friend topology, resource governance,
  deletion remediation, observability, provider behavior, or other product runtime semantics owned
  by `reshape-developer-facing-interfaces`.
- Synchronize canonical specs before independent approval.
- Edit immutable historical reviews or archived record content.
- Add compatibility aliases, migration shims, or provisional-schema upgrade paths.
- Author product runtime source, behavior tests, samples, packages, or provider migrations.
  Documentation/OpenSpec corrections and narrowly scoped infrastructure guard tests listed in the
  task ledger remain in scope.

## Decisions

### 1. One active delta owner per capability requirement

`reshape-developer-facing-interfaces` exclusively owns `event-routing-and-waits`,
`durable-persistence-and-outbox`, `repository-foundation`, and `durable-runtime` for the current
Section 7 contract. This change contains no delta directory for those capabilities, even when an
earlier harmonize version held byte-identical text. Byte identity is not safe coordination: either
copy can later diverge and the second synchronization would silently overwrite the first.

This change owns only `event-driven-prototype` and the non-overlapping `Ephemeral mode has explicit
limitations` requirement in `state-driven-runtime`.

Alternative considered: retain equal duplicate deltas and require a fixed sync order. Rejected
because structural strict validation does not prove equality or prevent later divergence.

### 2. Canonical synchronization is approval- and provenance-driven

Synchronization occurs only after a new independent approval of the reduced change. It applies the
two approved delta files to their exact unchanged canonical requirement headings, records the
canonical diff, applies any approved `Purpose` wording manually, and strict-validates the result.

The gate also enumerates every canonical capability and every active delta heading. It rejects an
unexplained missing capability, an uncoordinated duplicate owner, or canonical content with no
approved source change. Structural validity remains necessary but is never reported as semantic or
provenance approval by itself. Canonical preambles are independently pinned per capability after LF
normalization, including each `## Purpose` block, so requirement-only synchronization cannot erase
or rewrite unowned preamble text without a reviewed fixture refreeze.

Review approval uses an explicit two-phase transition so the must-be-green infrastructure lane
never depends on a commit naming itself. `ApprovalAwaitingEvidenceCommit` registers the external
`APPROVE` verdict byte-for-byte while keeping the dependent checkpoint blocked and leaving the
evidence commit unset. The first distinct checkpoint commits that verdict and transition record;
only a following mechanical activation may set `Approved` and pin the already-existing evidence
commit. Immutable earlier `REJECT` verdicts remain registered in both states.
A committed independent approval is valid only when its evidence commit is a single-parent child of
the exact reviewed target and introduces the governing verdict. The historical Task 5.3 owner
authorization is explicitly retroactive: its evidence commit must introduce the verdict and descend
from the reviewed target, but cannot truthfully claim that target as its direct parent.
A first-pass `APPROVE` is valid without any preceding `REJECT`; the awaiting-evidence state
therefore requires approval evidence but deliberately does not require rejection history. When a
later remediation round is also approved, every immutable approval remains registered and the
newest approval verdict governs the transition.
After an active freeze is checkpointed, its request, raw manifest, base, checkpoint, tree, and
scoped content record move into the permanent `archivedFreezes` registry before another active
freeze replaces it. Every archived freeze records `IndependentReview` or `OwnerAuthorization` plus
one registered immutable authority-evidence verdict; independent states and freezes SHALL reject an
`-owner-approval-verdict-` evidence path, while owner-authorized records require it. Archived freezes
remain executable committed-object evidence without conflating a remediation stage with the
task-level verdict state or leaving an older manifest undisclosed.

`stateEvidencePath` identifies the verdict governing the current transition.

`MissingApproval` is reserved for work that has not checkpointed: both checkpoint commit and tree
must be absent. When the repository owner explicitly authorizes a checkpoint without an independent
review, the registry instead uses the separately named
`OwnerAuthorizationAwaitingEvidenceCommit` / `OwnerAuthorized` transition and preserves a dated
owner-disclosure verdict. This makes the authority source visible without presenting owner approval
as independent review.

Task 5.2 content materialized before that approval checkpoint is not hidden by commit-subject
conventions. The provenance registry permanently records every such pre-approval content commit,
and the guard rediscovers every commit on the current reviewed lineage that touches any of the three
owned canonical paths between the Task 5.2 base and the parent of Task 5.1 approval evidence. This
catches split landings without scanning unrelated refs or depending on a checked task row. That
record is historical content provenance, not an approved Task 5.2 checkpoint; a later approved
target and its evidence commit must still descend from committed Task 5.1 approval evidence.

Canonical enumeration and active-delta ownership are separate inventories. A canonical capability
does not have to retain an active delta after its owning change is archived. Archival changes the
active provenance record and therefore requires a reviewed fixture refreeze, but no invariant may
make the archived state permanently invalid. Synchronized-removal provenance is a permanent catalog
that embeds the exact normalized historical canonical block and pins its hash. Currently active
synchronized removals must be a subset of that catalog, so archiving a change cannot require a C#
source-constant rewrite or erase already-reviewed history. The originating commit remains optional
corroborating metadata; when the object is available, validation reads it directly without requiring
ancestry from `HEAD`, and correctness does not depend on that object surviving integration-history
rewriting. CI executes the complete must-be-green infrastructure guard disposition while
intentional expected-red guards remain a separate lane.

A checkpoint is provenance-complete only when its exact target has immutable approval evidence.
The existence of a review request, green validation, a completed task checkbox, or a commit is not
approval. A dependent checkpoint remains blocked until its base checkpoint has either an
independent verdict or an explicit, dated owner-approval disclosure. The machine-readable guard
records `MissingApproval`, `Rejected`, `ApprovalAwaitingEvidenceCommit`, and `Approved` states
honestly, retains every immutable verdict with an exact byte hash, and requires approved evidence
to be preserved in a distinct repository checkpoint before a dependent checkpoint may descend
from it. Every non-missing state names one `stateEvidencePath` registered in that same verdict set:
`Rejected` requires `REJECT`, while both approval states require `APPROVE`. The exact independently
reviewed commit and tree are recorded separately from an older dirty-manifest checkpoint so later
remediation cannot mislabel one evidence shape as the other.

Alternative considered: rely on `openspec validate --all --strict`. Rejected because it validates
each change independently and permits contradictory or duplicated active deltas.

### 3. Late amendments re-enter every affected gate

An amendment approved after its original implementation-section gate closed must explicitly map to
canonical OpenSpec, numbered requirements, acceptance criteria, implementation tasks, executable
evidence, refreeze, and independent approval. A completed historical gate does not waive later
reconciliation.

The workflow records the affected capability/requirement IDs and the owning change. It does not
infer coverage from a broad task description or from the existence of a passing suite.

The machine-readable post-gate record carries eight mandatory stages: amendment approval,
canonical OpenSpec, numbered requirements, acceptance criteria, implementation tasks, executable
evidence, refreeze, and independent approval. Task references record their expected open/complete
state and resolve through exactly one active or dated archived change record. The record embeds the
exact requirement identities and validates them against that resolved change, so archival does not
erase the amendment contract or force a permanently active delta. Implementation task ranges carry
an exact count, and approval evidence is normalized before task-bound verdict validation. The known
Section 7B case records all 18 `developer-facing-surface` requirements. Task 5.1 has completed its
seven canonical operations, while the numbered requirement/acceptance corrections remain under
task 7.3. Closing this process task therefore cannot be mistaken for completing those downstream
edits.

### 4. Numbered requirements and acceptance criteria remain bidirectional

The authoring-session lifecycle and `WorkflowFailure` occurrence provenance already exist in the
approved reshape contract. Harmonization adds stable numbered requirements and acceptance criteria
that mirror those semantics without exposing implementation internals. Each new requirement maps to
an executable guard, and each acceptance criterion maps back to one normative requirement.

The failure-provenance acceptance mapping draws on the synchronized quality-and-verification
requirement, the structured-fiber-execution ordering contract, and the public-contract companion. It
must cover the single owning join failure, authored-branch and dynamic-item ordering keys,
non-negative item indexes, per-cause provenance retention, and rejection of unknown, missing, or
malformed fixed-codec occurrence data rather than naming only one contributing normative source.

`docs/specs/17-public-authoring-contract.cs` changes only if the review concludes that these
semantics alter its compile-shaped public surface. Internal lifecycle state alone is insufficient
reason to add a public declaration.

Task 6.5 resolves that decision by keeping the companion byte-unchanged. `AuthoringSessionState`,
the lifecycle session, lifecycle/join handles, lexical token, and shared workflow-authoring session
remain internal implementation types in `OrcaCore.Core`; that assembly's exact v1 API baseline has
no exported declarations. The existing twelve-assembly public API baseline independently rejects
any future visibility leak, while the companion continues to describe only application-authored
types and signatures. The companion's reviewed SHA-256 is owned by guard source; the mutable public-
contract fixture must reproduce that pin and cannot authorize coherent documentation drift by
re-pinning itself.

### 5. Active documentation and immutable history have different rules

Active guides, architecture, implementation, and readiness documents describe only the current
contract. Deferred capabilities remain discoverable through the future-capability registry but are
not shown as usable APIs. Removed concepts retain no active alias or how-to path. Newly approved
Section 7B buffering, fanout, start-or-deliver, and durable publish are not misclassified as deferred.

Task 6.6 gives that registry one exact cross-tree identity: `docs/specs/13-phasing-and-open-questions.md`
§13.4, "Future-capability registry". Its deferred-capability table records future promises and their
re-entry gates, while its separate removed-concepts subsection keeps retired names searchable
without treating them as future work. Canonical OpenSpec and the active deltas that still own those
requirements cite the same path and section name. Harmonization owns this name and cross-reference;
reshape task 9.6 remains the owner of final registry membership.

Task 6.6 post-review remediation makes provenance refreshes append-only in effect: guard source
permanently catalogs every superseded provenance artifact path and normalized hash before the
mutable fixture points at its successor. Removed-concept classification uses identifier boundaries
rather than Markdown spelling. The dated remediation record also discloses that Task 6.6 authority
predated the canonical gate, while its citation-only canonical/delta sync was prepared before the
exact 22-path target received independent approval; it preserves the exact sync diff and subsequent
checkpoint, approval-evidence, and activation sequence without recasting it as approval-first work.

Task 6.6 second post-review hardening makes those controls exhaustive and byte-explicit. A dated
addendum preserves the literal backticks in the two citation-only synchronization fragments without
rewriting the approved remediation record. The provenance guard enumerates every top-level
`*openspec-provenance-*.md` artifact and requires each to be either the fixture's current record or a
permanently catalogued predecessor. Removed-concept tokens are rejected across the complete §13.4
future-work region before the removed-concepts subsection, including its preamble.

Task 7.1 re-runs the original positive-call classification over root guidance, active documentation,
canonical OpenSpec, and active change proposals, designs, ledgers, and deltas. The dated checkpoint
record reports zero positive call forms while preserving concise deferred-capability notes and exact
§13.4 re-entry links in the ephemeral, Kubernetes scheduler, and Orleans guides. The same slice closes
Task 6.6 findings HH-1 and II-1: provenance-artifact discovery is recursive across all of
`openspec/changes/**`, and removed identifiers are prohibited everywhere outside the exact removed-
concepts subsection rather than treating every later subsection as removed territory.

The Task 7.1 review remediation preserves each rejected target under its own immutable raw-order
manifest while byte-pinning the request and `REJECT` verdict that produced it. The final source-record
companion uses ordinal path comparison and the executable guard enforces its 86-row order, LF-only
encoding, exact digest, and the scan artifact's exact real companion path. The companion is a
pre-finalization scan snapshot: its
`openspec/changes/harmonize-downstream-capability-specs/design.md` and
`openspec/changes/harmonize-downstream-capability-specs/tasks.md` rows intentionally predate their
final self-describing remediation text and therefore do not represent the checkpoint-tree digest.

Task 7.2 makes immutable-history classification executable. A machine-readable catalog preserves an
LF-normalized baseline derived from the exact committed blobs at the Task 7.2 base. Guard source owns
that baseline's count and digest, so checkout line-ending transforms cannot redefine it. Every later
record under either historical root belongs to one family-independent permanent append-only set. While
uncommitted, the catalog's active freeze manifest must name the record. After checkpoint, every Git
addition of that path must reproduce the catalogued normalized bytes. Re-adding identical content is
allowed; any differing addition fails. The guard also derives every post-baseline addition from Git history
and requires that path to remain present and cataloged; deletion or relocation requires a separately reviewed
tombstone mechanism, which the current contract does not provide. Both Git addition-history queries use
`--full-history`, so a record added and later deleted on a merged side branch remains visible. The exact
active archive index and reusable review template remain the only mutable surfaces.
Validation also replaces the leased-retry fixtures' second-attempt race against the inline start
operation with observation of the real workflow instance terminal state; the start operation can
complete before the scheduler publishes `SecondStarted`, while the persisted instance is the
authoritative completion boundary.
The first protected body remains cancellation-ignoring after physical release, retaining coverage of
late-return fencing without changing the runtime's select-once deadline arbitration. Terminal-status
observation is delayed and bounded so the test neither spins nor hangs when neither side progresses.
The same validation pass makes the durable metric-catalog listener use thread-safe collection
semantics because `MeterListener` may publish instruments concurrently.

Dated reviews and archived plans remain byte-immutable. Classification, supersession, and current
routing live in active indexes or new dated records. The current contract forbids relocating a
historical path. A future reviewed tombstone mechanism may define how a replacement path preserves
predecessor identity and Git provenance; until then the original path remains present and cataloged.

### 6. Freeze hashes use one explicit byte pipeline

A review freeze records only paths that can enter the checkpoint commit. Its commit-real entry set
is the union of `git diff --name-only --no-renames HEAD` and
`git ls-files --others --exclude-standard`. The manifest starts from
`git status --porcelain=v1 --untracked-files=all --no-renames` and retains, in Git-emitted order,
only records whose paths belong to that entry set. This excludes index- or attribute-only status
entries whose worktree bytes equal `HEAD`; such entries cannot appear in the resulting commit and
therefore cannot be claimed by its frozen manifest.

The raw anchor is SHA-256 over that exact ordered commit-real manifest using the review packet's
stated line-ending convention. The normalized comparison anchor uses exactly:

```text
commit-real Git-order manifest
| read one status/path record per LF line
| remove one trailing CR from each record
| sort records by ordinal byte order
| join with LF and one final LF
| SHA-256
```

The freeze uses `--no-renames`, so a move is represented by the same delete/add path pair the commit
projection can verify. Reviewers record both the commit-real entry count and normalized-line count.
PowerShell culture sorting, anchoring unfiltered status output, path-only sorting, and omission of
the final LF are different algorithms and SHALL NOT be compared to this anchor.

A checked-in human-readable dirty manifest that claims to reproduce the raw anchor SHALL retain
the filtered Git-emitted order. A path-sorted, status-grouped, or unfiltered status manifest is
non-authoritative evidence even when its entry count and byte length happen to match another
projection; its digest SHALL NOT be presented as the commit-real raw anchor.

Every harmonization dirty manifest is registered in a machine-readable fixture as either
`RawGitOrder` or `SetOnlyPathSorted`. New `RawGitOrder` captures use the commit-real filter above.
The registry pins the manifest bytes and digest, the frozen raw digest, and the counterfactual
path-sorted digest. A mutation regression both removes a synthetic status-only phantom and
path-sorts a known raw manifest, requiring the corresponding guard to reject either regression.
Historical set-only evidence remains
immutable and is disclosed as such; for a committed target, the guard independently validates the
exact changed-path set and commit tree against the recorded base instead of relabeling the manifest.
Every historical content record embeds its exact status, path, byte-count, and SHA-256 rows and
recomputes the aggregate from those rows. When the reviewed target is later committed, the historical
record SHALL equal the raw commit-blob projection whenever those bytes are identical; a deliberately
checkout-filtered projection is separate environment evidence and SHALL NOT be used to rewrite or
invalidate the published dirty anchor. An uncommitted rejected target remains reproducible from its
recorded rows even after the live worktree moves. Rows explicitly marked as still independently
verifiable from the current worktree are recomputed as the maximal matching historical-row set and
checked against their current bytes, so deleting an available pin or coherently rehashing surviving
historical evidence fails. Canonical OpenSpec markdown, the Section 7
declaration crosswalk, and the public-authoring companion use repository LF attributes so future
checkouts do not reintroduce line-ending-only provenance drift.

Because legitimate later work can change a currently matching historical row, the freeze procedure
SHALL run `tests/OrcaCore.DeveloperSurface.Guards/refresh-review-manifest-current-matches.ps1`
before focused guards and again before final anchors. The script changes only
`currentWorktreeMatchPaths`; historical rows, manifests, and aggregate anchors remain immutable.
`-Check` is the non-mutating CI/reviewer form. A stale fixture still fails closed with the exact
refresh command in its diagnostic, but ordinary planned edits no longer leave the recovery step
implicit.

A new dirty review manifest cannot hash the provenance fixture that registers that same manifest
without creating a self-reference. Schema 8 therefore carries one `activeFreeze` descriptor plus a
scoped content record covering every manifest path except the provenance fixture itself; that exact
singleton exclusion is enforced. Before commit, the guard binds its raw manifest byte-for-byte to
current commit-real porcelain and recomputes the scoped record from worktree bytes on the declared
base. After the approved checkpoint commit, the same state is green only when `HEAD^` is that base,
the commit's exact path set equals the manifest, and committed blobs reproduce the scoped record.
The next mechanical provenance transition converts it into an immutable historical entry using
committed blob hashes and clears `activeFreeze`; this never exempts a manifest from explicit
disposition.

Immutable review Markdown may retain intentional two-space hard line breaks that were already part
of a frozen record. The exact repository-attribute allowlist therefore contains only
`docs/review/**/*.md whitespace=-trailing-space` for that narrow historical surface. The companion
baseline guard pins the complete four-line `.gitattributes` file in order; a repository-wide or
otherwise broadened whitespace exemption is rejected.

Git status cannot represent empty untracked directories. Every freeze therefore records a third
anchor over repository-relative capability-directory paths matching
`openspec/changes/*/specs/*/`. Paths use `/`, retain one trailing `/`, are sorted by ordinal byte
order, and are joined with LF plus one final LF before SHA-256. The packet records the directory
count and hash and fails if any enumerated capability directory lacks `spec.md`. This inventory is
recomputed before and after validation alongside the two status anchors.

For the planning target immediately before this design was created, the authoritative normalized
projection contained 451 records and hashed to
`12332f853ca307a4bfac07b3f706854db7aafa7d338f34967ef6ab50fda587f3`.
This value is provenance evidence for that pre-design target, not the hash of a later freeze.

### 7. Empty and impossible guards receive explicit dispositions

Every empty capability directory is resolved as either a missing delta or stray directory before
freeze. The known `add-runtime-concurrency-limits/specs/state-driven-runtime/` instance was verified
as stray: that completed change declares only `runtime-resource-governance`, OpenSpec reports only
that delta, and its `state-driven-runtime` task reference describes canonical composition rather
than a second delta. The empty directory was therefore removed during planning remediation.
Namespace-pinned forbidden-symbol entries that cannot address a real historical or current owner
are corrected or deleted and receive a regression against the exact qualified owner. Neither an
empty directory nor an impossible negative guard may silently satisfy completeness.

### 8. Task 7.3 reconciles the approved Section 7B contract without widening product scope

Task 7.3 updates exactly the 22 stale active sources assigned by the Task 3.1 sweep; the separate
Orleans future-hosting note remains owned by Task 7.4. The reconciled record treats durable
`IWorkflowEventIngress`, its direct/correlation/definition-fanout/start-or-deliver route union,
caller-created global inbound event identity, retained pre-wait ownership, broker acknowledgement
after `Accepted` or `Duplicate`, and durable authored `Publish` through the application-registered
`IWorkflowEventDispatcher` as current behavior.
It preserves `orcacore-json-v1`, the exact role-specific host/provider graph, application-facing
absence of broad statistics and public archive/purge, and provider/operator ownership through
`IWorkflowOperationalStore` and `IWorkflowProviderMaintenanceStore`. Numbered requirements in
documents 05 and 12 are the explicit contract and acceptance owners; Task 7.5 still owns the
recurring repository-wide stale-negative scan.

The dated Task 7.3 artifact records an LF-normalized SHA-256 for every reconciled source, and guard
source pins that artifact. This makes every one of the 22 reviewed documents durable evidence rather
than relying on a vocabulary denylist that can miss reworded stale claims. Reconciliation preserves
unrelated timeout, collation, statistics, and dispatch-hook obligations. Acceptance-criterion traits
belong to the product, provider, hosting, and engine tests that exercise the behavior; the Markdown
corpus guard cannot satisfy product acceptance coverage by reading its own documentation.

Only the owner of a reviewed change that intentionally edits one of these 22 sources may refresh its
recorded hash. Task 7.4 or Section 8 may refresh a row only in the same frozen target that
intentionally edits the source and updates the artifact row, guard-source artifact digest, and
review evidence; neither may perform a mechanical follow-up refresh for an earlier unreviewed edit.
Task 7.5 review remediation corrects the two remaining legacy event-result statements in DU-055 and
AC-005 and refreshes only their Task 7.3 source rows plus the guard-owned artifact digest.

### 9. Task 7.4 keeps Orleans future hosting separate from superseded implementation plans

Task 7.4 retains one active Orleans future-hosting boundary at
`docs/orleans-engine/README.md`. It is a non-authorizing note aligned with the selected durable
contract: one ordinary cold-capable `Wait`, caller-created inbound identity, the four-route ingress
union with retained pre-wait ownership, transactional workflow-event outbox dispatch through the
application-registered dispatcher, fixed-codec payloads, and exact application/runtime/hosting/
provider ownership.

The 25-file superseded plan remains immutable under
`docs/archive/plans/orleans-engine-pre-v1/`; the active note links to it but does not promote its
provisional tasks or surface into current work. A dated Task 7.4 artifact pins every archived file
and the single active note. Any Orleans implementation still requires a new independently approved
OpenSpec change before projects, packages, migrations, or implementation tasks are added.

Task 7.4 review remediation makes that boundary independent of a whole-document hash: the guard
pins the numbered new-change prerequisite and Orleans-only adapter ownership semantically, derives
the 25-file archive from disk as well as the immutable-history fixture, and permits no additional
active task-ledger block that mentions Orleans implementation work.

### 10. Task 7.5 makes active documentation classification recurring

Task 7.5 turns the Task 3.1 classifications into a recurring active-tree gate. The gate reuses the
same evolving corpus and positive-call expressions as Task 7.1, excludes immutable `docs/archive/`
and `docs/review/` evidence, and pins the complete 23-source stale-negative fixture plus its historical
pre-reconciliation commit. The replay must equal the reviewed 56-result path/line/classifier record
and exercise every classifier, while the current active tree must produce no finding. Explicitly
superseded task/proposal sentences remain searchable history rather than being mistaken for current
guidance.

The first recurring run also found and corrected one stale status paragraph outside the Task 3.1
list: `docs/implementation/README.md` still described Section 7B as pending. The guard scans the
whole evolving active corpus, so future files and newly worded stale assertions are not limited to
the original 23-path fixture.

Task 7.5 review remediation broadens legacy event-surface, terminal-result, routing, publish, and
pre-wait phrase coverage. The artifact's 86-source value remains evidence of the reviewed snapshot,
not a live equality: benign additions and normal archival are accepted when the re-enumerated corpus
contains no positive removed/deferred call or stale Section 7B claim.
The dated `task-7-4-and-7-5-review-remediation-2026-09-22.md` artifact records the rejected freeze,
the exact fixes, and their mutation evidence without authorizing the superseding checkpoint.

## Risks / Trade-offs

- **[Removing duplicate deltas appears to discard approved text]** → Verify the authoritative
  reshape delta is a requirement-by-requirement superset before deletion and run a cross-change
  duplicate-heading scan after every revision.
- **[Canonical synchronization can race active reshape edits]** → Require both changes to reach
  their approval gates, freeze exact inputs, and synchronize in recorded ownership order.
- **[Vocabulary checks confuse deferred, removed, and newly approved behavior]** → Maintain three
  explicit classifications and test positive as well as negative usages in the active tree.
- **[History-preserving moves are platform- or similarity-sensitive]** → Verify `git log --follow`;
  when exact rename detection is impossible, record predecessor path/commit without altering the
  archived bytes.
- **[Two manifest hashes become incomparable]** → Store the exact pipeline, entry counts, expanded
  line counts, line endings, sort order, and final-newline rule with every freeze.
- **[Process work expands indefinitely]** → Limit this change to the tasks in its reduced proposal;
  product and Section 7B implementation stays in reshape.

## Migration Plan

1. Approve the reduced proposal, two unique deltas, this design, and tasks independently.
2. Resolve the empty delta directory and cross-change provenance checks.
3. Synchronize only the two approved harmonize deltas to canonical OpenSpec.
4. Add the mapped numbered requirements and acceptance criteria.
5. Reconcile active documentation, future-registry links, negative guards, archive provenance, and
   active vocabulary checks without editing historical content.
6. Validate all active changes, freeze with the explicit manifest pipelines, and obtain immutable
   exit approval.
7. After zero drift, create the mandatory harmonization checkpoint commit. Section 8 remains blocked
   until reshape also completes its own approved checkpoint.

Rollback is planning-only: revert this change's canonical/documentation synchronization and retain
the prior immutable verdicts. No product code, released package, or persisted data migration is
involved.

## Open Questions

- Is the empty runtime-concurrency delta directory a dropped artifact or a stray directory?
- Can the Phase-0 kickoff prompt move be represented as a Git rename at the final target, or must a
  separate immutable predecessor record carry its provenance?
