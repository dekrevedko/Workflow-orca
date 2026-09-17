# Kubernetes Scheduler Companion Handoff

**Status:** first-release planning baseline, revised 2026-07-18.

This handoff defines the boundary between OrcaCore and a Kubernetes Job scheduler. EKS is one
possible Kubernetes host, not an OrcaCore execution model. Kubernetes, AWS, and job-adapter
code lives in a separate outward-dependent companion project. That project may stay in
`OrcaCore.slnx` for the first release, but it is not part of the OrcaCore package dependency
closure.

The normative public contract is
[`17-selected-mode-capability-matrix.md`](specs/17-selected-mode-capability-matrix.md).

## Ownership boundary

OrcaCore owns:

- typed ephemeral/durable workflow input and output, registered definition versions, and
  immutable compiled fingerprints;
- `OrcaCore.Dag` typed run input, node workflow references, direct-dependency output mapping,
  validation, and `OrcaCore.Dag.Hosting` as the only bridge to runtime-owned progression through
  the versioned internal child-workflow protocol in `OrcaCore.Durable.Hosting`;
- durable retries, `WithStepTimeout`, `CompleteWithin`, `Wait`, event inbox deduplication,
  continuation handoff, and management projections;
- runtime-created `StepOperationId`, stable for one logical step visit across retry, replay,
  process replacement, and competing drivers;
- scoped-only durable `AcquireResources(request, body)`, exact lease accounting, quarantine,
  opaque `LeaseProtectionToken`, and trusted idempotent stop confirmation;
- one serialized resource-governance aggregate per configured provider partition, with atomic
  multi-pool FIFO admission and no force release, renewal, or time-only reclaim;
- `StartOrGetAsync`/`StartIdempotencyKey` semantics for scheduler occurrence deduplication.

The companion scheduler owns:

- Kubernetes client configuration and credentials, including EKS/AWS authentication or
  discovery only when a concrete deployment requires it;
- `batch/v1 Job` manifests, namespaces, cluster targets, names, labels/annotations, and tenant
  policy;
- bounded create/get/query/delete calls, Job UID preconditions, watcher relist/reconnect, and
  stop reconciliation;
- cron/recurrence, operator UI/API authorization, logs/artifacts, and Kubernetes-specific
  terminal-result interpretation;
- adapter and real-cluster acceptance tests.

No Kubernetes object, AWS type, kubeconfig path, cluster URI, manifest, or SDK dependency
appears in an OrcaCore public signature. Dependency direction is always:

```text
OrcaCore <- OrcaCore.Dag <- OrcaCore.Dag.Hosting <- companion scheduler application
OrcaCore.Durable.Hosting <- OrcaCore.Dag.Hosting
```

## First-release authored shape

Public `RunExternalJob`, `RunChild`, and `RunChildren` are deferred. Their rationale and re-entry
criteria remain in the
[future-capability registry](specs/13-phasing-and-open-questions.md#134-future-capability-registry).
A DAG node is one typed durable child workflow instance. That workflow uses ordinary v1 constructs:

```csharp
Workflow.Durable<NodeState>(definitionId, version)
    .Init<NodeInput>(CreateState)
    .AcquireResources(eksSlotRequest, leased => leased
        .Then<EnsureKubernetesJobSubmitted>()
            .WithStepTimeout(TimeSpan.FromSeconds(30))
            .WithRetry(maxAttempts: 3, fixedDelay: TimeSpan.FromSeconds(2))
        .Wait(KubernetesEvents.JobTerminal, state => state.Value.JobCorrelation)
        .Then<ApplyKubernetesJobOutcome>())
    .End<NodeOutput>(state => state.Value.Output)
    .Build();
```

The typed submit step performs only a short, bounded create-or-observe API call. The external
workload never runs inside the step or durable instance turn. The lease intentionally encloses
the `Wait` when it represents capacity consumed by the running Job. In that shape,
`ApplyKubernetesJobOutcome` is the immediate first step after `Wait`, consumes
`StepContext<NodeState>.ResumedEvent`, and validates its operation ID, protection token, Job
UID/incarnation, and terminal state before
the lease body may exit. A malformed, stale, or unproven terminal report cannot release capacity
and follows the applicable failure/quarantine path. A resource needed only by submission belongs
in a smaller acquisition scope so it is released before the wait.

Ephemeral lambda steps do not change this boundary. Durable scheduler workflows use named
registered step types so definition identity and replay remain stable.

## Identity and create-or-observe protocol

| Identity | Owner and use |
|---|---|
| `StartIdempotencyKey` | Scheduler-selected key for one DAG run/schedule occurrence |
| `StepOperationId` | OrcaCore-created key for one logical submit-step visit; external idempotency key |
| `AttemptNumber` | Durable retry-policy attempt ordinal; host-loss replay may reuse it; never an idempotency key |
| Kubernetes Job name | Deterministically derived companion identity suitable for Kubernetes naming rules |
| Kubernetes Job UID | Server-created object incarnation used for later query/stop preconditions |
| `LeaseProtectionToken` | OrcaCore-created opaque identity labeling every protected work item in one lease scope |
| `EventId` | Watcher-selected report identity, reused on redelivery for inbox deduplication |

The companion adapter records the operation ID, protection token, desired-spec fingerprint,
workflow instance, and definition identity/version as labels or annotations using a
Kubernetes-safe encoding. It implements create-or-observe:

1. Job absent: create it.
2. Matching Job already exists with the same operation identity and desired-spec fingerprint:
   observe/reattach and return the existing Job reference.
3. Name exists with another identity or fingerprint: return a permanent collision.
4. Create response is ambiguous: query by deterministic identity and compare the fingerprint;
   never blindly create under a new name.

OrcaCore guarantees stable identity and at-least-once step invocation. A physical replay after
uncertain host loss reuses the in-flight policy attempt's ordinal and persisted deadline and does
not consume another `maxAttempts` slot; only a committed policy retry advances the ordinal. The
companion adapter provides idempotent create-or-observe behavior. Neither component claims
exactly-once external API calls.

`DefinitionId` and `DefinitionVersion` are validated immutable reference values; empty/default
identity and non-positive versions are rejected at every seam. One such identity/version pair
has one immutable compiled structural fingerprint. Changing Job-construction logic or any opaque
input that affects the desired manifest requires a new `DefinitionVersion`; opaque code and captured
values do not contribute to the author fingerprint. A retried `StepOperationId` must never construct
a different desired Job silently.

## Watch and completion protocol

The watcher normalizes terminal Job state into a typed application event and delivers it
through OrcaCore `Wait` routing:

- include the workflow correlation selected by the authored wait;
- assign one stable `EventId` to the observed terminal report and reuse it on redelivery;
- tolerate watch disconnects and expired resource versions by relisting;
- treat duplicate status observations and watcher failover as normal;
- persist only compact Job reference/outcome DTOs in workflow state; keep Pod objects, logs,
  credentials, manifests, and large artifacts external.

Delivery before the target wait is active returns non-consuming `NoActiveWait`, writes no
mailbox/inbox/dedup state, and permits the same `EventId` to be redelivered as its first accepted
event after wait registration. Once accepted, inbox deduplication is per target `InstanceId` by
`EventId`: the same normalized envelope is a duplicate, while reusing the ID with different content
is an `EventConflict`. Correlation routing
matches exactly one active wait by `(DefinitionId, EventName, CorrelationId)`; a second active wait
for that key fails before parking. The companion must use occurrence-specific correlation values
when a loop can have more than one concurrent logical wait. Definition-targeted fanout is not a
v1 delivery route.

Do not delete a completed Job through `ttlSecondsAfterFinished` until the terminal outcome has
been durably accepted or the companion can reconstruct it elsewhere. Otherwise a watcher
outage may erase the evidence needed to complete the workflow.

## Deadlines, cancellation, and lease quarantine

These limits are deliberately distinct:

- `WithStepTimeout` bounds one durable create/query policy attempt. Host-loss replay of that
  in-flight attempt reuses its deadline. The timeout cannot prove whether an ambiguous API request
  created a Job.
- Kubernetes `activeDeadlineSeconds` may bound cluster execution, but it is Kubernetes policy,
  not an OrcaCore workflow deadline.
- `CompleteWithin` bounds the complete workflow, including queueing, retries, waits, and
  cleanup decisions.

A retryable timeout, ambiguous submit, process loss, or recovered in-flight create/query attempt
does not prove protected external work stopped. While retry remains allowed, the exact obligation
stays `AmbiguousHeld` with the same operation, policy-attempt ordinal/deadline, protection token,
tickets, and reserved capacity. There is no overlapping in-process leased retry; a host-loss
recovery may replay the same policy attempt without consuming retry budget. Only a committed
policy retry advances the ordinal and receives a new deadline.
The obligation enters capacity-reserving `Quarantined` only when ambiguous lexical-scope exit,
retry exhaustion, workflow cancellation/deadline/termination, or explicit abandonment wins before
safe progress. A trusted companion reconciler must then:

1. Observe the durable cancellation/deadline/quarantine state.
2. Locate every Job and active owned Pod labeled by the exact protection token.
3. Request idempotent stop using the Job UID as a precondition where available.
4. Confirm all matching work is terminal, absent, or end-to-end fenced.
5. Call the advanced generic
   `IDurableResourceLeaseRecovery.ConfirmProtectedWorkStoppedAsync` operation with the opaque
   protection token and a stable `StopConfirmationId`.

Delete acknowledgement, elapsed time, workflow terminal status, or label presence alone is
not stop proof. A stale or mismatched confirmation cannot release another occurrence. There is
no author TTL, holder renewal, or time-only reclaim.

Confirmation result precedence is total. A confirmation ID already bound to another token returns
`ConfirmationConflict` before the target token's state is evaluated. An ID already accepted for
the same token, or a token released by another accepted confirmation, returns `AlreadyConfirmed`.
Otherwise a live obligation returns `NotConfirmable`, the first valid quarantine confirmation
returns `Released`, and a normally released, unknown, or retention-purged token returns
`TokenNotFound`.

## Scheduled starts

Cron and recurrence remain companion-application concerns. Compute a deterministic occurrence
key and call the typed durable `StartOrGetAsync` operation. A recommended namespace is:

```text
kubernetes-scheduler/v1/tenant/{tenantId}/dag/{dagDefinitionId}/schedule/{scheduleId}/occurrence/{occurrenceStartUtc:yyyyMMddTHHmmssZ}
```

The occurrence time is the scheduled UTC instant, not when a trigger process noticed it. Keep
definition version in the registered workflow identity/version binding, not an ad hoc key
suffix. Use a separate namespace for manual backfills.

## Hosting composition

A scheduler execution host registers the durable role with
`AddOrcaCoreDurableEngine(DurableEngineHostOptions)`, a certified provider adapter, and
`OrcaCore.Dag.Hosting.AddOrcaCoreDag(DagHostOptions)`. A definition-less watcher/callback process
uses only `AddOrcaCoreDurableEventIngress()` plus the same provider role set; it does not run a
definition registry, execution worker, timer/reconciliation loop, or DAG coordinator. The
development/test in-memory provider is registered with `AddOrcaCoreInMemoryDurableProvider()` and
makes no process-restart claim. A catch-all `AddOrcaCore` or separate hosted-service toggle is not
part of v1.

## Acceptance ownership

OrcaCore tests own:

- stable `StepOperationId` across retry/replay/restart and distinct identity across loop,
  branch, item, and continue-as-new occurrences;
- typed event deduplication and definition-less callback-host continuation;
- scoped lease release, quarantine, exact stop confirmation, and capacity conservation;
- typed DAG mapping, one child instance per node, lineage, restart, and dependency failure.

The companion scheduler tests own:

- real Kubernetes create-or-observe behavior and manifest fingerprint collisions;
- watcher redelivery/relist, terminal normalization, and Job UID reuse/recreation cases;
- workflow/DAG restart while a Job is running;
- cancellation/deadline stop reconciliation and quarantine release only after proof;
- scheduled-occurrence idempotency and any EKS/AWS-specific authentication/discovery behavior.

The first-release solution may contain this companion project to prove the primary advanced
scheduler journey. Its SDKs, DTOs, and operational policy remain outside every OrcaCore
library package.
