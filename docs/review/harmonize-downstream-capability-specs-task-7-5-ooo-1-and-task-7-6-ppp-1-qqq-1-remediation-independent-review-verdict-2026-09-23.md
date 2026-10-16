# Harmonization Task 7.5/7.6 PPP-1 and QQQ-1 remediation independent review verdict

**Date:** 2026-09-23
**Reviewer:** independent review
**Scope reviewed:** the eighteen-entry freeze on base
`d6eee0d20d0e82135bc33caf2f8b25bdcd665772`, named by
`harmonize-downstream-capability-specs-task-7-5-ooo-1-and-task-7-6-ppp-1-qqq-1-remediation-dirty-manifest-2026-09-23.txt`.
It remediates the 2026-09-23 findings PPP-1 and QQQ-1.
**Authorization requested:** one checkpoint for Task 7.5 OOO-1 hardening, the Task 7.6
qualified-owner audit, and this remediation. This verdict authorizes that checkpoint for the exact
frozen target only.

## Summary

The blocking defect and every non-blocking observation are addressed:
- **PPP-1:** the 122-entry inventory again names `task:7.17`. The guard now requires that exact
  owner and the completed reshape Task 7.17 removal text, so `task:7.6`, `task:7.5`, or a
  reworded removal task is red.
- **QQQ-1 counts:** every Markdown accounting row is compared in order with the machine-readable
  counts, which are themselves measured against the recovery diff, compile removes, orphan roots,
  and retired packages. The old 89 / 5 / 6 values were stale; 134 / 0 / 5 are the measured ones.
- **QQQ-1 members:** member identities now need declaration syntax inside the named type body,
  so a parameter name such as `cancellationToken` is rejected.
- **QQQ-1 wording:** the `.Management` lineage and codec-project history are now described exactly.
- **Provenance:** the rejected 13-path packet is byte-exact and registered in all required places.
- **Validation:** every count, both anchors, the 129/129 fresh-package probes, and the 14/14
  baselines reproduce.

One P3 bundle is recorded (RRR-1). It does not block the checkpoint.

## Method

The review used the dedicated worktree `X:/Projects/GitHub/Workflow-orca-review-task-7-5-7-6`,
detached at `d6eee0d2`. This verdict was written there.

All builds, tests, and mutations ran in two further disposable detached worktrees holding the same
eighteen entries: one for validation, the committed-checkpoint simulation, and the fresh-package
runner; one for mutation probes. The review created no ref and moved no branch. Main, the review
worktree, and the probe copy were re-verified against the frozen anchors afterwards.

## 1. Freeze anchors and provenance

| Anchor | Claimed | Reproduced |
|---|---|---|
| Raw porcelain, `--untracked-files=all` | 18 entries, 1,792 B, `2e9794a7…4a6d` | identical in main and the review worktree |
| Scoped content record | 17 rows, 2,910 B, `e3e2adbb…274e` | identical in both |
| Entries | 10 modified, 8 untracked, 0 staged | identical |
| Simulated checkpoint | tree `e0130dbd…` from a copy of the live index | identical in both; its path set equals the manifest exactly (8 A / 10 M) |

- **Changes since the rejected target:** relative to the rejected tree `d8a9cba5`, exactly thirteen
  paths changed:
  - three new review records and the remediation artifact;
  - the audit artifact, `design.md`, and `tasks.md`;
  - the Markdown and JSON deletion ledgers;
  - two history fixtures and two guard sources.
- **Rejected packet:** the rejected request (6,322 B), manifest (13 lines, 1,195 B), and verdict
  (11,429 B, `c2a5fa0e…`) are byte-exact. Rejected freeze `7.5-ooo-1-and-7.6-rejected` records
  all three, and all are in `appendOnlyRecords`.
- **New Task 7.5 entry:** it holds the verdict in the `Rejected` state. Its thirteen historical
  rows equal the rejected tree's blobs exactly. Its record (2,116 B, `ed2925ca…`) and path-sorted
  digest reproduce, and its five current-match pins are maximal.
- **Registry:** the 18 archived freezes and 13 entries reproduce from committed objects.
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

## 3. PPP-1 and the ledger counts

| Probe | Mutation | Result |
|---|---|---|
| P1a / P1b | inventory owner changed to `task:7.6` / `task:7.5` | each **red** |
| P1c | reshape Task 7.17's removal sentence reworded | **red** |
| Q1a | Markdown retired-symbol count changed to 999 | **red** |
| Q1b | Markdown fourth inventory row removed | **red** |
| Q1c | Markdown deleted-path count restored to the stale 89 | **red** |

## 4. Member identities and the Task 7.6 catalog

An independent resolver applied the new declaration rule to comment- and string-masked source at
`ac46d995` and `666bc1e6`. All 122 identities resolve, and
`EphemeralWorkflowEngine::cancellationToken` does not.

| Probe | Mutation | Result |
|---|---|---|
| M1 | `EphemeralWorkflowEngine::cancellationToken` added as a member identity | **red**, identity named |
| M2 | the declaration filter replaced by a body-token match | **red**, the synthetic parameter control |
| B1 / B2 / B3 | `.Management` restored / codec back on `OrcaCore.Provider.Abstractions` / `WorkflowProjectionStatistics` reintroduced | each **red**, identity named |
| A1 / A2r | a Task 7.5 regression deleted / a classifier narrowed with its digest refreshed | each **red** |
| D1–D6 | the Task 7.6 owner sentence, remediation decision, design text, audit artifact, or remediation artifact changed | each **red** |

## 5. RRR-1 (P3): observations

- **Lexical masking is not exercised by any control.** Removing `MaskNonCode` from the synthetic
  control alone (M3s), or from both it and the archive reader (M3), leaves every guard green.
  - The synthetic comment has no access modifier and the string decoy is followed by a quote, so
    the declaration pattern rejects both without masking.
  - The request's statement that these controls "also fail if … lexical masking is removed" is
    therefore true only for the declaration filter.
  - Masking still matters for a declaration-shaped line inside a multi-line block comment or
    verbatim string, and no retained identity relies on one. A synthetic multi-line comment
    containing `public void Decoy()` would make the masking load-bearing.
- **Two latent limits of the declaration pattern:**
  - A member declared on a nested type also satisfies the enclosing type.
  - An interface member without an explicit access modifier cannot be resolved, which would fail
    safe (red).
  - Neither affects the current eleven member identities.

## 6. Reviewer hygiene and checkpoint instructions

Main and the review worktree remained at `d6eee0d20d0e82135bc33caf2f8b25bdcd665772`, with the
eighteen frozen entries and nothing staged. I created no commit or ref in the reviewed repository.
When this verdict was written, the review worktree showed exactly the frozen entries plus this new,
untracked verdict. Both disposable worktrees are removed.

This approval covers only the frozen bytes. To checkpoint:
- stage exactly the 18 manifest paths from the live index;
- confirm the tree is `e0130dbdaa8828d6931c1c81124df90fe5cfcbc9` with parent `d6eee0d2`;
- commit that tree.

This verdict is not part of the checkpoint. The evidence commit that follows must:
- have the checkpoint as its only parent;
- add this verdict and catalog it in `appendOnlyRecords`;
- list it as an `APPROVE` row beside the existing `REJECT` in the Task 7.5 entry, whose
  `task-7-5*verdict-*.md` glob discovers both;
- move that entry through the awaiting-evidence state;
- archive the active freeze.

The activation must then pin the evidence commit by its full id.

## Determination

The inventory owner is exact again and guarded against both same-numbered and unrelated tasks. The
Markdown ledger now reports measured counts and cannot drift silently. Member identities require
real declarations. The Task 7.5 hardening and the Task 7.6 catalog remain exact. The one
overstated control is recorded above; it leaves no identity unresolved.

**Verdict:** **APPROVE**
