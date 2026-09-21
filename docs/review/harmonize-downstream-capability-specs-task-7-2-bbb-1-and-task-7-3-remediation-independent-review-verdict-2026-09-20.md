# Harmonization Tasks 7.2 BBB-1 and 7.3 remediation independent review verdict

**Date:** 2026-09-20
**Reviewer:** independent review
**Scope reviewed:** the forty-nine-entry remediation freeze on base
`89e3ed55357e849852c1a0f6fefa2124433d7e30`, named by
`harmonize-downstream-capability-specs-task-7-2-bbb-1-and-task-7-3-remediation-dirty-manifest-2026-09-20.txt`.
It remediates the Round 65 findings BBB-1 through GGG-1.
**Authorization requested:** one combined checkpoint for Task 7.2 provenance hardening and the
remediated Task 7.3 reconciliation. This verdict does not authorize it.

## Summary

Most of Round 65 is closed and mutation-proven:
- **CCC-1:** inbound identity and dispatcher ownership now match canonical OpenSpec and the source.
- **DDD-1:** the timeout, collation, dynamic-wait, statistics, and dispatch-hook obligations are
  restored.
- **EEE-1:** AC traits now sit on real tests, and the stale waivers are gone.
- **FFF-1:** every governed source has a guard-owned whole-file hash.
- **GGG-1:** the hygiene items are fixed.
- **Validation:** every count reproduces, and both freeze anchors are exact.

Two blocking defects remain:

- **HHH-1 (P2):** BBB-1 is only partly closed. The new approval-evidence check accepts **any**
  later commit that contains the verdict, including another task's evidence commit. Round 65 required
  two rules that are missing: the evidence commit's parent must be the reviewed target, and it must
  add the verdict.
- **III-1 (P2):** remediated AC-116 lists a closed rejection set without `DirectInstanceTerminal`.
  EV-012, the source, and the reflection test now carrying AC-116 all have five rejections. The hash
  pin freezes that error.

One P3 bundle is recorded (JJJ-1).

## Method

All probing ran in two fresh disposable `git worktree` copies created from `89e3ed55`. One was used
for validation and the simulated checkpoint, the other for mutation probes. Each received the
forty-nine frozen entries byte-for-byte, and its porcelain was confirmed byte-identical to the
frozen manifest.

The reviewed worktree was never modified, `HEAD` never moved, and nothing was staged or committed in
it. Every probe commit was detached, no branch or other ref was created, and both worktrees are
removed. Guard-source variants were rebuilt in the probe worktree and restored byte-exact.

## 1. Freeze anchors, predecessor, and validation

- **Raw commit-real porcelain:** **3,218 bytes**, SHA-256
  `d26166c6e187c8c12f2799a9ddf11760a5262e2045c44b3d65362a5f1ba94ffb`, byte-identical to the frozen
  manifest. 43 modified, 6 untracked, none staged.
- **Scoped content record:** **48 rows, 6,530 bytes**, SHA-256
  `103e172f2296328a36f9387862a31947cd3e9120681e9d2e817478969619c6f3`. The review-provenance fixture is
  its only exclusion.
- **Simulated checkpoint:** tree `5ea985a21b10455d737f90f6231c8f00e4d1195a`, with 49 paths (6 added,
  43 modified), identical to the handoff.
- **Predecessor:** the Round 65 request (5,339 B `530e18c7…`), manifest (2,101 B `f83199de…`), and
  verdict (19,178 B `f9a68dae…`) are byte-exact. They are registered consistently in three places:
  as a Task 7.2 `REJECT` verdict, as append-only records, and as rejected freeze
  `7.2-aaa-1-and-7.3-rejected` (36 lines).
- **Projection:** all sixteen archived freezes and eleven entries reproduce, and the maximal
  current-match pins are order-exact (emulated `-Check`).

| Gate | Result |
|---|---|
| Debug and Release builds, non-incremental, warnings as errors | 0 warnings, 0 errors each |
| Exact package feed | 12 packages |
| Core / Ephemeral / Durable / Acceptance / Hosting / Certification | 350 / 79 / 99 / 37 / 24 / 96 |
| PostgreSQL / SQL Server / Integration | 101 / 72 / 11 |
| Full guard lane, dirty target and committed simulated checkpoint | 224 passed, 14 failed, all `ExecutableBehaviorExpectedRedGuards.Scenario_…` |
| Infrastructure lane, Release, CI filter `Disposition=Infrastructure` | 224/224 |
| OpenSpec `validate --all --strict` | 18 / 18 |
| Harmonization ledger | 27 complete, 7 open, 34 total; Task 7.4 open |
| `git diff --check`, dirty diff and committed simulated checkpoint | clean / clean |
| Changes under `src/**`, `openspec/specs/**`, or `docs/orleans-engine/**` | zero |

## 2. Part A controls

Task 7.2 `approvalEvidenceCommit` is now `087817776fe5747ed2d1b153f2dd52d26386b164`.
`ValidateCommittedApprovalEvidence` runs for `Approved` and `OwnerAuthorized` entries, before
active-freeze validation. It requires the value to be non-null and to resolve to exactly that full
lowercase object id. The commit must be an ancestor of `HEAD` and must hold the state-evidence path
with the registered verdict text.

| Probe | Mutation | Result |
|---|---|---|
| P1a–d | `null`; forty zeros; an unresolved SHA; the BBB-1 SHA `087817731b94…` | each **red**, naming the resolution failure |
| P2a | checkpoint `421c39b1`, which lacks the verdict | **red**, names the verdict path |
| P2d, P2e | seven-character prefix; upper-case full SHA | each **red** (exact object id) |
| P2b | Round 64 verdict edited and its registered SHA updated | **red**, content mismatch |
| P4a–e | Round 65 verdict deleted or flipped; its evidence row, append-only row, or rejected-freeze record removed | each **red** |

## 3. HHH-1 (P2): a later commit is accepted as approval evidence

The check proves only that the named commit **contains** the verdict. Any descendant of the true
evidence commit contains it too:

| Probe | Mutation | Result |
|---|---|---|
| P2c | Task 7.2 set to activation `89e3ed55` | **green** |
| P2f | Task 7.1 set to `89e3ed55` | **green** |
| P2g | Task 6.6 set to Task 7.2's evidence commit `087817776fe5` | **green** |

So a wrong provenance SHA still passes, the same defect class as BBB-1. P2g shows it across tasks.
The guard's own message says approved states "must name the distinct commit that preserved their
approval evidence". Round 65 listed two further required rules that were not implemented:
- the evidence commit's parent must be `reviewedTargetCommit`;
- it must add `stateEvidencePath` with the registered SHA-256.

Both rules already hold for the current fixture:
- **Parent:** for all ten independent-review `Approved` entries (Tasks 5.1, 5.2, 6.1–6.6, 7.1,
  7.2), `approvalEvidenceCommit^` equals `reviewedTargetCommit`.
- **Addition:** all eleven evidence commits, including owner-authorized Task 5.3, add their
  state-evidence verdict.

**Required:**
- Assert that `approvalEvidenceCommit^` equals `reviewedTargetCommit` for independent-review
  `Approved` entries.
- For every approved or owner-authorized entry, assert that the evidence commit adds the
  state-evidence path (for example `git diff-tree --diff-filter=A`) with the registered bytes.
- Add a regression that points one entry at a later commit containing the verdict.

## 4. III-1 (P2): AC-116 omits `DirectInstanceTerminal` from a closed set

Remediated AC-116 (`docs/specs/12-acceptance-criteria.md:214-218`) reads: durable ingress "returns
only `Accepted`, `Duplicate`, or `Rejected` with `EventConflict`, `DirectInstanceNotFound`,
`StartConflict`, or `FanoutLimitExceeded`." Every other source has five rejections:
- the Round 65 text of AC-116 listed all five;
- EV-012, which AC-116 cites (`05-requirements-events-waits-timers.md:54-58`);
- `WorkflowEventAcceptanceRejection` in the source and document 17;
- `Reflection_EventRoutesAndAcceptanceResultsAreClosedToTheExactCases`
  (`FacadeHostingContractGuards.cs:395-404`), the test that now carries AC-116.

A normative "only" list that excludes a shipped variant contradicts both its requirement and its
own acceptance evidence. The Task 7.3 row hash pins it. Restore `DirectInstanceTerminal` and refresh
the document-12 row and the artifact digest.

## 5. Part B controls

The Task 7.3 guard now checks the following:
- the ordinal 22-path list and its derivation from the Task 3.1 sweep;
- the absence of the Orleans guide;
- the closed stale-claim set;
- fragments whose whitespace is collapsed (for example "caller-created `EventId`, required
  `CorrelationId`", the three dispatcher-registration sentences, `StepAttemptTimeoutException`,
  dynamic `StepResult.WaitForEvent`, and "regardless of its default collation");
- the artifact digest `fc4372e1…`;
- row numbering 1–22 and path order;
- the LF-normalized SHA-256 of every governed source;
- the completed ledger text.

| Probe | Mutation | Result |
|---|---|---|
| B1a, B1b | EV-001 runtime-owned `EventId` and optional correlation; DR-033 engine registers the dispatcher | each **red**, naming the source |
| B2a–e | AC-113 clause deleted; MG-030 `SHALL`→`MAY`; DU-032 hooks weakened; PR-015 hook sentence deleted; AC-117 collation weakened | each **red** |
| B4a–c | `CLAUDE.md`, source map, or document 01 reverted wholesale | each **red**, naming the source (the Round 65 misses) |
| B4d | document 13 stale claim reworded with no forbidden token | **red** |
| B5a–d | artifact row dropped, swapped, duplicated, or its hash changed, guard unchanged | each **red** (digest) |
| B5e, B5f | row duplicated as 23, or a source hash changed, with the digest updated and the guard rebuilt | **red** (numbering / source hash) |
| B3a–e | every AC-108, AC-116, AC-118, AC-119, or AC-120 trait removed from its real tests | each **red**, naming the criterion |

Checks against the source:
- **EV-001** matches `workflow-contracts:320`.
- **Dispatcher ownership:** DR-032, DR-033, PR-040, AC-026, and production-readiness say the hosting
  assembly owns the port type, the application registers the implementation, and the durable
  engine consumes it. This matches `AddOrcaCoreDurableEngine`, which resolves the dispatcher with
  `GetService`.
- **Restored text:** AC-113 and AC-117 are verbatim, and AC-116 again binds EV-045. MG-030 is
  `SHALL`. DU-032 and PR-015 again carry the hooks document 15 derives from.
- **Traits:**
  - AC-108 is on the three provider fanout certifications.
  - AC-116 is on the reflection test and both dynamic-wait tests.
  - AC-118 is on the reflection test.
  - AC-119 is on the publish-atomicity, adapter-isolation, and retry-identity tests.
  - AC-120 is on the broker-acknowledgement and poison-path tests.

## 6. JJJ-1 (P3): observations

- **Regression of EEE-1 is not prevented.** In B3x, AC-118 moved back onto the Markdown Task 7.3
  guard satisfied the catalog again. Design section 8 says the Markdown guard "cannot satisfy
  product acceptance coverage", but nothing enforces that. A one-line guard rejecting AC traits in
  `OpenSpecCorpusGuards.cs` would.
- **MG-030 names a dimension the port lacks.** It now says `IWorkflowOperationalStore` exposes "age
  groups". `WorkflowOperatorStatistics` has status, stuck, and active-wait groups plus pressure
  counters, but no age dimension.
- **Two small losses remain.**
  - PR-040 still omits the extension-class ownership and the PostgreSQL options paragraph.
    Document 17 and canonical `developer-facing-surface` keep both, so coverage survives.
  - AC-114 and EV-032 no longer say a crash before commit leaves the wait `Active`.
- **The whole-file pins have no documented update path.** Any later approved edit to one of the 22
  active sources would have to rewrite the dated Task 7.3 artifact and the guard digest.
  - Examples: Task 7.4 may touch `CLAUDE.md` or document 16, and Section 8 will edit document 12.
  - Design section 8 should state who may refresh the pins, and when they retire; for example, when
    Task 7.5's recurring check supersedes them, or at archival.

## 7. Reviewer hygiene and next steps

`HEAD` remained at `89e3ed55357e849852c1a0f6fefa2124433d7e30` throughout. Nothing was staged, and I
created no commit or ref in the reviewed repository. When this verdict was written, the repository
still showed exactly the forty-nine frozen entries. Both disposable worktrees are removed and
pruned.

This verdict matches the Task 7.2 discovery glob. A superseding freeze must:
- register it as a second `REJECT` in the Task 7.2 entry;
- add a rejected-freeze record for this request, manifest, and verdict;
- catalog the verdict in `appendOnlyRecords` with its LF-normalized bytes.

Everything else in this target can be refrozen unchanged apart from the HHH-1 guard rule and the
AC-116 token.

## Determination

The remediation is substantial and, apart from two items, exact:
- the normative contradictions are gone;
- the collateral deletions are restored;
- acceptance evidence is behavioral;
- every governed document is hash-bound.

Two blockers remain. The approval-evidence rule still accepts a wrong, later SHA, which is the
BBB-1 defect class. The acceptance criterion rewritten to name the closed rejection set drops one
of its five members.

**Verdict:** **REJECT**
