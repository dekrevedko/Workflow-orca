# Phase 4b — DAG Front-End, External Jobs, Durable Pools (spec Slice 4b)

> **2026-07-16 contract amendment:** this historical slice delivered the initial provider
> baseline. Ongoing developer-surface work supersedes its decorator/expiry assumptions with
> structural no-author-TTL `AcquireResources`, exact fiber-occurrence ownership, and
> review-mark/owner-state reconciliation (MG-062…065, DR-038, reshape task 5.2).

> **2026-07-18 first-release simplification:** this entire page is historical implementation
> context, not the current public API plan. `OrcaCore.Dag` now ships as a separate typed
> planning project; `OrcaCore.Dag.Hosting` is the only bridge to the versioned internal
> child-instance protocol in `OrcaCore.Durable.Hosting`. Public
> `RunExternalJob`, `RunChildren`, and Saga are deferred; `WaitLong` and author `Yield` are
> removed. Durable resource acquisition is scoped-only `AcquireResources(request, body)`.
> The current contract is document 17 and the active developer-surface phased plan.

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
| T4B-01 | Durable pool store port + InMemory + Postgres | Sonnet | Historical provider baseline; reshape adds exact obligation/ticket/provider generation and strong `ResourcePoolName` certification |
| T4B-02 | Acquisition-as-wait + symmetric release | Sonnet | Historical wait baseline; reshape replaces decorator acquisition with structural `AcquireResources` and pending-to-held owned state |
| T4B-03 | Ticket expiry + pool operations | Haiku | Historical title only: current review marks retain capacity; exact-owner reconciliation, `LeaseLost`, resize debt, quarantine, and trusted stop/fence confirmation replace expiry reclaim or force release (MG-064/065) |
| T4B-04 | `RunExternalJob` composite | Sonnet | Historical runtime evidence only; public `RunExternalJob` is deferred, while v1 uses a named durable create-or-observe step plus ordinary `Wait` and companion-owned reconciliation. |
| T4B-05 | DAG builder + validation | Sonnet | Historical builder evidence only; v1 uses typed `OrcaCore.Dag` and the sole internal `OrcaCore.Dag.Hosting` bridge, with no public child node. |
| T4B-06 | Run cancellation propagation + DAG observability | Haiku | Historical cancellation/projection evidence only; typed DAG management stays in `OrcaCore.Dag`, while Kubernetes/job stop behavior stays in the outward companion. |

## Phase-wide guardrails

Current first-release disposition:

- `OrcaCore.Dag` owns immutable typed execution-plan authoring and operation contracts, including
  typed run input, typed node workflow references, direct-dependency output mapping, and
  validation. V1 exposes no DAG visualization surface. `OrcaCore.Dag.Hosting` alone maps each node
  to one durable child workflow instance through the named/versioned internal protocol; no public
  `RunChildren` member ships.
- The historical `RunExternalJob` work is runtime/protocol evidence only, not approval of a
  public composite. Kubernetes create-or-observe uses an ordinary typed durable step plus
  `StepOperationId`, normal `Wait`, and a companion watcher/reconciler.
- Durable resource authoring is lexical `AcquireResources(request, body)` only. No
  point/fiber-lifetime overload, author TTL, renewal, or resource-bracket placeholder remains.
- Kubernetes Job API clients, EKS/AWS composition, manifests, watchers, and reconcilers live
  in a separate outward-dependent companion project that may share `OrcaCore.slnx`.

- No Kubernetes or AWS dependency enters OrcaCore. Retained historical fake-dispatcher tests
  are protocol evidence only; the companion scheduler owns concrete Kubernetes/AWS choices.
- Historical decorator-only authoring is superseded. The current lexical scope and deferred
  registry above supersede the old external-job bracket assumption.
