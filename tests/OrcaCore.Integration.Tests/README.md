# OrcaCore First-Release Integration Gate

Run these commands from the repository root so the local SDK pin in `global.json` is used.

## Prerequisites

The PostgreSQL journey uses Docker through Testcontainers. Before running the
gate locally, clear stale build workers if prior runs were interrupted:

```powershell
dotnet build-server shutdown
docker info
```

## Active gate

The active `CurrentSurface` lane verifies only the approved first-release
application facade and package set:

- an in-memory ephemeral start/signal/output journey;
- an in-memory durable replacement-host and start-binding journey;
- a PostgreSQL durable replacement-host journey;
- exact durable-engine and callback-only ingress role composition.

Run:

```powershell
dotnet build tests/OrcaCore.Integration.Tests/OrcaCore.Integration.Tests.csproj --no-restore
dotnet test tests/OrcaCore.Integration.Tests/OrcaCore.Integration.Tests.csproj --no-build
```

Current result: 5 passed, 0 failed, 0 skipped.

## Recoverable provisional sources

The project file excludes 115 provisional `[Fact]`/`[Theory]` declarations
without deleting their source files. They remain recoverable and are classified
in
`docs/review/developer-facing-interface-section-07-inactive-test-project-audit-2026-07-30.md`.
Do not re-enable them wholesale: port supported behavior through the public
application facade and the exact split hosting roles.
