# DAG hosting runtime-view closeout independent review verdict

**Date:** 2026-10-04
**Reviewer:** independent review
**Scope reviewed:** the twenty-three-entry freeze on base `1d8941b7c4e4a96752b33684075d152494057c49`,
named by
`developer-facing-interface-section-08-task-8-3-runtime-view-closeout-dirty-manifest-2026-10-04.txt`.
This is `admit-dag-hosting-runtime-view` tasks 3.1/3.2: the permanent `Complete` record for the
approved runtime-view source, together with the disposition of round 90's four observations.
**Authorization requested:** checkpoint of this closeout target only. This verdict does not
authorize:
- archival of either amendment;
- reshape 8.3–8.5 completion, or durable bridge, codec, or child-start work.

## Summary

The closeout records the approved source truthfully, and its new bindings resist coherent forgery:

- **Complete record.** Schema 9 adds `runtimeViewCompleteEvidence` and the activation coordinate,
  pinned in guard source. They bind:
  - checkpoint `4eb2e8d` and its tree `bd378abb`;
  - the single-parent evidence commit `2fdfa59`, which actually adds the terminal APPROVE
    (`b8c6c1da…`);
  - the source request (`0404fcb6…`) and manifest (`6d92fc3d…`), byte-equal to their blobs in the
    checkpoint;
  - implementation tasks 2.1–2.4 plus 3.1;
  - three executable-evidence paths that exist in the checkpoint.

  The guard also replays activation `1d8941b` as an exact one-checkbox diff of task 2.4. Every
  earlier `Proposed`, `ApprovedPending`, and authoring `Complete` record is unchanged.
- **Round 90's notes:**
  - *Reflection.* The metadata policy rejects `System.Reflection` types, `Type` lookups and
    `InvokeMember`, `Activator`, and `Delegate.DynamicInvoke`. It covers both the shipped Hosting
    assembly and the harness, and a compiled private-reflection probe proves it.
  - *Harness.* The harness must now pass the same 3-type, 15-member equality before its
    34 assertions run.
  - *Bridge input.* Open reshape task 8.4 and the Task 8.0 map now require the bridge to supply
    exactly the declared successful direct resultful outputs, and to prove behavior through the
    shipped Hosting assembly.
  - *Status.* Current documentation names the real source checkpoint, and dated entries are
    appended to, not rewritten.
- **Scope.** No `src/**`, canonical spec, delta, package-source, public contract, or crosswalk
  change. Provenance is unchanged.

No P0–P2 findings.

## Method

Validation ran in one disposable detached worktree. Probes and a scoped InspectCode run ran in a
second one. Both held the same twenty-three entries. Every script asserted its disposable location
and that the main `HEAD` was still `1d8941b`. Main was never modified, and the review created no
ref in the reviewed repository.

## 1. Freeze anchors and provenance

| Anchor | Claimed | Reproduced |
|---|---|---|
| Raw porcelain, `--untracked-files=all --no-renames` | 23 lines, 1,686 B, `95061a95…558d` | identical; the NUL form has 23 entries; the union of diff and untracked paths is exactly 23; nothing staged |
| Semantic record: 19 rows, ordinal sort | 2,570 B, `ee8f32eb…7176` | identical with the stated recipe |
| Active record: 22 rows | 3,161 B, `73f4db75…181d` (handoff) | identical, equal to the fixture |
| All-file content record, 23 rows (reviewer) | — | 3,316 B, `48305684e18c72090e33276ae1c4b401ae2cbe4249f86470cae8089f69908ccc` |
| Simulated checkpoint | tree `f675f04ab9e8a5b5883a3577ce870b14b077c3ce` (handoff) | identical from a copied index and from a committed disposable copy; parent `1d8941b`; 4 A / 19 M; all blobs equal the raw bytes |

- **Source chain:**
  - `4eb2e8d` has tree `bd378abb` and parent `13fe5b9`;
  - `2fdfa59` is its only child among all refs, adds the round-90 verdict byte-exact
    (11,630 B, `b8c6c1da…`), catalogs it, clears the freeze, and keeps the immutable-history
    pointer;
  - `1d8941b` checks only task 2.4.
  - The disclosed local correction of the evidence commit left no other referenced child.
- **Provenance:** 180 rows, 47,347 B, `40d4d8c0…`, zero pending.
- **Pins:**
  - doc 17 numbered block `e79eda78…`; §17.5 `ba63b470…`; CP-020/CP-022 `7d59ee90…`;
  - reshape handoff `5f85f8eb…`; Task 8.0 map `e4544277…`;
  - all 22 Task 7.3 rows, with digest `ebad624d…`;
  - closeout artifact `8a2eb6b3…`;
  - the source-validation artifact (`76d74024…`) and harness (`ee97d40d…`) are unchanged;
  - 427 baseline and 94 append-only history records are byte-exact.
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
| `OpenSpecCorpusGuards`, Release / strict OpenSpec | 15/15 / 20/20 |
| `git diff --check`, worktree and committed simulation | clean |
| Guards on the committed simulation | 243 passed plus the same 14 expected-red |
| Evidence state: committed simulation with the freeze cleared | Infrastructure 243/243 |
| Planned activation: task 3.2 checked on that state, no guard edit | Infrastructure 243/243 |
| InspectCode 2026.2.3.1, changed guard file only (independent run) | 6 `RedundantNameQualifier` warnings, zero errors or notes; equal to the retained closeout SARIF, all cosmetic |

## 3. Independent negative controls

| Control | Mutation | Result |
|---|---|---|
| Q0 | unmodified: exact Hosting references plus reflection policy / canonical gate | green / green |
| Q1 | task 3.2 checked only (the planned activation) | green, as intended |
| Q2 | Hosting calls `typeof(WorkflowDagPlan<int>).GetMethod(…, NonPublic)` | **red**: reflection policy (`System.Reflection.BindingFlags`) |
| Q5 | fixture and source `Complete` tree repointed to the atomic tree `572d86f6` | **red**: checkpoint tree mismatch |
| Q6 | fixture and source evidence path repointed to a file absent from the checkpoint | **red**: unreadable from checkpoint `4eb2e8d` |
| Q7 | source refreeze manifest edited by one byte (freeze refreshed) | **red**: packet hash and checkpoint blob |
| Q8 / Q9 | source task 2.4 / closeout task 3.1 reopened | **red** / **red**: literal task state |
| Q3 (informational) | Hosting declares `[UnsafeAccessor(Method, Name = "get_AuthoredOrdinal")] static extern int Ordinal(DagNodeRef)` | green: not covered (P3-1) |
| Q4 (informational) | Hosting calls `Expression.Property(Expression.Constant(plan), "NodePlans")` and compiles it | green: not covered (P3-1) |
| R0 | restored | green; porcelain equals the manifest; the 23 target files equal main byte-for-byte |

## 4. Non-blocking observations (P3)

- **P3-1: two static routes around the boundary remain.** The new policy is real, but "reflection
  is now explicitly covered" overstates it. Two static, compile-time constructs bypass both the
  member allowlist and the reflection rule:
  - `UnsafeAccessorAttribute` in `System.Runtime.CompilerServices` reaches an internal member with
    no MemberRef to it and no reflection type (Q3).
  - `System.Linq.Expressions.Expression.Property(…, string)` falls back to non-public members by
    name (Q4).

  Neither the product code nor the harness uses either. Hosting has no legitimate need for them,
  so a later guard edit can simply reject any TypeRef to `UnsafeAccessorAttribute` and any member
  reference into `System.Linq.Expressions` from Hosting and the harness.
- **P3-2: the evidence path list is uneven.** `executableEvidencePaths` lists the Hosting adapter
  (product) and two test files, but not `src/OrcaCore.Dag/DagRuntimeView.cs`, the core
  implementation. The checkpoint and tree binding already cover all source bytes, so this is only
  descriptive.
- **Recorded (expected):** `CLAUDE.md`'s "Complete promotion is prepared only in the separately
  reviewed closeout" becomes historical once this lands. Refresh it with the next reviewed
  documentation target, such as archival or the 8.4 contract, not in activation.

## 5. Reviewer hygiene and checkpoint instructions

`HEAD` is `1d8941b7c4e4a96752b33684075d152494057c49`, with the twenty-three frozen entries and
nothing staged. When this verdict was written, the repository showed exactly the frozen entries
plus this new, untracked verdict. The disposable worktrees are removed.

This approval covers only the frozen bytes. To checkpoint:
- stage exactly the 23 manifest paths from the live index;
- confirm the tree is `f675f04ab9e8a5b5883a3577ce870b14b077c3ce` with parent `1d8941b`;
- commit that tree.

This verdict is not part of the checkpoint. The evidence commit that follows must:
- have the checkpoint as its only parent;
- add this verdict byte-exact and catalog it;
- clear the active freeze while keeping the immutable-history manifest pointer.

The activation should then check task 3.2 only; that rehearses green. The runtime-view amendment is
then complete. Archival and the reshape 8.4/8.5 durable bridge (contract first) remain separately
reviewed targets.

## Determination

The runtime-view seam now has a permanent `Complete` record bound to its real, independently
approved source checkpoint, evidence, verdict, and packet bytes. Its activation is replayed exactly,
and its review observations are dispositioned with executable checks. It changes no product,
canonical, or public surface.

**Verdict:** **APPROVE**
