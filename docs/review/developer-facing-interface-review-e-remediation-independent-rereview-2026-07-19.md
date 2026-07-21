# Review-E remediation independent planning re-review

**Date:** 2026-07-19

**Verdict:** **APPROVE**

**Scope:** planning readiness for retargeting the Phase 0 guard packet only. This verdict does
not approve Phase 0 exit or authorize task 4.0 product source work.

**Reviewed baseline:** commit `8c2dd712284f3b638f9bf812ad2172f24d0a8863` plus the uncommitted
planning-gate routing edits present at review time.

**Dirty-file manifest reviewed:**

- `docs/active-implementation-index.md`
- `docs/implementation/developer-facing-interface-refactor-phased-plan-2026-07-14.md`
- `docs/review/developer-facing-interface-v1-simplification-review-e-remediation-and-phase-00-status-2026-07-19.md`
- `docs/review/developer-facing-interface-review-e-remediation-independent-rereview-2026-07-19.md`
- `tests/OrcaCore.DeveloperSurface.Guards/Fixtures/v1-public-contract.json`
- `tests/OrcaCore.DeveloperSurface.Guards/NormativeContractGuards.cs`

**Validation commands:**

- `openspec.cmd validate reshape-developer-facing-interfaces --strict`
- `openspec.cmd validate add-runtime-concurrency-limits --strict`

## Findings

- P0: none.
- P1: none.
- P2: none.
- P3: update current routing text from pending/NOT READY to approved/READY for guard retargeting.

## Basis

The independent review checked the live authority rather than the immutable dated review
snapshots. It found document 17, the exact authoring companion, affected canonical requirements,
both coordinated OpenSpec changes, and the phased plan coherent on:

- root-only `Parallel`, `ForEach`, and `While`, with nested `If` as the only nestable structural
  control-flow member;
- exact staged authoring, completion, definition/reference, strong-value, diagnostic, package,
  hosting, DAG, deadline, retry, and scoped-leasing contracts;
- the Phase 0 boundary for DAG unified-outbox and lineage internals, without an invented public
  or friend-only query seam;
- the four closed lease slices in tasks 3.11a through 3.11d;
- all 15 section-3 tasks being authorable without a new public signature, protocol, lifecycle,
  package edge, or timing decision.

Task 3.12 still requires actual green and expected-red execution evidence plus an independent
review of the completed guard packet. Task 4.0 remains blocked until that later verdict.

## Routing disposition

Guard-retarget readiness moves from **NOT READY** to **READY**. Phase 0 exit remains
**NOT APPROVED**.

## Task 3.1 completion checklist

The current partial slice receives no task-completion credit. Task 3.1 remains open until guards
cover all of the following against frozen, reviewable expectations:

- project to `PackageId` to `AssemblyName` identity;
- row-scoped direct package dependencies;
- diagnostic code, stable name, meaning, and `Error` severity;
- failure-code ownership;
- complete application, DAG, DAG-hosting, provider, protocol, companion, and internal tier
  classification;
- compiled `InternalsVisibleTo` metadata, including the exact friend and exposed-type barriers;
- recursive public-signature cross-tier leak checks;
- exact namespace, generic arity, member signature, overload, construction-family, alias,
  placeholder, and root-only placement checks from the companion and document 17.
