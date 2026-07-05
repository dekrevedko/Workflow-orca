# OrcaCore reference dashboard

This Blazor app is a host-owned reference sample for OrcaCore observability. It is not a built-in OrcaCore product dashboard.

The sample reads per-instance workflow detail through the management API and reads process-local telemetry through the same BCL metrics, logs, and spans that a host exports through `AddOrcaCoreOpenTelemetry`.

Run locally:

```powershell
dotnet run --project samples\OrcaCore.Dashboard\OrcaCore.Dashboard.csproj
```

The sample enables the OpenTelemetry Prometheus exporter and exposes `/metrics` for local scraping. Production hosts should choose their own exporters and dashboards through `OrcaCore.Hosting`.
