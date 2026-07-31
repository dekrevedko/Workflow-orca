# Durable resource-governance dashboard

OrcaCore emits BCL `System.Diagnostics.Metrics` instruments from meter
`OrcaCore.Engine.Durable`. OpenTelemetry SDK registration, readers, exporters, sampling, and
backend-specific aggregation remain host-owned; no OrcaCore package registers an SDK or exporter.

The first-release dashboard should include these panels:

| Instrument | Unit and dimensions | Dashboard use |
|---|---|---|
| `orcacore.resource.quarantined.units` | logical units, tagged by `pool.name` | Show retained capacity per pool and alert whenever a nonzero value persists beyond the pool's recovery objective. |
| `orcacore.resource.quarantined.oldest_age` | seconds, tagged by `pool.name` | Show the oldest outstanding quarantine age and alert before the trusted stop-confirmation SLA is breached. |
| `orcacore.execution.fenced_bodies.running` | physical bodies, process-wide | Show timed-out or cancelled bodies that are still returning without commit authority; alert on a sustained nonzero value or steady growth. |

The quarantine gauges refresh when the trusted in-process diagnostics sequence is enumerated.
Production collectors should enumerate it on their scrape/refresh cadence and treat a missing
pool series as zero. The fenced-body gauge is updated directly at the timeout fence and physical
body return.

`IDurableResourceLeaseDiagnostics` is an advanced in-process contract, not a remote management
endpoint. A host adapter that publishes its snapshots must authenticate and authorize the
operator, redact application identifiers and authored locations according to local policy, and
apply its own transport rate limits before returning data. OrcaCore supplies neither an
authorization policy nor an unredacted built-in HTTP endpoint.
