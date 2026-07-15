The normative selected-mode availability, naming, and lifetime taxonomy is
[`docs/specs/17-selected-mode-capability-matrix.md`](../../../docs/specs/17-selected-mode-capability-matrix.md).

## Context

OrcaCore already guarantees **serialized advancement per workflow instance** (one logical mutator at a time). That prevents overlapping mutation of the same instance but does **not** limit how many **different** instances or steps run at once, nor does it cap shared external resources (database connections, outbound dispatch, CPU-bound fan-out). Parallel workflow branches exist in the model; the current interpreter may execute branch paths sequentially while preserving join semantics—true concurrent branch scheduling is a separate optimization.

Operational requirements often need:

- A ceiling on **how many instances** are actively executing transitions (or step bodies) at once.
- A ceiling on **how many steps** run concurrently across the whole host (or per definition).
- A **shared pool** keyed by name (for example `db-updates`) with a fixed concurrency budget reused by multiple step types or workflows.

The developer-facing taxonomy has three non-interchangeable categories:

1. **Per-step execution throttle**: host-local capacity held only around one step body.
2. **Named cross-instance transient pool**: host-local shared capacity across instances, supported by selected ephemeral or durable hosts but reset and re-evaluated after host restart.
3. **Durable resource lease**: persisted cross-host capacity owned by a fiber/scope with queueing, deterministic release, expiry, and recovery.

This change owns the first two categories. Durable resource leases are a separate durable-runtime contract and are not implemented or implied by an in-process semaphore.

This design defines governance as an optional layer that composes with existing instance serialization.

## Goals / Non-Goals

**Goals**

- Specify configurable limits for: (1) concurrent workflow instance execution (advancement slots), (2) per-step execution throttles (global or scoped), and (3) named cross-instance transient pools with independent host-local limits.
- Define composition rules with **per-instance locks** and with **durable rehydration** (limits apply in-process; distributed locks are an extension).
- Provide clear semantics when a step cannot acquire a slot (block with cancellation, fail-fast, or surface backpressure—decisions recorded below).
- Allow authors or operators to associate steps or definitions with pool keys without embedding provider-specific details in business state.

**Non-Goals (initial phase)**

- Distributed enforcement and persisted durable resource leases across multiple processes or nodes.
- Replacing per-instance serialization with pool-based locking.
- Changing parallel join correctness; governance may restrict **when** work runs, not **what** outcome is computed.

## Decisions

### 1. Separation of concerns: instance serialization vs resource governance

- **Per-instance execution lock** (existing): ensures at most one logical mutator advances a given instance’s durable/interpreter state at a time.
- **Resource gates** (new): optional acquisition **before** a step body runs (or before a bounded region), possibly shared across instances.

Composition: a step transition still runs under instance serialization rules; governance adds an additional **optional wait** for a pool or global slot **inside** the serialized critical section when the implementation chooses to gate at step boundaries. Alternative: gate **outer** worker dispatch so only N instances run—both are valid; the spec allows either if observable behavior matches configured limits.

**Recommended default for “max N DB operations”:** implement **named pool** acquire/release bracketing **only** the database work inside the step (or inside a dedicated adapter), keeping instance lock scope minimal if the runtime later splits “plan transition” vs “side effect.” For the current interpreter, instance lock may span the whole step; pool acquisition still limits **cross-instance** concurrent DB work because each instance releases the pool after the step completes.

### 2. Per-step throttles, named transient pools, and durable leases

- **Per-step execution throttle**: one transient limit around business-step bodies, optionally global, per definition, or per step category.
- **Named cross-instance transient pools**: multiple independent host-local limits; a step may participate in one or more pools such as `db` and `http`. Durable hosts re-evaluate transient admission after restart and do not claim that pool ownership survived.
- **Durable resource leases**: excluded from this change; they use persisted cross-host capacity and scope-owned lifecycle rather than these transient gates.

Transient pools are identified by **stable string keys** configured at host startup or definition metadata. Public binding and mode discoverability follow `reshape-developer-facing-interfaces`; no selected-mode builder exposes a transient-pool method unless its host enforces the declared semantics.

### 3. Blocking vs reject when saturated

- Default cross-instance admission policy: **async wait** for a slot with **cancellation** tied to host shutdown and step cancellation tokens.
- Local fiber policy: represent unavailable capacity as an owned blocked resource obligation, end the fiber quantum, and release the instance mutation turn. Ephemeral mode keeps the obligation in memory; durable mode commits it. Grant or cancellation transitions make the exact owner runnable or terminal. A local step never awaits a pool while retaining the instance turn.
- Optional policy (operator-defined): **fail fast** with a typed error so callers can retry externally—useful for overload protection.

The requirements scenario will assert **deterministic cancellation** when the host stops.

### 4. Parallel branches and future scheduling

- Local `Parallel`, `WhenFirst`, and ephemeral `ForEach` branches are deterministic cooperative fibers over isolated branch/item state. At most one local business-step body per instance executes at a time; pool saturation blocks only the selected fiber and affects scheduler ordering without retaining the instance turn.
- True concurrent work remains explicit through external jobs, child workflow instances, host advancement across different instances, and infrastructure dispatch. Those paths acquire resource-governance slots under their own ownership and concurrency policies; parent-instance mutation remains serialized.

### 5. Durable and outbox pumps

- Outbox dispatch and replay workers may need **separate** pools or exclusions so governance does not starve internal bookkeeping; the spec treats “workflow step execution” as distinct from **infrastructure dispatch** unless explicitly configured.

### 6. Observability

- Implementations SHOULD expose metrics or hooks: active waits on each pool, rejected/waiting counts, and configured limits (for operators).

## Risks / Trade-offs

- **Deadlock risk** if instance lock is held while waiting on a pool and another holder waits on instance ordering—mitigate by short critical sections, consistent lock ordering (always acquire instance lock before pool, or document the opposite), and avoiding nested pools without ordering rules.
- **Cross-process limits** require a distributed semaphore or partition assignment—not covered in v1.
- **Fairness**: FIFO wait on pools is recommended but not always required; prioritize documented behavior under load tests.

## Migration Plan

1. Land specification and API sketch in workflow/runtime contracts (follow-up implementation change).
2. Implement in-process throttles and named transient pools for the ephemeral host first; expose them on a durable host only with the same host-local semantics and explicit restart reset behavior.
3. Add acceptance tests: pool saturation, cancellation, cross-instance contention, interaction with instance serialization.

## Open Questions

- Should pool keys be declared on `IStep`, workflow definition metadata, or host configuration only?
- Should global instance concurrency count instances in **Waiting** state or only **Running** advancement?
- Integration with host DI for custom pool factories (testing vs production).
