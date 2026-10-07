# DAG run atomic canonical transition (1.3/1.4) independent review verdict

**Date:** 2026-10-06
**Reviewer:** independent review
**Scope reviewed:** the thirty-six-entry freeze on base `d59f8307977dc71d1b77444b9711a0468c65f93d`
(tree `3d834a59…`), named by
`developer-facing-interface-section-08-task-8-4-dag-run-atomic-canonical-transition-dirty-manifest-2026-10-05.txt`.
This is `admit-durable-dag-run-progression` tasks 1.3 and 1.4, after my round-98 APPROVE of the
RC-3 contract (17,803 B, `b7c9d54d…`), landed as checkpoint `e6802a3`, evidence `578593e` and
1.2-only activation `d59f830`. It contains:
- the canonical sync of three `durable-persistence-and-outbox` blocks and one `durable-runtime`
  block;
- a one-word delta refinement (`MAY` -> `SHALL` for lower-ordinal discard);
- the new transition addendum (5,890 B) and approved-pending provenance artifact (3,246 B);
- four `ApprovedPending` registry rows, four reshape supersessions and the RC-3 evidence binding;
- guard source for the transition and one new lifecycle Fact;
- status refreshes in `CLAUDE.md`, docs 03/04/05/06/08/10/12/16/17, five guides, a dated
  `00-stack-decisions.md` note, the change's proposal/design/tasks, the Task 7.3 artifact and
  reshape task 7.20;
- the provenance, history, crosswalk and review-manifest fixtures.

**Authorization requested:** checkpoint of this target, its direct-child evidence, then the
1.4-only activation.

This verdict approves the addendum, the one-word refinement and this exact canonical and registry
target, as the addendum states RC-3 alone does not. It authorizes that checkpoint, the evidence
child and the 1.4-only activation, in the sequence the request states. It authorizes no source,
no archival and no withdrawal.

## Summary

The transition is mechanical, exactly bounded and correctly guarded.

- **Sync.** Applying the four delta blocks to canonical at `d59f830` with my own script reproduces
  both canonical files byte for byte. Only those four blocks change; preambles (including
  `## Purpose`), heading order and every other block are unchanged.
- **One word.** The only delta change since `e6802a3` is `MAY` -> `SHALL` in "A newly Ready lower
  ordinal … invalidate one higher speculative receipt", which resolves my round-98 P3-1. The guard
  re-reads the RC-3 delta from the checkpoint object, proves it still hashes to the approved value,
  and requires the current block to equal it with exactly that substitution.
- **Registry.** The four `Proposed` rows stay as history with their RC-3 hashes. Four
  `ApprovedPending` rows bind predecessor, approved and canonical hashes, and exactly four reshape
  predecessors are superseded. The evidence record binds the real RC-3 checkpoint, tree,
  single-parent evidence that adds my verdict, and the addendum hash, all source-pinned.
- **Provenance.** 184 rows, all resolved: 173 synchronized, 8 superseded, 3 bootstrap-only. Semantic
  approval eligibility is now true, which here means only that the corpus is synchronized.
- **Notes.** All four round-98 observations are dispositioned. The P3-3 skip rule is safe: it never
  relies on `StreamVersion` alone, so a host that dies after acknowledging a wake leaves other
  hosts obliged to reload.
- **Evidence.** Every anchor, record and inventory reproduces, and validation is green in all three
  states. Thirty of my thirty-one targeted controls are red. The exception exposes a pre-existing
  gap outside this transition (P3-2).

The remaining observations are non-blocking.

## Method

Validation ran in one disposable detached worktree at `d59f830`, and probes in a second. Both held
the same thirty-six entries.

- Every script asserted its disposable location and that the main `HEAD` was still `d59f830`.
- Main was never modified, and the review created no ref in the reviewed repository.
- The sync, block hashes, provenance, preambles, permanent catalog, Task 7.3 rows and current-match
  refresh were recomputed with my own scripts.
- The deltas were checked with `openspec show --json --deltas-only` (CLI 1.6.0): exactly four
  `MODIFIED` operations.

## 1. Freeze anchors and provenance

| Anchor | Claimed | Reproduced |
|---|---|---|
| Raw porcelain, `--untracked-files=all --no-renames` | 36 lines, 2,427 B, `57d94a5f…` | identical (`57d94a5f5e52ad8bb76871b1e2286d9bbd8d48467fa3b5286099894a9251b32c`); the NUL form has 36 entries (`5a98ed43…`); diff (32) plus untracked (4) paths are exactly the 36; nothing staged |
| Semantic record: 32 rows | 4,166 B, `06730a22…` | identical with the stated recipe |
| Active record: 35 rows | 4,824 B, `7a8fb21a…` | identical (`7a8fb21a19660451a67a60bfd7840c878951d0da6ff4b55b6b14971e471f811a`), equal to the fixture |
| All-file record, with status (my form) | not embedded | 36 rows, 4,979 B, `f0454e5489ae58ab7aa04b1a4ba0dfff683ed1f6e2c0ba525e709a371f407bc9` |
| Simulated checkpoint | tree `61b5900c0a6a87c34429e15c9cad7832d891c615` | identical from a copied index (1,767 + 4 entries, no phantoms) and from a committed disposable copy; parent `d59f830`; 4 A / 32 M; all 36 blobs equal the raw bytes (`hash-object --no-filters`); committed `diff --check` clean |
| Provenance (reviewer script) | 184 / 48,504 B / `fd6ad044…` | identical: 173 synchronized, 8 superseded, 3 bootstrap-only, 0 pending; capability directories 22 / 1,808 B / `2869d4ad…`; canonical inventory 14 / 547 B / `7165dac4…` |
| Canonical preambles | 14 / 1,233 B / `595528c6…` | identical, and unchanged from the base |
| Permanent provenance catalog | 13 / 2,291 B / `8555cbe8…` | identical; it adds the RC-3 provenance artifact (`bee1707d…`) unchanged, and every listed file matches |

- **Block hashes.** All twelve hashes in the request's table reproduce under the guard's recipe
  (block from heading to the next requirement or section, trailing blank lines removed, no final
  LF). The persistence blocks are unchanged since `e6802a3`; only the runtime block moves
  `fe738cf9…` -> `81fe1db4…`.
- **RC-3 packet.** Contract `f8a1a7f6…`, catalog `766ad5d8…`, signatures `8301094b…`, remediation
  record and my RC-3 verdict are outside the manifest and unchanged; the API totals stay
  7 + 193 + 165 = 365.
- **History.** The request (9,762 B, `dccaf2f1…`) and manifest are catalogued in immutable history,
  and the active freeze points at them.
- **Current matches.** My emulation of `refresh-review-manifest-current-matches.ps1 -Check`, which
  still cannot run under Windows PowerShell 5.1, gives 16 entries, 100 matches and 0 stale. The
  two canonical specs correctly leave one historical entry's match list.
- **Crosswalk.** 340 physical files / 1,406 declarations, active 718: the single new Fact. Reshape
  task 7.20 names this guard slice; the 866-row retired ledger is unchanged.

## 2. Validation

| Lane | Result |
|---|---|
| Debug and Release non-incremental `-warnaserror` builds | 0 warnings, 0 errors |
| Exact package feed / package fixtures | 12 packages / 8 green, exactly `dag-hosting` red |
| Compile fixtures (`Green`) / public API baseline | green / 14/14 |
| Core / Ephemeral / Durable / Acceptance / Hosting / ProviderCertification | 350 / 79 / 99 / 37 / 24 / 96 |
| PostgreSQL / SQL Server / Integration | 101 / 72 / 11 |
| `Disposition=Infrastructure`, Release | 244/244 |
| `Disposition=ExpectedRed` | exactly 14, all `ExecutableBehaviorExpectedRedGuards` scenarios |
| `OpenSpecCorpusGuards`, Release / Debug / strict OpenSpec | 16/16 / 16/16 / 21/21 |
| `git diff --check`, worktree and committed simulation | clean |
| Guards on the committed simulation | 244 passed plus the same 14 expected-red |
| Evidence state: committed simulation with the freeze cleared / then 1.4 checked | Infrastructure 244/244 / 244/244 |

## 3. Transition review

| Item | Finding |
|---|---|
| Mechanical sync | my applier reproduces both canonical files exactly; four blocks change; preambles, `## Purpose`, order and unrelated blocks byte-exact; `MODIFIED` headings match canonical literally |
| Delta refinement | `git diff e6802a3` shows one word, `MAY` -> `SHALL`; guard proves the RC-3 block from the checkpoint object and allows only that substitution |
| Registry | schema 11; four `Proposed` rows unchanged; four `ApprovedPending` rows and four supersessions equal source-pinned arrays; RC-3 evidence verified as single-parent direct child adding a unique terminal APPROVE, tree `d367ced6` |
| Supersession classification | only the four hash-matched reshape predecessors become `SupersededByApprovedSuccessor`, bound to the new canonical hashes |
| Task state | 1.2 and 1.3 complete, 1.4 open and its checkbox alone normalized out of the whole-file pin; 2.1–3.2 and reshape 8.4/8.5 open |
| Lifecycle | one active or one `YYYY-MM-DD-<change>` archive resolves; duplicates, an owner present only as an undated archive, and a missing owner fail closed; withdrawal and archival are named policies that need their own reviews |
| Crosswalk | primary docs 06/10/16 amended; reverse audit amends 03/04/05/08/12/17 and the guides; doc 11 and the public-authoring companion explicitly excluded |
| Status wording | every block says approved, source pending; mapper invocation is never claimed to be exactly once |
| Round-98 P3-1 | resolved by the one-word refinement |
| Round-98 P3-2 | carried as a named Task 2.3 source obligation; no gauge member, option or catalog key is added |
| Round-98 P3-3 | addendum skip rule: unchanged version AND (matching local evaluation or result for the lowest `Ready` node, OR F = 0); cost disclosed as O(run heads) plus non-skippable checkpoint loads; doc 16 line corrected; DG-P24 must test the unchanged-version death case |
| Round-98 P3-4 | duplicate removed; all 22 Task 7.3 rows and the whole-artifact pin reproduce |

## 4. Independent negative controls

Thirty-four cases ran in the disposable probe copy. Every case except K0, K31 and K32 (which runs
only the lifecycle Fact) cleared the active freeze first, so content anchoring could not mask a rule. The focused lane is
`OpenSpecCorpusGuards` (16 tests) unless stated.

| # | Mutation | Result |
|---|---|---|
| K0 | none (freeze active) | green, 16/16 |
| K1 | evidence state: freeze cleared | green |
| K2 | evidence state plus 1.4 checked only | green |
| K3 | task 1.3 unchecked again | **red**: whole `tasks.md` pin, only 1.4 exempt |
| K4 | task 2.1 checked early | **red**: same pin |
| K5 | old `MAY` restored in delta and canonical together | **red**: canonical runtime block pin |
| K6 | canonical alone reverted to `MAY` | **red**: same |
| K7 | a second wording change (five -> ten seconds) in delta and canonical together | **red**: same |
| K8 | unrelated canonical requirement in `durable-runtime` edited (no active delta owns it) | green; also green under the whole Infrastructure lane (242/242 without the two feed tests). See P3-2 |
| K9 | canonical `## Purpose` of persistence edited | **red**: preamble record |
| K10 | fifth reshape supersession row | **red**: "only the four hash-matched reshape predecessors" |
| K11 | an `ApprovedPending` row's stage set to `Complete` | **red** |
| K12 | a `Proposed` history row set to `ApprovedPending` | **red** |
| K13 | fixture cites activation `d59f830` as approval evidence | **red**: source-pinned evidence record |
| K14 / K15 | addendum edited by one byte / deleted | **red** / **red** |
| K16 | approved RC-3 contract edited by one byte | **red** |
| K17 | my RC-3 APPROVE edited by one byte | **red**: approval evidence and immutable history |
| K18 / K19 | duplicate Task 7.3 section re-added / a Task 7.3 row reverted | **red** / **red**: Task 7.3 pins |
| K20 | doc 16 line reverted to "Startup discovery recovers runs" | **red**: document pin and Task 7.3 row |
| K21 | `CLAUDE.md` heading reverted to "Proposed … pending" | **red**: same |
| K22 | `semanticApprovalEligible` set false | **red**: recomputed, must match live evidence |
| K23 | a pending `durable-runtime` operation re-added | **red** |
| K24 | active owner plus a dated archive copy | **red**: "exactly one active or dated archived record; found 2" |
| K25 | owner moved to an undated archive only | **red**: "found 0" |
| K26 | owner moved to a dated archive with no reviewed refreeze | **red**: pinned active-change paths are missing |
| K27 | withdrawal: `approvedDagRunSuccessors` emptied and owner removed | **red**: "found 0" |
| K28 | reshape 8.4 checked | **red** |
| K29 | this request edited by one byte | **red**: immutable history |
| K30 | crosswalk declarations back to 1,405 (crosswalk lane) | **red** |
| K31 | freeze active, addendum edited by one byte | **red**: pin and active record |
| K32 | source: archive resolver accepts undated names again (lifecycle Fact) | **red**: the new Fact fails |
| K33 | source and fixture both cite `d59f830` as evidence | **red**: Git parent check, `d59f830`'s parent is `578593e`, not `e6802a3` |
| R0 | restored | green; worktree equals the manifest and every target byte equals main |

Thirty of the thirty-one targeted mutations (K3–K33) are red; K8 is the exception, recorded as
P3-2. The unmodified, evidence and activation states stay green.

## 5. Non-blocking observations (P3)

- **P3-1: this transition's own approval is not yet bound.**
  - The `ApprovedPending` evidence binds RC-3's checkpoint, evidence and verdict and the addendum
    hash. The canonical runtime block `81fe1db4…` and the addendum are approved only by this
    verdict, which the registry cannot cite before it exists.
  - After the evidence child lands, the next reviewed target must bind this transition's
    checkpoint, tree, single-parent evidence and verdict the way this target binds RC-3, before or
    with the first source slice.
- **P3-2: canonical text outside delta-owned blocks is unguarded once the freeze clears.**
  - K8 edits a `durable-runtime` requirement that no active delta owns, and every Infrastructure
    guard stays green. The provenance record covers delta-owned blocks, and preambles and the
    capability inventory are pinned, but nothing pins the rest of a canonical file.
  - Before this target, both synced files were pinned only incidentally, through an older
    record's current-match list. The sync correctly drops them from it. Eight of the fourteen
    canonical specs were already in this state, including the two synced by the runtime-view
    transition, so this is not a regression.
  - The request's "unrelated canonical drift" control holds only for owned blocks (K6, K9). Add a
    reviewed per-file canonical content inventory to the provenance checkpoint, refreshed by each
    reviewed sync, so a hand edit to `openspec/specs/` cannot pass silently.
- **P3-3: status text goes stale at the checkpoint.**
  - The guide blocks, the dated note and the proposal/design/tasks notes say the transition is
    "prepared for independent review, not yet checkpointed", and the RC-3 paragraph says the four
    blocks are "prepared for sync" although this target performs the sync.
  - The 1.4 activation may not edit documentation, so the wording stays stale until the next
    reviewed target. This follows the RC-3 precedent; refresh it there.
- **P3-4: preamble recipe wording.** The provenance artifact says the preamble is "trimmed at the
  end", but the guard hashes the untrimmed prefix. The trimmed form gives `3991e9f3…`; the pinned
  `595528c6…` is the untrimmed form. Correct the description in the next provenance artifact.
- **P3-5: lifecycle resolver strictness.** The archive pattern checks date shape only (it accepts
  `2026-13-45`), and an undated archive copy beside an active owner is ignored rather than
  rejected. Neither can create a second owner. Tighten both when archival is reviewed.
- **Recorded:**
  - The historical-block check reads `e6802a3` from Git. CI fetches with `fetch-depth: 0`, the
    runtime-view guards already depend on Git objects the same way, and a missing object fails
    closed.
  - The lifecycle controls are sequential steps in one Fact, each with its own assertion. Many
    pins hard-code the active change path (K26), so a real archival needs its own reviewed
    refreeze, as the addendum says.

## 6. Reviewer hygiene and checkpoint sequence

`HEAD` is `d59f8307977dc71d1b77444b9711a0468c65f93d`, with the thirty-six frozen entries and
nothing staged. When this verdict was written, the repository showed exactly the frozen entries
plus this new, untracked verdict. The disposable worktrees are removed.

Checkpoint sequence, as the request states:
1. Confirm zero drift against the 36-entry manifest and the anchors above.
2. Stage exactly those 36 paths on `d59f830`, expecting tree `61b5900c…`, and commit.
3. In its only direct child, add and catalog this verdict and clear the active freeze, keeping the
   history pointer.
4. Activate by checking task 1.4 only.

Source slices 2.1–2.4 need their own independent reviews after that chain. Tasks 3.1–3.2 and
reshape 8.4/8.5 stay open.

## Determination

The four approved blocks are now canonical by a reproducible mechanical sync, with one reviewed
word that makes the contract's mandatory discard normative. Their predecessors are superseded by
exact hash, the RC-3 approval is bound to real objects, the proposed history is retained, and the
provenance corpus is fully resolved. All four round-98 observations are dispositioned, the
discovery refinement is safe, and the controls show that every decision is guarded. The five
observations above are non-blocking.

**Verdict:** **APPROVE**
