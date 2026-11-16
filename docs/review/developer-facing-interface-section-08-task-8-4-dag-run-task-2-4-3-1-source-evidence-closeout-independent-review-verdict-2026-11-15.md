# DAG run Task 2.4/3.1 source-evidence closeout independent review verdict

**Date:** 2026-11-15
**Reviewer:** independent review
**Scope reviewed:** the 20-entry freeze in the main checkout `X:/Projects/GitHub/Workflow-orca`
(branch `codex/dag-task-2-4-3-1-closeout`) on parent `da1a4729ea2cdb9474eb219eb507760574f83dcb`,
named by
`developer-facing-interface-section-08-task-8-4-dag-run-task-2-4-3-1-source-evidence-closeout-dirty-manifest-2026-11-15.txt`.
It checks Task 2.4, adds the Task 2.4/3.1 source-evidence inventory, adds a guard that binds the
five checkpointed source slices, and refreshes the DAG status blocks and their pins. The refreshed
blocks are in CLAUDE.md, six guide and implementation documents, and specs 05, 06 and 16.

**Authorization requested:** checkpoint of this exact target and its direct-child evidence.
There is no activation.

This verdict authorizes that checkpoint and the evidence child. It authorizes no other task
checkbox, no registry transition, no Task 3.1 closure and no Task 8.6 work.

## Summary

The five slice bindings are correct and enforced by a guard that fails when given a wrong tree or a
wrong evidence commit. The status refresh is accurate and stays inside the DAG status blocks. No
blocking defect was found.

Two findings are recorded:
- **F1 (P2, carried forward):** three of the 24 inventory rows credit assertions that no cited test
  makes.
- **F2 (P3):** Task 2.4 is checked while Tasks 2.1–2.3 stay open, and needs a sentence of scope.

- **Slice bindings (question 1).** For each of the five rows, `ValidateDagSourceSliceApprovals`
  checks:
  - the checkpoint's single parent and its exact tree;
  - a single-parent direct-child evidence commit that adds the verdict with the pinned bytes and one
    final APPROVE line;
  - that the evidence commit is an ancestor of `HEAD`.

  The inventory table matches the guard values, and the inventory itself is whole-file pinned as a
  tenth planning pin.
- **Task states.** Task 2.4 becomes Complete. Tasks 2.1–2.3, 3.1 and 3.2 stay Open, and reshape 8.6
  is newly pinned Open alongside 8.4 and 8.5. The reshape 7.20 edit only rewords "candidate" as a
  checkpointed slice; counts and checkbox are unchanged.
- **Documentation (question 3).** Every edit is inside the `dag-run-contract-proposal` block.
  - Each edit drops "source pending" and adds one paragraph naming the five checkpoints,
    ApprovedPending and the open Task 8.6.
  - Specs 03, 04, 08, 10, 12 and 17 are untouched.
  - The Task 7.3 rows that change are exactly 1, 6, 7, 9, 12, 15, 16 and 22.
- **Boundaries (question 4).** Nothing changes under `src/` or `openspec/specs/`, in any delta,
  any API baseline or the expected-red list, or in any existing `docs/review/` record.
- **Evidence rows (question 2).** I compared the rows with the 14 named shared cases and the
  Hosting DAG tests. Spot checks of DG-P01, 08, 09, 12, 14, 15, 20, 23 and 24 match their tests,
  including GUID collision in both directions and one observed evaluation. F1 covers three rows
  that do not.

## Validation

As the owner asked, freeze anchors were not re-derived. The working tree's status equals the
manifest, and nothing is staged. I rely on the owner's reported lanes:
- clean Debug/Release builds;
- Hosting 53/53;
- Infrastructure 319/319 with exactly 14 expected-red;
- strict OpenSpec 26/26;
- 14 named DAG cases on each of InMemory, PostgreSQL and SQL Server.

I ran the following in a disposable detached copy on `E:` with the 20 frozen files, which is now
removed.

| Lane / probe | Result |
|---|---|
| Canonical synchronization gate and TaskAccountingGuards | 3/3 |
| Hosting `FullyQualifiedName~Dag` / InMemory DAG certification | 29/29 / 14/14 |
| G1: one slice's reviewed tree altered in the guard | **red**: canonical gate, "the source slice must be the reviewed tree" |
| G2: one slice's evidence commit replaced by an older commit | **red**: canonical gate |
| P1: admission's Running/Ready check bypassed in `DurableDagAdmission` | **green**: 29/29 Hosting DAG and 14/14 InMemory DAG certification |

## Findings

- **F1 (P2, evidence precision; carried forward): three rows credit untested assertions.** The
  inventory states that it "credits only the stated assertions". These three do not:
  - **DG-P19, "cancellation stops late admission".** Both scheduler tests read the cancelled run
    while the mapper is still blocked, release it, and stop. No test lets the late receipt reach
    admission and checks that it is refused, and no test asserts `AdmissionStopped`. Probe P1 shows
    the bridge check is untested.
    - The product behavior is defended twice: the bridge returns `AdmissionStopped`, and
      `DagRunStateReducer` rejects `InputAdmitted` unless the run is Running and the node Ready.
    - Neither defense is exercised by any test, including provider certification.
  - **DG-P17, "observer expiry/unsubscribe cleanup".** No test calls `UnsubscribeDagRunAsync`. The
    shared controlled-clock case covers expiry pruning, claim ownership, poison retention and
    pressure counts only. RC-3 lists "unsubscribe purge" as a separate DG-P17 obligation.
  - **DG-P22, "shared scheduler admits 0 then 1 on all providers".** In that case node 1 depends on
    node 0, so the order is forced by the dependency rather than by authored-order selection.
    - Authored order among nodes that are Ready at the same time is covered only by the round-106
      InMemory tests.
    - The limit column therefore understates the gap. RC-3's one-host, real-storage ordering case
      with simultaneous Ready nodes and skewed mappers is open, in addition to the two-host and
      stale-rank cases.

  These rows already list remaining qualification, and RC-3 still owns the criteria. The defect
  therefore understates open work rather than closing a row, and this verdict, committed in the
  evidence child, records the correction. In the next Task 3.1 target, either add the three tests or
  narrow the three rows. Task 3.1 must not close on the current wording.
- **F2 (P3, task semantics): Task 2.4 is complete while 2.1–2.3 stay open.** Task 2.4 requires
  approval and a checkpoint "for each large slice". Tasks 2.1–2.3 stay open for qualification, so
  later test or source slices are still expected. CLAUDE.md requires each of them to be reviewed
  and checkpointed anyway, and the five-row guard cannot credit a later slice. Still, add one
  sentence to the inventory or task note: Task 2.4 covers the five slices to date, and any later
  slice needs its own approval, checkpoint and guard row.
- **Recorded:**
  - `docs/implementation/00-stack-decisions.md` rewrites its 2026-10-05 entries in place (heading,
    "source pending", transition status). The 2026-10-07 entry still says those entries are
    "retained unchanged as history", and the new entry says earlier entries describe their own
    checkpoints. The content is now accurate; the history claim is not. Append rather than rewrite
    next time, or reword the 2026-10-07 sentence.
  - The 7.20 wording still says the "Task 2.3 review follow-up" without "checkpointed". This is
    harmless.

## Hygiene and checkpoint sequence

The main checkout is at `da1a4729ea2cdb9474eb219eb507760574f83dcb` with the 20 frozen entries and
nothing staged. When this verdict was written, it showed exactly those entries plus this new,
untracked verdict. My disposable worktree is removed. The other worktrees (`X:/ocv-ev011`,
`X:/ocv-shutdown-spec`, `X:/ocv302`, `X:/ocv310`) are not mine and were not touched.

1. Confirm zero drift against the 20-entry manifest.
2. Stage exactly those 20 paths from the manifest (not `git add -A`), expecting the owner's projected
   tree `ea16748a4afa5ee88777c5f85f508acc7aa887af` on parent `da1a4729`, and commit.
3. In its only direct child, add and catalog this verdict and clear the active freeze, keeping the
   history pointer.

There is no activation. Carry F1 and F2 into the next Task 3.1 target.

## Determination

The checkpoint chain for all five slices is bound by an enforcing guard. The status documents now
describe checkpointed internal slices with ApprovedPending and open Task 8.6. No task other than
2.4 changes state. The evidence-precision gaps are recorded for the next Task 3.1 target and do
not affect product code or any closed gate.

**Verdict:** **APPROVE**
