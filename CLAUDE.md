# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Active implementation track

The repository root is the sole active codebase. The superseded root prototype is
preserved under `archive/legacy-poc/`. All build, test, and source work targets
the root `OrcaCore.slnx`, `src/`, and `tests/`.

## Build and test commands

All commands run from the repository root so the SDK pin in `global.json` is respected.

```powershell
# Build everything
dotnet build OrcaCore.slnx

# Build release (CI mode — warnings are errors)
dotnet build OrcaCore.slnx -c Release -warnaserror

# Run a single unit test project
dotnet test tests/OrcaCore.Engine.Ephemeral.Tests/OrcaCore.Engine.Ephemeral.Tests.csproj
dotnet test tests/OrcaCore.Engine.Durable.Tests/OrcaCore.Engine.Durable.Tests.csproj
dotnet test tests/OrcaCore.Acceptance.Tests/OrcaCore.Acceptance.Tests.csproj

# Run a single test by name filter
dotnet test tests/OrcaCore.Engine.Ephemeral.Tests/OrcaCore.Engine.Ephemeral.Tests.csproj --filter "FullyQualifiedName~MyTestClass"

# Integration gate (requires Docker via Testcontainers)
dotnet test tests/OrcaCore.Integration.Tests/OrcaCore.Integration.Tests.csproj --no-build

# Integration smoke gate (E2E + Hosting only — fastest)
dotnet test tests/OrcaCore.Integration.Tests/OrcaCore.Integration.Tests.csproj --no-build --filter "FullyQualifiedName~OrcaCore.Integration.Tests.E2E|FullyQualifiedName~OrcaCore.Integration.Tests.Hosting"

# Run the console examples
dotnet run --project samples/OrcaCore.Examples/OrcaCore.Examples.csproj
```

Before running integration tests after an interrupted run, clear stale build workers:
```powershell
dotnet build-server shutdown
```

Expected integration baseline: 107 passed, 1 skipped (`INT_JS_018` — one-hour soak, intentionally skipped). Do not delete or unskip skipped tests unless the blocker is implemented and verified.

CI enforces 80% line coverage for `OrcaCore.Engine.*` assemblies.

## Architecture

### Two engines, one abstraction surface

OrcaCore provides two runtime engines that share the same workflow definition API (`OrcaCore.Core`) and step contract (`OrcaCore.Abstractions`):

| Engine | Project | Persistence | Use case |
|--------|---------|-------------|----------|
| Ephemeral | `OrcaCore.Engine.Ephemeral` | In-memory only; lost on restart | Development, testing, short-lived coordination |
| Durable | `OrcaCore.Engine.Durable` | Pluggable `IWorkflowStore` (PostgreSQL, SQL Server) | Production, long-running, crash-tolerant |

### Project structure (`src/`)

```
OrcaCore.Abstractions      — IStep, StepContext, StepResult, EventEnvelope, WaitRecord/Status/Mode,
                             WorkflowStatus, durable ports (IWorkflowStore, IMessageDispatcher)
OrcaCore.Core              — WorkflowBuilder, workflow definition nodes (Init/End/If/While/Parallel/Wait/WaitLong)
OrcaCore.Engine.Ephemeral  — In-process execution loop, timer pump, management surface
OrcaCore.Engine.Durable    — Durable engine aggregates, outbox, checkpoint/replay, routing
OrcaCore.Hosting           — AddOrcaCore() / AddOrcaCoreHostedServices() DI extensions
OrcaCore.Providers.InMemory     — In-memory IWorkflowStore (ephemeral testing)
OrcaCore.Providers.PostgreSql   — PostgreSQL provider (Dapper + Npgsql)
OrcaCore.Providers.SqlServer    — SQL Server provider
OrcaCore.Providers.RabbitMq     — RabbitMQ outbox dispatcher
OrcaCore.Providers.Redis        — Redis outbox/signaling
OrcaCore.Providers.ZeroMq       — ZeroMq transport
OrcaCore.Providers.Relational   — Shared SQL utilities across relational providers
```

### Durable engine model

The durable engine persists workflow state as an event-sourced aggregate. On each step:
1. The engine loads the current aggregate snapshot from `IWorkflowStore`.
2. It applies commands and emits events (append-only).
3. A committed outbox record is dispatched via `IMessageDispatcher` (at-least-once).
4. On restart, the engine replays from the last checkpoint.

The `WaitLong` node (durable-only) parks execution until an external event arrives, surviving host restarts.

### Control flow nodes

`Init`, `End`, `If`, `While`, `Parallel`, `Wait` (ephemeral — channel-based), `WaitLong` (durable — persisted).

### Provider certification

`OrcaCore.ProviderCertification` contains provider contract tests that any `IWorkflowStore` implementation must pass. Run it when adding or modifying a provider.

## Key conventions

- Target framework: `net10.0`; SDK pinned in `global.json` (10.0.301, `rollForward: latestFeature`)
- C#: file-scoped namespaces, `Nullable=enable`, `LangVersion=latest`, `TreatWarningsAsErrors=true`
- Package versions: centrally managed in `Directory.Packages.props` — never use floating versions
- Test framework: xunit.v3
- Integration tests use Testcontainers — Docker must be running
- CRLF line endings, 4-space indentation (`.editorconfig`)

## Documentation

- `docs/ephemeral-engine-developer-guide.md` — complete API walkthrough with examples
- `docs/durable-driver-lane-host.md` — durable segment execution model and multi-host contention
- `docs/specs/` — consolidated product requirements (source of truth for intended behavior)
- `docs/orleans-engine/` — self-contained specs for a planned Orleans-hosted durable engine variant
