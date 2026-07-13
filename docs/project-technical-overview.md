# OrcaCore — technical overview

OrcaCore is a .NET workflow engine focused on composable workflow definitions,
ephemeral in-process execution, durable event-sourced execution, and pluggable
provider infrastructure. The active solution and all implementation code are at
the repository root.

## Runtime model

The product has two execution modes over the shared `OrcaCore.Core` definition
model and `OrcaCore.Abstractions` contracts:

1. **Ephemeral** — `EphemeralWorkflowEngine` executes definitions in process.
   Timers, management, governance, waits, composition, and limited saga behavior
   are held in memory and are lost when the host exits.
2. **Durable** — `DurableWorkflowRuntime` and `OrcaCore.Engine.Durable` persist
   aggregate events, checkpoints, waits, timers, inbox/outbox records, and
   projections through provider ports. The durable driver advances a workflow
   segment until it completes or yields a restart-safe continuation.

PostgreSQL, SQL Server, and in-memory providers implement the durable persistence
ports. Hosting packages register the runtime, continuation pump, operational
services, and telemetry.

## Code map

| Area | Location | Notable responsibilities |
|------|----------|--------------------------|
| Public contracts | `src/OrcaCore.Abstractions/` | IDs, steps, events, snapshots, provider ports, codecs, and durable commands/events. |
| Definition model | `src/OrcaCore.Core/` | `WorkflowBuilder`, definition nodes, lifecycle transitions, policies, DAGs, sagas, and child-workflow composition. |
| Ephemeral runtime | `src/OrcaCore.Engine.Ephemeral/` | Execution loop, wait/event routing, timers, management, governance, and in-memory lifecycle. |
| Durable runtime | `src/OrcaCore.Engine.Durable/` | Aggregate decisions, replay, checkpoints, command pipeline, outbox, driver, and continuation signals. |
| Hosting | `src/OrcaCore.Hosting/` | DI extensions, hosted pumps, operational sweeps, and telemetry observers. |
| Providers | `src/OrcaCore.Providers.*` | In-memory, relational, PostgreSQL, SQL Server, RabbitMQ, Redis, and ZeroMQ adapters. |
| Tests | `tests/` | Unit, acceptance, hosting, provider certification, integration, and support fixtures. |
| Samples | `samples/` | Public-API console examples, generic host, and Blazor operations dashboard. |

The repository solution is [`OrcaCore.slnx`](../OrcaCore.slnx), and package
versions are centrally managed by [`Directory.Packages.props`](../Directory.Packages.props).

## Durable execution boundary

The durable aggregate is the consistency root: it rehydrates from provider
events, applies commands, and returns an atomic commit batch. The command
processor materializes checkpoints and outbox work; the driver interprets the
definition from the checkpoint until the next durable boundary. Providers own
storage and leasing, while hosting owns recurring pump scheduling.

This separation keeps workflow semantics testable without a database while
allowing provider certification and Testcontainers-backed integration tests to
exercise the real persistence boundary.

## Repository layout

```text
src/       active production projects
tests/     unit, acceptance, provider, and integration tests
samples/   runnable examples and dashboard
benchmarks/ performance scenarios
docs/      requirements, plans, architecture, and guides
archive/legacy-poc/  superseded prototype and original root metadata
OrcaCore.slnx
```

For API walkthroughs and current verification commands, see the
[active implementation index](active-implementation-index.md). For intended
behavior, use the [requirements index](specs/README.md).
