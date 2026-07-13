# Orleans Engine — Implementation Plan

Six phases, O0–O5. Task sizing, template, and execution protocol are
[implementation/04-task-protocol.md](../../implementation/04-task-protocol.md) verbatim,
with paths under **``**. Phases O0–O1 ship fully detailed task files (they set the
patterns); O2–O5 start with an `OTn-00` expansion task that turns this index into full task
files against the code that exists then. Dependencies point backward only; tasks whose
dependencies are done may run in any order within a phase.

Progress log: `PROGRESS.md` in this folder (initialized by OT0-01; append-only,
one line per task).

## Phase O0 — Skeleton (detailed task files ready)

Entry: none beyond a green root build. Exit: Orleans engine project builds inside the
solution, a TestingHost cluster runs in tests, dependency isolation guard passes, transport
envelopes round-trip, DI composition compiles.

| Task | Title | Summary |
|------|-------|---------|
| [OT0-01](OT0-01-project-skeleton.md) | Engine project + packages | CPM pins the exact reviewed Orleans 10.x version (10.2.1 as of 2026-07-04); `OrcaCore.Engine.Orleans` project wired into `OrcaCore.slnx`; dependency rules hold; PROGRESS.md initialized |
| [OT0-02](OT0-02-testinghost-smoke.md) | TestingHost smoke test | `OrcaCore.Engine.Orleans.Tests` project; 1-silo cluster starts; echo grain round-trips; dependency isolation guard begins OE-AC-040 |
| [OT0-03](OT0-03-transport-envelopes.md) | Transport envelopes | `WorkflowCommandEnvelope`/`WorkflowCommandResultEnvelope` with STJ payloads; add-only codec covering the v1 kinds (start, deliver); turns OE-AC-041 green (OE-060) |
| [OT0-04](OT0-04-silo-composition.md) | Silo composition entry point | `UseOrcaCoreOrleans()` registering ports, shared `DurableCommandProcessor`, options record; turns OE-AC-042 green (OE-070) |

## Phase O1 — Grain execution core (detailed task files ready)

Entry: O0 exit. Exit: OE-AC-001, OE-AC-002, OE-AC-003, OE-AC-010, OE-AC-011,
OE-AC-012, OE-AC-013, OE-AC-020 green on TestingHost +
in-memory providers; OT1-03b certification green on Testcontainers PostgreSQL; review
gate on grain contract shape and OT1-00 seam decisions (incl. the seam-4 claimed-timer
answer); OOQ-2 resolved.

| Task | Title | Summary |
|------|-------|---------|
| [OT1-00](OT1-00-durable-seams-review.md) | Durable seam review (gate) | Verify/propose the three production-critical seams: cluster-safe start reservation, public command dispatch for delivery, lifecycle-participant pump hosting. Review-gated; blocks OT1-01a/02/03a/03/04 and Phase O2 |
| [OT1-01](OT1-01-instance-grain-start.md) | Instance grain + start path | `IWorkflowInstanceGrain`, `ExecuteAsync(envelope, ct)` → shared processor; start command commits (OE-AC-001); cancellation guard begins OE-AC-044. Start processing is already public — needs no seam |
| [OT1-01a](OT1-01a-public-delivery-seam.md) | Public delivery dispatch seam | Implement the approved SEAMS.md proposal in `Engine.Durable`: delivery/resume command dispatch becomes publicly callable (today `internal`, DurableCommandProcessor.cs:391). Review-gated Engine.Durable change |
| [OT1-02](OT1-02-wait-and-resume.md) | Wait/resume through grain | Wait suspends turn; direct-to-grain event delivery resumes via the OT1-01a seam (OE-AC-002 core) |
| [OT1-03a](OT1-03a-atomic-start-reservation.md) | Atomic start reservation | Extend `IWorkflowStartIdempotencyStore` with reserve-or-return; reservation materialized atomically in the start commit boundary; in-memory provider + certification test (review-gated port change) |
| [OT1-03b](OT1-03b-postgres-start-reservation.md) | PostgreSQL start reservation | Provider implements the OT1-03a semantics in the existing commit transaction; inherited certification suite green on Testcontainers. Parallel-safe; required before phase exit |
| [OT1-03](OT1-03-engine-facade.md) | Facade + cluster-safe StartOrGet | `StartIdempotencyGrain` over the OT1-03a reservation; concurrent/retry races converge (OE-AC-013, OE-042); facade parity guard begins OE-AC-043 |
| [OT1-04](OT1-04-event-routing.md) | Correlation routing (split surfaces) | Correlation-targeted delivery via projections; fanout as separate surface; dedup asserted (OE-AC-010, OE-AC-043, OE-040) |
| [OT1-05](OT1-05-concurrency-defense.md) | Concurrency & duplicate-activation defense | Racing deliveries single-winner (OE-AC-011); forced concurrent append (OE-AC-012) |
| [OT1-06](OT1-06-deactivation-rehydration.md) | Deactivation → rehydration | Idle deactivate + resume-by-event (OE-AC-020); `[ReadOnly]` snapshot + projection-only queries (OE-AC-003) |

## Phase O2 — Timers & durable waits (index; expand via [OT2-00](OT2-00-expand-task-index.md))

Entry: O1 exit. Exit: OE-AC-021, OE-AC-022, OE-AC-023 green; OOQ-1 resolved at gate.

- **OT2-01 Timer pump service** — `ILifecycleParticipant<ISiloLifecycle>` starting at
  `ServiceLifecycleStage.Active` (OE-072; wraps existing pump internals, does NOT reuse the
  `BackgroundService` hosting from `OrcaCore.Hosting`), claiming `ITimerScheduler` due
  commands and delivering to grains (OE-031). Tests: due timer → grain turn; two pumps →
  single fire; pump does not run before silo Active; claimed-but-undelivered timer is
  recovered, and a repeated `FireTimer` apply is a no-op (OE-033, OE-AC-023, OE-AC-045).
- **OT2-02 WaitLong across silo restart** — 2-silo cluster; stop hosting silo mid-wait;
  deliver approval; continuity assertions (OE-AC-021, OE-032).
- **OT2-03 Timer after full downtime** — cluster stop past due time; restart; late-but-once
  fire and timeout branch (OE-AC-022).
- **OT2-04 Turn-budget observability** — per-turn duration/outcome metrics + version-conflict
  counter per OE-053 and OE-012; test via metric listener (OE-AC-046).

## Phase O3 — Distribution & pumps (index; expand via [OT3-00](OT3-00-expand-task-index.md))

Entry: O2 exit. Exit: OE-AC-030 and OE-AC-031 green; **OOQ-4 resolved (pre-production architecture
gate: clustering provider, membership artifacts, client config)**.

- **OT3-01 Outbox pump hosting** — existing outbox pump wrapped as a silo lifecycle
  participant (OE-072); dispatch at-least-once from silo; marked dispatched (OE-AC-031).
- **OT3-02 Cross-silo routing** — 2-silo placement spread; events via clients on either
  silo reach their instances (OE-AC-030).
- **OT3-03 Management operations parity** — pause/resume/cancel/retention through the
  facade with durable-engine result parity (OE-071, OE-AC-043); buffered-while-paused semantics reused.
- **OT3-04 Projection-only management queries** — status/wait/history queries never
  activate grains; activation-count assertions (OE-052, OE-AC-003 hardening).
- **OT3-05 Production clustering spike** — AdoNet clustering over PostgreSQL
  (Testcontainers): membership tables provisioned, 2 real silos join/leave, client config
  validated. Output: the OOQ-4 resolution proposal for the phase gate.

## Phase O4 — Hosting polish & operations (index; expand via [OT4-00](OT4-00-expand-task-index.md))

Entry: O3 exit. Exit: composition, options, diagnostics, and docs complete; feature-matrix
entry published (DU-002).

- **OT4-01 Options hardening** — one options record (pump intervals, batch sizes,
  activation-collection age, turn-budget warning threshold) bound via `IOptions`; validation.
- **OT4-02 Diagnostics completion** — activation counters, pump gauges, `ActivitySource`
  spans per naming conventions (OE-053, OE-AC-046); listener-based tests.
- **OT4-03 Co-hosting sample wiring** — `OrcaCore.Hosting`-style registration for a generic
  host co-hosting silo + application; smoke test.
- **OT4-04 Docs & feature matrix** — engine README; add Orleans column to the feature
  matrix; update docs/README code map (docs-only task).

## Phase O5 — Certification & e2e (index; expand via [OT5-00](OT5-00-expand-task-index.md))

Entry: O4 exit. Exit: **package complete** — OE-081 subset plus OE-AC-050, OE-AC-051,
OE-AC-052, OE-AC-060 green;
OOQ-5/6 resolved (OOQ-4 closed at O3); final review gate.

- **OT5-01 Acceptance parity subset** — run the engine-observable durable AC set against
  the Orleans engine via shared fixtures (OE-081, OE-AC-052); document any plumbing deltas.
- **OT5-02 Postgres multi-silo integration** — Testcontainers PostgreSQL providers with
  the OOQ-4 clustering resolution from OT3-05; basic lifecycle across 2 silos.
- **OT5-03 Capstone e2e: driving scenario** — OE-AC-050 paths (a) approve, (b) timeout,
  (c) restart-mid-wait on TestingHost.
- **OT5-04 Capstone e2e: Postgres restart-mid-wait** — OE-AC-051 against the real database.
- **OT5-05 Chaos edges** — duplicate activation under silo kill; call-timeout retry storms;
  dedup and single-winner assertions under stress (OE-021, OE-041, OE-042, OE-015).
- **OT5-06 Load test: bounded per-turn I/O** — sustained mixed workload on Postgres 2-silo
  cluster; latency/tail-length/provider metrics (OE-AC-060, OE-023); output feeds the
  OOQ-6 warm-cache decision.

## Definition of package-done

All OE-AC traits green in CI; zero warnings; no Orleans reference outside the whitelist
scope; every OOQ resolved or explicitly deferred with rationale; final phase review passed.
