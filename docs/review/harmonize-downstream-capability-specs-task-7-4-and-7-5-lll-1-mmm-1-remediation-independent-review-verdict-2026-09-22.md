# Harmonization Tasks 7.4 and 7.5 LLL-1/MMM-1 remediation independent review verdict

**Date:** 2026-09-22
**Reviewer:** independent review
**Scope reviewed:** the twenty-three-entry freeze on base
`7b1bf74ee83c97a19f2c8b08ddcd30d54d443658`, named by
`harmonize-downstream-capability-specs-task-7-4-and-7-5-lll-1-mmm-1-remediation-dirty-manifest-2026-09-22.txt`.
It remediates the 2026-09-21 findings LLL-1, MMM-1, and NNN-1.
**Authorization requested:** one checkpoint for the combined Task 7.4 and Task 7.5 target. This
verdict authorizes that checkpoint for the exact frozen target only.

## Summary

Both blocking defects are closed, and every requested control behaves:
- **LLL-1:** the historical replay now pins all 56 exact `path:line:classifier` results and
  requires every classifier to be represented. Deleting any classifier, narrowing any alternative,
  or editing any recorded row is red.
- **MMM-1:** DU-055 names `WorkflowEventAcceptanceResult` and AC-005 names
  `Rejected(DirectInstanceTerminal)`. Both regressions stay red even with a coherent row and
  digest refresh, and the superseded names are now classified.
- **NNN-1:** the artifact whitespace, snapshot-only corpus count, semantic Orleans pins,
  disk-derived archive check, Orleans task-ledger rejection, README indentation, and verdict
  naming are all fixed and proven.
- **Provenance:** the rejected 2026-09-21 packet is byte-exact and registered as a rejected freeze
  with all five records catalogued.
- **Validation:** every reported count and both freeze anchors reproduce, and the committed
  `git diff --check` is now clean.

One P3 bundle is recorded (OOO-1). It does not block the checkpoint.

## Method

The review used the dedicated worktree `X:/Projects/GitHub/Workflow-orca-review-task-7-4-7-5`,
detached at `7b1bf74e`, as the request asks. This verdict was written there.

All builds, tests, and mutations ran in two further disposable detached worktrees holding the same
twenty-three entries: one for validation and checkpoint simulation, one for mutation probes. The
review created no ref and moved no branch. Main, the review worktree, and the probe copy were
re-verified against the frozen anchors afterwards.

## 1. Freeze anchors and provenance

| Anchor | Claimed | Reproduced |
|---|---|---|
| Raw porcelain, `--untracked-files=all` | 23 entries, 1,968 B, `f1529d22…0666` | identical in main and the review worktree |
| Scoped content record | 22 rows, 3,440 B, `6bbd780e…3dcd` | identical in both |
| Entries | 15 modified, 8 untracked, 0 staged | identical |
| Simulated checkpoint | tree `770018dc…` | identical from a copy of each live index, 23 paths (8 A / 15 M) |

- Staging only the manifest paths from a copy of the live index produces the same tree in main and
  in the review worktree, so the line-ending-only paths reported on 2026-09-21 stay out of the
  checkpoint.
- The 17 archived freezes and 11 entries reproduce from committed objects, and every declared
  current-match pin is exact.
- The rejected freeze `7.4-and-7.5-combined-rejected` records the 2026-09-21 request (7,257 B),
  manifest (17 lines, 1,380 B), and verdict (13,548 B, `7af6d9be…`), each byte-exact. All five
  review records are in `appendOnlyRecords`, and `activeFreezeManifestPath` names the new manifest.
- Scope contains no `src/**`, canonical `openspec/specs/**`, or archived Orleans-plan path.
- **Recomputed from disk:** the active note is 3,307 normalized bytes (`e4bf37e2…`); the 25-file
  archive record is 2,497 B (`c8a39515…`); all 22 Task 7.3 rows, including the refreshed rows 16
  and 19, match their sources; and the four artifact digests equal their guard constants.

## 2. Validation

| Lane | Result |
|---|---|
| Debug and Release non-incremental `-warnaserror` builds | 0 warnings, 0 errors |
| Exact package feed | 12 packages |
| Core / Ephemeral / Durable / Acceptance / Hosting / ProviderCertification | 350 / 79 / 99 / 37 / 24 / 96 |
| PostgreSQL / SQL Server / Integration | 101 / 72 / 11 |
| Guards, full project | 226 passed, 14 failed, all 14 `ExecutableBehaviorExpectedRedGuards.Scenario_*` |
| `Disposition=Infrastructure`, Release | 226/226 |
| Focused Task 7.1/7.3/7.4/7.5, provenance, and accounting guards | 6/6 |
| Strict OpenSpec | 18/18 |
| Harmonization ledger | 29 complete / 5 open / 34, Task 7.6 still open |
| `git diff --check`, worktree and committed simulated checkpoint | clean |
| Guards on the committed simulated checkpoint | 226 passed plus the same 14 expected-red |

## 3. LLL-1: exact historical replay

An independent Python replay of the eight expressions over the 23 Task 3.1 sources at
`89e3ed55` produced 56 findings identical to the artifact's `HISTORICAL` rows, with every
classifier represented: legacy event client 18, non-buffering pre-wait 19, definition fanout 4,
superseded deferred list 3, deferred publish 2, two-route 2, delivery status 4, unapproved 7B 4.

| Probe | Mutation | Result |
|---|---|---|
| L1a–L1f | each of six classifiers deleted, including the four that were never a sole match | each **red** |
| L2 | the whole `returns/yields … NoActiveWait` alternative removed | **red** |
| L4a / L4b | `DeliverToInstanceAsync` / `EventDeliveryResult` dropped from a classifier | **red** |
| L3a–L3e | a recorded row's line, classifier, or path changed, deleted, or duplicated | each **red** |
| L5 | a classifier suppressed while its recorded rows and the digest were refreshed | **red**, the classifier-coverage check |

The four classifiers that 2026-09-21 could delete silently are now individually load-bearing.

## 4. MMM-1: current event vocabulary

- DU-055 reads "The application receives the exact `WorkflowEventAcceptanceResult` from EV-012",
  which matches EV-012 and `WorkflowInboundEvent.cs`.
- AC-005 reads "direct durable event ingress returns `Rejected(DirectInstanceTerminal)`".
- `EventDeliveryResult`, `DeliverToInstanceAsync`, and the old terminal phrasing are in the Task
  7.3 denylist and the Task 7.5 classifier.
- Only Task 7.3 rows 16 and 19 plus the artifact digest were refreshed, as the pin-refresh rule
  requires.

| Probe | Mutation | Result |
|---|---|---|
| M1 / M2 | DU-055 or AC-005 regressed | each **red** in Task 7.3 and Task 7.5 |
| M3 / M4 | the same regressions with the row and digest coherently refreshed | each **red** |
| M30 | benign document 06 edit with the same refresh (positive control) | green |

## 5. NNN-1: observations remediated

| Probe | Mutation | Result |
|---|---|---|
| N1a, N1c, N1e–N1h | six of the eight reviewed rewordings, individually | each **red** with path and line |
| N1all | all eight inserted together | **red** |
| N2a / N2b | a benign new active guide / new OpenSpec change proposal | green, no fixture churn |
| N3a | the numbered new-change prerequisite removed, note hash and digest refreshed | **red** |
| N3b / N3c | the Orleans adapter exposing internal seams / the paragraph deleted, same refresh | each **red** |
| N30 | benign note edit with the same refresh (positive control) | green |
| N4a / N4b | an Orleans implementation task added to the harmonization or reshape ledger | each **red**, independent of any active freeze |
| N5 | an archived Orleans file edited while the mutable Task 7.4 artifact row was refreshed | **red**, disk against the immutable fixture |

The Task 7.4 artifact and the two new artifacts have no trailing whitespace, and the committed
`diff --check` confirms it. The Task 7.5 artifact records its 86 sources as a checkpoint snapshot,
and the guard no longer compares that number with the live corpus.

## 6. Carry-forward

| Probe | Mutation | Result |
|---|---|---|
| C1a / C1b | `[Fact, Trait("AC", …)]` or `[Xunit.Trait("AC", …)]` on the corpus guard | each **red** |
| C3a / C3c | owner-exception constant moved to 6.6 / an approved entry relabelled `OwnerAuthorized` | each **red** |
| C4a / C4b | EV-032 `Active` removed / the approval-history sentence truncated | each **red** |

## 7. OOO-1 (P3): observations

- **Phrase coverage is still partial.** Two of the eight rewordings from the last round, and six
  further natural ones, pass green:
  - "Fanout to every instance of a definition is not supported in v1" and "Definition fanout is not
    supported";
  - "Section 7B is not yet approved" and "Section 7B has not been approved yet";
  - "Events that arrive before a wait exists are dropped and must be redelivered" and "The durable
    engine does not buffer events before a wait exists";
  - "Publishing events from a workflow is not available in v1";
  - "The engine supports instance and correlation delivery only".
  - A phrase list cannot be exhaustive, so this is a coverage note, not a defect. Adding
    "not supported", "not available", and "not yet/never approved" forms would catch these.
- **A classifier can still be retired in one reviewed edit.** Removing a classifier's tuple
  together with its recorded rows and the artifact digest is green, after which a live
  "exactly two routes" claim also passes. This is the same guard-source-plus-artifact limit that
  every pinned record here has, and the review of that edit remains the control.
- **Approval registration needs an entry.** An archived freeze binds an authority verdict that must
  appear in some registry entry, and each entry discovers verdicts by
  `harmonize-downstream-capability-specs-task-{task}*verdict-*.md`. No 7.4 or 7.5 entry exists yet,
  and the active-freeze key `7.4+7.5` cannot serve as one. A Task 7.4 entry whose glob finds both
  the 2026-09-21 `REJECT` and this verdict is the straightforward path; this verdict is named for
  that stem.

## 8. Reviewer hygiene and checkpoint instructions

Main and the review worktree remained at `7b1bf74ee83c97a19f2c8b08ddcd30d54d443658`, with the
twenty-three frozen entries and nothing staged. I created no commit or ref in the reviewed
repository. When this verdict was written, the review worktree showed exactly the frozen entries
plus this new, untracked verdict. Both disposable worktrees are removed.

This approval covers only the frozen bytes. To checkpoint:
- stage exactly the 23 manifest paths, from the live index rather than a rebuilt one;
- confirm the resulting tree is `770018dc5571815eea5282a5c481342e7cc743b5` with parent
  `7b1bf74e`, and that no line-ending-only path entered it;
- commit that tree.

This verdict is not part of the checkpoint. The evidence commit that follows must have the
checkpoint as its only parent, add this verdict, catalog it in `appendOnlyRecords`, register it as
an `APPROVE` row in the entry whose glob discovers it, and archive the active freeze. The
activation must then pin that evidence commit by its full id.

## Determination

The historical replay now proves what it claims: every classifier is exercised, and the recorded
56 results are exact. The two stale event-vocabulary statements are corrected at their source and
guarded from both directions. Every NNN-1 observation is addressed, and all the controls the
request asks for behave as described.

**Verdict:** **APPROVE**
