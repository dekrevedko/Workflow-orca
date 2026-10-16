# DAG run contract RC-2 refreeze independent review verdict

**Date:** 2026-10-05
**Reviewer:** independent review
**Scope reviewed:** the fifty-three-entry freeze on base `dab498ff54321d802b36ce052100954bbe00487d`,
named by
`developer-facing-interface-section-08-task-8-4-dag-run-contract-rc-2-refreeze-dirty-manifest-2026-10-05.txt`.
This is `admit-durable-dag-run-progression` task 1.1, revised after my round-96 REJECT. It contains:
- the RC-2 run contract (`task-1-1-run-contract-rc-2-remediation-2026-10-05.md`, 71,351 B);
- its exact API catalog (74,904 B), standalone signature proof (13,605 B) and remediation record
  (11,485 B);
- the four revised deltas, proposal, design and tasks;
- the 16 proposed document blocks, the Task 7.3 and Task 8.0 refreshes, and the reshape 8.4/8.5
  links;
- both rejected packets, retained byte-exact, and the unreviewed rc-2-remediation draft
  request and manifest;
- guard pins, the registry and provenance.

**Authorization requested:** checkpoint of this contract target, then the 1.2-only activation.

This verdict authorizes no checkpoint, no canonical sync (1.3/1.4), no source and no archival.

## Summary

RC-2 is resolved, and so are all six round-96 notes.

- **Evaluation boundary.** Eager decode, the application mapper and fixed-codec normalization now
  run before `BeginTurnAsync`, through a read-only evaluation adapter. A completed result becomes
  an immutable prepared receipt that survives unrelated version conflicts and `CapacityDeferred`.
  A fresh short turn only rechecks binding, readiness, barrier, capacity and version, then appends
  the receipt's exact bytes. There is no mapper time budget and no expiry failure, and DG-P19
  certifies a mapper slower than 15 s committing exactly once.
- **RC-1 protocol.** The bridge-only FIFO lease, one append per turn, 5 s lease and absolute 15 s
  deadline, and release before every wait are unchanged.
- **Round-96 notes.** Pressure has fifteen exact `WorkflowOperationalPressure` additions and seven
  named gauges. Time comes from the database in production and from an internal, owning-provider
  test clock in certification. Same-host requests share one local FIFO, with explicit `OwnerBusy`
  and `LocalQueueFull`. The host limit is one explicit parameter. Cancellation originates on a
  DAG-role host, and the persisted `Running` node state is gone.
- **Evidence.** The packet, pins and validation all reproduce.

The new scheduler introduces one blocking defect. It reuses `MaxConcurrentNodes`, the per-run
admitted-child ceiling, as a host-global ceiling on mapper evaluations across all runs, and it
commits receipts in whatever order evaluations finish. Doc 17 requires those limits to stay
distinct, and CP-027 requires ready ties to be admitted by authored ordinal, "never … wall-clock
order". In practice one run's slow or stuck mappers can delay or block every other run's mapping
on a host, and on every DAG host that evaluates the same node. Neither change is dispositioned
against docs 08, 12 or 17.

## Method

Validation ran in one disposable detached worktree at `dab498f`. Probes ran in a second, and
independent compiles in the scratchpad. Both worktrees held the same fifty-three entries.

- Every script asserted its disposable location and that the main `HEAD` was still `dab498f`.
- Main was never modified, and the review created no ref in the reviewed repository.
- Provenance and the superseded catalog were recomputed with my own scripts.
- The deltas were compared block-by-block with canonical, and checked with
  `openspec show --json --deltas-only` (CLI 1.6.0).
- The round-96 reconstruction table was compared row-for-row with my own round-96 record.
- The standalone proof was compiled independently, then reflected and compared member-by-member
  with the API catalog.
- The contract was read in full against both trees, the current provider and outbox ports, the
  operational pressure queries, and the doc 08 / 12 / 17 DAG concurrency and ordering rules.

## 1. Freeze anchors and provenance

| Anchor | Claimed | Reproduced |
|---|---|---|
| Raw porcelain, `--untracked-files=all --no-renames` | 53 lines, 4,715 B (handoff) | identical (`5fd6806e0721312621af4353945a1955c943f63f927c79ebb94a30a596f93943`); the NUL form has 53 entries (`a15c30a0…`); the union of diff (29) and untracked (24) paths is exactly 53; nothing staged |
| Semantic record: 41 rows | 6,061 B, `3b962439…ba25c` | identical with the stated recipe |
| Active record: 52 rows | the fixture's `activeFreeze` | 8,311 B, `7980b02f023451ca63f437e0d69bf2782f94a3c8c69188d0aa90b2641d853a4e`, equal to the fixture |
| All-file content record, 53 rows (reviewer) | — | 8,466 B, `99589e2c78d5d0acbbd40b71298af312b943470dc9f399d3e7918512b39a4409` |
| Simulated checkpoint | tree `b81c0da8585f4c93fb1faa9e6a16768ee6a209b3` (handoff) | identical from a copied index (1,734 + 24 entries, no phantoms) and from a committed disposable copy; parent `dab498f`; 24 A / 29 M; all 53 blobs equal the raw bytes (`hash-object --no-filters`); cached and committed `diff --check` clean |
| Provenance (reviewer script) | 184 / 48,464 B / `57620af0…9cc4` | identical: 173 synchronized, 4 superseded, 4 `PendingModification`, 3 bootstrap-only; semantic approval false; the multi-owner pairs are exactly the 2 runtime-view chains and the 4 registered pairs; capability directories 22 / 1,808 B / `2869d4ad…`; canonical inventory 14 / 547 B / `7165dac4…` unchanged |

- **Rejected round-96 packet:**
  - the request (9,224 B, `7c814469…`), manifest (3,456 B, `ba7b90e9…`) and my REJECT
    (21,654 B, `0ef31625…`) are unchanged;
  - all three are in immutable history and in `rejectedFreezes` row
    `reshape-dag-run-contract-rc-2-rejected`;
  - the remediation record's 43-row table equals my own round-96 all-file record (6,502 B,
    `868ec359…`), and without the review-manifest fixture it reproduces the round-96 active record
    (42 rows, 6,347 B, `010cf1fa…`).
- **Rejected round-95 packet:** unchanged, still in row `reshape-dag-run-contract-rc-1-rejected`.
- **Rejected artifacts:** seven permanent pins. The three round-95 artifacts are joined by the
  RC-1 contract (`24fa34f3…`), catalog (`3b776f58…`), signatures (`f655afc1…`) and remediation
  record (`12e166d5…`), all unchanged.
- **Planning pins:** seven whole-file pins, all equal to my hashes:
  - proposal (`981c7097…`), design (`a7e73ae2…`) and tasks (`a5367221…`, with only the 1.2
    checkbox normalized out);
  - the RC-2 contract (`4cab5d4e…`), catalog (`a11d2c44…`), signatures (`8301094b…`) and
    remediation record (`bb9269d9…`).
- **Document blocks:** all 16 are re-pinned. Each now links the RC-2 contract first and appends a
  dated RC-2 paragraph; every byte outside the blocks is unchanged from round 96.
- **Superseded provenance catalog:** now 11 entries. It adds the RC-1 provenance artifact
  (`82221fa6…`) unchanged; my recomputation gives 1,919 B / `c3ac3858…`, equal to the guard. The
  current artifact normalizes to `ed72ca15…`, equal to the fixture.
- **Registry:** the four `proposedDagRunSuccessors` rows change only their proposed block hashes,
  identically in source and fixture. They stay `Proposed`, with sync task 1.3.
- **Deltas:** each preserves the canonical statement and scenarios verbatim and adds its SHALL
  text before the first scenario. OpenSpec parses every new paragraph into the requirement text,
  and no scenario contains SHALL text. The durable-runtime delta adds "Slow valid mapper still
  commits", and the cancellation scenario now names another DAG-role host.
- **Other refreshes:** Task 7.3 refreshes the 11 of its 22 rows whose files this target edits
  (all 22 equal current bytes) and appends a dated paragraph. Task 8.0 and reshape `tasks.md`
  append dated paragraphs, and the reshape 8.4/8.5 lines change only their contract link.
- **Guard source:** changes only pins, counts and the two catalogs; it adds no Fact or Theory.
- **Scope:** no `src/**`, canonical, public API baseline or friend changes.
- **API catalog:**
  - its approved-baseline hashes equal the committed `OrcaCore` (`ef13f660…`),
    `Runtime.Protocol` (`ed710c56…`) and `Provider.Abstractions` (`d9549da7…`) baselines;
  - added keys are 7 + 193 + 165 = 365, with 0 removed. The differences from round 96 are exactly
    `Running` removed, `OwnerBusy` added, and the fifteen pressure properties;
  - the standalone proof compiles independently with only `OrcaCore` referenced: 0 warnings,
    0 errors;
  - its 24 public types match the catalog member-for-member. The catalog's other 8 types are
    existing types or `OrcaCore`-assembly additions that a standalone proof cannot redeclare.

  I did not re-run the catalog's isolated extraction.
- **Draft packet:** the unreviewed rc-2-remediation request (8,309 B, `cc0a1318…`) and manifest
  (4,458 B, `c3270883…`) are pinned in immutable history. The request differs from this one only
  in its manifest link, path counts, semantic hash, verdict name and the added retention note.

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
| `OpenSpecCorpusGuards`, Release / Debug / strict OpenSpec | 15/15 / 15/15 / 21/21 |
| `git diff --check`, worktree and committed simulation | clean |
| Guards on the committed simulation | 243 passed plus the same 14 expected-red |
| Evidence state: committed simulation with the freeze cleared / 1.2 checked | Infrastructure 243/243 / 243/243 |
| Standalone signature proof (`OrcaCore` only), independent compile | 0 warnings, 0 errors |

## 3. RC-2 and round-96 notes: dispositions

| Round-96 item | Remediation | Assessment |
|---|---|---|
| RC-2: mapper inside the 15 s turn, late result discarded | evaluation before `BeginTurnAsync` through a read-only adapter; prepared receipts with an immutable-input fingerprint; short recheck-and-append turn; receipts survive version conflicts and `CapacityDeferred`; no budget; DG-P19 | resolved; the new scheduler raises RC-3 |
| P3-1 pressure classification | child kinds and unaddressed hints are `InternalDag*`, addressed observers `DagObserver*`, legacy kinds stay external; 15 exact properties; 7 gauges; stream/checkpoint/stuck projections from stream heads; DG-P20 | resolved |
| P3-2 clock versus controlled time | database UTC sample after the ownership locks, clamped monotonic per run; internal `Func<DateTimeOffset>? TestUtcNow` reachable only from the owning provider tests | resolved |
| P3-3 same-host arbitration | one boot-local FIFO per run across purposes; `OwnerBusy`, `LocalQueueFull` (1,024); stable local and provider sequences; additive bound; DG-P21 | resolved |
| P3-4 limit source | `OpenChildBridgeForTurn(turn, prepared, maxConcurrentNodes)` is the single source, and direct admission takes the same value | resolved |
| P3-5 role-free cancellation | the trigger is `DagRunHandle` on a DAG-role host; role-free hosts consume committed child cancellation and joins | resolved |
| P3-6 `Running` | removed from `DagNodeStateV1`; `StartAuthorized` projects as public `Running` with a logical admission `StartedAt`; transition table added | resolved, with one omission in the table (P3-1 below) |
| Round-96 "recorded" items | successor linked first in all 16 blocks; design "Risks" updated | resolved |

## 4. Blocking finding

### RC-3 (P2): the out-of-turn evaluation scheduler conflicts with the normative DAG concurrency and ordering rules, which it does not amend

**What the target says.**
- Contract §4: "There are at most that host's validated MaxConcurrentNodes physical evaluation
  workers globally; a token-ignoring evaluation retains its slot until it returns, even after
  run/caller cancellation." Also: "another host may evaluate independently".
- The durable-runtime delta, which 1.3 would make canonical: "a host-global MaxConcurrentNodes
  worker ceiling SHALL prevent overlapping retries".
- DG-P19 expects "repeated hints launch one worker/node/host".
- `AdmitPreparedMapping` "validates expected version/fence/exact binding/readiness and the
  CURRENT owner's validated host limit". It has no ordinal check. "Eligible ties use authored
  ordinal" appears only in the evaluation paragraph.

**What the normative owners say.**
- CP-025, AC-609 and doc 17 define `MaxConcurrentNodes` as the per-run ceiling on started,
  nonterminal child instances. Doc 17 lists it among limits that "are distinct and MUST NOT share
  names or persistence claims".
- CP-027: "Ready-admission ties and all snapshot ordering use authored ordinal, never dictionary or
  wall-clock order." AC-617: "ready ties use the same ordinal". Doc 17: the authored ordinal is
  "the admission/tie-break order among nodes that are simultaneously eligible", and "Eligible nodes
  wait by authored ordinal". DG-P10 certifies "stable authored ordering".
- The §10 dispositions for docs 08, 12 and 17 mention neither change.

**Failure scenarios.**
1. **Unrelated runs are delayed by valid code.** Take `MaxConcurrentNodes = 2`.
   - Run A has two `Ready` nodes whose valid mappers take ten minutes each, which the contract
     explicitly permits.
   - Run B, from a different definition on the same host, has a root whose mapper takes 1 ms.
   - Both global workers belong to A, so B cannot even start mapping for ten minutes.
   - Mappers receive no cancellation token (`DagNodeInputContext` exposes only `RunInput` and
     `OutputOf`), so every slow mapper holds its slot until it returns.
   - Unaddressed hints from other progress in run A let each DAG host start its own evaluation of
     the same nodes, as DG-P19 anticipates. The same two nodes can then hold two slots on every DAG
     host. With `MaxConcurrentNodes = 1`, one slow mapper can stop DAG mapping on all of them.
2. **Stuck code permanently blocks healthy runs.**
   - A definition's mapper loops forever on some inputs, and an operator cancels each affected run.
   - Cancellation commits, but each evaluation "retains its slot until it returns".
   - After `MaxConcurrentNodes` such evaluations, the host evaluates nothing for any run until the
     process restarts. With one DAG host, every new run stalls at its first node, contrary to
     CP-024 for runs whose own mappers are valid.
   - The existing per-step throttles (doc 17 limit 1) are keyed by step type, so stuck work of one
     type cannot exhaust capacity for others. This pool has no such isolation.
3. **Admission order follows mapper duration.**
   - Take `MaxConcurrentNodes = 2`, with node 5 running. Nodes 1 and 2 become `Ready` together when
     their shared dependency joins.
   - The host's two workers evaluate both. Node 2's mapper returns first, takes the next Admission
     turn and commits into the only free slot. Node 1 then receives `CapacityDeferred`.
   - That is wall-clock order, which CP-027 forbids. Across hosts, the order follows whichever host
     finishes first.
4. **Speculation is not tied to capacity.** Nothing limits evaluation to nodes that could be
   admitted. A 10,000-node fan-out with `MaxConcurrentNodes = 8` may evaluate and retain up to
   10,000 normalized inputs per host ("at most one completed receipt per ready node/host"). Those
   receipts can also fill the 1,024-entry local queue ahead of joins.

The contract's statement that arbitrary mapping "contributes no mapper-duration term" holds for
turn waits. It does not hold for when an unrelated node, or a lower-ordinal node, gets evaluated
and admitted. My round-96 recommendation (a) did not raise the ordering consequence either.

**Required.** Any design that meets all of these is acceptable:
1. **A separate evaluation bound.** Give physical evaluation its own bound, not
   `MaxConcurrentNodes`: an internal constant, or a distinctly named option entered in doc 17's
   limit taxonomy. Make it fair across runs, for example round-robin, so one run's slow or stuck
   mappers cannot starve another run. State what a stuck mapper can still consume.
2. **Bounded speculation.** Evaluate only the lowest-ordinal `Ready` nodes that could fit the
   run's current free window, plus at most a small fixed look-ahead. That also bounds receipt
   memory and local queue use.
3. **Ordinal commit.** `AdmitPreparedMapping` commits a successful receipt for ordinal *n* only
   when fewer lower-ordinal `Ready`, unadmitted nodes remain than there are free slots. Otherwise
   it returns `CapacityDeferred` and keeps the receipt. A mapping-failure receipt needs no slot.
   State that higher nodes then wait for a slow lower-ordinal mapper, as doc 17 already requires.
4. **Cross-host duplicates and dispositions.** Accept cross-host duplicate evaluation explicitly
   and size for it, or coordinate it. Disposition docs 08, 12 and 17 for whatever is chosen.
5. **Certification.** Add real-storage DG rows for:
   - ordinal admission ties under skewed mapper durations, on one host and across hosts;
   - one run's slow and stuck mappers not blocking another run's evaluation on the same host.

## 5. Independent negative controls

All controls ran the focused `OpenSpecCorpusGuards` lane (15 tests). J1 to J26 ran with the active
freeze cleared, as the evidence commit will leave it.

| Control | Mutation | Result |
|---|---|---|
| J0 | unmodified, freeze active | green |
| J1 | evidence state: freeze cleared | green |
| J2 | planned activation: 1.2 checked only | green, as intended |
| J3 | task 1.3 checked early | **red**: whole `tasks.md` pin |
| J4 | contract: evaluation moved inside the granted turn | **red**: RC-2 contract pin |
| J5 | contract: overlapping evaluations per node allowed | **red**: RC-2 contract pin |
| J6 | contract: `CapacityDeferred` discards the completed receipt | **red**: RC-2 contract pin |
| J7 | contract: DG-P19 slow-mapper row deleted | **red**: RC-2 contract pin |
| J8 | durable-runtime delta: "Slow valid mapper still commits" deleted | **red**: source-pinned proposed block hash |
| J9 | contract: reserved DAG kinds may count as external | **red**: RC-2 contract pin |
| J10 | contract: test clock reachable from public DI options | **red**: RC-2 contract pin |
| J11 | contract: cancellation may jump an older join locally | **red**: RC-2 contract pin |
| J12 | signatures: explicit host-limit parameter removed | **red**: signature pin |
| J13 | contract: limit taken from a hidden registration | **red**: RC-2 contract pin |
| J14 | contract: role-free host initiates cancellation | **red**: RC-2 contract pin |
| J15 | signatures: persisted `Running` re-added | **red**: signature pin |
| J16 | my round-96 REJECT edited by one byte | **red**: immutable history and the rejected-freeze record |
| J17 | retained RC-1 contract artifact edited by one byte | **red**: permanent rejected-artifact pin |
| J18 | RC-1 provenance artifact edited | **red**: permanent superseded catalog |
| J19 | unreviewed draft request edited by one byte | **red**: immutable history |
| J20 | `semanticApprovalEligible` set true | **red**: measured provenance |
| J21 | durable-runtime registry row set to `ApprovedPending` | **red**: exact proposed successor rows |
| J22 | API catalog edited by one byte | **red**: catalog pin |
| J23 | `rejectedFreezes` round-96 verdict hash altered | **red**: rejected verdict must stay byte-exact |
| J24 | doc 08 block relabeled "Approved" | **red**: document block pin |
| J25 | RC-2 remediation record edited by one byte | **red**: planning pin |
| J26 | reshape 8.4 checked | **red**: exact open task state |
| J27 | freeze active, RC-2 contract edited by one byte | **red**: active content record and contract pin |
| R0 | restored | green; porcelain equals the manifest; all 53 target files equal main byte-for-byte |

The pins are sound. RC-3 concerns the scheduling decision they pin, not their enforcement.

## 6. Non-blocking observations (P3)

- **P3-1: the "exact" transition table omits blocking after a failed child.**
  - §5 states: "ChildJoined moves StartAuthorized/CancellationRequested -> Succeeded/Failed/Cancelled
    and eligible Pending dependants -> Ready". It says nothing about descendants of a non-success
    join.
  - The same section says "The runtime reducer validates each allowed kind/state transition" and
    "Illegal state regressions … fail closed". DG-P18 certifies the "exact state-transition table".
  - §4 says that `ApplyChildTerminal` "blocks descendants or readies dependants". The delta
    scenario "Required dependency fails", AC-611 and CP-024 all require transitive
    `DependencyBlocked`.
  - Read literally, the table either rejects a legitimate join event or leaves descendants
    `Pending`, so the run never terminates. Add "a non-success join moves reachable `Pending`
    descendants to `DependencyBlocked`".
- **P3-2: the ready hint's lifecycle during a long evaluation is undefined.**
  - §6 acknowledges a hint "only after the owning transition/hub wake has completed".
  - `IWorkflowOutboxStore` has no claim renewal, and the providers increment `dispatch_attempt` on
    every claim.
  - Once evaluation has no time limit, a hint held for it either:
    - is acknowledged before commit, in which case a host death leaves a `Ready` node with no
      durable trigger, and the contract names only startup discovery; or
    - expires and is redelivered with a growing attempt count, under a poison policy the ready pump
      does not state.
  - Name the acknowledgement point and the poison or rediscovery rule, and certify host death
    during an evaluation.
- **P3-3: stale present-tense wording.**
  - All 16 blocks keep the RC-1 sentence "the current exact contract/API catalog is
    [RC-1 remediation]", and its "role-free join/cancel coordination". This follows the new line
    saying earlier links are rejected history.
  - The durable-runtime delta keeps the scenario title "Remote join releases admission despite a
    live mapping owner", although no mapping owns a turn any more.
  - Both are re-pinned in the next refreeze anyway; mark the sentence as superseded and retitle the
    scenario.
- **Recorded:**
  - the draft rc-2-remediation request is cataloged only through immutable history. Its retention
    is explained in prose, not by a registry row. Its semantic anchor (`8835c020…`) cannot be
    re-derived from what was retained, so "no contract bytes were changed by this repair" is
    stated, not verifiable;
  - `refresh-review-manifest-current-matches.ps1 -Check` still cannot run under Windows PowerShell
    5.1. My emulation gives 16 entries, 102 matches and 0 stale, equal to the fixture.

## 7. Reviewer hygiene and next step

`HEAD` is `dab498ff54321d802b36ce052100954bbe00487d`, with the fifty-three frozen entries and
nothing staged. When this verdict was written, the repository showed exactly the frozen entries
plus this new, untracked verdict. The disposable worktrees are removed.

Do not checkpoint this target. Keep this request and manifest byte-exact as a third rejected freeze,
and catalog this verdict as before. Redesign the evaluation scheduler for RC-3, ideally also
P3-1 to P3-3, re-pin coherently and refreeze for a new independent review. The RC-1 lease protocol
and the RC-2 receipt model need no other change. Tasks 1.2–3.2 and reshape 8.4/8.5 stay open; no
canonical sync and no source.

## Determination

Mapping now happens outside every lease turn, and a slow valid mapper commits exactly once while
joins and cancellation keep moving. All six round-96 notes are resolved, and the packet is exactly
pinned and reproducible. The new evaluation scheduler, however, gives `MaxConcurrentNodes` a second,
host-global meaning, and it lets admission order follow mapper duration. That couples unrelated
runs, and it breaks doc 17's limit taxonomy and CP-027's ordinal tie-break, none of which the
contract amends. The scheduler must be reconciled before this contract becomes canonical.

**Verdict:** **REJECT**
