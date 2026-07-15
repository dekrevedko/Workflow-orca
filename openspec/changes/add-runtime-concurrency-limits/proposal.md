Joint taxonomy baseline: [`docs/specs/17-selected-mode-capability-matrix.md`](../../../docs/specs/17-selected-mode-capability-matrix.md).
This change owns per-step execution throttles and named cross-instance transient pools; it
does not implement or imply persisted durable resource leases.

## Why

Hosts need predictable upper bounds on how much work the orchestration runtime drives at once: concurrent workflow instances advancing, concurrent step bodies executing, and shared infrastructure limits such as database connections or pooled HTTP clients. The archived fiber work delivered these controls for the ephemeral engine; the durable engine still lacks equivalent transient governance and restart re-admission. Operators and authors need one explicit cross-mode contract for caps such as “no more than four concurrent database-backed operations across this host” without confusing host-local limits with durable leases.

## What Changes

- Accept the delivered ephemeral **runtime resource governance** baseline for two transient categories: per-step execution throttles held only around one step body, and named host-local cross-instance pools shared by multiple steps or workflows.
- Complete the equivalent durable-host governance, blocked-owner restart re-admission, cross-mode verification, and operator configuration before durable transient-pool authoring is exposed.
- Reserve **durable resource lease** for the separate persisted cross-host/fiber/scope capability with queueing, deterministic release, expiry, and recovery; this change does not make transient pools restart-durable.
- Record how these limits **compose** with existing **one logical mutator per instance** semantics (they address different concerns: correctness ordering versus operational capacity).
- Capture implementation-facing scenarios for configuration, observability, cooperative local-fiber blocking, and true concurrent external or cross-instance work.
- Maintain the normative specifications and focused design for the delivered ephemeral baseline and the remaining durable implementation.

## Capabilities

### Modified Capabilities

- `runtime-resource-governance`: Configurable per-step throttles and named cross-instance transient pools for workflow execution, composable with per-instance serialization and cooperative fiber turns and distinct from durable resource leases.

## Impact

- The archived structured-fiber work already delivered ephemeral advancement/step limits, named transient pools, cooperative blocked obligations, and focused regressions. This change now records that accepted baseline and owns the remaining durable-host and cross-mode work.
- Future durable paths share the same governance contract so limits behave consistently where both modes claim support; durable public authoring remains absent until that evidence exists.
- The archived `adopt-structured-fiber-execution` baseline is authoritative for local composition: local branch bodies execute cooperatively, and a saturated local fiber releases the instance turn through an owned blocked obligation rather than awaiting capacity in-turn.
- `reshape-developer-facing-interfaces` is authoritative for public naming and mode-guaranteed discoverability. Static builders do not gain methods from later host composition: ephemeral transient-pool authoring is delivered, while durable transient-pool authoring remains absent until every supported durable host enforces it. All three artifacts use the same per-step throttle / named transient pool / durable lease taxonomy.
