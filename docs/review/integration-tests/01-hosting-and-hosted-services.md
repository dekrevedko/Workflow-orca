# Hosting & Hosted Services — Integration Scenarios

**Components:** `OrcaCore.Hosting`, `OrcaCore.SampleHost`, `IHost`, `BackgroundService` loop,
`DurableOutboxPump`, `OrcaCoreTimerHostedService`, `OrcaCoreOperationalSweepHostedService`.

## Existing coverage

| Test | Project | What is wired |
|------|---------|---------------|
| `AddOrcaCore_RegistersCoreEnginesAndInMemoryDefaults` | Hosting.Tests | DI only |
| `HostedOutboxPump_StartsHostAndDispatchesCommittedOutboxRecords` | Hosting.Tests | `IHost` + recording fake store + real `DurableOutboxPump` |
| `HostedTimerService_StartsHostAndFiresDueDurableTimers` | Hosting.Tests | Host + fake scheduler + real `DurableCommandProcessor` |
| `HostedOperationalSweep_StartsHostAndExpiresResourcePoolTickets` | Hosting.Tests | Host + recording pool store |
| `SampleHost_StartsWithInMemoryProvider_ResolvesHostedServices` | Hosting.Tests | Sample host DI smoke |

---

## Missing integration scenarios

### INT-HO-001 — Sample host runs workflow to completion
- **Priority:** P0 | **AC:** AC-001, PR-040 | **Status:** Missing
- **Components:** `SampleHostApplication` → `EphemeralWorkflowEngine` → registered definition
- **Setup:** Register straight-line definition in host startup hook (test override)
- **Act:** `StartAsync` via resolved engine
- **Assert:** `Completed` without calling `DurableCommandProcessor` directly

### INT-HO-002 — Outbox pump drains commit from processor (not manual append)
- **Priority:** P0 | **AC:** AC-310, DU-032 | **Status:** Missing
- **Components:** Host + `DurableCommandProcessor` + `InMemoryWorkflowProvider` + pump
- **Act:** `ProcessAsync` command that emits outbox row; start host
- **Assert:** Dispatcher received payload; outbox `Dispatched` — **no direct `AppendAsync` in test**

### INT-HO-003 — Timer host fires wait timeout end-to-end
- **Priority:** P0 | **AC:** AC-111, EV-050 | **Status:** Partial (manual `ScheduleTimerCommand`)
- **Components:** Host + processor + scheduler + timer hosted service
- **Act:** Start workflow with durable wait+timeout; advance `FakeTimeProvider`; host tick
- **Assert:** `WorkflowTimerFiredEvent` + instance advanced via interpreter path

### INT-HO-004 — Graceful host shutdown completes pump cycle
- **Priority:** P1 | **AC:** AC-316 | **Status:** Missing
- **Components:** Host with short pump interval
- **Act:** `StopAsync` while outbox batch in flight
- **Assert:** No duplicate dispatch; either committed `Dispatched` or safe retry on restart

### INT-HO-005 — Host shutdown mid-processor command
- **Priority:** P1 | **AC:** AC-316 | **Status:** Missing
- **Components:** Host + processor; inject slow append fake
- **Act:** Kill host during append
- **Assert:** Restart + retry → single committed event

### INT-HO-006 — Operational sweep + pool expiry lifecycle event
- **Priority:** P1 | **AC:** AC-521, MG-064 | **Status:** Partial
- **Components:** Host + real `InMemoryResourcePoolStore` + held ticket past expiry
- **Act:** Advance clock; run sweep hosted service
- **Assert:** Ticket expired; lifecycle/query reflects expiry (not only `ExpiredAt` TCS)

### INT-HO-007 — `AddOrcaCorePostgreSql` host profile (when exists)
- **Priority:** P0 | **AC:** PR-040 | **Status:** Missing
- **Components:** Host + `AddOrcaCore` + PostgreSQL extensions replacing InMemory
- **Act:** Start host with Testcontainers connection string
- **Assert:** `IWorkflowEventStore` is `PostgreSqlWorkflowStore`; workflow survives host rebuild

### INT-HO-008 — Hosted services idempotent registration
- **Priority:** P2 | **Status:** Missing
- **Act:** `AddOrcaCoreHostedServices()` twice
- **Assert:** Single pump/timer/sweep execution per tick (or documented throw)

### INT-HO-009 — Outbox pump batch size boundary
- **Priority:** P1 | **AC:** DU-032 | **Status:** Missing
- **Setup:** 15 due outbox rows; `OutboxPumpBatchSize = 10`
- **Act:** Two pump cycles via host
- **Assert:** All 15 dispatched exactly once

### INT-HO-010 — Timer sweep with no due timers
- **Priority:** P2 | **AC:** EV-050 | **Status:** Missing
- **Act:** Host start + one tick
- **Assert:** No `FireTimerCommand` processed; no error

### INT-HO-011 — FakeTimeProvider drives all three hosted intervals
- **Priority:** P1 | **AC:** NF-020 | **Status:** Partial
- **Setup:** Shared `FakeTimeProvider` registered in host
- **Act:** Advance past outbox, timer, sweep intervals
- **Assert:** All three subsystems ran without `Task.Delay`

### INT-HO-012 — Sample host with durable wait + external event injection
- **Priority:** P1 | **AC:** AC-301 | **Status:** Missing
- **Components:** Sample host + durable processor + management query
- **Act:** Start durable wait; stop/start host; deliver event via processor
- **Assert:** Management query shows resumed state

### INT-HO-013 — Host exposes both engines; durable path not used by ephemeral API
- **Priority:** P2 | **AC:** MG-011 | **Status:** Missing
- **Act:** Ephemeral start + durable start different instances
- **Assert:** Projection lists both; no cross-contamination

### INT-HO-014 — CancellationToken stops hosted loops
- **Priority:** P1 | **Status:** Missing
- **Act:** `host.StopAsync` during `PeriodicTimer` wait
- **Assert:** `ExecuteAsync` exits; no orphaned timers

### INT-HO-015 — Outbox pump observer/metrics hook (future OB)
- **Priority:** P2 | **AC:** OB-010 | **Status:** Missing
- **Components:** Host + `IOutboxPumpObserver` when implemented
- **Assert:** Success/failure/latency recorded per batch

---

## Fixture notes

Reuse pattern from `OrcaCoreHostingServiceCollectionTests.BuildHost`:

- Register `FakeTimeProvider` as `TimeProvider`
- Prefer **real** `DurableCommandProcessor` + `DurableOutboxPump` always
- Swap `RecordingWorkflowProvider` → `InMemoryWorkflowProvider` for less brittle tests
- For PostgreSQL profile, use shared `PostgreSqlOrcaFixture` (see [07-harness-ci-and-fixtures.md](07-harness-ci-and-fixtures.md))

## Priority order for implementation

1. INT-HO-002, INT-HO-003 (close gap between manual append and real command path)
2. INT-HO-001, INT-HO-007 (sample host proves operator path)
3. INT-HO-004, INT-HO-005 (shutdown safety)
4. INT-HO-009, INT-HO-011 (operational hardening)
