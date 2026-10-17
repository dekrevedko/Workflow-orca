# Issue #1 retention contract R1 — independent review verdict

**Date:** 2026-10-16
**Reviewer:** independent review
**Request:** `artifacts/contract-r1-review-request-2026-10-16.md`. This review did not modify it.
**Prior verdict:** `artifacts/contract-independent-review-verdict-2026-10-16.md` (REJECT). It is
preserved byte-exact at `71385cd2…`.
**Target:** branch `issue/1-ephemeral-retention`, base `370b239402c8974a45fc0a54f539a823ab470cf4`. The
target is the 19 entries named by `contract-r1-dirty-manifest-2026-10-16.txt`: 17 untracked files and
2 tracked modifications.

## Verdict: APPROVE

I approve the exact frozen R1 contract and provenance target described in section 1. F1–F4 are each
resolved, and none of the changes introduces a new blocking defect. Five P3 notes remain; they are
non-blocking and carried to later gates (section 4).

This approval does not cover my rulings in the rejecting verdict. It is a fresh verdict on the
R1 bytes.

### Authorized next action: task 1.4 only, in this order

1. Re-verify zero drift against the anchors in section 1.
2. Stage **exactly** the 19 manifest paths. Do not stage this verdict file. Confirm the staged tree is
   `9881d97d63ded744141d8994a1759580bbe061c2`, with parent `370b239`.
3. Create the checkpoint commit on `issue/1-ephemeral-retention`.
4. Create its **direct child**, which adds this verdict file byte-exact and nothing else that changes
   provenance or contract text.
5. Report both SHAs and the clean worktree state.

### Not authorized

- checking or activating any task beyond 1.4;
- task 2.1/2.2 canonical or numbered-document synchronization;
- post-gate registry records;
- executable public-API, compile or package baselines;
- product source or runtime fixes;
- a rebase, push, merge or pull request;
- issue comments or closure;
- any issue #2 or #3 work.

Each of those remains a separately reviewed gate.

## 1. Freeze anchors

| Anchor | Value | Result |
|---|---|---|
| HEAD / index | `370b239…`; nothing staged | Unchanged at start and end of review |
| Raw porcelain, `--untracked-files=all`, ordinal sort, LF + final LF | 19 lines, 1,762 B, `db68933dbc7f9e077464be480eee1c95ef7cce23b539600351b731e10eea19a1` | Matches the author's report and the self-inclusive manifest byte-for-byte |
| Content record (`status\tpath\tlength\tsha256`, LF + final LF) | 19 rows, 3,100 B, `59e64b800bc1d88b138be8789efb8c4fd367b136a86968e0753a1c88a776cc9c` | Identical at start and end of review |
| Candidate tree, staged in a disposable copy | `9881d97d63ded744141d8994a1759580bbe061c2`; 19 files changed; `git diff --cached --check` clean | Matches the author's report |

**Preserved records.** The original request, initial assessment, original route, `.openspec.yaml`
and my rejecting verdict are byte-identical to the rejected target. The two tracked edits are the only
non-change-directory paths: the provenance fixture and the guard's pin constants.

**Base drift (informational).** `origin/master` is at `1c86c23`, which is ahead of `021b2bd`. The
target deliberately stays on `370b239`, and the request states that. See P3-1 for what this means at
integration time.

## 2. Validation reproduced independently

All runs used a fresh short-path disposable detached worktree, `E:/ocv192`, at `370b239`, holding a
byte copy of the 19 entries. Its content record reproduced as `59e64b80…`.

| Check | Result |
|---|---|
| `openspec validate --all --strict` | 22 passed, 0 failed (structural only) |
| `dotnet build OrcaCore.slnx -c Release -warnaserror` | 0 warnings, 0 errors |
| Exact feed: `pack-exact-package-feed.ps1`, run with Windows PowerShell | 12 packages |
| Full `Disposition=Infrastructure` lane | **247 / 247 passed** |
| Five delta block hashes | Equal across the deltas, `contract-r1-provenance-record-2026-10-16.tsv` and `proposed-amendment-route-r1-2026-10-16.json`. Four changed as remediated; the idempotency block is still `58e479b5…` |
| Provenance TSV | 189 rows, 49,579 B, `1abed32398a353509dfc7daab037156aaafc0067bd710a78fe5a4cb7820bb127`. The non-issue subset is 184 rows, `fd6ad044…`, unchanged |
| Current artifact, LF-normalized | `833de7f663e22f4faae4c6be66f1f4ca83cb392ed13834a4824957dac2e90ccf`, equal to the guard pin and the fixture |
| Superseded catalog, recomputed from guard source | 15 rows, 2,653 B, `039a151898cfa09f44754020c35314ddb33d050ee9fc8919d4c495df31fb1a65`. All 15 predecessor files are byte-exact and untouched |
| Expanded occurrence sweep, rerun over the stated scope and pattern | The same 499 path:line set. The record is 128,197 B, `6d199566…`, and every row is classified (144 owning, 331 cross-capability, 24 acceptance) |

The gate is green, so it computed exactly the fixture's `1abed323…` record from the live corpus.

### Mutation probes

Each mutation was applied in the disposable copy, followed by a run of
`CanonicalSynchronizationGate_EnumeratesCapabilitiesDeltasAndRequirementOwners` and then a restore.

| Probe | Mutation | Gate |
|---|---|---|
| R0 | none | green |
| R1 | management delta: abort fence `Terminated` changed to `Cancelled` | red (record SHA-256) |
| R2 | fixture `semanticApprovalEligible` set to true | red |
| R3 | one pending operation dropped | red (pending set) |
| R4 | task 2.2 claim `management-and-querying` (4) changed to (3) | red (turns-green task) |
| R5 | artifact claim "record rows: 189" changed to 188 | red (artifact claim) |
| R6 | guard current-artifact pin reverted to `f2739090…` | red (self-attestation pin) |
| R7 | superseded predecessor artifact edited | red (catalog bytes) |
| R8 | appended catalog row removed from guard source | red (count 15) |
| Final | after all restores | green |

The copy's content record returned to `59e64b80…` afterward. No commit, stage or ref was created in the
reviewed repository.

## 3. F1–F4 disposition

### F1 — provenance prerequisite: RESOLVED

All five prerequisite items are inside the target.

1. Fixture record values: 189 rows, 49,579 B, `1abed323…`, and semantic approval false. The record
   SHA-256 legitimately differs from the rejected `b7172021…`, because four blocks changed.
2. Pending operations: 1 and 4, both owned by task 2.2, and task 2.2 contains the literal claims.
3. Directory inventory: 24 directories, 1,971 B, `a50788bb…`.
4. A new governed current artifact carrying exact executable claims.
5. Guard-source pins, changed only as pins: current hash, catalog count 14→15, catalog SHA-256, and
   one appended row.

No assertion, algorithm or CI filter changed. The checkpoint can therefore be green while recording
honest pending state. ADDED-only routing needs no process amendment.

### F2 — aborted provisional starts: RESOLVED

The settlement requirement now gives aborted provisional units a definitive fate.

- **Triggers.** Two things abort an unaccepted unit: caller cancellation observed after runtime
  creation, and an exception after `Init` before acceptance.
- **Fence.** The unit is fenced through its owning lane to `Terminated`, or keeps an existing
  immutable terminal status. The fence uses cleanup authority that does not depend on the caller's
  token.
- **Visibility.** No binding, key or handle is ever published for the unit.
- **Budget and release.** The unit sits outside the accepted budget, stays protected until physical
  settlement, and is then released eagerly in both policy modes.
- **Race and errors.** If acceptance wins the race, the start returns `Accepted`. The caller still
  receives the original cancellation or exception.
- **Implementability.** The existing `RequestTerminationAsync` already uses `SignalCancellation` plus
  a `CancellationToken.None` lane work item (`EphemeralWorkflowEngine.cs:641`). The
  required caller-independent fence is therefore an existing pattern.
- **Coverage.** Four scenarios cover the cases: provisional failure, first-step cancellation,
  exception after `Init`, and acceptance winning. Tasks 3.1/3.2 require the barrier tests, including
  the `Running`-orphan case my round-1 probe reproduced.

### F3 — executable baselines: RESOLVED

- Task 2.2 now carries signature text, dispositions, permanent records and provenance only. It leaves
  the declaration companion, the public-API baseline, compile fixtures and package records unchanged.
- Task 4.2 changes those executable records atomically with the members, and task 4.5 reviews them.
- Design, proposal and the occurrence dispositions agree with this split.

### F4 — start-to-observe window: RESOLVED

- A normative scenario, *Count pressure retires a result between start return and observation*,
  states the outcome. The wait throws not-found for the original ID even though the job ran. Replaying
  the key yields a fresh ID with `WasExisting = false` and may repeat side effects. Not-found is
  neither proof that the run never started nor permission for an effect-safe retry.
- Task 4.3 requires the barrier case.
- Task 2.1 requires both guide warnings.

### Round-1 P3 notes

| Note | Resolution |
|---|---|
| P3-2 sweep | Expanded and reproduced (section 2) |
| P3-3 wording | Steady-state wording adopted |
| P3-4 router map | The router's `accepted` map is named in the delta, design and tasks |
| P3-4 tiny interval | A one-tick interval is accepted as valid, with documented CPU overhead and no silent clamp |
| P3-4 pin linearization | Pin-first versus retire-first linearization is stated explicitly |
| P3-5 permanent records | Deferred to task 2.2, after real approval and checkpoint evidence. No fabricated row exists today |

All are adequately resolved.

## 4. Remaining P3 notes (non-blocking; carry to later gates)

- **P3-1 — Integration drift.** `origin/master` (`1c86c23`) changes `OpenSpecCorpusGuards.cs` in a
  different hunk (the Task 7.3 documentation pin), along with `docs/production-readiness.md` and two
  ephemeral test files covering timer and throttle waits. The branch checkpoint on `370b239` is
  sound. Any rebase or merge toward `master` must refreeze, re-run the full Infrastructure lane and get
  review, as the request already states. The source slices (task 3.x onward) should start from a base
  that contains the stabilized ephemeral tests.
- **P3-2 — Proposal summary omits the default-mode abort change.** The deltas and design say that
  aborted unaccepted starts are fenced and released "even with `TerminalRetention` omitted". That
  changes host behavior even when retention is disabled. It is a correction of unreachable orphan
  state, but `proposal.md`'s "What Changes" list does not mention it, and its framing says defaults are
  unchanged. Add one bullet at task 2.1.
- **P3-3 — Scenario wording.** The developer-facing scenario *Existing application omits the policy*
  says "the selected retention behavior … remain[s] unchanged". At task 2.2, narrow that to
  accepted-instance retention so it agrees with the body's mandatory abort cleanup.
- **P3-4 — Abort-fence telemetry.** If the fence reuses `RequestTerminationAsync`, it records a
  termination command and terminal-lifecycle metrics that no application issued. When task 2.1
  reconciles doc 15, classify the abort fence's observations, for example as an internal abort and
  not as an application termination command.
- **P3-5 — Generic occurrence dispositions.** Most of the 499 rows share a handful of template
  dispositions. That is acceptable for contract classification, but task 2.1 still owes the
  per-target edits it promises, and its review should check them hit by hit rather than by template.

## 5. Hygiene

- **Read-only target.** The target worktree was only read, apart from writing this file. This file is
  outside the frozen 19-path manifest and must not enter the checkpoint tree.
- **Probes.** All probes ran in `E:/ocv192`, and every script asserted that location. The copy was
  restored to the frozen content record.
- **Removal.** The copy is registered in the main repository's worktree list and can be removed with
  `git worktree remove --force E:/ocv192`. Use `--force` because it contains build outputs and the
  package feed.
