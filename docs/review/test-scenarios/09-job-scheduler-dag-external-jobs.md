# Job Scheduler, DAG & External Jobs — Negative Tests & Edge Cases

Scope: EKS job-scheduler driving scenario (doc 14): DAG compilation, `RunExternalJob` composite,
resource pools bound to jobs, cancellation propagation. Requirements: JS-*, JS-AC-*.

## Existing negative coverage (reference)

| Area | Test(s) | What is proven |
|------|---------|----------------|
| DAG cycle | `DagBuilderTests` (JS-AC-002) | Build rejection |
| Heterogeneous definitions | `DagBuilderTests` | Build rejection |
| DAG failure blocks dependents | `DagAcceptanceTests` (JS-AC-003) | Plan blocks D when B fails |
| External job idempotent completion | `RunExternalJobTests` (JS-AC-004) | Duplicate complete once |
| External job cancel stop commands | `ExternalJobCancellationTests` (JS-AC-009) | stop outbox records |
| Pool held / released on job paths | `RunExternalJobTests`, pool tests | JS-AC-010…013 |
| DAG observability reconstruct | `DagObservabilityAcceptanceTests` (JS-AC-005) | Node status from stream |

---

## Missed negative tests

### NEG-JS-001 — DAG compile: disconnected node
- **Priority:** P1 | **AC:** JS-AC-001 | **Status:** Missing
- **Given** node with no path from root
- **When** build DAG
- **Then** validation warning or unreachable node error

### NEG-JS-002 — DAG compile: multiple roots
- **Priority:** P1 | **AC:** JS-001 | **Status:** Missing
- **When** two nodes with zero in-degree
- **Then** reject or require super-root

### NEG-JS-003 — RunExternalJob without pool when required
- **Priority:** P1 | **AC:** JS-AC-012 | **Status:** Missing
- **Given** job requires `db` pool
- **When** pool exhausted indefinitely
- **Then** job waits; no start outbox until granted

### NEG-JS-004 — RunExternalJob start outbox fails
- **Priority:** P0 | **AC:** JS-AC-004 | **Status:** Missing
- **Given** dispatcher permanent failure on start
- **When** composite runs
- **Then** job fails per policy; ticket released

### NEG-JS-005 — Completion event wrong correlation
- **Priority:** P1 | **AC:** JS-AC-004 | **Status:** Covered
- **Given** job waiting on `job-1`
- **When** completion for `job-2`
- **Then** no resume; job-1 still waiting

### NEG-JS-006 — Completion event before start committed
- **Priority:** P0 | **AC:** EV-030 | **Status:** Missing
- **Given** completion arrives early
- **When** start later commits
- **Then** buffer and match (durable) — same as AC-104

### NEG-JS-007 — Job timeout: stop command failure
- **Priority:** P1 | **AC:** JS-AC-006 | **Status:** Missing
- **Given** timeout fires; delete job dispatch fails retryable
- **When** pump retries
- **Then** eventual stop or operator alert state

### NEG-JS-008 — Cancel run: partial stop dispatch failure
- **Priority:** P1 | **AC:** JS-AC-009 | **Status:** Missing
- **Given** 3 running jobs; 1 stop fails permanent
- **When** cancel run
- **Then** recorded failure; other stops succeed

### NEG-JS-009 — Cancel run: no running jobs
- **Priority:** P2 | **AC:** JS-AC-009 | **Status:** Missing
- **When** cancel empty run
- **Then** success; zero stop commands

### NEG-JS-010 — Quota exceeded across definitions
- **Priority:** P1 | **AC:** JS-AC-007 | **Status:** Missing
- **Given** pool shared by workflow A and B
- **When** combined holders > N
- **Then** never occurs — cross-definition capacity test

### NEG-JS-011 — Scheduled start duplicate (waived AC)
- **Priority:** P1 | **AC:** JS-AC-008 | **Status:** Missing (waived)
- **Given** same occurrence key two triggers
- **When** second trigger
- **Then** returns existing run id

### NEG-JS-012 — DAG runner: start node fails before any child
- **Priority:** P1 | **AC:** JS-AC-003 | **Status:** Partial
- **When** root A fails
- **Then** B, C, D never scheduled

### NEG-JS-013 — External job: acquire pool then append fails
- **Priority:** P0 | **AC:** JS-AC-010 | **Status:** Partial (`R4DurableEngineFindingsTests`)
- **Then** ticket rolled back

### NEG-JS-014 — External job: multi-pool partial failure
- **Priority:** P0 | **AC:** JS-AC-012 | **Status:** Missing
- **Given** needs db-A and db-B
- **When** A granted; B wait; A released on B timeout
- **Then** never holds A alone past policy

### NEG-JS-015 — K8s adapter: invalid manifest in config
- **Priority:** P1 | **AC:** JS-003 | **Status:** Missing
- **When** dispatcher maps outbox to K8s call
- **Then** permanent failure; outbox poison/retry policy

### NEG-JS-016 — Watcher duplicate JobSucceeded events
- **Priority:** P1 | **AC:** EV-031 | **Status:** Missing
- **Given** K8s relist sends duplicate succeeded
- **When** ingested
- **Then** one resume only

---

## Edge-case scenarios

### EDGE-JS-001 — Diamond DAG happy path per-node definitions
- **Priority:** P0 | **AC:** JS-AC-001 | **Status:** Missing
- **Given** A→{B,C}→D with definitions 1–4
- **When** run to completion
- **Then** each child uses correct `DefinitionId`

### EDGE-JS-002 — DAG single-node run
- **Priority:** P2 | **AC:** JS-AC-001 | **Status:** Missing
- **Given** one node DAG
- **Then** completes after one job

### EDGE-JS-003 — DAG wide fan-out (100 children)
- **Priority:** P1 | **AC:** CP-021 | **Status:** Missing
- **Given** A→100 leaves
- **When** throttle applies
- **Then** maxConcurrency respected

### EDGE-JS-004 — Job duration crosses Continue-as-new boundary
- **Priority:** P1 | **AC:** AC-313 | **Status:** Missing
- **Given** long wait job; history rolls
- **When** completion arrives
- **Then** still resumes correct job

### EDGE-JS-005 — Scheduler restart mid-job (full E2E)
- **Priority:** P0 | **AC:** JS-AC-005 | **Status:** Partial
- **Given** external job started; host dies
- **When** restart
- **Then** wait still active; completion resumes; no second start

### EDGE-JS-006 — Job timeout exactly at completion arrival
- **Priority:** P1 | **AC:** JS-AC-006 | **Status:** Missing
- **Given** timeout and success same tick
- **Then** race policy picks one; stop not sent if success wins

### EDGE-JS-007 — Quota release on job failure after partial logs
- **Priority:** P1 | **AC:** JS-AC-011 | **Status:** Partial
- **Given** job failed externally
- **When** failure event processed
- **Then** ticket released exactly once

### EDGE-JS-008 — Queued job preserves place under FIFO
- **Priority:** P1 | **AC:** JS-AC-013 | **Status:** Partial
- **Given** jobs Q1, Q2 waiting for quota
- **When** slot frees
- **Then** Q1 starts; Q2 still waiting; no start outbox for Q2

### EDGE-JS-009 — RunExternalJob retry after transient K8s failure
- **Priority:** P1 | **AC:** CR-006 | **Status:** Missing
- **Given** start fails retryable twice then succeeds
- **Then** one committed job identity; one wait correlation

### EDGE-JS-010 — DAG reconstruction under retention purge
- **Priority:** P1 | **AC:** JS-AC-005 | **Status:** Missing
- **Given** old nodes purged per policy
- **When** reconstruct DAG
- **Then** terminal nodes show purged/tombstone; no throw

### EDGE-JS-011 — Concurrent completion of DAG sibling nodes
- **Priority:** P1 | **AC:** JS-AC-001 | **Status:** Missing
- **Given** B and C complete simultaneously
- **When** both recorded
- **Then** D scheduled exactly once

### EDGE-JS-012 — External job payload size limit
- **Priority:** P2 | **AC:** DU-052 | **Status:** Missing
- **Given** large job parameters in outbox
- **When** append
- **Then** within limits or explicit error

### EDGE-JS-013 — Pause run with jobs in flight
- **Priority:** P1 | **AC:** MG-013 | **Status:** Missing
- **Given** DAG run paused
- **When** job completions arrive
- **Then** buffered; run does not advance

### EDGE-JS-014 — Mixed outbox: child-start + job-start + lifecycle same commit
- **Priority:** P1 | **AC:** AC-613 | **Status:** Partial
- **When** pump processes
- **Then** all kinds dispatched; order preserved within transaction semantics

### EDGE-JS-015 — Lineage query across nested DAG runs
- **Priority:** P2 | **AC:** AC-614 | **Status:** Missing
- **Given** parent run spawned child DAG runs
- **When** navigate `RootInstanceId`
- **Then** full tree returned
