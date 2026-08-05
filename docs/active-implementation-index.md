# OrcaCore Active Implementation Documentation

This folder contains GitHub-readable documentation for the active implementation
at the repository root.

> **Routing:** current guides below describe the selected first-release surface. The exact
> authority is
> [spec 17](specs/17-selected-mode-capability-matrix.md), with exact authoring declarations in
> [`17-public-authoring-contract.cs`](specs/17-public-authoring-contract.cs). Superseded guides and
> status records are preserved under [`archive/`](archive/README.md), not mixed into this index.

## Developer Guides

- [Ephemeral Engine Developer Guide](ephemeral-engine-developer-guide.md) -
  selected-v1 host, authoring, state, event-delivery, and management contract.
- [Ephemeral Engine Runtime Diagrams](ephemeral-engine-diagrams.md) -
  selected-v1 application-contract flows without internal compiler or scheduler types.
- [Durable Driver Lane Host](durable-driver-lane-host.md) - how registered durable
  definitions advance (segments, restart-safe continuation signal, poison parking) and
  the honest multi-host contention model (DR-030..037).
- [Durable Development Store Reset](durable-development-store-reset.md) - required reset
  procedure for stale cursor checkpoints and plan-binding changes during active development.

## Operations And Handoffs

- [Samples](../samples/README.md) - runnable examples from simple engine usage
  through durable host APIs, plus the advanced dashboard sample.
- [End-to-End Plan](end-to-end-plan.md) - current integration baseline,
  missing host/runtime surfaces, staged e2e workstreams, and verification gates.
- [Developer-Facing Interface Refactor Plan](implementation/developer-facing-interface-refactor-phased-plan-2026-07-14.md) -
  the 2026-07-18 simplified first-release phase order, review gates, and removal/defer policy.
- [Historical Review-E Remediation and Phase 0 Status](review/developer-facing-interface-v1-simplification-review-e-remediation-and-phase-00-status-2026-07-19.md) -
  immutable evidence for the completed Phase 0 gate. Sections 4 through 7 were subsequently
  implemented, independently approved, and checkpointed through commit `50254d08175431896d580ecfcc93d8e49e1c2ec7`.
  The post-checkpoint Section 7A public-surface/test-evidence closure is active, and the pending
  Section 7B amendment proposes durable messaging and application catalogs. Section 8 remains
  blocked until the combined Section 7A/7B target is approved and checkpointed and the revised,
  non-conflicting downstream-capability harmonization change is approved, synchronized, reviewed,
  and checkpointed.
- [Historical Root-Only Fan-Out Decision and Revalidation](review/developer-facing-interface-v1-root-only-fan-out-decision-and-revalidation-2026-07-19.md) -
  immutable owner decision selecting root-only `Parallel`, `ForEach`, and `While`. Its placement
  rule remains incorporated in current authority; its earlier guard-readiness verdict is a
  superseded snapshot.
- [Historical Consolidated V1 Planning Review](review/developer-facing-interface-v1-simplification-consolidated-review-2026-07-19.md) -
  immutable reconciliation of reviews A-D for the preceding nested-`Parallel` snapshot; its
  placement decision is superseded by the root-only owner decision above.
- [Historical V1 Simplification Amendment](review/developer-facing-interface-v1-simplification-amendment-2026-07-18.md) -
  immutable revision input; current authority is spec 17, its companion, and the live OpenSpec changes.
- [Historical Strong-Value Construction Amendment](review/developer-facing-interface-v1-strong-value-construction-amendment-2026-07-19.md) -
  immutable decision input now incorporated into the live normative contract.
- [Historical Phase 0 Status](review/developer-facing-interface-phase-00-public-consumer-guards-implementation-status-2026-07-18.md) -
  immutable pre-remediation snapshot; do not use its blockers or counts as current status.
- Parallel V1 planning reviews
  [A](review/developer-facing-interface-v1-simplification-review-2026-07-19.md),
  [B](review/developer-facing-interface-v1-simplification-review-2026-07-19-b.md),
  [C](review/developer-facing-interface-v1-simplification-review-2026-07-19-c.md),
  [D](review/developer-facing-interface-v1-simplification-review-2026-07-19-d.md), and
  [E](review/developer-facing-interface-v1-simplification-review-2026-07-19-e.md) - immutable
  independent evidence retained for the contract snapshots each review inspected.
- [Historical V1 Planning Review Prompt](review/developer-facing-interface-v1-simplification-reviewer-prompt-2026-07-18.md) -
  completed copy-ready instructions retained as review history.
- [Production Readiness Notes](production-readiness.md) - current guarantees,
  local package-certification versus deferred external-publishing work, security checklist,
  benchmark commands, and sample
  host notes.
- [Kubernetes Scheduler Companion Handoff](eks-scheduler-handoff.md) - outward-only boundary
  for typed create-or-observe steps, watcher events, scoped lease protection, stop
  reconciliation, and optional EKS/AWS composition outside OrcaCore packages.

## Build And Test

Run commands from the repository root so the local SDK pin is used:

```powershell
dotnet build OrcaCore.slnx
dotnet test tests/OrcaCore.Engine.Ephemeral.Tests/OrcaCore.Engine.Ephemeral.Tests.csproj
```
