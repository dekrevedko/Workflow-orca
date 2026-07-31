# Developer-facing interface — Section 6: Docker-backed provider gate, superseding verdict

**Date:** 2026-07-29
**Reviewer role:** independent exit re-reviewer (Section 6 exit and authorization of task `7.0`)
**Target:** the 484-entry frozen target of
`developer-facing-interface-section-06-ancestor-terminal-remediation-rereview-request-2026-07-29.md`
**Supersedes:** `developer-facing-interface-section-06-ancestor-terminal-remediation-independent-rereview-verdict-2026-07-29.md` (APPROVE)

## Verdict

**REJECT — this verdict supersedes my earlier APPROVE on the same target.**

Section 6 does not exit. **Task `7.0` is blocked again.**

My earlier verdict approved this exact target while recording that the Docker-backed PostgreSQL and
SQL Server provider suites could not be executed, and it flagged that limitation as something to
discharge. A Docker engine subsequently became available, I ran those suites, and they fail
deterministically on three shared resource-governance certification contracts in **both** relational
providers. Three of those failing behaviors are ones my earlier verdict cited as evidence that the
Section 6 lease-governance claims were substantiated. That citation was valid only for the in-memory
store, and I should not have generalized it to "provider stores" while the relational evidence was
missing.

Nothing in the prior remediation is withdrawn: the R1 ancestor-terminal fix remains correct and
complete, and every non-Docker lane still reproduces exactly. The blocker is new evidence, not a
reversal of the earlier findings.

## 1. Provenance

`HEAD` `d76192f089dd07f68e310c21fe4e5a38dd93cf7f`, tree
`2264e670493ecc76359d42ee5273028eb287a566`, branch `feature/v3-rebuild`, 484 frozen entries.
I re-verified after every command in this review, including all container runs: the frozen 484-entry
target is **byte-identical** to
`developer-facing-interface-section-06-ancestor-terminal-remediation-rereview-dirty-manifest-2026-07-29.txt`,
with both hashes unchanged. The Testcontainers runs left no artifact in the target. This file is the
only addition.

Environment: Docker Desktop engine reachable, server 29.6.1, `linux/x86_64`. The blocker recorded in
both earlier verdicts is therefore discharged as an *environment* matter — and executing the gate is
what produced the finding below.

## 2. R1 remediation — still approved

Re-confirmed on this target and unchanged from my prior verdict: ownership is derived from
`turnsGreenTask` by maximum section with no hardcoded lists; derived Section 6 is exactly the prior 31
plus `ancestor-terminal-suppresses-merge` with nothing dropped; the fixture entry is unchanged; the
driver exists, executes in 0.43 s, and covers eight `CompleteWithin` schedules across two engines,
two fan-out shapes, and two joins, with a two-active-children precondition, two independent
regression detectors, committed-state verification, `due.Count == 1`, and replacement-host re-drive.

Non-Docker lanes all reproduce exactly: build 0/0; Core 464; Ephemeral 173; Durable 332; Hosting 17;
Acceptance 70; in-memory provider certification 78; infrastructure 104/104 ×3; Section 6 drivers
32/32; expected-red exactly 61 with zero passes; compile fixtures green with 0 authoring gaps; 8
named package reds; OpenSpec 17/17; audit clean over 32 projects; `git diff --check` clean; tasks
90/46/136 and 16/0/16; task `7.0` open.

## 3. Release-blocking finding

### R2 — both relational providers fail the resource-governance certification contract

| Suite | Result |
|---|---|
| `OrcaCore.Providers.PostgreSql.Tests` | **77 total, 3 failed**, 0 skipped (137 s; reproduced 124 s) |
| `OrcaCore.Providers.SqlServer.Tests` | **63 total, 3 failed**, 0 skipped (273 s) |

The same three tests fail in both providers, with identical messages, deterministically across
repeated runs:

| Certification test | Expected | Observed |
|---|---|---|
| `ResizePoolAsync_RejectsNonPositiveAndUnknownBeforeMutation` | `ArgumentOutOfRangeException` on resize-to-0; `ResourcePoolNotConfiguredException` on unknown pool | **no exception thrown** |
| `UpsertPoolAsync_AfterResize_ValidatesCreationDefinitionWithoutResettingCurrentCapacity` | capacity stays at the resized value `1` | **capacity reverted to `3`** |
| `ReleaseAsync_QueuedHolder_CancelsWaiterWithoutAllocatingTickets` | `QueuedWaiters` empty | **waiter still queued** |

**This is provider implementation drift, not a test defect.** All six failures resolve to the *same
shared* certification source, `tests/OrcaCore.ProviderCertification/ResourcePoolStoreCertificationTests.cs`
(lines 224 and 249), which passes 78/78 against `InMemoryResourcePoolStore`. The contract is
identical; only the relational implementations fail it.

I traced each defect to its root cause, and they are the same in both providers:

1. **Nonpositive resize accepted.** `PostgreSqlResourcePoolStore.ResizePoolAsync` (line 299) and
   `SqlServerResourcePoolStore` (line 207) both use
   `ArgumentOutOfRangeException.ThrowIfNegative(capacity)`, which permits **zero**. The in-memory
   reference uses `ThrowIfLessThan(capacity, 1)`.
2. **Unknown pool silently no-ops.** Both issue a bare `update … where pool_name = @pool_name`,
   affecting zero rows and throwing nothing. Neither file contains
   `ResourcePoolNotConfiguredException` at all; the in-memory reference throws it.
3. **Upsert destroys operator resizes.** Both upserts do
   `on conflict … do update set capacity = excluded.capacity`, unconditionally overwriting current
   capacity with the creation definition's value. The PostgreSQL table
   (`orcacore_resource_pools`: `pool_name`, `capacity`, `lease_duration_seconds`) holds a **single
   capacity column** and stores no creation definition, so it structurally cannot preserve current
   capacity across upsert or detect creation-definition disagreement. The in-memory reference models
   `CurrentCapacity` separately.

**Why this blocks Section 6 rather than deferring to Section 7.** These are not hosting-registration
concerns. They are the resource-governance store contract, and Section 6 claims them directly:

- Task 6.9 implements "positive-only resize, creation-versus-current capacity startup agreement, and
  resize-debt recomputation" and "grant/cancel serialization with zero-ticket queued cancellation and
  exact pre-activation compensation".
- Task 6.11 requires turning the "conservation, resize-bound/conflict/debt, and provider-fence
  suites green" — not green for one in-memory provider only.
- Request §3.4 asserts that "direct waiter transfer, positive resize, creation/current-capacity
  agreement, resize debt, stale confirmation rejection, and exact conservation are covered."
- PostgreSQL is the designated production provider.

The request itself stated that the unexecuted Docker suites were "an environment limitation, not
passing product evidence" and invited the reviewer to run them where an engine exists. Run, they
fail. Under the request's own framing that is a release blocker.

**Operational severity.** Defect 3 is the most serious: any host that re-upserts its pool catalog at
startup silently reverts every operator resize, so a deliberate production capacity change is undone
by a restart. Defect 2 leaks a cancelled queued waiter, which contradicts the conservation and
zero-ticket-cancellation properties Section 6 claims. Defect 1 permits a zero-capacity pool via
resize and hides typos in pool names.

**Remedy.** Bring both relational stores up to the shared contract: reject nonpositive capacity;
throw `ResourcePoolNotConfiguredException` for unknown pools before mutation; and separate the
persisted creation definition from current capacity — which requires a schema migration adding a
creation-capacity column, upsert logic that preserves current capacity and rejects creation-definition
disagreement with `InvalidOperationException`, and waiter removal on queued-holder release. Then
re-run both Docker suites to 77/0 and 63/0 and re-freeze.

## 4. Secondary finding — not the blocker, but needs disposition

### R3 — the integration gate has drifted from its documented baseline

`dotnet test tests/OrcaCore.Integration.Tests` reports **4 failed, 110 passed, 1 skipped, 115 total**
(reproduced twice). `CLAUDE.md` documents the expected baseline as **107 passed, 1 skipped** with no
failures, so both the total and the pass/fail shape have drifted.

Failures:

- `JobSchedulerStackIntegrationTests.INT_JS_004_ExternalJob_WaitSurvivesProcessorRestart`
- `JobSchedulerStackIntegrationTests.INT_JS_014_PauseRunWithInFlightJobs_OnPostgreSql`
- `MultiNodePostgreSqlIntegrationTests.INT_MN_009_StartOrGetFromTwoHosts_BlockedUntilPgIdempotencyStore`
- `HostingPostgreSqlIntegrationTests.INT_HO_011_FakeTimeProviderDrivesHostedIntervals`

I characterize these as **stale legacy-surface tests rather than Section 6 public-contract
regressions**, and I checked rather than assumed: `INT_JS_004` drives `RunExternalJob` /
`CompleteExternalJob` as raw commands through the internal `DurableCommandProcessor`. External jobs
and pause/resume are surfaces task 4.10 deleted from the public API and task 1.4 recorded as
deferred; they survive only as internal engine records. `INT_MN_009` is self-labelled
`BlockedUntilPgIdempotencyStore`.

They are consequently owned by the Section 9/10 reconciliation tasks (9.6, 9.9, 10.5, 10.8), not by
Section 6, and I do not treat them as the exit blocker. They still require explicit disposition —
repaired, removed, or skipped with a recorded blocker per the `CLAUDE.md` rule against unskipping or
deleting skipped tests without a verified blocker — and the documented baseline in `CLAUDE.md` should
be corrected to whatever the post-refactor truth is. Leaving four red integration tests
undocumented invites exactly the "expected red is fine" habit that produced R1.

## 5. Correction to my prior verdict

My prior verdict's section 4 stated, under question 8, that passing certification tests prove
"nonpositive/unknown resize rejection before mutation, creation-definition validation that does not
reset current capacity, … and queued-holder cancellation allocating no ticket." That statement was
true of `InMemoryResourcePoolStore` and **false of both relational providers**. I verified it against
the in-memory lane and generalized it to "provider stores" without the relational evidence, having
myself recorded that the relational evidence was missing. Answer 8 in that verdict should read **No**
for the relational providers.

The prior verdict remains in the record unedited, as required. This file supersedes it.

## 6. Scope

Section 6 does not exit; **task `7.0` is blocked**. R2 is the release blocker; R3 requires disposition
but does not by itself gate Section 6. Approval should be sought again on a new frozen target once R2
is fixed, with both Docker-backed relational suites green and R3 explicitly dispositioned. Because a
Docker engine is now available, the Docker-backed suites should be treated as a standing gate for
every future freeze, not an optional lane — this finding existed at the prior two freezes and was
invisible only because the engine was down.

No reviewed source, test, task, spec, plan, document, manifest, request, or existing review artifact
was modified during this review. This file is the sole addition.
