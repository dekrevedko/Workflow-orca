# 14. Driving Scenario — Kubernetes Job Scheduler with Typed DAGs (JS)

Scope: an advanced scheduler in which every executable DAG node is a durable workflow and may
submit one standard Kubernetes `batch/v1 Job`. EKS is one possible cluster environment, not the
product boundary. The scenario proves OrcaCore's typed DAG, durable wait, deadline, operation
identity, and resource-lease contracts without coupling OrcaCore to Kubernetes, AWS, or a
generic public external-job protocol.

## 14.1 Scenario vocabulary and project boundary

- A **DAG definition** is an immutable typed `OrcaCore.Dag` plan. Nodes reference typed durable
  workflows and edges declare direct dependencies.
- A **DAG run** has one immutable typed run input. Each executable node occurrence is one
  durable child workflow instance with lineage and a typed successful output.
- A **Kubernetes Job** is a standard `batch/v1 Job` created through the Kubernetes API. The
  cluster may be EKS, another managed Kubernetes service, or a self-managed cluster.
- The **scheduler companion** is a separate outward application/integration project. It may
  remain in `OrcaCore.slnx`, but it owns Kubernetes clients, manifests, namespaces, cluster
  targets, authentication/discovery, watchers/reconcilers, job DTOs, and operator policy.

Required dependency direction:

```text
scheduler companion -> OrcaCore.Dag -> OrcaCore application contracts
scheduler companion -> Kubernetes client SDK
scheduler companion -> AWS SDK only for a concrete AWS-specific capability

OrcaCore -X-> OrcaCore.Dag / Kubernetes / AWS / scheduler code
```

Kubernetes/AWS/job-system types SHALL NOT appear in any OrcaCore public signature or dependency
closure. The scheduler gateway is application integration code, not an OrcaCore persistence or
transport provider. Standard Kubernetes API access alone does not justify an AWS dependency.

## 14.2 Typed DAG requirements

### JS-001 Typed DAG input and node workflows
The scheduler SHALL define each DAG with immutable typed run input, unique validated
`DagNodeId` values, and typed `DurableWorkflowRef<TNodeInput,TNodeOutput>` node references. The
DAG compiler SHALL reject inspectable structure: cycles, duplicate node/dependency identities,
self/foreign references, and missing/duplicate `MapInput`. Typed references make wrong/resultless
output use compile-impossible. Because mapper code is opaque, undeclared/non-direct/foreign
`OutputOf` access SHALL fail deterministically during mapping as `DAG_INPUT_MAPPING_INVALID`
before mapped-input commit or child start, not as a fabricated build diagnostic.

Each pure node input projector may read only the immutable run input and typed outputs of
declared direct successful resultful dependencies. It may be reevaluated before one
fixed-codec-normalized mapped-input commit, which is reused after restart. Resultless nodes may
be dependencies but cannot be passed to `OutputOf`. Child business state is private. Small
immutable output DTOs flow through the graph; large datasets, artifacts, and logs use typed
external references. A node with no dependencies is valid and may map only immutable run input.

### JS-002 Child-instance-per-node execution
Each admitted node occurrence SHALL execute as one durable child workflow instance. Internal
runtime child-start/join records provide deterministic identity, lineage, recovery, output
handoff, and cancellation propagation; no public `RunChild`/`RunChildren` member is used or
shipped. The runtime, not caller code, advances ready nodes and prevents duplicate starts after
concurrent dependency completions or host replacement.

Without a DAG cancellation request, child failure/timeout/termination/cancellation maps to a
stable failed-node code and blocks transitive dependants as `DependencyBlocked`; independent
ready/running nodes continue and the run fails. A DAG cancellation request prevents admission,
cancels pending/ready nodes, requests running-child cancellation, and completes as `Cancelled`
after running children terminate. DAG-host `MaxConcurrentNodes` remains separate from workflow
path tokens and resource pools and counts every started nonterminal child, including one parked
inside its workflow wait, until terminal. `DagRunHandle.WaitForTerminalAsync` uses notification
subscribe/recheck without polling; caller cancellation cancels only that local wait.

### JS-003 Immutable DAG and workflow versions
Every DAG and referenced workflow identity/version is bound to a structural fingerprint. Graph
node/edge/member order, strong values, referenced workflow types, and fixed codec format
contribute. Mapping/projector/selector bodies, scheduler request construction, and opaque
configuration do not pretend to be hashed; changing any requires a new definition version.
Reusing an identity/version with another structural fingerprint returns a typed conflict.

## 14.3 Kubernetes Job workflow pattern

### JS-004 Bounded idempotent submit step
The node workflow MAY execute a named durable application step that makes one short,
`WithStepTimeout`-bounded create-or-observe call through an application-owned Kubernetes
gateway. It SHALL use `StepExecutionContext.OperationId` as the logical idempotency identity;
`AttemptNumber` is the durable retry-policy attempt ordinal and is diagnostic only. A host-loss
replay of an in-flight attempt reuses the same attempt ordinal and persisted attempt deadline and
does not consume another `maxAttempts` slot; only a committed policy retry advances the ordinal.
One external effect per durable step occurrence is the v1 authoring rule.

The gateway SHALL derive a deterministic Kubernetes name and/or idempotency labels from the
workflow instance and `StepOperationId`, and record a deterministic desired-spec fingerprint.
Its retry behavior is:

1. no matching Job -> create;
2. matching Job with the same operation/spec fingerprint -> observe/reattach and return the
   same logical submission result;
3. same name with another operation or spec fingerprint -> permanent conflict;
4. ambiguous API response -> query and prove which of the prior outcomes occurred.

OrcaCore supplies stable operation identity and at-least-once step invocation. It does not
claim exactly-once Kubernetes API effects. Polly MAY be used inside the gateway for transport
resilience, but it does not define workflow retry, timeout, replay, or terminal semantics.

### JS-005 Small durable Kubernetes reference
The node state SHALL persist only application-owned intent and a small reference containing a
logical cluster target, namespace, name, Kubernetes UID when observed, `StepOperationId`, and
desired-spec fingerprint, plus normalized terminal outcome/result references. It SHALL NOT
persist client credentials, complete Kubernetes API objects, Pod lists, logs, or watch state as
workflow business state.

Cluster target is a registered logical name, not a kubeconfig path or API URL. Stop/delete
reconciliation SHALL use the observed Kubernetes UID (or an equivalently strong precondition),
not object name alone, so deletion/recreation cannot stop a successor Job accidentally.

### JS-006 Watch and resume
After submit/observe commits, the workflow uses ordinary structural `Wait` correlated by its
application job reference. A companion watcher/reconciler observes Kubernetes and reports one
normalized terminal event through OrcaCore's standard event facade. It creates one caller-stable
`EventId` per logical report and reuses it unchanged on retry/redelivery. If completion is
reported before wait registration, delivery returns non-consuming `NoActiveWait`; the companion
observes the active wait and redelivers the same `EventId` and identical normalized envelope.
Only acceptance writes durable inbox/dedup state. Watcher disconnect/relist, host loss, and
duplicate delivery therefore require no pending-event mailbox.

The watcher SHALL not depend on uninterrupted Kubernetes watch history; after disconnect or an
unavailable resource version it relists/reconciles current state. Automatic Kubernetes
finished-Job cleanup SHALL not erase the only terminal evidence before OrcaCore durably accepts
the result.

### JS-007 Three distinct time bounds

- `WithStepTimeout` bounds one submit/query API attempt and fences its late result. It does not
  prove whether an ambiguous create succeeded and does not stop an already-created Job.
- Kubernetes `activeDeadlineSeconds` (when used) bounds cluster workload execution under
  Kubernetes semantics.
- `CompleteWithin` bounds the node workflow from start, including lease queueing, retries,
  waits, and cleanup decision; its deadline survives restart.

None of these alone proves protected work stopped or authorizes unsafe resource release.

## 14.4 Mandatory durable resource leasing

### JS-008 Scheduler jobs declare external capacity atomically
Any Kubernetes Job that may consume capacity governed by the scheduler (for example database
connection budgets) SHALL run under scoped durable
`AcquireResources(ResourceLeaseRequest, body)`. The non-empty request names one or more
`ResourcePoolName` values and positive units; all pools grant atomically before create/observe.
If unavailable, only the requesting fiber parks and no Kubernetes resource is created.

When the external Job consumes the governed resource for its whole lifetime, submit, `Wait`,
and the immediate first post-resume outcome-validation step remain inside the lexical lease body;
the lease intentionally survives the wait and host restart. That step SHALL validate the terminal
event's operation ID, lease-protection token, Kubernetes Job UID/incarnation, and terminal state
before the lexical body may complete and release capacity. A malformed, stale, or otherwise
unproven report cannot release the lease and follows the applicable failure/quarantine path. If
only a short step needs a resource, its lease scope ends before the long wait; later processing
opens a new scope only if it needs capacity again. OrcaCore never silently releases and reacquires
a lease at `Wait`, because capacity and external-work identity could change between the two sides.

Sequential scopes and one fully lexical scope per root-`While` iteration are legal. Independent
sibling scopes may acquire independently. A descendant cannot acquire while an ancestor scope
is pending/held; concurrently needed resources belong in one request.

### JS-009 Cancellation and protected-work proof
The leased step context exposes a runtime-created round-trippable `LeaseProtectionToken`. The
scheduler SHALL label/record it with every protected Job occurrence. Cancellation, workflow
deadline, step timeout, ambiguous submit, process loss, or forced termination while protected
work may exist retains exact capacity. A retryable timeout/ambiguous submit/recovered in-flight
attempt remains `AmbiguousHeld` while the lexical owner is recoverable, keeping the same
`StepOperationId`, policy `AttemptNumber`, persisted attempt deadline, `LeaseProtectionToken`,
tickets, and units. A host-loss replay of that in-flight attempt does not consume retry budget;
only a committed policy retry advances `AttemptNumber`. A next retry cannot overlap a
still-running prior body in the same process; host-loss replay may proceed, and success alone does
not erase ambiguity. Ambiguous scope exit, exhaustion, cancellation, deadline, forced
termination, or abandonment transfers to `Quarantined` before workflow/DAG progression.

A trusted scheduler reconciler locates the exact Job/owned Pods, requests idempotent stop using
UID preconditions, and waits until all protected work is terminal, absent under a causally
sufficient observation, or end-to-end fenced. It then calls the provider-neutral advanced
`IDurableResourceLeaseRecovery.ConfirmProtectedWorkStoppedAsync(token, confirmationId, ...)`.
The caller-created `StopConfirmationId` is reused for retry. Confirmation precedence is total: a
confirmation ID already bound to another token returns `ConfirmationConflict` before the target
token's lifecycle is evaluated; an ID already accepted for the same token, or a token already
released by another accepted confirmation, returns `AlreadyConfirmed`; otherwise live lexical
ownership returns `NotConfirmable`, the first valid quarantine confirmation returns `Released`,
and a normal-release, unknown, or retention-purged token returns `TokenNotFound`.

A delete acknowledgement, elapsed time, workflow terminal status, Kubernetes label, or pool
review deadline alone is not proof. Until exact confirmation, capacity stays reserved. The
author supplies no lease TTL/renewal/holder ID. Pool review marks and audits an obligation, then
reconciles exact owner/ticket/provider-generation state without time-only reclaim.

## 14.5 Observability and operational reconstruction

### JS-010 DAG and Job inspection
Operators SHALL reconstruct a DAG run from complete authored-order `DagRunSnapshot`/
`DagNodeSnapshot` projections: node IDs/ordinals, dependency edges, child instance IDs, closed
run/node status, ready/start/completion timings, mapped-input fingerprint, output availability,
and failure. Kubernetes-specific details are joined by the
scheduler companion using its small job reference and cluster API; OrcaCore management APIs do
not expose Kubernetes objects.

Operational views SHALL distinguish workflow terminal state from pending protected-work
cleanup/quarantine, and expose lease pool, units, queue/reconciliation age, protection identity
(redacted as appropriate), and stop-confirmation outcome. Operators must be able to tell why a
new Job is not admitted without inspecting business payloads. The companion SHALL discover
outstanding or crash-before-label obligations through trusted
`IDurableResourceLeaseDiagnostics`, then correlate by `LeaseProtectionToken`; ordinary workflow
handles remain free of advanced ownership/ticket facts.

## 14.6 Acceptance criteria

- **JS-AC-001** *Diamond DAG joins typed outputs* — Given `A -> {B,C} -> D`, D starts once
  only after B/C outputs commit; it maps run input plus those declared direct outputs.
  [JS-001/002]
- **JS-AC-002** *Structural build and opaque mapper failures differ* — Cycles, duplicate node/
  dependency identities, self/foreign references, and missing/duplicate mapping return
  accumulated build diagnostics. Wrong/resultless output use does not compile; undeclared/
  non-direct output access hidden in mapper code fails as `DAG_INPUT_MAPPING_INVALID` before
  input commit/child start, while independent nodes continue. [JS-001]
- **JS-AC-003** *Node child identity survives restart* — **[provider]** Host replacement at
  every child-start/output boundary creates no duplicate child and reuses committed mapped
  input. [JS-001/002]
- **JS-AC-004** *Failure closure and cancellation are stable* — Without DAG cancellation,
  every non-success child maps to a stable failed-node code, blocks transitive dependants, and
  fails the run while independent nodes continue. A DAG cancellation request uses the exact
  cancellation state progression and waits for running children. Snapshots stay authored-order.
  [JS-002]
- **JS-AC-005** *Structural drift conflicts; code drift versions* — Changed graph structure
  under the same identity/version conflicts. Changed mapping or Kubernetes request-construction
  code uses a new version because opaque code is intentionally outside the fingerprint.
  [JS-003]
- **JS-AC-006** *Create succeeds but response is lost* — The durable step retries with the
  same `StepOperationId`; the gateway observes the matching Job/spec and no duplicate Job is
  created. Another occurrence gets another operation ID. [JS-004]
- **JS-AC-007** *Watcher report is restart-safe* — A Job completes before or after wait
  registration and across host/watcher replacement. Pre-wait delivery returns `NoActiveWait`;
  after observing the wait the companion redelivers the same `EventId`, which is accepted once
  and resumes exactly once.
  [JS-006]
- **JS-AC-008** *Three time bounds do not alias* — Submit-attempt timeout, Kubernetes active
  deadline, and workflow `CompleteWithin` are independently observable and survive their
  documented boundaries. [JS-007]
- **JS-AC-009** *Start is idempotent* — Retrying the same `StartIdempotencyKey` and deterministic
  input returns one DAG run; conflicting input/version/fingerprint returns a conflict.
  [DU-053, JS-003]
- **JS-AC-010** *Capacity precedes Kubernetes creation* — A queued node creates no Job; grant
  of the complete atomic request commits before the bounded create-or-observe step runs.
  [JS-008]
- **JS-AC-011** *Lease spans Job lifetime when authored that way* — A running Job holds its
  exact tickets across ordinary `Wait` and scheduler restart. The immediate first post-resume
  step remains inside the lease, validates operation ID, protection token, Job UID/incarnation,
  and terminal state, and only then may release once before the lease body's parent resumes. A
  malformed, stale, or unproven report cannot release. [JS-008]
- **JS-AC-012** *Multi-pool acquisition is atomic* — A Job requiring two pools holds neither
  until both can grant; it never starts with a partial budget. [JS-008]
- **JS-AC-013** *Timeout/cancel ambiguity and quarantine are safe* — Retryable timeout/process
  loss with potentially live Job/Pods remains `AmbiguousHeld` under the same operation,
  policy-attempt ordinal/deadline, token, and tickets; no in-process attempt overlap occurs,
  host-loss replay consumes no retry slot, and success alone does not
  release. Ambiguous exit/cancel/deadline/exhaustion transfers to quarantine before progress;
  delete acknowledgement alone does not release it. [JS-009]
- **JS-AC-014** *Trusted stop confirmation is exact and idempotent* — **[provider]** Live
  lexical ownership is `NotConfirmable`; confirmed terminal/absent/fenced quarantine releases
  once; retry or another confirmation after an accepted release is `AlreadyConfirmed`; and
  normal-release/unknown token is `TokenNotFound`. A confirmation ID bound to another token wins
  precedence as `ConfirmationConflict` before target-token lifecycle evaluation and cannot
  release a successor. [JS-009]
- **JS-AC-015** *Review deadline never steals live capacity* — **[provider]** A due live or
  ambiguous owner is marked/audited and remains held without renewal until normal exact release
  or trusted stop/fence proof. [JS-009]
- **JS-AC-016** *Dependency isolation is enforced* — Architecture tests prove the primary
  `OrcaCore` application package and all core packages reference no DAG/Kubernetes/AWS/scheduler
  SDK; the companion points inward and
  works against a non-EKS Kubernetes test cluster/fake API. [JS-001, PR-005]
- **JS-AC-017** *Resultless prerequisite is valid* — A resultless setup/validation node may be
  a declared dependency and gates its dependant, but exposes no output and rejects `OutputOf`.
  [JS-001]
- **JS-AC-018** *Scheduler waits and quarantine discovery are reactive* — DAG terminal waiting
  and typed workflow output waiting use notification/recheck without polling or casts; caller
  cancellation is local. Trusted lease diagnostics enumerate a quarantine even when the crash
  preceded Job-label persistence, allowing token-correlated reconciliation. [JS-002/009/010]

## 14.7 Deferred generic job surface

The companion pattern does not pre-approve public `RunExternalJob`, job-specific runtime facade
methods, `ExternalJobKey`, or `ExternalJobId`. A future generic composite requires one explicit
amendment covering typed request/result, dispatch/outbox topology, identity, timeout/stop,
report deduplication, resource bracket, and provider certification. Until then, the ordinary
typed step + ordinary `Wait` pattern is the supported v1 scheduler integration.
