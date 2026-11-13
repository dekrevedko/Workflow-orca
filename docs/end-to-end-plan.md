# OrcaCore End-to-End Delivery Plan

This plan routes current integration work. Exact semantics come from both normative trees through
the [normative source map](normative-source-map.md); exact public ownership comes from the
[selected-mode capability matrix](specs/17-selected-mode-capability-matrix.md). Exact task state and
validation counts belong in the active OpenSpec task graphs and frozen review requests, not in this
guide.

## Current gate

Sections 4 through 7 of `reshape-developer-facing-interfaces`, including the Section 7A/7B
developer-facing contract, are independently approved and checkpointed. Durable self-routing
ingress, pre-wait retention, definition fanout, start-or-deliver, workflow-authored `Publish`, the
application dispatcher, the fixed codec, exact host roles, and the three certified durable
providers are current product authority.

The separate `harmonize-downstream-capability-specs` change has synchronized the canonical
requirements and is reconciling the remaining active guides. Section 8 source work remains blocked
until that harmonization target is independently approved and checkpointed; this guide does not
grant Section 8 authority.

## Selected application journeys

End-to-end evidence is organized around public contracts rather than concrete engine or provider
implementations:

1. Author and build a typed definition from PackageId/assembly `OrcaCore`.
2. Register it through `IWorkflowDefinitionRegistry` and inspect the closed registration result.
3. Start or reopen through a typed definition handle with a deterministic idempotency key.
4. Submit a payloadless or typed durable inbound event through direct, correlation,
   definition-fanout, or start-or-deliver routing.
5. Inspect detached snapshot, root state, and typed output through the instance handle.
6. Request cooperative cancellation or immediate fenced termination through that handle.
7. Repeat the durable journey on a replacement host and a certified provider.

The ephemeral host role is registered by
`AddOrcaCoreEphemeralEngine(EphemeralEngineHostOptions)`. The durable host uses
`AddOrcaCoreDurableEngine(DurableEngineHostOptions)` or callback-only
`AddOrcaCoreDurableEventIngress`, together with exactly one durable provider role. Development and
test use `AddOrcaCoreInMemoryDurableProvider`; production certification covers
`AddOrcaCorePostgreSqlDurableProvider(PostgreSqlDurableProviderOptions)` and
`AddOrcaCoreSqlServerDurableProvider(SqlServerDurableProviderOptions)`.

## Validation lanes

Every review target records and reproduces these lanes independently:

- clean Release solution build with zero warnings and errors;
- active product, acceptance, hosting, provider-certification, and integration suites;
- PostgreSQL container suites when Docker is available, with environment failures reported
  separately from product failures;
- infrastructure guards separated from intentional expected-red future-section scenarios;
- current-tree and fresh-package public API baselines plus packed compile fixtures;
- strict validation of every active OpenSpec change;
- active-document vocabulary and link checks;
- live package vulnerability audit when network policy permits;
- `git diff --check` and exact ordered dirty-manifest reproduction before and after validation.

Container-backed provider suites run sequentially to avoid shared Docker resource contention. Safe
non-container build/test lanes may run in parallel when they use independent outputs.

## Workstream A: completed Sections 7A and 7B

- the exact twelve-package surface, role-specific hosting, three durable providers, and exported
  type/member baselines are implemented and checkpointed;
- durable ingress accepts the closed four-route union, retains accepted pre-wait records, and uses
  global event identity plus stable fanout membership and start-intent ownership;
- durable workflow-authored `Publish` commits through the transactional outbox and reaches only
  `IWorkflowEventDispatcher`; internal continuation records stay isolated;
- application-facing broad enumeration/statistics and public retention commands remain absent,
  while provider/operator ports own retained statistics and maintenance;
- removed and deferred surfaces remain rejected by metadata and fresh-package compile fixtures.

## Workstream B: harmonize normative and guide trees

- remove the event-routing/durable-messaging ownership superseded by Section 7B and independently
  approve the non-conflicting harmonization remainder against the unchanged canonical baseline;
- only then synchronize approved deltas and the explicitly listed purpose text;
- reconcile numbered requirements, acceptance criteria, active guides, and the future-capability
  registry without rewriting frozen review or archived history;
- enforce active-document vocabulary through an exact classified-occurrence baseline rather than a
  broad file allowlist;
- freeze and independently approve the final canonical/docs target before its checkpoint.

## Workstream C: Section 8 DAG boundary

Section 8 begins only after both preceding workstreams are approved and checkpointed. Its task graph
owns the typed DAG packages, the single internal durable child bridge, runtime-owned progression,
restart/cancellation behavior, and outward companion scheduler boundary. No current guide or test
may pre-approve those source changes.

## Observability and operations

Product packages emit BCL logs, activities, and metrics. The application host owns OpenTelemetry
SDK/exporter registration; OrcaCore publishes no exporter-registration facade. Operator dashboards
consume documented diagnostics and provider/runtime projections without adding broad application
instance enumeration or a public statistics query.

Provider-owned maintenance may clean operational records while preserving runtime correctness and
documented inspection guarantees. It does not create a provider-neutral application retention API.

## Checkpoint rule

After each independently approved phase or other large coherent change:

1. verify the current worktree differs from the frozen manifest only by the new immutable verdict;
2. stage the complete approved target and its verdict;
3. create one coherent checkpoint commit;
4. verify the resulting worktree state before starting the next phase.

The superseded pre-v1 plan and its historical counts/workstreams are preserved unchanged at
[`archive/plans/end-to-end-plan-pre-v1.md`](archive/plans/end-to-end-plan-pre-v1.md).
