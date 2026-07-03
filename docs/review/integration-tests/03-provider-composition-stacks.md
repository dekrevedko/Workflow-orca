# Provider Composition Stacks — Integration Scenarios

**Components:** Multiple real providers wired as a production-like host would:
PostgreSQL (event store + projection + timers + pools) + RabbitMQ (dispatcher) + optional Redis
(projection cache).

Today each plugin is tested **in isolation**. These scenarios catch wiring mistakes at
`AddOrcaCore` / host registration boundaries.

## Existing coverage

| Stack | Test | Gap |
|-------|------|-----|
| RabbitMQ only | `RabbitMqDispatcherIntegrationTests` | No outbox pump |
| PostgreSQL only | Certification + event store tests | No dispatcher |
| Redis only | `RedisProjectionProviderTests` | In-process only |
| InMemory all-in-one | Hosting.Tests, Acceptance | Not production shape |

---

## Missing integration scenarios

### INT-ST-001 — PostgreSQL commit → outbox pump → RabbitMQ queue
- **Priority:** P0 | **AC:** AC-310, DU-032 | **Status:** Missing
- **Components:** `PostgreSqlWorkflowStore` + `DurableOutboxPump` + `RabbitMqMessageDispatcher` + Testcontainers (PG + RabbitMQ)
- **Act:** Processor commits outbox row → host pump runs → message in queue
- **Assert:** Payload bytes match; outbox `Dispatched` in PG

### INT-ST-002 — RabbitMQ failure → outbox retry → eventual dispatch
- **Priority:** P0 | **AC:** DU-032 | **Status:** Missing
- **Setup:** Broker down on first pump tick; up on second
- **Assert:** `RetryableFailure` leaves outbox pending; success on retry; no duplicate side effects

### INT-ST-003 — RabbitMQ permanent failure → outbox poison
- **Priority:** P1 | **AC:** DU-032 | **Status:** Missing
- **Act:** Unroutable message with `Mandatory=true`
- **Assert:** Outbox `Poisoned` or documented terminal state; no infinite pump loop

### INT-ST-004 — Publisher confirm wait before Dispatched (R5)
- **Priority:** P0 | **AC:** DU-032 | **Status:** Missing
- **Act:** Slow confirm / nack from broker
- **Assert:** Outbox not `Dispatched` until confirm; matches `RabbitMqDispatcherIntegrationTests` intent through pump

### INT-ST-005 — PostgreSQL projection + Redis cache
- **Priority:** P1 | **AC:** PR-013 | **Status:** Covered
- **Components:** PG authoritative + Redis read-through
- **Act:** Commit on PG → apply to Redis → second host reads Redis only
- **Assert:** Management list consistent with PG

### INT-ST-006 — Timer in PostgreSQL → host timer service → processor
- **Priority:** P0 | **AC:** EV-050 | **Status:** Missing
- **Components:** PG `ITimerScheduler` + `OrcaCoreTimerHostedService` + processor
- **Act:** Schedule via processor; advance clock; host tick
- **Assert:** `WorkflowTimerFiredEvent` in PG stream

### INT-ST-007 — Resource pool PostgreSQL + operational sweep host
- **Priority:** P1 | **AC:** AC-521, JS-AC-007 | **Status:** Missing
- **Act:** Acquire ticket → advance past expiry → hosted sweep
- **Assert:** Ticket expiry recorded/queryable in PG; held capacity is not silently reclaimed until an explicit release/force-release path runs

### INT-ST-008 — Retention purge PostgreSQL + outbox safety
- **Priority:** P1 | **AC:** AC-314 | **Status:** Partial (certification)
- **Act:** Terminal instance + pending outbox on another instance
- **Assert:** Purge rejected when unsafe; safe purge clears all related tables

### INT-ST-009 — ZeroMQ dispatcher in host stack (optional profile)
- **Priority:** P2 | **Status:** Missing
- **Components:** InMemory or PG store + ZeroMQ dispatcher
- **Act:** Outbox pump → ZMQ round-trip test peer
- **Assert:** Same as INT-ST-001 for ZMQ transport

### INT-ST-010 — Multi-dispatcher routing by outbox kind
- **Priority:** P2 | **AC:** DU-033 | **Status:** Missing
- **Setup:** `child-start` → RabbitMQ; `lifecycle-event` → no-op / different exchange
- **Assert:** Router dispatches by `OutboxWrite.Kind`

### INT-ST-011 — PostgreSQL + RabbitMQ container startup ordering
- **Priority:** P2 | **Status:** Missing
- **Fixture:** Compose both; host starts only when both healthy
- **Assert:** No race on first integration test in collection

### INT-ST-012 — Connection string rotation (simulate secret update)
- **Priority:** P2 | **Status:** Missing
- **Act:** Stop host → change password in container → update config → restart
- **Assert:** Host recovers; no silent in-memory fallback

### INT-ST-013 — SqlServer stack
- **Priority:** P1 | **Status:** Partial
- Mirror INT-ST-001 and INT-ST-006 for SqlServer + RabbitMQ. Current coverage proves SqlServer outbox, timers, and SQL-backed resource-pool persistence; host-profile wiring remains open.

### INT-ST-014 — InMemory dispatcher + PostgreSQL store mismatch guard
- **Priority:** P2 | **AC:** PR-040 | **Status:** Missing
- **Negative:** Host misconfigured with PG store but InMemory dispatcher
- **Assert:** Documented unsupported or test fails fast at startup

### INT-ST-015 — End-to-end lifecycle outbox + RabbitMQ consumer
- **Priority:** P1 | **AC:** AC-509 | **Status:** Missing
- **Act:** Instance reaches `Completed`; lifecycle record in outbox → pump → queue
- **Assert:** External consumer can parse lifecycle JSON

---

## Docker Compose sketch (for CI / local)

```yaml
services:
  postgres:
    image: postgres:17-alpine
  rabbitmq:
    image: rabbitmq:4-management-alpine
  redis:
    image: redis:7-alpine
```

Use [Testcontainers](https://dotnet.testcontainers.org/) modules already referenced in provider
test projects; share one container per collection via `ICollectionFixture`.

## Priority order

1. INT-ST-001, INT-ST-006 (minimal production spine: PG + host)
2. INT-ST-002, INT-ST-004 (dispatch reliability)
3. INT-ST-005 (Redis when implemented)
4. INT-ST-015 (lifecycle observability)
