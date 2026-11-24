## Why

OrcaCore's ephemeral wait contract depends on interactions between correlation reservation, event matching, continuation completion or rollback, timer competition, and cleanup. Existing scenarios exercise selected schedules; a small executable TLA+ model can explore the bounded combinations and expose either contradictory requirements or implementation gaps before expanding formal verification to durable recovery.

## What Changes

- Introduce a development-only TLA+/TLC pilot for ephemeral wait ownership, with two competing instances, repeated wait occurrences, duplicate events, timeout, and cancellation.
- Check ownership uniqueness, reservation lifetime and accounting, single successful consumption, exact-owner cleanup, rollback consistency, and conditional progress.
- Maintain a requirement/property/action/source/test crosswalk, including explicit abstraction and atomicity assumptions; distinguish an abstract contract model from an implementation-aligned model.
- Qualify the model with reachable scenario witnesses and intentional faulty variants, then reproduce actionable counterexamples using deterministic C# tests.
- Add a version-pinned, checksum-verified TLC runner and bounded CI job during implementation, retaining model configurations, traces, and evidence tied to the source revision.
- End the pilot with an explicit scope-limited verdict and a decision on whether durable handoff merits a separate proposal. This proposal creates planning artifacts only; all implementation and verification tasks remain pending.

## Capabilities

### New Capabilities

- `wait-ownership-model-verification`: Executable, bounded verification and code/test traceability for the ephemeral wait-ownership protocol.

### Modified Capabilities

None. Existing event-routing, shutdown, durable execution, and public API requirements remain authoritative. Any discovered conflict requiring a product-contract change must be recorded and resolved through an explicit amendment rather than silently incorporated into the model.

## Impact

- Proposed implementation locations: `verification/tla/wait-ownership/`, `scripts/verification/`, a dedicated workflow under `.github/workflows/`, and focused tests in `tests/OrcaCore.Engine.Ephemeral.Tests/`.
- Owning source: `EphemeralRoutingIndex`, `WorkflowInstance`, `RuntimeWaitRecord`, `SuspensionScheduler`, the instance execution lane, and the ephemeral routing facade. This proposal does not modify these implementations.
- Requirements: `openspec/specs/event-routing-and-waits/spec.md` and the EV-011/020/023/043/044 contract in `docs/specs/05-requirements-events-waits-timers.md`, interpreted with the current descriptor-based public contract.
- Development/CI dependencies only: a pinned official `tla2tools.jar` release and a compatible pinned Java runtime; no NuGet, production-runtime, package, or public-surface dependency.
- Excludes durable inbox/outbox and SQL providers, restart/crash guarantees, host shutdown, start-idempotency, full fiber scheduling, resource leases, payload serialization, and whole-program correctness. Process loss discards ephemeral state and carries no survival guarantee.
