# 14. Driving Scenario — EKS Job Scheduler with DAG Support (JS)

A concrete target application built **on top of** the OrcaCore library: a job scheduler
where each job runs as a Kubernetes Job (EKS), and job runs are organized as **DAGs** of
dependent jobs (Airflow/Argo-style dependencies). This document maps the scenario onto the
existing spec, derives the requirements it adds, and fixes the boundary between the library
and the scheduler application.

Requirement prefix: `JS-`. Acceptance prefix: `JS-AC-`.

## 14.1 Scenario summary

- A **run** is one execution of a DAG definition: nodes are jobs, edges are dependencies
  (including diamond fan-in: `A → {B, C} → D`).
- A **job** executes outside the scheduler process as an EKS Job: the scheduler submits it,
  then waits — minutes to hours — for completion/failure reported back asynchronously.
- Runs must survive scheduler restarts, support timeouts, retries, quotas (max N concurrent
  jobs per cluster/queue), operator pause/cancel, and full run observability.
- Runs may start on a schedule (cron-like recurrence).

## 14.2 Fit assessment — what the spec already covers

The durable engine covers the hard parts of this scenario without change:

| Scheduler need | Covered by |
|---|---|
| Submit job then sleep for hours, restart-safe | `WaitLong` cold wait (EV-041), rehydration (DU-013) |
| Job submission consistent with committed state | Outbox, publish-after-commit (DU-031..033) |
| Completion/failure reported asynchronously | Correlated events (EV-001/002), routing (EV-010), dedup (EV-031, DU-030) |
| Job timeout ("kill after 2h") | Timer/event race (EV-051), timeout policies (EV-052) |
| Job retry with backoff | Structured retry decorator (CR-006) |
| Fan-out node (map over N inputs) | `RunChildren` + partitioners (CP-021, CP-030) |
| Max N concurrent jobs per cluster/queue | Durable resource pools with tickets (MG-062…064, bound to jobs by JS-007); `RunChildren` dispatch windows (CP-023) apply only to group-scoped child throttling. Transient in-process pools (MG-061) are unsuitable here — see JS-007 |
| Exactly-once "all dependencies done" continuation | Resume token barrier (CP-024), joins (CP-002) |
| Operator pause / resume / cancel of a run | MG-013, CR-031 (cancel is cooperative → job deletion) |
| Idempotent scheduled starts across scheduler restarts | `StartOrGet` (DU-053) |
| Run inspection, timings, failure reasons | Lineage (CP-020), history (DU-071), statistics (MG-030) |
| No K8s specifics in definitions | Provider boundary (PR-002, PR-015) |

## 14.3 What the scenario adds — requirements

### JS-001 DAG definition input
The library SHALL support DAG-shaped definitions — nodes plus dependency edges, including
diamond fan-in — as an **additional authoring input** that compiles onto the standard
execution semantics: a node becomes runnable when the joins over all its incoming edges are
satisfied (per-node `WhenAll` over dependency completions); node failure behavior maps to the
existing failure-policy axis (CP-021: fail-fast vs continue-independent-branches vs
wait-all-then-fail).

Constraints:
- Cycles SHALL be rejected at build time with accumulated diagnostics (CR-002).
- The DAG builder is a **compile front-end**, not a second runtime: it lowers to the same
  plan/primitives the tree builder produces. The fluent tree builder remains the primary
  authoring path (CR-001); DAG authoring does not change runtime contracts.
- The compile target (each node as a durable child instance vs in-instance join state) is
  open question 15 in document 13; child-instance-per-node is the recommended default for
  isolation, lineage, and per-node retry.

### JS-002 External job composite (`RunExternalJob`-style)
The library SHOULD provide a first-class composite for the submit-and-await-external-work
pattern: emit a start command through the outbox → durably wait (cold) on completion/failure
events correlated by job identity → apply timeout, retry, and cancellation policy. Its
semantics SHALL be fully reducible to existing primitives (`Publish` + `WaitLong` +
timer race) — the composite is convenience plus a documented contract, not new runtime
behavior. Cancellation of the composite SHALL emit a compensating "stop external work"
command (job deletion) through the outbox.

### JS-003 Kubernetes adapter contracts (provider layer)
EKS integration SHALL live entirely in the provider layer:
- a **dispatcher adapter** (PR-015) that turns outbox job-start/job-delete records into
  Kubernetes API calls (create/delete Job), with the standard dispatch outcomes
  (success / retryable / permanent failure);
- a **watcher/ingestor** that observes Job status and raises normalized envelopes
  (EV-001) — `JobSucceeded` / `JobFailed` with the job's `CorrelationId` — subject to
  standard inbox dedup on redelivery.

Kubernetes concepts (namespaces, manifests, backoff limits) MUST NOT leak into workflow
definitions (PR-002); they are adapter configuration.

### JS-004 Scheduled starts are a host concern
Cron/recurrence triggering SHALL be a scheduler-application concern layered on the library:
each occurrence starts a run via `StartOrGet` with a deterministic occurrence key
(definition + schedule + occurrence timestamp), making scheduled starts idempotent across
scheduler restarts and overlapping triggers (DU-053). The core library SHALL NOT embed a
cron engine.

### JS-005 DAG run observability
A DAG run SHALL be reconstructable — nodes, dependency edges, per-node status, timings,
attempts, failure reasons — from lineage metadata, projections, and history alone (CP-020,
DU-070/071, MG-030), without scheduler-private bookkeeping storage. Named End outcomes
(CR-008) SHOULD carry per-node terminal outcomes.

### JS-006 Run cancellation propagates to jobs
Cancelling a run SHALL cooperatively cancel its in-flight nodes (CR-031): each running
external job receives a stop command (via JS-002's cancellation path); the run terminates
`Cancelled` only after cancellation intent for all in-flight jobs is durably recorded.

### JS-007 Global DB-connection budgets across jobs
Jobs that consume database connections SHALL declare their needs as **durable-pool ticket
requirements** (MG-062/063) on the external-job composite — e.g. `requires: db-A×1,
db-B×2`, one pool per database. Semantics:

- the ticket set is acquired (all-or-nothing) **before** the job-start command is
  dispatched through the outbox — a job never starts without its budget;
- with pools exhausted, the node waits cold in the grant queue (FIFO) — a queued job costs
  no memory or cluster resources;
- tickets are held for the **entire job lifetime**, across the cold wait and any scheduler
  restarts (transient in-process pools, MG-061, are unsuitable here by design);
- release happens on the job's terminal outcome — success, failure, timeout-kill (JS-AC-006)
  and run cancellation (JS-006) included;
- ticket expiry SHOULD derive from the job timeout plus a reporting margin, so a job that
  dies without reporting cannot leak connections silently (MG-064).

This is the global "rate limiter with tickets" across all jobs, all DAGs, and all
definitions sharing the scheduler's store. Note it is a **concurrency cap** (N connections),
not a rate (N per second); rate-based token-refill pools are tracked as open question 16.

## 14.4 Acceptance criteria

- **JS-AC-001** *Diamond DAG joins correctly* — Given `A → {B, C} → D`, D starts only after
  both B and C complete, regardless of their completion order. [JS-001, CP-002/003]
- **JS-AC-002** *Cycle rejected at build* — A cyclic DAG fails build with accumulated
  diagnostics naming the cycle. [JS-001, CR-002]
- **JS-AC-003** *Node failure policy enforced* — Given a failing node, dependents do not
  start, and the run outcome follows the configured failure policy; independent branches
  behave per policy. [JS-001, CP-021]
- **JS-AC-004** *External job completion resumes exactly once* — A correlated completion
  event resumes the awaiting node exactly once; watcher redelivery is deduplicated.
  [JS-002, EV-023/031]
- **JS-AC-005** *Run survives scheduler restart mid-job* — **[provider]** With a job running
  in EKS and the scheduler restarted, the cold-waiting instance rehydrates and the
  completion event resumes it. [JS-002, AC-301/304]
- **JS-AC-006** *Job timeout kills the job* — On timeout, the configured policy fires
  deterministically and a job-delete command is dispatched through the outbox. [JS-002,
  EV-051, DU-031]
- **JS-AC-007** *Queue quota honored durably* — At most N jobs holding tickets of a named
  durable pool run concurrently — across definitions, DAG runs, and scheduler restarts.
  Group-scoped child dispatch windows (CP-023) compose with, and never substitute for, the
  pool cap. [JS-007, MG-062, MG-063]
- **JS-AC-008** *Scheduled occurrence idempotent* — Two triggers for the same occurrence key
  yield one run. [JS-004, DU-053]
- **JS-AC-009** *Run cancel propagates* — Cancelling a run dispatches stop commands for all
  in-flight jobs and ends the run `Cancelled`. [JS-006, CR-031]
- **JS-AC-010** *Ticket held across restart* — **[provider]** Given a running job holding a
  db ticket, after scheduler restart the ticket is still held and capacity is not
  double-counted. [JS-007, MG-062]
- **JS-AC-011** *Ticket released on every job outcome* — Job success, job failure,
  timeout-kill, and run cancellation each release the job's tickets exactly once, freeing
  capacity for queued jobs. [JS-007, MG-062]
- **JS-AC-012** *Multi-DB job acquires atomically* — A job requiring db-A and db-B never
  holds one ticket while waiting for the other; it starts only with the full set. [JS-007,
  MG-063]
- **JS-AC-013** *Queued job consumes nothing* — A job waiting for a ticket keeps its
  instance cold-evictable and creates no EKS resources until granted. [JS-007, MG-062,
  EV-041]

## 14.5 Phasing

- JS-001/002/005/006 depend on Slices 2–4 (durable core, timers/policies, `RunChildren` +
  unified outbox). Recommended landing: a **Slice 4b — DAG front-end & external-job
  composite**, immediately after Slice 4.
- JS-003 (EKS adapters) and JS-004 (scheduler host app) are provider/host deliverables in
  the Slice 6 family; the scheduler application itself is a separate codebase consuming the
  library — and serves as the first real-world certification consumer (PR-024).
- Slices 1–3 are unaffected: the scenario validates the existing spec rather than reshaping
  it.

## 14.6 Boundary statement

The **library** owns: DAG compilation, durable orchestration, waits/joins/retries/timeouts,
outbox dispatch contracts, quotas, lineage/observability. The **scheduler application**
owns: cron triggering, the K8s adapters' deployment/configuration, UI/API over the
management surface, and multi-tenant policy. Nothing in this scenario requires weakening
that boundary — which is the strongest signal the current spec is fit for purpose.
