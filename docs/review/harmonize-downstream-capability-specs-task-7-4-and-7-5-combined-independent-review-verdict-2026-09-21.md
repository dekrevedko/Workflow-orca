# Harmonization Tasks 7.4 and 7.5 combined independent review verdict

**Date:** 2026-09-21
**Reviewer:** independent review
**Scope reviewed:** the seventeen-entry freeze on base
`7b1bf74ee83c97a19f2c8b08ddcd30d54d443658`, named by
`harmonize-downstream-capability-specs-task-7-4-and-7-5-combined-dirty-manifest-2026-09-21.txt`,
with request
`harmonize-downstream-capability-specs-tasks-7-4-and-7-5-combined-independent-review-request-2026-09-21.md`.
**Authorization requested:** one checkpoint for the Task 7.4 Orleans boundary, the Task 7.5
recurring active-tree documentation guard, and the KKK-1 carry-forward. This verdict does not
authorize it.

## Summary

Most of the target is correct and mutation-proven:
- **Task 7.4:** the single active Orleans note matches the Section 7B contract. The 25 archived
  plan files are byte-unchanged, and every requested Part A control is red.
- **KKK-1 carry-forward:** all six fixes hold, and every Part C control is red.
- **Task 7.5 recurring scan:** each named stale wording is red with path, line, and
  classification. Immutable evidence is ignored while the history guard still pins it.
- **Validation:** every reported count and both freeze anchors reproduce.

Two blocking defects are in Task 7.5:

- **LLL-1 (P2):** the historical replay does not protect the classifier set. Deleting four of
  the eight stale classifiers leaves all 23 replays green, as does narrowing one alternative.
  Required control B3 therefore fails, and so does the artifact's claim that the replay
  "prevents a narrowed denylist from passing".
- **MMM-1 (P2):** the active tree still has stale Section 7B vocabulary. DU-055 in normative
  document 06 says the application receives "the exact `EventDeliveryResult` status from EV-012".
  That type does not exist; EV-012 defines `WorkflowEventAcceptanceResult`. The guard's
  legacy-surface classifier does not know the name, so its "0 stale Section 7B claims" result is
  wrong. AC-005 in document 12 has a weaker second case. Round 67 approved both documents without
  noticing.

One P3 bundle is recorded (NNN-1).

## Method

The review used the dedicated worktree `X:/Projects/GitHub/Workflow-orca-review-task-7-4-7-5`,
detached at `7b1bf74e`, as the request asks. This verdict was written there.

All builds, tests, and mutations ran in two further disposable detached worktrees holding the same
seventeen entries: one for validation and checkpoint simulation, one for mutation probes. The
review created no ref and moved no branch. Main and the review worktree were re-verified against
the frozen anchors before this record was written.

## 1. Freeze anchors and provenance

| Anchor | Claimed | Reproduced |
|---|---|---|
| Raw porcelain, `--untracked-files=all` | 17 entries, 1,380 B, `6cd54f43…9846` | identical in main and the review worktree |
| Scoped content record | 16 rows, 2,429 B, `8cbc0c66…6c9f` | identical in both |
| Entries | 13 modified, 4 untracked, 0 staged | identical |
| Simulated checkpoint | parent `7b1bf74e`, tree `6bc4acc4…` | identical, 17 paths (4 A / 13 M) |

- **Chain since Round 67:**
  - Checkpoint `9e921753` has tree `9c1ac465`.
  - Evidence commit `0b979942` is its only child. It adds the Round 67 verdict byte-for-byte
    (13,772 B, `f7c7dc84…`) plus the history row and archived freeze.
  - Activation `7b1bf74e` changes only `approvalEvidenceCommit` and `reviewState`.
- **Registry:** all 17 archived freezes reproduce from committed objects, including
  `7.2-hhh-1-and-7.3-contract-hardening`. Every declared current-match pin is exact.
- **New records:** the request (7,257 B, `93018ad9…`) and manifest are in `appendOnlyRecords`, and
  `activeFreezeManifestPath` names the manifest.
- **Scope:** no `src/**`, canonical `openspec/specs/**`, or archived Orleans-plan file changes.
- **Recomputed from disk:** the active note is 3,307 normalized bytes (`e4bf37e2…`). The 25-file
  archive record is 2,497 B (`c8a39515…`) and equals both disk and fixture. All four artifact
  digests and all 22 Task 7.3 rows match.

## 2. Validation

| Lane | Result |
|---|---|
| Debug and Release non-incremental `-warnaserror` builds | 0 warnings, 0 errors |
| Exact package feed | 12 packages |
| Core / Ephemeral / Durable / Acceptance / Hosting / ProviderCertification | 350 / 79 / 99 / 37 / 24 / 96 |
| PostgreSQL / SQL Server / Integration | 101 / 72 / 11 |
| Guards, full project | 226 passed, 14 failed, all 14 `ExecutableBehaviorExpectedRedGuards.Scenario_*` |
| `Disposition=Infrastructure`, Release | 226/226 |
| Strict OpenSpec | 18/18 |
| Harmonization ledger | 29 complete / 5 open / 34 |
| `git diff --check`, worktree | clean |
| Guards on the committed simulated checkpoint | 226 passed plus the same 14 expected-red |
| `git diff --check`, committed simulated checkpoint | **fails**: trailing whitespace in the Task 7.4 artifact (NNN-1) |

## 3. Part A: Task 7.4 Orleans boundary

The note now carries the current contract:
- a new-change prerequisite;
- one cold-capable `Wait` and caller-created `EventId`;
- all four routes with retained pre-wait acceptance and acknowledgement only after
  `Accepted`/`Duplicate`;
- atomic outbox dispatch through the application-registered dispatcher;
- the fixed codec, and engine, hosting, and provider ownership.

| Probe | Mutation | Result |
|---|---|---|
| A1a / A1b | second file under `docs/orleans-engine/` / note deleted | **red** |
| A1c | stale two-route / `NoActiveWait` bullet restored | **red** (Task 7.4 hash and Task 7.5 line 16) |
| A2a / A2c / A2e | archived file edited / added uncatalogued / deleted | **red** via the Task 7.2 history guard |
| A2b / A2d | archived file edited or added, with the fixture refreshed to match | **red** in both Task 7.2 and Task 7.4 |
| A3a–A3f | prerequisite sentence, `Wait`, routes, dispatch, codec, or provider ownership removed, all with the note hash, artifact, and digest refreshed | each **red** |
| A30 | benign double space, same refresh (positive control) | green |
| A4a | Orleans project under `src/`, on the committed checkpoint | **red**, `ProductAssemblies_MatchExactV1Manifest` |
| A4b / A4c | Orleans `PackageVersion` / PostgreSQL `002_…` migration, same state | **red**, package source provenance |

## 4. Part B: Task 7.5 recurring guard

| Probe | Mutation | Result |
|---|---|---|
| B1a / B1b | `WaitLong(` in a guide / `.Pause(` in a canonical spec | **red**, path, line, and classification |
| B2a–B2g | each stale wording named by the request | each **red**, path and line |
| B4a / B4b | the same text only under `docs/archive/` or `docs/review/` | Task 7.5 green; the history guard **red** |
| B5 | `docs/implementation/README.md` reverted | **red** |
| B3, 1 of 8 | "legacy event client", "non-buffering pre-wait delivery", "superseded deferred list", or "unapproved Section 7B" deleted | each **red** |
| B3, 4 of 8 | "deferred definition fanout", "deferred durable publish", "two-route ingress", or "superseded delivery status" deleted | each **green** |
| B3n | the `returns … NoActiveWait` alternative removed | **green** |

## 5. LLL-1 (P2): the historical replay does not pin the classifiers

The replay requires only that each of the 23 historical files yields at least one finding. At
`89e3ed55`, those files trip these classifiers:
- "legacy event client": 12 files, the sole match for 6;
- "non-buffering pre-wait delivery": 13 files, sole for 3;
- "superseded deferred list": 3 files, sole for 2;
- "unapproved Section 7B": 3 files, sole for 1;
- "deferred definition fanout": 4 files; "deferred durable publish": 2; "two-route ingress": 2;
  "superseded delivery status": 1. None of these four is ever the only match.

So any of the last four classifiers, or a narrower alternative, can be deleted with no replay
failing. A Python replay of the same expressions predicted exactly the four real rebuild results.
Recording the exact historical `(path, classifier)` findings, or at least requiring every
classifier and alternative to fire in the replay, would make control B3 hold.

## 6. MMM-1 (P2): stale legacy event vocabulary remains

- **Document 06, DU-055:** "The application receives the exact `EventDeliveryResult` status from
  EV-012."
  - `EventDeliveryResult` appears nowhere in `src/` or canonical OpenSpec.
  - EV-012 and `WorkflowInboundEvent.cs` define `WorkflowEventAcceptanceResult`.
  - The sentence dates from `8c2dd71`. It is absent from the Task 3.1 sweep, the Task 7.3
    denylist, and all eight Task 7.5 classifiers.
- **Document 12, AC-005:** terminal "event delivery returns `InstanceTerminal`".
  - Only an internal ephemeral enum and the test-support `ProcessLocalEventRouteStatus` still
    carry that name.
  - Durable ingress returns `Rejected(DirectInstanceTerminal)`, so the criterion reads as the
    pre-7B status union.
- **Why the guard misses them:** the legacy-surface classifier knows only `IWorkflowEventClient`
  and `EventDeliveryStatus`. Eight natural rewordings also pass green, including
  `DeliverToInstanceAsync`, "yields `NoActiveWait`", "only instance and correlation routing", and
  "durable `Publish` is deferred" (NNN-1).

Both documents are Task 7.3 sources, so the fix is the reviewed edit plus a refresh of rows 16 and
19 and the artifact digest, which the pin-refresh rule allows. The legacy names should also be
added to the classifier and to the historical replay.

## 7. Part C: carry-forward

| Probe | Mutation | Result |
|---|---|---|
| C1a / C1b | `[Fact, Trait("AC", …)]` / `[Xunit.Trait("AC", …)]` on the corpus guard, removed from real tests | **red** |
| C2a / C2b | PR-040 no-split sentence / builder-ingress-dispatcher list deleted, document 10 row and digest refreshed | **red** |
| C20 | benign document 10 edit, same refresh (positive control) | green |
| C3a / C3b | owner-exception constant set to 6.6 / also admits 7.4 | **red** |
| C4a / C4r / C4b | EV-032 `Active` removed without and with refresh / approval-history sentence truncated | **red** |

Lineage failures now name their task, and the corrected EV-032 row refresh is exact.

## 8. NNN-1 (P3): observations

- **Rewordings escape.** V1–V8 all stay green, so the design's "newly worded stale assertions"
  overstates what a phrase-based classifier covers.
- **The recurring scope is not really evolving.** The dated Task 7.5 artifact must contain the
  live corpus count, which is 86 today.
  - A benign new guide (G1) or a new OpenSpec change proposal (G2) turns Task 7.5 red.
  - Archival will do the same.
  - Recording the count without a live equality check, or documenting who may refresh it, would
    avoid this.
- **Two Task 7.4 clauses are only hash-pinned.** The numbered new-change step (A3a2) and the
  Orleans-adapter tier limit (A3g) stay green behind a coherent refresh.
- **Task 7.4 reads the archive from the fixture, not disk.** Disk changes are caught by the
  Task 7.2 guard, so "independently derives both inventories" is slightly overstated.
- **Orleans implementation tasks are only transiently guarded.** Such a task added to the ledger
  (A4d) is caught only by the active-freeze drift check, which disappears once the freeze is
  archived.
- **Committed `git diff --check` fails.** The Task 7.4 artifact has Markdown hard-break trailing
  spaces on lines 3–4. The request's "clean" held only because untracked files are outside the
  worktree diff.
- **Verdict naming:** the request uses a `tasks-7-4-…` stem, which no `task-{task}*verdict-*.md`
  discovery glob can match. An archived freeze's approval verdict must be registered in some
  entry. This verdict therefore uses the manifest's `task-7-4-…` stem, so a future Task 7.4 entry
  can discover it.
- **Main worktree hygiene:** 123 tracked files there have CRLF or mixed working copies over LF
  blobs, hidden by Git's stat cache. A real `git add -A` still produces tree `6bc4acc4`, but a
  fresh-index or renormalizing add would sweep them in. Create the next checkpoint from the review
  worktree, or verify the tree before committing.
- **Unreviewed earlier freeze:** a standalone Task 7.4 request and manifest
  (`…-task-7-4-orleans-boundary-…`) exist only in `Workflow-orca-review-task-7-4`. They are
  outside main's `docs/review/` and need no disposition unless they are copied in.
- **Cosmetic:** the last two lines of the `docs/implementation/README.md` paragraph lose its
  leading-space indentation.

## 9. Reviewer hygiene and next steps

Main and the review worktree remained at `7b1bf74ee83c97a19f2c8b08ddcd30d54d443658`, with the
seventeen frozen entries and nothing staged. I created no commit or ref in the reviewed repository.
When this verdict was written, the review worktree showed exactly the frozen entries plus this new,
untracked verdict. Both disposable worktrees are removed.

A superseding freeze must:
- record a rejected freeze for this request, manifest (17 lines), and verdict;
- catalog this verdict in `appendOnlyRecords` with its LF-normalized bytes;
- register it as a `REJECT` row in any entry whose discovery glob matches it;
- close LLL-1 and MMM-1.

The Task 7.4 note and archive, the KKK-1 carry-forward, and the `docs/implementation/README.md`
correction can be refrozen unchanged.

## Determination

Task 7.4 and the carry-forward are exact. Task 7.5's recurring scan works for the wordings it
names. Its two central claims do not hold: that the historical fixture protects the classifier
set, and that the current active tree has no stale Section 7B claims.

**Verdict:** **REJECT**
