# Phase 4b — DAG Front-End, External Jobs, Durable Pools (spec Slice 4b)

**Goal**: the scheduler-scenario enablers: DAG definition input, `RunExternalJob` composite,
durable resource pools with tickets, run-cancellation propagation, DAG observability.

**Entry criteria**: Phase 4 exit green. Spec open question 15 (DAG compile target) MUST be
resolved before T4B-02 (recommended default: node-per-child-instance via `RunChildren`).
**Exit criteria**: JS-AC-001…007, JS-AC-009…013, AC-518…522 green. JS-AC-007 (durable pool
quota across DAG runs and restarts) lands here with durable pools — verified against
InMemory/Postgres pool stores and the fake dispatcher; only JS-AC-008 and the real EKS
adapters remain for Phase 6.

## Task index (expanded by T4B-00)

| Task | Title | Difficulty | Summary |
|------|-------|-----------|---------|
| T4B-00 | Expand index; resolve spec open question 15 with reviewer | Sonnet | Resolution logged in the 00-stack-decisions decision log (§5) AND marked resolved in spec §13.2 |
| T4B-01 | Durable pool store port + InMemory + Postgres | Sonnet | `IResourcePoolStore`: pools, tickets, FIFO queue, all-or-nothing grant, expiry (MG-062…064); certification additions; AC-518 [provider], JS-AC-007 |
| T4B-02 | Acquisition-as-wait + symmetric release | Sonnet | Requirement decorator on steps/scopes; suspend-cold on exhausted pool; release on all five terminal paths (MG-062); AC-519, AC-520, AC-522 |
| T4B-03 | Ticket expiry + pool operations | Haiku | Expiry policy, audible reclaim, resize/force-release via management surface (MG-064); AC-521 |
| T4B-04 | `RunExternalJob` composite | Sonnet | Outbox start command → cold wait on correlated completion/failure → timeout/retry/cancel; ticket acquisition before dispatch (JS-002, JS-007); JS-AC-004…006, JS-AC-010…013 |
| T4B-05 | DAG builder + validation | Sonnet | Nodes + edges input; cycle rejection with accumulated diagnostics; compile to per-node dependency joins per spec open question 15 resolution (JS-001); JS-AC-001…003 |
| T4B-06 | Run cancellation propagation + DAG observability | Haiku | Stop commands for in-flight jobs before `Cancelled`; run reconstruction from lineage/projections (JS-005/006); JS-AC-009, part of JS-AC-005 |

## Phase-wide guardrails

- No Kubernetes anything in this phase: `RunExternalJob` is tested against
  `FakeDispatcher`/InMemory dispatcher; the EKS adapter is Phase 6 / scheduler-app work
  (JS-003, IOQ-12).
- Pools are a decorator-declared requirement — an explicit Acquire node is a rejected
  design (spec §13.3 rationale).
