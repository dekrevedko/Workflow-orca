# OrcaCore — technical overview

> **Current-source map, not the approved v1 API:** this document names provisional runtime
> features that still exist in the pre-release tree. The migration target is
> [spec 17](specs/17-selected-mode-capability-matrix.md); the active OpenSpec tasks remove or
> internalize deferred members before the first release.

The exact planned authoring declarations are mirrored in
[`17-public-authoring-contract.cs`](specs/17-public-authoring-contract.cs). It is normative
alongside document 17 for signature review; the code map below remains a map of today's tree.

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

## Approved v1 target boundaries

The refactor is converging on this package direction:

```text
OrcaCore (application contracts/authoring) <- OrcaCore.Core / engines / durable hosting
OrcaCore <- OrcaCore.Dag <- OrcaCore.Dag.Hosting <- companion scheduler/application
OrcaCore.Durable.Hosting <- OrcaCore.Dag.Hosting

OrcaCore.Runtime.Protocol <- OrcaCore.Provider.Abstractions <- provider adapters
```

This is a dependency-direction summary, not a partial package declaration. The exhaustive v1
manifest is `OrcaCore`, `OrcaCore.Core`, `OrcaCore.Engine.Ephemeral`,
`OrcaCore.Runtime.Protocol`, `OrcaCore.Provider.Abstractions`, `OrcaCore.Engine.Durable`,
`OrcaCore.Durable.Hosting`, `OrcaCore.Providers.InMemory`, `OrcaCore.Providers.PostgreSql`,
`OrcaCore.Dag`, and `OrcaCore.Dag.Hosting`; exact direct edges and CLR namespace ownership are
normative in [`specs/17-selected-mode-capability-matrix.md`](specs/17-selected-mode-capability-matrix.md#175-package-and-integration-boundary).
`OrcaCore.Hosting` is a shared CLR namespace, not a PackageId.

`OrcaCore.Dag.Hosting` is the only same-release friend bridge to the versioned internal child
start/join seam; it is not a public child-management or provider SPI. Kubernetes/AWS/job projects
depend outward on that bridge and never appear in an OrcaCore signature/dependency closure.

Each step attempt runs on a codec-detached copy of committed state and can replace that copy via
`StepContext<TState>.ReplaceState`; only the winning attempt commits. The first release fixes the
certified workflow-state format to `orcacore-json-v1`. Durable event dedup is per target instance
and event ID, while correlation routing permits exactly one active wait per
`(DefinitionId, EventName, CorrelationId)`.

Microsoft hosting uses role-specific owners and entry points: `OrcaCore.Engine.Ephemeral` owns
`AddOrcaCoreEphemeralEngine`; `OrcaCore.Durable.Hosting` owns `AddOrcaCoreDurableEngine` and
callback-only `AddOrcaCoreDurableEventIngress`; `OrcaCore.Providers.InMemory` owns development/test
`AddOrcaCoreInMemoryDurableProvider`; `OrcaCore.Providers.PostgreSql` owns production
`AddOrcaCorePostgreSqlDurableProvider`; and `OrcaCore.Dag.Hosting` owns `AddOrcaCoreDag`. Options
are programmatically constructed, copied, and validated without a binder facade. There is no v1
catch-all registration or separate hosted-service switch.

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
