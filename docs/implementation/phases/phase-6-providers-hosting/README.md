# Phase 6 — Providers, Hosting, Production Readiness (spec Slice 6)

**Goal**: real transports and remaining store plugins, hosting integration,
continue-as-new, archival, and the production-readiness gates.

**Entry criteria**: Phase 5 exit green.
**Exit criteria**: AC-313 (if not resolved into Phase 2 by spec OQ-10), AC-315 (if
multi-node pursued), all `[Trait("Category","Certification")]` suites green against every
shipped provider; packaging/security/performance gates per spec Slice 6.

## Task index (expanded by T6-00)

| Task | Title | Difficulty | Summary |
|------|-------|-----------|---------|
| T6-00 | Expand index; sequence the plugin backlog with the owner | Sonnet | Also resolves OQ-8 (benchmarks) and OQ-9 (packaging) |
| T6-01 | RabbitMQ dispatcher plugin | Sonnet | `IMessageDispatcher` over `RabbitMQ.Client`: publisher confirms → outcomes; poison → dead-letter; Testcontainers certification |
| T6-02 | Hosting package | Haiku | `AddOrcaCore()` + per-plugin `AddOrcaCore<X>()` extensions; hosted services for outbox pump, timer service, stuck-detection sweep (PR-040) |
| T6-03 | Continue-as-new | Sonnet | Rollover event + new checkpoint baseline preserving logical identity (DU-042); AC-313 |
| T6-04 | Archival policy | Haiku | Archive tier distinct from purge (DU-050/051); certification additions |
| T6-05 | Redis plugin (projections/cache roles first) | Sonnet | Scoped by owner decision; certification for implemented ports only |
| T6-06 | SQL Server plugin | Sonnet | Port of the Postgres design; `Microsoft.Data.SqlClient` |
| T6-07 | DynamoDB plugin | Sonnet | Resolve OQ-10 (single-table design) first |
| T6-08 | ZeroMQ dispatcher plugin | Sonnet | Brokerless at-least-once — document the weaker delivery envelope explicitly |
| T6-09 | Multi-node lease layer (only if pursued) | Sonnet | Per-instance leases atop expected-version (DU-060); AC-315 [provider, advanced] |
| T6-10 | Production gates | Sonnet | Versioning/breaking-change policy, security review checklist (NF-040), BenchmarkDotNet suite (OQ-8), sample host app, packaging (OQ-9) |
| T6-11 | EKS scheduler enablement handoff | Sonnet | JS-003/JS-004 boundary doc for the scheduler app repo: K8s dispatcher/watcher contracts (OQ-12), occurrence-key convention; JS-AC-007/008 land with the app |

## Phase-wide guardrails

- Every plugin ships with: its certification-suite test project (Testcontainers or
  equivalent), an `AddOrcaCore<X>()` registration extension, and a README stating which
  ports it implements and its delivery/consistency envelope.
- No plugin may require a change to a port to pass certification without a spec-level
  review (03-tdd-workflow §2).
