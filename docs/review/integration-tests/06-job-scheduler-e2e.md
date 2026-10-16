# Job Scheduler E2E — Integration Scenarios

**Components:** Full stack for doc 14 (EKS job scheduler): DAG plan, `RunExternalJob`,
durable resource pools, outbox → message broker, host timers, management observability.
Requires **PostgreSQL + RabbitMQ + Host** at minimum for realistic paths.

## Existing coverage (non-integration)

| Test | Level | Gap |
|------|-------|-----|
| `DagAcceptanceTests` | Manual commands + InMemory | Not host-integrated |
| `RunExternalJobTests` | Processor unit | InMemory |
| `ExternalJobAcceptanceTests` | Acceptance | InMemory |
| `DagObservabilityAcceptanceTests` | Reconstruct API | InMemory stream |
| `ResourcePoolStoreCertificationTests` | Pool store only | No job composite |

---

## Missing integration scenarios

### INT-JS-001 — Diamond DAG happy path on PostgreSQL
- **Priority:** P0 | **AC:** JS-AC-001 | **Status:** Missing
- **Components:** Host + PG + processor; optional fake K8s dispatcher
- **Act:** Run DAG A→{B,C}→D to completion (engine-driven or documented manual waves)
- **Assert:** D runs once after B,C; per-node `DefinitionId` in child-start outbox rows

### INT-JS-002 — DAG failure blocks dependents (integrated)
- **Priority:** P0 | **AC:** JS-AC-003 | **Status:** Partial
- **Act:** Node B fails; attempt D
- **Assert:** D never scheduled; parent policy applied in PG projection

### INT-JS-003 — External job: start outbox → broker → completion event
- **Priority:** P0 | **AC:** JS-AC-004 | **Status:** Missing
- **Stack:** PG + RabbitMQ + host pump
- **Act:** `RunExternalJob` → message on queue → test harness publishes `JobSucceeded` envelope → inbox
- **Assert:** Job completes once; ticket released

### INT-JS-004 — Scheduler restart mid external job
- **Priority:** P0 | **AC:** JS-AC-005 | **Status:** Missing
- **Act:** Start job → stop host → start host → send completion
- **Assert:** No second `external-job-start`; wait still correlated

### INT-JS-005 — Job timeout kills external work
- **Priority:** P0 | **AC:** JS-AC-006 | **Status:** Missing
- **Components:** Host timer + PG + dispatcher recording stop commands
- **Act:** Timeout policy; advance clock
- **Assert:** `external-job-stop` outbox row dispatched; lifecycle shows timeout

### INT-JS-006 — Queue quota across definitions (integrated)
- **Priority:** P0 | **AC:** JS-AC-007, AC-518 | **Status:** Missing
- **Act:** Two DAG runs share pool `eks-cluster`; N=2
- **Assert:** At most 2 `external-job-start` in flight in PG + dispatcher

### INT-JS-007 — Ticket held across host restart
- **Priority:** P0 | **AC:** JS-AC-010 | **Status:** Missing
- **Act:** Job holds ticket; restart host; query pool in PG
- **Assert:** Ticket still held; capacity reduced

### INT-JS-008 — Ticket released on success/fail/timeout/cancel
- **Priority:** P0 | **AC:** JS-AC-011, AC-520 | **Status:** Partial
- **Four integration tests** for each terminal path on PG stack

### INT-JS-009 — Multi-pool atomic acquire (db-A + db-B)
- **Priority:** P0 | **AC:** JS-AC-012, AC-522 | **Status:** Missing
- **Act:** Job needs two pools; B exhausted
- **Assert:** No partial hold on A

### INT-JS-010 — Queued job consumes no quota slot
- **Priority:** P1 | **AC:** JS-AC-013 | **Status:** Partial
- **Act:** Job waiting in PG; inspect pool occupancy
- **Assert:** Waiting job not counted as active ticket holder

### INT-JS-011 — Cancel run propagates stop to all jobs
- **Priority:** P0 | **AC:** JS-AC-009 | **Status:** Missing
- **Stack:** PG + RabbitMQ
- **Act:** 3 running jobs; cancel parent run
- **Assert:** 3 stop messages; jobs terminal

### INT-JS-012 — DAG observability under load
- **Priority:** P1 | **AC:** JS-AC-005 | **Status:** Missing
- **Act:** 20-node DAG; `ReconstructDagRunAsync`
- **Assert:** Completes within time budget (NF-030); node timings present

### INT-JS-013 — Scheduled start idempotent (host cron simulation)
- **Priority:** P1 | **AC:** JS-AC-008 | **Status:** Missing (waived)
- **Act:** Two host triggers same occurrence key
- **Assert:** Single run instance in PG

### INT-JS-014 — Pause run with in-flight jobs
- **Priority:** P1 | **AC:** MG-013 | **Status:** Missing
- **Act:** Pause DAG run; job completions arrive
- **Assert:** Buffered in PG; run does not advance

### INT-JS-015 — Continue-as-new on long-running scheduler run
- **Priority:** P2 | **AC:** AC-313 | **Status:** Missing
- **Act:** History rollover mid-DAG
- **Assert:** Lineage preserved; DAG reconstruct still works

### INT-JS-016 — Heterogeneous DAG definitions end-to-end
- **Priority:** P0 | **AC:** JS-AC-001 | **Status:** Missing
- **Act:** Each node different child definition
- **Assert:** Outbox child-start carries correct definition per node

### INT-JS-017 — K8s watcher duplicate JobSucceeded (inbox dedup)
- **Priority:** P1 | **AC:** EV-031 | **Status:** Missing
- **Act:** Ingest duplicate completion through host inbox path
- **Assert:** Single job completion in stream

### INT-JS-018 — Full scenario soak: 1 hour fake clock DAG
- **Priority:** P2 | **Status:** Missing
- **Act:** Advance `FakeTimeProvider` through waves, timeouts, one failure, cancel one branch
- **Assert:** Consistent final observability snapshot

---

## Reference architecture for tests

```
┌─────────────┐     outbox      ┌──────────┐
│  IHost      │ ──────────────► │ RabbitMQ │
│  + Processor│                 └────┬─────┘
│  + Pump     │                      │ fake K8s / test consumer
└──────┬──────┘                      ▼
       │                      JobSucceeded envelope
       ▼                              │
┌─────────────┐ ◄─────────────────────┘
│ PostgreSQL  │   inbox + event store
└─────────────┘
```

**Test harness:** Background service in test assembly subscribes to queue, records messages,
injects completion events via `DurableCommandProcessor` or public ingest API when available.

## Priority order

1. INT-JS-003, INT-JS-004 (core submit-and-wait)
2. INT-JS-001, INT-JS-016 (DAG correctness)
3. INT-JS-006, INT-JS-009 (quota)
4. INT-JS-005, INT-JS-011 (timeout + cancel)
5. INT-JS-012 (observability perf)

## New test class layout

```
OrcaCore.Integration.Tests/JobScheduler/
  DagPostgreSqlIntegrationTests.cs
  ExternalJobStackIntegrationTests.cs
  SchedulerRestartIntegrationTests.cs
  QuotaFairnessIntegrationTests.cs
```
