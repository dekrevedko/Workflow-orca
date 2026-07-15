## 1. Specifications

- [ ] 1.1 Review and approve `openspec/specs/runtime-resource-governance/spec.md` baseline wording.
- [ ] 1.2 Confirm alignment with `state-driven-runtime` composition note (per-instance serialization unchanged).
- [ ] 1.3 Reconcile with `reshape-developer-facing-interfaces` and `adopt-structured-fiber-execution` on the three-way per-step throttle / named cross-instance transient pool / durable resource lease taxonomy and strict-validate all three changes before adding source Interfaces.

## 2. Contracts and configuration surface

- [ ] 2.1 Define host-level options for global instance admission, per-step execution throttles, named cross-instance transient pools (key to max concurrency), wait/fail-fast policies, and optional timeouts without using durable-lease vocabulary.
- [ ] 2.2 Decide transient-pool binding through the mode-first selected-host contract and expose no authoring member for a host that does not enforce it.
- [ ] 2.3 Add internal transient-gate acquire/release contracts and keep persisted cross-host durable lease contracts separate.

## 3. Runtime integration

- [ ] 3.1 Ephemeral `WorkflowEngine`: enforce optional global limits at instance start and/or step execution boundaries without breaking serialized instance semantics.
- [ ] 3.2 `DurableWorkflowEngine`: same governance semantics for advancement and step bodies; ensure pumps/dispatch paths can be excluded or given separate pools.
- [ ] 3.3 Integrate cooperative local-fiber pool acquisition through owned blocked obligations that release the instance turn; keep true-concurrency pool acquisition on external jobs, child workflows, cross-instance advancement, and explicitly classified infrastructure paths.

## 4. Testing

- [ ] 4.1 Unit tests for per-step throttle and transient-pool starvation, fairness basics, step-boundary release, failure release, and distinct durable-lease naming.
- [ ] 4.2 Acceptance tests where N instances exceed a transient pool of K, cancellation releases slots, and durable host restart resets capacity and re-evaluates blocked admission.
- [ ] 4.3 Regression: existing MC-AT concurrency serialization tests still pass.

## 5. Observability and documentation

- [ ] 5.1 Expose minimal metrics or debug counters for pool wait depth (implementation-dependent).
- [ ] 5.2 Update operator-facing documentation with recommended patterns for database and HTTP limits.
