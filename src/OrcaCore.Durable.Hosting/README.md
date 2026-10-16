# OrcaCore durable hosting diagnostics

`OrcaCore.Durable.Hosting` emits structured logs through `Microsoft.Extensions.Logging` only.
Applications own logging providers and OpenTelemetry SDK/exporter registration. Log scopes use the
canonical keys in `OrcaCoreDiagnostics`; workflow payloads and business state are never logged.

The assembly reserves stable event-ID ranges by category:

| Range | Category |
|---:|---|
| 1000-1099 | Durable command processing |
| 1100-1199 | Outbox dispatch and pump cycles |
| 1200-1299 | Step execution |
| 1300-1399 | Wait and timer transitions |
| 1400-1499 | Provider commits |
| 1500-1599 | Stuck detection |
| 1600-1699 | Resource-pool transitions and pressure |
| 1700-1799 | Workflow lifecycle transitions |
| 1800-1899 | Operational sweep summaries |
| 1900-1999 | Hosted-service failure boundaries |

Existing IDs are compatibility contracts. Add a new ID within the owning range; do not reuse or
renumber an existing ID.
