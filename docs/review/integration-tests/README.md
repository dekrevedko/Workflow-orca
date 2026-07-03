# Integration Testing — Catalog & Plan

Companion to [R9-test-coverage-gaps.md](../findings/R9-test-coverage-gaps.md) and
[test-scenarios/](../test-scenarios/README.md). Documents **cross-boundary** tests that wire
two or more real components (host, engine, provider, dispatcher) — distinct from unit tests
(single module) and acceptance tests (public API + InMemory only, per `03-tdd-workflow.md`).

## Taxonomy (per `docs/implementation/03-tdd-workflow.md`)

| Level | Project today | Scope | Real dependencies |
|-------|---------------|-------|-------------------|
| **Unit** | `*.Tests` per project | One module via interface | Fakes in `TestSupport` |
| **Acceptance** | `OrcaCore.Acceptance.Tests` | Public API, AC-tagged | Engines + `InMemoryWorkflowProvider` |
| **Certification** | `OrcaCore.ProviderCertification` | Port invariants PR-020…024 | Provider under test |
| **Provider integration** | `OrcaCore.Providers.*.Tests` | Certification + plugin-specific | Testcontainers per plugin |
| **Host integration** | `OrcaCore.Hosting.Tests` (partial) | DI + `IHost` + hosted services | Recording fakes or InMemory |
| **Full-stack integration** | **Missing dedicated project** | Engine + real store + host + dispatcher | Multi-container fixtures |

## Current state (2026-07-03)

| Area | Exists | Gap |
|------|--------|-----|
| PostgreSQL store/timers/pools/retention | Testcontainers + certification | No `DurableCommandProcessor` E2E on PG |
| SqlServer | Testcontainers + certification + engine/stack integration | Event/projection/timer/outbox/resource-pool paths are real SQL; host profile still not wired |
| RabbitMQ dispatcher | `RabbitMqDispatcherIntegrationTests` | Not wired through host outbox pump |
| Redis projection | `RedisProjectionProviderTests` + `INT-ST-005` | No production read-through host profile |
| Hosting pump/timer/sweep | `OrcaCoreHostingServiceCollectionTests` | Recording fakes only; not real processor+store |
| Sample host | `SampleHostSmokeTests` | DI resolve only; no workflow run |
| Acceptance durable flows | `*AcceptanceTests` | All InMemory; no restart boundary |
| Multi-node | — | AC-315 waived; no tests |
| Job scheduler E2E | Partial unit/acceptance | No host+PG+RabbitMQ scenario |

## Recommended project: `OrcaCore.Integration.Tests`

Add to solution under `tests/`:

```
tests/OrcaCore.Integration.Tests/
  Fixtures/
    PostgreSqlOrcaFixture.cs      # IAsyncLifetime container + store init
    OrcaHostFixture.cs            # IHost with configurable provider profile
    MultiNodeFixture.cs           # Two hosts, shared connection string
  Hosting/
  EnginePostgreSql/
  ProviderStacks/
  JobScheduler/
```

**Traits for CI filtering:**

```csharp
[Trait("Category", "Integration")]
[Trait("Container", "PostgreSql")]  // optional sub-filter
[Trait("AC", "AC-301")]
```

**CI jobs (suggested):**

```yaml
# Fast path (every PR) — no Docker
dotnet test --filter "Category!=Integration"

# Integration path (main + nightly) — Docker required
dotnet test --filter "Category=Integration"
```

## Scenario files

| File | Focus |
|------|-------|
| [01-hosting-and-hosted-services.md](01-hosting-and-hosted-services.md) | `IHost`, pump, timer, sweep, sample host, shutdown |
| [02-engine-with-real-providers.md](02-engine-with-real-providers.md) | Processor + PostgreSQL/SqlServer persistence boundaries |
| [03-provider-composition-stacks.md](03-provider-composition-stacks.md) | PG + RabbitMQ, PG + Redis, outbox→broker round-trip |
| [04-durable-workflow-e2e.md](04-durable-workflow-e2e.md) | Full durable AC paths on real store (not InMemory) |
| [05-multi-node-and-restart.md](05-multi-node-and-restart.md) | Process/host restart, two hosts, AC-315 |
| [06-job-scheduler-e2e.md](06-job-scheduler-e2e.md) | DAG + external job + quota on integrated stack |
| [07-harness-ci-and-fixtures.md](07-harness-ci-and-fixtures.md) | Shared fixtures, compose, stability, tagging |

## Scenario ID format

- **INT-** prefix — integration scenario
- **Status:** `Covered` | `Partial` | `Missing`
- **Components:** explicit wiring under test

## Cross-references

- Negative/edge scenarios: [test-scenarios/](../test-scenarios/README.md)
- Integration scenarios: [integration-tests/](../integration-tests/README.md)
- Provider defects: [R5](../findings/R5-providers-certification.md)
- Hosting: [R7](../findings/R7-hosting-cross-cutting.md)
