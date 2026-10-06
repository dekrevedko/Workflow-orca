# DAG run contract RC-1 remediation independent review verdict

**Date:** 2026-10-04
**Reviewer:** independent review
**Scope reviewed:** the forty-three-entry freeze on base `dab498ff54321d802b36ce052100954bbe00487d`,
named by
`developer-facing-interface-section-08-task-8-4-dag-run-contract-rc-1-remediation-dirty-manifest-2026-10-04.txt`.
This is `admit-durable-dag-run-progression` task 1.1, revised after my round-95 REJECT. It contains:
- the successor run contract (`task-1-1-run-contract-rc-1-remediation-2026-10-04.md`, 58,168 B);
- its exact API catalog (71,325 B), standalone signature proof (13,208 B) and remediation record;
- the four revised deltas, proposal, design and tasks;
- the 16 proposed document blocks, the reshape 8.4/8.5 and Task 8.0 map rows;
- the rejected round-95 packet, retained byte-exact;
- guard pins, the registry and provenance.

**Authorization requested:** checkpoint of this contract target, then the 1.2-only activation.

This verdict authorizes no checkpoint, no canonical sync (1.3/1.4), no source and no archival.

## Summary

RC-1 is resolved, and so are all three round-95 notes.

- **Fence path.** `IDurableDagRunBridgeV1` now has `BeginTurnAsync`, `RenewTurnAsync`,
  `EndTurnAsync`, `AbandonTurnRequestAsync` and `OpenChildBridgeForTurn`, and §4.1 forbids
  Dag.Hosting any `IWorkflowEventStore`, provider, reflection, `UnsafeAccessor` or expression
  path.
- **Coordinator location.** Dag.Hosting owns admission and mapping; Durable.Hosting owns the lease
  broker, every provider call, and joins and cancellation on every durable host. The captured-turn
  child bridge calls the same Durable.Hosting `AdmitMappedInput` and never dispatches to a remote
  coordinator.
- **Liveness.**
  - The provider FIFO grants only the lowest live sequence, with no same-owner bypass.
  - Each grant allows one append and has a lease of at most 5 s and an absolute 15 s deadline
    that renewal cannot extend.
  - The turn is released before every idle, capacity or child wait, and joins and cancellation
    from any host enter the same queue.
  - A renewing owner can therefore no longer starve a remote join or cancellation, and DG-P14 to
    DG-P16 make that a certification obligation.
- **Round-95 notes.**
  - Behavioral scenarios replace the process-only scenario, with the canonical text byte-exact
    and the new SHALL text above the first scenario.
  - Dead-observer notices are purged within a bound.
  - Events now carry only changed-node facts, plus one overwritten checkpoint.
- **Evidence.** The packet, pins and validation all reproduce.

The remediation introduces one new blocking defect. It bounds every turn at 15 seconds but runs
the application's mapper inside the turn, and it discards any result that arrives late. A valid
mapper that needs longer than the turn budget can therefore never commit its node on any host or
attempt. That run never becomes terminal and records no failure code, which contradicts CP-022
and CP-024. Nothing published tells authors that the budget exists.

## Method

Validation ran in one disposable detached worktree at `dab498f`. Probes ran in a second, and an
independent standalone compile in the scratchpad. Both worktrees held the same forty-three entries.

- Every script asserted its disposable location and that the main `HEAD` was still `dab498f`.
- Main was never modified, and the review created no ref in the reviewed repository.
- Provenance was recomputed with my own script.
- The deltas were compared block-by-block with canonical, and checked with
  `openspec show --json --deltas-only` (CLI 1.6.0).
- The rejected-freeze reconstruction table was compared row-for-row with my own round-95 record.
- The contract was read in full against both trees, the current provider ports, the operational
  pressure queries, and doc 08 / doc 17 mapping semantics.

## 1. Freeze anchors and provenance

| Anchor | Claimed | Reproduced |
|---|---|---|
| Raw porcelain, `--untracked-files=all --no-renames` | 43 lines, 3,456 B, `ba7b90e9…` | identical (`ba7b90e912eff5fd2db3c7d2253a74d34390872a02e411543e91595919ca6e6d`); the NUL form has 43 entries; the union of diff (29) and untracked (14) paths is exactly 43; nothing staged |
| Semantic record: 36 rows | 5,120 B, `994198bc…1b50` | identical with the stated recipe |
| Active record: 42 rows | 6,347 B, `010cf1fa…` | identical (`010cf1fa0b77ff963973c659a4c515817bb6c6ba30a83413c551fbc8e5262aa7`), equal to the fixture |
| All-file content record, 43 rows (reviewer) | — | 6,502 B, `868ec359839c91dc0ab365855102553a84a4a2472423e239d3f64c2780db630a` |
| Simulated checkpoint | tree `2302ee0879e7b913a69bd93290b0cfcea3ccb0a9` (handoff) | identical from a copied index and from a committed disposable copy; parent `dab498f`; 14 A / 29 M; all blobs equal the raw bytes (`hash-object --no-filters`); cached `diff --check` clean |
| Provenance (reviewer script) | 184 / 48,464 B / `cde64d8d…b6d` | identical: 173 synchronized, 4 superseded, 4 `PendingModification`, 3 bootstrap-only; the multi-owner pairs are exactly the 2 runtime-view chains and the 4 registered pairs; capability directories 22 / 1,808 B / `2869d4ad…`; canonical inventory 14 / 547 B / `7165dac4…` unchanged |

- **Rejected round-95 packet:**
  - the request (13,827 B, `842cbec6…`), manifest (2,471 B, `fa360497…`) and my REJECT
    (15,033 B, `5f2d0751…`) are unchanged;
  - all three are in immutable history and in `rejectedFreezes` row
    `reshape-dag-run-contract-rc-1-rejected`;
  - the remediation record's 35-row reconstruction table equals my own round-95 all-file record
    exactly, and reproduces the round-95 active record (34 rows, 4,797 B, `38c40cc6…`).
- **Rejected artifacts:** the old contract (`a792b9d4…`), API catalog (`61afd2af…`) and signatures
  (`5dab933d…`) are unchanged and permanently pinned in guard source.
- **Planning pins:** seven whole-file pins:
  - proposal, design and tasks, with only the 1.2 checkbox normalized out of the tasks hash;
  - the successor contract, catalog and signatures (`f655afc1…`, which equals my own hash);
  - the remediation record.

  The 16 proposed document blocks are re-pinned.
- **Superseded provenance catalog:** now 10 entries. It adds the 0.1 process artifact
  (`e28513fa…`) and the round-95 contract artifact (`056ab2f1…`) unchanged.
- **Registry:** the four `proposedDagRunSuccessors` rows change only their proposed block hashes,
  identically in source and fixture. They stay `Proposed`, with sync task 1.3.
- **Deltas:** each preserves the canonical statement and scenarios verbatim and adds its SHALL
  paragraph before the first scenario. OpenSpec parses every new paragraph into the requirement
  text, and no scenario contains SHALL text. New behavioral scenarios: 2 + 3 + 2 in persistence and
  5 in durable-runtime; the process-only scenario is gone.
- **Guard source:** adds one validator (`ValidateDagRunContractPlanning`) and no Fact or Theory.
  It now pins 0.2 and 1.1 complete, binds the process approval evidence and pins 1.3–3.2 and
  reshape 8.4/8.5 open.
- **Scope:** no `src/**`, canonical, public API baseline or friend changes.
- **Current-match refresh:** emulated, because Windows PowerShell 5.1 lacks `SHA256.HashData`.
  16 entries, 102 matches, 0 stale, equal to the fixture.
- **API catalog:**
  - its approved-baseline hashes equal the committed `OrcaCore` (`ef13f660…`),
    `Runtime.Protocol` (`ed710c56…`) and `Provider.Abstractions` (`d9549da7…`) baselines;
  - added keys are 7 + 194 + 149 = 350, with 0 removed;
  - the standalone proof compiles independently, Release `-warnaserror`, with only `OrcaCore`
    referenced: 0 warnings, 0 errors.

  I did not re-run the catalog's isolated extraction.

## 2. Validation

| Lane | Result |
|---|---|
| Debug and Release non-incremental `-warnaserror` builds | 0 warnings, 0 errors |
| Exact package feed / package fixtures | 12 packages / 8 green, exactly `dag-hosting` red |
| Compile fixtures (`Green`) / public API baseline | green / 14/14 |
| Core / Ephemeral / Durable / Acceptance / Hosting / ProviderCertification | 350 / 79 / 99 / 37 / 24 / 96 |
| PostgreSQL / SQL Server / Integration | 101 / 72 / 11 |
| `Disposition=Infrastructure`, Release | 243/243 |
| `Disposition=ExpectedRed` | exactly 14, all `ExecutableBehaviorExpectedRedGuards` scenarios |
| `OpenSpecCorpusGuards`, Release / strict OpenSpec | 15/15 / 21/21 |
| `git diff --check`, worktree and committed simulation | clean |
| Guards on the committed simulation | 243 passed plus the same 14 expected-red |
| Evidence state: committed simulation with the freeze cleared / 1.2 checked | Infrastructure 243/243 / 243/243 |
| Standalone signature proof (`OrcaCore` only), independent compile | 0 warnings, 0 errors |

## 3. RC-1 disposition: resolved

| Round-95 gap | Remediation | Assessment |
|---|---|---|
| No bridge path obtains a fence | `BeginTurnAsync`/`RenewTurnAsync`/`EndTurnAsync`/`AbandonTurnRequestAsync`; the captured-turn `OpenChildBridgeForTurn`; §4.1 bans every Dag.Hosting provider, reflection, `UnsafeAccessor` and expression path; DG-P13 requires a closed allowlist | resolved |
| Coordinator location contradictory | Dag.Hosting admission/mapping coordinator; Durable.Hosting broker plus join/cancel coordinator; the child adapter calls the same `AdmitMappedInput` with its captured turn and never borrows or reacquires one | resolved (one P3 on the limit's source) |
| Joins and cancellation can starve | persistent FIFO, lowest-live grant, one append per turn (`append_committed`), 5 s / 15 s absolute deadline, release before waits, per-purpose validation; bound (K+1)×15 s plus service delays, 30 s per dead waiter; DG-P14 to DG-P16 | resolved |
| Decision 5 | capacity authority is per granted turn; successive turns may use different host limits, never concurrently; this is consistent with CP-025's "host-owned limit … without treating admission as persisted ownership" | resolved |

## 4. Blocking finding

### RC-2 (P2): the 15-second turn applies to the application mapper, so a slow valid mapper can never commit and its run never terminates

The remediation puts user code inside the bounded turn and gives a late result no outcome:
- §4: "Only the currently granted admission turn reads mapping inputs/evaluates and admits."
- §3.1: "HardDeadline = granted_at + 15 seconds", which "renewal cannot extend".
- §3.1: the watchdog ends the turn even if a synchronous mapper ignores it; "its result is
  discarded", "Expiry is NOT a mapping-failure code", and "such a mapper may never admit its own
  node". Also: "No background continuation may commit that expired mapping result under a fresh
  turn."
- the durable-runtime delta, which 1.3 would make canonical: "Renewal SHALL NOT extend its
  absolute 15-second deadline; late mapper results SHALL be discarded".

Consequences:

1. **Deterministic livelock for a valid program.** Within one 15-second grant, a turn must read
   inputs, eagerly decode every successful dependency output, run the mapper, normalize and
   append. A valid mapper that needs longer fails on every attempt, on every host, because each
   attempt starts again from scratch. The node stays `Ready` with no failure code, so the run
   never terminates.
   - This contradicts CP-022: the runtime "fixed-codec-normalizes a valid input … commits it
     before child start".
   - It contradicts doc 17 §17.2.6: "exactly one codec-normalized node input commits".
   - It contradicts CP-024: the runtime continues "until the DAG is terminal".
   - Design Decision 5 claims this target preserves CP-024 behavior.
   - The same authored DAG completes on the ephemeral engine.
   - If admission follows doc 17's "Eligible nodes wait by authored ordinal" strictly, every
     later eligible node is blocked behind it too.
2. **Abandoned evaluations accumulate.**
   - The mapper is a synchronous `Func<DagNodeInputContext<TRunInput>, TNodeInput>` with no token,
     and the contract lets it "continue on its worker" after each expiry.
   - Each new turn starts another evaluation of the same node, and nothing bounds the evaluations
     already in flight.
   - A slow mapper therefore keeps overlapping copies running indefinitely, and one that never
     returns ties up another worker on every attempt.
3. **The budget is unpublished and untested.**
   - No public owner discloses a mapper time budget, and the doc 08/17 blocks say the
     "OutputOf/null/mapper policy stay unchanged".
   - DG-P14 tests only that a stalled mapper cannot block another host. No row covers the stalled
     mapper's own node.

The RC-1 properties do not require this. Once a node is `Ready`, its mapper's inputs are immutable:
the run input and committed successful outputs. Doc 17 already permits re-invoking a projector
before commit, and `CapacityDeferred` already discards speculative mappings.

**Required.** Choose one of these and freeze it in the contract, deltas and DG rows.
- **(a), recommended:** evaluate the mapping (eager decode, mapper and normalization) outside any
  turn, from the committed immutable inputs.
  - Take a short Admission turn only to revalidate version, readiness, barrier and capacity, then
    append.
  - Commit `DAG_INPUT_MAPPING_INVALID` the same way.
  - Allow at most one evaluation per node in flight on each host.
  - Turns then hold only provider I/O, and every RC-1 bound is unchanged.
- **(b):** keep evaluation inside the turn, but give a mapper that repeatedly exceeds the turn a
  bounded, observable outcome. That means:
  - a stable failure code with a stated attempt rule that a provider or host stall alone cannot
    trigger;
  - publishing the budget in CP-022 and doc 17 §17.2.6, which is an amendment of those owners;
  - bounding abandoned evaluations for each node.

Either way, add a real-storage DG row in which a mapper slower than the turn budget either commits
exactly once or fails with the stated code, without accumulating workers.

## 5. Independent negative controls

All controls ran the focused `OpenSpecCorpusGuards` lane (15 tests). J1 to J20 ran with the active
freeze cleared, as the evidence commit will leave it.

| Control | Mutation | Result |
|---|---|---|
| J0 | unmodified, freeze active | green |
| J1 | evidence state: freeze cleared | green |
| J2 | planned activation: 1.2 checked only | green, as intended |
| J3 | task 1.3 checked early | **red**: whole `tasks.md` pin |
| J4 | contract FIFO weakened to "grants any live QueueSequence" | **red**: successor contract pin |
| J5 | absolute deadline 15 s → 60 s | **red**: successor contract pin |
| J6 | remote joins and cancellation no longer share the FIFO | **red**: successor contract pin |
| J7 | `BeginTurnAsync` deleted from the standalone proof | **red**: signature pin |
| J8 | API catalog edited by one byte | **red**: catalog pin |
| J9 | durable-runtime behavioral scenario replaced by a process-only scenario | **red**: source-pinned proposed block hash |
| J10 | outbox observer-cleanup obligation weakened to "MAY remain" | **red**: source-pinned proposed block hash |
| J11 | contract reverts to a full snapshot per event | **red**: successor contract pin |
| J12 | rejected round-95 contract artifact edited by one byte | **red**: permanent rejected-artifact pin |
| J13 | my round-95 REJECT verdict edited by one byte | **red**: immutable history and the rejected-freeze record |
| J14 | superseded round-95 contract provenance artifact edited | **red**: permanent superseded catalog |
| J15 | `semanticApprovalEligible` set true | **red**: measured provenance |
| J16 | doc 10 block relabeled "Approved" | **red**: Task 7.3 row and document block pin |
| J17 | remediation record edited by one byte | **red**: planning pin |
| J18 | `design.md` edited by one byte | **red**: planning pin (my round-94 P3-4 is now closed) |
| J19 | `rejectedFreezes` round-95 verdict hash altered | **red**: rejected verdict must stay byte-exact |
| J20 | reshape 8.4 checked | **red**: exact open task state |
| J21 | freeze active, successor contract edited by one byte | **red**: active content record and contract pin |
| R0 | restored | green; porcelain equals the manifest; all 43 target files equal main byte-for-byte |

The pins are sound. RC-2 concerns the decision they pin, not their enforcement.

## 6. Non-blocking observations (P3)

- **P3-1: the new DAG lanes have no defined place in operator pressure.**
  - Excluding observer notices from business pressure answers my round-95 note.
  - The contract never says where the three child kinds and unaddressed ready hints are counted.
    All three providers currently count every non-`continue` kind as external outbox pressure:
    - PostgreSQL: `kind <> @continue_kind` in `PostgreSqlWorkflowStore.Operational.cs`;
    - InMemory: `CountExternalOutbox`;
    - SQL Server: the same split.

    Those counts feed `orca.outbox.external.pending.count`. DR-050, however, requires separate
    gauges for internal continuation and external outbox records, and the contract states
    "Workflow/outbound pressure is unchanged".
  - "The new maintenance accounting reports active/expired observer counts" names no projection
    member, instrument or API, and the exact catalog adds none.
  - DAG stream growth and checkpoint lag (DU-052) and stuck classification are not addressed.
  - I did not raise the child-kind classification in round 95. State it within the existing
    surfaces, or catalog any addition, and reconcile DR-050 and DU-052.
- **P3-2: the time source conflicts with the certification method.**
  - Existing provider ports take runtime-supplied times (`OutboxClaimRequest.ClaimedAt`,
    `WorkflowOperatorStatisticsRequest.ObservedAt`), and no current provider reads the database
    clock.
  - The new lease and subscription requests carry no time, and "the provider's transaction clock
    decides all times".
  - Yet DG-P05 and DG-P14 to DG-P17 require "controlled provider time, not sleeps" on real
    PostgreSQL and SQL Server.
  - Name the seam: for example, a database clock in production plus an internal test clock
    visible only to the owning provider test assemblies, or a caller-supplied observation time
    with a stated skew bound.
- **P3-3: same-host arbitration is unspecified.**
  - With "at most ONE live request per run" for each boot owner, Dag.Hosting admission and
    Durable.Hosting join and cancellation on the same host share one slot.
  - The closed `DagRunLeaseOutcomeV1` has no outcome for a second distinct request, and the
    broker's local order is not stated.
  - The stated bound counts only provider-queue predecessors, not that local backlog.
  - Define the outcome, a local FIFO across purposes, and the resulting bound.
- **P3-4: the host limit's source for the captured-turn bridge is not in the signatures.**
  `OpenChildBridgeForTurn(DurableDagTurnV1)` and the unchanged `CommitMappedInputAsync` carry no
  limit; the contract cites "the local trusted registration's host limit", but no registration
  member is catalogued. `AdmitMappedInputAsync` meanwhile takes the limit explicitly, so the limit
  has two sources. Name one.
- **P3-5: a role-free host cannot request run cancellation.**
  - The only public cancellation is `DagRunHandle.RequestCancellationAsync`, which requires
    `AddOrcaCoreDag`.
  - The scenario "Role-free host requests cancellation" and DG-P15 therefore name no reachable
    trigger.
  - Either name the entry point, or reword them to "another DAG host while a different host holds
    the admission turn".
- **P3-6: no record sets a node to `Running`.** `DagNodeStateV1.Running` and `StartedAt` exist, but
  none of the eight record kinds moves a node from `StartAuthorized` to `Running`, and §5 gives no
  transition table. This was already present in round 95, and I missed it then. Either define the
  transition, or drop the persisted state, given that "StartAuthorized projects as public Running".
- **Recorded:**
  - each of the 16 document blocks still links the rejected artifact as "Full exact contract", with
    the successor appended; make the successor the primary link in the next refresh;
  - design "Risks" still says "Fence one admission owner";
  - `refresh-review-manifest-current-matches.ps1 -Check` cannot run under Windows PowerShell 5.1,
    so it was emulated.

## 7. Reviewer hygiene and next step

`HEAD` is `dab498ff54321d802b36ce052100954bbe00487d`, with the forty-three frozen entries and
nothing staged. When this verdict was written, the repository showed exactly the frozen entries
plus this new, untracked verdict. The disposable worktrees are removed.

Do not checkpoint this target. Keep this request and manifest byte-exact as a second rejected freeze
and catalog this verdict as before. Revise the mapper-evaluation boundary for RC-2, ideally also
P3-1 to P3-6. Re-pin the planning files, blocks and provenance coherently, then refreeze for a new
independent review. The RC-1 protocol needs no redesign. Tasks 1.2–3.2 and reshape 8.4/8.5 stay
open; no canonical sync and no source.

## Determination

The fence protocol is now concrete, bridge-only and fair. Joins and cancellation from any host make
bounded progress, the round-95 notes are resolved, and the packet is exactly pinned and
reproducible. The remediation's 15-second turn also bounds the application's own mapper, yet the
contract gives a late result no outcome, so a valid DAG can stall permanently, contrary to CP-022
and CP-024. That boundary must be redrawn before this contract becomes canonical.

**Verdict:** **REJECT**
