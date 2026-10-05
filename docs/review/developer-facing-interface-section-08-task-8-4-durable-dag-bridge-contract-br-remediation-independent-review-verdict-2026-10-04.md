# Durable DAG child bridge contract BR remediation independent review verdict

**Date:** 2026-10-04
**Reviewer:** independent review
**Scope reviewed:** the fifteen-entry freeze on base `c85fd0f301f110db691a026bcdd0c0e35c6b26c1`,
named by
`developer-facing-interface-section-08-task-8-4-durable-dag-bridge-contract-br-remediation-dirty-manifest-2026-10-04.txt`.
This is the remediation of my 2026-10-04 REJECT (BR-1, BR-2, P3-1 to P3-4). It contains:
- the revised child-protocol contract
  `artifacts/task-8-4-durable-dag-bridge-contract-br-remediation-2026-10-04.md` (36,345 B,
  `6be4d3d9…06ce`);
- status reconciliation in `CLAUDE.md`, doc 08 CP-022 and doc 17 §17.2.6;
- the Task 8.4 link and gate;
- guard-source pins;
- the byte-exact rejected packet and verdict.

**Authorization requested:** checkpoint of this contract-only target.

This verdict does not authorize:
- bridge source, or the dedicated DAG pump in code;
- the section 10 run/progression API, ready kind, `dag-run-v1` aggregate, stream or schema shape;
- any canonical or numbered amendment;
- completion of 8.4 or 8.5, or archival.

## Summary

Both blocking findings are resolved as contract decisions. The narrowed scope is unambiguous and is
gated by executable pins.

- **BR-1, dispatch.** §7 now reserves `dag-child-start/join/cancel-v1` to a dedicated,
  include-only runtime consumer over the existing outbox. That consumer is registered whenever the
  durable engine role runs, independent of `IMessageDispatcher` and of `AddOrcaCoreDag`. The
  contract also requires:
  - every external pump excludes the reserved kinds unconditionally: the Engine.Durable default
    selector, the Durable.Hosting hosted selector, caller selectors and post-claim validation;
  - a wrong-lane record is released, never acknowledged;
  - an InMemory sink is explicitly not a DAG handler;
  - each kind has a permanent-failure disposition that never frees admission or claims success.

  DR-037, PR-015 and DR-AC-029 still name only two partitions. Their extension, and any canonical
  delta, is required before source, together with a three-provider certification matrix. This
  matches the canonical internal-continuation rule and the approved Task 8.0 map: "only the
  internal runtime pump".
- **BR-2, scope.**
  - §3 now calls the six operations a child-protocol slice that "cannot create or finish a run".
  - §10 makes a separately reviewed run-persistence and progression contract mandatory before any
    bridge source. It lists every missing responsibility: create/reopen, fenced recovery and
    discovery, mapping failure with blocked closure, child terminal handling, run cancellation and
    terminalization, waits, and faults.
  - It fixes the callback direction. `IDurableDagReadySinkV1` is owned by Durable.Hosting and
    implemented by Dag.Hosting through `AddOrcaCoreDag`, via a fourth, separately reviewed kind,
    with recovery scans and subscribe/recheck rather than polling.
  - It states that a `dag-run-v1` aggregate lies outside the Task 8.0 internal-continuation
    reading, so a canonical amendment is required before implementation, and
    `WorkflowEventCodec` must not coerce unknown records.

  Task 8.4 links the contract and repeats the gate.
- **Notes.**
  - *P3-1:* the DAG child uses no caller start key. Callers cannot choose an `InstanceId`, so the
    deterministic `InstanceId` is not caller-claimable.
  - *P3-2:* a present null is the fixed-codec `null` (`6e 75 6c 6c`, which `JsonSerializer`
    emits). Presence, type and format are explicit, and the fingerprint is the uppercase SHA-256
    of the committed bytes.
  - *P3-3:* `MaxConcurrentNodes` is explicitly per run. That is consistent with "a DAG has
    reached", and both trees state no cross-run scope.
  - *P3-4:* both artifacts are pinned in guard source on the canonical gate, independent of the
    active freeze.
  - *Status:* the stale `CLAUDE.md`, CP-022 and doc 17 sentences now state the true
    evaluator-versus-bridge status, and no other stale copy remains in either tree.
- **Unchanged.** The mapping, value, identity, lineage and commit-ordering clauses I found faithful
  in round 92. The C# block is byte-identical to the one I compiled then.

No P0–P2 findings.

## Method

Validation ran in one disposable detached worktree. Probes and an independent signature compile ran
in a second. Both held the same fifteen entries.

- Every script asserted its disposable location and that the main `HEAD` was still `c85fd0f`.
- Main was never modified, and the review created no ref in the reviewed repository.
- Every revised clause was re-read against both trees and the code: the outbox pumps and
  selectors, `IMessageDispatcher` registration, `FixedWorkflowValueCodec`, `PayloadFingerprint`,
  public start signatures, `WorkflowStreamId`, `WorkflowEventCodec` and the provider ports.

## 1. Freeze anchors and provenance

| Anchor | Claimed | Reproduced |
|---|---|---|
| Raw porcelain, `--untracked-files=all --no-renames` | 15 lines, 1,446 B, `e6330a42…3734` | identical; the NUL form has 15 entries; the union of diff and untracked paths is exactly 15; nothing staged |
| Semantic record: 8 rows | 1,162 B, `dedc94ab…cf39` | identical with the stated recipe |
| Active record: 14 rows | pinned in the fixture | 2,354 B, `45a538683b1618d2c4b12f2fca978122e5af977dee8b501184eee54bddd1aa54`, equal to the fixture |
| All-file content record, 15 rows (reviewer) | — | 2,509 B, `088643ed8b8c53d1141750971e424c91b951a58ee2405d8b83e031e4127dd4ff` |
| Simulated checkpoint | tree `4b83f4d9a5cf2ecaf8d3dc2e60b6e1c56e6c02e2` (handoff) | identical from a copied index and from a committed disposable copy; parent `c85fd0f`; 7 A / 8 M; all blobs equal the raw bytes |

- **Rejected evidence:**
  - the original manifest (534 B, `a7235ef3…`), request (7,483 B, `bcd4da55…`) and artifact
    (21,406 B, `acbb4b70…`) equal their blobs in the rejected tree `80a4b5b6`;
  - my REJECT (16,148 B, `914ae808…`, LF-only, so its raw and normalized forms coincide) is
    unchanged;
  - the new rejected-freeze row has exactly the fields of the earlier runtime-view row;
  - immutable history admits the five new and rejected review files with their exact sizes and
    hashes.
- **Pins refreshed in the same freeze:**
  - doc 17 runtime-view block `01ddf544…`;
  - CP-020/CP-022 `979d6ee2…`;
  - reshape 8.3–8.5 handoff `6417b48b…`;
  - Task 7.3 row 1 (`CLAUDE.md`) and digest `30c2e083…`;
  - §17.5 and the signature digest `93e6490d` are unchanged;
  - the guard source adds two artifact pins and no Fact or Theory.
- **Provenance:** unchanged at 180 rows, 47,347 B, `40d4d8c0…`, zero pending; no canonical or
  delta file changes.
- **Current-match refresh:** emulated, because Windows PowerShell 5.1 lacks `SHA256.HashData`.
  16 entries, 106 matches, 0 stale.

## 2. Validation

| Lane | Result |
|---|---|
| Debug and Release non-incremental `-warnaserror` builds | 0 warnings, 0 errors |
| Exact package feed / package fixtures | 12 packages / 8 green, exactly `dag-hosting` red |
| Compile fixtures (`Green`) / public API baseline | green / 14/14 |
| Core / Ephemeral / Durable / Acceptance / Hosting / ProviderCertification | 350 / 79 / 99 / 37 / 24 / 96 |
| PostgreSQL / SQL Server / Integration | 101 / 72 / 11 (rerun on this freeze) |
| `Disposition=Infrastructure`, Release | 243/243 |
| `Disposition=ExpectedRed` | exactly 14, all `ExecutableBehaviorExpectedRedGuards` scenarios |
| `OpenSpecCorpusGuards`, Release / strict OpenSpec | 15/15 / 20/20 |
| `git diff --check`, worktree and committed simulation | clean |
| Guards on the committed simulation | 243 passed plus the same 14 expected-red |
| Evidence state: committed simulation with the freeze cleared | Infrastructure 243/243 |
| Activation | none planned: task 8.4 stays open, and checking it is red (F13) |
| InspectCode | not run: the C# change is twelve lines of constants and two pin assertions |
| Independent compile of the §3 C# block (byte-identical to round 92), `OrcaCore` reference only, Release `-warnaserror` | 0 warnings, 0 errors |

## 3. Independent negative controls

All controls ran the focused `OpenSpecCorpusGuards` lane (15 tests). F1 to F13 ran with the active
freeze cleared, as the evidence commit will leave it.

| Control | Mutation | Result |
|---|---|---|
| F0 | unmodified, freeze active | green |
| F1 | evidence state: freeze cleared | green |
| F2 | revised contract identity recipe changed from "first" to "last" 32 hex characters | **red**: durable artifact pin |
| F3 | revised contract routing rule weakened ("MUST exclude … unconditionally" to "MAY … when configured") | **red**: durable artifact pin |
| F4 | rejected artifact edited by one byte | **red**: rejected-artifact pin |
| F5 | doc 08 CP-022 stale status restored (base bytes) | **red**: CP-020/CP-022 pin |
| F6 | doc 17 stale null status restored (base bytes) | **red**: doc 17 runtime-view block pin |
| F7 | `CLAUDE.md` stale closeout line restored (base bytes) | **red**: Task 7.3 row |
| F8 | Task 8.4 link and gate removed (base `tasks.md`) | **red**: 8.3–8.5 handoff pin |
| F9 / F10 / F11 | rejected request / manifest / REJECT verdict edited by one byte | **red** / **red** / **red**: rejected-freeze record and immutable history |
| F12 | new request edited by one byte | **red**: immutable history |
| F13 | reshape task 8.4 checked | **red**: 8.3–8.5 handoff pin |
| F14 | freeze active, revised artifact edited by one byte | **red**: active content record and artifact pin |
| R0 | restored | green; porcelain equals the manifest; the 15 target files equal main byte-for-byte |

## 4. Non-blocking observations (P3) for the section 10 contract

- **P3-1: a port named in the proposal does not exist.** §10 proposes to "reuse
  IWorkflowEventStore, IWorkflowCommitStore and IWorkflowOutboxStore". No `IWorkflowCommitStore`
  exists. Commits go through `IWorkflowEventStore.AppendAsync(ProviderCommitBatch)`.
- **P3-2: the proposed stream is not plain reuse.**
  - A `dag-run-<guid>` stream cannot be expressed through today's ports. `ProviderCommitBatch`
    is keyed by the public `OrcaCore` type `WorkflowStreamId`, which holds only an `InstanceId`
    and renders `workflow-{id}`.
  - The section 10 amendment should treat stream identity and kind as a public and provider
    boundary change: API baseline, both production schemas and certification. It is not plain
    port reuse.
- **P3-3: the persisted ceiling needs reconciling with CP-025.** §10 commits an "effective
  per-run ceiling" at run creation. CP-025 says restart "re-applies the host-owned limit".
  Section 10 should state which value governs after a reconfigured restart or across hosts with
  different `DagHostOptions`.
- **P3-4: avoid ready-kind churn on hosts without a sink.** "No sink means release/retry" is
  correct, but a durable host without `AddOrcaCoreDag` should not include `dag-run-ready-v1` in
  its claim selector at all. Otherwise it churns claims and releases on every ready record.
- **P3-5: full-ceiling outcome is unnamed.** `CommitMappedInputAsync` has `Committed`,
  `MappingInvalid` and `AdmissionStopped`. The contract should name the outcome when a ready node
  meets a full per-run ceiling at the serialized decision: the mapping is discarded and
  re-evaluated later. Otherwise `AdmissionStopped` silently conflates that case with cancellation.
- **Still owned by the source target, as stated:**
  - forbid `UnsafeAccessor` and `System.Linq.Expressions` in Hosting and the harness;
  - add `DagRuntimeView.cs` to the permanent evidence paths.

## 5. Reviewer hygiene and checkpoint instructions

`HEAD` is `c85fd0f301f110db691a026bcdd0c0e35c6b26c1`, with the fifteen frozen entries and nothing
staged. When this verdict was written, the repository showed exactly the frozen entries plus this
new, untracked verdict. The disposable worktrees are removed.

This approval covers only the frozen bytes. To checkpoint:
- stage exactly the 15 manifest paths from the live index;
- confirm the tree is `4b83f4d9a5cf2ecaf8d3dc2e60b6e1c56e6c02e2` with parent `c85fd0f`;
- commit that tree.

This verdict is not part of the checkpoint. The evidence commit that follows must:
- have the checkpoint as its only parent;
- add this verdict byte-exact and catalog it;
- clear the active freeze while keeping the immutable-history manifest pointer.

No activation follows: tasks 8.4 and 8.5 stay open (checking 8.4 is red, F13). The next permitted
step is the separately reviewed section 10 run-persistence and progression contract, with its
required amendment and sync. It is not bridge source and not archival.

## Determination

The revised contract fixes the two decisions the rejected one left open:
- DAG runtime records belong to a dedicated runtime pump that every external partition excludes;
- the six-operation child protocol is honestly scoped, with an executable, one-way-safe gate for
  the run, persistence and progression contract and its canonical amendment.

Its remaining choices are faithful to both trees and are durably pinned.

**Verdict:** **APPROVE**
