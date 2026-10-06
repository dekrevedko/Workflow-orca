# DAG run contract RC-3 remediation independent review verdict

**Date:** 2026-10-05
**Reviewer:** independent review
**Scope reviewed:** the sixty-one-entry freeze on base `dab498ff54321d802b36ce052100954bbe00487d`,
named by
`developer-facing-interface-section-08-task-8-4-dag-run-contract-rc-3-remediation-dirty-manifest-2026-10-05.txt`.
This is `admit-durable-dag-run-progression` task 1.1, revised after my round-97 REJECT. It contains:
- the RC-3 run contract (`task-1-1-run-contract-rc-3-remediation-2026-10-05.md`, 81,818 B);
- its API catalog (74,939 B), standalone signature proof (13,605 B) and remediation record
  (13,698 B);
- the revised deltas, proposal, design and tasks;
- the 16 proposed document blocks, a dated note in `00-stack-decisions.md`, and the Task 7.3,
  Task 8.0 and reshape `tasks.md` refreshes;
- all three rejected packets and the unreviewed RC-2 draft, retained byte-exact;
- guard pins, the registry and provenance.

**Authorization requested:** checkpoint of this contract target, then the 1.2-only activation.

This verdict authorizes that checkpoint and the 1.2-only activation, in the sequence the request
states. It authorizes no canonical sync (1.3/1.4), no source and no archival.

## Summary

RC-3 is resolved, and so are all three round-97 notes.

- **Separate bound.** `MaxConcurrentNodes` keeps one meaning: the per-run ceiling on admitted
  children and start intents. Physical evaluation uses a distinct internal constant,
  `DagEvaluationParallelismPerRun = 1`, with one isolated lane per run per host, a dedicated
  worker per dispatched lane, and a round-robin dispatcher that never waits on a mapper.
- **Isolation.** A slow or stuck mapper, including one in a cancelled run, holds only its own
  run's lane, so other runs keep evaluating. The cost is disclosed: threads scale with
  concurrently evaluated and retired runs, with no process-wide cap.
- **Bounded speculation.** Each host evaluates only the lowest `Ready` ordinal inside the run's free
  window, evaluates nothing when the window is full, and holds at most one evaluation or receipt
  per run per host, with no look-ahead.
- **Ordinal admission.** Under the fence, a successful receipt for *n* commits only when the
  window has room and no lower-ordinal node is `Ready`. Otherwise it gets `CapacityDeferred`. A
  newly `Ready` lower node invalidates a held higher receipt, which prevents a single-host
  deadlock. CP-027 and AC-617 hold by construction, and DG-P22 certifies ties under skewed mapper
  durations on one and two hosts.
- **Notes.**
  - A failed child join now blocks its descendants in the same append.
  - Ready hints are acknowledged when the local scheduler accepts the wake, never across a mapper,
    and continuous wrapping discovery recovers lost or poisoned hints and host death (DG-P24).
  - The stale RC-1 wording is gone, and the scenario is retitled.
- **Evidence.** The packet, pins and validation all reproduce, and every targeted control is red.

The remaining observations are non-blocking.

## Method

Validation ran in one disposable detached worktree at `dab498f`, and probes in a second. Both held
the same sixty-one entries.

- Every script asserted its disposable location and that the main `HEAD` was still `dab498f`.
- Main was never modified, and the review created no ref in the reviewed repository.
- Provenance and the superseded catalog were recomputed with my own scripts.
- The deltas were compared block-by-block with canonical, and checked with
  `openspec show --json --deltas-only` (CLI 1.6.0).
- The round-97 reconstruction table was compared row-for-row with my own round-97 record.
- The RC-3 contract was diffed against RC-2 and read against CP-024/025/027, AC-609/611/617,
  doc 17's limit taxonomy, NF-030/031, the outbox port, and the scenarios and SHALL text of both
  deltas.

## 1. Freeze anchors and provenance

| Anchor | Claimed | Reproduced |
|---|---|---|
| Raw porcelain, `--untracked-files=all --no-renames` | 61 lines, 5,714 B, `babf761c…` | identical (`babf761c710d6928a1592d3fc9655252bbf87981d9e38bd824a5447e10969745`); the NUL form has 61 entries (`7f289eeb…`); the union of diff (29) and untracked (32) paths is exactly 61; nothing staged |
| Semantic record: 46 rows | 7,003 B, `efa5c8e2…` | identical with the stated recipe |
| Active record: 60 rows | 9,876 B, `4f631367…` | identical (`4f6313678524cd2cc8cf0250896632e711257e5c64525081431dd193a5619192`), equal to the fixture |
| All-file record: 61 rows, no status column | 9,848 B, `36389606…` | identical; with the status column (my usual form) it is 10,031 B, `5090225fb711664bb6506661cfeee35989ca9971777b5e983dba9a5dfd3fb4cf` |
| Simulated checkpoint | tree `d367ced646461fb78c8c85dea5bf1fb142e640f7` | identical from a copied index (1,734 + 32 entries, no phantoms) and from a committed disposable copy; parent `dab498f`; 32 A / 29 M; all 61 blobs equal the raw bytes (`hash-object --no-filters`); cached and committed `diff --check` clean |
| Provenance (reviewer script) | 184 / 48,464 B / `af9fdd10…0b66` | identical: 173 synchronized, 4 superseded, 4 `PendingModification`, 3 bootstrap-only; semantic approval false; capability directories 22 / 1,808 B / `2869d4ad…`; canonical inventory 14 / 547 B / `7165dac4…` unchanged |

- **Rejected round-97 packet:**
  - the request (8,638 B, `052739c6…`), manifest (4,715 B, `5fd6806e…`) and my REJECT
    (24,510 B, `3cbba061…`) are unchanged;
  - all three are in immutable history and in `rejectedFreezes` row
    `reshape-dag-run-contract-rc-3-rejected`;
  - the remediation record's 53-row table equals my own round-97 all-file record, and without the
    review-manifest fixture it reproduces the round-97 active record (52 rows, 8,311 B,
    `7980b02f…`).
- **Older packets:** the round-95 and round-96 packets and the RC-2 draft are unchanged.
- **Rejected artifacts:** eleven permanent pins. The round-97 contract, catalog, signatures and
  remediation record are added unchanged.
- **Superseded provenance catalog:** now 12 entries. It adds the RC-2 provenance artifact
  (`ed72ca15…`) unchanged; my recomputation gives 2,105 B / `21b63710…`, equal to the guard. The
  current artifact normalizes to `bee1707d…`, equal to the fixture.
- **Planning pins:** seven whole-file pins move to the RC-3 proposal, design, tasks, contract,
  catalog, signatures and remediation record. Only the 1.2 checkbox is normalized out of the
  tasks hash.
- **Document blocks:**
  - All 16 are re-pinned. Each links RC-3 first, relabels the RC-1 and RC-2 paragraphs as rejected
    history, and appends an RC-3 paragraph.
  - Every byte outside the blocks is unchanged, except a dated RC-3 note appended to
    `00-stack-decisions.md`, which Task 7.3's whole-file row pins.
- **Registry:** three of the four `proposedDagRunSuccessors` rows change only their proposed
  block hashes, identically in source and fixture. The first persistence block is unchanged. All
  stay `Proposed`, with sync task 1.3.
- **Deltas:**
  - Each preserves the canonical statement and scenarios verbatim. OpenSpec parses every new
    paragraph into the requirement text, and no scenario contains SHALL text.
  - Durable-runtime adds three behavioral scenarios (ordinal tie, stuck-mapper isolation, wake
    acknowledged before host death) and retitles the slow-mapping join scenario.
- **Guard source:** changes only pins, counts and the two catalogs; it adds no Fact or Theory.
- **Scope:** no `src/**`, canonical, public API baseline or friend changes.
- **API:** the signature proof is byte-identical to RC-2 (`8301094b…`), which already compiled
  independently against `OrcaCore` alone and matched the catalog member for member. The catalog
  differs from RC-2 only in its date line, so the totals are unchanged at 7 + 193 + 165 = 365, with
  0 removed.

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
| Evidence state: committed simulation with the freeze cleared / then 1.2 checked | Infrastructure 243/243 / 243/243 |

## 3. RC-3 and round-97 notes: dispositions

| Round-97 item | Remediation | Assessment |
|---|---|---|
| RC-3(a): `MaxConcurrentNodes` reused as a host-global mapper ceiling | single meaning restored; internal `DagEvaluationParallelismPerRun = 1`; dedicated per-run lanes; nonblocking round-robin; stuck mapper keeps only its own lane; doc 17 taxonomy dispositioned; DG-P23 | resolved |
| RC-3(b): commits follow mapper completion | fenced recheck: F > 0, L < F and L = 0; otherwise `CapacityDeferred` with no effects; new-lower invalidation; DG-P22 on one and two hosts | resolved (stronger than CP-027 requires; throughput cost disclosed) |
| RC-3(c): speculation not tied to capacity | lowest `Ready` ordinal inside the free window only; none at F = 0; one evaluation or receipt per run per host; one coalesced admission request | resolved |
| RC-3(d): duplicates and dispositions | cross-host duplicates explicit and bounded, one per run per boot host; docs 08 (CP-025/027), 12 (AC-609/611/617) and 17 dispositioned | resolved |
| P3-1 transition table | non-success join moves every reachable `Pending`/`Ready` descendant to `DependencyBlocked` in the same append; success alone readies dependants | resolved |
| P3-2 ready-hint lifecycle | acknowledgement at validated local wake acceptance; no claim across user code; continuous wrapping discovery, ≤ 5 s between passes; poison never fails a run; DG-P24 | resolved |
| P3-3 stale wording | RC-1/RC-2 paragraphs relabeled as rejected history; scenario retitled "…during slow out-of-turn mapping" | resolved |

## 4. Independent negative controls

All controls ran the focused `OpenSpecCorpusGuards` lane (15 tests). J1 to J30 ran with the active
freeze cleared, as the evidence commit will leave it.

| Control | Mutation | Result |
|---|---|---|
| J0 | unmodified, freeze active | green |
| J1 | evidence state: freeze cleared | green |
| J2 | planned activation: 1.2 checked only | green, as intended |
| J3 | task 1.3 checked early | **red**: whole `tasks.md` pin |
| J4 | contract: `MaxConcurrentNodes` again caps a host-global mapper pool | **red**: RC-3 contract pin |
| J5 | contract: all runs share one evaluation lane | **red**: RC-3 contract pin |
| J6 | contract: dispatcher visits lanes in any order | **red**: RC-3 contract pin |
| J7 | contract: evaluation may start at F = 0 | **red**: RC-3 contract pin |
| J8 | contract: bounded look-ahead allowed | **red**: RC-3 contract pin |
| J9 | contract: fenced lower-ordinal check removed | **red**: RC-3 contract pin |
| J10 | contract: receipts evicted for time or capacity | **red**: RC-3 contract pin |
| J11 / J12 / J13 | contract: DG-P22 / DG-P23 / DG-P24 row deleted | **red**: RC-3 contract pin (each) |
| J14 | contract: non-success join blocks no descendant | **red**: RC-3 contract pin |
| J15 | contract: ready claim held across mapping | **red**: RC-3 contract pin |
| J16 | contract: discovery reduced to startup only | **red**: RC-3 contract pin |
| J17 | durable-runtime delta: stuck-mapper isolation scenario deleted | **red**: source-pinned proposed block hash |
| J18 | persistence delta: "A ready claim MAY span mapper evaluation" | **red**: source-pinned proposed block hash |
| J19 | rejected RC-2 contract artifact edited by one byte | **red**: permanent rejected-artifact pin |
| J20 | RC-2 provenance artifact edited | **red**: permanent superseded catalog |
| J21 | my round-97 REJECT edited by one byte | **red**: immutable history and the rejected-freeze record |
| J22 / J23 / J24 | doc 08 / 12 / 17 block: "also a host-global mapper pool" | **red**: document block pin (each) |
| J25 | `00-stack-decisions.md` appended RC-3 note edited, outside the block | **red**: Task 7.3 whole-file row |
| J26 | signatures: explicit host-limit parameter removed | **red**: signature pin |
| J27 | API catalog edited by one byte | **red**: catalog pin |
| J28 | `semanticApprovalEligible` set true | **red**: measured provenance |
| J29 | `rejectedFreezes` round-97 verdict hash altered | **red**: rejected verdict must stay byte-exact |
| J30 | reshape 8.4 checked | **red**: exact open task state |
| J31 | freeze active, RC-3 contract edited by one byte | **red**: active content record and contract pin |
| R0 | restored | green; porcelain equals the manifest; all 61 target files equal main byte-for-byte |

These are document and guard controls, not scheduler behavior tests. DG-P22 to DG-P24 remain
future real-storage exits.

## 5. Non-blocking observations (P3)

- **P3-1: the delta makes the deadlock-avoiding discard optional.**
  - Durable-runtime says a newly `Ready` lower ordinal "MAY invalidate one higher speculative
    receipt to preserve single-host progress". It also requires "at most one evaluation OR
    retained receipt … per run/host".
  - On a single DAG host, a lane holding a higher receipt that cannot commit (L > 0) has no other
    way to evaluate the lower node. A conforming implementation that never invalidates therefore
    stalls the run.
  - Contract §4.2 makes the discard mandatory ("discard that higher-node speculative receipt"),
    and DG-P22 certifies it, so source is bound correctly. The canonical text is weaker than the
    contract, though.
  - Make it SHALL at the next reviewed edit of this delta, before or with the 1.3/1.4 transition.
    Until then, treat §4.2 as governing.
- **P3-2: the disclosed thread trade-off has no operator signal.**
  - Live and retired evaluation lanes, including stuck mappers in cancelled runs, are the one
    resource §4.2 says can grow without a cap.
  - No gauge or projection reports them: §6.2 adds none, and `StuckDagRunCount` covers only
    nonterminal runs.
  - Add a lane gauge (for example by state: active or retired) in the source review, and say
    whether dispatched lanes may reuse idle dedicated workers. Then fast mapping need not create a
    thread per dispatch.
- **P3-3: discovery cost per pass is unstated.**
  - Every DAG host pages all nonterminal runs continuously, with at most 5 s between passes, and
    "reconsiders" `Ready` work on each pass.
  - `DagRunDiscoveryV1` carries `StreamVersion`, but the contract doesn't say that unchanged runs
    with no actionable local work are skipped without loading their checkpoints.
  - Without that, idle long-running runs cost one checkpoint read per host per pass. State the skip
    rule; NF-030 asks designs to avoid such traps.
  - Also, the §10 doc 16 disposition still says only "Startup discovery recovers runs".
- **P3-4: duplicated section.** The Task 7.3 artifact contains the heading "Proposed RC-3 DAG
  scheduling pin refresh (2026-10-05)" and its paragraph twice. It is harmless and pinned, but
  remove the copy in the next edit of that artifact.
- **Recorded:**
  - The serial rule (lowest `Ready` ordinal only, even with free slots) is a disclosed throughput
    choice, stronger than CP-027 needs. A slow lower-ordinal mapper delays its own run's higher
    nodes, and one started while it was lowest can delay a lower node that becomes `Ready` later on
    the same host.
  - Cross-host duplicates make a slow mapper run once per DAG host.
  - `refresh-review-manifest-current-matches.ps1 -Check` still cannot run under Windows PowerShell
    5.1. My emulation gives 16 entries, 102 matches and 0 stale.

## 6. Reviewer hygiene and checkpoint sequence

`HEAD` is `dab498ff54321d802b36ce052100954bbe00487d`, with the sixty-one frozen entries and
nothing staged. When this verdict was written, the repository showed exactly the frozen entries
plus this new, untracked verdict. The disposable worktrees are removed.

Checkpoint sequence, as the request states:
1. Confirm zero drift against the 61-entry manifest and the anchors above.
2. Stage exactly those 61 paths on `dab498f`, expecting tree `d367ced6…`, and commit.
3. In its only direct child, add and catalog this verdict and clear the active freeze, keeping the
   history pointer.
4. Activate by checking task 1.2 only.

The atomic 1.3/1.4 canonical and registry transition needs its own independent review. The
four successor rows stay `Proposed` until then, and source stays unauthorized. Tasks 1.3–3.2 and
reshape 8.4/8.5 stay open.

## Determination

The contract now keeps every earlier resolution:
- bridge-only, bounded and fair lease turns;
- mapping outside turns, with immutable receipts;
- exact pressure, clocks, same-host queueing, limits, cancellation and state.

It also reconciles the evaluation scheduler with the normative DAG rules. `MaxConcurrentNodes`
keeps its single per-run meaning, evaluation is isolated per run, speculation is bounded by the
free window, and admission follows authored order under the fence. A failed child's descendants
block in the same append, and ready-hint loss is recoverable. The packet is exactly pinned and
reproducible, and the controls show that every decision is guarded. The four observations above
are non-blocking.

**Verdict:** **APPROVE**
