# OrcaCore End-to-End Delivery Plan

This plan routes current integration work. Exact semantics come from both normative trees through
the [normative source map](normative-source-map.md); exact public ownership comes from the
[selected-mode capability matrix](specs/17-selected-mode-capability-matrix.md). Exact task state and
validation counts belong in the active OpenSpec task graphs and frozen review requests, not in this
guide.

## Current gate

Sections 4 through 7 of `reshape-developer-facing-interfaces` are independently approved and
checkpointed. The post-checkpoint Section 7A closure is active and owns the non-event public-surface
cleanup, exported API baseline machinery, packed negative consumers, recovery crosswalk, and timer
regression. Its event-surface baseline and final acceptance accounting wait for the pending Section
7B durable-messaging/application-catalog amendment.

Section 7B is a proposal, not current product authority. Until task 7.23 approves it, the selected
matrix and canonical specs continue to define the implemented event contract. Guides must not teach
the proposed buffered ingress, fanout, start-or-deliver, publish, dispatcher, or catalog APIs as
available v1 members.

The separate `harmonize-downstream-capability-specs` change is also pending independent planning
approval and still contains event semantics superseded by the Section 7B draft. Its non-conflicting
remainder must be reconciled and approved before any delta is synchronized into `openspec/specs/`.
Section 8 source work remains blocked until the combined Section 7A/7B target and the final
synchronized harmonization target are independently approved and checkpointed.

## Selected application journeys

End-to-end evidence is organized around public contracts rather than concrete engine or provider
implementations:

1. Author and build a typed definition from PackageId/assembly `OrcaCore`.
2. Register it through `IWorkflowDefinitionRegistry` and inspect the closed registration result.
3. Start or reopen through a typed definition handle with a deterministic idempotency key.
4. Deliver payloadless or typed events by exact instance or correlation route.
5. Inspect detached snapshot, root state, and typed output through the instance handle.
6. Request cooperative cancellation or immediate fenced termination through that handle.
7. Repeat the durable journey on a replacement host and a certified provider.

The ephemeral host role is registered by
`AddOrcaCoreEphemeralEngine(EphemeralEngineHostOptions)`. The durable host uses
`AddOrcaCoreDurableEngine(DurableEngineHostOptions)` or callback-only
`AddOrcaCoreDurableEventIngress`, together with exactly one durable provider role. Development and
test use `AddOrcaCoreInMemoryDurableProvider`; production certification targets
`AddOrcaCorePostgreSqlDurableProvider(PostgreSqlDurableProviderOptions)`.

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

## Workstream A: close Sections 7A and 7B

- finish non-event Section 7A cleanup and approve one exact exported type/member baseline for each
  of the eleven v1 assemblies after Section 7B fixes the event surface;
- reject every legacy/deferred public surface through metadata and fresh-package compile fixtures;
- preserve the currently approved event behavior until the Section 7B amendment is approved, then
  implement its descriptor, durable ingress/publish, route-inbox, catalog, and provider evidence;
- recalculate the exact current-v1 acceptance lane after Section 7B and preserve deferred or
  historical sources outside that positive lane;
- maintain a method-level old-declaration-to-current-evidence crosswalk before removing any source;
- retain the public-facade timer retry/no-loss regression;
- freeze the exact target and obtain focused independent approval.

No post-Section-7 checkpoint is created until one combined Section 7A/7B approval reproduces the
frozen target with zero drift.

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
