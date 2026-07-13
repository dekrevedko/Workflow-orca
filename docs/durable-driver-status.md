# Durable Driver — Implementation Status (Review + Continuation Handoff)

Status as of 2026-07-12, branch `feature/v3-rebuild`, **all work uncommitted**.
Normative specs: `docs/specs/16-requirements-durable-driver.md` (DR-*) and
`docs/specs/06-requirements-durable-execution.md` (DU-*). Companion doc:
[durable-driver-lane-host.md](durable-driver-lane-host.md) (advancement + contention model).
Implementation evidence and review deltas:
[durable-driver-audit.md](durable-driver-audit.md).

## Phase status

| Phase | Scope | Gate | Status |
|-------|-------|------|--------|
| DR-P1 | Interpreter + envelope + parked/start-input/policies | DR-AC-001/002/005/006/009/013/016/018/023/024/025/028/032/033 | **COMPLETE** — reviewed policy, re-arm, `WhenFirst`, and continue-as-new gates are green |
| DR-P2 | Lane host + continuation signal | DR-AC-003/004/010/011/014/015/017/019/020/021/022/026/027/029 | **REMEDIATION IN PROGRESS** — durable poison accounting and DR-AC-027/029 green; full DR-AC-026 disposition matrix remains |
| DR-P3 | Saga + DAG driving (DR-040/041) | DR-AC-007/008 | **IN PROGRESS** — `RunChild`/`RunChildren` driver bridge implemented; saga and complete DAG orchestration remain |
| DR-P4 | Facade, parity, samples, telemetry (DR-032/050/051/060/061) | DR-AC-012/030/031 + sample e2e + DU-002 matrix | **IN PROGRESS** — routing, management, options, and all seven instruments implemented; parity and sample remain |

Pre-review verified baseline (2026-07-07): PostgreSQL provider suite 68/68, SqlServer 54/54,
Integration 114 passed / 1 skipped (`INT_JS_018` soak — intentional), all in-memory unit
lanes green (Engine.Durable 207, Core 276, Acceptance 71, Hosting 11, Certification 58,
Ephemeral 134), `dotnet build OrcaCore.slnx -c Release -warnaserror` clean. These results
prove the original gates, not the reviewed DR-AC-023..033 additions.

## What was built

### DR-P1 — Interpreter + position envelope (baseline implemented)

- `src/OrcaCore.Engine.Durable/Driver/` — the run-to-suspension interpreter
  (`DurableWorkflowDriver`, `DurableDriverSegmentRun` + `.Steps` partial, models). One
  advancement segment per invocation; every kernel command is one atomic commit through
  the existing `DurableCommandProcessor` seam (DR-002 honored — no new store ports beyond
  the sanctioned DR-037 claim selector).
- `src/OrcaCore.Abstractions/Durable/DurableExecutionEnvelope.cs` — versioned checkpoint
  payload (ContentType `application/vnd.orcacore.durable-envelope.v1+json`): business
  state + execution position (cursor stack with frames; phases AtNode / SuspendedOnWait /
  SuspendedOnTimer / SuspendedOnChildren / SuspendedOnExternalJob / SuspendedOnResourcePool /
  Yielded / Completed).
- `Parked` workflow status + `DurableParkCommand`/event (reasons: `RuntimeStateVersion`,
  `VersionBinding`, `Poison`). Parked instances remain parked when definitions are registered.
  Public `RearmAsync` requires the observed stream version and validates reason-specific
  preconditions; poison re-arm additionally requires explicit acknowledgement.
- Durable rejects ephemeral-only `ForEach` at registration.
- Start input is persisted on `StartWorkflowCommand`/`WorkflowStartedEvent`. DR-AC-023 proves
  typed input survives replacement before `Init`, while serialization failure commits no
  runnable instance.
- Step execution snapshots committed business state before every attempt and restores it after
  exceptions, timeout, or failed results, preventing failed mutable attempts from leaking into
  retries. Retry attempt/backoff eligibility, stable logical-operation key, and absolute timeout
  deadline persist on the execution cursor across host replacement.

### DR-P2 — Lane host + restart-safe continuation (baseline implemented)

- **Continuation signal (DR-034):** `DurableCommitMaterializer` appends an internal
  `continue` outbox record (`OutboxKinds.Continue`, payload `DurableContinuationSignal`)
  inside every commit that leaves the instance runnable. Runnability predicate: projected
  status Running OR an unblocking event in the batch (WaitMatched, TimerFired, Unparked,
  Resumed, ExternalJobCompleted, ResourcePoolAcquired, ParentResumeTokenRecorded) OR a
  runnable cursor in the committed envelope. Over-emission is harmless (stale claims
  no-op); status alone under-emits for parallel shapes — hence the three-way predicate.
- **Kind-partitioned claims (DR-037):** `OutboxClaimRequest.KindSelector`
  (`Including`/`Excluding`) implemented in InMemory, PostgreSQL, SqlServer + fake store;
  index migrations PG `006_outbox_kind.sql`, SqlServer `007_outbox_kind.sql`; certification
  tests pin the contract. The external dispatcher pump claims `Excluding(Continue)` and
  additionally releases any continue record it ever sees.
- **Continuation pump:** `DurableContinuationPump` claims only continue records, drives
  the instance in `Required` mode via `DurableWorkflowRuntime.DriveAsync`; conflicts are
  retryable. Failed attempts, position version, and next-eligible time are workflow facts and
  checkpoint state. A drive failure retains its claimed signals as retryable even if the failure
  fact has no runnable checkpoint (for example, a waiting parallel sibling), so a missing
  successor signal cannot strand the instance. Success/no-op progress resets the durable count;
  threshold crossing parks as `Poison`, so host replacement cannot reset the poison policy.
  Hosted by `OrcaCoreContinuationPumpHostedService` with graceful drain (DR-033) via a
  TimeProvider-scheduled drain timeout (default 30s); options: interval 1s, batch 100,
  lease 5m — all on `OrcaCoreHostedServiceOptions`.
- **Drive modes:** `Opportunistic` (facade paths; skips non-driver instances) vs
  `Required` (claimed continuations; parks driver-owned instances with unregistered
  versions). Driver-owned = the checkpoint payload is a `DurableExecutionEnvelope` —
  kernel-direct usage is never parked by the pump. Opportunistic facade drives continue through
  committed policy and continue-as-new boundaries, while required pump drives release the host
  turn for the persisted successor signal.
- **Segment budget (DR-051):** `DurableDriverBudget` (256 commands / 30s,
  resolved DR-OQ-3 defaults), injectable into the runtime; `BudgetExhausted` ends the segment
  and a fresh continue record reschedules (fairness, DR-AC-017). The duration is an admission
  deadline checked between operations, not forcible preemption. Worker concurrency, poison
  threshold/backoff, and segment budgets are exposed as positive validated hosted options.
- **External jobs + resource pools as step results:** `StepResult.RunExternalJob(...)`
  and `StepResult.AcquireResources(...)` (no new definition nodes). The driver supplies
  the `WaitId` so the persisted position references the kernel-registered wait;
  `RunExternalJobCommand`/`AcquireResourcePoolCommand` gained
  WaitId/Envelope/ExpectedStreamVersion/ConsumedResumeWaitIds. Grant-resume semantics:
  reserved event name `"ResourcePoolGranted"` re-runs the guarded node (re-attempts
  acquisition); any other resume advances and feeds the event. Ephemeral engine throws
  `NotSupportedException` for both (durable-only surface).
- **Multi-host model (DR-035, honest):** independent-instance distribution via
  claim-based pumps over expected-version append; no per-instance lease, no cluster-wide
  single activation (that is the Orleans host's territory). Documented in
  [durable-driver-lane-host.md](durable-driver-lane-host.md).

### Latent bugs found and fixed during P2 (review these)

1. **.NET 10 STJ source-gen fast path serializes a null `byte[]` property as `""`.**
   `OrcaCoreJsonSerializerContext` is pinned to `JsonSourceGenerationMode.Metadata` with a
   warning comment. Do not re-enable the fast path without a null-`byte[]` round-trip test.
   Surfaced as PG round-trip failures on `InputPayload`/buffered-delivery payloads.
2. **Pending-resume context loss:** `DurableWaitState.Apply(WaitMatched)` dropped
   EventName/CorrelationId for kernel-driven matches (e.g. `CompleteExternalJobCommand`)
   because the event carries none; now falls back to the matched wait's registered
   context, and the driver uses an `"(uncorrelated)"` sentinel instead of constructing an
   empty CorrelationId.
3. **Non-idempotent pool re-acquire:** after release→grant-signal, the holder's re-acquire
   queued behind its own tickets. All three resource-pool stores now short-circuit when
   the holder already holds satisfying tickets; certification tests pin it.

### Known accepted edges (documented, not blocking)

- A completion/grant event delivered *before* the corresponding wait registers is
  buffered and does not match the later-registered wait — the external watcher/operator
  re-signals. (Buffered-delivery replay for driver-registered waits is a possible P4+ item.)
- No kernel machinery turns a store-side pool grant into a delivered
  `"ResourcePoolGranted"` event — signaling the grant is the releasing side's job today.

### Reviewed remediation implemented on 2026-07-12

- Explicit expected-version `RearmAsync` with reason-specific preconditions; definition
  registration no longer changes parked state.
- Durable continuation failure count, position binding, exponential backoff, threshold park,
  and reset-on-progress semantics. The successor `continue` signal carries `NotBefore`.
- Continuation budget disposition now distinguishes progress from zero-progress exhaustion:
  the prior claim is dispatched only when a progress commit produced its successor; otherwise
  it remains retryable so an extremely short admission deadline cannot strand the instance.
- Correlation-targeted and definition-fanout event delivery on `DurableWorkflowRuntime`, plus
  public `DurableManagement` exposure. Fanout assigns a distinct event id per target.
- Validated hosted controls for continuation concurrency, poison threshold/backoff, and segment
  command/time admission budgets.
- All seven DR-050 instruments, including provider-backed continuation and external-outbox
  backlog gauges split by pending/retryable/claimed state.
- `RunChild` and `RunChildren` now commit child scheduling with a `SuspendedOnChildren` envelope.
  Parent resume-token consumption, child matched-resume cleanup, and cursor advancement commit
  atomically. Child ids/waits and fanout item snapshots are deterministic across restart.
- Typed `StepResult.ContinueAsNew<TState>` now commits replacement state and a fresh post-`Init`
  root cursor inside the versioned envelope. The old segment releases after rollover and the
  successor continuation resumes the same logical instance on a replacement host (DR-AC-033).

## Review reconciliation (2026-07-12)

The reviewed specification added or tightened requirements after the baseline implementation:

- **Implemented and reviewed-gate green:** durable start input (DR-018/AC-023), kind
  partitioning (DR-037/AC-029), explicit reason-specific re-arm (DR-017/AC-009/028), durable
  alternating-host poison accounting (DR-036/AC-027), and public facade routing/management
  (DR-032/AC-030). Definition fanout resolves matching active waits only, preventing delivery
  to completed instances. Durable `WhenFirst` now proves one committed winner across racing
  hosts, residual wait cancellation, and no second winner after host replacement (DR-AC-032).
  `WhenFirst` residual ownership now includes every nested cursor beneath a losing branch, so
  nested `If`/`While`/split waits are cancelled and cannot later fail the parent. Completed-cursor
  merge passes also skip deferred `Parallel` joins and continue scanning, preventing those joins
  from starving a ready `WhenFirst` in a composed definition.
- **Implemented, broader proof pending:** conflict retry and stale/no-op continuation handling
  (part of DR-034/AC-026).
- **Implemented and reviewed-gate green:** fixed retry backoff commits a durable timer with the
  next attempt, eligibility time, and stable logical-operation key; failed-attempt mutations are
  rolled back before that boundary (DR-019/AC-024). Timeout-decorated steps commit an absolute
  deadline before user code starts, and cooperative operator cancellation reaches the running
  decorated-step token without an extra retry (DR-019/AC-025).
- **Sequencing decision:** P1 remediation is complete and P3 may proceed. P2 remains open only
  on DR-AC-026, whose lease-loss case needs an ownership-bearing outbox claim contract before
  the full phase can be claimed complete.

## What is NOT done

### DR-P3 — Saga + DAG driving (in progress; gate DR-AC-007/008)

- **Saga (DR-040):** kernel already owns SG semantics (compensation planning,
  forward-action dedup); the driver must contribute step execution + command sequencing
  so a saga definition runs through the interpreter, with compensation surviving restart.
  DR-AC-007: forward failure after N committed actions, restart mid-compensation → all N
  compensation transitions commit once in reverse order; a crash-before-commit may repeat
  an idempotent compensation action under DR-014/SG-012.
- **Child composition baseline:** `RunChild`/`RunChildren` driver scheduling and restart-safe
  parent resumption are implemented once child completion enters the existing kernel command
  path. Remaining work is hosted child-start/completion routing, DAG-plan integration,
  transitive failure closure, and well-defined cancellation/cleanup when a child-owning cursor
  loses a `WhenFirst` race. DR-AC-008 still requires a diamond plan to complete driver-only
  with no manual pumping.

### DR-P4 — Facade, parity, samples, telemetry (gate DR-AC-012/030/031)

- Facade routing and `DurableManagement` exposure are implemented and DR-AC-030 is green;
  ephemeral-shape parity for WorkflowCore migration remains.
- DR-AC-012 parity coverage remains.
- Telemetry (DR-050) and options (DR-051) are implemented. DR-AC-031 proves exact
  continuation/external state gauges on all three workflow stores, invalid option rejection,
  and valid overrides reaching the resolved runtime and continuation pump.
- One durable e2e sample, DU-002 feature-matrix column, durable developer guide.

### Housekeeping

- **Nothing is committed.** The driver work shares the working tree
  with unrelated concurrent edits from another session (dashboard Kubernetes sample,
  `docs/orleans-engine/` docs, `_review/`, `.codex-run/`, ephemeral-engine refactor
  files). The parallel test-review process is complete, but broad staging would still mix
  unrelated work; stage from the driver file inventory.
- DR-OQ-3 is resolved: defaults are 256 commands / 30 seconds with positive validated
  per-host overrides; unbounded production segments are unsupported.

## File inventory (driver work only)

New: `src/OrcaCore.Engine.Durable/Driver/*` (interpreter, pump, models),
`src/OrcaCore.Abstractions/Durable/DurableExecutionEnvelope.cs` + `DurableContinuationSignal.cs`,
`src/OrcaCore.Hosting/Services/OrcaCoreContinuationPumpHostedService.cs`,
`src/OrcaCore.Providers.PostgreSql/Migrations/006_outbox_kind.sql`,
`src/OrcaCore.Providers.SqlServer/Migrations/007_outbox_kind.sql`,
`tests/OrcaCore.Engine.Durable.Tests/Driver/*`,
`tests/OrcaCore.Hosting.Tests/ContinuationPumpHostedServiceTests.cs`,
`tests/OrcaCore.Integration.Tests/E2E/DurableDriverPostgreSqlIntegrationTests.cs`,
`docs/durable-driver-audit.md`, `docs/durable-driver-lane-host.md`.

Modified (driver-relevant): `OrcaCoreJsonSerializerContext` (Metadata pin),
`DurableCommitMaterializer` (continue records), `DurableOutboxPump` +
`OrcaCoreOutboxPumpHostedService` (exclude continue), `ProviderPorts`/`ProviderCommitContracts`
(KindSelector), all three workflow stores (kind claims) and resource-pool stores
(idempotent re-acquire), `StepResult` (+2 results), `WorkflowCommand` (job/pool command
fields), `DurableExternalJobCommandHandler` / `DurableResourcePoolCommandHandler` /
`DurableResourcePoolState` / `DurableWaitState` / `DurableWaitTimerCommandHandler`,
`DurableWorkflowRuntime` (budget, DriveAsync, re-arm, routing, management),
`IDurableDriverObserver`, telemetry instruments/observer, `OrcaCoreHostedServiceOptions` +
`OrcaCoreServiceCollectionExtensions` (pump registration), ephemeral `StepExecutor`
(NotSupported), certification + fake-store tests.

## Verification (run from ``)

```powershell
dotnet build OrcaCore.slnx -c Release -warnaserror
dotnet test tests/OrcaCore.Engine.Durable.Tests/OrcaCore.Engine.Durable.Tests.csproj
dotnet test tests/OrcaCore.Hosting.Tests/OrcaCore.Hosting.Tests.csproj
dotnet test tests/OrcaCore.ProviderCertification/OrcaCore.ProviderCertification.csproj
# Container gates (Docker required):
dotnet test tests/OrcaCore.Integration.Tests/OrcaCore.Integration.Tests.csproj --no-build
```

Gate tests are discoverable by trait: `--filter "Trait=AC"` groups, e.g.
`dotnet test ... --filter "AC=DR-AC-003"`. Expected integration baseline:
114 passed / 1 skipped (`INT_JS_018`).

Current local verification after reviewed remediation (2026-07-12):

- `OrcaCore.Engine.Durable` build: clean, 0 warnings/errors.
- `OrcaCore.Engine.Durable.Tests`: 229/229 passed.
- `OrcaCore.Hosting` build: clean, 0 warnings/errors.
- `OrcaCore.Hosting.Tests`: 15/15 passed.
- `OrcaCore.ProviderCertification`: 66/66 passed.
- PostgreSQL provider: 72/72 passed.
- SQL Server provider: 58/58 passed.
- Integration: 114 passed / 1 intentional soak skip.

Reviewed gates now green in the reconciled tree: DR-AC-009, DR-AC-023, DR-AC-027,
DR-AC-024, DR-AC-025, DR-AC-028, DR-AC-029, DR-AC-030, DR-AC-031, DR-AC-032, and
DR-AC-033. The only reviewed-remediation waiver remaining is the incomplete DR-AC-026
continuation fault matrix.
