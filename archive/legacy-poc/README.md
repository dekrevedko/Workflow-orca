# OrcaCore

**OrcaCore** (**Workflow Orca**) is a .NET workflow engine experiment: in-process orchestration with reusable steps and **pluggable** storage and messaging. The project is public so design discussions, requirements, and code can reach a wider audience.

> **Status:** active research and prototyping — **not** production-ready. APIs and persistence shapes may change.

## Two engines

Workflow Orca splits **state-driven orchestration** (mutable runtime state, explicit step transitions) from an experimental **append-only, event-driven** slice. Both use the same high-level ideas (`IStep`, `EventEnvelope`, waits, correlation), but persistence and replay models differ.

| Engine | Projects & entry points | Best for | Execution model |
|--------|-------------------------|----------|-----------------|
| **State-driven** | [`OrcaCore.Abstractions`](src/OrcaCore.Abstractions) + [`OrcaCore.Runtime`](src/OrcaCore.Runtime): **`WorkflowEngine`** (in-memory via `InMemoryInstanceStore`) and **`DurableWorkflowEngine`** with **`IWorkflowStore`** (persisted instances, waits, outbox, history) | Production-shaped experiments today: ephemeral runs, or durable runs with a pluggable store contract | The engine advances **orchestration state** in memory and, in the durable path, **persists** frames, waits, and related records through `IWorkflowStore` — not an append-only domain event log as the primary source of truth. |
| **Event-driven (prototype)** | [`OrcaCore.EventDrivenPrototype`](src/OrcaCore.EventDrivenPrototype): **`EventDrivenWorkflowEngine`** | Proving long-running semantics with an **append-only per-instance stream**, checkpoints, and projections | Facts are **appended**; checkpoints and projections support resume, deduplication, and routing. Currently in-memory store only. |

See [Code map & types](docs/project-technical-overview.md#code-map) for a file-level index, and [Quick vs durable event-driven engine — feature matrix](docs/architecture/quick-vs-durable-engine-feature-matrix.md) for capabilities.

## Goals

- **Composable workflows** — control-flow and business steps: `Init` / `End`, `If` / `While`, `Parallel` (branch + join semantics in tests; not separate `WhenAll`/`WhenFirst` APIs), `Wait`, durable-only `WaitLong`, and user-defined `IStep` types.
- **Two engines, one product direction** — state-driven ephemeral + durable paths in `OrcaCore.Runtime`; append-only event-driven exploration in `OrcaCore.EventDrivenPrototype` (convergence TBD).
- **Replaceable infrastructure** — persistence and dispatch behind contracts rather than a single hard-coded backend.
- **Clear semantics** — explicit modeling of waits, sagas vs regular workflows, and operational hooks as the design matures.

See [docs/project-technical-overview.md](docs/project-technical-overview.md) for architecture notes, durable-runtime details, core concepts, and links to the full requirements and research tree.
For the current implementation status and feature-by-feature roadmap, see [docs/plans/current-roadmap.md](docs/plans/current-roadmap.md).

## Current stage

| Area | State |
|------|--------|
| **State-driven runtime** — `OrcaCore.Runtime` + `OrcaCore.Abstractions` | `WorkflowEngine`, `DurableWorkflowEngine`, `IWorkflowStore`, outbox pump, correlation routing; covered by `OrcaCore.Tests`. |
| **Documentation** | Requirements, architecture, and research notes under [docs/](docs/README.md). |
| **Event-driven prototype** — `OrcaCore.EventDrivenPrototype` | Append-only stream, checkpoints, projections, straight-line steps + `Wait`, correlation, restart-safe resume. See [event-driven prototype status](docs/architecture/event-driven-prototype-status.md). |

The prototype intentionally does **not** yet match the state-driven builder surface (no `If` / `While` / `Parallel` / `WaitLong`, timers, or outbox). **Saga** workflows are specified in requirements docs only — there is no `SagaDefinition` in source yet.

## Roadmap: research → production-ready

Rough phases; overlap is expected.

1. **Research & specification** — nail execution model, wait/residency semantics, branch rules, idempotency, and provider boundaries (ongoing; see [docs/requirements/](docs/requirements/README.md) and [docs/research/](docs/research/research-backlog.md)).
2. **Core runtime convergence** — stabilize APIs, close gaps in tests vs requirements, document breaking-change policy.
3. **Durable hardening** — operational commands, retention, observability, and at-least-once / replay behavior validated under realistic adapters.
4. **Event-driven path** — evolve the prototype into a supported execution style or merge learnings into the main engine; extract real store contracts from the in-memory prototype.
5. **Production readiness** — versioning for long-running instances, security review, performance targets, sample host apps, and published packages with semantic versioning — **explicit non-goal** until earlier phases are satisfied.

## Build and test

Requires a [.NET SDK](https://dotnet.microsoft.com/download) compatible with **.NET 10** (see project files for `TargetFramework`).

```bash
dotnet build OrcaCore.slnx
dotnet test OrcaCore.slnx
```

## Documentation

- [Documentation map](docs/README.md)
- [Current roadmap](docs/plans/current-roadmap.md)
- [Technical overview (detailed)](docs/project-technical-overview.md)
- [Event-driven prototype status](docs/architecture/event-driven-prototype-status.md)

## Contributing

Issues and PRs are welcome. Because the project is still in a research phase, it helps to align larger changes with the documented requirements or open an issue first.

## License

Licensed under the [Apache License 2.0](LICENSE).
