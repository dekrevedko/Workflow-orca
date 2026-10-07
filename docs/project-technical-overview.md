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

The source-approved exact nine-product-friend graph keeps implementation types non-public: `OrcaCore`
grants `OrcaCore.Core`, `OrcaCore.Engine.Ephemeral`, `OrcaCore.Engine.Durable`, and
`OrcaCore.Dag` for authoring;
`OrcaCore.Core` grants both engines; `OrcaCore.Engine.Durable` grants
`OrcaCore.Durable.Hosting`; `OrcaCore.Durable.Hosting` grants `OrcaCore.Dag.Hosting`; and
`OrcaCore.Dag` grants `OrcaCore.Dag.Hosting` for the exact runtime view.
Decision 22's authoring-only `OrcaCore -> OrcaCore.Dag` edge covers five compiler-created
build-value families and a shared fingerprint operation, subject to an exact compiled-member
guard; Task 8.2 source is independently approved at checkpoint
`a9f835f939d683500ca231c7ba491ab8eae2aaae`. Exact owning-test friends plus the
Durable Engine-to-ProviderCertification barrier edge remain closed. `OrcaCore.Dag.Hosting`
remains the only same-release DAG-to-durable *runtime* bridge; Kubernetes/AWS/job projects
depend outward and never appear in an OrcaCore signature/dependency closure.

`admit-dag-hosting-runtime-view` independently approves a ninth product friend,
`OrcaCore.Dag -> OrcaCore.Dag.Hosting`, for three immutable runtime-view types and fifteen
exact method/getter signatures. Its contract is independently approved at `43d869fc29e7daa3ec567d4602960f458eb98492` and canonical at independently approved checkpoint `a0da21ba9597e3864a3d4134120fbb0138417bd7`; compiled and independently source-approved at checkpoint `4eb2e8d3a68e0ae7d873bd4f54c53735beefb132`, with direct-child evidence `2fdfa59a747a5d1d1667abd63a8def88168a172b`, and it exposes
no delegates, drafts, constructors, test friendship, codec or child-start access to Dag.
The durable bridge decodes successful committed dependency outputs before mapping and owns
fixed-codec normalization, exact committed bytes/fingerprints and child start.

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

<!-- dag-run-contract-proposal:start -->
#### Approved durable DAG run contract; source pending (2026-10-05)

Propose first-class run persistence/fenced admission with explicitly catalogued advanced ports; existing workflow behavior, application signatures and nine friends stay unchanged.

Full exact independently approved contract: [Task 1.1 RC-3](../openspec/changes/admit-durable-dag-run-progression/artifacts/task-1-1-run-contract-rc-3-remediation-2026-10-05.md), with its exact API catalog. Earlier contract links below are rejected history. Approved RC-3 checkpoint e6802a3 / evidence 578593e; source pending. The four-block canonical transition is prepared for independent review, not yet checkpointed.

Rejected RC-1 predecessor (resolved lease parts retained by RC-3): Dag.Hosting owns admission/mapping coordination; Durable.Hosting owns bridge-only lease access and role-free committed child-join/cancel coordination. Provider FIFO grants one bounded mutation turn (5s lease, 15s absolute cap), then release before idle/child/capacity waits. New arrivals cannot starve prior joins/cancellation. Expired boot-host observer hints have bounded cleanup; compact changed-node events retain one current full checkpoint, not a full snapshot per event. The earlier linked contract is the rejected predecessor; the superseded rejected contract/API catalog was [RC-1 remediation](../openspec/changes/admit-durable-dag-run-progression/artifacts/task-1-1-run-contract-rc-1-remediation-2026-10-04.md). No source, canonical sync or new friend is claimed.

Rejected RC-2 refinement (2026-10-05; superseded by RC-3 below): read/decode/evaluate/normalize immutable inputs outside all lease turns; retain one physical evaluation per node/host and completed receipts across unrelated version conflicts/capacity deferral. Short commit turns only recheck and append. No mapper time budget or expiry failure exists. Exact operational pressure/gauges, provider production/test clocks, same-host FIFO outcomes, explicit captured host-limit parameter, reachable DAG-role cancellation and StartAuthorized-only persisted state were in the rejected RC-2 successor. DG-P19–21 require real-storage evidence before source approval.
RC-3 approved contract (2026-10-05; source pending): MaxConcurrentNodes remains ONLY the PER-RUN child/start-intent ceiling, never a host-global mapper pool. The distinct internal DagEvaluationParallelismPerRun=1 uses dedicated isolated run lanes and fair round-robin dispatch; a stuck mapper retains its own lane, not another run's permit. Evaluate only the lowest Ready ordinal in the free child window, zero look-ahead, one evaluation OR receipt/run/host; successful admission rechecks authored order under the fence, never mapper-completion order. Cross-host evaluation duplicates are allowed one/run/boot host. A newly Ready lower node invalidates a higher speculative receipt to avoid single-host order deadlock; capacity/time/unrelated versions do not. Non-success joins transitively block descendants. Ready hints ack bounded local wake acceptance before mapping and continuous wrapping discovery recovers acked/lost/poisoned hints. DG-P22–24 require real-storage ordering, isolation and wake/death evidence later, not current acceptance credit. Docs 08 CP-025/027, doc 12 AC-609/611/617 and doc 17's distinct-limit taxonomy keep their existing obligations. No public option, new friend or source implementation is claimed; only the four reviewed canonical blocks are prepared for sync.

Transition addendum: [1.3/1.4 P3 dispositions](../openspec/changes/admit-durable-dag-run-progression/artifacts/task-1-3-1-4-rc-3-transition-addendum-2026-10-05.md). Mapping may be evaluated once per run/boot host on competing hosts and after the named precommit invalidation; no exactly-once mapper invocation is promised.

<!-- dag-run-contract-proposal:end -->
