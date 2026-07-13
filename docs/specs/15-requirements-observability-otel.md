# 15. OpenTelemetry Observability & Logging (OB)

Scope: runtime **telemetry** — structured logs, metrics, and distributed traces — that powers
operator **system dashboards** with deep insight into **running workflows**. This document
complements, but does not replace, the **product observability** surface in document 09
(`Statistics()`, lifecycle events, projections, DAG reconstruction — MG-030/031/032).

**Primary goal:** an operator viewing a Grafana / Datadog / Azure Monitor / similar dashboard
SHALL be able to answer, without ad-hoc host instrumentation:

- How many workflows are running, waiting, stuck, or failing — and **for which definitions**?
- What are they **blocked on** (wait event names, timers, resource pools)?
- Is the engine **healthy** (outbox backlog, commit latency, dispatch failures)?
- **Which instances** explain a spike — and what happened on each (logs correlated to metrics)?
- For DAG/child runs: how is the **run progressing** node by node?

---

## 15.0 active implementation review (2026-07-03)

Assessment of `` against IOQ-5 (`docs/implementation/00-stack-decisions.md`) and
MG/DU observability requirements. This section is **provenance only**; normative requirements
follow in §15.1+.

### What exists today (product observability)

| Area | Status | Notes |
|------|--------|-------|
| Management statistics | **Partial** | `Statistics()` on ephemeral management; `GetStatisticsAsync` on durable projection store. Grouped counts by definition/version/status. |
| Pressure metrics | **Partial** | `WorkflowPressureMetrics` (stream events, checkpoints, pending outbox, active instances) in PostgreSQL/in-memory providers. Missing checkpoint **lag** and payload-size pressure (DU-052). |
| Ephemeral statistics richness | **Ahead of abstractions** | Ephemeral `WorkflowStatistics` includes `ActiveWaitsByEventName`, `OldestActiveInstanceAge`, `StuckCount`; durable abstraction type does not — type split in `EphemeralManagement.cs` vs `OrcaCore.Abstractions`. |
| Lifecycle events | **Good** | `LifecycleEventSnapshot` with `Durable` flag; ephemeral in-process; durable committed as outbox `"lifecycle-event"` records in same commit (`DurableCommandProcessor.CreateLifecycleOutboxRecords`). |
| DAG run inspection | **Good** | `DurableManagement.ReconstructDagRunAsync` — node status/timings from projections + child schedule events (JS-005). Loads full stream tail — scalability concern (R7). |
| Stuck detection | **Good** | Lifecycle events + queryable `IsStuck` / `HasStuckStep` on snapshots. |

### What is missing (runtime telemetry)

| Area | Status | Gap |
|------|--------|-----|
| `ILogger` / `[LoggerMessage]` | **Absent** | Zero usages under `src/` (R8 P2). Only `OrcaCore.Hosting` references `Logging.Abstractions`; no log calls. |
| `ActivitySource` (traces) | **Absent** | No spans on command processing, commits, outbox dispatch, or step execution. |
| `Meter` (metrics) | **Absent** | No runtime instruments; dashboard consumers cannot scrape OrcaCore-native signals. |
| OpenTelemetry SDK | **Absent** | No OTel packages in `Directory.Packages.props`; hosting does not wire exporters. |
| Outbox pump observability hooks | **Absent** | `DurableOutboxPump` has no `IOutboxPumpObserver`-equivalent (required by DU-032/PR-014). Prior `src/OrcaCore.Runtime` had observer interface; current implementation does not. |
| Hosted pump/timer services | **Stub** | `OrcaCoreOutboxPumpHostedService` is a no-op (R7 P0) — even if metrics existed, background paths are not live in sample host. |
| Log ↔ metric correlation | **Absent** | No shared attribute model, trace context propagation, or exemplars. |

### Conclusion

current implementation delivers **queryable workflow state** suitable for management APIs and application-level
dashboards built on `Statistics()` / projections, but **does not yet emit OTel logs or metrics**.
Hosts cannot populate a system dashboard from OrcaCore instrumentation alone. Implementation
of this document closes that gap while preserving the IOQ-5 boundary: BCL diagnostics in
core/engines/providers; OTel SDK wiring only in `OrcaCore.Hosting`.

---

## 15.1 Architecture boundary

### OB-001 BCL instrumentation in library; OTel SDK in hosting
Core (`Abstractions`, `Core`, engines, providers) SHALL emit diagnostics through BCL APIs only:
`ILogger<T>`, `System.Diagnostics.ActivitySource`, and `System.Diagnostics.Metrics.Meter`.
No OpenTelemetry NuGet dependency SHALL appear outside `OrcaCore.Hosting` (IOQ-5).

### OB-002 Hosting wires exporters
`OrcaCore.Hosting` SHALL provide opt-in extension methods (e.g. `AddOrcaCoreOpenTelemetry`)
that register OTel `MeterProvider`, `TracerProvider`, and `LoggerProvider` exporters
(OTLP gRPC/HTTP, Prometheus, console) and connect them to the BCL sources/meters/loggers
emitted by OrcaCore. Host applications MAY override exporter endpoints and sampling.

### OB-003 No logging in Abstractions
`OrcaCore.Abstractions` SHALL remain free of `ILogger` references (02 §5). Contracts MAY
define telemetry attribute names and observer interfaces; implementations live in engines,
providers, and hosting.

### OB-004 Product events ≠ telemetry spans
Lifecycle events (MG-020/021) are **product records** for operators and integrators — committed
durably via outbox where required. They SHALL NOT be the sole observability mechanism.
Telemetry (logs/metrics/traces) is **diagnostic**, best-effort at the process boundary,
and MAY be sampled or dropped without affecting workflow correctness.

---

## 15.2 Dashboard insight model

### OB-010 Dashboard-first signal design
Every metric and high-value log line SHALL be designed so a dashboard panel or alert rule
can be built from **documented instrument names and attribute keys** without reading source
code. A companion **dashboard catalog** (SHOULD ship with hosting docs) lists recommended
panels, PromQL/LogQL examples, and alert thresholds.

### OB-011 Workflow-centric dimensions
Telemetry SHALL use a stable, low-cardinality attribute set so dashboards can slice running
work by workflow semantics. Required dimensions (when applicable):

| Attribute key | Source | Dashboard use |
|---------------|--------|----------------|
| `orca.execution.mode` | `ephemeral` \| `durable` | Mode comparison |
| `orca.definition.id` | `DefinitionId` | Per-workflow-type panels |
| `orca.definition.version` | `DefinitionVersion` | Version rollout monitoring |
| `orca.instance.id` | `InstanceId` | Drill-down (logs/traces; **avoid** high-cardinality metric labels in aggregate panels) |
| `orca.root.instance.id` | lineage | DAG/run-level aggregation |
| `orca.parent.instance.id` | lineage | Child-workflow panels |
| `orca.status` | `WorkflowStatus` | State funnel |
| `orca.step.path` | active step | Step hot-spots |
| `orca.wait.event_name` | active wait | Blocked-on-external-event heatmap |
| `orca.outbox.kind` | outbox record kind | Dispatch breakdown |
| `orca.command.type` | command discriminant | Command throughput |
| `orca.event.type` | event discriminant | Event throughput |
| `orca.provider.name` | provider plugin | Provider health |

`orca.instance.id` MAY appear on **logs and traces** always; on **metrics** it SHALL appear
only on fine-grained instruments explicitly marked `high_cardinality=true` in the catalog
(e.g. per-instance gauges for management export), not on aggregate counters/histograms used
for fleet-wide dashboards.

### OB-012 Insights the dashboard MUST support
The combined metrics + logs surface SHALL enable these operator questions:

1. **Fleet health** — non-terminal instance counts by status and definition; stuck count;
   oldest active instance age; resource-pool wait depth.
2. **Blocking analysis** — active waits grouped by `orca.wait.event_name`; timer backlog;
   instances in `Waiting` longer than threshold.
3. **Throughput** — commands processed, events applied, steps completed/failed per minute
   by definition.
4. **Latency** — command processing duration, provider commit duration, step execution
   duration, outbox dispatch duration (histograms with p50/p95/p99).
5. **Durable pressure** — pending/retryable outbox count, stream event growth rate,
   checkpoint lag, dispatch poison count (DU-052, MG-031).
6. **Failure drill-down** — from a failed-instance metric or alert, correlated logs/traces
   for the same `orca.instance.id` showing last command, last step, error summary, and
   outbox dispatch failures.
7. **DAG/run progress** — child nodes by status under `orca.root.instance.id`; optional
   metric `orca.dag.nodes` with labels `node_id`, `status`.

---

## 15.3 Metrics requirements

### OB-020 Meter naming
Meters SHALL be named per assembly: `OrcaCore`, `OrcaCore.Engine.Ephemeral`,
`OrcaCore.Engine.Durable`, `OrcaCore.Providers.<Name>`. Instrument names use `snake_case`
with `orca.` prefix, e.g. `orca.instances.active`, `orca.commands.duration`.

### OB-021 Required gauge instruments (fleet / pressure)
| Instrument | Type | Attributes | Maps to |
|------------|------|------------|---------|
| `orca.instances.active` | UpDownCounter or ObservableGauge | `mode`, `definition.id`, `definition.version`, `status` | MG-030 |
| `orca.instances.stuck` | ObservableGauge | `mode`, `definition.id` | MG-040 |
| `orca.waits.active` | ObservableGauge | `mode`, `wait.event_name`, `definition.id` | MG-030 |
| `orca.outbox.pending` | ObservableGauge | `state` (`pending`/`retryable`/`claimed`) | DU-052 |
| `orca.stream.events` | ObservableGauge | `provider.name` | DU-052 |
| `orca.checkpoints.count` | ObservableGauge | `provider.name` | DU-052 |
| `orca.checkpoints.lag` | ObservableGauge | `provider.name` | DU-052 (max stream version − checkpoint version) |
| `orca.resource_pool.waiters` | ObservableGauge | `pool.name` | MG-062 |
| `orca.resource_pool.tickets` | ObservableGauge | `pool.name` | MG-062 |

Observable gauges SHOULD be backed by the same projection queries that power `Statistics()`
where possible, so dashboard numbers match management API answers (AC-312).

### OB-022 Required counter instruments (throughput)
| Instrument | Attributes | When incremented |
|------------|------------|------------------|
| `orca.commands.processed` | `mode`, `command.type`, `definition.id`, `outcome` | After command handling completes |
| `orca.events.applied` | `mode`, `event.type`, `definition.id` | After event applied to aggregate |
| `orca.steps.completed` | `mode`, `definition.id`, `step.path` | Step success |
| `orca.steps.failed` | `mode`, `definition.id`, `step.path`, `error.kind` | Step failure |
| `orca.outbox.dispatched` | `outbox.kind`, `result` (`success`/`retryable`/`permanent`) | After dispatch attempt |
| `orca.lifecycle.events` | `event.name`, `durable` | Lifecycle event recorded |
| `orca.inbox.duplicates` | `mode` | Deduplicated inbound delivery |

### OB-023 Required histogram instruments (latency)
| Instrument | Unit | Attributes |
|------------|------|------------|
| `orca.commands.duration` | `s` | `mode`, `command.type`, `definition.id` |
| `orca.steps.duration` | `s` | `mode`, `definition.id`, `step.path` |
| `orca.provider.commit.duration` | `s` | `provider.name`, `operation` |
| `orca.outbox.dispatch.duration` | `s` | `outbox.kind`, `result` |
| `orca.waits.duration` | `s` | `wait.event_name`, `definition.id` | Time from wait registered to matched |

Histograms SHALL use explicit bucket boundaries documented for dashboard compatibility
(recommended: 5 ms, 10 ms, 25 ms, 50 ms, 100 ms, 250 ms, 500 ms, 1 s, 2.5 s, 5 s, 10 s, 30 s, 60 s, +Inf for engine operations; tune per instrument in catalog).

### OB-024 Metric export cadence
Observable gauges for fleet state SHOULD refresh at least once per scrape interval (default
15 s) via a hosting `BackgroundService` or callback registration, without blocking the
hot execution path.

### OB-025 Exemplars for drill-down
Latency histograms (`orca.commands.duration`, `orca.steps.duration`, `orca.outbox.dispatch.duration`)
SHALL support **exemplars** (trace ID + span ID) when recorded inside an active `Activity`,
enabling dashboard click-through from a latency spike to the causative trace (OB-040).

---

## 15.4 Logging requirements

### OB-030 Structured logging only
All OrcaCore log entries SHALL be structured (no string interpolation in log calls).
Hot paths SHALL use source-generated `[LoggerMessage]` partial methods (02 §5). Log levels:

| Level | Use |
|-------|-----|
| `Debug` | Per-command/step detail; sampling-friendly |
| `Information` | State transitions, lifecycle milestones, pump cycle summaries |
| `Warning` | Retryable failures, stuck detection, version mismatch, dedup hits |
| `Error` | Permanent dispatch failure, commit failure, unrecoverable processing error |

### OB-031 Required log categories
Each category SHALL use a stable `EventId` range per assembly (documented in hosting README).

| Category | Example message intent | Key properties |
|----------|------------------------|----------------|
| Command processing | Command accepted/rejected/completed | `orca.command.type`, `orca.instance.id`, `orca.definition.id`, duration_ms, outcome |
| Step execution | Step started/completed/failed | `orca.step.path`, `orca.instance.id`, error_summary |
| Wait/timer | Wait registered/matched; timer scheduled/fired | `orca.wait.event_name`, `orca.correlation.id`, fire_at |
| Outbox | Record claimed/dispatched/poisoned | `orca.outbox.record_id`, `orca.outbox.kind`, attempt, exception_type |
| Provider commit | Append/commit succeeded or conflict | `orca.provider.name`, stream_version, expected_version |
| Stuck detection | Instance/step marked stuck | threshold, `orca.step.path` |
| Resource pool | Acquire/release/wait/expiry | `pool.name`, ticket_id, waiters |

Payloads and business state SHALL NOT be logged by default (NF-040). Logs carry **metadata
and error summaries** only; hosts MAY opt in to sanitized payload logging via a dangerous
host flag documented as non-production.

### OB-032 Pump and background service logs
The outbox pump, timer hosted service, and operational sweeps SHALL emit `Information`
summary logs per cycle (records claimed, dispatched, failures, duration_ms) and `Error`
logs with exception detail on permanent failures — enabling log-based alerts when metrics
are unavailable.

---

## 15.5 Correlation: logs ↔ metrics ↔ traces

### OB-040 Trace context propagation
Command processing, provider commits, outbox dispatch, and step execution SHALL run inside
`Activity` spans created from module `ActivitySource` instances. Child activities (commit
within command, dispatch within pump) SHALL nest under the parent.

ActivitySource names mirror meter names: `OrcaCore.Engine.Durable`, etc.

### OB-041 Logs carry trace identifiers
When logging inside an active `Activity`, log scopes SHALL include OpenTelemetry log
correlation fields so backends index logs with traces:

- `trace_id` (32-hex)
- `span_id` (16-hex)
- `trace_flags` when sampled

In .NET this is achieved by enabling `Activity` listener + `ILogger` scope enrichment in
hosting (OTel `AddOpenTelemetry().WithLogging()` with `IncludeFormattedMessage` per host
policy). OrcaCore engines SHALL call `logger.BeginScope` with `orca.instance.id` and
other §OB-011 dimensions on command boundaries.

### OB-042 Shared attribute bag
The same §OB-011 keys used as **metric labels** SHALL appear as **log scopes** and **span
attributes** for a given operation, so a dashboard can:

1. Alert on `orca.outbox.pending{state="retryable"} > N`
2. Filter logs with `orca.outbox.kind` + time range
3. Open trace from histogram exemplar
4. See identical `orca.definition.id` / `orca.instance.id` on all three signals

### OB-043 Metric-to-log navigation contract
For each alertable metric in OB-021/022/023, the dashboard catalog SHALL document a
**correlated log query** (e.g. "pending outbox spike → `EventName=OutboxDispatchFailed` OR
`OutboxRecordPoisoned` with matching `orca.outbox.kind`"). OB-043 is satisfied when the
catalog exists and acceptance test OB-AC-003 passes.

### OB-044 Resource attributes
Hosting OTel setup SHALL set standard resource attributes on all signals:

- `service.name` (host-provided, default `orca-core-host`)
- `service.instance.id` (pod/process id)
- `orca.hosting.profile` (e.g. `sample`, `durable-postgres`)

---

## 15.6 Tracing requirements

### OB-050 Span catalog (minimum)
| Span name | Parent | Key attributes |
|-----------|--------|----------------|
| `orca.command.process` | root or host | `orca.command.type`, `orca.instance.id`, `orca.definition.id` |
| `orca.provider.commit` | command | `orca.provider.name`, `stream_version` |
| `orca.step.execute` | command | `orca.step.path` |
| `orca.outbox.dispatch` | pump cycle | `orca.outbox.kind`, `orca.outbox.record_id` |
| `orca.outbox.pump_cycle` | background | records_claimed, duration |
| `orca.event.apply` | command | `orca.event.type` |

Spans SHALL record exceptions on failure (`ActivityStatusCode.Error`).

Tracing MAY be sampled (head-based or tail-based) in hosting; correctness MUST NOT depend
on traces being exported.

---

## 15.7 Outbox pump observer contract

### OB-060 Observer port
DU-032 and PR-014 require observability hooks on the dispatch pipeline. The durable engine
SHALL expose a public `IOutboxPumpObserver` (or equivalent) with callbacks:

- `OnRecordClaimed(OutboxRecord)`
- `OnDispatchCompleted(OutboxRecord, DispatchResult, TimeSpan duration)`
- `OnDispatchFailed(OutboxRecord, Exception)`
- `OnCycleCompleted(OutboxPumpCycleResult)` — claimed/dispatched/failed counts, duration

The default hosting registration SHALL attach an observer that updates OB-021/022/023
instruments and writes OB-031 outbox logs. Hosts MAY register additional observers.

### OB-061 Retry delay strategy
`IOutboxPumpDelayStrategy` (PR-014) remains separate from observer; both SHALL be
registrable in DI. Metrics SHALL include `orca.outbox.retry.delay` histogram when retries
occur.

---

## 15.8 Hosting integration

### OB-070 Extension method contract
`AddOrcaCoreOpenTelemetry(IConfiguration)` (or overload) SHALL:

1. Register `MeterProvider` listening to all OrcaCore meters (OB-020).
2. Register `TracerProvider` listening to all OrcaCore activity sources (OB-050).
3. Enrich `ILogger` with OTel correlation (OB-041).
4. Register gauge collection hosted service (OB-024).
5. Wire default `IOutboxPumpObserver` (OB-060).

Configuration keys (example):

```json
{
  "OrcaCore": {
    "OpenTelemetry": {
      "OtlpEndpoint": "http://localhost:4317",
      "EnableConsoleExporter": false,
      "EnablePrometheusExporter": true,
      "TraceSamplingRatio": 0.1
    }
  }
}
```

### OB-071 Sample host
`OrcaCore.SampleHost` SHOULD enable OTel exporters behind a config flag so operators can
run a local dashboard stack (Prometheus + Grafana or OTel Collector) against a reference
configuration.

### OB-072 Dashboard artifact
Hosting docs SHOULD ship `docs/observability/dashboards/orca-core-overview.json` (Grafana)
or equivalent with panels for §OB-012 insights, exemplar-enabled latency charts, and
log links pre-configured for the log backend.

---

## 15.9 Relationship to management API

### OB-080 Statistics parity
Where `Statistics()` / `GetStatisticsAsync` returns a value (MG-030/031), the corresponding
observable gauge (OB-021) SHOULD match within one scrape interval. Divergence SHALL be
documented (e.g. ephemeral in-memory vs durable projection lag).

### OB-081 Management API remains authoritative for detail
Telemetry supports **fleet dashboards and alerting**; per-instance deep inspection
(history, state, lifecycle event list, DAG reconstruction) remains on the management API
(DU-071, JS-005). Dashboards link to host-owned detail UIs via `orca.instance.id` in
exemplars/logs — not embedded in OrcaCore.

---

## 15.10 Non-goals

- OrcaCore does not ship a built-in web dashboard (host responsibility).
- Business payload content in logs/traces (NF-040).
- Exactly-once telemetry delivery — drops and sampling are acceptable.
- Replacing lifecycle outbox events with spans (OB-004).

---

## 15.11 Acceptance criteria

These criteria extend document 12; suggested IDs for catalog merge:

- **OB-AC-001** *Metrics emitted* — With hosting OTel enabled, scraping `orca.instances.active`
  and `orca.outbox.pending` returns non-zero series after starting workflows in integration
  test. [OB-021]
- **OB-AC-002** *Structured command log* — Processing a command produces a log entry with
  `orca.instance.id`, `orca.command.type`, and `trace_id` when tracing is enabled. [OB-031, OB-041]
- **OB-AC-003** *Metric-log correlation* — Given a failed outbox dispatch, filtering logs
  by `orca.outbox.kind` and time window retrieves the error log; the same `orca.instance.id`
  appears on the `orca.outbox.dispatched{result="permanent"}` counter increment. [OB-042, OB-043]
- **OB-AC-004** *Exemplar trace link* — A recorded `orca.commands.duration` exemplar
  references a span whose attributes include `orca.instance.id` for that command. [OB-025, OB-050]
- **OB-AC-005** *Statistics parity* — `Statistics().Groups` counts match
  `orca.instances.active` sums by `status` and `definition.id` for the same fixture. [OB-080, AC-312]
- **OB-AC-006** *Pump observer* — Injecting a test `IOutboxPumpObserver` receives
  `OnDispatchCompleted` for each dispatched outbox record in pump integration test. [OB-060, DU-032]
- **OB-AC-007** *No OTel in core* — `OrcaCore.Engine.Durable` and provider projects have
  no `OpenTelemetry.*` package references; only BCL diagnostics. [OB-001]

---

## 15.12 Phasing

| Phase | Deliverable |
|-------|-------------|
| 1 | `ILogger` + `[LoggerMessage]` on command/outbox/commit paths; `IOutboxPumpObserver` |
| 2 | `Meter` instruments OB-021/022; gauge collector hosted service |
| 3 | `ActivitySource` spans OB-050; log scope enrichment |
| 4 | `AddOrcaCoreOpenTelemetry`; exemplars; sample dashboard JSON |
| 5 | OB-AC test suite; statistics parity hardening |

---

## Provenance

- IOQ-5 resolution: `docs/implementation/00-stack-decisions.md`
- Engineering conventions: `docs/implementation/02-engineering-conventions.md` §5
- Product observability: `docs/specs/09-requirements-management-operations.md` §9.4
- Durable pressure: `docs/specs/06-requirements-durable-execution.md` DU-052
- Outbox hooks: `docs/specs/06-requirements-durable-execution.md` DU-032, `docs/specs/10-provider-model-and-extensibility.md` PR-014
- Review findings: `docs/review/findings/R7-hosting-cross-cutting.md`, `docs/review/findings/R8-dotnet10-csharp-quality.md`
- OpenTelemetry: [Logs data model](https://opentelemetry.io/docs/specs/otel/logs/data-model/), [Metrics exemplars](https://opentelemetry.io/docs/specs/otel/metrics/data-model/#exemplars), [Trace-Log correlation](https://opentelemetry.io/docs/specs/otel/logs/supplementary-guidelines/)
