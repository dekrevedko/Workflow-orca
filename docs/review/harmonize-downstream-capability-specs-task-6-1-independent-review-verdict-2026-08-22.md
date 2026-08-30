# Harmonization Task 6.1 independent review verdict

**Date:** 2026-08-22
**Target base:** `87f0b0dd1d0815e9334eb686eb52bb9c30d02513`
**Target tree:** `7e6162d3d66ef9faffbcf55d3839e3225211be7d`
**Target worktree:** 11 entries — 8 modified, 3 untracked, 0 staged
**Request:** `docs/review/harmonize-downstream-capability-specs-task-6-1-independent-review-request-2026-08-22.md`
**Manifest:** `docs/review/harmonize-downstream-capability-specs-task-6-1-dirty-manifest-2026-08-22.txt`

## Scope

This verdict covers only the Task 6.1 checkpoint. It does not complete Tasks 6.2–8.3, archive
`harmonize-downstream-capability-specs`, or authorize reshape Task 8.0.

## Method

Every anchor, count, and validation claim was recomputed independently. All execution ran in a
disposable `git worktree` at `X:/ocv61` created from the declared base and populated with the exact
11 frozen entries. The reviewed worktree was never modified, staged, or committed, and `HEAD` never
moved.

## 1. Freeze anchors

Recomputed from the live target and again from the clean disposable checkout; both reproduce
exactly.

| Anchor | Recomputed | Declared |
|---|---|---|
| Raw porcelain (`--untracked-files=all`) | 878 B · `8cb4edaf79b230ca5e2ed74cd55cc63c966a47e4b5b8b988145d0aec5d1cce3c` | identical |
| Content record | 1,657 B · `e6863f9ce56bfc630f6e422f8463fb8d07ff5c3ce85d2cb3c0ce3867291cd1a0` | identical |

The manifest file is byte-identical to the live porcelain output. All 8 tracked entries appear in
`git diff --name-only --no-renames HEAD`, so the manifest contains no status-only or index phantom
rows and the commit-real projection equals the manifest exactly.

## 2. Prior checkpoint chain

| Commit | Tree | Parent | Paths |
|---|---|---|---|
| `76c6340fa003bfd209cfadce7b4ea271d9d7a50c` | `b66c81cc…` | `5e8e25b9…` | 10 |
| `87f0b0dd1d0815e9334eb686eb52bb9c30d02513` | `7e6162d3…` | `76c6340f…` | 1 |

`87f0b0d` changes only `tests/OrcaCore.DeveloperSurface.Guards/Fixtures/review-manifest-provenance.json`,
and its tree equals the declared target base tree. The Task 5.3 entry records
`reviewState: "MissingApproval"` with `verdictEvidence: []` and `stateEvidencePath: null`; the
registry does not overstate an approval that does not exist. Task 5.3's committed-blob content
record was recomputed from `76c6340`'s blobs and reproduces `1,529 B · dac6b84204188a00fbae5421db5055b5332c4d17835ae7eef6f8095cf4557bfa`,
identical to its published historical record.

## 3. Task 6.1 claims

1. **Confirmed.** `docs/specs/04-requirements-core-runtime.md` gains exactly one heading
   `### CR-009a Authoring sessions have one explicit lifecycle`, placed between `CR-009` and the
   `## 4.2 Execution model` boundary. The letter-suffix convention has precedent (`CR-013a`,
   `DR-011a`).
2. **Confirmed with one qualification** — see finding Q-2. Every other clause was checked against
   `src/OrcaCore.Core/Building/AuthoringLifecycle.cs`: `BeginJoin` sets `JoinPending` under the
   operation gate; `CompleteJoin` commits, increments `epoch`, restores `Open`, and returns a fresh
   root handle; `Freeze` commits then sets `Frozen`; every lifecycle rejection precedes `commit()`;
   `Monitor.TryEnter(operationGate)` implements the single-owner mutation gate.
3. **Confirmed.** `SFE-AUTH-LIFECYCLE-001` through `-005` resolve consistently in
   `docs/specs/17-selected-mode-capability-matrix.md`, `openspec/specs/quality-and-verification/spec.md`,
   `src/OrcaCore.Abstractions/Instances/WorkflowDiagnosticCatalog.cs`, and the implementation. The
   `[document 17](17-selected-mode-capability-matrix.md)` link target exists.
4. **Confirmed.** The class trait moves from `[Trait("AC", "AC-021")]` to
   `[Trait("Requirement", "CR-009a")]`. `AC-021` lambda-mode safety retains independent coverage in
   `tests/OrcaCore.Core.Tests/Building/StagedWorkflowBuilderTests.cs`, so no criterion is orphaned.
5. **Confirmed.** The guard is declared on `OpenSpecCorpusGuards`, which carries
   `[Trait(GuardTraits.Disposition, GuardTraits.Infrastructure)]`, so it is inside the must-be-green
   lane. Non-vacuity is mutation-proven below. Residual limits are Q-3 and Q-4.
6. **Confirmed and machine-derived.** `RecoveryCrosswalkGuards` compares the recorded inventory to a
   live discovery with `recordedSourceRows.Should().Equal(discoveredSources)`, so the
   `OpenSpecCorpusGuards.cs` row moving 4 → 5 is recomputed, not asserted. Accounting moves by
   exactly one declaration: 336 physical sources, 1,384 declarations, 188 active files, 696 active
   declarations, 866 retired rows unchanged.
7. **Confirmed.** None of the 11 entries is under `src/`, `openspec/specs/`, a public API baseline,
   or the package manifest.

## 4. Validation reproduced from a clean checkout

| Lane | Result |
|---|---|
| `dotnet build OrcaCore.slnx -c Debug --no-incremental -m:1 -warnaserror` | 0 warnings / 0 errors |
| `dotnet build OrcaCore.slnx -c Release --no-incremental -m:1 -warnaserror` | 0 warnings / 0 errors |
| `pack-exact-package-feed.ps1` | 12 packages |
| `OrcaCore.Core.Tests` | 350 / 350 |
| Guards `Disposition=Infrastructure` | 216 / 216 |
| Guards `Disposition=ExpectedRed` | exactly 14, all `ExecutableBehaviorExpectedRedGuards.Scenario_HasOneRuntimeRecordedExactProductDriverAndPassingAssertion` |
| Guards, unfiltered | 230 total (216 + 14) |
| `OrcaCore.ProviderCertification` | 96 / 96 |
| `openspec validate --all --strict` | 18 passed / 0 failed |
| Harmonization ledger | 19 complete / 15 open / 34 total |
| `git diff --check` | exit 0 |

The OpenSpec provenance checkpoint is unchanged and remains semantically eligible: 176 rows /
46,211 B / `e1420f367a491e62e896f041f288b3235eeaf7647d721069cb71dda509b4021b`, 0 pending canonical
operations, 14 canonical capabilities / 547 B / `7165dac4…`, 14 preambles / 1,233 B / `595528c6…`,
16 active capability directories / 1,321 B / `5e8a9725…`, 11-entry historical removal catalog.

**Committability.** The target was committed inside the disposable worktree as a simulated
checkpoint. It produced exactly 11 changed paths, a clean worktree, and a 216/216 Infrastructure
lane, exercising the post-commit branch of `ValidateActiveReviewFreeze`. The phantom-entry defect
found in round 40 cannot recur on this manifest.

## 5. Mutation testing

Control before and after: the focused Task 6.1 guard and both crosswalk guards pass on the restored
corpus.

| # | Mutation | Result |
|---|---|---|
| M-1 | `CR-009a` heading renamed to `CR-009b` | RED |
| M-2 | `SFE-AUTH-LIFECYCLE-004` corrupted in the requirement body | RED |
| M-3 | `capture one immutable authored-graph snapshot` removed from the CR-009a block and reinserted verbatim under `CR-010` | RED |
| M-4 | evidence trait reverted to `[Trait("AC", "AC-021")]` | RED |
| M-5 | evidence member `CallbackHandle_ExpiresWhenCallbackReturns` renamed | RED |
| M-6 | canonical clause `SHALL leave the authored graph unchanged` reworded | RED |
| M-7 | crosswalk declaration count reverted 5 → 4 | RED |
| M-8 | `JoinPending` dropped from the state enumeration | RED |
| S-1 | Task 5.3 spot-check: friend-topology body copied byte-identically into `add-runtime-concurrency-limits` | RED, naming the competing owner |

M-3 is the decisive one: it proves the self-reviewed section boundary is real. The clause is still
present in the file, but outside the extracted block, and the guard fails. The first draft's
`###`-only terminator would have absorbed it.

S-1 is not part of Task 6.1. It was run because `76c6340` carries a 211-line guard addition into
this target's base without an independent verdict (finding Q-1), and this verdict should not
silently endorse unverified base content.

## 6. Provenance pin maximality

`currentWorktreeMatchPaths` was recomputed for all three historical entries against the target
worktree by hashing each historical row's path:

| Task | Historical rows | Recomputed maximal | Recorded | Match |
|---|---|---|---|---|
| 5.1 | 17 | 11 | 11 | exact, ordered |
| 5.2 | 12 | 4 | 4 | exact, ordered |
| 5.3 | 10 | 4 | 4 | exact, ordered |

Zero rows are byte-verifiable today yet left unpinned. Task 5.3's set falls from 9 to 4 because
Task 6.1 edits five files it had pinned. This is the predicted erosion of live pins, and it is
handled correctly: the drop is mechanically forced by the maximality rule, the refresh script is
mandated by Task 5.3's own ledger entry, and Task 5.3's committed-blob projection still reproduces,
so no provenance is actually lost.

## 7. Findings

### Q-1 (P2) — `MissingApproval` does not block a checkpoint commit

`ValidateReviewStateEvidence` requires only that a `MissingApproval` entry carry no verdict evidence
and no state-evidence path. It places no constraint on `checkpointCommit`. Task 5.3 therefore
records a real checkpoint (`76c6340`) with zero review evidence and the must-be-green lane is green.

The invariant that blocked a Task 5.2 checkpoint on an unapproved Task 5.1 base is hardcoded to
those two task identifiers in `ReviewCheckpointProvenance_BlocksTask52CheckpointUntilTask51ApprovalExists`.
It does not generalize. The gate built over rounds 39–42 protects exactly one edge and does not
apply to Task 5.3, Task 6.1, or anything later.

The repository owner authorized `76c6340` explicitly and the registry is honest about its state, so
this is a disclosed deviation rather than a concealed one. It is recorded here because CLAUDE.md
requires approval before the checkpoint commit, and because the structural protection is narrower
than it appears. Recommended fix: in `ValidateReviewStateEvidence`, require `MissingApproval` to
imply a null `checkpointCommit`, or add an explicit named owner-authorized exception row so the
waiver is data rather than an absent assertion.

### Q-2 (P2) — CR-009a widens the callback-local handle set beyond both OpenSpec sources

CR-009a states that `callback-local nested, branch, item, leased, and scope handles SHALL expire
when their callback returns`. Canonical `openspec/specs/workflow-authoring/spec.md` enumerates only
`A nested, branch, item, or leased handle`, and so does the owning active delta in
`openspec/changes/reshape-developer-facing-interfaces/specs/workflow-authoring/spec.md`.
`openspec/specs/quality-and-verification/spec.md` lists `SFE-AUTH-LIFECYCLE-004`
`ExpiredLexicalBuilderHandle` with no enumeration at all.

`scope` is not invented. `docs/specs/17-selected-mode-capability-matrix.md` line 1542 defines `-004`
as covering `a callback-local nested, branch, item, leased, or scope builder`, and
`src/OrcaCore.Core/Building/SelectedWorkflowBuilder.cs` creates and expires real `scopeHandle`
lexical handles at lines 585 and 610. CR-009a is the more accurate text.

The consequence is that the request's claim that CR-009a "mirrors the canonical `workflow-authoring`
lifecycle without adding semantics" is not literally true on this point, and the two normative trees
disagree with no approved change covering the wider reading on the `workflow-authoring` side. The
correction belongs in the `workflow-authoring` delta, not in CR-009a. This does not block Task 6.1.

### Q-3 (P3) — the trait correction is not enforced as an exclusion

Re-adding `[Trait("AC", "AC-021")]` alongside the new `[Trait("Requirement", "CR-009a")]` leaves the
guard GREEN (probe G-1). The guard asserts the presence of the correct trait but never the absence
of the incorrect one, so the specific defect Task 6.1 fixed can be reintroduced without detection.

### Q-4 (P3) — evidence binding proves text, not execution

Marking `CallbackHandle_ExpiresWhenCallbackReturns` as `[Theory(Skip = "temporarily disabled")]`
leaves the guard GREEN (probe G-3). The guard matches five member names as source text; it does not
require them to be discovered, unskipped, or passing. Related: `Trait("Requirement", ...)` occurs in
exactly one file in the whole test corpus and no guard, filter, or CI lane consumes it, so the
requirement-to-evidence binding is presently decorative. Task 6.3, which owns the acceptance
criterion, is the natural place to make it load-bearing.

### Q-5 (P3) — the content-record anchor is not inside the frozen target

The porcelain anchor is self-evidencing because the manifest file is itself a frozen entry. The
content-record hash `e6863f9c…` appears in no file within the target and reached the reviewer only
through the handoff message. It is reproducible from the target, and it is pinned by this verdict,
but a freeze whose strongest anchor lives outside its own evidence set is weaker than it needs to
be. Consider recording it in the `activeFreeze` fixture entry alongside `baseCommit`.

## 8. Operational note on landing this verdict

`ValidateActiveReviewFreeze` asserts `manifest.Lines.Should().Equal(currentProjection)` while the
worktree is dirty, so adding this verdict file to the reviewed worktree makes the live projection 12
entries against an 11-entry manifest. This was measured, not inferred: with the verdict present,
`OpenSpecCorpusGuards.ReviewManifests_PreserveRawGitOrderOrDiscloseSetOnlyEvidence` fails with
`contains 1 item(s) less`, naming the missing
`?? docs/review/harmonize-downstream-capability-specs-task-6-1-independent-review-verdict-2026-08-22.md`
row, and the must-be-green lane is red until the state is reconciled.

Commit the approved 11-path checkpoint first and add the verdict afterwards, or refreeze the
manifest to 12 entries before running the lane. This is a consequence of the new active-freeze
guard working as designed, not a defect in it.

## 9. Reviewer hygiene

The disposable worktree `X:/ocv61` was removed and pruned. The simulated checkpoint commit
`b9b2c763` is unreachable, referenced by no ref, and absent from `git log --all`. The reviewed
repository remains at `HEAD` `87f0b0dd1d0815e9334eb686eb52bb9c30d02513` with 11 worktree entries and
nothing staged. No commit was created in the reviewed repository by this review.

## Determination

The freeze anchors reproduce exactly, the manifest is phantom-free and equals the commit-real
projection, every one of the seven claims holds, all reported validation was reproduced
independently from a clean checkout, the new guard is mutation-proven non-vacuous including its
section boundary, the declaration accounting is machine-derived rather than asserted, and the target
was proven committable as an exactly 11-path checkpoint with a green lane. Q-1 and Q-2 are real and
should be scheduled, but neither is caused by Task 6.1 and neither makes this target wrong to
commit.

**Verdict:** **APPROVE**
