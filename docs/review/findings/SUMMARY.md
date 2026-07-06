# Full Audit Synthesis — 2026-07-03 (post-remediation verification)

Scope: `v3-gpt/` **working tree** on `feature/v3-rebuild` (includes ~2,800 lines of uncommitted
remediation changes — aggregate decomposition, SQL Server store rework, hosting wiring).
This synthesis closes the R0–R10 audit cycle: it records the verified baseline, confirms which
prior findings are fixed in code, adds the previously missing [R1](R1-abstractions.md) /
[R2](R2-core.md) phase findings, and ranks everything still open.

## 1. Verified baseline (2026-07-03, Docker available)

| Check | Result |
|---|---|
| `dotnet build v3-gpt/OrcaCore.slnx -warnaserror` | ✅ 0 warnings, 0 errors |
| Full `dotnet test v3-gpt/OrcaCore.slnx` (run 1) | 892 passed / **1 failed** / 16 skipped |
| Full re-run (run 2) | **894 passed / 0 failed / 16 skipped** (Hosting 10/10; flake did not reproduce, incl. 8× isolated re-runs) |
| Container suites (PostgreSQL 60, SQL Server 46, RabbitMQ 8, Redis 8, ZeroMQ 5) | ✅ all executed against real containers |
| Integration suite | 83 passed, 16 skipped (all skips are explicit unimplemented-feature markers, listed in §4) |
| AC trait coverage | **98/98** acceptance-criteria IDs from spec 12 have `[Trait("AC", …)]` tests |
| Line coverage (local, per-assembly best) | Engine.Ephemeral **91%**, Engine.Durable **93%**, Core **75%**, Abstractions **88%**, Providers.InMemory **75%** |
| Production hygiene | 0 TODO/HACK/`NotImplementedException` in `src/**`; no file ≥ 1000 lines; wall-clock statics guarded by test |

## 2. Prior P0/P1 remediation — verified in code (not just claimed)

| Finding | Status | Evidence |
|---|---|---|
| R4-P0 `StartOrGet` process-local only | **Fixed** | `DurableStartService` consults `IWorkflowStartIdempotencyStore` (durable lookup before create); two-host integration scenario active |
| R5-P0 SqlServer store in-memory stub | **Fixed** | Real SQL via migrations + delegated projection/timer/retention/resource-pool stores; 46 provider tests green |
| R6-P0 `RunChildren` throttling stalls after initial window | **Fixed** | `DurableChildWorkflowState.DispatchChildrenIfCapacity` + `NextDispatchIndex` replay |
| R7-P0 hosted services are no-ops | **Fixed** | `OrcaCoreOutboxPumpHostedService` / `OrcaCoreTimerHostedService` / `OrcaCoreOperationalSweepHostedService` are real `BackgroundService`s with failure boundaries + backoff tests |
| R3/R4/R6-P1 branch-blind wait matching | **Fixed** | `BranchId` on wait records + ambiguity rejection in both `WorkflowInstance` (ephemeral) and `DurableWaitState` (durable) |
| R3-P1 timer service unsynchronized / cancel not signaled | **Fixed** | `lock(gate)` in `EphemeralTimerService`; per-instance `CancellationTokenSource` with linked execution tokens |
| R6-P1 DAG compiler collapses heterogeneous children | **Fixed** | `CreateChildBatches` groups by (definition, version, policy); homogeneous-only `CreateChildBatch` now throws on misuse |
| R3-P2 correlation routing scans all instances | **Fixed** | `EphemeralRoutingIndex` extracted |
| R4-P2 durable management query-only | **Fixed** | `DurableManagement` exposes Pause/Resume/Cancel/Terminate/Purge |
| R8 quality set (reflection serializers, no Channels, GUIDv4, wall-clock) | **Fixed** | Source-gen STJ context; `InstanceLane` on bounded Channels (race traced sound — see R2 positives); `Guid.CreateVersion7`; `TimeProvider` seams + guard test |

## 3. Open findings, ranked — status after the 2026-07-03 fix pass

### P1
1. ~~**CR-002: durable-only `RunChild(ren)` fails at runtime on the ephemeral engine**~~ —
   **FIXED.** `WorkflowBuilder` now computes `RequiresDurableEngine` (recursive node scan) onto
   `WorkflowDefinition`; `EphemeralWorkflowEngine.RegisterDefinition` rejects such definitions
   with `WorkflowDefinitionException` before any execution. Tests:
   `WorkflowBuilderTests.Build_With*RequiresDurableEngine*`,
   `CoreRuntimeScenarioTests.NEG_CR_017_*` (scenario NEG-CR-017 added to the catalog). The
   interpreter's runtime failure remains as defense-in-depth.
2. **Negative/edge-case scenario backlog** — **STILL OPEN; catalog re-baselined 2026-07-04.**
   The catalog was re-checked against the suite: all 22 scenario IDs that tests reference are now
   `Covered` (20 were stale at `Missing`). Corrected totals: **~275 scenarios — 187 Missing,
   56 Partial, 32 Covered**. The 187 Missing (chiefly rejection/race/restart depth) remain the
   largest pre-ship work item; plan by area file, highest `Priority` first. This pass also added
   EDGE-EV-008 (concurrent duplicate `RaiseEventAsync`, exactly-once resume) and NEG-CR-017.

### P2
3. ~~**Durable event discriminators derive from CLR type names**~~ — **FIXED.** Codec entries
   now carry explicit string discriminators (pinned to the current names — no stream breakage),
   `WorkflowEventCodec.EventTypeNamesByClrTypeName` exposes the frozen table, and
   `WorkflowEventCodecTests.EventTypeNames_AreFrozenStreamDiscriminators` guards every entry.
4. ~~**CR-022 epoch/version absent from public snapshot surface**~~ — **FIXED.**
   `WorkflowInstanceSnapshot.StreamVersion` (nullable; durable-populated) flows from
   `DurableWorkflowAggregate.ToInstanceSnapshot()` through all projection stores: InMemory/Redis
   (record `with`-clones), PostgreSQL (migration `004_stream_version.sql` + upsert/select), SQL
   Server (migration `005_stream_version.sql` + upsert/select). Certification test
   `EventStoreCertificationTests.ProjectionUpsert_RoundTripsStreamVersion` enforces round-trip on
   every provider; the TestSupport fake projection store gained real upsert/list fidelity to
   participate. Ephemeral snapshots report null (the in-process lane is the ephemeral guarantee).
5. **Flaky hosting test under full-suite load** — **FIXED (root cause found 2026-07-04).** It
   recurred in a second full-suite run (2 of 3 full runs, never in isolation), which localized
   it: `AdvanceUntilObservedAsync` in `OrcaCoreHostingServiceCollectionTests` gave the hosted
   service only `Task.Yield()`s between fake-clock advances, so under thread-pool saturation
   the service could fail to schedule its backoff delay before all 50 advance iterations were
   spent — tripping the helper's assertion. The helper now waits with
   `Task.WhenAny(observed, Task.Delay(25))` (a bounded ~5 s liveness budget, exits immediately
   on observation; no-load duration unchanged at ~300 ms). Verified 6× isolated + 8× under
   four concurrent heavy suites.
6. ~~**CI single lane, token coverage gate**~~ — **FIXED.** `ci.yml` now has a `unit` job (7 fast
   projects, coverage, engine floor raised 0.20 → **0.80**, trx) and a `providers` job
   (PostgreSQL/SQL Server/RabbitMQ/Redis + integration, trx). Duplicate `ci-v3.yml` and dead
   `ci-v3-workspace.yml` (targeted the deleted `v3/` lineage) were removed.
7. ~~**DAG transitively-blocked nodes invisible**~~ — **FIXED.** `GetBlockedByFailures` computes
   the transitive closure; `WorkflowDagPlan.IsComplete` / `WorkflowDagRunner.IsComplete` expose
   the terminal-run check. Tests in `DagBuilderTests`.
8. **File-size watch items** — open, **deliberately deferred** (`PostgreSqlResourcePoolStore` 930,
   `SqlServerResourcePoolStore` 873, `WorkflowInstance` 887 — all under the 1000 hard limit).
   Assessed 2026-07-04: the resource-pool stores are already cleanly layered (public methods
   orchestrate a transaction and delegate to focused private SQL helpers), so the god-class risk
   is line count, not tangled logic. Concrete split when they cross 1000: extract the
   `(connection, transaction, …)` helpers into three stateless companions per provider —
   `*ResourcePoolTickets` (insert/delete/held-counts), `*ResourcePoolWaiters` (upsert/grant-queued),
   `*ResourcePoolExpiry` (expire/force-release/audit). This is behavior-preserving but only
   verifiable via the ~5-min-per-provider container suites, so it is not worth the regression
   surface while under the hard limit. Defer per standing guidance.
9. **Aggregate encapsulation loosened by decomposition** — open (internal-only, low urgency).

### P3
10. **Legacy vs lease claim overloads** — **DEFERRED deliberately.** Dozens of test call sites use
    the short overload, and a default-interface forward would need a wall-clock "now" (banned by
    the repository guard). Revisit only if a provider ships the legacy path without lease
    semantics; certification covers lease behavior today.
11. ~~Empty `If` then-branch validation~~ — **WITHDRAWN**: pinned intentional behavior
    (`Run_IfWithEmptyThenBranch_ContinuesAfterBranch`). `Then(IStep)` statelessness contract —
    **FIXED** (doc comment).
12. ~~`EventEnvelope.Payload` convention undocumented~~ — **FIXED** (doc comment).

## 4. Known missing implementations (honestly marked, not bugs)

**Update 2026-07-04:** the e2e push (commits `a3f55dd6…fd82feac`) closed **15 of the 16**
integration skips. `DurableDagRunner` now lives in the durable engine with PG e2e coverage
(JS-001), the durable saga and yield command paths landed, the definition facade covers
version binding, the PostgreSQL store gained append-failure hooks, and scheduler start
idempotency plus an observability (OTel) integration surface were added (new spec doc
[15-requirements-observability-otel.md](../../specs/15-requirements-observability-otel.md),
`OB-` prefix). The **single remaining skip** is the 1-hour fake-clock DAG soak, deferred to a
nightly slow suite by design.

The original gap table is kept for history:

| Gap | Was | Now |
|---|---|---|
| Engine-integrated DAG runner (JS-001) | 9 skips | **Closed** — `DurableDagRunner` + PG e2e |
| Durable saga interpreter e2e | 1 skip | **Closed** |
| Durable yield command path | 1 skip | **Closed** |
| Definition-registry version binding | 1 skip | **Closed** — definition facade |
| Store fault-injection seams | 2 skips | **Closed** — PG append-failure hooks |
| Host cron-scheduled starts | 1 skip | **Closed** — scheduler start idempotency e2e |
| Nightly DAG soak | 1 skip | Deferred by design (nightly lane) |

## 5. Verdict

**Fix-then-ship** (recorded 2026-07-03), with the "fix" half now substantially done:

- 2026-07-03 fix pass (§3): the CR-002 P1 and the P2 hardening items are fixed and verified
  (all provider suites green against real containers, including the new stream-version
  certification round-trip on PostgreSQL and SQL Server).
- 2026-07-04 e2e push (§4): all declared feature gaps closed; integration skips went 16 → 1
  (the deliberate nightly soak).
- Lineage consolidation committed: `v3/`, `v3-cursor/`, and their CI workflows removed;
  the two-lane `ci.yml` with the 0.80 engine coverage floor is the single pipeline.

What remains open before a "ship" call: the bulk of the negative/edge scenario backlog
(§3 item 2), the two file-size watch items (§3 item 8), and the observability gaps below.

### 6. Post-cycle audits (2026-07-04)

- **[R11](R11-new-surfaces.md)** — new-surfaces audit (DAG runner, durable saga/yield, definition
  facade, telemetry observer). One P1 fixed (DAG re-drive idempotency); yield/saga/observer/registry
  clean.
- **[R13](R13-samples.md)** — `samples/` examples audit. The dashboard was relocated under
  `samples/` (resolving R12's §15.10 scope decision toward "reference sample"). Two P2 example
  fixes applied (business failures used `WorkflowDefinitionException` → `OrcaCoreException`; the
  ForEach fanout body taught item identity via a mutable counter that only works for synchronous
  steps). Of the two API recommendations, the **ForEach per-item accessor is now implemented**
  (`StepContext.ForEachItem` / `ForEachItemContext` — a stable index + typed `Item<T>()`/`Items<T>()`,
  correct across interleaved suspension/resume; the example uses it). A **business-failure exception
  type** remains an open recommendation. The kubectl-shelling K8s sample is injection-safe. Examples
  build `-warnaserror` clean and run end-to-end.
- **[R12](R12-observability-dashboard.md)** — spec-15 `OB-*` observability + `OrcaCore.Dashboard`.
  **Two P1s open:** (1) `orca.instances.active` is a process-static command-side tally, not
  projection-backed, so it can't match `Statistics()` after restart/across hosts (OB-021/OB-080);
  (2) `AddOrcaCoreOpenTelemetry` (OB-070, the central §15.8 hosting deliverable) is absent — OTel
  wiring is inlined in the dashboard app. **One P2 decision:** `OrcaCore.Dashboard` ships a Blazor
  web dashboard, contradicting the §15.10 "no built-in dashboard" non-goal — reposition as a sample
  or amend the spec. Instrument/span coverage is ~20% of the OB catalog (tracks the spec's own
  phasing). All 7 `OB-AC` tests pass for the implemented subset. Observability is **not ship-blocking
  for correctness** (telemetry is best-effort, OB-004), but the fleet-dashboard goal is not yet met.
- **[R14](R14-scrutiny.md)** (2026-07-05) — fresh scrutiny of the durable kernel, ephemeral engine,
  providers, and uncommitted samples. **Found and fixed same day:** (P1, reproduced) the checkpoint
  schema dropped saga state and resume-token dedup — and PG/SQL Server persisted **no** checkpoint
  `RuntimeState` at all, losing waits/timers/children/tickets across checkpoint rehydration on SQL
  providers; fixed via schema extension + migrations (PG 005, SqlServer 006) + a certification
  round-trip gate on every provider. (P1) ephemeral engine retained terminal instances forever —
  `Management.Evict`/`EvictTerminal` added. (P2) post-commit ticket-release failures masked
  committed results; the k8s sample used an unstable random `DefinitionId`. (P3) resume replay
  ignored pending timers; guard scans included `obj/`. R12's two P1s verified fixed. Remaining open
  items are feature surfaces (durable definition driver → Orleans plan, RMQ consumer bridge, cron).
