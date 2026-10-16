# Hosting & Hosted Services — Integration Scenarios

> [!IMPORTANT]
> **SUPERSEDED ROUTING NOTICE (2026-07-19):** The catch-all `AddOrcaCore`, separate
> `AddOrcaCoreHostedServices`, PostgreSQL aliases, and a host exposing both engines are rejected v1
> paths. Historical test names below are provenance only and do not satisfy the active gate. Active
> scenarios use exactly one engine role (or callback-only ingress), an explicit complete provider
> role where durable persistence is required, and that role's owned `IWorkflowEventClient`.

Task 4.0 remains blocked until all task-3 guards are retargeted, executed, and independently
re-reviewed. Updating this inventory does not mark any guard or product behavior implemented.

**Active components:** `OrcaCore.Hosting.OrcaCoreEphemeralEngineServiceCollectionExtensions`,
`OrcaCore.Hosting.OrcaCoreDurableEngineServiceCollectionExtensions`,
`OrcaCore.Providers.InMemory.OrcaCoreInMemoryProviderServiceCollectionExtensions`,
`OrcaCore.Providers.PostgreSql.OrcaCorePostgreSqlProviderServiceCollectionExtensions`,
`OrcaCore.Dag.Hosting`, `IHost`, the selected role's `IWorkflowEventClient`, and internal hosted
outbox/timer/continuation/resource-governance loops.

## Historical coverage (not active-contract evidence)

| Test | Project | What is wired |
|------|---------|---------------|
| `AddOrcaCore_RegistersCoreEnginesAndInMemoryDefaults` | Hosting.Tests | Superseded catch-all/both-engine/default-provider behavior; replace, do not preserve |
| `HostedOutboxPump_StartsHostAndDispatchesCommittedOutboxRecords` | Hosting.Tests | `IHost` + recording fake store + real `DurableOutboxPump` |
| `HostedTimerService_StartsHostAndFiresDueDurableTimers` | Hosting.Tests | Host + fake scheduler + real `DurableCommandProcessor` |
| `HostedOperationalSweep_StartsHostAndExpiresResourcePoolTickets` | Hosting.Tests | Superseded time-expiry semantics; replace with review-mark/reconciliation coverage |
| `SampleHost_StartsWithInMemoryProvider_ResolvesHostedServices` | Hosting.Tests | Historical DI smoke; retarget to explicit durable role + in-memory provider |

These names may remain in history while replacement work is pending, but an active implementation
must rename/delete obsolete tests rather than weaken the exact registration and no-time-reclaim
contract to keep them green.

---

## Active missing integration scenarios

### INT-HO-001 — Sample host runs workflow to completion
- **Priority:** P0 | **AC:** AC-001, PR-040 | **Status:** Missing
- **Components:** `SampleHostApplication` +
  `AddOrcaCoreEphemeralEngine(EphemeralEngineHostOptions)` + ephemeral definition registry/event
  routing
- **Setup:** Register one straight-line typed definition through the public registry
- **Act:** Inspect the registration/start unions, use `GetHandleOrThrow()` on the success path, and
  `await start.WaitForOutputAsync(token)`
- **Assert:** Typed output completes without casts, snapshot polling, or a direct processor call;
  the resolved `IWorkflowEventClient` belongs to the ephemeral role and durable services are absent

### INT-HO-002 — Outbox pump drains commit from processor (not manual append)
- **Priority:** P0 | **AC:** AC-310, DU-032 | **Status:** Missing
- **Components:** Host + `AddOrcaCoreDurableEngine(DurableEngineHostOptions)` +
  `AddOrcaCoreInMemoryDurableProvider()` + real durable start path/pump
- **Act:** Start a public typed workflow whose committed step emits an outbox row; start host
- **Assert:** Dispatcher received payload; outbox `Dispatched` — **no direct `AppendAsync` in test**

### INT-HO-003 — Timer host fires wait timeout end-to-end
- **Priority:** P0 | **AC:** AC-111, EV-050 | **Status:** Partial (manual `ScheduleTimerCommand`)
- **Components:** Exact durable-engine role + complete in-memory provider + scheduler + timer hosted
  service
- **Act:** Start workflow with durable wait+timeout; advance `FakeTimeProvider`; host tick
- **Assert:** `WorkflowTimerFiredEvent` + instance advanced via interpreter path; any completion wait
  wakes by notification/recheck and never a test polling loop

### INT-HO-004 — Graceful host shutdown completes pump cycle
- **Priority:** P1 | **AC:** AC-316 | **Status:** Missing
- **Components:** Durable-engine host with one complete provider role and short pump interval
- **Act:** `StopAsync` while outbox batch in flight
- **Assert:** No record is lost: either `Dispatched` committed or a safe at-least-once retry remains
  after restart; the test does not claim exactly-once external delivery

### INT-HO-005 — Host shutdown mid-processor command
- **Priority:** P1 | **AC:** AC-316 | **Status:** Missing
- **Components:** Durable-engine host + certified provider-test seam; inject slow append fake
- **Act:** Kill host during append
- **Assert:** Restart + retry → single committed event

### INT-HO-006 — Operational review-mark + reconciliation lifecycle event
- **Priority:** P1 | **AC:** AC-521, MG-064 | **Status:** Partial
- **Components:** Exact durable-engine role + complete in-memory provider + exact workflow
  obligation/owner occurrence + held ticket past its pool review deadline
- **Act:** Advance clock; run mark/reconciliation for live, retryable-ambiguous, exhausted/exit
  ambiguity, proven-released, bare-terminal, terminal-with-confirmed-stop/fence, and missing-ticket
  owner states. Crash/restart permutations may stop only at friend-only barriers
  `WorkflowPendingObligationCommitted`, `GovernanceReservationCommitted`,
  `WorkflowActivationCommitted`, and `GovernanceOwnershipConfirmed`, using immutable correlated
  facts rather than a public hook
- **Assert:** Review marking retains capacity; retryable ambiguity stays `AmbiguousHeld` under the
  same operation/token/tickets while retrying; ambiguity transfers atomically to `Quarantined`
  before parent/join progression when exit/terminal/exhaustion wins; only causal stop/fence proof
  releases once; missing expected ticket produces `LeaseLost`; elapsed time never grants a waiter

### INT-HO-007 — Exact PostgreSQL durable-provider host profile
- **Priority:** P0 | **AC:** PR-040 | **Status:** Missing
- **Components:** Host + `AddOrcaCoreDurableEngine(DurableEngineHostOptions)` +
  `OrcaCorePostgreSqlProviderServiceCollectionExtensions.AddOrcaCorePostgreSqlDurableProvider(postgreSqlOptions)`
- **Act:** Start a typed workflow using Testcontainers, rebuild the host with the same provider
  options whose required get-only values are `ConnectionString` and `Schema`, and reattach
- **Assert:** One call supplied the complete certified production role; get-only nonblank
  `ConnectionString`/`Schema` were copied and validated before services became visible; the workflow
  survives rebuild. No alias, raw-string overload, binder API, or per-port application wiring exists

### INT-HO-008 — Hosted services idempotent registration
- **Priority:** P2 | **Status:** Missing
- **Act:** Call the same exact engine method twice with equal options, then separately exercise
  conflicting options/roles and duplicate provider registration
- **Assert:** Identical role/options are idempotent with one loop set; conflicts fail deterministically.
  Reflection proves the ephemeral and durable methods have their distinct owning extension classes
  and no catch-all/toggle registration exists

### INT-HO-009 — Outbox pump batch size boundary
- **Priority:** P1 | **AC:** DU-032 | **Status:** Missing
- **Setup:** Exact durable role + complete provider; 15 due outbox rows;
  `OutboxPumpBatchSize = 10`
- **Act:** Two pump cycles via host
- **Assert:** All 15 reach `Dispatched`; batch boundaries preserve at-least-once delivery and the
  documented requirement that external consumers remain idempotent

### INT-HO-010 — Timer sweep with no due timers
- **Priority:** P2 | **AC:** EV-050 | **Status:** Missing
- **Act:** Start an exact durable-engine host + one complete provider and advance one timer tick
- **Assert:** No `FireTimerCommand` processed; no error

### INT-HO-011 — FakeTimeProvider drives all three hosted intervals
- **Priority:** P1 | **AC:** NF-020 | **Status:** Partial
- **Setup:** Exact durable role + complete provider + shared `FakeTimeProvider`
- **Act:** Advance past outbox, timer, sweep intervals
- **Assert:** All three subsystems ran without `Task.Delay`

### INT-HO-012 — Sample host with durable wait + external event injection
- **Priority:** P1 | **AC:** AC-301 | **Status:** Missing
- **Components:** Execution host with `AddOrcaCoreDurableEngine(DurableEngineHostOptions)` +
  PostgreSQL provider; separate definition-less callback host with
  `AddOrcaCoreDurableEventIngress()` + the same complete provider
- **Act:** Start durable wait, restart the execution host, then deliver through the callback host's
  durable `IWorkflowEventClient`
- **Assert:** Durable persistence/continuation handoff resumes the workflow. The callback host has no
  registry, execution worker, timer/reconciler, or DAG coordinator; pre-wait `NoActiveWait` does
  not consume `EventId`, so same-envelope redelivery after registration can be accepted

### INT-HO-013 — One service provider rejects both engine roles
- **Priority:** P0 | **AC:** MG-011 | **Status:** Missing
- **Act:** Configure both `AddOrcaCoreEphemeralEngine(EphemeralEngineHostOptions)` and
  `AddOrcaCoreDurableEngine(DurableEngineHostOptions)` in one service collection in both orders
- **Assert:** Registration/startup fails deterministically; no last-registration-wins registry or
  `IWorkflowEventClient` routing ownership is observable

### INT-HO-014 — CancellationToken stops hosted loops
- **Priority:** P1 | **Status:** Missing
- **Act:** Stop an exact durable-engine host during an internal scheduled-loop wait
- **Assert:** `ExecuteAsync` exits; no orphaned timers

### INT-HO-015 — Outbox pump observer/metrics hook (future OB)
- **Priority:** Deferred | **AC:** OB-010 | **Status:** Not an active v1 hosting gate
- **Components:** Host + `IOutboxPumpObserver` when implemented
- **Assert:** Success/failure/latency recorded per batch

### INT-HO-016 — DAG terminal wait and parked-node admission
- **Priority:** P1 | **Status:** Missing
- **Components:** Exact durable role + complete provider +
  `OrcaCore.Dag.Hosting.AddOrcaCoreDag(DagHostOptions)`
- **Act:** Start a typed DAG through its inspectable start union; park one admitted child in a wait
  at the `MaxConcurrentNodes` ceiling and await `DagRunHandle.WaitForTerminalAsync(token)`
- **Assert:** The parked nonterminal child continues to consume admission; the terminal wait uses
  notification/recheck without polling; no visualization service/projection is registered

---

## Fixture notes

Retarget the historical `OrcaCoreHostingServiceCollectionTests.BuildHost` pattern before reuse:

- Select exactly one engine registration, or the callback-only ingress role; never both engines
- Register `FakeTimeProvider` as `TimeProvider`
- Exercise the **real** internal durable processor/pump through the public registry/start/event
  surface; do not resolve or invoke a processor from ordinary application code
- Compose durable tests with one complete provider role. Use
  `AddOrcaCoreInMemoryDurableProvider()` only for development/test semantics
- For the production profile, use shared `PostgreSqlOrcaFixture` and exact
  `AddOrcaCorePostgreSqlDurableProvider(PostgreSqlDurableProviderOptions)` (see
  [07-harness-ci-and-fixtures.md](07-harness-ci-and-fixtures.md))
- Phase 0 package consumers restore exact `0.0.0-phase0` artifacts from
  `artifacts/phase0-packages` through `PackageReference` only

## Priority order for implementation

1. INT-HO-013, INT-HO-007, INT-HO-008 (lock role/provider ownership and exact registrations)
2. INT-HO-001, INT-HO-002, INT-HO-003, INT-HO-012 (prove public execution and split-host routing)
3. INT-HO-004, INT-HO-005, INT-HO-006 (shutdown and capacity-safety lifecycles)
4. INT-HO-009, INT-HO-011, INT-HO-016 (operational bounds and reactive waiting)
