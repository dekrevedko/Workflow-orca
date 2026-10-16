# R12 — Observability (spec 15 OB-*) & dashboard app — Findings

> **Scope:** the OTel/observability code and the `OrcaCore.Dashboard` app added by the e2e push,
> judged against `docs/specs/15-requirements-observability-otel.md` (`OB-*`).
> **Baseline:** build clean under `-warnaserror`; the 7 `OB-AC-00x` integration tests pass
> (`OrcaCore.Integration.Tests/Observability`). This is a **review-only** phase — no code changed.
> **Files:** `OrcaCore.Abstractions/Diagnostics/{OrcaCoreDiagnostics,OrcaCoreMetrics}.cs`,
> `OrcaCore.Hosting/Telemetry/OrcaCoreTelemetryObserver.cs`, `Engine.Durable/Execution/DurableCommandProcessor.cs`
> + `Outbox/DurableOutboxPump.cs` (spans), `dashboard/**`.

> **Addendum 2026-07-05:** a follow-up (the parallel observability work) added
> `OrcaCoreTelemetryGaugeCollector` + `OrcaCoreTelemetryInstruments` (a projection-backed fleet
> gauge collector), partially addressing findings P1/P2 below. While making its test suite
> guard-compliant, one further defect was found and fixed: `UpdateFleetGauges` published its nine
> gauges via nine sequential `Volatile.Write`s, so a scrape landing mid-update saw a partial
> snapshot (new waiters, stale tickets). Fixed by publishing all fleet gauges as one immutable
> `FleetGaugeSnapshot` swapped atomically. A wall-clock `Task.Delay` in the gauge test
> (`WaitForMetricAsync`) was replaced with a yield-until-observed loop (repo guard compliance).

## Findings

### [P1] `orca.instances.active` gauge is a process-static command-side tally, not projection-backed — `src/OrcaCore.Abstractions/Diagnostics/OrcaCoreMetrics.cs:19`
- **Requirement/convention:** OB-021 ("SHOULD be backed by the same projection queries that power `Statistics()`"), OB-080 (statistics parity), OB-AC-005
- **Evidence:** `private static readonly ConcurrentDictionary<InstanceId, ActiveInstanceMetric> ActiveInstances` is mutated inside `RecordCommandProcessed` (a side effect of command handling) and read back by the `ObservableGauge` callback. The gauge never queries the projection store.
- **Failure scenario:** the gauge reflects only instances that received a command **in this process since startup**. After a host restart the dictionary is empty while projections still hold the running fleet → gauge under-reports; a second host sees only its own traffic; a `Waiting` instance receiving no commands is invisible. OB-080 parity therefore holds only transiently in one process — exactly why `OB-AC-005` passes (single fresh instance, filtered by a new `DefinitionId`). A Grafana panel on this gauge shows wrong counts after any restart.
- **Secondary:** the static dictionary is process-global mutable state shared across all engines/hosts/tests, accumulating until each instance goes terminal — a real test-isolation hazard and a DI/lifetime smell (no per-engine scoping).
- **Recommendation:** implement the OB-024 gauge-collector as an `ObservableGauge` (or hosted `BackgroundService`) whose callback queries `IWorkflowProjectionStore.GetStatisticsAsync`, so the gauge is projection-backed and matches `Statistics()`. Delete the static tally.
- **Confidence:** CONFIRMED (traced; OB-AC-005 passes only under the single-process fixture).

### [P1] `AddOrcaCoreOpenTelemetry` (OB-070 / OB-002) is not implemented in hosting — OTel wiring is inlined in the dashboard app — `dashboard/Program.cs:40`
- **Requirement/convention:** OB-002, OB-070 (the central §15.8 hosting deliverable), OB-024, OB-025
- **Evidence:** `OrcaCore.Hosting` registers only the telemetry **observer** (`OrcaCoreServiceCollectionExtensions.cs:53-69`). No `AddOrcaCoreOpenTelemetry` extension exists. The Meter/Tracer registration and Prometheus exporter are hand-rolled in `dashboard/Program.cs`: `AddOpenTelemetry().WithMetrics(m => m.AddMeter(OrcaCoreDiagnostics.SourceName)).WithTracing(t => t.AddSource(...))`, `app.UseHttpMetrics()`, `app.MapMetrics("/metrics")`.
- **Failure scenario:** every host except the bundled dashboard must hand-wire the entire OTel bridge; OB-070's five-point contract (meter provider, tracer provider, log enrichment, gauge collector, pump observer) is unmet as a library surface; OB-024 gauge cadence and OB-025 exemplars are absent everywhere.
- **Recommendation:** implement `AddOrcaCoreOpenTelemetry(IConfiguration)` in `OrcaCore.Hosting` per OB-070 and have the dashboard consume it instead of inlining. Add the exporter packages to `Directory.Packages.props` (OTLP/Prometheus/console) so OB-002 is satisfiable without the dashboard.
- **Confidence:** CONFIRMED.

### [P2] `OrcaCore.Dashboard` ships a built-in web dashboard, contradicting the §15.10 non-goal — `dashboard/OrcaCore.Dashboard.csproj`
- **Requirement/convention:** §15.10 ("OrcaCore does **not** ship a built-in web dashboard (host responsibility)"); OB-071 (sample host) / OB-072 (a Grafana **JSON artifact**) are the sanctioned forms
- **Evidence:** a full Blazor Server app (`Components/`, `wwwroot/`, `DashboardReadModel`, `/api/dashboard/snapshot`) lives at `dashboard/`, not under `samples/`, named as an OrcaCore product component. It depends on `prometheus-net` (`Prometheus` namespace, `UseHttpMetrics`, `MapMetrics`) — a different library than the OTel Prometheus exporter the spec references.
- **Failure scenario:** not a runtime bug — a **scope/spec conflict**. Shipping a product dashboard changes OrcaCore's dependency surface (web framework, prometheus-net) and its stated boundary; leaving §15.10 as-is makes the codebase contradict its own spec.
- **Recommendation:** author decision — either (a) reposition the app as an explicit **reference sample** (move under `samples/`, README it as non-shipped, and add the OB-072 Grafana JSON artifact it stands in for), or (b) amend §15.10 to adopt the dashboard as an official deliverable and reconcile OB-002's exporter choice (OTel exporter vs prometheus-net). The `DashboardReadModel` correctly reads the **management API** (`management.All().ListAsync/StatisticsAsync`, `GetHistoryAsync`) per OB-081, which is the right data source either way.
- **Confidence:** CONFIRMED (documented non-goal vs shipped app).

### [P2] Instrument & span coverage is ~20% of the OB-021/022/023/050 catalog — `src/OrcaCore.Abstractions/Diagnostics/OrcaCoreMetrics.cs:14`
- **Requirement/convention:** OB-021 (9 gauges), OB-022 (7 counters), OB-023 (5 histograms), OB-050 (6 spans)
- **Evidence:** implemented — counters `orca.commands.processed`, `orca.outbox.dispatched`; histogram `orca.commands.duration`; gauge `orca.instances.active`; spans `orca.command.process`, `OrcaCore.Outbox.PumpOnce`. Missing — gauges `instances.stuck`, `waits.active`, `outbox.pending`, `stream.events`, `checkpoints.count`, `checkpoints.lag`, `resource_pool.waiters`, `resource_pool.tickets`; counters `events.applied`, `steps.completed`, `steps.failed`, `lifecycle.events`, `inbox.duplicates`; histograms `steps.duration`, `provider.commit.duration`, `outbox.dispatch.duration`, `waits.duration`; spans `orca.provider.commit`, `orca.step.execute`, `orca.outbox.dispatch`, `orca.event.apply`. `orca.outbox.dispatched` also omits the required `outbox.kind` attribute.
- **Failure scenario:** OB-012 dashboard insights 1–2, 4–5, 7 (fleet health, blocking analysis, latency, durable pressure, DAG progress) cannot be built — the instruments they name don't exist.
- **Recommendation:** this tracks the spec's own §15.12 phasing (phases 1–4 partially delivered). Record as the observability coverage baseline and drive the remaining instruments through the phase plan; it is a scoped-but-unmet spec surface, not a defect.
- **Confidence:** CONFIRMED (instrument inventory).

### [P2] Single `OrcaCore` meter/source instead of the per-assembly set (OB-020/OB-050) — `src/OrcaCore.Abstractions/Diagnostics/OrcaCoreDiagnostics.cs:19`
- **Requirement/convention:** OB-020 (meters `OrcaCore`, `OrcaCore.Engine.Ephemeral`, `OrcaCore.Engine.Durable`, `OrcaCore.Providers.<Name>`), OB-050 (source names mirror meters)
- **Evidence:** one `Meter("OrcaCore")` and one `ActivitySource("OrcaCore")` in Abstractions; all engines/providers record through them. `CommandTags`/`ObserveActiveInstances` hardcode `orca.execution.mode = "durable"`, so the shared meter cannot serve ephemeral metrics (OB-011 mode dimension is a constant).
- **Failure scenario:** dashboards cannot slice by `provider.name`/assembly source (OB-011 `orca.provider.name`), and `AddMeter`/`AddSource` cannot selectively enable per-assembly signals; ephemeral-mode panels are impossible.
- **Recommendation:** define per-assembly `Meter`/`ActivitySource` in the engine and provider projects (Abstractions keeps only attribute-name constants and observer contracts per OB-003); thread `orca.execution.mode` as a parameter rather than a constant.
- **Confidence:** CONFIRMED.

### [P3] Span-name and attribute-prefix drift (OB-050 / OB-011) — `src/OrcaCore.Engine.Durable/Outbox/DurableOutboxPump.cs:47`
- **Evidence:** the pump span is `"OrcaCore.Outbox.PumpOnce"` (catalog OB-050 name is `orca.outbox.pump_cycle`), and its tags use the `orcacore.outbox.*` prefix (`orcacore.outbox.max_count`, `orcacore.outbox.claimed_count`) instead of the spec's `orca.` prefix (OB-011). The command span records `outcome` but does not set `ActivityStatusCode.Error` on a genuine provider exception (OB-050 "Spans SHALL record exceptions on failure") — non-throwing outcomes are tagged, but a thrown exception disposes the span without Error status.
- **Recommendation:** rename to `orca.outbox.pump_cycle`, normalize tag keys to `orca.outbox.*`, and set `ActivityStatusCode.Error` + `activity.AddException` in the command/pump catch paths. No test pins these names, so the rename is safe.
- **Confidence:** CONFIRMED.

### [P3] Telemetry recording logic lives in `OrcaCore.Abstractions` (OB-003 boundary) — `src/OrcaCore.Abstractions/Diagnostics/OrcaCoreMetrics.cs:1`
- **Evidence:** OB-003 permits Abstractions to "define telemetry attribute names and observer interfaces," with "implementations … in engines, providers, and hosting." `OrcaCoreMetrics` holds the `Meter`, instrument creation, the static active-instance store, and recording logic — an implementation, not a contract.
- **Recommendation:** move instrument recording into the engine/provider assemblies (aligns with the OB-020 per-assembly meter fix above); keep only attribute-key constants + `IWorkflowRuntimeObserver`/`IOutboxPumpObserver` in Abstractions.
- **Confidence:** CONFIRMED (minor boundary drift).

## Positives verified (no finding)

- **OB-030 / OB-041 structured logging + correlation:** `OrcaCoreTelemetryObserver` uses
  source-generated `[LoggerMessage]` (`OrcaCoreTelemetryLog`) with a `BeginScope` bag carrying
  `orca.instance.id`, command type/outcome, definition id/version, status, and `trace_id`/`span_id`
  from `Activity.Current`. Correct per OB-031/OB-041.
- **OB-060 pump observer:** `IOutboxPumpObserver` exists with the default hosting registration
  wiring the telemetry observer (`OrcaCoreServiceCollectionExtensions.cs:56`); `OB-AC-006` passes.
- **OB-AC discipline:** all seven `OB-AC-001…007` have passing integration tests, including
  `OB-AC-007` (no `OpenTelemetry.*` refs in `Engine.Durable`/providers — verified: OTel appears
  only in the dashboard app).
- **OB-081:** the dashboard reads per-instance detail from the management API, not from scraped
  metrics — the correct authority split.
- **Command span attributes:** `orca.command.process` carries command.type, instance.id, outcome,
  stream.version, and definition.id/version/status on completion — well-dimensioned (OB-050).

## Coverage note

Verified: OB-001 (BCL-only in core — OTel confined to the dashboard app; but OB-070 hosting
deliverable absent), OB-003 (partial — recording logic in Abstractions), OB-011 (attribute set
present on the implemented instruments; mode hardcoded), OB-020 (single meter — gap), OB-021/022/023
(≈20% coverage), OB-030/031/041 (met), OB-050 (2/6 spans, name/prefix drift), OB-060 (met),
OB-070/OB-002 (absent as a library surface), OB-080 (transient in-process only), OB-081 (met),
OB-AC-001…007 (all pass for the implemented subset). Not reached: OB-025 exemplars, OB-024 gauge
cadence, OB-044 resource attributes, OB-072 Grafana artifact (none ship). Recommend prioritizing
the two P1s (projection-backed gauge; `AddOrcaCoreOpenTelemetry`) and resolving the P2 dashboard
scope decision before extending instrument coverage.
