# Documentation map

The repository root is the active implementation. This folder contains the
requirements, architecture, implementation plans, research, review history, and
developer guides that explain it.

## Start here

1. [Normative source map](normative-source-map.md) — which sources are normative, how the two
   spec trees map to each other, and what is archived. **Read before changing any spec or doc.**
2. [Project README](../README.md) — public orientation, build/test commands, and samples.
3. [V1 public-surface baseline](specs/17-selected-mode-capability-matrix.md) — the approved
   greenfield release contract, including removed/deferred capabilities and package boundaries.
   Its exact authoring declarations are mirrored in
   [`17-public-authoring-contract.cs`](specs/17-public-authoring-contract.cs).
   The post-review caller-created strong-value construction decision is recorded in the
   [2026-07-19 amendment](review/developer-facing-interface-v1-strong-value-construction-amendment-2026-07-19.md).
4. [Active implementation index](active-implementation-index.md) — current runtime guides,
   durable-driver notes, operations, and handoffs.
5. [Project technical overview](project-technical-overview.md) — architecture and code map.
6. [Current refactor plan](implementation/developer-facing-interface-refactor-phased-plan-2026-07-14.md)
   — review-gated implementation order for the approved v1 surface. The April roadmap under
   `plans/` is a historical source snapshot.
7. [Durable development store reset](durable-development-store-reset.md) - required after
   provisional cursor checkpoints or incompatible compiled-plan changes.

## Active source layout

This table describes the current pre-release implementation, not a compatibility promise. The
active refactor will remove provisional members in place, add `OrcaCore.Dag` as an optional
project/package plus `OrcaCore.Dag.Hosting` as its sole durable bridge, and keep
Kubernetes/AWS/job scheduler code in a companion project with no
reverse dependency into OrcaCore. The OpenSpec task graphs linked from the implementation guide
are authoritative for that transition.

The solution file is [`OrcaCore.slnx`](../OrcaCore.slnx). The main projects are:

| Project | Purpose |
|---------|---------|
| [`OrcaCore.Abstractions`](../src/OrcaCore.Abstractions) | **PackageId `OrcaCore`** — application contracts, identifiers, workflow events, snapshots, facades. |
| [`OrcaCore.Core`](../src/OrcaCore.Core) | Authoring builders, immutable definitions, lifecycle, policies, compiler. |
| [`OrcaCore.Engine.Ephemeral`](../src/OrcaCore.Engine.Ephemeral) | In-process execution, waits, timers, governance; owns `AddOrcaCoreEphemeralEngine`. |
| [`OrcaCore.Runtime.Protocol`](../src/OrcaCore.Runtime.Protocol) | Durable commands, committed facts, checkpoints, envelopes. |
| [`OrcaCore.Provider.Abstractions`](../src/OrcaCore.Provider.Abstractions) | Provider ports, commit DTOs, certification contracts. |
| [`OrcaCore.Engine.Durable`](../src/OrcaCore.Engine.Durable) | Event-sourced durable aggregate, replay, checkpointing, outbox, driver. |
| [`OrcaCore.Durable.Hosting`](../src/OrcaCore.Durable.Hosting) | Owns `AddOrcaCoreDurableEngine` and callback-only `AddOrcaCoreDurableEventIngress`. |
| [`OrcaCore.Providers.InMemory`](../src/OrcaCore.Providers.InMemory) | Development/test provider; owns `AddOrcaCoreInMemoryDurableProvider`. |
| [`OrcaCore.Providers.PostgreSql`](../src/OrcaCore.Providers.PostgreSql) | Production provider; owns `AddOrcaCorePostgreSqlDurableProvider`. |
| [`OrcaCore.Dag`](../src/OrcaCore.Dag) | Typed DAG planning and operation contracts. |
| [`OrcaCore.Dag.Hosting`](../src/OrcaCore.Dag.Hosting) | Sole DAG-to-durable bridge; owns `AddOrcaCoreDag`. |
| *provisional — not in the v1 manifest* | `OrcaCore.Hosting`, `OrcaCore.Providers.SqlServer`, `.RabbitMq`, `.Redis`, `.ZeroMq`, `.Relational`. Slated for removal or relocation; do not build new work on them. |
| [`tests/`](../tests) | Core, engine, hosting, provider certification, integration, and support test projects. |
| [`samples/`](../samples) | Runnable console, generic-host, and Blazor dashboard examples. |
| [`benchmarks/`](../benchmarks) | BenchmarkDotNet scenarios for execution, providers, management, and scheduling. |

The approved v1 package direction, which the current source table is being migrated toward, is:

```text
OrcaCore (application contracts/authoring) <- OrcaCore.Core / engines / durable hosting
OrcaCore <- OrcaCore.Dag <- OrcaCore.Dag.Hosting <- companion scheduler/application
OrcaCore.Durable.Hosting <- OrcaCore.Dag.Hosting

OrcaCore.Runtime.Protocol <- OrcaCore.Provider.Abstractions <- provider adapters
```

That diagram summarizes dependency direction; it is not a package manifest. The exhaustive v1
manifest is `OrcaCore`, `OrcaCore.Core`, `OrcaCore.Engine.Ephemeral`,
`OrcaCore.Runtime.Protocol`, `OrcaCore.Provider.Abstractions`, `OrcaCore.Engine.Durable`,
`OrcaCore.Durable.Hosting`, `OrcaCore.Providers.InMemory`, `OrcaCore.Providers.PostgreSql`,
`OrcaCore.Dag`, and `OrcaCore.Dag.Hosting`, with exact edges and CLR ownership in
[`specs/17-selected-mode-capability-matrix.md`](specs/17-selected-mode-capability-matrix.md#175-package-and-integration-boundary).
There is no `OrcaCore.Hosting` PackageId; that CLR namespace is split across the approved owning
assemblies.

Microsoft hosting is role-specific: `OrcaCore.Engine.Ephemeral` owns
`AddOrcaCoreEphemeralEngine`; `OrcaCore.Durable.Hosting` owns `AddOrcaCoreDurableEngine` and
callback-only `AddOrcaCoreDurableEventIngress`; `OrcaCore.Providers.InMemory` owns development/test
`AddOrcaCoreInMemoryDurableProvider`; `OrcaCore.Providers.PostgreSql` owns production
`AddOrcaCorePostgreSqlDurableProvider`; and `OrcaCore.Dag.Hosting` owns `AddOrcaCoreDag`. Options
are constructed programmatically, copied, and validated at registration; no binder facade is part
of v1. There is no catch-all `AddOrcaCore` or separate hosted-service toggle.

## Documentation areas

- [`specs/`](specs/README.md) — consolidated product requirements and acceptance criteria.
- [`implementation/`](implementation/README.md) — agent-executable implementation guide.
- [`orleans-engine/`](orleans-engine/README.md) — planned Orleans-hosted durable engine.
- [`review/`](review) — dated review findings and verification records (frozen provenance).
- [`archive/`](archive/README.md) — superseded documentation: architecture, requirements, plans,
  research, durable notes. Provenance only, never current.

The superseded prototype and its original root solution metadata are preserved
under [`../archive/legacy-poc/`](../archive/legacy-poc/).
