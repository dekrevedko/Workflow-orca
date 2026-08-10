# Durable resource-governance dashboard

OrcaCore emits BCL `System.Diagnostics.Metrics` instruments from meter
`OrcaCore.Engine.Durable`. OpenTelemetry SDK registration, readers, exporters, sampling, and
backend-specific aggregation remain host-owned; no OrcaCore package registers an SDK or exporter.

The first-release dashboard should include these resource panels:

| Instrument | Unit and dimensions | Dashboard use |
|---|---|---|
| `orca.resource_pool.waiters` | requests, tagged by `pool.name` | Show queued pressure per pool. |
| `orca.resource_pool.tickets` | tickets, tagged by `pool.name` and `state` | Show pending, held, review-marked, ambiguous, and quarantined ownership per pool. |
| `orca.resource_pool.reserved_units` | units, tagged by `pool.name` and capacity-reserving `state` | Compare reservations with configured capacity without conflating ticket count and units. |
| `orca.resource_pool.over_capacity_debt` | units, tagged by `pool.name` | Alert when a resize leaves retained obligations above current capacity. |
| `orca.resource_pool.reconciliation_due` | tickets, tagged by `pool.name` | Show expired tickets awaiting an explicit recovery decision. |
| `orca.resource_pool.quarantined_units` | units, tagged by `pool.name` | Show retained capacity per pool and alert whenever a nonzero value persists beyond the pool's recovery objective. |
| `orca.resource_pool.oldest_quarantined_obligation_age` | seconds, tagged by `pool.name` | Show the oldest outstanding quarantine age and alert before the trusted stop-confirmation SLA is breached. |
| `orca.execution.fenced_bodies.running` | physical bodies, process-wide | Show timed-out or cancelled bodies that are still returning without commit authority; alert on a sustained nonzero value or steady growth. |

The quarantine gauges refresh when the trusted in-process diagnostics sequence is enumerated.
Production collectors should enumerate it on their scrape/refresh cadence and treat a missing
pool series as zero. The fenced-body gauge is updated directly at the timeout fence and physical
body return.

The durable host refreshes grouped instance, wait, stream, checkpoint, continuation, external
outbox, and resource-pool gauges from the provider-owned `IWorkflowOperationalStore` during the
configured operational sweep. The same immutable `WorkflowOperatorStatistics` snapshot is the
advanced operator view and the source of the observable measurements, so dashboards and direct
inspection cannot disagree within one sweep interval. Queue gauges carry `state` and
`orca.queue.lane`; instance gauges carry execution mode, definition, version, and status.

Physical cleanup is reachable only through the provider-authoring
`IWorkflowProviderMaintenanceStore`. It rejects active instances and live inbox/outbox references,
retains accepted-event confirmations, start bindings, and monotonic inbox-route revisions as
tombstones, and exposes no ordinary application archive or purge handle.

`IDurableResourceLeaseDiagnostics` is an advanced in-process contract, not a remote management
endpoint. A host adapter that publishes its snapshots must authenticate and authorize the
operator, redact application identifiers and authored locations according to local policy, and
apply its own transport rate limits before returning data. OrcaCore supplies neither an
authorization policy nor an unredacted built-in HTTP endpoint.
