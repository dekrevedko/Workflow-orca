# DAG hosting runtime-view process gate independent review verdict

**Date:** 2026-09-30
**Reviewer:** independent review
**Scope reviewed:** the fifteen-entry freeze on base `6d49716384b50e2cbaf503fcd782b63c6384bc1e`,
named by
`developer-facing-interface-section-08-task-8-3-runtime-view-successor-process-dirty-manifest-2026-09-30.txt`.
This is `admit-dag-hosting-runtime-view` task 0.1, the process-only successor gate.
**Authorization requested:** checkpoint of this process target only. This verdict does not
authorize:
- the `OrcaCore.Dag -> OrcaCore.Dag.Hosting` friend contract (tasks 1.1–1.2);
- canonical synchronization or any registry promotion (tasks 1.3–1.4);
- a friend attribute, codec access for `OrcaCore.Dag`, or Task 8.3 source.

## Summary

The target registers the proposed runtime-view successors as process evidence only, and its new
checks resist coherent forgery:

- **Ownership.**
  - Registry schema 6 keeps the authoring `Proposed`, `ApprovedPending`, and `Complete` records
    unchanged and adds two source-pinned `proposedRuntimeViewSuccessors` rows.
  - Only the two exact headings admit the three-owner chain
    reshape -> completed authoring friend -> proposed runtime view. The guard checks the owner
    set, both operations, the predecessor and proposed block hashes, the stage, and the
    turns-green task.
  - It also validates the authoring closeout chain: task 3.2 checked, one terminal APPROVE, and
    the single-parent evidence commit `dbc3086` on checkpoint `b5fb28e`.
- **No invented approval.**
  - The authoring blocks still equal canonical (`Synchronized`). The proposed blocks classify as
    `PendingModification`, with 2 pending operations and semantic approval false.
  - Task 0.1 is checked, and tasks 1.1–3.2 are pinned open.
- **Provenance.** I reproduced the base record (178 rows) and the target record (180 rows) with
  my own implementation; both match the fixture.
- **History.** The former current approved-pending artifact moves, unedited, into the permanent
  superseded catalog. All 427 immutable-history records are byte-exact.
- **Round 85 P3 resolved.** Only the Task 8.0 map's 8.4 row changes. It now names the approved
  source checkpoint `a9f835f` and the checkpointed closeout `b5fb28e`, labels the runtime view
  proposed and uncompiled, and keeps the sole DAG-to-durable bridge statement.
- **Scope:**
  - no product source, canonical spec, friend attribute, public API, package fixture, existing
    delta, reshape task ledger, or test declaration changes;
  - the plan keeps fixed-codec normalization, fingerprinting, and commit on the durable bridge.

No P0–P2 findings.

## Method

Validation ran in one disposable detached worktree. Probes ran in a second one. Both held the same
fifteen entries. Every script asserted its disposable location and that the main `HEAD` was still
`6d49716`. Main was never modified, and the review created no ref in the reviewed repository.

## 1. Freeze anchors and provenance

| Anchor | Claimed | Reproduced |
|---|---|---|
| Raw porcelain, `--untracked-files=all --no-renames` | 15 lines, 1,303 B, `9c4bc8db…23e6` | identical; the union of diff and untracked paths is exactly 15; nothing staged |
| Semantic record: 11 rows, ordinal sort, `path<TAB>bytes<TAB>sha256` | 1,620 B, `35b340f3…3cc9` | identical with the stated recipe |
| Active record: all except `review-manifest-provenance.json` | pinned in the fixture | 14 rows, 2,206 B, `310e0664…`, equal to the fixture |
| All-file content record, 15 rows (reviewer) | — | 2,361 B, `1c88a74f843ce81490acfc34d84b7b03b02b47098097fb621309430724195718` |
| Simulated checkpoint (reviewer) | not stated in the packet | tree `d8f7d84fe751c323c11e2190b7c03a39f1d8cc5e`, identical from a copied index and from a committed disposable copy; parent `6d49716`; 9 A / 6 M; all blobs equal the raw bytes |

- **Prior chain:**
  - checkpoint `b5fb28e` has the approved tree `2ae62652` and parent `738c3b5`;
  - evidence `dbc3086` is its only child: it adds the round-85 verdict byte-exact (7,888 B,
    `221c60c3…`), catalogs it, and clears the active freeze;
  - activation `6d49716` changes only the task 3.2 checkbox, and all thirteen authoring tasks are
    checked.
- **Blocks:**
  - Under the guard's block algorithm, the predecessor hashes `bed102a2…` and `bbae0c22…` equal
    both canonical and the authoring deltas.
  - The proposed hashes are `5561f46b…` and `feab5ce4…`.
  - A word diff shows each proposed block is canonical plus one friend-list insertion, one
    appended paragraph, and three or one new scenarios.
- **Provenance (my own Python reproduction):**
  - base: 178 rows, 46,784 B, `0dd47120…`;
  - target: 180 rows, 47,327 B, `77d388fa…` (173 synchronized, 2 superseded, 2 pending
    modifications, 3 new-capability);
  - active capability directories: 20, 1,645 B, `dfabdc30…`;
  - canonical directories: 14, 547 B, `7165dac4…`; no canonical file changed.
- **Catalogs and pins:**
  - superseded catalog: 4 entries hashing to `566eda77…`, each artifact matching its pin;
  - current artifact: `91c0cba8…`, equal to the guard pin;
  - all 427 history records are byte-exact, including the new manifest and request (11,885 B,
    `f0929aaf…`);
  - Task 8.0 map pin: `dbb6721b…` -> `54b6fc8c…`, from a single changed row.
- **Current-match refresh:** this host's Windows PowerShell 5.1 lacks `SHA256.HashData`, so I
  emulated the script's ordered comparison. Result: 16 entries, 106 current matches, 0 stale.

## 2. Validation

| Lane | Result |
|---|---|
| Debug and Release non-incremental `-warnaserror` builds | 0 warnings, 0 errors |
| Exact package feed | 12 packages |
| Core / Ephemeral / Durable / Acceptance / Hosting / ProviderCertification | 350 / 79 / 99 / 37 / 24 / 96 |
| PostgreSQL / SQL Server / Integration | 101 / 72 / 11 |
| `Disposition=Infrastructure`, Release | 240/240 |
| `Disposition=ExpectedRed` | exactly 14 failures, all `ExecutableBehaviorExpectedRedGuards` scenarios |
| `OpenSpecCorpusGuards`, Release | 15/15 |
| Package fixtures, `Green` / `ExpectedRed` | 8 green / exactly `dag-hosting` red |
| Strict OpenSpec | 20/20 |
| `git diff --check`, worktree and committed simulated checkpoint | clean |
| Guards on the committed simulated checkpoint | 240 passed plus the same 14 expected-red |
| `src/**` and canonical `openspec/specs/**` | unchanged |

## 3. Independent negative controls

These are my own controls, independent of the fifteen the author reported. Each was restored
byte-exactly, with rebuilds where source changed.

| Control | Mutation | Result |
|---|---|---|
| Q0 | unmodified: canonical gate / Task 5.3 ownership / product friend allowlist | green / green / green |
| Q1 | runtime-view deltas copied into a new active change (fourth owner) | **red**: exact owner count; Task 5.3 also red |
| Q2 | runtime-view delta header `MODIFIED` -> `ADDED`, block bytes unchanged | **red**: successor operation |
| Q3 | fixture and source predecessor -> reshape, hashes repinned to the reshape blocks | **red**: exact ordered three-owner chain |
| Q4 | fixture and source stage -> `ApprovedPending` | **red**: stage must stay `Proposed` |
| Q5 | proposed block made equal to canonical, hash repinned in fixture and source | **red**: must classify as `PendingModification` |
| Q6 | map 8.4 row claims the runtime view approved and compiled | **red**: active-freeze content record; with that record coherently refreshed, **red** at the Task 8.0 map pin |
| Q7 | closeout verdict body edited, SHA repinned in source | **red**: evidence-commit blob mismatch |
| Q9 / Q10 | task 0.1 reopened / task 1.2 checked | **red** / **red**: literal task states |
| Q11 | task 1.3 loses its `repository-foundation` (1) owner text | **red**: turns-green task text |
| Q12 | second same-heading block inside the runtime-view delta | **red**: exact owner count |
| Q13 | runtime-view change withdrawn with no registry change | **red**: the authoring chain requires three owners |
| Q14 | duplicate runtime-view row appended to the fixture | **red**: exact source-pinned rows |
| Q15 | `InternalsVisibleTo("OrcaCore.Dag.Hosting")` added to `OrcaCore.Dag` | **red**: `ProductFriendAssemblies_AreExactlyApproved` |
| Q8 (informational) | task 0.2 checked | green by design: activation is checkbox-only, as in the authoring precedent |
| R0 | restored | all green; porcelain equals the manifest; the 15 target files equal main byte-for-byte |

## 4. Non-blocking observations (P3)

- **P3-1: the tree is not in the packet.** The request says the simulated tree is published
  outside the self-inclusive file, but neither the request nor the handoff states it. I reproduced
  `d8f7d84fe751c323c11e2190b7c03a39f1d8cc5e` from both a copied index and a committed copy.
  Confirm it before staging, and state the tree in future handoffs.
- **P3-2: proposed wording for contract Task 1.1.** Once two DAG grants exist, three phrases become
  ambiguous or stale:
  - "The new grant" in `developer-facing-surface` paragraph 1;
  - "the DAG grant" in `repository-foundation` paragraph 1;
  - the scenario text "exactly one new `OrcaCore -> OrcaCore.Dag` friend is present".

  Name each grant explicitly. The contract should also state that the durable bridge decodes
  committed dependency outputs before the evaluator receives them, as design section 3 implies.
  These blocks are source-pinned, so revise them in Task 1.1 with a reviewed repin.
- **P3-3: the authoring task 3.2 text is stale.** The line is now checked, but it still says the
  closeout "is not yet approved or checkpointed" and "stays open". The checkbox-only activation
  followed round 85's instruction. Refresh the text in a later reviewed document target, not in
  this checkpoint or its activation.
- **Recorded coupling (no action here):** the authoring successor validator now requires exactly
  three owners (Q13). Withdrawing this proposal, or archiving the authoring change before it
  resolves, therefore needs a reviewed registry change. The atomic 1.3/1.4 target must also define
  how the authoring rows become superseded, because the current supersession lookup matches only
  reshape predecessors.

## 5. Reviewer hygiene and checkpoint instructions

`HEAD` is `6d49716384b50e2cbaf503fcd782b63c6384bc1e`, with the fifteen frozen entries and nothing
staged. When this verdict was written, the repository showed exactly the frozen entries plus this
new, untracked verdict. The disposable worktrees are removed.

This approval covers only the frozen bytes. To checkpoint:
- stage exactly the 15 manifest paths from the live index;
- confirm the tree is `d8f7d84fe751c323c11e2190b7c03a39f1d8cc5e` with parent `6d49716`;
- commit that tree.

This verdict is not part of the checkpoint. The evidence commit that follows must:
- have the checkpoint as its only parent;
- add this verdict byte-exact and catalog it;
- clear the active freeze.

The activation should then check task 0.2 only. The next target is contract Tasks 1.1–1.2,
including P3-2 and P3-3, followed by the atomic 1.3/1.4 transition. No runtime friend, codec access,
or Task 8.3 source may land before those gates.

## Determination

The runtime-view successor is registered as proposal-only ownership. It is bounded to two exact
headings and an ordered three-owner chain, pinned in guard source, and kept honest by an
independently reproducible provenance record. It invents no approval, and it preserves every
earlier approval byte-exact.

**Verdict:** **APPROVE**
