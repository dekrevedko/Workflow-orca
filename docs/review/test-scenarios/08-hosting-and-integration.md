# Hosting & Integration — Negative Tests & Edge Cases

Scope: `OrcaCore.Hosting`, `OrcaCore.SampleHost`, DI registration, hosted services (outbox pump,
timer sweep, operational sweep), cross-layer wiring. Requirements: MG-*, DU-032, R7 findings.

## Existing negative coverage (reference)

| Area | Test(s) | What is proven |
|------|---------|----------------|
| DI resolves core services | `OrcaCoreHostingServiceCollectionTests` | Engine, store, management registered |
| Hosting not referencing RabbitMQ | `RepositoryGuardTests` | No direct RabbitMQ dep in Hosting csproj |
| Sample host smoke | `SampleHostSmokeTests` | Host builds/starts minimally |

---

## Missed negative tests

### NEG-HO-001 — AddOrcaCore without store registered
- **Priority:** P1 | **Status:** Missing
- **Given** incomplete `ServiceCollection`
- **When** resolve `DurableCommandProcessor`
- **Then** DI exception at resolve time with clear message

### NEG-HO-002 — Duplicate provider registration
- **Priority:** P2 | **Status:** Missing
- **Given** two `IWorkflowEventStore` registrations
- **When** resolve
- **Then** last wins or throw — document behavior

### NEG-HO-003 — Hosted services not registered
- **Priority:** P1 | **Status:** Missing
- **Given** `AddOrcaCore` without `AddOrcaCoreHostedServices`
- **When** host runs
- **Then** outbox not pumped; test observes zero dispatch

### NEG-HO-004 — Outbox pump hosted service no-op (R7)
- **Priority:** P0 | **Status:** Missing
- **Given** committed outbox row
- **When** host runs one tick
- **Then** should dispatch — **currently fails / no-op**

### NEG-HO-005 — Timer hosted service without scheduler
- **Priority:** P1 | **Status:** Missing
- **Given** missing `ITimerScheduler` registration
- **When** host starts
- **Then** fail fast or skip with log

### NEG-HO-006 — Invalid configuration section
- **Priority:** P1 | **Status:** Missing
- **Given** `appsettings` missing connection string
- **When** host starts with PostgreSQL provider
- **Then** startup failure with config path in message

### NEG-HO-007 — Host shutdown mid-pump
- **Priority:** P1 | **AC:** AC-316 | **Status:** Missing
- **Given** pump processing batch
- **When** `IHostApplicationLifetime.StopApplication`
- **Then** no duplicate dispatch; safe stop

### NEG-HO-008 — Host shutdown mid-command
- **Priority:** P1 | **AC:** AC-316 | **Status:** Missing
- **Given** command mid-append
- **When** kill host
- **Then** either committed or not; no partial apply

### NEG-HO-009 — Sample host wrong engine mode
- **Priority:** P2 | **Status:** Missing
- **Given** config ephemeral vs durable mismatch
- **When** start workflow
- **Then** clear error

### NEG-HO-010 — Operational sweep on empty store
- **Priority:** P2 | **Status:** Missing
- **When** sweep runs
- **Then** completes; no throw

### NEG-HO-011 — Two hosted pumps duplicate registration
- **Priority:** P1 | **Status:** Missing
- **Given** `AddOrcaCoreHostedServices` called twice
- **When** run host
- **Then** single pump instance or explicit throw

### NEG-HO-012 — OpenTelemetry exporter misconfigured
- **Priority:** P2 | **AC:** OB-001 | **Status:** Missing (future)
- **When** OTel endpoint invalid
- **Then** host starts; exporter fails gracefully without crashing engine

---

## Edge-case scenarios

### EDGE-HO-001 — Full pipeline: start → wait → event → complete
- **Priority:** P0 | **Status:** Missing
- **Given** generic host with in-memory provider
- **When** workflow runs end-to-end through hosted activation
- **Then** terminal state reachable without manual processor calls

### EDGE-HO-002 — Outbox pump batch size boundary
- **Priority:** P1 | **AC:** DU-032 | **Status:** Missing
- **Given** exactly `maxCount` due rows
- **When** one pump cycle
- **Then** all claimed once

### EDGE-HO-003 — Timer pump due at startup
- **Priority:** P1 | **AC:** EV-050 | **Status:** Missing
- **Given** overdue timer persisted
- **When** host starts
- **Then** timer fires within first sweep

### EDGE-HO-004 — Concurrent host instances same DB (AC-315)
- **Priority:** P0 | **AC:** AC-315 | **Status:** Missing
- **Given** two hosts one PostgreSQL
- **When** both process same instance
- **Then** one mutator wins

### EDGE-HO-005 — Host recovers after provider transient outage
- **Priority:** P1 | **Status:** Missing
- **Given** DB connection drops mid-run
- **When** retry policy on host
- **Then** resumes without corrupt state

### EDGE-HO-006 — Configuration reload (if supported)
- **Priority:** P2 | **Status:** Missing
- **When** connection string changes live
- **Then** documented: restart required or hot reload safe

### EDGE-HO-007 — Benchmark host isolation
- **Priority:** P3 | **Status:** Missing
- **Given** benchmarks project
- **When** run
- **Then** does not require Docker/RabbitMQ

### EDGE-HO-008 — Sample host with RabbitMQ optional package
- **Priority:** P2 | **Status:** Missing
- **Given** user adds RabbitMQ provider only in app
- **When** sample runs
- **Then** dispatcher resolves from app DI not Hosting

### EDGE-HO-009 — Graceful degradation: projection store optional
- **Priority:** P2 | **Status:** Missing
- **Given** event store only, no projection
- **When** management query
- **Then** falls back or clear not-configured

### EDGE-HO-010 — Health check reflects outbox backlog
- **Priority:** P1 | **AC:** OB-010 (future) | **Status:** Missing
- **Given** growing outbox
- **When** health endpoint
- **Then** degraded status

### EDGE-HO-011 — Hosted service exception isolation
- **Priority:** P1 | **Status:** Missing
- **Given** pump throws once
- **When** next cycle
- **Then** host continues; metric incremented

### EDGE-HO-012 — Engine singleton vs scoped lifetime
- **Priority:** P1 | **Status:** Missing
- **Given** scoped service resolves engine
- **When** two scopes
- **Then** same singleton instance for command serialization
