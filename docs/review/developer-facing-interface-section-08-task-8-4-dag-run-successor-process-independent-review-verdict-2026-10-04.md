# DAG run successor process gate independent review verdict

**Date:** 2026-10-04
**Reviewer:** independent review
**Scope reviewed:** the fourteen-entry freeze on base `095bbfd1a6b559f8f1be47fb8d4bcd4749d40854`,
named by
`developer-facing-interface-section-08-task-8-4-dag-run-successor-process-dirty-manifest-2026-10-04.txt`.
This is `admit-durable-dag-run-progression` task 0.1. It contains:
- the new change: proposal, design, tasks and two MODIFIED deltas;
- the schema-10 registry rows `proposedDagRunSuccessors`;
- pending provenance and the process provenance artifact;
- the guard source;
- the self-inclusive packet.

**Authorization requested:** checkpoint of this process gate, then the 0.2-only activation.

This verdict does not authorize:
- the run-persistence and progression contract (1.1/1.2);
- the content of any proposed block, stream/record identity, port, provider schema or result type;
- canonical sync (1.3/1.4), bridge or provider source, completion of 8.4 or 8.5, or archival.

## Summary

The process exception is narrow, exact and executable, and every prerequisite claim is true.

- **Prerequisite chain.**
  - `1d9672a` has the approved tree `4b83f4d9` and parent `c85fd0f`.
  - `095bbfd` is its only child among all refs. It adds my round-93 APPROVE byte-exact
    (13,162 B, `2fe0cac1…`), catalogs it, clears the freeze and keeps the history pointer.
  - No activation followed.
  - The new guard binds exactly that checkpoint, tree, evidence and verdict, with task 8.4 as the
    gate.
- **Exception scope.** Exactly four (capability, heading) pairs may have the two owners `reshape`
  and `admit-durable-dag-run-progression`:
  - `durable-persistence-and-outbox`: "Workflow store persists first-class durable records";
  - `durable-persistence-and-outbox`: "Outbox delivery is adapter-driven and at-least-once";
  - `durable-persistence-and-outbox`: "First-release provider ownership and schemas are exact";
  - `durable-runtime`: "Durable DAG progression is runtime owned".

  Each row is source-pinned in C#: owners, both operations, stage `Proposed`, sync task 1.3, and
  the predecessor and proposed block hashes. The guard requires:
  - the predecessor to equal canonical;
  - the proposal to differ from it;
  - the owner set to be exactly the two resolved owners.

  Existing ownership chains are untouched.
- **Full-block copying.** Each of the four deltas begins with the byte-exact canonical block and
  only appends text.
- **Provenance.** My independent reproduction measures the new pending state. Eligibility is
  false, the 4 pending rows are owned by 1.3, and the previous provenance artifact enters the
  superseded catalog unedited (now 8 entries).
- **Task pinning.** Tasks 1.1–3.2 and reshape 8.4/8.5 are pinned open; 0.2 is deliberately
  unpinned.
- **Direction.** The design and proposed blocks carry all five of my round-93 notes as
  direction, not as approved signatures:
  - the real `AppendAsync(ProviderCommitBatch)` boundary;
  - an explicit public/provider stream-identity amendment;
  - the current fenced owner's limit after restart;
  - a DAG-role-only ready partition;
  - `CapacityDeferred` distinct from `AdmissionStopped`.

No P0–P2 findings.

## Method

Validation ran in one disposable detached worktree at `095bbfd`, and probes in a second. Both held
the same fourteen entries.

- Every script asserted its disposable location and that the main `HEAD` was still `095bbfd`.
- Main was never modified, and the review created no ref in the reviewed repository.
- Provenance was recomputed with my own script.
- Each delta was compared block-by-block with canonical.
- The OpenSpec parse of the proposed blocks was inspected with `openspec show --json --deltas-only`
  (CLI 1.6.0).

## 1. Freeze anchors and provenance

| Anchor | Claimed | Reproduced |
|---|---|---|
| Raw porcelain, `--untracked-files=all --no-renames` | 14 lines, 1,198 B, `a68caa71…deca` | identical; the NUL form has 14 entries; the union of diff and untracked paths is exactly 14; nothing staged |
| Semantic record: 10 rows | 1,458 B, `8dfd3d15…6298` | identical with the stated recipe |
| Active record: 13 rows | pinned in the fixture | 2,031 B, `0249c128a84b53a064a2cd251403072f850acf6bf0c471f3b62e180a01197157`, equal to the fixture |
| All-file content record, 14 rows (reviewer) | — | 2,186 B, `e776f4be76ff10650305b55fe03cd89df0213a4ea251cc928349bbf9d7db5367` |
| Simulated checkpoint | tree `5880c8595f7b4f34026ec90f287a17bd3d63e6c6` (handoff) | identical from a copied index and from a committed disposable copy; parent `095bbfd`; 9 A / 5 M; all blobs equal the raw bytes |
| Provenance (reviewer script) | 184 rows / 48,464 B / `d76b8221…18d5` | identical: 173 synchronized, 4 superseded, 4 `PendingModification`, 3 bootstrap-only; the only multi-owner pairs are the 2 reviewed runtime-view chains and these 4 |
| Active capability directories | 22 / 1,808 B / `2869d4ad…` | identical; canonical inventory 14 / 547 B / `7165dac4…` unchanged |

- **Registry and fixtures:**
  - registry schema 9 → 10 adds only the four `proposedDagRunSuccessors`;
  - the provenance fixture records eligibility false and pending durable-persistence-and-outbox 3
    plus durable-runtime 1, both turned green by 1.3;
  - the process artifact is whole-file pinned (`e28513fa…`);
  - the superseded catalog adds `approved-pending-openspec-provenance-2026-10-03.md` unchanged at
    `612d4b9e…`;
  - immutable history admits exactly the new request (10,345 B, `0741393a…`) and manifest.
- **Guard source:** adds one validator and no Fact or Theory.
- **Scope:** no `src/**`, canonical, approved-artifact or dated-decision byte changes.
- **Current-match refresh:** emulated, because Windows PowerShell 5.1 lacks `SHA256.HashData`.
  16 entries, 106 matches, 0 stale.

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
| Evidence state: committed simulation with the freeze cleared | Infrastructure 243/243 |
| Planned activation: task 0.2 checked on that state, no guard edit | Infrastructure 243/243 |

## 3. Independent negative controls

All controls ran the focused `OpenSpecCorpusGuards` lane (15 tests). G1 to G12 ran with the active
freeze cleared, as the evidence commit will leave it.

| Control | Mutation | Result |
|---|---|---|
| G0 | unmodified, freeze active | green |
| G1 | evidence state: freeze cleared | green |
| G2 | planned activation: 0.2 checked only | green, as intended |
| G3 | contract task 1.1 checked | **red**: exact open task state |
| G4 | reshape task 8.5 checked | **red**: exact open task state |
| G5 | canonical durable-runtime hand-synced with the proposed block | **red**: canonical gate (predecessor no longer equals canonical) |
| G6 | proposed durable-runtime scenario edited (`CapacityDeferred` to `AdmissionStopped`) | **red**: source-pinned proposed block hash |
| G7 | registry fixture stage `Proposed` to `ApprovedPending` (fixture only) | **red**: source-pinned exact rows |
| G8 | registry fixture drops the durable-runtime row | **red**: source-pinned exact rows |
| G9 | superseded predecessor provenance artifact edited | **red**: permanent superseded catalog |
| G10 | current process provenance artifact edited | **red**: whole-file artifact pin |
| G11 | `semanticApprovalEligible` set true | **red**: measured provenance |
| G12 (informational) | `design.md` edited by one byte | green: proposal and design are not pinned after evidence (P3-4) |
| G13 | freeze active, `design.md` edited by one byte | **red**: active content record |
| R0 | restored | green; porcelain equals the manifest; the 14 target files, canonical spec and catalogued artifact equal main byte-for-byte |

## 4. Non-blocking observations (P3) for contract task 1.1

- **P3-1: the appended obligations parse as scenario text.**
  - All four proposed blocks append their new SHALL paragraph after the canonical scenarios.
  - OpenSpec 1.6.0 parses that paragraph into the body of the preceding canonical scenario, so it
    is not part of the requirement text. Examples:
    - "DAG hosting starts a child" absorbs the whole new lifecycle paragraph;
    - in persistence, "Start-or-deliver is accepted before a definition host exists" absorbs the
      `dag-run-v1` obligation.
  - No current canonical block has prose after a scenario, and strict validation cannot detect
    this.
  - Synced as written, the new obligations would live inside unrelated scenarios. Task 1.1 should
    place them in the requirement statement before the first scenario, keeping the canonical text
    unchanged, and re-pin.
- **P3-2: the decode-failure classification is weakened.** The proposed durable-runtime text routes
  decode failure "through a named durable bridge failure". The approved texts classify an ordinary
  declared-type decode or materialization failure as `DAG_INPUT_MAPPING_INVALID` before
  mapper, commit or start:
  - doc 17 §17.2.6 and CP-022;
  - the reshape 8.4 task text;
  - the approved child contract §5.

  Name that code, or explicitly amend those owners.
- **P3-3: the child identity wording drifts.** "Deterministic from complete run/node/workflow
  coordinates" (design Decision 6 and the delta) differs from the approved recipe. That recipe
  hashes only the run GUID and authored ordinal, and binds the workflow identity and types
  separately as a collision check. State the approved recipe, or amend it explicitly.
- **P3-4: the planning documents are unpinned after evidence.** After the freeze clears, only the
  four blocks, tasks, registry and provenance are durable (G12). Edits to `proposal.md` and
  `design.md`, including the withdrawal and archival rules in Decision 1, would pass silently until
  1.1. Either pin them in 1.1, or re-review them as part of that target.
- **Recorded:**
  - the new delta calls `IMessageDispatcher` a "compatibility transport", but PR-015 names it the
    internal provider-dispatch port; align the wording with doc 10 when 1.1 reconciles PR-015;
  - the deferred source-only `UnsafeAccessor`/expression ban and `DagRuntimeView.cs` evidence path
    remain in task 2.3, as stated.

## 5. Reviewer hygiene and checkpoint instructions

`HEAD` is `095bbfd1a6b559f8f1be47fb8d4bcd4749d40854`, with the fourteen frozen entries and nothing
staged. When this verdict was written, the repository showed exactly the frozen entries plus this
new, untracked verdict. The disposable worktrees are removed.

This approval covers only the frozen bytes. To checkpoint:
- stage exactly the 14 manifest paths from the live index;
- confirm the tree is `5880c8595f7b4f34026ec90f287a17bd3d63e6c6` with parent `095bbfd`;
- commit that tree.

This verdict is not part of the checkpoint. The evidence commit that follows must:
- have the checkpoint as its only parent;
- add this verdict byte-exact and catalog it;
- clear the active freeze while keeping the immutable-history manifest pointer.

The activation should then check task 0.2 only; that rehearses green. Contract 1.1 follows as its own
reviewed target. Do not check 1.1–3.2 or reshape 8.4/8.5, start source, or archive.

## Determination

The successor gate admits exactly four pending, source-pinned modifications on top of a verified
child-protocol approval. Provenance is measured honestly as ineligible, and every task and history
pin needed to stop early sync, early completion or silent history edits holds. The proposed content
is direction for 1.1, not approved here. Three wording points and one pinning gap are recorded for
that review.

**Verdict:** **APPROVE**
