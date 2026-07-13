# Documentation map

The repository root is the active implementation. This folder contains the
requirements, architecture, implementation plans, research, review history, and
developer guides that explain it.

## Start here

1. [Project README](../README.md) — public orientation, build/test commands, and samples.
2. [Active implementation index](active-implementation-index.md) — current runtime guides,
   durable-driver notes, operations, and handoffs.
3. [Project technical overview](project-technical-overview.md) — architecture and code map.
4. [Current roadmap](plans/current-roadmap.md) — implemented features and planned scope.

## Active source layout

The solution file is [`OrcaCore.slnx`](../OrcaCore.slnx). The main projects are:

| Project | Purpose |
|---------|---------|
| [`OrcaCore.Abstractions`](../src/OrcaCore.Abstractions) | Public contracts, identifiers, workflow events, snapshots, provider ports, and serialization. |
| [`OrcaCore.Core`](../src/OrcaCore.Core) | Workflow builders, immutable definitions, lifecycle, policies, and composition nodes. |
| [`OrcaCore.Engine.Ephemeral`](../src/OrcaCore.Engine.Ephemeral) | In-process execution, waits, timers, management, governance, and ephemeral saga behavior. |
| [`OrcaCore.Engine.Durable`](../src/OrcaCore.Engine.Durable) | Event-sourced durable aggregate, replay, checkpointing, outbox, driver, and continuation execution. |
| [`OrcaCore.Hosting`](../src/OrcaCore.Hosting) | Dependency-injection registration, hosted pumps, lifecycle sweeps, and telemetry. |
| [`OrcaCore.Providers.InMemory`](../src/OrcaCore.Providers.InMemory) | In-memory provider implementation used by tests and local runs. |
| [`OrcaCore.Providers.PostgreSql`](../src/OrcaCore.Providers.PostgreSql) | PostgreSQL event, projection, timer, resource-pool, and outbox persistence. |
| [`OrcaCore.Providers.SqlServer`](../src/OrcaCore.Providers.SqlServer) | SQL Server provider implementation and migrations. |
| [`tests/`](../tests) | Core, engine, hosting, provider certification, integration, and support test projects. |
| [`samples/`](../samples) | Runnable console, generic-host, and Blazor dashboard examples. |
| [`benchmarks/`](../benchmarks) | BenchmarkDotNet scenarios for execution, providers, management, and scheduling. |

## Documentation areas

- [`specs/`](specs/README.md) — consolidated product requirements and acceptance criteria.
- [`implementation/`](implementation/README.md) — agent-executable implementation guide.
- [`orleans-engine/`](orleans-engine/README.md) — planned Orleans-hosted durable engine.
- [`architecture/`](architecture) — design decisions and runtime diagrams.
- [`durable/`](durable) — durable-runtime plans and historical implementation notes.
- [`plans/`](plans) — roadmaps, acceptance matrices, and planning documents.
- [`research/`](research) — prior-art and competitor studies.
- [`reviews/`](reviews) and [`review/`](review) — review findings and verification records.

The superseded prototype and its original root solution metadata are preserved
under [`../archive/legacy-poc/`](../archive/legacy-poc/).
