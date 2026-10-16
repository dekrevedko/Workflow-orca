# Multi-Node & Restart — Integration Scenarios

**Components:** Process boundary, multiple `IHost` instances, shared PostgreSQL (or SQL Server),
optimistic concurrency, idempotency stores, inbox/outbox leasing. Covers **AC-315** (advanced,
currently waived) and restart semantics **AC-301, AC-302, AC-316, JS-AC-005**.

## Existing coverage

| Test | What it simulates | Limitation |
|------|-------------------|------------|
| `DurableRecoveryTests.WaitingInstance_RehydrateAfterRestart` | New processor object | Same process, InMemory |
| `PostgreSql_DurableWaitSurvivesRestart` | New store connection | Store API only |
| `R4_StartOrGet_RestartUsesDurableIdempotencyKey` | New starter | InMemory |
| `HostedOutboxPump_*` | Host start/stop | Single host, fake store |

**No test today spins up two hosts or two processes against one database.**

---

## Missing integration scenarios

### INT-MN-001 — Two hosts one instance: racing deliver event
- **Priority:** P0 | **AC:** AC-315, AC-309 | **Status:** Missing
- **Components:** `Host A` + `Host B` + shared PG + same `InstanceId`
- **Act:** Both call `ProcessAsync(DeliverEvent)` concurrently
- **Assert:** Exactly one `WorkflowWaitMatchedEvent`; other conflict/no-op

### INT-MN-002 — Two hosts: racing append step completion
- **Priority:** P0 | **AC:** AC-315 | **Status:** Missing
- **Act:** Parallel `DurableStepCompletedCommand` from two hosts
- **Assert:** Single version increment chain; no forked stream

### INT-MN-003 — Host A commits; Host B loads stale checkpoint
- **Priority:** P1 | **AC:** DU-013 | **Status:** Missing
- **Act:** A appends v5; B loaded checkpoint at v3; B attempts append expecting v3
- **Assert:** B gets conflict with actual v5

### INT-MN-004 — Outbox claim lease: only one host dispatches row
- **Priority:** P0 | **AC:** DU-032, AC-315 | **Status:** Missing
- **Components:** Two hosts pumping same PG outbox
- **Act:** Parallel `PumpOnceAsync`
- **Assert:** Each outbox row dispatched once total

### INT-MN-005 — Timer claim lease: single fire per timer id
- **Priority:** P0 | **AC:** EV-050, AC-315 | **Status:** Missing
- **Act:** Two timer hosted services claim same due timer
- **Assert:** One `WorkflowTimerFiredEvent`

### INT-MN-006 — Process exit mid-append; new process resumes
- **Priority:** P0 | **AC:** AC-302, AC-316 | **Status:** Missing
- **Simulation:** Dispose processor mid-command without `StopAsync`; new process
- **Assert:** Last committed version intact; retry safe

### INT-MN-007 — Process exit mid-pump; at-least-once dispatch
- **Priority:** P1 | **AC:** DU-032 | **Status:** Missing
- **Act:** Kill during `DispatchAsync` after broker accept but before mark Dispatched
- **Assert:** Redispatch safe; consumer dedupes

### INT-MN-008 — Rolling deploy: old host drains; new host takes work
- **Priority:** P1 | **Status:** Missing
- **Act:** Host A processing; start Host B; stop A; B continues instance
- **Assert:** No stuck `Running` without mutator

### INT-MN-009 — StartOrGet from two hosts same millisecond
- **Priority:** P0 | **AC:** AC-311 | **Status:** Missing
- **Act:** Parallel `StartOrGetAsync` same key
- **Assert:** One instance; PG idempotency unique constraint enforced

### INT-MN-010 — Inbox dedup across hosts
- **Priority:** P0 | **AC:** AC-305 | **Status:** Partial
- **Act:** Host A applies EventId; Host B receives duplicate
- **Assert:** B no-op; no double stream events

### INT-MN-011 — Projection eventual consistency across hosts
- **Priority:** P1 | **AC:** DU-070 | **Status:** Missing
- **Act:** Host A commits; Host B queries before projection apply completes
- **Assert:** Documented consistency window or read-your-writes via same connection

### INT-MN-012 — Pool acquire cross-host FIFO
- **Priority:** P1 | **AC:** AC-519, JS-AC-007 | **Status:** Missing
- **Act:** Host A and B queue for last ticket
- **Assert:** Global FIFO order in PG

### INT-MN-013 — Child deterministic ids across host restart
- **Priority:** P0 | **AC:** AC-607 | **Status:** Missing
- **Act:** Host A starts children; crash; Host B resumes group
- **Assert:** No duplicate child instance ids in PG streams

### INT-MN-014 — Pause on Host A; resume only on Host B
- **Priority:** P1 | **AC:** AC-515 | **Status:** Missing
- **Act:** Pause; stop A; start B; resume
- **Assert:** Still paused until explicit resume; buffered events intact in PG

### INT-MN-015 — Eviction + rehydrate across hosts (when activation exists)
- **Priority:** P2 | **AC:** AC-504, AC-506 | **Status:** Missing
- **Act:** Host A evicts idle; Host B receives event
- **Assert:** B rehydrates and processes; no dual mutator

---

## Fixture: `MultiNodeFixture`

```csharp
public sealed class MultiNodeFixture : IAsyncLifetime
{
    public PostgreSqlContainer Database { get; }
    public IHost CreateHost(string name) =>
        Host.CreateApplicationBuilder()
            .ConfigureServices(s => s.AddOrcaCorePostgreSql(Database.GetConnectionString()))
            .AddOrcaCoreHostedServices()
            .Build();
}
```

Run tests sequentially within collection to avoid port conflicts; use parallel only for
in-process processor races (INT-MN-001) without two full hosts if startup cost is high.

## CI note

Mark `[Trait("Category", "Integration")]` + `[Trait("Category", "MultiNode")]`.
Run nightly — slower and flakier if containers undersized.

## Waiver update

When INT-MN-001…005 pass, remove `AC-315` from `RepositoryGuardTests.AcceptanceCriterionWaivers`.

## Priority order

1. INT-MN-001, INT-MN-004, INT-MN-009 (core multi-writer safety)
2. INT-MN-006, INT-MN-013 (restart)
3. INT-MN-014, INT-MN-012 (operational)
4. INT-MN-008 (deploy simulation)
