# Phase 6 - Providers, Hosting, Production Readiness (spec Slice 6)

**Goal**: real transports and remaining store plugins, hosting integration,
continue-as-new, archival, and the production-readiness gates.

**Entry criteria**: Phase 5 exit green.
**Exit criteria**: AC-315 (if multi-node pursued), all `[Trait("Category","Certification")]`
suites green against every shipped provider; security/performance gates per spec Slice 6.
Spec open question 10 is resolved early: AC-313 belongs to Slice 2. Because the current
`current implementation` track reached Phase 6 without DU-042, T6-04/T6-05 are corrective backfill tasks for
that early requirement. Public package publishing is deferred by the IOQ-9 owner decision
recorded in `docs/implementation/00-stack-decisions.md`.

## Task index (expanded by T6-00)

| Task | Title | Difficulty | Summary |
|------|-------|-----------|---------|
| T6-00 | Expand index; resolve Phase 6 decisions | Sonnet | Resolve IOQ-8 and IOQ-9, split broad provider/readiness work into executable task files, and stop before T6-01 |
| T6-01 | RabbitMQ dispatcher project | Sonnet | Add `OrcaCore.Providers.RabbitMq` and its unit tests; map normalized outbox records to `RabbitMQ.Client` publish outcomes without leaking broker concepts into workflow definitions (PR-015) |
| T6-02 | RabbitMQ integration certification | Sonnet | Add RabbitMQ Testcontainers coverage for publisher confirms, retryable failures, and permanent poison/dead-letter behavior through `IMessageDispatcher` (PR-015, DU-032) |
| T6-03 | Hosting registration package | Haiku | Fill `OrcaCore.Hosting` with explicit `AddOrcaCore()` and per-plugin registration extensions plus hosted services for outbox pump, timers, and operational sweeps (PR-040) |
| T6-04 | Continue-as-new contracts and aggregate backfill | Sonnet | Corrective backfill for early Slice 2 requirement: add rollover command/event facts and durable aggregate behavior that preserves logical identity while bounding history growth (DU-042, AC-313) |
| T6-05 | Continue-as-new provider projection backfill | Sonnet | Corrective backfill for early Slice 2 requirement: persist/query rollover lineage in in-memory and PostgreSQL providers; acceptance and provider tests prove AC-313 across durable metadata |
| T6-06 | Archival and retention policy | Sonnet | Introduce declarative archive/purge policy distinct from active eviction; certification additions prove active instances and dispatch are never broken (DU-050/051, AC-314) |
| T6-07 | Redis projection/cache provider profile | Sonnet | Add a Redis provider only for explicitly supported projection/cache roles, with README envelope and certification limited to implemented ports |
| T6-08 | SQL Server provider event-store slice | Sonnet | Add SQL Server provider project and port the atomic event/checkpoint/inbox/outbox commit path using `Microsoft.Data.SqlClient` |
| T6-09 | SQL Server projections, timers, and pools | Sonnet | Complete SQL Server provider coverage for projections, durable timers, and resource pools; run provider certification with Testcontainers |
| T6-10 | DynamoDB implementation deferral gate | Sonnet | Resolve IOQ-10 by deferring DynamoDB implementation for this run while preserving the existing provider-port compatibility boundary for a future adapter |
| T6-11 | ZeroMQ dispatcher plugin | Sonnet | Add brokerless `IMessageDispatcher` adapter over NetMQ and document its weaker delivery envelope explicitly |
| T6-12 | Benchmark project skeleton | Haiku | Add `benchmarks/OrcaCore.Benchmarks`, central package entry, solution entry, and CI build-only wiring for BenchmarkDotNet (IOQ-8) |
| T6-13 | Benchmark scenarios | Sonnet | Implement BenchmarkDotNet scenarios for the resolved IOQ-8 hot paths: ephemeral loop, provider serialization/materialization, management projections, pools/timers, and provider commits |
| T6-14 | Production readiness docs and sample host | Sonnet | Add versioning/breaking-change notes, security checklist, sample host app, and benchmark run instructions; omit public package publishing per IOQ-9 |
| T6-15 | EKS scheduler enablement handoff | Sonnet | Document scheduler-app boundaries for K8s dispatcher/watcher contracts, `StartOrGet` occurrence keys, and JS-AC-008 ownership (JS-003/004) |
| T6-16 | Multi-node lease layer gate | Sonnet | Optional task only if owner pursues AC-315: define per-instance leases atop expected-version without weakening single-host semantics (DU-060) |

## Phase-wide guardrails

- Every plugin ships with: its certification-suite test project (Testcontainers or
  equivalent), an `AddOrcaCore<X>()` registration extension, and a README stating which
  ports it implements and its delivery/consistency envelope.
- No plugin may require a change to a port to pass certification without a spec-level
  review (03-tdd-workflow section 2).
- BenchmarkDotNet belongs only under `benchmarks/OrcaCore.Benchmarks`; PR CI builds
  the benchmark project but does not run benchmarks.
- Package publishing, package IDs, signing, and per-package release README work are out of
  scope until the owner explicitly reopens public packaging.
