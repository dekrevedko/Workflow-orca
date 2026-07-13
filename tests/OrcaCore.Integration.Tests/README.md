# OrcaCore Integration Gates

Run these commands from the repository root so the local SDK pin in `global.json` is used.

## Prerequisites

Broad integration gates use Docker through Testcontainers. Before running a broad
gate locally, clear stale build workers if prior runs were interrupted:

```powershell
dotnet build-server shutdown
docker info
```

## Smoke Gate

Use this after small e2e or hosting edits:

```powershell
dotnet build OrcaCore.slnx --no-restore
dotnet test tests/OrcaCore.Integration.Tests/OrcaCore.Integration.Tests.csproj --no-build --filter "FullyQualifiedName~OrcaCore.Integration.Tests.E2E|FullyQualifiedName~OrcaCore.Integration.Tests.Hosting"
```

## Focused Gates

```powershell
dotnet test tests/OrcaCore.Integration.Tests/OrcaCore.Integration.Tests.csproj --no-build --filter "FullyQualifiedName~OrcaCore.Integration.Tests.E2E"
dotnet test tests/OrcaCore.Integration.Tests/OrcaCore.Integration.Tests.csproj --no-build --filter "FullyQualifiedName~OrcaCore.Integration.Tests.Hosting"
dotnet test tests/OrcaCore.Integration.Tests/OrcaCore.Integration.Tests.csproj --no-build --filter "FullyQualifiedName~OrcaCore.Integration.Tests.Stacks"
dotnet test tests/OrcaCore.Integration.Tests/OrcaCore.Integration.Tests.csproj --no-build --filter "FullyQualifiedName~OrcaCore.Integration.Tests.MultiNode"
dotnet test tests/OrcaCore.Integration.Tests/OrcaCore.Integration.Tests.csproj --no-build --filter "FullyQualifiedName~OrcaCore.Integration.Tests.JobScheduler"
dotnet test tests/OrcaCore.Integration.Tests/OrcaCore.Integration.Tests.csproj --no-build --filter "FullyQualifiedName~OrcaCore.Integration.Tests.Observability"
```

The observability gate covers `OB-AC-001` through `OB-AC-007`: command metrics,
structured command logs with trace correlation, outbox dispatch health,
activity tags, management/statistics parity, default pump metrics, and the
OpenTelemetry package-boundary guard.

## Full Integration Gate

```powershell
dotnet test tests/OrcaCore.Integration.Tests/OrcaCore.Integration.Tests.csproj --no-build
```

Existing skipped tests are named backlog items. Do not delete or retag them just
to make the full gate look cleaner; unskip each one only when its blocker is
implemented and verified.

Current expected result: 107 passed, 1 skipped, 0 failed. The remaining skip is
`INT_JS_018` for the one-hour slow soak.
