# Section 7 checkpoint-rule refreeze: independent exit-review verdict

**Date:** 2026-07-30  
**Verdict:** `REJECT`  
**Gate effect:** Section 7 exit is not approved. No checkpoint commit is authorized by this
verdict, and task `8.0` remains blocked and unchecked.

## 1. Decision

The build, active test lanes, package graph, package/compile fixtures, OpenSpec validation, and
680-path administrative refreeze are reproducible. The checkpoint rule added to `CLAUDE.md` is
also coherent: review precedes commit, the approved target must be committed before later large
work, and review provenance must not be changed while approval is pending.

Approval is nevertheless blocked by three first-release contract failures:

1. durable start idempotency cannot preserve or compare the required definition fingerprint and
   fixed-codec input fingerprint across restart;
2. durable accepted-event deduplication cannot preserve the target instance or normalized
   envelope required for restart-safe `Duplicate` versus `EventConflict`; and
3. public cancellation/termination results do not implement the specified cooperative,
   idempotent, race-safe lifecycle.

The 707 retired white-box cases were not counted as passing tests. Their proposed replacement
evidence is not sufficient because the surviving current-facing suite omits the exact supported
durable and lifecycle cases above.

No reviewed source, test, task, specification, plan, request, manifest, or prior review artifact
was edited. This verdict is the only file added by the review.

## 2. Frozen target and provenance

The authoritative target is the administrative refreeze, which supersedes the earlier 678-path
request only for the checkpoint-rule amendment.

| Item | Independently observed |
|---|---|
| Repository | `X:\Projects\GitHub\Workflow-orca` |
| Branch | `feature/v3-rebuild` |
| Baseline | `8c2dd712284f3b638f9bf812ad2172f24d0a8863` |
| `HEAD` | `d76192f089dd07f68e310c21fe4e5a38dd93cf7f` |
| `HEAD` tree | `2264e670493ecc76359d42ee5273028eb287a566` |
| Baseline relationship | baseline is an ancestor of `HEAD` |
| Frozen manifest entries | 680 |
| Entry classes | 396 modified / 56 deleted / 228 untracked |
| Raw manifest SHA-256 | `6042C0E60FC968A18B33EC3976CE300DC03BBA4A50DF88D5E80F087CACC85348` |
| Sorted LF status SHA-256 | `8EC355121B84680C6002427415F9D571F5D6D29A78B21BD4B9884337BF0966E2` |

Before source review and again after validation, `git status --porcelain=v1 -uall` reproduced all
680 ordered entries in
`developer-facing-interface-section-07-checkpoint-rule-refreeze-dirty-manifest-2026-07-30.txt`
with zero differences. The two-entry difference from the original 678-path freeze is exactly the
administrative-refreeze request and its self-inclusive manifest.

## 3. Blocking findings

### P1 — Durable start conflicts are not restart-safe

The normative contract binds a provider-global `StartIdempotencyKey` to definition identity,
version, structural fingerprint, and deterministic fixed-codec input bytes
(`docs/specs/17-selected-mode-capability-matrix.md:2483-2485`,
`docs/specs/12-acceptance-criteria.md:262-267`).

The persisted provider contracts retain only key, instance, definition ID, and definition version:

- `src/OrcaCore.Provider.Abstractions/ProviderCommitContracts.cs:86-93`;
- `src/OrcaCore.Provider.Abstractions/ProviderPorts.cs:53-70`; and
- `src/OrcaCore.Providers.PostgreSql/Migrations/001_initial.sql:37-42`.

`DurableStartService.TryGetDurableExistingAsync` consequently compares only definition ID/version
(`DurableStartService.cs:76-97`). The facade's complete `StartBinding` exists only in its process
dictionary (`DurableWorkflowFacade.cs:46-50,245-304`). After host restart, reuse with changed input
or a changed fingerprint under the same ID/version is accepted as the prior instance instead of a
closed `WorkflowStartResult.Conflict`. Reuse with another ID/version escapes through the legacy
version exception rather than returning the required typed conflict.

This can return an existing instance through a handle constructed from attempted, incompatible
facts. The passing PostgreSQL AC-311 test proves only that the reduced four-field record survives
restart; it cannot prove the specified binding.

### P1 — Durable event dedup cannot return restart-safe duplicate/conflict results

The accepted-event contract is per target instance and requires identical normalized bytes to
return `Duplicate` and changed bytes under the same target/event ID to return `EventConflict`,
including after restart (`openspec/specs/durable-runtime/spec.md:154-177`,
`docs/specs/05-requirements-events-waits-timers.md:100-106`).

The durable facade stores `(InstanceId, EventId) -> fingerprint` only in a process-local dictionary
(`DurableWorkflowFacade.cs:654-662`). Its provider port and PostgreSQL table persist only global
`EventId -> InboxRecordState`:

- `src/OrcaCore.Provider.Abstractions/ProviderPorts.cs:42-47`;
- `src/OrcaCore.Provider.Abstractions/ProviderCommitContracts.cs:431-436`; and
- `src/OrcaCore.Providers.PostgreSql/Migrations/001_initial.sql:32-35`.

After restart, `DeliverToInstanceCoreAsync` checks terminal/no-active-wait before the reduced inbox
record and, if it reaches that record, classifies every prior event as `Duplicate` without comparing
target or envelope (`DurableWorkflowFacade.cs:761-795`). It therefore cannot:

- distinguish changed bytes from identical bytes;
- preserve per-target identity;
- classify a consumed event after its wait disappeared; or
- preserve correlation-route dedup after the original target is no longer routable.

The Section 7 public dedup/conflict driver uses `EphemeralServices` only
(`ApplicationJourneyScenarioHost.cs:74-150`). The PostgreSQL restart test checks only the reduced
inbox state, not public facade results, target identity, or envelope conflict.

### P1 — Public terminal operations violate their exact lifecycle contract

The contract requires a live cancellation to return `Requested`, a repeat to return
`AlreadyRequested`, a terminal call to return `AlreadyTerminal`, and a live instance to pass
through `CancellationRequested` while work remains
(`docs/specs/04-requirements-core-runtime.md:253-268`,
`docs/specs/12-acceptance-criteria.md:342-347`).

Both facades bypass that state:

- durable `RequestCancellationAsync` pre-reads status, invokes the legacy terminal
  `CancelAsync`, ignores the command result, and always returns `Requested`
  (`DurableWorkflowFacade.cs:431-446`);
- ephemeral cancellation tests `IsTerminal` before its `Cancelled` check, making
  `AlreadyRequested` unreachable, then immediately calls terminal cancellation
  (`EphemeralWorkflowFacade.cs:420-450`); and
- neither status mapper maps any runtime state to public `CancellationRequested`
  (`DurableWorkflowFacade.cs:609-630`,
  `EphemeralWorkflowFacade.cs:551-571`).

Termination has the same stale-pre-read problem: if another terminal transition wins between the
read and command, the durable facade ignores the no-op/conflict result and still reports
`Terminated`. An independent scan of the current test sources found zero calls to the public
`WorkflowInstanceHandle.RequestCancellationAsync`, so the reduced suite cannot detect this.

### P2 — The stated task accounting omits four real task IDs

The substantive request reports reshape accounting as `102/30/132`. Counting every checked task
identifier in the active ledger, including `3.11a` through `3.11d`, yields
`106 complete / 30 pending / 136 total`, with no duplicate IDs. The runtime-governance ledger is
correct at `16/0/16`.

This arithmetic defect is not the reason for rejection, but the next request should report the
complete ledger rather than silently excluding letter-suffixed tasks.

## 4. Explicit 707-test retirement judgment

The six active project files contain exactly the disclosed 128 `Compile Remove` entries:

| Test project | Retired files |
|---|---:|
| `OrcaCore.Core.Tests` | 22 |
| `OrcaCore.Engine.Ephemeral.Tests` | 28 |
| `OrcaCore.Engine.Durable.Tests` | 52 |
| `OrcaCore.Acceptance.Tests` | 21 |
| `OrcaCore.Hosting.Tests` | 4 |
| `OrcaCore.Providers.PostgreSql.Tests` | 1 |
| **Total** | **128** |

I treated the recorded 707 prior cases as retired and gave them no passing credit. The retirement
is rejected as sufficiently replaced for this target:

- the public cancellation operation has no direct test call;
- public dedup/conflict coverage is ephemeral-only;
- durable start/dedup restart tests certify reduced provider records instead of the full
  application contract; and
- five whole test projects are also absent from the active solution. Those contain 146
  `[Fact]`/`[Theory]` declarations, including 115 in the former integration project, and are not
  part of the 707 per-file exclusion table.

Removing test-only friends and obsolete package tests is defensible in a greenfield package
reshape. It does not justify retiring current supported durable facade and lifecycle regressions.
Fresh regressions for the three P1 findings are required before the retirement can be accepted.

## 5. Independent validation

| Lane / command | Independent result |
|---|---:|
| Release solution build, no incremental | succeeded; 0 warnings / 0 errors |
| Core | 267 passed / 0 failed / 0 skipped |
| Ephemeral | 2 passed / 0 failed / 0 skipped |
| Durable | 35 passed / 0 failed / 0 skipped |
| Hosting | 1 passed / 0 failed / 0 skipped |
| Acceptance | 5 passed / 0 failed / 0 skipped |
| Provider certification | 78 passed / 0 failed / 0 skipped |
| PostgreSQL provider, Docker-backed | 39 passed / 0 failed / 0 skipped |
| Section 7 drivers | 37 passed / 0 failed |
| Infrastructure, isolated run 1 | 158 passed / 0 failed / 0 skipped |
| Infrastructure, isolated run 2 | 158 passed / 0 failed / 0 skipped |
| Infrastructure, isolated run 3 | 158 passed / 0 failed / 0 skipped |
| ExpectedRed | exit 1 as designed; 0 passed / 14 named Section 8 failures / 0 skipped |
| Green compile fixtures | exact/product positives compiled; 26 + 26 forbidden calls rejected; mutation control rejected |
| ExpectedRed compile fixtures | exit 0; 0 remaining gaps |
| Green package fixtures | 6 consumers built from the exact local feed |
| Package ExpectedRed | exit 1 as designed; exactly `dag-hosting` and `kubernetes-companion` |
| Strict OpenSpec, reshape | valid |
| Strict OpenSpec, runtime governance | valid |
| Strict OpenSpec, all | 17 passed / 0 failed |
| NuGet vulnerability audit | no vulnerable packages in all 23 active solution projects |
| `git diff --check` | exit 0; line-ending notices only |

One attempted parallel infrastructure batch caused a reviewer-induced lock on the shared generated
NuGet `.nuspec`; that contended result was discarded. The required three infrastructure runs were
then executed serially and each passed 158/158.

ExpectedRed and package-red nonzero exits are intentional negative evidence and were not counted as
passing tests.

## 6. Required remediation and gate disposition

Before refreezing:

1. persist and compare the complete start-idempotency binding, returning the closed start conflict
   after restart and across hosts;
2. persist per-target normalized event identity/fingerprint and prove public durable
   duplicate/conflict behavior after restart, after wait consumption, and across correlation
   delivery;
3. implement and directly test cooperative `CancellationRequested`,
   `Requested`/`AlreadyRequested`/`AlreadyTerminal`, and race-derived termination results in both
   modes; and
4. update the retirement/replacement record and task accounting to include all removed validation
   surfaces accurately.

Preserve this rejection as immutable evidence. Remediate in a new target, rerun the full Section 7
gate, and request a fresh independent review. Until that target is approved, do not create the
checkpoint commit and do not begin Section 8.
