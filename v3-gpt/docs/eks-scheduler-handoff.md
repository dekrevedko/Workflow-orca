# EKS Scheduler Enablement Handoff

This handoff defines the boundary between OrcaCore and an EKS job scheduler application.
Kubernetes concepts stay out of workflow definitions; they belong to the scheduler app and
its provider adapters.

## Ownership Boundary

OrcaCore owns:

- durable workflow orchestration, joins, waits, retries, timeouts, cancellation semantics,
  lineage, projections, and management contracts;
- normalized durable provider ports for event store, inbox, outbox, projections, timers,
  resource pools, payload serialization, and message dispatch;
- `StartOrGet` idempotency semantics for durable scheduled starts;
- provider certification invariants for commit atomicity, expected-version conflicts,
  inbox deduplication, outbox at-least-once dispatch, projections, timers, and retention.

The scheduler application owns:

- cron or recurrence triggering;
- Kubernetes client configuration, namespaces, manifests, labels, service accounts, and
  tenant policy;
- dispatcher deployment and watcher deployment;
- operator UI/API authentication and authorization;
- mapping scheduler concepts to normalized OrcaCore outbox records and inbound envelopes.

## JS-003 Kubernetes Adapter Contracts

The scheduler should implement two adapter sides.

### Dispatcher

The dispatcher reads OrcaCore outbox records and performs Kubernetes API calls. Kubernetes
job creation/deletion is an external side effect and should not run inline inside a durable
workflow step or Orleans grain turn. OrcaCore commits normalized outbox/job intent first;
the scheduler dispatcher performs the Kubernetes call and the watcher/ingestor reports the
result back through normalized events with stable `EventId` values. It should support at
least:

- job-start records: create a Kubernetes Job;
- job-stop records: delete or otherwise terminate a Kubernetes Job;
- retryable failures for transient Kubernetes API, network, quota, and conflict conditions;
- permanent failures for invalid manifests, invalid tenant configuration, or policy-denied
  requests that cannot succeed by retrying unchanged.

The dispatcher must return the normalized `DispatchResult` category to OrcaCore. It must not
expose Kubernetes object types through workflow definitions.

### Watcher/Ingestor

The watcher observes Kubernetes Job status and raises normalized workflow events back into
OrcaCore:

- `JobSucceeded` for terminal success;
- `JobFailed` for terminal failure;
- the original job `CorrelationId` on every event;
- a stable `EventId` for inbox deduplication on redelivery.

Watcher redelivery is expected. The scheduler app should treat Kubernetes watch reconnects,
resyncs, and duplicate status observations as normal and rely on OrcaCore inbox dedup for
the workflow side.

## JS-004 Scheduled Starts

Cron and recurrence are outside the library. The scheduler app should compute due
occurrences and call durable `StartOrGet` for each occurrence.

Use a deterministic occurrence key with this canonical shape:

```text
eks-scheduler/v1/tenant/{tenantId}/dag/{dagDefinitionId}/schedule/{scheduleId}/occurrence/{occurrenceStartUtc:yyyyMMddTHHmmssZ}
```

Rules:

- `occurrenceStartUtc` is the scheduled occurrence start in UTC, not the wall-clock time when
  the trigger process noticed it.
- `tenantId`, `dagDefinitionId`, and `scheduleId` must already be canonical scheduler
  identities. Do not include display names.
- The same schedule occurrence must produce the same key across scheduler restarts, leader
  changes, retries, and clock skew in individual trigger processes.
- A changed DAG definition version should remain part of the OrcaCore `DefinitionVersion`,
  not be hidden in a noncanonical key suffix.
- If the scheduler later supports manual backfills, use a separate key namespace such as
  `eks-scheduler/v1/tenant/{tenantId}/dag/{dagDefinitionId}/backfill/{backfillId}` so
  scheduled and manual starts cannot collide accidentally.

JS-AC-008 belongs to the scheduler app acceptance suite: two triggers for the same canonical
occurrence key must call `StartOrGet` and yield one run. The expected observable result is
one created instance and subsequent calls returning the existing instance for that key.


## Dispatched Long-Work Boundary

Kubernetes work is long external work and must not run inside an OrcaCore workflow step,
durable lane segment, or Orleans grain turn. The scheduler app should model it as durable
dispatch: OrcaCore commits a normalized outbox job-start/job-stop record, the scheduler
dispatcher performs the Kubernetes API call, and the watcher/ingestor raises a normalized
completion event back into OrcaCore. This keeps document-16 durable-driver continuation
semantics, outbox at-least-once dispatch, and inbox deduplication as the reliability
boundary.

## Scheduler-App Acceptance Ownership

The scheduler app owns acceptance tests for:

- JS-AC-004 watcher redelivery dedup through the normalized event path;
- JS-AC-005 restart while an EKS Job is running;
- JS-AC-006 timeout dispatching a job-stop outbox record and deleting the Kubernetes Job;
- JS-AC-008 scheduled occurrence idempotency with the canonical key;
- JS-AC-009 run cancellation dispatching stop commands for in-flight jobs;
- JS-AC-010 through JS-AC-013 durable resource-pool behavior under real scheduler
  deployment and restart conditions.

OrcaCore provider certification continues to own provider-invariant tests. The scheduler app
should consume those invariants but still prove its Kubernetes-specific mappings, policies,
and restart behavior in its own repository.
