# Developer-facing interface — Section 6 exit: independent review verdict

**Date:** 2026-07-29
**Reviewer role:** independent exit reviewer (Section 6 exit and authorization of task `7.0`)
**Request under review:** `developer-facing-interface-section-06-exit-review-request-2026-07-29.md`
**Prerequisite verdict relied upon:** `developer-facing-interface-section-04-05-amendment-remediation-independent-rereview-verdict-2026-07-29.md`

## Verdict

**REJECT.** One release-blocking evidence gap.

Section 6 does not exit. **Task `7.0` remains blocked.**

The finding is narrow and specific: the behavior scenario `ancestor-terminal-suppresses-merge` was
explicitly retained as a **Section 6** obligation by the immutable prerequisite approval, and it is
proven by nothing in this target — no scenario driver of either kind, and no engine test covering
the leg that is unique to it.

Everything else in the request reproduced exactly. The build, all six product lanes, the
infrastructure lane, the 31 selected Section 6 drivers, compile and package fixtures, strict
OpenSpec validation, the vulnerability audit, and the whitespace check are all green and honest, and
the Section 7 boundary is honestly drawn. The defect is an evidence gap in one retained Section 6
proof, not a broad failure.

### Reviewer note on this verdict

An earlier draft of this file recorded APPROVE, treating the finding below as non-blocking
bookkeeping on the grounds that adjacent tests plus a shared code path covered the behavior. That
reasoning was wrong on the governing point, and section 4 explains why: the prerequisite approval
assigned this specific scenario to Section 6, and the one leg unique to it is covered by no
executed evidence. Code-reading by a reviewer is not a substitute for the executed driver the guard
packet exists to require. The verdict is REJECT.

## 1. Provenance verification

Verified directly, before reading any conclusion in the request.

| Item | Claimed | Independently observed | Result |
|---|---|---|---|
| Repository | `X:\Projects\GitHub\Workflow-orca` | same | match |
| Branch | `feature/v3-rebuild` | `feature/v3-rebuild` | match |
| `HEAD` | `d76192f089dd07f68e310c21fe4e5a38dd93cf7f` | identical | match |
| `HEAD` tree | `2264e670493ecc76359d42ee5273028eb287a566` | identical | match |
| Baseline ancestry | baseline is ancestor of `HEAD` | `git merge-base --is-ancestor` exit 0 | match |
| Porcelain entries | 480 | 480 | match |
| Entry classes | 347 M / 9 D / 124 `??` | 347 M / 9 D / 124 `??` | match |
| Raw-manifest SHA-256 | `1F0667…EAAFC` | `1F0667403678F12870A51DA2F5218B389959B8CCF00E0A2BD5DBA46574CEAAFC` | match |
| LF-normalized sorted-status SHA-256 | `C10630…6B21D7` | `C106305B04F733C5A9A2765D64074800026A3DBF847B20465BB28FFB5E6B21D7` | match |
| Reshape tasks | 90 / 46 / 136, 0 duplicate IDs | 90 / 46 / 136, 0 duplicates | match |
| Runtime-governance tasks | 16 / 0 / 16, 0 duplicate IDs | 16 / 0 / 16, 0 duplicates | match |

### Manifest comparison after validation

The full provenance set was recomputed after every validation command in section 2 completed:

- `HEAD`, `HEAD` tree, entry count (480), and all entry classes: unchanged
- raw-manifest SHA-256: `1F0667403678F12870A51DA2F5218B389959B8CCF00E0A2BD5DBA46574CEAAFC` (unchanged)
- LF-normalized sorted-status SHA-256: `C106305B04F733C5A9A2765D64074800026A3DBF847B20465BB28FFB5E6B21D7` (unchanged)

No path or status drift occurred across the validation matrix. Both hashes reproduced exactly before
and after.

### Concurrent-artifact disclosure

After the post-validation revalidation above and before this verdict was finalized, a second review
artifact appeared in the working tree that is not mine and is not part of the frozen 480-entry
manifest: `docs/review/developer-facing-interface-section-06-independent-exit-review-verdict-2026-07-29.md`
(written 17:35 local; this file 17:37). It records REJECT on the same scenario identified below.

This is disclosed for completeness because it is drift relative to the frozen target. It did not
determine this verdict. I reached the same conclusion by verifying the load-bearing factual claim
directly against the immutable prerequisite approval and against the source tree, as recorded in
section 4; that verification, not the other document's conclusion, is the basis here. Its presence
does mean the working tree now holds two independent verdicts plus this one's target drift, which
the implementation owner should reconcile before re-freezing.

## 2. Reproduced commands and results

All commands run from `X:\Projects\GitHub\Workflow-orca`.

| Lane | Claimed | Observed | Result |
|---|---|---|---|
| Solution build | 0 warnings / 0 errors | 0 warnings / 0 errors | match |
| Full `--no-incremental` rebuild | — (reviewer-added) | 0 warnings / 0 errors | clean |
| Core | 464 / 0 / 0 | Total 464, Failed 0, Skipped 0 | match |
| Ephemeral | 173 / 0 / 0 | Total 173, Failed 0, Skipped 0 | match |
| Durable | 332 / 0 / 0 | Total 332, Failed 0, Skipped 0 | match |
| Hosting | 17 / 0 / 0 | Total 17, Failed 0, Skipped 0 | match |
| Acceptance | 70 / 0 / 0 | Total 70, Failed 0, Skipped 0 | match |
| Provider certification | 78 / 0 / 0 | Total 78, Failed 0, Skipped 0 | match |
| Infrastructure guards ×3 | 103 / 0 / 0 on three isolated runs | 103/0/0, 103/0/0, 103/0/0 | match |
| Section 6 current-physical drivers | 31 passed / 0 failed | Total 31, Failed 0 | match |
| Expected-red guards | 0 passed / 62 named failures / 0 skipped | Total 62, Failed 62, exit 1 | match |
| Green compile fixtures | fresh package, 26+26 diagnostics, incomplete-control rejection | reported exactly that; exit 0 | match |
| Product-authoring ExpectedRed compile set | 0 remaining gaps | `Expected-red compile fixtures (0)` | match |
| Package ExpectedRed compile set | exactly 8 named gaps | 8 named reds | match |
| Strict OpenSpec (both changes) | valid | both `is valid`, exit 0 | match |
| Strict OpenSpec (all) | 17 items passed | `Totals: 17 passed, 0 failed` | match |
| NuGet vulnerability audit | clean across 32 projects | 32 projects, none vulnerable | match |
| Whitespace | exit 0, line-ending notices only | exit 0, only CRLF notices | match |

The eight package reds are exactly `primary-package`, `minimal-ephemeral`, `postgresql-durable`,
`callback-ingress`, `in-memory-durable`, `dag-hosting`, `provider-custom-host`,
`kubernetes-companion` — all Section 7/8 scope.

Procedural note: my first attempt ran the six product lanes concurrently with a rebuild, which
deleted three test binaries mid-run and produced five `MSB3061` file-lock warnings. Both were
artifacts of my own scheduling, not product defects. Every figure above comes from a clean
sequential re-run.

## 3. Expected-red decomposition

The 62 failures decompose exactly as claimed: **52 scenario cases + 10 non-scenario facts**, zero
passes. The 10 facts are package-graph, tier-edge, project-metadata, friend-assembly,
normative-owner, facade, journey, feed, provider-author, and DAG-assembly facts, all Section 7/8.

Every one of the 52 scenario failures fails at the same point — the final-target driver does not
exist (41 in `OrcaCore.DeveloperSurface.BehaviorScenarios`; the 11 `3.11d` governance cases in the
existing `OrcaCore.ProviderCertification`). None reaches a behavior assertion and fails it.

I cross-checked ownership of all 52 against Section 7/8 task text, and separately against each
fixture's own `turnsGreenTask` field. Sixteen scenarios name a Section 4/5/6 task; fifteen of those
also name an unfinished Section 7/8/9 task and are correctly red. The sixteenth is the finding in
section 4.

Fifty-one of the 52 are unambiguously Section 7/8-owned. The `3.11d` set maps one-to-one onto task
`7.11`, `3.11c` diagnostics onto `7.9`, `3.7`/`3.8` onto `7.5`–`7.10`, `3.10` onto `8.2`–`8.10`, and
`3.11b`/`3.9` create-or-observe onto `8.9`.

## 4. Release-blocking finding

### R1 — the Section 6 deadline-versus-fan-out merge race is proven by nothing

`ancestor-terminal-suppresses-merge` (`taskId 3.6`, fixture `structured-fanout-scenarios.json`)
carries `turnsGreenTask: "5.4-6.2"` — a range whose endpoints are both complete — and records **no**
Section 7 or Section 8 dependency. Its assertion is:

> The winning ancestor terminal commit fences work and suppresses `WhenAll` and `WhenAllOutcomes`
> merges without sibling-cancellation semantics.

Its setup requires racing **cancellation, termination, and `CompleteWithin`** against active
branches and items.

**Why this is Section 6 and not deferrable.** The immutable prerequisite approval — the same verdict
that authorized task `6.0` — states at line 162 that of the six carried-forward Section 4/5 fixture
entries, five retain Section 7 package ownership and `ancestor-terminal-suppresses-merge` "retains
its **Section 6** deadline race." At Section 5 exit this scenario was accepted as red *on the
express basis that Section 6 would prove it*. It is a Section 6 deliverable by the frozen authority,
not bookkeeping and not later-section work.

**What the target actually proves.** Nothing:

- **No driver of either kind exists.** `grep` for `Phase0Scenario("ancestor-terminal-suppresses-merge"` across
  `tests/` returns no match. The scenario has no final driver, and because it is excluded from
  `Section6ScenarioIds` it is not run against the current physical owner either. It is the one
  carried-forward Section 6 obligation that no lane executes.
- **The unique leg is untested.** Four passing engine tests cover ancestor-terminal merge
  suppression across termination and cancellation, both join policies, both fan-out forms, and both
  engines. `CompleteWithin` appears **zero** times in any of them (verified by scoped `grep` over
  each `SuppressesMerge` test body in both engines). The deadline-expiry terminal — precisely the
  leg the prerequisite retained for Section 6 — is exercised by no test. The deadline suite covers
  expiry after host replacement, preservation across `ContinueAsNew`, wait-timeout races, coordinate
  commit, and attempt fencing, but never expiry against an active fan-out scope.

**Why the shared-code-path argument does not discharge it.** Deadline expiry does route through the
same terminal machinery: it emits `WorkflowTerminalEvent` with `Status = TimedOut` via the same
`AddAllOwnedCleanupEvents` cleanup used by cancellation and termination, and `TimedOut` is a member
of the `IsTerminal` set that drives merge fencing. I traced this and found no separate path that
could commit a merge after terminalization, so I judge the risk of a live defect low.

But low residual risk is not the exit standard. Request §3.1 makes the affirmative claim that expiry
"suppresses merges and rollover," §4 directs rejection of "any Section 6 scenario that fails for
missing behavior," and the entire guard packet exists so that such claims rest on runtime-recorded
executed drivers rather than on a reviewer's reading of the source. Counting this scenario among 52
later-section reds makes the red count look correct while leaving a required Section 6 proof
unexecuted. That is an evidence gap in a retained Section 6 obligation, and it is release-blocking.

**Remedy.** Implement `ancestor-terminal-suppresses-merge` as a runtime-recorded driver that races
`CompleteWithin` expiry (alongside cancellation and termination) against active root `Parallel`
branches and root `ForEach` items, asserting merge suppression for both `WhenAll` and
`WhenAllOutcomes` with no sibling-cancellation semantics; move its id into `Section6ScenarioIds` so
it executes against the current physical owner; and re-freeze. Expected counts after the fix: 32
Section 6 drivers passing and 61 expected-red failures.

## 5. Findings that are sound

These were independently derived from source and executed evidence, not accepted from the request.
They are recorded so the remedy above is not confused with broader doubt.

**Forged runtime defenses are genuine, not legal controls (question 7).** The ancestry driver writes
a forged `Held` root obligation into a real checkpoint, restarts with a replacement runtime, and
asserts `SFE-RUN-002` **together with** `AvailableCapacity == 1` and `HeldTickets.Count == 0` —
failure strictly before pool mutation. The rollover driver forges a non-quiescent scope and asserts
`SFE-RUN-001` **together with** `ContinueAsNewGeneration == 0` and `OwnedObligations.Count == 1` — no
generation or ownership drift. The legal control is asserted separately in the same scenario.

**Deadlines.** `CompleteWithin` throws `SFE-AUTH-DEADLINE-001` *before* assigning
`deadlineLocation`/`Deadline`, so the first authored deadline and fingerprint survive; the
diagnostic carries the second call as primary and the first as related. "Positive finite" holds
because the only infinite sentinel, `Timeout.InfiniteTimeSpan` (−1 ms), fails the positive check.
Expiry is terminalize-once behind an `IsTerminal` early return.

**Host governance absence claims (question 11).** `StructuredExecutionHostOptions` carries exactly
the path ceiling and `StepThrottles`, validated and copied with no binder API.
`StepExecutionThrottle.For<TStep>` keys on `typeof(TStep)` and requires `IStep<TState>`, so lambdas
are untargetable and duplicate exact types are rejected. No advancement, general-body,
per-definition, category, fail-fast, wait-timeout, or custom governance SPI surface exists. Neither
engine options type contains a toggle, flag, implicit mode, or serializer/codec hook; transient
pools appear only on ephemeral options and resource pools only on durable options, so the split is
structural.

**Reduced portable surface (task 6.10).** `StepResult` exposes only `Completed`, `Failed`, and
`WaitForEvent`; the `Yield`, `ContinueAsNew`, external-job, and `AcquireResources` variants exist
solely as `internal` engine records.

**Provider store behavior (question 8).** Passing certification tests prove FIFO grant-on-release,
all-or-none multi-pool grants, nonpositive/unknown resize rejection before mutation,
creation-definition validation that does not reset current capacity, double-release not
double-crediting, and queued-holder cancellation allocating no ticket. `ExpireTicketsAsync` **marks**
expiry while the ticket remains in `HeldTickets` — capacity is never reclaimed by time — and
shrinking below held count retains held tickets at zero availability, which is the resize-debt
behavior. The "marks or reconciles, never renews or reclaims by time" claim is substantiated.

**Tombstone retention.** Implemented as a persisted phase rather than a named type. After cancelling
a queued holder, the obligation is retained as `CancelledBeforeGrant`, the waiter is removed, no
ticket was allocated, and an unrelated external holder's ticket is untouched.

**Anti-shortcut check.** All 31 Section 6 contracts declare non-empty required product calls and 27
of 31 require a deterministic seam; `ExecuteAsync` plus the `runtimeErrors` assertion forces
execution through guard-owned observations. The infrastructure lane pins the catalog at 95 scenarios
with unique keys and contract/scenario equivalence. No setup or discovery shortcut found.

**Section 7 boundary is honest (question 13).** Only `AddOrcaCoreEphemeralEngine` and
`AddOrcaCoreDurableEngine` were added. Inherited `AddOrcaCore`, `AddOrcaCoreHostedServices`, the
provider registrations, and `AddOrcaCoreOpenTelemetry` all remain, and OpenTelemetry package
references remain confined to `OrcaCore.Hosting` — the assembly task `7.12` requires absent from the
manifest. Engine assemblies emit BCL `System.Diagnostics.Metrics` only. `six-hosting-entry-owners`
and the manifest/project-metadata facts remain red. No package, facade, or hosting cleanup is
credited to Section 6, and catch-all deletion remains task `7.10`. The
`AssertExecutableAgainstCurrentPhysicalAssemblyAsync` substitution is bounded and explicit, relaxing
only assembly-owner identity while preserving the observation and determinism requirements.

## 6. Answers to the independent review questions

1. **Yes.** 480 entries and both hashes reproduced exactly before and after all commands; see the
   concurrent-artifact disclosure in section 1 for post-validation drift.
2. **No — blocking.** Structural, start-relative, durable across restart and rollover, and
   atomically terminal, except that the deadline-versus-active-fan-out merge-suppression race
   retained for Section 6 is proven by no driver and no test (R1).
3. **Yes.** Retry classification, detached attempt copies, `ReplaceState`, honest ordinary late
   overlap, leased no-overlap, and expired replay are covered.
4. **Yes.** The whole coordinate is committed before dispatch and reused across every redispatch
   cause without advancing retry budget.
5. **Yes.** Factory-only immutable request values, exact placements, capability-restricted leased
   builders, fingerprint participation, path-sensitive ancestry.
6. **Yes.** One correlated identity across all phases, atomic grants, serialized
   cancellation/release, quarantine before progression, no time-only reclaim.
7. **Yes.** Genuinely forged checkpoints asserting the error code together with pool non-mutation and
   generation/ownership non-drift.
8. **Yes** at engine and store level; the named `3.11d` certification drivers are legitimately
   deferred to task `7.11`.
9. **Yes.** Ceiling-of-one progresses, limits compose by the lower value, parked items retain node
   slots, restart re-admits only unfinished indexes in order.
10. **Yes.** Exact-type keying, ephemeral-only transient pools, pending cancellation with
    physical-slot retention until the guarded body returns.
11. **Yes.** BCL-only instruments; no advancement, general-body, fail-fast, wait-timeout, or custom
    SPI claims added.
12. **No.** 51 of 52 reds are genuinely Section 7/8-owned; `ancestor-terminal-suppresses-merge` is a
    retained Section 6 obligation hidden among them (R1).
13. **Yes.** Verified against the actual hosting surface.
14. **No.** R1 is a release blocker, so task `7.0` must not begin.

## 7. Structural observation (not the blocker)

Section membership is three hardcoded id lists (`Section4/5/6ScenarioIds`), and the expected-red set
is computed by *exclusion* from them. Because all 52 reds fail at driver-existence before any
behavior assertion, a scenario that is genuinely current-section work but omitted from its list fails
**identically** to a legitimately deferred Section 7/8 scenario. The lane cannot self-distinguish the
two cases, which is exactly how R1 survived to exit review: the red count and every red name matched
the request precisely, and the misfiling was invisible at the lane level.

Recommended alongside the R1 remedy: derive section membership from the fixtures' existing
`turnsGreenTask` field instead of a parallel hardcoded list, and add an infrastructure guard
asserting that no scenario whose `turnsGreenTask` tasks are all complete remains in the expected-red
set. That makes the packet self-certifying and prevents recurrence.

## 8. Scope of this verdict

Section 6 does not exit and **task `7.0` remains blocked**. The rejection rests on R1 alone;
no other Section 6 defect was found, and the reproduced evidence for every other claim was accurate
and honest.

Approval should be sought again on a new frozen target after the R1 remedy in section 4 lands, with
32 Section 6 drivers passing and 61 expected-red failures. The Docker-backed PostgreSQL and SQL
Server provider suites remain unexecuted: the Docker CLI is present (29.6.1) but `docker ps` fails
with `open //./pipe/dockerDesktopLinuxEngine: The system cannot find the file specified` and the
named pipe does not exist. **I independently reproduce the reported blocker** — the request's
characterization is accurate, and this is an environment limitation, not passing product evidence.
It does not bear on R1 and should be discharged before any release shipping the PostgreSQL
production role set under tasks `7.10`/`7.11`.

No reviewed source, test, task, spec, plan, document, manifest, request, or existing review artifact
was modified during this review. This file is the sole addition made by this reviewer.
