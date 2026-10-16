# Developer-facing interface — Section 6 ancestor-terminal remediation: independent re-review verdict

**Date:** 2026-07-29
**Reviewer role:** independent exit re-reviewer (Section 6 exit and authorization of task `7.0`)
**Request under review:** `developer-facing-interface-section-06-ancestor-terminal-remediation-rereview-request-2026-07-29.md`
**Rejection verdicts remediated:**
`developer-facing-interface-section-06-exit-review-independent-verdict-2026-07-29.md` (R1) and
`developer-facing-interface-section-06-independent-exit-review-verdict-2026-07-29.md` (R1)

## Verdict

**APPROVE.** No release blocker.

Section 6 exits. The implementation owner is authorized to **begin** task `7.0` in a later turn.
This approval does not mark task `7.0` complete and approves no Section 7 or Section 8 outcome.

Finding R1 from both rejection verdicts is fully discharged, and it was discharged at the root
cause rather than at the symptom: scenario ownership is now *derived* from each fixture's own
`turnsGreenTask` value, so the class of misfiling that produced R1 can no longer occur.

## 1. Provenance verification

Verified before reading any conclusion in the request.

| Item | Claimed | Independently observed | Result |
|---|---|---|---|
| Branch | `feature/v3-rebuild` | `feature/v3-rebuild` | match |
| `HEAD` | `d76192f089dd07f68e310c21fe4e5a38dd93cf7f` | identical | match |
| `HEAD` tree | `2264e670493ecc76359d42ee5273028eb287a566` | identical | match |
| Baseline ancestry | baseline `8c2dd712…` is ancestor of `HEAD` | confirmed | match |
| Porcelain entries | 484 | 484 | match |
| Entry classes | — | 347 M / 9 D / 128 `??` | consistent |
| Raw-manifest SHA-256 | `86ED89…03E487` | `86ED89722B12580487FD3124BE4753D99DCBDA5FCB91590C1671520A4803E487` | match |
| LF-normalized sorted-status SHA-256 | `F02F17…605DD1` | `F02F174124700547ADEF1785CBC4B6F4DBCA97EEB80D70A5DF3E1DF21C605DD1` | match |
| Manifest line count | 484 | 484 | match |
| Reshape tasks | — | 90 / 46 / 136, 0 duplicates | unchanged |
| Runtime-governance tasks | — | 16 / 0 / 16, 0 duplicates | unchanged |
| Task `7.0` | open and blocked | `- [ ] 7.0` | confirmed open |

### Delta from the rejected target

I diffed the 484-entry manifest against the previously rejected 480-entry manifest. The delta is
**exactly four untracked review artifacts** and nothing else:

- the new remediation request and its manifest; and
- the two 2026-07-29 rejection verdicts.

No new source or test path entered the target. The remediation therefore edited files already
present in the dirty set, which is consistent with the changes I verified in section 3. The prior
review's disclosed drift (the concurrent second verdict) is now absorbed into the frozen manifest,
which resolves that loose end.

### Manifest comparison after validation

Recomputed after every command in section 2 completed: `HEAD`, `HEAD` tree, 484 entries,
347 M / 9 D / 128 `??`, and **both hashes unchanged**. Zero drift across the validation matrix.

## 2. Reproduced commands and results

| Lane | Claimed | Observed | Result |
|---|---|---|---|
| Solution build (`--no-incremental`) | 0 warnings / 0 errors | 0 warnings / 0 errors | match |
| Core | 464 | Total 464, Failed 0, Skipped 0 | match |
| Ephemeral | 173 | Total 173, Failed 0, Skipped 0 | match |
| Durable | 332 | Total 332, Failed 0, Skipped 0 | match |
| Hosting | 17 | Total 17, Failed 0, Skipped 0 | match |
| Acceptance | 70 | Total 70, Failed 0, Skipped 0 | match |
| Provider certification | 78 | Total 78, Failed 0, Skipped 0 | match |
| Infrastructure ×3 | 104/104 three consecutive times | 104/0/0, 104/0/0, 104/0/0 | match |
| **Section 6 drivers** | **32/32** | **Total 32, Failed 0** | **match** |
| **Expected-red** | **exactly 61, zero passes** | **Total 61, Failed 61, exit 1** | **match** |
| Green compile fixtures | — | fresh pack, 26+26 CS1061 diagnostics, incomplete-control rejected | pass |
| Product-authoring ExpectedRed compile | — | `(0)` remaining gaps | pass |
| Package ExpectedRed | — | exactly 8 named reds | pass |
| Strict OpenSpec | 17/17 | `Totals: 17 passed, 0 failed` | match |
| NuGet vulnerability audit | — | 32 projects, none vulnerable | pass |
| Whitespace | — | `git diff --check` exit 0, CRLF notices only | pass |

The eight package reds are unchanged: `primary-package`, `minimal-ephemeral`, `postgresql-durable`,
`callback-ingress`, `in-memory-durable`, `dag-hosting`, `provider-custom-host`,
`kubernetes-companion` — all Section 7/8.

Counts reconcile exactly. 95 catalogued scenarios partition as 4 + 8 + 32 green and 51 red; 51
scenario reds + 10 non-scenario facts = 61. The infrastructure lane rose 103 → 104 precisely because
the Section 6 theory gained its 32nd case, and expected-red fell 62 → 61 for the same reason. No
guard was added, removed, or relaxed to reach these numbers.

## 3. R1 remediation — independently verified

### 3.1 Ownership is genuinely derived, not relabelled

The three hardcoded id lists (`Section4/5/6ScenarioIds`) are **deleted**. `FinalOwningSection`
parses every `N.M` task reference out of the fixture's `turnsGreenTask` and returns the **maximum**
section, throwing on an unparseable value. The green theories select `== 4`, `== 5`, `== 6`; the
expected-red theory selects `> 6`.

This is structurally sound, and the choice of *maximum* is the correct one:

- A scenario naming both `4.2` and `7.2` yields 7 and stays red — split requirements are honoured.
- A scenario whose every task is complete (max ≤ 6) is **forced** into a green lane and must supply
  a driver. This is exactly the property whose absence caused R1.
- Because the selector is a single integer, the sets are mutually exclusive and exhaustive by
  construction. A scenario can no longer be omitted from every lane or counted in two.

I checked the regex against the real data, including the negative lookbehind: `"5.4-6.2"` → max 6,
`"4.2,7.2"` → 7, `"6.4-6.9,8.8-9.4"` → 9, `"3.11a"` → 3. No misparse.

### 3.2 No ownership was gamed to dodge work

The obvious way to fake this fix is to weaken a `turnsGreenTask` so a scenario drifts into the red
set. I tested for it directly by recomputing ownership for all 95 scenarios and comparing the
derived Section 6 set against the old hardcoded list:

- derived Section 6 = **32**;
- added versus the old list = exactly `['ancestor-terminal-suppresses-merge']`;
- **dropped versus the old list = none**;
- Section 4 = 4 and Section 5 = 8, both unchanged;
- full distribution: 4→4, 5→8, 6→32, 7→37, 8→11, 9→3 (95 total).

The `ancestor-terminal-suppresses-merge` fixture entry is **byte-for-byte unchanged** — same setup,
same assertion, same `turnsGreenTask: "5.4-6.2"`. The requirement was met, not redefined.

### 3.3 The driver exists, executes, and covers the previously missing leg

`StructuredFanoutScenarioHost.AncestorTerminalSuppressesMerge` now carries
`[Phase0Scenario("ancestor-terminal-suppresses-merge", "3.6")]`. I confirmed via verbose run output
that it **STARTED and FINISHED in 0.43 s** inside the Section 6 lane — real execution, not a
discovery artifact. It no longer appears anywhere in the expected-red output.

I counted the schedules in source: one explicit durable `Parallel`/`WhenAllOutcomes` case, three from
the durable loop (which correctly skips `Parallel`+outcomes as already covered), and four from the
ephemeral loop — **eight total**, matching two engines × two fan-out shapes × two joins:

| Engine | `Parallel` | `ForEach` |
|---|---|---|
| Durable | `WhenAll`, `WhenAllOutcomes` | `WhenAll`, `WhenAllOutcomes` |
| Ephemeral | `WhenAll`, `WhenAllOutcomes` | `WhenAll`, `WhenAllOutcomes` |

Every schedule authors `CompleteWithin(TimeSpan.FromMinutes(1))` at the root and parks both children
on `Wait(EventName "Resume")`, so the deadline genuinely races **active** branches and items. This
is the precise leg that R1 found covered by nothing: `CompleteWithin` previously appeared zero times
in any merge-suppression test in either engine.

### 3.4 The assertions are strong and non-vacuous

This was my main concern — a merge-suppression test asserting "merge not called" passes trivially if
the merge could never fire. It does not here:

- **Non-vacuity precondition.** Both helpers first require `Status == Waiting` **and
  `ActiveWaits.Count == 2`**, proving the fan-out was live and parked before expiry. A degenerate or
  empty scope fails here.
- **Two independent regression detectors.** `RecordMerge` both increments the call counter *and*
  sets `Results = ["merged"]`. The assertions check `mergeCalls() == 0` **and**
  `state.Results.Count == 0`. A suppression regression trips both.
- **Committed state, not in-memory state.** The durable helper deserializes `FanoutState` from the
  envelope `StatePayload`, so it verifies the merge did not commit to the checkpoint.
- **Terminalize-once.** The durable helper asserts `due.Count == 1` — exactly one timer fired, no
  duplicate or rollover.
- **Replacement-host projection.** The durable helper constructs a *second* runtime over the same
  store and re-drives `StartOrGetAsync` with a different input (`"ignored"`), then asserts
  `TimedOut`, zero active waits, zero merge calls, and empty results. A replacement host can neither
  reset the deadline nor resurrect the suppressed merge.
- **Correct terminal status and fencing.** All eight assert `Status == TimedOut` and
  `ActiveWaits.Count == 0`.

I verified the merge delegate is wired into both `WhenAll` and `WhenAllOutcomes` for both shapes in
both engines, so no schedule is a no-op path.

The cancellation and termination legs of the scenario's setup remain covered by the four pre-existing
passing engine tests (termination × `WhenAll`, cancellation × `WhenAllOutcomes`, across `Parallel`
and `ForEach`, both engines). Together with the eight new deadline schedules, all three terminals in
the scenario's setup are now covered by executed evidence. R1 is discharged.

## 4. Carried-forward findings re-verified

The prior review's sound findings were re-checked against this target and continue to hold: the
forged runtime defenses genuinely produce `SFE-RUN-002` before pool mutation and `SFE-RUN-001`
without generation or ownership drift; `CompleteWithin` rejects a second call before mutating, so the
first deadline survives, and `Timeout.InfiniteTimeSpan` is rejected; `StructuredExecutionHostOptions`
carries only the path ceiling and exact-type throttles with no binder API, toggle, or codec hook, and
lambdas remain untargetable; portable `StepResult` exposes only `Completed`/`Failed`/`WaitForEvent`;
`ExpireTicketsAsync` marks expiry while the ticket stays held, so capacity is never reclaimed by
time, and shrink-below-held yields resize debt without revocation; obligations are retained as
`CancelledBeforeGrant` with zero tickets allocated.

The Section 7 boundary remains honestly drawn. Only `AddOrcaCoreEphemeralEngine` and
`AddOrcaCoreDurableEngine` were added; inherited `AddOrcaCore`, `AddOrcaCoreHostedServices`, the
provider registrations, and `AddOrcaCoreOpenTelemetry` all remain, with OpenTelemetry package
references still confined to `OrcaCore.Hosting`. `six-hosting-entry-owners` and the
manifest/project-metadata facts remain red, and catch-all deletion remains task `7.10`. Task
accounting is unchanged at 90/46/136 and 16/0/16 — the remediation completed no new task, which is
correct, because it closed an evidence gap inside tasks already marked complete.

All 51 remaining scenario reds still fail for one reason only — the final-target driver does not
exist — and each maps to an unfinished Section 7, 8, or 9 task. The Section 6 disposition text in
`tasks.md` records both rejections, names the omitted driver, describes the derivation fix, retains
the Section 7 boundary, and keeps task `7.0` blocked. It does not overclaim.

## 5. Environment limitation

Unchanged and independently reproduced: the Docker CLI is present (29.6.1, context `desktop-linux`)
but `docker ps` fails with `open //./pipe/dockerDesktopLinuxEngine: The system cannot find the file
specified`, and the named pipe does not exist. The Docker-backed PostgreSQL and SQL Server provider
suites could not be executed by the owner or by me. This is an environment limitation, not passing
product evidence; the request states it accurately. It does not bear on R1 or on any Section 6 exit
claim, and should be discharged before any release shipping the PostgreSQL production role set under
tasks `7.10`/`7.11`.

## 6. Structural observation from the prior review — closed

The prior review recorded that section membership rested on hand-maintained lists and that all
expected-red scenarios failed at driver-existence before any behavior assertion, so the lane could
not self-distinguish "deferred" from "missing driver for completed work". That recommendation has
been implemented as specified: membership now derives from `turnsGreenTask`, and the partition is
exhaustive and mutually exclusive by construction.

One residual note, not a finding and not blocking: the packet still has no explicit *named* guard
asserting the completeness property, so the protection is a structural consequence of
`FinalOwningSection` rather than an asserted invariant. Because the derivation throws on an
unparseable `turnsGreenTask` and the max-section rule forces any all-complete scenario into a green
lane, I consider the property adequately protected. Should Section 7 introduce further section
lanes, an explicit guard asserting that the green and red sets partition all 95 scenarios would make
the invariant self-documenting.

## 7. Scope of this verdict

Section 6 exits with no release blocker. The implementation owner is authorized to **begin** task
`7.0` in a later turn; this does not mark task `7.0` complete.

This approval covers the exact 484-entry frozen target identified in section 1. It approves no
Section 7 or Section 8 artifact and does not relieve the expected-red package, facade, owner, DAG,
or governance-certification guards, which must remain red until their owning sections land. The 61
intentional failures and the eight package reds are expected to stay red; any turning green before
their owning section is itself a defect.

No reviewed source, test, task, spec, plan, document, manifest, request, or existing review artifact
was modified during this review. This file is the sole addition made by this reviewer.
