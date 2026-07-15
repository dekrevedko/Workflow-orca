Joint taxonomy baseline: [`docs/specs/17-selected-mode-capability-matrix.md`](../../../docs/specs/17-selected-mode-capability-matrix.md).
This change owns per-step execution throttles and named cross-instance transient pools; it
does not implement or imply persisted durable resource leases.

## Why

Hosts need predictable upper bounds on how much work the orchestration runtime drives at once: concurrent workflow instances advancing, concurrent step bodies executing, and shared infrastructure limits such as database connections or pooled HTTP clients. Today, OrcaCore emphasizes **per-instance serialized mutation** and sequential interpretation of parallel branches; it does not expose first-class, configurable **cross-instance** or **named resource** limits. Operators and authors need explicit requirements so implementations can enforce caps like “no more than four concurrent database-backed operations across the whole process.”

## What Changes

- Introduce a **runtime resource governance** capability for two transient categories: per-step execution throttles held only around one step body, and named host-local cross-instance pools shared by multiple steps or workflows.
- Reserve **durable resource lease** for the separate persisted cross-host/fiber/scope capability with queueing, deterministic release, expiry, and recovery; this change does not make transient pools restart-durable.
- Record how these limits **compose** with existing **one logical mutator per instance** semantics (they address different concerns: correctness ordering versus operational capacity).
- Capture implementation-facing scenarios for configuration, observability, cooperative local-fiber blocking, and true concurrent external or cross-instance work.
- Add baseline specifications and a focused design; implementation remains a follow-up change after review.

## Capabilities

### Modified Capabilities

- `runtime-resource-governance`: Configurable per-step throttles and named cross-instance transient pools for workflow execution, composable with per-instance serialization and cooperative fiber turns and distinct from durable resource leases.
- `state-driven-runtime`: Clarify that optional cross-instance or pooled limits may gate step execution without replacing per-instance mutation ordering.

## Impact

- Specification and design only in this change; no source code changes until implementation is approved.
- Future runtime and durable paths should share the same governance contract where possible so limits behave consistently in ephemeral and durable hosts.
- `adopt-structured-fiber-execution` is authoritative for local composition: local branch bodies execute cooperatively, and a saturated local fiber releases the instance turn through an owned blocked obligation rather than awaiting capacity in-turn.
- `reshape-developer-facing-interfaces` is authoritative for public naming and selected-mode discoverability. All three changes SHALL use the same per-step throttle / named transient pool / durable lease taxonomy before any pool-shaped source Interface is added.
