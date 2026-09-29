# OrcaCore — technical overview

> **Current-source map:** the Section 7 checkpoint is implemented; the post-checkpoint Section 7A
> gate is closing residual public-surface and test-evidence gaps against
> [spec 17](specs/17-selected-mode-capability-matrix.md). Internal implementation types named below
> are not consumer contracts.

The exact planned authoring declarations are mirrored in
[`17-public-authoring-contract.cs`](specs/17-public-authoring-contract.cs). It is normative
alongside document 17 for signature review; the code map below remains a map of today's tree.

OrcaCore is a .NET workflow engine focused on composable workflow definitions,
ephemeral in-process execution, durable event-sourced execution, and pluggable
provider infrastructure. The active solution and all implementation code are at
the repository root.

## Runtime model

The product has two execution modes over application contracts in `OrcaCore` and an internal
compiler/execution kernel in `OrcaCore.Core`:

1. **Ephemeral** — the internal ephemeral engine executes registered definitions in process;
   applications compose it through `AddOrcaCoreEphemeralEngine`, `IWorkflowDefinitionRegistry`,
   and typed handles. Runtime state and waits are lost when the host exits; no durable external
   ingress acknowledgement is exposed.
2. **Durable** — the internal durable runtime persists aggregate events, checkpoints, waits, timers,
   inbox/outbox records, and projections through provider ports. `IWorkflowEventIngress` accepts
   direct, correlation, definition-fanout, and start-or-deliver routes; the durable driver advances
   a workflow segment until it completes or yields a restart-safe continuation.

PostgreSQL, SQL Server, and the development/test in-memory provider implement the current
durable-provider source roles. SQL Server is the third first-release provider and implements the
complete current split ports with SQL Server-native migration and real-storage certification; the
deleted provisional SQL Server shape was replaced rather than restored. Broker, Redis, and shared Relational
source/test roots remain removed, and durable storage providers beyond PostgreSQL and SQL Server
require a future amendment and complete current certification. Hosting packages register internal
runtimes, continuation processing, operational services, and telemetry.

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
`OrcaCore.Providers.SqlServer`, `OrcaCore.Dag`, and `OrcaCore.Dag.Hosting`; exact direct edges and CLR namespace ownership are
normative in [`specs/17-selected-mode-capability-matrix.md`](specs/17-selected-mode-capability-matrix.md#175-package-and-integration-boundary).
`OrcaCore.Hosting` is a shared CLR namespace, not a PackageId.

The current exact eight-product-friend graph keeps implementation types non-public: `OrcaCore`
grants `OrcaCore.Core`, `OrcaCore.Engine.Ephemeral`, `OrcaCore.Engine.Durable`, and
`OrcaCore.Dag` for authoring;
`OrcaCore.Core` grants both engines; `OrcaCore.Engine.Durable` grants
`OrcaCore.Durable.Hosting`; and `OrcaCore.Durable.Hosting` grants `OrcaCore.Dag.Hosting`.
Decision 22's authoring-only `OrcaCore -> OrcaCore.Dag` edge covers five compiler-created
build-value families and a shared fingerprint operation, subject to an exact compiled-member
guard; the Task 8.2 source checkpoint remains under independent review. Exact owning-test friends plus the
Durable Engine-to-ProviderCertification barrier edge remain closed. `OrcaCore.Dag.Hosting`
remains the only same-release DAG-to-durable *runtime* bridge; Kubernetes/AWS/job projects
depend outward and never appear in an OrcaCore signature/dependency closure.

Each step attempt runs on a codec-detached copy of committed state and can replace that copy via
`StepContext<TState>.ReplaceState`; only the winning attempt commits. The first release fixes the
certified workflow-state format to `orcacore-json-v1`. Durable event identity is global by
`EventId` plus the full normalized-envelope fingerprint before route evaluation. Accepted events
remain provider-owned until applied or observably poisoned; definition fanout freezes one stable
current nonterminal target set and deduplicates independently per target.

Microsoft hosting uses role-specific owners and entry points: `OrcaCore.Engine.Ephemeral` owns
`AddOrcaCoreEphemeralEngine`; `OrcaCore.Durable.Hosting` owns `AddOrcaCoreDurableEngine` and
callback-only `AddOrcaCoreDurableEventIngress`; `OrcaCore.Providers.InMemory` owns development/test
`AddOrcaCoreInMemoryDurableProvider`; `OrcaCore.Providers.PostgreSql` owns production
`AddOrcaCorePostgreSqlDurableProvider`; `OrcaCore.Providers.SqlServer` owns production
`AddOrcaCoreSqlServerDurableProvider`; and `OrcaCore.Dag.Hosting` owns `AddOrcaCoreDag`. Options
are programmatically constructed, copied, and validated without a binder facade. There is no v1
catch-all registration or separate hosted-service switch. Workflow-authored `Publish` commits
through the durable outbox and reaches `IWorkflowEventDispatcher`; internal continuations never do.
Applications receive no broad workflow enumeration/statistics or public archive/purge contract.
Provider/operator roles own statistics and retention through `IWorkflowOperationalStore` and
`IWorkflowProviderMaintenanceStore`.

## Code map

| Area | Location | Notable responsibilities |
|------|----------|--------------------------|
| Public application contract | `src/OrcaCore.Abstractions/` (package/assembly `OrcaCore`) | IDs, typed definitions and handles, authoring, steps, application events, results, and detached snapshots. |
| Internal authoring/runtime kernel | `src/OrcaCore.Core/` | Definition compilation, lifecycle transitions, policies, codecs, and structured-execution machinery shared only with the two engines. |
| Ephemeral engine | `src/OrcaCore.Engine.Ephemeral/` | Public ephemeral registration/options plus the internal execution loop, wait/event routing, timers, and in-memory lifecycle. |
| Durable engine | `src/OrcaCore.Engine.Durable/` | Internal aggregate decisions, replay, checkpoint interpretation, command handling, outbox materialization, and continuation signals. |
| Durable hosting | `src/OrcaCore.Durable.Hosting/` | Public durable engine/ingress registration, management and diagnostics facades, plus internal hosted pumps and operational sweeps. |
| Provider contract and protocol | `src/OrcaCore.Provider.Abstractions/`, `src/OrcaCore.Runtime.Protocol/` | Advanced split provider ports/commit records and advanced durable wire/storage records; neither is an ordinary application surface. |
| V1 providers | `src/OrcaCore.Providers.InMemory/`, `src/OrcaCore.Providers.PostgreSql/`, `src/OrcaCore.Providers.SqlServer/` | Development/test in-memory registration plus complete independently certified PostgreSQL and SQL Server production role sets. |
| DAG boundary | `src/OrcaCore.Dag/`, `src/OrcaCore.Dag.Hosting/` | Reserved v1 DAG packages; Section 8 implementation remains gated. |
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
