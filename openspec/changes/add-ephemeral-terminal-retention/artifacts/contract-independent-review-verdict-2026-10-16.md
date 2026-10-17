# Issue #1 retention contract — independent review verdict

**Date:** 2026-10-16
**Reviewer:** independent review
**Request:** `artifacts/contract-review-request-2026-10-16.md` (not modified by this review)
**Target:** branch `issue/1-ephemeral-retention`, base `370b239402c8974a45fc0a54f539a823ab470cf4`. The
target is the nine untracked files listed in the request. HEAD and the ordinary index are unchanged.

## Verdict: REJECT

The policy design is sound. I accept the requested decisions below, and the five ADDED identities and
the ADDED-only routing are correct. The target is still not approvable as frozen, for two reasons.

- It cannot be checkpointed. Task 1.4 would commit a tree whose must-be-green CI Infrastructure lane
  fails. The repository's own precedent puts the provenance refreeze inside the contract target.
- The contract contradicts itself on aborted provisional starts. A probe reproduced the case.

Two further P2 gaps also need a fix in the refrozen contract.

**Authorized next gate:** remediate F1–F4, refreeze the complete self-inclusive target, including the
F1 provenance prerequisite, and obtain a fresh independent review.

**Not authorized:** a checkpoint commit, canonical synchronization, numbered-document edits, product
source, a push or merge, issue comments or closure, and starting issue #2.

## 1. Freeze anchors

| Anchor | Value |
|---|---|
| HEAD | `370b239402c8974a45fc0a54f539a823ab470cf4`; nothing staged |
| Raw porcelain, `--untracked-files=all`, ordinal sort, LF + final LF | 9 lines, 742 B, `8a5fd2e1a14a8f2fc3b89cb9ffd1fdd8c77171f9277472ff4e846a8679931367` |
| Content record (`status\tpath\tlength\tsha256`, LF + final LF) | 9 rows, 1,371 B, `eff7c4a9cb23017841334f574353e897acc1bff12446241ca397aa7f6792a5af` |

I took both anchors at the start of review and again before writing this file. They were identical, so
there was no drift. A byte copy of the nine files was then placed in a disposable detached worktree at
the same base, and its content record also reproduced as `eff7c4a9…`.

**Manifest note.** Adding this verdict makes the directory hold ten untracked files. The verdict is not
part of the reviewed scope.

**Base drift (P3-1).** `origin/master` has moved to `021b2bd` (commits `cc2213d` and `021b2bd`,
NuGet.org trusted publishing). They touch `docs/production-readiness.md`, which the request names as an
affected guide, and one unrelated `OpenSpecCorpusGuards` pin. The provenance fixture is unchanged, so
the inventory below is still valid. The next freeze must either stay on `370b239` and say so, or rebase
and refreeze. The assessment's statement that the default branch is at `370b239` is now stale.

## 2. Validation reproduced

| Check | Result |
|---|---|
| Five delta block SHA-256 values (heading through block end, trailing blank lines stripped, LF, no final LF) | All five equal the request and route JSON |
| `openspec validate add-ephemeral-terminal-retention --strict` / `--all --strict` | valid / 22 of 22 passed (structural only) |
| Focused `CanonicalSynchronizationGate_EnumeratesCapabilitiesDeltasAndRequirementOwners` | Expected red: `Expected collection to contain 184 item(s), but found 189` |
| Full provenance record, dumped by a probe in the disposable copy | 189 rows, 49,579 B, `b717202106d488a443c949a724952df9ac0b91e1ccbb5e7e98f88bd0e166f311`; the 184-row subset is 48,504 B, `fd6ad044…`, equal to the fixture |
| `dotnet build OrcaCore.slnx -c Release -warnaserror` | 0 warnings, 0 errors |
| Full `Disposition=Infrastructure` lane, after building the exact 12-package feed with Windows PowerShell | 246 of 247 pass; the only failure is the canonical synchronization gate |

## 3. Reviewer decisions requested

1. **Policy signatures, default and validation: ACCEPTED as written.**
   - The shape: `EphemeralTerminalRetentionPolicy` with a private constructor and `Create`, matching
     the existing `TransientPoolDefinition` idiom, plus `EphemeralEngineHostOptions.TerminalRetention`
     with a null default and no timer.
   - The validation: count ≥ 1, `maxAge` > 0, and 0 < interval ≤ `maxAge`, with the interval also
     ≤ 1 day. Violations throw `ArgumentOutOfRangeException` in signature order.
   - The policy is part of the existing role fingerprint, including whether it is enabled.

   **Host policy versus deferred destructive commands: ACCEPTED.**
   - Canonical *Command availability follows execution-mode capability* defers retention and history
     **commands**. Its scenario keeps members absent "until a separate lifecycle/authorization/retention
     amendment is approved", which is this amendment.
   - *Deferred management operations have no placeholder* scopes eviction to ordinary application
     handles.
   - A host option changes neither rule, so no existing canonical block needs a MODIFIED successor and
     no process-only successor exception is required.

2. **Settlement as the eligibility condition: ACCEPTED, except provisional starts (F2).**
   - Per-instance ownership tracking that does not depend on throttles is necessary.
   - Today the only physical tracking is `GovernanceLease.RetainUntil`, applied on fenced timeout paths
     (`InMemoryExecutionStateAdapter.cs:473`, `:521`).
   - When `DetachedAttemptState` is false, a fenced root attempt still holds the live root-state object.

3. **Count and age: ACCEPTED.** This covers:
   - the monotonic `TimeProvider` timestamp and the settlement-ordinal tie-break;
   - age retirement before count retirement;
   - maximum age, not a guaranteed minimum;
   - pins counted in addition to the budget;
   - idle sweeps.

   **Count-before-age-window trade-off: ACCEPTED,** with the hard bound winning, on the condition that
   F4 makes its consequence normative.

4. **Handles, keys and waits: ACCEPTED.** This covers:
   - not-found through the existing `WorkflowInstanceNotFoundException`, code `WF-INSTANCE-NOT-FOUND`
     (`WorkflowFacadeContracts.cs:390`);
   - definition mismatch unchanged for retained instances;
   - stale-handle isolation;
   - subscribe-then-recheck wait pins with release exactly once;
   - key expiry only when atomic retirement happens.

   This is consistent with MG-003, doc 17 §17 start/query text and canonical *Typed committed root
   state and output are queryable*.

5. **Phase ledger: ACCEPTED in structure,** subject to F1 (tasks 1.3 and 1.4) and F3 (task 2.2).

## 4. Findings

### F1 — P1 — The contract target cannot be checkpointed as specified

`OpenSpecCorpusGuards` carries `Disposition=Infrastructure`, and CI runs that lane as must-be-green
(`.github/workflows/ci.yml`, filter `Disposition=Infrastructure`). Task 1.4 checkpoints this target
while the assessment says "Existing fixtures are not promoted", so the checkpoint commit would be red.

The precedent is the RC-3 contract checkpoint `e6802a3`. It refroze
`openspec-provenance-checkpoint.json` inside the contract target, with
`"semanticApprovalEligible": false` and its pending operations listed.

The assessment correctly says the single observed failure "does not establish the result of later
assertions". I probed further in the disposable copy and found that the gate has more pins than the
row count. The complete prerequisite is:

1. **Fixture record values:** `recordCount` 189, `recordBytes` 49,579, `recordSha256` `b7172021…`,
   and `semanticApprovalEligible: false`.
2. **Fixture pending operations:** `pendingCanonicalOperations` set to `developer-facing-surface` 1
   and `management-and-querying` 4, both with `turnsGreenChange` `add-ephemeral-terminal-retention`
   and `turnsGreenTask` `2.2`. Task 2.2 already contains the literal ``` `capability` (n) ``` claims
   the guard requires.
3. **Capability-directory inventory:** it moves from 22 directories (1,808 B, `2869d4ad…`) to
   24 directories, 1,971 B, `a50788bb2f903a5903ddee5a494a92421e34c347e3a9b67f8a4c935a009dc03a`.
4. **A new current provenance artifact:** a file under `openspec/changes/**` whose name contains
   `openspec-provenance-`, carrying the exact record claims.
5. **Matching guard-source pins:**
   - `CurrentOpenSpecProvenanceSha256` updated;
   - the current artifact, `task-2-1-stream-identity-openspec-provenance-2026-10-07.md` (`f2739090…`),
     appended to the superseded catalog;
   - `SupersededOpenSpecProvenanceArtifactCatalogCount` raised from 14 to 15, with its catalog SHA-256
     updated.

With items 1–3 applied, the gate passed every earlier check: requirement ownership, the post-gate path,
canonical inventory, historical removals and the CI-lane check. It then stopped at
`ValidateReviewArtifact`, which needs items 4–5.

**Required:**
- Fold all five items into the next frozen contract target.
- Run the full Infrastructure lane green on that target and record the result.
- Keep semantic approval false.
- Do not open a separate process-amendment target. Every operation is ADDED and there is no
  duplicate owner.

### F2 — P2 — Aborted provisional starts contradict "protect every active instance"

- *Ephemeral terminal retention requires physical settlement* (management delta, line 4) protects
  every active, waiting or delayed instance indefinitely.
- Scenario *A provisional start fails* (line 33) promises that provisional records "are then released
  without an unbounded orphan payload/timer backlog".

Neither the delta nor design decision 4 says what state an unaccepted provisional instance ends in. The
engine saves the runtime instance during `Init` (`EphemeralWorkflowEngine.cs:275`). The facade
publishes a binding only after the engine call returns (`EphemeralWorkflowFacade.cs:320`, `:345`).

**Reproduced.** I ran a disposable probe test, not kept. The scenario was a one-step workflow whose
start token is cancelled while the step runs. The step then returns. Results:

- the caller receives `TaskCanceledException`;
- the runtime registry still holds one instance with status `Running`;
- the facade holds zero bindings;
- replaying the key creates a new instance.

Nothing can reach that orphan. Under the drafted rules it is active, so it stays protected forever,
and the scenario's promise cannot be met.

**Required.** State the definitive fate of a provisional unit that fails or is cancelled before
acceptance, for example:
- it is fenced through its owning lane to one named terminal status, as the ambiguous-wait path already
  does with `TerminateInstanceAsync`;
- it is never key-bound or handle-reachable;
- it is outside the eligible budget, and is released immediately after physical settlement.

Also state whether caller cancellation of `StartOrGetAsync` after runtime creation counts as an abort.
No canonical rule covers start-token cancellation today. Add both the cancellation case and the
exception-after-`Init` case to task 3.1.

### F3 — P2 — Task 2.2 schedules executable API, compile and package baselines before any source exists

`PublicApiBaselineGuards.EveryTargetAssembly_MatchesTheApprovedExactPublicApiBaseline` compares the
**compiled** assemblies with `Fixtures/PublicApi/v1/OrcaCore.Engine.Ephemeral.api.txt` in the
Infrastructure lane. Positive compile fixtures that use `TerminalRetention` cannot compile before the
type exists. If task 2.2 changes these baselines in a source-free transition, its checkpoint goes red.
The approved DAG flow changed its API baseline in the source slice (task 2.1, round 100).

**Required:**
- Task 2.2 carries only the reviewed signature text (doc 17 matrix and namespace table, plus the
  companion disposition in P3-2) and the registry and provenance state.
- The executable public-API baseline, compile fixtures and package-source records change in the source
  slice that adds the members (task 4.2), and task 4.5 reviews them.

### F4 — P2 — The start-to-wait window under count pressure is not normative

Design decision 5 says a returned `StartOrGetAsync` handle is not a wait subscription. Callers "must
begin a wait while the instance remains retained". `design.md` is never synchronized, and the deltas
only imply this, through "SHALL NOT grant a pin" and "neither a minimum retention duration".

**Consequence.** Retirement is ordered by settlement ordinal, so the unit stays retained only until
`MaxRetainedInstances` other units settle. If that many settle between a start returning and its
caller's wait admission, two things follow:
- the canonical helper `start.WaitForOutputAsync()` throws not-found for a job that actually
  completed;
- the natural retry, replaying the same key, starts a fresh run and repeats its side effects.

**Required:**
- Add a scenario that states this outcome to *Retired ephemeral handles have deterministic outcomes*
  or *Ephemeral start idempotency expires with atomic retirement*.
- Add the barrier-controlled case "unit settles and is retired by count between start return and wait
  admission" to task 4.3.
- Require guide text, in task 2.1, saying two things: a not-found after `Accepted` does not mean the
  run never started, and `MaxRetainedInstances` should exceed the completions that can occur within a
  caller's start-to-observe gap.

### P3 findings (non-blocking; carry them into the refreeze or the task 2.1 transition)

- **P3-1 — Base drift.** See section 1.
- **P3-2 — Reverse-sweep coverage.** The keyword set never included the names being changed
  (`EphemeralEngineHostOptions`, `HostOptions`, `StartOrGetAsync`, `lifetime`, `memory`). As a
  result, these hits are unclassified:
  - `repository-foundation`, *Package topology and ownership are exact*, which binds options
    namespace and assembly ownership to doc 17's table. That table's row at line 2082 needs the new
    type. The request calls this capability "no-hit".
  - `runtime-resource-governance`: "V1 SHALL NOT expose an independent host-wide workflow-instance or
    advancement ceiling", and the host-option placement rule.
  - Doc 17: the §17.4 options block and the distinct-limit taxonomy.
  - MG-020: the `evicted` instance event, and archive/delete events reserved for "the deferred public
    retention capability".
  - MG-050 and MG-053, and doc 03 "Terminal instances leave active memory quickly". Read literally,
    these conflict with the default of host-lifetime retention.
  - NF-031.

  In addition, the declaration companion declares no host options at all, only builders. Decide
  whether to amend it or exclude it explicitly. Do not add only the retention policy type.
- **P3-3 — Steady-state wording.** Wording such as "Existing `EphemeralEngineHostOptions` SHALL add
  exactly one…" and "preserve current host-lifetime retention" describes a change. It will read wrong
  once canonical, so state the resulting surface instead.
- **P3-4 — Retirement inventory and boundaries:**
  - Name the internal event router's per-instance `accepted` deduplication map
    (`EphemeralWorkflowFacade.cs:692`) in the unit inventory and its tests.
  - `SweepInterval` has no floor, so one tick is valid and produces a continuously running sweep.
    Either add a floor or document the footgun.
  - Decision 4's "after admitted readers finish" never applies to an eligible unit, because pinned
    units are ineligible under decision 2. Say that pin admission and retirement linearize instead.
- **P3-5 — Post-gate registry classification.** `ValidatePostGateAmendmentPath` passes with no row,
  yet the route JSON lists `post-gate-amendment-path.json` as a promotion target. Record explicitly
  whether this new ADDED-only change needs a permanent post-gate record under CLAUDE.md, rather than
  leaving that open.

## 5. Method and hygiene

- **Where things ran.** Validation and probes ran only in the disposable detached worktree `E:/ocv190`,
  at `370b239`. Every script asserted that location. The target worktree was only read, apart from
  writing this file.
- **Probes and their reversal:**
  - a guard row dump, reverted;
  - fixture and inventory edits, reverted;
  - a facade probe test, deleted.

  After all three, the copy's content record was back at `eff7c4a9…`.
- **No refs created.** No commit, stage or ref was created in the reviewed repository. The disposable
  worktree is registered in the main repository's worktree list and can be removed with
  `git worktree remove E:/ocv190`.
