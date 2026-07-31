## 1. Reconcile Specifications and Public Vocabulary

- [x] 1.1 Approve the three-way per-step execution throttle / named cross-instance transient pool / durable resource lease taxonomy in the normative selected-mode matrix.
- [x] 1.2 Confirm the promoted `state-driven-runtime` composition rule: governance never replaces per-instance serialized mutation.
- [x] 1.3 Reconcile mode-guaranteed discoverability with `reshape-developer-facing-interfaces` and the archived structured-fiber baseline; static builders do not vary by later host composition.

## 2. Reuse the Delivered Ephemeral Substrate

- [x] 2.1 Inventory the delivered ephemeral advancement/general-body options as provisional implementation substrate, not approved v1 signatures; retain only the bounded-channel mechanics that can implement the exact matrix contract.
- [x] 2.2 Accept cancellation-safe token-channel mechanics for per-instance path, exact-step-type, and named-pool admission without accepting a fail-fast/capacity-wait-timeout policy.
- [x] 2.3 Accept structured-fiber transient-pool blocking through exact owner obligations that release the instance turn and allow runnable siblings to advance.
- [x] 2.4 Accept focused ephemeral tests for saturation, sibling progress, retry fairness, and cancellation/release.

## 3. Complete Durable Host Governance

- [x] 3.1 Implement the exact `StructuredExecutionHostOptions` under both role-specific engine options with only host-owned `MaxConcurrentExecutionPathsPerInstance` and `StepThrottles`; key every `StepExecutionThrottle.For<TStep>` solely by the exact named `Then<TStep>()` type, with no lambda/assignable/base-type target. Remove provisional host-wide advancement/general-body ceilings, fail-fast/capacity-wait-timeout policy, and any untyped/global/per-definition/category throttle scope. Register durable hosting only through `AddOrcaCoreDurableEngine(...)`; do not add a catch-all registration or separate hosted-service toggle. Define one countable execution-path token model: runnable roots/branches/items hold one token, release it on park/join, and fan-out parents release before child scheduling and reacquire only for merge/continuation. Do not retain or introduce a live-fiber admission resource.
- [x] 3.2 Enforce durable per-instance path and exact-step-type limits while allowing pumps and infrastructure dispatch to use explicitly separate capacity; forced workflow termination cancels pending admission but cannot release a granted transient slot until the actual guarded body returns/stops. A timed-out/fenced attempt drops commit authority and its logical path token but keeps its physical throttle/transient slot until return. Saturation always parks the exact owner and is exited only by grant or governing cancellation.
- [x] 3.3 Persist only the blocked fiber's admission obligation, not host-local slot ownership; the durable transition must commit that exact obligation and release the instance mutation turn before the host awaits capacity. After restart, reset capacity and re-evaluate exact-owner admission.
- [x] 3.4 Keep `WithTransientPool(TransientPoolName)` absent from durable root/nested builders and retain compiler rejection; record that future durable named-pool authoring requires a new matrix amendment rather than appearing automatically through host composition.
- [x] 3.5 Create every fixed root-`Parallel` branch fiber at scope start and schedule runnable branches fairly in authored order under `MaxConcurrentExecutionPathsPerInstance`; prove a host ceiling of one avoids parent-held path-token deadlock, compose a positive root-`ForEach` node-local `maxConcurrency` as the lower admitted-item ceiling, count admitted nonterminal item scopes separately from runnable path tokens, document the pending-item dependency caveat, and re-admit unfinished bounded durable items after restart without persisting host slots.

## 4. Verify Cross-Mode Governance

- [x] 4.1 Add durable and cross-mode tests where N instances exceed capacity K, cancellation/failure releases slots after guarded work stops, forced termination cannot free a slot from a still-running body, capacity misses commit/record the exact obligation and release the instance turn before waiting, local siblings progress, a path ceiling of one avoids parent-held-token deadlock, every fixed root-`Parallel` branch exists and token scheduling is authored-order fair, node/host root-`ForEach` ceilings compose by minimum while parked admitted items still count against the node cap, admitted-item dependence on pending work is explicitly not claimed to progress, restart re-admits unfinished items in index order without persisted host slots, parked started DAG children still count against `MaxConcurrentNodes` until terminal, timed-out token-ignoring bodies lose logical tokens but retain physical slots, durable restart re-admits host-owned work, durable named-pool authoring remains compile-absent, and exact/case-sensitive `TransientPoolName` lookup plus complete missing-pool `HostIncompatible` registration matches every ephemeral host configuration adapter without mutation.
- [x] 4.2 Re-run existing per-instance serialization and structured scheduler suites to prove governance changes timing but not workflow outcomes or mutation ordering.

## 5. Observability and Documentation

- [x] 5.1 Expose stable metrics or debug counters for configured limits, `TransientPoolName`, active slots, wait depth, cancellation, and host-compatibility validation failures without mixing transient pools with durable lease tickets; missing-pool registration and invalid startup configuration are distinct from saturation and there is no saturation-rejection counter.
- [x] 5.2 Update operator documentation for the countable execution-path token model, host-owned `MaxConcurrentExecutionPathsPerInstance`, node-local root-`ForEach` bounds, named-step-only exact-type throttles, the single-pool-per-step ephemeral `TransientPoolName` model, durable named-pool absence, infrastructure exclusions, restart semantics, and `ResourcePoolName` durable-lease alternatives; explicitly state that v1 has no host-wide advancement ceiling or custom transient-governance SPI and cross-link the exact root/root-`If`/root-`While`/root-`Parallel`-branch/root-`ForEach`-item lease placements, leased-body fan-out absence, `AmbiguousHeld` retry retention, quarantine transfer, and no-renewal review reconciliation without implementing those leases here.

**Implementation disposition:** all coordinated runtime-governance tasks are complete in the
current physical assemblies and are frozen with reshape Section 6 for independent exit review.
The inherited catch-all hosting surface is not claimed as removed here; its deletion and final
package ownership remain task 7.10 of `reshape-developer-facing-interfaces`.
