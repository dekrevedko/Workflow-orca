# Harmonization Task 7.5 OOO-1 hardening and Task 7.6 independent review verdict

**Date:** 2026-09-23
**Reviewer:** independent review
**Scope reviewed:** the thirteen-entry freeze on base
`d6eee0d20d0e82135bc33caf2f8b25bdcd665772`, named by
`harmonize-downstream-capability-specs-task-7-5-ooo-1-and-task-7-6-dirty-manifest-2026-09-22.txt`.
**Authorization requested:** one checkpoint for Task 7.5 post-review hardening and the Task 7.6
qualified-owner audit. This verdict does not authorize it.

## Summary

Nearly all of the target is correct and mutation-proven:
- **Task 7.5 OOO-1:** all eight reviewed rewordings are executable probes. Two guard-source digests
  pin the classifier and regression catalogs, and the pinned hardening artifact embeds both
  digests. Deleting or narrowing a classifier or probe is red even with coherent refreshes.
- **Task 7.6 audit:** independent resolution confirms the base catalog had exactly fifteen owner
  defects. All 122 retained identities resolve to a real historical namespace and type, even with
  comments and strings masked out. The three deleted names were never declared anywhere in the
  repository's history.
- **Validation:** every reported count, both freeze anchors, the 129/129 fresh-package probes,
  and the 14/14 baselines reproduce.

One blocking defect remains:

- **PPP-1 (P2):** the production deletion ledger now gives the 122-entry inventory the owner
  `task:7.6`. That ledger resolves owners against the reshape change, where Task 7.6 is the
  superseded two-route `IWorkflowEventClient` implementation, not this harmonization task. The
  guard only checks that some task with that number exists, so the false binding is green.

One P3 bundle is recorded (QQQ-1).

## Method

The review used the dedicated worktree `X:/Projects/GitHub/Workflow-orca-review-task-7-5-7-6`,
detached at `d6eee0d2`, as the request asks. This verdict was written there.

All builds, tests, and mutations ran in two further disposable detached worktrees holding the same
thirteen entries: one for validation, the committed-checkpoint simulation, and the fresh-package
runner; one for mutation probes. The review created no ref and moved no branch. Main, the review
worktree, and the probe copy were re-verified against the frozen anchors afterwards.

## 1. Freeze anchors and provenance

| Anchor | Claimed | Reproduced |
|---|---|---|
| Raw porcelain, `--untracked-files=all` | 13 entries, 1,195 B, `ed1c3952…d47a` | identical in main and the review worktree |
| Scoped content record | 12 rows, 1,961 B, `f947d3e0…133e` | identical in both |
| Entries | 9 modified, 4 untracked, 0 staged | identical |
| Simulated checkpoint | tree `d8a9cba5…` | identical from a copy of each live index, 13 paths (4 A / 9 M) |

- **Prior chain:** checkpoint `7a262404` has tree `770018dc`, exactly the approved target.
  Evidence commit `bd7c01bf` is its only child and adds the 2026-09-22 verdict byte-for-byte
  (11,098 B, `d8e45af7…`). Activation `d6eee0d2` changes only the two transition values. The new
  Task 7.4 entry registers both 7.4/7.5 verdicts.
- **Registry:** all 18 archived freezes and 12 entries reproduce from committed objects, and every
  declared current-match pin is exact.
- **New records:** the request (6,322 B, `d2e49cad…`) and manifest are in `appendOnlyRecords`, and
  `activeFreezeManifestPath` names the manifest.
- **Scope:** no `src/**`, canonical `openspec/specs/**`, package-manifest, or migration change.

## 2. Validation

| Lane | Result |
|---|---|
| Debug and Release non-incremental `-warnaserror` builds | 0 warnings, 0 errors |
| Exact package feed | 12 packages |
| Core / Ephemeral / Durable / Acceptance / Hosting / ProviderCertification | 350 / 79 / 99 / 37 / 24 / 96 |
| PostgreSQL / SQL Server / Integration | 101 / 72 / 11 |
| Guards, full project | 226 passed, 14 failed, all 14 `ExecutableBehaviorExpectedRedGuards.Scenario_*` |
| `Disposition=Infrastructure`, Release | 226/226 |
| `run-public-api-baseline.ps1 -FreshPack`, on the committed simulated checkpoint | 129/129 isolated compiler diagnostics; baselines 14/14 |
| Strict OpenSpec | 18/18 |
| Harmonization ledger | 30 complete / 4 open / 34, Task 7.7 still open |
| `git diff --check`, worktree and committed simulated checkpoint | clean |
| Guards on the committed simulated checkpoint | 226 passed plus the same 14 expected-red |

## 3. Part A: Task 7.5 OOO-1 hardening

The eight classifier expressions were extracted and replayed independently. Both catalog digests
reproduce: classifiers `fa5cb2d5…` and regressions `df9180ed…`. The broadened expressions still
produce exactly the 56 recorded historical results, and the 86 current sources produce none.

| Probe | Mutation | Result |
|---|---|---|
| A1 | one regression entry deleted | **red**, regression-catalog digest |
| A1r | the same with that digest refreshed | **red**; the pinned hardening artifact embeds the old digest |
| A2 | the fanout classifier narrowed to drop "supported" | **red**, classifier-catalog digest |
| A2r | the same with that digest refreshed | **red**, the direct probe "Definition fanout is not supported." |
| A3 | the two-route classifier deleted, its historical rows and the Task 7.5 artifact digest refreshed | **red**, classifier-catalog digest |
| A3r | the same with the classifier digest also refreshed | **red**, the direct two-route probe |
| A4a–A4c | the ledger decision, design decision, or hardening artifact weakened | each **red** |
| A4d | the hardening-artifact digest constant changed | **red** |

Retiring a classifier or probe now takes a coordinated guard, artifact, and digest edit, which is
exactly the reviewed decision the design describes.

## 4. Part B: Task 7.6 qualified-owner audit

An independent Python resolver read `src/` at `ac46d995` and `666bc1e6` and applied the same
owner rules, once on raw text and once with comments and strings masked:
- **At base:** exactly fifteen of the 125 identities fail. They are the ten `.Management`
  identities, the two codec interfaces under `OrcaCore.Provider.Abstractions`, and the three
  projection-statistics names.
- **In the target:** all 122 resolve in both modes, so none relies on a comment or string.
- **Catalog digest:** the ordered LF-joined catalog with a final LF hashes to `302b74d5…`.
- **Members:** all eleven member identities are real declarations, five members and six enum
  values.
- **History:** no commit on any ref ever declared `WorkflowProjectionStatistics`,
  `WorkflowProjectionStatisticsGroup`, or `WorkflowProjectionPressureMetrics`, so deletion is
  correct.
- **Codec coverage:** moving the codec interfaces to `OrcaCore` loses nothing, because
  `StateAndCodecGreenGuards` still bans both names across every product assembly.

| Probe | Mutation | Result |
|---|---|---|
| B1 | `.Management` restored on `EphemeralManagement` | **red**, identity named |
| B2 | `IWorkflowPayloadCodec` back on `OrcaCore.Provider.Abstractions` | **red**, identity named |
| B3 / B3m | `WorkflowProjectionStatistics` reintroduced / a nonexistent member added | each **red**, identity named |
| B4 | member lookup widened to the whole namespace region | **red**, the synthetic sibling-type control |
| B4b | the namespace check dropped | **red**, the synthetic wrong-namespace control |
| B5a / B5b | a fresh-package marker removed / mismatched | each **red** |
| B5c / B5d | the ledger count or ordered digest changed | each **red** |
| B6a–B6c | the Task 7.6 ledger decision, audit artifact, or design decision weakened | each **red** |

## 5. PPP-1 (P2): the ledger owner names the wrong task

`production-deletion-ledger.json` changes the `retired-public-symbols` inventory owner from
`task:7.17` to `task:7.6`.
- **How the owner resolves:** `ProductionDeletionLedgerGuards.AssertOwnerExists` looks the owner up
  in `openspec/changes/reshape-developer-facing-interfaces/tasks.md`, the change that owns this
  ledger.
- **What it resolves to:** there, Task 7.6 reads "Implement exactly two instance/correlation
  `IWorkflowEventClient` route names". That is the superseded pre-7B event-client task this whole
  harmonization change retires from authority. Harmonization Task 7.6 cannot be named by this
  unqualified ID.
- **The guard only checks existence.** Setting the owner to `task:7.5`, another unrelated reshape
  task, also stays green (probe P1).
- **Why it matters:** Task 7.6 exists to make owners exact, and this change records a false,
  machine-readable owner in the reshape ledger that no guard can detect.

The simplest fix is to restore `task:7.17`, the reshape task that removed these symbols, and keep
the Markdown row's "pinned by harmonization task 7.6" wording. If the ledger should name the
harmonization task, it needs a change-qualified owner form that the guard resolves against that
change's ledger. Either way, a guard should tie this inventory to its owning task text, for
example by requiring the owning task to mention the retired public symbols.

## 6. QQQ-1 (P3): observations

- **The Markdown companion counts are unguarded.** Changing the retired-symbol count to 999 stays
  green (probe P2). That is how the base carried 120 there against the JSON's 125.
  - The prose now says "four ordered symbol inventories".
  - The table still lists three; the three removed hosting and codec types have no row.
- **Member ownership is token-level.** A member identity passes if the token appears anywhere in
  the type body. Adding `EphemeralWorkflowEngine::cancellationToken`, a parameter name, stays green
  (probe B3t).
  - All eleven current member identities are real declarations, so this is latent.
  - Declarations and members are also matched on unmasked text, so a comment could satisfy one.
    No retained identity relies on that today.
- **Two wording imprecisions:**
  - The `.Management` namespace is described as "nonexistent". It did exist in the superseded
    `v3/` and `v3-cursor/` lineages (`b8b2e3d`, `11d0e8d`; removed by `1c2f268`), but never in the
    promoted product lineage.
  - The codec interfaces' "historical `OrcaCore` assembly" is inexact. At `666bc1e6` they lived in
    `OrcaCore.Abstractions.csproj` with the default assembly name, and the project became the
    `OrcaCore` assembly later. `OrcaCore` is the correct current successor to probe.

## 7. Reviewer hygiene and next steps

Main and the review worktree remained at `d6eee0d20d0e82135bc33caf2f8b25bdcd665772`, with the
thirteen frozen entries and nothing staged. I created no commit or ref in the reviewed repository.
When this verdict was written, the review worktree showed exactly the frozen entries plus this new,
untracked verdict. Both disposable worktrees are removed.

A superseding freeze must:
- record a rejected freeze for this request, manifest (13 lines), and verdict;
- catalog this verdict in `appendOnlyRecords` with its LF-normalized bytes;
- register it as a `REJECT` row in any entry whose `task-{task}*verdict-*.md` glob discovers it,
  including a future Task 7.5 entry;
- fix PPP-1.

Everything else in this target can be refrozen unchanged.

## Determination

The Task 7.5 hardening and the Task 7.6 catalog audit are exact and well guarded. The one
remaining defect is small but squarely in scope: the change that makes forbidden-symbol owners
exact records a wrong, unguarded owner for that same inventory.

**Verdict:** **REJECT**
