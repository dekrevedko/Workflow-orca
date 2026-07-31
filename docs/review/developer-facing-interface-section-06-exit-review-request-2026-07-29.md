# Section 6 deadlines, operations, leasing, and governance: independent exit review request

**Date:** 2026-07-29  
**Requested verdict:** `APPROVE` or `REJECT`  
**Authorization requested:** Section 6 exit and the later start of task `7.0`

This file is an evidence request, not an approval. Section 6 source work and the coordinated
`add-runtime-concurrency-limits` change are implemented. Task `7.0` remains open, and Section 7
must not begin unless an independent reviewer approves this exact frozen target without a release
blocker.

## 1. Authority and entry gate

Review the target in this order:

1. `docs/specs/17-selected-mode-capability-matrix.md`;
2. `docs/specs/17-public-authoring-contract.cs`;
3. canonical requirements under `openspec/specs/`;
4. `openspec/changes/reshape-developer-facing-interfaces/tasks.md`;
5. `openspec/changes/add-runtime-concurrency-limits/`;
6. Decision 17 in `openspec/changes/reshape-developer-facing-interfaces/design.md`;
7. the phased implementation plan and immutable review history.

The prerequisite is the immutable
`developer-facing-interface-section-04-05-amendment-remediation-independent-rereview-verdict-2026-07-29.md`.
It approved the exact Section 4/5 target and authorized task `6.0`. It did not approve Section 6
exit.

Canonical specifications control. Prior requests, manifests, approvals, and rejections are
immutable evidence and were not rewritten.

## 2. Frozen target provenance

| Item | Value |
|---|---|
| Repository | `X:\Projects\GitHub\Workflow-orca` |
| Branch | `feature/v3-rebuild` |
| Baseline checkpoint | `8c2dd712284f3b638f9bf812ad2172f24d0a8863` |
| `HEAD` | `d76192f089dd07f68e310c21fe4e5a38dd93cf7f` |
| `HEAD` tree | `2264e670493ecc76359d42ee5273028eb287a566` |
| Baseline relationship | baseline is an ancestor of `HEAD` |
| Review target | `HEAD` plus every entry in the self-inclusive Section 6 manifest |
| Self-inclusive porcelain entries | 480 |
| Entry classes | 347 modified / 9 deleted / 124 untracked |
| Raw-manifest SHA-256 | `1F0667403678F12870A51DA2F5218B389959B8CCF00E0A2BD5DBA46574CEAAFC` |
| LF-normalized sorted-status SHA-256 | `C106305B04F733C5A9A2765D64074800026A3DBF847B20465BB28FFB5E6B21D7` |
| Reshape tasks | 90 complete / 46 pending / 136 total; 0 duplicate IDs |
| Runtime-governance tasks | 16 complete / 0 pending / 16 total; 0 duplicate IDs |

The authoritative self-inclusive manifest is
`developer-facing-interface-section-06-exit-review-dirty-manifest-2026-07-29.txt`. Reproduce it
before reading conclusions and again after every validation command. Any path/status drift
invalidates the review.

## 3. Section 6 implementation claims

### 3.1 Deadlines and structural waits

- `CompleteWithin` accepts one positive finite start-relative duration, rejects a second call
  eagerly with `SFE-AUTH-DEADLINE-001`, and keeps the first authored deadline and fingerprint.
- The absolute workflow deadline is computed from the original start, checkpointed, and retained
  across admission, retries, delays, waits, lease queueing, restart, and `ContinueAsNew`.
- Expiry terminalizes once as `TimedOut`, suppresses merges and rollover, and cannot be reset by a
  replacement host.
- `Wait` timeout is a structural event/timer race. Event, timer, cancellation, and terminal
  completion consume or fence the losing obligation without leaving an active timer or wait.
- `WithStepTimeout` decorates only the preceding business step. Attempt state is detached;
  `ReplaceState` commits only from the winning attempt.

### 3.2 Retry classification, operation identity, and fencing

- Retry eligibility excludes cancellation, workflow deadline, lease loss, builder/type/capability
  failures, and other terminal contract errors.
- Before first dispatch the durable checkpoint contains the stable `StepOperationId`, positive
  retry-policy attempt number, optional absolute attempt deadline, and in-flight marker.
- Crash, lost response, conflict, replay, and host replacement redispatch the same full coordinate
  without consuming retry budget. Only a committed eligible failure or timeout advances
  `AttemptNumber`.
- Expired-at-replay attempts are not physically redispatched. Loop iterations, branch occurrences,
  item indexes, and `ContinueAsNew` generations produce distinct operation identities.
- A timed-out ordinary token-ignoring body loses commit authority and its logical path token but
  keeps its physical exact-step/transient slot until the CLR body returns. A leased in-process
  prior body must return before retry; host-loss recovery retains the same lease obligation and
  operation coordinate.

### 3.3 Lease values, authoring, and compilation

- `ResourceLeaseRequirement.Require` and `ResourceLeaseRequest.Create` are factory-only immutable
  values. Requests are nonempty, copied, duplicate-free, and contain no author duration, holder,
  renewal, or mutation bypass.
- Static and selector `AcquireResources(request, body)` are available only at the approved durable
  root, root-`If`/root-`While` nested, root-`Parallel` branch, and root-`ForEach` item placements
  when no live capacity ancestor exists.
- Dedicated leased builders omit `Parallel`, `ForEach`, `While`, another acquisition, and
  `ContinueAsNew`; acquisition lowers directly into the structured plan and affects the
  structural fingerprint.
- Compiler ancestry is path-sensitive: mutually exclusive arms, siblings, sequential scopes, and
  one fully exited scope per root-loop iteration remain legal.

### 3.4 Durable lease lifecycle and recovery

- One exact obligation/protection identity spans request, queued admission, provider tickets,
  activation, step attempts, release, review, quarantine, reconciliation, and stop proof.
- The persisted lifecycle is `Queued -> PendingCommit -> Held -> ReviewMarked -> AmbiguousHeld ->
  Quarantined -> Released`, including only the specified safe skipped transitions.
- Whole-request grants are atomic. Only the requesting fiber parks. Queued cancellation creates no
  ticket; cancellation at later handoff barriers compensates exactly once.
- Release commits before parent, loop, join, or terminal progression. Ambiguous protected work
  retains capacity across retry and transfers atomically to quarantine before progression.
- `LeaseProtectionToken`, `StopConfirmationId`, and `IDurableResourceLeaseRecovery` implement the
  exhaustive confirmation statuses, idempotent bindings, confirmation precedence, serialized
  normal-release races, and retained tombstones.
- Review time marks or reconciles; it never renews or reclaims by time. Missing provider tickets
  become `LeaseLost`. Direct waiter transfer, positive resize, creation/current-capacity
  agreement, resize debt, stale confirmation rejection, and exact conservation are covered.
- Runtime defenses execute actual forged checkpoints: live ancestor acquisition fails with
  `SFE-RUN-002` before pool mutation; non-quiescent rollover fails with `SFE-RUN-001` without
  changing generation or owned obligations.

### 3.5 Host governance and observability

- `StructuredExecutionHostOptions` carries only the host-owned path ceiling and exact named-step
  throttles. Exact `Then<TStep>()` types are case/type identity keys; lambda, base, assignable,
  category, per-definition, global-body, and advancement throttles are absent.
- One execution-path token model covers runnable roots, branches, and items. Parking and joins
  release tokens; fan-out parents release before child scheduling and reacquire only for merge or
  continuation. A ceiling of one makes progress without parent-held deadlock.
- Node `ForEachOptions.MaxConcurrency` and the host path ceiling compose by their lower value;
  admitted nonterminal parked items retain node slots while releasing path tokens.
- Durable host slots are not persisted. Replacement hosts re-evaluate exact blocked owners and
  re-admit unfinished indexes in order.
- `WithTransientPool(TransientPoolName)` remains ephemeral-only, exact, case-sensitive, shared
  across instances, and checked as one complete copied host-compatibility set before registry
  mutation. Durable shared capacity uses scoped resource leases.
- Pending admission is cancelled by termination, but a granted physical slot is retained until a
  token-ignoring body actually returns.
- Stable BCL metrics/debug snapshots cover configured limits, active slots, wait depth,
  cancellation, transient-pool identity, and host-compatibility failures without treating
  saturation as rejection or mixing transient pools with durable tickets.

### 3.6 Boundary retained for Section 7

The current physical hosting assembly now has the role-specific engine options and registration
path required to prove Section 6 governance. The inherited `AddOrcaCore` surface and current
package ownership are deliberately not deleted or relocated here: Decision 17 maps repository
tiers, exact assembly ownership, catch-all deletion, final facades, management, and provider
packages to Section 7, specifically task `7.10`.

Review this boundary for accidental new catch-all/toggle work, but do not treat the inherited
surface as a hidden Section 6 completion claim. The final owner/package guards must remain red
until Section 7.

## 4. Guard disposition

The fixture catalog contains 95 named behavior scenarios:

- 4 Section 4 drivers pass;
- 8 Section 5 drivers pass;
- 31 Section 6 drivers pass against the current physical owner;
- 52 scenarios remain ExpectedRed for Sections 7 and 8.

Ten additional product/package facts remain ExpectedRed, producing exactly 62 named failures and
zero passes. The non-scenario facts cover the exact package graph, public tier edges, project
metadata, friend assemblies, failure owners, common facade/hosting, clean application journey,
provider-author packages, and DAG assemblies. The separate package compile lane reports the eight
expected-red consumer roles.

Reject any Section 6 scenario that fails for missing behavior, any unexpected green later-section
fact, any setup/discovery shortcut, or any expected-red count/name drift.

## 5. Reproduced owner-run evidence

| Lane | Result |
|---|---:|
| Solution build | succeeded; 0 warnings / 0 errors |
| Core | 464 passed / 0 failed / 0 skipped |
| Ephemeral | 173 passed / 0 failed / 0 skipped |
| Durable | 332 passed / 0 failed / 0 skipped |
| Hosting | 17 passed / 0 failed / 0 skipped |
| Acceptance | 70 passed / 0 failed / 0 skipped |
| Provider certification | 78 passed / 0 failed / 0 skipped |
| Infrastructure guards | 103 passed / 0 failed / 0 skipped on three consecutive isolated runs |
| Section 6 current-physical drivers | 31 passed / 0 failed |
| Expected-red guards | 0 passed / 62 intentional named failures / 0 skipped |
| Green compile fixtures | passed; fresh source package, exact/product consumers, 26 source and 26 package forbidden-call diagnostics, incomplete control rejection |
| Product-authoring ExpectedRed compile set | passed with 0 remaining gaps |
| Package ExpectedRed compile set | nonzero as designed; exactly 8 named Section 7/8 package/application gaps |
| Strict OpenSpec | both active changes valid; all 17 items passed |
| Task accounting | reshape 90/46/136, coordinated 16/0/16, no duplicate IDs |
| NuGet vulnerability audit | no vulnerable packages in all 32 solution projects |
| Whitespace | `git diff --check` exit 0; line-ending notices only |

Docker-backed PostgreSQL and SQL Server provider suites could not be executed because the local
Docker Desktop Linux engine named pipe does not exist. This is an environment limitation, not
passing product evidence. The provider certification suite is green 78/78; the independent
reviewer should run the Docker-backed suites if a Docker engine is available.

## 6. Minimum independent commands

Run from `X:\Projects\GitHub\Workflow-orca`:

```powershell
dotnet build OrcaCore.slnx --no-restore -p:NuGetAudit=false -v minimal

.\tests\OrcaCore.Core.Tests\bin\Debug\net10.0\OrcaCore.Core.Tests.exe -noColor
.\tests\OrcaCore.Engine.Ephemeral.Tests\bin\Debug\net10.0\OrcaCore.Engine.Ephemeral.Tests.exe -noColor
.\tests\OrcaCore.Engine.Durable.Tests\bin\Debug\net10.0\OrcaCore.Engine.Durable.Tests.exe -noColor
.\tests\OrcaCore.Hosting.Tests\bin\Debug\net10.0\OrcaCore.Hosting.Tests.exe -noColor
.\tests\OrcaCore.Acceptance.Tests\bin\Debug\net10.0\OrcaCore.Acceptance.Tests.exe -noColor
.\tests\OrcaCore.ProviderCertification\bin\Debug\net10.0\OrcaCore.ProviderCertification.exe -noColor

.\tests\OrcaCore.DeveloperSurface.Guards\bin\Debug\net10.0\OrcaCore.DeveloperSurface.Guards.exe -trait "Disposition=Infrastructure" -noColor
.\tests\OrcaCore.DeveloperSurface.Guards\bin\Debug\net10.0\OrcaCore.DeveloperSurface.Guards.exe -trait "Disposition=ExpectedRed" -noColor
.\tests\OrcaCore.DeveloperSurface.Guards\bin\Debug\net10.0\OrcaCore.DeveloperSurface.Guards.exe -method "*Section6Scenario_CurrentPhysicalOwnerHasOneRuntimeRecordedExactDriverAndPassingAssertion*" -noColor

powershell.exe -NoProfile -ExecutionPolicy Bypass -File tests/OrcaCore.DeveloperSurface.Guards/run-compile-fixtures.ps1 -Disposition Green
powershell.exe -NoProfile -ExecutionPolicy Bypass -File tests/OrcaCore.DeveloperSurface.Guards/run-compile-fixtures.ps1 -Disposition ExpectedRed
powershell.exe -NoProfile -ExecutionPolicy Bypass -File tests/OrcaCore.DeveloperSurface.Guards/run-package-fixtures.ps1 -Disposition ExpectedRed

openspec.cmd validate reshape-developer-facing-interfaces --strict
openspec.cmd validate add-runtime-concurrency-limits --strict
openspec.cmd validate --all --strict --no-interactive
dotnet list OrcaCore.slnx package --vulnerable --include-transitive --no-restore
git diff --check
```

The ExpectedRed guard and package commands must return nonzero with exactly the named intentional
failures. A nonzero exit alone is not evidence; compare the complete names and meanings. Run the
infrastructure lane three consecutive times to check the compile-fixture lock regression.

## 7. Independent review questions

1. Does the 480-entry self-inclusive manifest reproduce exactly before validation and again after
   all commands, including both hashes and all entry classes?
2. Are workflow/wait/attempt deadlines structural, start-relative where required, durable across
   restart and rollover, and atomically terminal without merge or losing obligations?
3. Is retry classification exact, and do attempt copies, state replacement, cancellation,
   ordinary late overlap, leased no-overlap, and expired replay behave as claimed?
4. Is the full operation/attempt/deadline/in-flight coordinate committed before dispatch and
   reused across every redispatch cause without incorrectly advancing retry budget?
5. Are lease request values immutable and closed, placements exact, leased builders capability
   restricted, fingerprinting complete, and compiler ancestry path-sensitive?
6. Does every lease phase preserve one correlated obligation/protection/ticket identity with
   atomic grant, serialized cancellation/release, quarantine before progression, and no
   time-only reclaim?
7. Do the forged current-physical drivers genuinely produce `SFE-RUN-002` before pool mutation
   and `SFE-RUN-001` without generation/ownership drift, rather than passing a legal control?
8. Do provider stores and certification prove conservation, direct waiter transfer, creation
   agreement, review marks, missing-ticket loss, confirmation precedence, tombstones, resize
   conflict/debt, and stale-fence rejection?
9. Does the exact execution-path model avoid ceiling-one deadlock, compose host/node limits, keep
   parked admitted-item slots, and re-admit only unfinished work after restart?
10. Are exact step throttles and ephemeral transient pools keyed and scoped exactly, with pending
    cancellation but physical-slot retention until the guarded body returns?
11. Are metrics/debug counters and operator documentation complete without adding advancement,
    general-body, fail-fast, wait-timeout, or custom governance SPI claims?
12. Are all 62 ExpectedRed failures and all eight package reds genuinely owned by Sections 7/8,
    with no Section 6 behavior or evidence gap hidden among them?
13. Does the Section 7 boundary remain honest: no package/facade cleanup is credited now, task
    `7.0` is open, and the inherited catch-all deletion remains task `7.10`?
14. Is Section 6 free of release blockers, so and only so may task `7.0` begin?

## 8. Verdict instructions

Write exactly one new dated immutable verdict under `docs/review/`. Record provenance, manifest
comparison before and after, every command/result, independently derived findings, environment
limitations, and `APPROVE` or `REJECT`.

Do not edit reviewed source, tests, tasks, specs, plans, documentation, manifests, requests, or
existing review artifacts. Approval authorizes the implementation owner to begin task `7.0` in a
later turn; it does not mark task `7.0` complete. A rejection must keep Section 7 blocked.
