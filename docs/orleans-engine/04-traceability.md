# 04. Orleans Engine Traceability Matrix

Last audited: 2026-07-06.

This file is the self-audit surface for the Orleans engine docs. It maps every normative
`OE-` requirement to the task ids that implement it and the `OE-AC-` criteria that verify
it. External ids such as `DU-`, `PR-`, `EV-`, `OR-`, `OOQ-`, and `IOQ-` are intentionally
defined outside this folder; this matrix checks the local `OE-`, `OE-AC-`, and `OT` graph.

## Audit Checks

- Local id graph: every `OE-`, `OE-AC-`, and `OT` reference in this folder must either be
  defined here or in the task index. Literal example ids are not allowed.
- Requirement coverage: every `OE-` requirement must have at least one implementing task
  and at least one verifying `OE-AC`.
- Testability: every `OE-AC` must describe observable behavior suitable for an automated
  test, guard, or review-gated certification test.
- Semantic locality: Orleans remains an opt-in adapter at the engine seam. Durable
  semantics and definition interpretation stay behind the durable driver/interpreter
  contract; Orleans owns concurrency, lifecycle, distribution, and transport only.

## Requirement To Task To AC

| Requirement | Implementing task(s) | Verifying AC(s) | Audit note |
|-------------|----------------------|-----------------|------------|
| OE-001 | OT0-01, OT0-02 | OE-AC-040 | Dependency isolation is a repository guard, not a runtime behavior test. |
| OE-002 | OT0-03, OT1-01, OT1-01a, OT1-03, OT1-04, OT5-01 | OE-AC-041, OE-AC-043, OE-AC-052 | Parity is verified at transport, facade, and certification levels. |
| OE-003 | OT0-01, OT0-02 | OE-AC-040 | Same guard as OE-001; keeps Orleans packages and attributes confined. |
| OE-010 | OT1-01 | OE-AC-001, OE-AC-030 | Cross-silo routing also proves the one-grain-per-instance address model. |
| OE-011 | OT1-01, OT1-02 | OE-AC-001, OE-AC-002 | Advancement-segment semantics are observable through start commit, wait return, and later DR-host rehosting. |
| OE-012 | OT2-04, OT4-02 | OE-AC-046 | Budget is measured through diagnostics and hard segment-budget continuation behavior; slow steps are not hidden by timeout changes. |
| OE-013 | OT1-01, OT1-03, OT1-05 | OE-AC-011 | Non-reentrant mutation path is verified by racing delivery behavior. |
| OE-014 | OT1-01, OT1-04 | OE-AC-044 | Cancellation coverage includes pre-cancel and mid-delivery paths. |
| OE-015 | OT1-03, OT5-05 | OE-AC-013, OE-AC-023 | Retry safety is split across start and timer delivery semantics. |
| OE-020 | OT0-04, OT1-01 | OE-AC-040 | Guard forbids Orleans grain persistence for workflow truth. |
| OE-021 | OT1-05, OT5-05 | OE-AC-012 | Expected-version append remains the correctness backstop. |
| OE-022 | OT1-01, OT1-06 | OE-AC-020 | Activation is cheap because rehydration happens per command. |
| OE-023 | OT5-06 | OE-AC-060 | Production sign-off depends on sustained-load evidence. |
| OE-030 | OT1-02 | OE-AC-002 | Waits are facts, not held grain calls. |
| OE-031 | OT2-01 | OE-AC-022, OE-AC-023 | Timer truth is the durable scheduler, not Orleans timers/reminders. |
| OE-032 | OT2-02, OT2-03 | OE-AC-021, OE-AC-022 | Restart survival is tested with event and timer wake-up paths. |
| OE-033 | OT1-00, OT2-01 | OE-AC-023 | Claimed-but-undelivered timer recovery is seam-gated before implementation. |
| OE-040 | OT1-00, OT1-01a, OT1-04, OT3-02 | OE-AC-030, OE-AC-043 | Split delivery surfaces prevent correlation and fanout semantics from merging. |
| OE-041 | OT1-04, OT5-05 | OE-AC-010 | Inbox dedup remains in the durable commit path. |
| OE-042 | OT1-00, OT1-03a, OT1-03b, OT1-03, OT5-05 | OE-AC-013 | Start idempotency requires both serialization and atomic reservation durability. |
| OE-050 | OT1-06 | OE-AC-020 | Deactivation may be skipped without losing correctness. |
| OE-051 | OT1-06, OT4-01 | OE-AC-020 | Idle collection remains normal operation. |
| OE-052 | OT1-06, OT3-04 | OE-AC-003 | Projection queries must not activate grains. |
| OE-053 | OT2-04, OT4-02 | OE-AC-046 | Diagnostics are observable through listener-based tests. |
| OE-060 | OT0-03 | OE-AC-041 | Serializer shape is add-only and Orleans-specific only at the transport Seam. |
| OE-070 | OT0-04, OT4-01 | OE-AC-042 | The composition Interface deepens over phases, but stays one Seam. |
| OE-071 | OT1-03, OT3-03 | OE-AC-043 | Facade parity is verified first for start, then management operations. |
| OE-072 | OT2-01, OT3-01 | OE-AC-045 | Pumps are silo lifecycle participants, not plain hosted services. |
| OE-080 | OT0-02, OT5-00 | OE-AC-042, OE-AC-052 | TestingHost-first applies to behavior tests; PostgreSQL e2e is explicitly scoped. |
| OE-081 | OT5-01 | OE-AC-052 | The parity subset must be explicit and reviewed before execution. |
| OE-082 | OT5-03, OT5-04 | OE-AC-050, OE-AC-051 | Package completion is the driving scenario on in-memory and PostgreSQL clusters. |

## Acceptance Criterion Ownership

| AC | Primary task(s) |
|----|-----------------|
| OE-AC-001 | OT1-01 |
| OE-AC-002 | OT1-02, OT1-04 |
| OE-AC-003 | OT1-06, OT3-04 |
| OE-AC-010 | OT1-04 |
| OE-AC-011 | OT1-05 |
| OE-AC-012 | OT1-05 |
| OE-AC-013 | OT1-03a, OT1-03 |
| OE-AC-020 | OT1-06 |
| OE-AC-021 | OT2-02 |
| OE-AC-022 | OT2-03 |
| OE-AC-023 | OT2-01 |
| OE-AC-030 | OT3-02 |
| OE-AC-031 | OT3-01 |
| OE-AC-040 | OT0-02 |
| OE-AC-041 | OT0-03 |
| OE-AC-042 | OT0-04, extended by OT1-03/OT2-01/OT3-01 as registrations land |
| OE-AC-043 | OT1-03, OT1-04, OT3-03 |
| OE-AC-044 | OT1-01, OT1-04 |
| OE-AC-045 | OT2-01, OT3-01 |
| OE-AC-046 | OT2-04, OT4-02 |
| OE-AC-050 | OT5-03 |
| OE-AC-051 | OT5-04 |
| OE-AC-052 | OT5-01 |
| OE-AC-060 | OT5-06 |

## Current Semantic Gaps Closed By This Pass

- Added explicit AC coverage for structural requirements that were previously only in task
  prose: dependency isolation, transport versioning, composition, facade parity,
  cancellation, lifecycle-pump hosting, turn-budget diagnostics, and certification parity.
- Replaced the dangling task-shaped example from the O2 expansion task with non-id prose so
  cross-reference checks do not report a false task reference.
- Made the README's self-contained claim precise: agents avoid unbounded exploration, but
  tasks may read the exact source files listed in "Read first".
- Reconciled OE-011 with document 16: a grain turn is one durable advancement segment;
  individual kernel commands remain the atomic commit unit inside that segment.
