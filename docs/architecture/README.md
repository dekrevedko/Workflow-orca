# Architecture document routing

The files in this directory preserve the design exploration that produced the current runtime.
Many intentionally show superseded alternatives such as public `RunChild`/`RunChildren`,
`WhenFirst`, `WaitLong`, Saga, or a generic external-job composite. They are useful history and
future-design input, but they are not the first-release approval baseline.

For current work, use these sources in order:

1. [Selected-mode capability and signature matrix](../specs/17-selected-mode-capability-matrix.md)
2. [Exact public authoring declarations](../specs/17-public-authoring-contract.cs)
3. [Consolidated product requirements](../specs/README.md)
4. [Active developer-facing change](../../openspec/changes/reshape-developer-facing-interfaces/)
5. [Active concurrency change](../../openspec/changes/add-runtime-concurrency-limits/)
6. [Current Review-E remediation and Phase 0 status](../review/developer-facing-interface-v1-simplification-review-e-remediation-and-phase-00-status-2026-07-19.md)
7. [Active implementation plan](../implementation/developer-facing-interface-refactor-phased-plan-2026-07-14.md)

The dated simplification and strong-value amendments, 2026-07-18 prompt/status, reviews A-E,
their earlier consolidated review, and the
[root-only owner decision](../review/developer-facing-interface-v1-root-only-fan-out-decision-and-revalidation-2026-07-19.md)
are immutable provenance. Root-only `Parallel`, `ForEach`, and `While` remain incorporated in live
authority, but the older readiness verdicts do not advance the current gate; none of those
historical files is a current-work slot in this ordering.

An older proposal becomes implementation input again only after an explicit matrix/spec
amendment. Do not copy its provisional signatures into product source or compile guards.
