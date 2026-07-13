# OrcaCore reference dashboard

This Blazor app is a host-owned reference sample for OrcaCore observability. It is not a built-in OrcaCore product dashboard.

The sample reads per-instance workflow detail through the management API and reads process-local telemetry through the same BCL metrics, logs, and spans that a host exports through `AddOrcaCoreOpenTelemetry`.

On startup, the app seeds in-memory durable demo data: child fanout, an external
job waiting on a resource pool, a compensation-failed saga, and an operator
cancelled workflow. The seed data is intentionally local to the sample process.

The `Kubernetes` page is the advanced local-cluster sample. It uses the active
`kubectl` context and creates ordinary `batch/v1` Jobs in the `default`
namespace:

- one scheduled job every 15 minutes, with a random 1-15 minute sleep;
- a dependency workflow with 2-5 sequential jobs, each 1-3 minutes;
- a DAG workflow with 3-9 jobs, each 1-3 minutes.

Each Kubernetes job is mirrored into OrcaCore as a durable external job, so the
main dashboard also shows the corresponding durable instance history.

Run locally:

```powershell
dotnet run --project samples\OrcaCore.Dashboard\OrcaCore.Dashboard.csproj
```

For the Kubernetes page, start a local cluster such as Docker Desktop,
Rancher Desktop, minikube, or kind before running the dashboard, and confirm
`kubectl get namespace default` succeeds.

The sample enables the OpenTelemetry Prometheus exporter and exposes `/metrics` for local scraping. Production hosts should choose their own exporters and dashboards through `OrcaCore.Hosting`.
