# Harmonization Tasks 7.2 HHH-1 and 7.3 contract-hardening independent review verdict

**Date:** 2026-09-21
**Reviewer:** independent review
**Scope reviewed:** the fifty-two-entry freeze on base
`89e3ed55357e849852c1a0f6fefa2124433d7e30`, named by
`harmonize-downstream-capability-specs-task-7-2-hhh-1-and-task-7-3-contract-hardening-dirty-manifest-2026-09-20.txt`.
It remediates the Round 66 findings HHH-1, III-1, and JJJ-1.
**Authorization requested:** one combined checkpoint for Task 7.2 approval-evidence lineage
hardening and the remediated Task 7.3 active-documentation contract. This verdict authorizes that
checkpoint for the exact frozen target only.

## Summary

Every Round 66 finding is closed and mutation-proven:
- **HHH-1:** an independent approval now needs a single-parent evidence commit whose parent is the
  exact reviewed target and which adds the governing verdict. Wrong-commit, merge, wrong-parent,
  and contains-but-does-not-add probes are all red. A simulated checkpoint, evidence commit, and
  activation still pass the full Infrastructure lane.
- **III-1:** AC-116 names all five closed rejection variants, and dropping any one is red.
- **JJJ-1:** AC-114, MG-030, and PR-040 now match source and canonical text. The corpus guard
  rejects the repository's AC-trait form. The pin-refresh authority is recorded in the design, the
  ledger, and the artifact, and weakening it in any of them is red.
- **Provenance:** the Round 66 request, manifest, and verdict are byte-exact and registered in all
  three required places.
- **Validation:** every reported count and both freeze anchors reproduce exactly.

No blocking defect was found. One P3 bundle is recorded (KKK-1).

## Method

At the owner's request, the frozen target was first copied byte-for-byte into a dedicated review
worktree, `X:/Projects/GitHub/Workflow-orca-review-hhh-1`, detached at `89e3ed55`, so the main
worktree could move on. This verdict was written there. Its porcelain and scoped content record
were verified against the frozen anchors before and after probing.

All builds, tests, and mutations ran in two further disposable detached worktrees holding the same
fifty-two entries: one for validation and checkpoint simulation, one for mutation probes. Probe
commits and the two synthetic commit objects (`701558a2…`, `f00e593c…`, made with
`git commit-tree`) are unreferenced. The review created no ref and never moved any branch; section 7
records the owner's checkpoint commit made during the review.

## 1. Freeze anchors and provenance

| Anchor | Claimed | Reproduced |
|---|---|---|
| Raw porcelain, `--untracked-files=all` | 3,623 B, `2e68da71…32cb` | identical, byte-for-byte |
| Scoped content record | 51 rows, 7,147 B, `8f1195d4…0bbd` | identical |
| Entries | 43 modified, 9 untracked, 0 staged | identical |
| Simulated checkpoint | parent `89e3ed55`, tree `9c1ac465…` | identical, 52 paths (9 A / 43 M) |

- The 16 archived freezes and 11 registry entries reproduce from committed objects, and every
  declared current-match pin equals the maximal match set (the `-Check` emulation is exact).
- The Round 65 and Round 66 packets are byte-exact. The Round 66 request is 6,368 B `342b0ddb…`,
  its manifest 3,218 B `d26166c6…`, and its verdict 12,808 B `6da026a0…`.
- The Round 66 verdict is a Task 7.2 `REJECT` row, the rejected freeze
  `7.2-bbb-1-and-7.3-remediation-rejected` has 49 lines, and its three files are in
  `appendOnlyRecords` with LF-normalized bytes.
- The new request (6,418 B `6031b816…`) and manifest are catalogued, and
  `activeFreezeManifestPath` names that manifest.
- Relative to the rejected Round 66 tree, exactly twelve paths changed. They are the three new
  review records, documents 09, 10, and 12, the Task 7.3 artifact, `design.md`, `tasks.md`, the two
  history fixtures, and `OpenSpecCorpusGuards.cs`.
- Scope contains no `src/**`, canonical `openspec/specs/**`, or Orleans path.

## 2. Validation

Every claimed result reproduced:

| Lane | Result |
|---|---|
| Debug and Release non-incremental `-warnaserror` builds | 0 warnings, 0 errors |
| Exact package feed | 12 packages |
| Core / Ephemeral / Durable / Acceptance / Hosting / ProviderCertification | 350 / 79 / 99 / 37 / 24 / 96 |
| PostgreSQL / SQL Server / Integration | 101 / 72 / 11 |
| Guards, full project | 224 passed, 14 failed, all 14 `ExecutableBehaviorExpectedRedGuards.Scenario_*` |
| `Disposition=Infrastructure`, Release | 224/224 |
| Strict OpenSpec | 18/18 |
| Harmonization ledger | 27 complete / 7 open / 34 (accounting guards green) |
| `git diff --check`, worktree and simulated commit | clean |
| Guards on the committed simulated checkpoint | 224 passed plus the same 14 expected-red |

## 3. Part A: approval-evidence lineage (HHH-1)

`ValidateCommittedApprovalEvidence` now checks, in order:
- the id resolves to a commit and is the exact full lowercase object id;
- `rev-list --parents` gives exactly one parent;
- for `Approved`, that parent equals `reviewedTargetCommit`;
- for `OwnerAuthorized`, the reviewed target is an ancestor instead;
- the evidence commit is an ancestor of `HEAD`;
- the committed verdict equals the registered text;
- `diff-tree` reports exactly `A` for the verdict path.

All ten independently approved entries have evidence parent equal to reviewed target, and all
eleven entries add their governing verdict. Task 5.3's evidence commit `9f4fa0b5` has parent
`603dffc0`. It descends from target `76c6340f`, and its owner disclosure says a later mechanical
transition may first preserve it.

| Probe | Mutation | Result |
|---|---|---|
| A1 | Task 7.2 evidence -> activation `89e3ed55` | **red**, parent is not the reviewed target |
| A2 | Task 7.1 evidence -> `89e3ed55` | **red**, parent |
| A3 | Task 6.6 evidence -> Task 7.2 evidence commit `08781777` | **red**, parent |
| A4a | Task 7.2 -> merge of `421c39b1` and `89e3ed55` with the evidence tree | **red**, three ids, not one parent |
| A4b | Task 7.2 -> single-parent commit on `e046e04a` that adds the verdict | **red**, parent |
| A4c | Task 7.2 target and evidence both shifted one commit later | **red**, reviewed-tree immutability |
| A5a | Task 5.3 -> `87da8c2b`, which descends and contains but does not add the verdict | **red**, addition |
| A5b / A5c | Task 5.3 -> `87f0b0d` / its own target, neither containing the verdict | **red**, names the task and path |
| A5d | Task 5.3 target moved after its evidence commit | **red**, reviewed-tree immutability |
| A6 | Task 6.6 relabelled `OwnerAuthorized` to reach the relaxed branch | **red**, approval count |
| R1 / R2 | Round 65 / Round 66 `REJECT` row removed from Task 7.2 | **red** |
| R3 | Round 66 rejected freeze removed | **red**, undispositioned manifest |
| R4 / R4b | Round 66 `verdictBytes` +1 / Round 65 `requestSha256` zeroed | **red** |
| R5 / R6 / R6b | Round 66 verdict row removed / Round 65 bytes +1 / Round 66 request row removed | **red** |
| R7 / R8 / R9 | Round 66 verdict +1 byte / deleted / flipped to `APPROVE` | **red** |
| R10 | one line removed from the Round 66 manifest | **red** |

A4b isolates the parent rule, because that commit adds the verdict. A5a isolates the addition
rule, because it satisfies ancestry and containment. Only one commit ever introduces a given
verdict, so each entry's evidence commit is now unique.

## 4. Post-approval sequence

To confirm the stricter rule does not block the next step, the simulated checkpoint `7f6471cd`
(tree `9c1ac465`) received a synthetic `APPROVE` evidence commit `b811b105`. That commit adds the
verdict, its `appendOnlyRecords` row, the archived freeze `7.2-hhh-1-and-7.3-contract-hardening`,
and Task 7.2 in `ApprovalAwaitingEvidenceCommit`. A mechanical activation `eeb08566` then set
`Approved` with the full evidence id. The worktree was clean, and `Disposition=Infrastructure` was
224/224.

## 5. Part B: contract corrections (III-1, JJJ-1)

Checks against source and canonical text:
- **AC-116** lists `EventConflict`, `DirectInstanceNotFound`, `DirectInstanceTerminal`,
  `StartConflict`, and `FanoutLimitExceeded`. This matches EV-012 and the AC-116 reflection test.
- **AC-114** again says a crash before the consuming transition leaves the wait `Active` and the
  accepted record re-matchable.
- **MG-030** now names exactly what `WorkflowOperatorStatistics` returns: groups by definition,
  version, and status; active waits by definition and event name; and stuck instances by definition.
  Its pressure covers instances, waits, streams, checkpoints, continuations, and external outbox
  records. It explicitly promises no age buckets.
- **PR-040** now has the ownership and no-split text of canonical `developer-facing-surface:105`,
  word for word. That text matches the six public extension classes and two builders in `src/`.
- **Artifact:** all 22 rows were recomputed independently from LF-normalized sources and match. The
  rows are numbered 1 to 22 in ordinal path order, and the artifact digest is `397ce4cc…`. Only
  rows 17, 18, and 19 (documents 09, 10, and 12) changed from the rejected target.

| Probe | Mutation | Result |
|---|---|---|
| B1a | `DirectInstanceTerminal` removed from AC-116 | **red** |
| B2a / B2b / B2c | AC-114 `Active` removed / MG-030 `age groups` restored / PR-040 ephemeral ownership deleted | each **red**, document named |
| B4a / B4b | pin-refresh sentence replaced in the design / removed from the ledger | **red** |
| B4c / B4d | pin-refresh weakened in the artifact / in the design | **red** |
| B0r | benign trailing space in document 12, row and digest refreshed (positive control) | green |
| B1r / B1s | AC-116 token removed / exact Round 66 four-variant wording, both refreshed | **red** |
| B2r / B2s / B2t | AC-114, MG-030, and PR-040 mutations with a coherent refresh | each **red** |
| B4r | artifact pin-refresh weakened with its digest refreshed | **red** |
| B3a | `[Trait("AC", "AC-116")]` on the Task 7.3 guard, removed from all three real tests | **red**; the general AC catalog alone stays green |

## 6. KKK-1 (P3): observations

None of these blocks the checkpoint.

- **The AC-trait check is narrower than the catalog.** The corpus guard rejects only the literal
  `[Trait("AC"`, but the catalog counts any `Trait("AC", "…")`.
  - In B3b and B3c, `[Fact, Trait("AC", "AC-116")]` or `[Xunit.Trait("AC", "AC-116")]` on the Task
    7.3 guard, with AC-116 removed from every real test, left both tests green.
  - All 393 existing uses are the literal form, so this needs a deliberate spelling change.
  - Reusing the catalog pattern `Trait\("AC",` would close it.
- **Two PR-040 clauses are only hash-pinned.** The semantic check covers the two "owns
  `…ServiceCollectionExtensions`" prefixes and the dispatcher sentence.
  - With a coherent row and digest refresh, deleting the provider/DAG no-split sentence (B2u) or
    the durable builder/ingress/dispatcher list (B2v) stays green.
  - Without a refresh, both are red and name document 10 (B2x/B2y). Given the new pin-refresh
    rule, this is defense in depth only.
- **Lineage failures do not always name the task.** Parent-count and parent-mismatch failures (A1
  to A4b) print only commit ids. The request says each fails with the task or evidence path named.
  The containment failures name both, and the addition failure names the path.
- **The owner exception is keyed on state, not on Task 5.3.**
  - The design and ledger describe one historical, retroactive exception.
  - The code relaxes the parent rule for any `OwnerAuthorized` entry.
  - It is safe today: A6 is red, and the addition rule still pins the unique introducing commit.
  - A future contemporaneous owner authorization would silently get the weaker rule.
- **EV-032 still omits the wait-state clause.** AC-114 now says the wait stays `Active` before
  commit, but EV-032, the requirement it cites, still does not. The behavior follows from the
  atomic transition, so this is wording only.
- **Pre-existing hygiene:** `design.md` line 83 ends mid-sentence ("remains registered and"). This
  dates from `87da8c2`, beside the new insertion. MG-030 has one 107-character line.

## 7. Reviewer hygiene and checkpoint instructions

I created no commit or ref in the reviewed repository and staged nothing. The review worktree's
`HEAD` remained `89e3ed55357e849852c1a0f6fefa2124433d7e30`. When this verdict was written, it
still showed exactly the fifty-two frozen entries plus this new, untracked verdict. Both disposable
worktrees are removed.

While the review was running, after the review copy had been taken, the owner created checkpoint
`9e921753eeadf71fcb641b6c91002fc89541d6df` on `feature/v3-rebuild`. It was created before this
verdict existed.
- Its only parent is `89e3ed55`.
- Its tree is `9c1ac4657b27d5980aaa02389f61321a2e2dcb3c`, identical to the reviewed target and to
  the simulated checkpoint validated above (9 A / 43 M).

It therefore carries exactly the approved content, and no further checkpoint is needed. This
verdict is not part of that checkpoint. The evidence commit that follows must:
- have `9e921753…` as its only parent, recorded as Task 7.2's `reviewedTargetCommit`;
- add this verdict;
- catalog it in `appendOnlyRecords` with its LF-normalized bytes;
- register it as a Task 7.2 `APPROVE` row;
- archive the active freeze.

The activation must then pin that evidence commit by its full id.

## Determination

HHH-1 is closed: evidence commits are now bound to their exact reviewed target and to the commit
that introduces the verdict. III-1 and the JJJ-1 items are corrected in text and guarded, and the
latest rejection is recorded exactly. The remaining observations are defense-in-depth and wording
refinements.

**Verdict:** **APPROVE**
