# Independent review: Task 8.4 DAG run successor PROCESS gate

Date: 2026-10-04. Change: admit-durable-dag-run-progression. Task 0.1 only.
Base/parent: `095bbfd1a6b559f8f1be47fb8d4bcd4749d40854`. Index empty; this target is uncommitted.

## Exact authorization boundary

Review exactly the fourteen paths in `docs/review/developer-facing-interface-section-08-task-8-4-dag-run-successor-process-dirty-manifest-2026-10-04.txt`.
This approves at most a PROCESS exception for four exact requirement headings,
not the run-persistence contract, public/advanced stream identities, provider
schema changes, canonical sync, a friend/codec grant, bridge source or reshape
8.4/8.5 completion. No product or canonical file changes. Task 0.1 records
preparation only; 0.2 remains open for review/checkpoint. Contract 1.1–1.4,
source 2.1–2.4 and closeout 3.1–3.2 remain open.

The mandatory order in CLAUDE.md is process approval/checkpoint/evidence/0.2
activation, then the separately reviewed section 10 contract 1.1/1.2, then
atomic canonical/registry 1.3/1.4, then source. Do not interpret CLI apply-ready
as source authorization. It only means the four planning artifacts exist.

## Prior approval checkpointed with zero drift

- Child-protocol checkpoint: `1d9672a51ed6f2770dcf7023d13a915a9b135571`;
  parent `c85fd0f301f110db691a026bcdd0c0e35c6b26c1`, approved tree
  `4b83f4d9a5cf2ecaf8d3dc2e60b6e1c56e6c02e2`, exactly 15 paths.
- Direct-child evidence: `095bbfd1a6b559f8f1be47fb8d4bcd4749d40854`, exactly APPROVE verdict,
  immutable-document-history.json and review-manifest-provenance.json. The
  verdict is 13,162 raw/LF bytes, SHA-256
  `2fe0cac10719ab0aa9e6321461fb4b9cb5d63504a0436585dc66f51b32375557`.
  It was added, not merely inherited, by the evidence commit. That state ran
  Infrastructure 243/243. There is NO activation and 8.4/8.5 remain open.
- Both approved and rejected child-contract artifacts and packets remain
  byte-exact. The six-operation contract is a child protocol, NOT a complete
  run lifecycle. Its section 10 explicitly requires this successor gate.

## New successor gate, not a general ownership waiver

Only these four pairs may have the two resolved owners reshape and
admit-durable-dag-run-progression:

| Capability | Exact requirement |
|---|---|
| durable-persistence-and-outbox | Workflow store persists first-class durable records |
| durable-persistence-and-outbox | Outbox delivery is adapter-driven and at-least-once |
| durable-persistence-and-outbox | First-release provider ownership and schemas are exact |
| durable-runtime | Durable DAG progression is runtime owned |

Schema 10 retains every previous row/evidence and adds only the four
`proposedDagRunSuccessors`, checked against a C# source-pinned array of exact
owner, heading, operation, stage, sync task and old/new block hashes. Each delta
is the FULL unchanged canonical/predecessor block plus additions. All four are
MODIFIED and Proposed; only Task 1.3 owns their sync. A genuine third owner fails
with its name. Prior runtime-view/authoring ownership chains are untouched.
The predecessor still equals canonical; no new supersession is performed here.

Measured provenance: 184 rows / 48,464 bytes /
`d76b82216c3504f40788299162961b8dbdef1da89268bf6eaafe7092532418d5`: 173 synchronized, 4 previously superseded,
4 pending modifications and 3 bootstrap-only rows. semanticApprovalEligible is
false. New pending counts are durable-persistence-and-outbox (3) and
durable-runtime (1), both this change's 1.3. Active directories are
22 / 1,808 bytes /
`2869d4ad4541be53adeac5819091605eff85c39b5140abec4438c668ac99b6e2`. Canonical inventory/preambles remain
14 / 547 / 7165dac4… and 14 / 1,233 / 595528c6… respectively.

The current process provenance artifact is whole-file pinned in guard source
and fixture. Its predecessor is added UNEDITED to the permanent superseded
catalog (now eight entries); no artifact is moved/deleted or silently rewritten.
No Fact/Theory is added, so the declaration crosswalk correctly stays unchanged.

## Five review notes carried into this proposed contract direction

These are proposed corrections, NOT approved implementation signatures:

1. Replace the nonexistent IWorkflowCommitStore assumption with actual
   IWorkflowEventStore.AppendAsync(ProviderCommitBatch) ownership and explicitly
   review changes required for non-workflow records.
2. Today's public WorkflowStreamId renders only workflow-{id}. A dag-run-v1
   stream needs an explicit public/advanced identity, protocol, API baseline,
   complete production schema and certification amendment; no fake port reuse.
3. CP-025 restart authority is the current fenced owner's validated host limit,
   not a sticky creation-time ceiling. Preserve all prior admitted slots when
   the limit falls; stale owners cannot admit under different local options.
4. Child start/join/cancel runtime pumps remain installed on all durable hosts;
   the separate dag-run-ready-v1 claim partition is DAG-role-only. Role-free
   hosts do not claim/release ready work. External dispatch excludes all four.
5. Full capacity is CapacityDeferred, not AdmissionStopped: no input/start
   commit, speculative mapping discarded, notification-driven retry after
   capacity/ownership changes. Cancellation/terminal barriers remain distinct.

The design also retains present-null fixed-codec bytes, deterministic bound
child identities, eager decoding of every declared successful resultful output,
one-way ready-sink direction, separate-stream recovery and complete run lifecycle
responsibilities. Exact signatures/fences/ports/results must be frozen in 1.1;
this gate deliberately does not substitute another partial V1 seam for them.
The deferred source-only UnsafeAccessor/expression ban and DagRuntimeView.cs
evidence-path audit are not claimed fixed here.

## Reproduction recipes and self-inclusive anchors

- Raw manifest: 14 entries, 1198 bytes,
  SHA-256 `a68caa71b6869cbb5d8b5daf5dbbddbb4ba8236dbe56a730cf8e7de54498deca`. XY + space + repository-relative
  path, exact Git-emitted tracked then untracked order, UTF-8/LF, one final LF.
  Compare with `git status --porcelain=v1 --untracked-files=all --no-renames`
  and its NUL form. Also compare the diff-plus-untracked set and a copied-index
  staged set, so attribute/cache-only phantom entries cannot enter the freeze.
- Semantic record: 10 rows / 1458 bytes /
  `8dfd3d157cc0e55d29ced80e621dd6cebdfb9b8e4866a127dbeb584041716298`. Take manifest paths except docs/review/**
  and the two history/review provenance fixtures; read RAW bytes; render
  `path<TAB>byte-count<TAB>sha256`, ordinal-sort the FULL rows, LF-join plus
  one final LF. No normalization of semantic file bytes.
- Active record: all manifest paths EXCEPT the self-referential
  review-manifest-provenance.json. Render
  `XY<TAB>path<TAB>raw-byte-count<TAB>raw-sha256`, ordinal-sort FULL rows,
  UTF-8 without BOM, LF-join with one final LF. The computed bytes/hash are pinned
  in activeFreeze; request and manifest ARE included. Read that fixture for the
  exact final value rather than confusing it with the semantic projection.
- The simulated tree is supplied in the handoff after this self-inclusive
  request/manifest are frozen. Embedding the tree of a request inside that same
  request would create a hash self-reference. Recompute with a copied index,
  stage exactly all manifest paths, verify raw file/blob equality and write-tree.

## Validation and mutation evidence

This target's Debug and Release non-incremental -warnaserror builds pass 0/0.
Core 350, Ephemeral 79, Durable 99, Acceptance 37, Hosting 24, certification 96
pass. Infrastructure 243/243; full guards 243/14/257 with every failure in
ExecutableBehaviorExpectedRedGuards. OpenSpec --all --strict is 21/0. Current-match
refresh -Check is exact; no product/canonical/declaration-crosswalk drift.
The container lane results and final copied-index/checkpoint checks are supplied
with the final handoff after completion; do not infer them from earlier approvals.

Thirteen negative controls on a byte-verified isolated copy are RED:

| Control | Result / first relevant detector |
|---|---|
| C1 stage promoted to ApprovedPending | exact source-pinned row |
| C2 drop one row | exact source-pinned row |
| C3 wrong predecessor | exact source-pinned row |
| C4 edit a proposed block | proposed block hash |
| C5 edit a predecessor | predecessor block hash |
| C6 canonical hand-synced early | prior independent canonical runtime block hash fires first |
| C7 close contract task 1.1 | exact Open state |
| C8 close reshape 8.4 | existing three-row handoff pin fires first |
| C9 semanticApprovalEligible true | independent human-record semantic claim fires first |
| C10 edit prior provenance artifact | permanent superseded-artifact pin |
| C11 repoint sync task | exact source-pinned row |
| C12 swap MODIFIED operation | exact source-pinned row |
| C13 genuine third owner | owner set names rogue-dag-run |

Baseline and restored control are GREEN; P1 checking 0.2 alone is GREEN,
matching checkbox-only post-approval activation. C13's first harness construction
omitted an Impact heading and failed structurally; the corrected second run fails
at the two-owner invariant. No probe edited/reset/committed main. Temporary
probe work is disposable; old review records never become test-edit targets in main.

## Verdict and checkpoint protocol

Please independently test the process exception, prerequisite evidence chain,
full-block copying, pending provenance, immutable catalogs and exact frozen scope.
Write a NEW immutable verdict with stem
developer-facing-interface-section-08-task-8-4-dag-run-successor-process-
independent-review-verdict-YYYY-MM-DD.md, exactly one terminal APPROVE or REJECT
line. Do not edit this packet, old approvals or main's index. The verdict is a
later evidence file, not part of this fourteen-path freeze.

Only after APPROVE and byte-exact zero-drift verification: checkpoint exactly
these fourteen paths at the handoff tree; its single-parent direct-child evidence
commit adds/catalogs the verdict and clears activeFreeze while keeping the history
manifest pointer; activate by checking 0.2 only. Then prepare 1.1 contract review.
Do not check 1.2–3.2 or reshape 8.4/8.5, and do not start source or archive anything.
