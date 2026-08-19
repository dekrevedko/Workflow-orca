# OrcaCore telemetry dashboard

This Blazor app is a host-owned reference for collecting OrcaCore's public BCL
metrics, logs, and activities. It is not a product management API or a built-in
dashboard. The sample deliberately consumes only the current application-facing
diagnostics catalog and an explicitly selected ephemeral engine role.

Run locally:

```powershell
dotnet run --project samples\OrcaCore.Dashboard\OrcaCore.Dashboard.csproj
```

The app exposes `/health/live`, `/health/ready`, and
`/api/dashboard/snapshot`. Production hosts should export the same public BCL
signals through the telemetry stack selected by the application.
