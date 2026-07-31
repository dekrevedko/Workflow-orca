The normative selected-mode availability, naming, and lifetime taxonomy is
[`docs/specs/17-selected-mode-capability-matrix.md`](../../../docs/specs/17-selected-mode-capability-matrix.md).

## Context

OrcaCore already guarantees **serialized advancement per workflow instance** (one logical mutator at a time). That prevents overlapping mutation of the same instance but does **not** limit how many **different** instances or steps run at once, nor does it cap shared external resources (database connections, outbound dispatch, CPU-bound fan-out). Root `Parallel` workflow branches exist in the model; the current interpreter may execute branch paths sequentially while preserving join semantics—true concurrent branch scheduling is a separate optimization.

Operational requirements often need:

- A host-owned ceiling on **how many runnable structured paths** one instance may admit.
- A ceiling on **how many steps of one exact configured step type** run concurrently across the host.
- A **shared pool** keyed by name (for example `db-updates`) with a fixed concurrency budget reused by multiple step types or workflows.
- A bounded root `ForEach` node that may request a smaller local fan-out ceiling than the host permits.

The developer-facing taxonomy has three non-interchangeable categories:

1. **Per-step execution throttle**: host-local capacity keyed only by the exact named `Then<TStep>()` type and held only around that step body; lambda bodies have no exact-type throttle target.
2. **Named cross-instance transient pool**: host-local shared capacity identified by `TransientPoolName` across instances. Ephemeral authoring is delivered; durable authoring is deferred from the first release and remains absent even as durable host-owned path/step governance lands.
3. **Durable resource lease**: persisted cross-host capacity identified by distinct `ResourcePoolName`, owned by an exact lexical fiber/scope occurrence with atomic queueing, `AmbiguousHeld` retry retention, quarantine transfer, deterministic release, and owner-state reconciliation. Its pool-owned review deadline marks ownership for reconciliation; it is not an expiry or TTL and does not reclaim capacity from elapsed time alone.

This change owns the first two categories. Durable resource leases are a separate durable-runtime contract and are not implemented or implied by an in-process semaphore.

This design defines governance as an optional layer that composes with existing instance serialization.

The current implementation contains useful bounded-channel and blocked-obligation mechanics plus
provisional advancement/general-body options. Those mechanics are substrate evidence, not the v1
public contract. Both roles converge on the exact `StructuredExecutionHostOptions` baseline:
`MaxConcurrentExecutionPathsPerInstance` and exact-type `StepThrottles`. Ephemeral hosting alone
adds named `TransientPools`; durable named transient-pool authoring is not a first-release
application capability. Remaining work retargets the delivered mechanics, implements durable
restart re-admission, and removes every provisional option or policy not present in the selected-
mode matrix.

## Goals / Non-Goals

**Goals**

- Specify configurable limits for: (1) per-instance structured execution paths, (2) exact-step-type execution throttles, and (3) named cross-instance transient pools with independent host-local limits.
- Make `MaxConcurrentExecutionPathsPerInstance` exclusively host-owned; remove the author-level global path cap and define deterministic composition with root fixed-`Parallel` and root node-local `ForEach` admission.
- Define composition rules with the **per-instance mutation turn**, countable execution-path tokens, and **durable rehydration** (transient limits apply in-process; distributed durable leasing is a separate capability).
- Specify one saturation behavior: park the exact requesting owner, release the instance turn, and remain cancellable; v1 has no fail-fast or capacity-wait-timeout mode.
- Allow ephemeral authors to associate a step with at most one `TransientPoolName` without embedding provider-specific details in business state; operators may configure multiple named pools, while durable authoring remains compile-absent.
- Keep governance options inside the exact role-specific hosting wrappers: both engine roles carry `StructuredExecutionHostOptions`, only `EphemeralEngineHostOptions` carries `TransientPools`, and only `DurableEngineHostOptions` carries durable resource-pool configuration. Registration includes its hosted loops; no catch-all or second toggle exists.

**Non-Goals (initial phase)**

- Distributed enforcement and persisted durable resource leases across multiple processes or nodes.
- Replacing per-instance serialization with pool-based locking.
- Changing parallel join correctness; governance may restrict **when** work runs, not **what** outcome is computed.

## Decisions

### 1. Separation of concerns: instance serialization vs resource governance

- **Per-instance execution lock** (existing): ensures at most one logical mutator advances a given instance’s durable/interpreter state at a time.
- **Resource gates** (new): optional acquisition **before** a step body runs (or before a bounded region), possibly shared across instances.

Composition is mandatory rather than implementation-selectable. A step transition still obeys per-instance serialized commit authority. If a selected local path cannot acquire host-local path, step, or transient-pool capacity immediately, it SHALL record the exact owned blocked obligation in mode-appropriate state, end its quantum, and release the instance mutation turn before awaiting a grant. Ephemeral mode records that obligation in memory; durable mode commits it before the host waits. No implementation may retain the instance turn while waiting for capacity.

For a limit such as “max N DB operations,” the granted physical step-throttle or transient-pool slot brackets only the actual guarded body. A timed-out or otherwise fenced ordinary logical attempt releases its logical execution-path token, and its detached state copy can no longer commit, but the physical slot remains counted until the body returns. This prevents transient capacity reuse while token-ignoring work still executes. The separate durable-lease contract is stricter: it does not begin an overlapping in-process leased retry while the previous body still runs and it retains the same persistent lease obligation across recovery.

### 2. Per-step throttles, named transient pools, and durable leases

- **Per-step execution throttle**: one host-local limit keyed solely by an exact configured named step type authored through `Then<TStep>()` and held only around that business-step body. V1 has no inferred lambda-step type, untyped global, per-definition, assignable/base-type, or step-category throttle scope.
- **Named cross-instance transient pools**: multiple independent ephemeral host-local limits may be configured, each identified by `TransientPoolName`; one step may select at most one through its single `WithTransientPool(...)` decorator. V1 does not stack several named pools on one step. Durable v1 uses host-owned path/step admission or `ResourcePoolName` leasing instead of authored transient-pool metadata.
- **Durable resource leases**: excluded from this change; they use `ResourcePoolName`, persisted cross-host capacity, exact lexical occurrence ownership, `AmbiguousHeld` retry retention, quarantine transfer, deterministic release, and review-mark/owner-state reconciliation rather than these transient gates. They are authorable at the durable root, root-nested `If`/`While` bodies, and independent durable root-`Parallel` branch/root-`ForEach` item bodies only when no live ancestor lease exists. Dedicated leased builders expose no fan-out, nested acquisition, or `ContinueAsNew`. They have no baseline holder renewal or force-release API.

Transient pools are identified by the validated immutable **`TransientPoolName`** in ephemeral definition metadata, with capacities configured by the same type at host startup. It uses exact ordinal scalar equality and is not interchangeable with `ResourcePoolName`. Public binding and mode discoverability follow `reshape-developer-facing-interfaces`: v1 exposes `WithTransientPool(TransientPoolName)` only on ephemeral builders. Later DI composition cannot add methods to a builder type; durable support requires a future explicit matrix amendment.

The common definition registry validates compatibility before mutation. Engine-mode mismatch wins first. On an ephemeral host, every authored transient-pool name is compared with the host's copied catalog; missing names are returned together as copied, distinct, ordinal-sorted `HostIncompatible.MissingTransientPools` before fingerprint-conflict evaluation. A durable host never silently accepts transient-pool metadata. This is startup/registration compatibility, not saturation: a configured but busy pool still parks the exact owner at runtime.

### 3. Park the exact owner when saturated

- Default cross-instance admission policy: **async wait** for a slot with **cancellation** tied to host shutdown and step cancellation tokens.
- Local fiber policy: represent unavailable capacity as an owned blocked resource obligation, end the fiber quantum, and release the instance mutation turn. Ephemeral mode keeps the obligation in memory; durable mode commits it. Grant or cancellation transitions make the exact owner runnable or terminal. A local step never awaits a pool while retaining the instance turn. Pending admission cancels with its owner, but a granted host-local slot remains counted until the actual guarded body returns/stops—even after forced logical workflow termination—so capacity cannot be reused while work still executes.
- Saturation itself never fails the workflow. V1 exposes no fail-fast switch, capacity-wait deadline, or provider-defined admission policy; workflow, step, and host-shutdown cancellation remain the only exits from a pending admission. Driver segment budgets such as `MaxSegmentDuration` are fairness mechanics and do not end a capacity wait.

The requirements scenario will assert **deterministic cancellation** when the host stops.

### 4. Parallel branches and future scheduling

- Root `Parallel` and root `ForEach` paths are deterministic cooperative fibers over isolated branch/item state. At most one unfenced attempt owns commit authority for an instance, but a token-ignoring timed-out body may continue physically against its discarded copy while a retry or sibling progresses. Pool saturation blocks only the selected fiber and never retains the instance turn.
- The host owns `MaxConcurrentExecutionPathsPerInstance`. A runnable root, branch, or item owns one countable path token. It releases that token when it parks on a wait, delay, resource request, or join and reacquires one before progressing. A parent releases its token before child scheduling and reacquires one only for merge/continuation, so path-token capacity alone cannot create a parent-held-token deadlock at a ceiling of one. A workflow definition has no author-global equivalent and cannot loosen or replace the host ceiling. Every fixed root-`Parallel` branch fiber exists at scope start, and runnable branches queue for tokens in authored order; no separate live-fiber admission resource exists. A root `ForEach` node may declare a positive node-local `maxConcurrency`; the effective admitted-item bound is `min(host MaxConcurrentExecutionPathsPerInstance, node maxConcurrency)`. Omitting the node-local value uses the host ceiling. `ForEach`'s node-local limit separately counts admitted nonterminal item scopes, including parked items, until terminal completion. Consequently, v1 does not promise global progress when admitted items depend on work assigned only to pending items.
- Bounded durable root `ForEach` uses the same rule after its finite item input has been committed. Restart re-admits unfinished item paths under the current host ceiling and the committed node-local ceiling; it does not persist a host slot as durable ownership.
- True concurrent work remains possible across workflow instances, DAG child instances, and external systems. `DagHostOptions.MaxConcurrentNodes` counts every started nonterminal child, including one parked in a wait, delay, or lease queue, until terminal; releasing a child workflow path token does not free DAG admission. Only the exact approved path, step-throttle, transient-pool, DAG-node, and durable-lease limits apply; there is no additional host-wide workflow-advancement slot. Parent-instance mutation remains serialized.

Within one structured root fan-out scope, path tokens and admitted `ForEach` item slots are the only
two quantities owned by the structured-fiber scheduler. That scope statement does not collapse exact-step throttles,
transient pools, durable leases, or DAG-node admission, which retain their independent lifetimes.

### 5. Durable and outbox pumps

- Outbox dispatch and replay workers may need **separate** pools or exclusions so governance does not starve internal bookkeeping; the spec treats “workflow step execution” as distinct from **infrastructure dispatch** unless explicitly configured.

### 6. Observability

- Implementations SHOULD expose metrics or hooks for configured limits, active slots, wait depth, and cancellation. Saturation has no rejection metric because it parks rather than rejects; invalid host configuration fails startup separately.

## Risks / Trade-offs

- **Blocked-obligation correctness risk** if ownership is not committed/recorded before the turn is released. Mitigate with an exact owner occurrence, one release-turn-before-wait rule, and deterministic grant/cancel races; lock-ordering advice is not a substitute for this protocol.
- **Cross-process limits** require a distributed semaphore or partition assignment—not covered in v1.
- **Fairness**: FIFO wait on pools is recommended but not always required; prioritize documented behavior under load tests.

## Greenfield Implementation Plan

1. Land specification and API sketch in workflow/runtime contracts (follow-up implementation change).
2. Treat the delivered ephemeral transient-pool implementation as the v1 named-pool Adapter; implement durable host-owned path/step governance and restart re-admission without amending the durable builder surface.
3. Add acceptance tests: pool saturation, cancellation, cross-instance contention, interaction with instance serialization.

## Resolved Boundaries

- `TransientPoolName` values are stable authored definition metadata only in ephemeral mode; host configuration supplies positive capacities, not an admission-policy plug-in.
- V1 has no independent host-wide workflow-instance or advancement ceiling. `MaxConcurrentExecutionPathsPerInstance` is the sole workflow-path ceiling.
- V1 has no public provider/runtime SPI or custom factory for host-local throttles and transient pools; the engines own their implementation.
