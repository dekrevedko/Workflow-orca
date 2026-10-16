# Proposed durable DAG run-persistence and progression contract

Date: 2026-10-04. Status: proposed; NOT contract approval, canonical sync or source authority.
Process chain: checkpoint `b55056c0d9136f509296167ae5984881e7b5de97` (tree
`5880c8595f7b4f34026ec90f287a17bd3d63e6c6`), direct-child evidence
`e99f338d9319b9af5459f5685c69585493773d7c`, checkbox-only activation
`dab498ff54321d802b36ce052100954bbe00487d`. The 12,342-byte process verdict is
`f54a6ba50a1ea1ce0d18d2f904a388a1ab91145d7ea59f65da1fb89acc70370c`.

## 1. Authority and compatibility

This is the complete proposed section 10 successor of the independently approved
six-call child protocol, not a replacement for its unchanged identity, lineage,
codec or failure rules. It modifies ONLY the four registered requirement headings.
Workflow instances remain workflow instances; DAG runs are distinct aggregate
streams. No new friend, reverse package edge, public child API, codec permission,
provider family or private outbox is proposed. Main `src/**`, current API baselines
and canonical `openspec/specs/**` stay unchanged in this contract target.

All declarations below are proposed. Application DAG types remain in Dag; durable
bridge signatures carry GUID/ordinal coordinates and OrcaCore value types, never
Dag definitions, delegates or private child state. Provider lease/append types are
advanced API in Provider.Abstractions; record types and the versioned codec belong to
Runtime.Protocol, which already depends only on OrcaCore. Their concrete layout
and any required exported signatures are reviewed here before Task 2.1 implements
them. Ordinary workflow codec entries, stream text and payload bytes stay stable.

## 2. Exact stream and append evolution

BEFORE (`OrcaCore.Abstractions.Ids`, assembly OrcaCore):

```csharp
public readonly record struct WorkflowStreamId
{
    public WorkflowStreamId(InstanceId instanceId);
    public InstanceId InstanceId { get; }
    public override string ToString();
}
```

AFTER: retain that constructor, property and workflow rendering. Add
`public enum DurableAggregateKind { Workflow = 0, DagRun = 1 }`,
`public DurableAggregateKind AggregateKind { get; }`,
`public Guid? DagRunId { get; }`, and
`public static WorkflowStreamId ForDagRun(Guid runId)`.
The factory rejects Guid.Empty. Workflow values have kind Workflow and null
DagRunId. DAG values have kind DagRun and the exact nonempty run GUID;
InstanceId throws InvalidOperationException on a DAG value. Equality/hash include
the discriminator; no implicit scalar conversions or raw-string constructor is
added. ToString returns `workflow-{existing InstanceId text}` or
`dag-run-{lowercase D GUID}`. Default/empty values are rejected at every port.
Keeping the type name preserves workflow call sites, not permission to interpret
a DAG as a workflow. Every workflow-only port rejects kind DagRun before any
read/write, even if its GUID matches an ordinary instance.

BEFORE ProviderCommitBatch: required WorkflowStreamId StreamId, required
StreamVersion ExpectedVersion, Events, Checkpoint, InboxOperations,
InboxRouteMutations, InboxTargetPoisonOperations, OutboxRecords,
ProjectionOperations, TimerSchedules and StartIdempotencyOperations.
AFTER: keep every existing signature and add
`public DagRunAppendV1? DagRun { get; init; }`.
For Workflow streams DagRun must be null. For DagRun streams DagRun is required;
all workflow-specific lists are empty and workflow Checkpoint is null. Shared
OutboxRecords is permitted. Validate the whole batch before effects. This is an
explicit advanced provider-boundary extension, not a fake workflow InstanceId.

IWorkflowEventStore.AppendAsync(ProviderCommitBatch, CancellationToken) keeps its
existing Task<Result<AppendEventsResult>> signature and remains the sole append
boundary for orchestration facts. The provider serializes version, fence,
operation identity, records, checkpoint, run index and related outbox writes in
ONE transaction or commits none. No IWorkflowCommitStore exists or is introduced.
Lease/subscription operations below mutate only operational ownership, not DAG
business facts. They are explicit additions to IWorkflowEventStore, not a new
DAG-private storage port.

WorkflowEventCodec BEFORE and AFTER has exactly the same 21 discriminator entries.
No DAG subclass of WorkflowEvent, fallback, coercion or new workflow-event entry is
added. A separate DagRunRecordCodecV1 encodes/decodes the eight closed DAG kinds in
section 5, through exact version/property allowlists. Wrong aggregate/codec kinds
fail visibly before replay. Protocol and checkpoint framing are not the business
value codec; embedded input/output bytes remain orcacore-json-v1 unchanged.

## 3. Proposed advanced provider contracts

Value/binding/graph/snapshot/record/checkpoint types below are in
OrcaCore.Abstractions.Durable.Dag (Runtime.Protocol). Append/fence/lease/discovery/
subscription types below are in OrcaCore.Abstractions.Providers.Dag
(Provider.Abstractions). The record codec is in Runtime.Protocol and refers only
to its own records and OrcaCore, never back to Provider.Abstractions. Buffers and nested
collections are copied/validated by construction and again by ports. No CLR Type,
delegate or assembly-qualified name is persisted. Contract keys are exact ordinal
logical roles: `dag:{definition-id}:{version}:run-input:{type-shape-sha256}`,
`workflow:{definition-id}:{version}:input` or `:output`. Recovery resolves the
exact installed definition/version/fingerprint and its declared CLR types, then
checks the fixed-codec type graph. The DAG run-input shape digest is separate
from the unchanged authored DAG fingerprint: UTF-8/LF, full normalized declared
type name (not assembly-qualified), then each supported codec member's JSON name,
normalized declared type and CLR nullable-value status, in the codec's deterministic declared
order, recursively; C# nullable-reference annotations are not runtime discriminators.
Include enum scalar/member names and array/nullable element
types, reject cycles/polymorphic graphs under the existing codec rules. Use a
final LF and lowercase SHA-256. A different TRunInput/shape therefore conflicts
on reopen/registration even if the authored DAG fingerprint is equal. Task 8.6
must compare this separate exact input contract key. A changed binding is an integrity conflict,
not a remapping opportunity.

```csharp
public sealed record DagRunValueV1(
    bool IsPresent, string ContractKey, string CodecFormat,
    byte[] Bytes, PayloadFingerprint Fingerprint);
public sealed record DagRunBindingV1(
    DefinitionId DefinitionId, DefinitionVersion DefinitionVersion,
    DefinitionFingerprint Fingerprint);
public sealed record DagRunNodeV1(
    int Ordinal, string NodeId, DagRunBindingV1 Workflow,
    string InputContractKey, string? OutputContractKey,
    IReadOnlyList<int> DirectDependencies);
public sealed record DagRunGraphV1(
    DagRunBindingV1 Definition, string RunInputContractKey,
    IReadOnlyList<DagRunNodeV1> Nodes);
public sealed record DagRunStartKeyV1(string Value);
public enum DagRunStateV1 { Running, CancellationRequested, Succeeded, Failed, Cancelled, IntegrityFault }
public enum DagNodeStateV1 { Pending, Ready, StartAuthorized, Running, CancellationRequested, Succeeded, Failed, Cancelled, DependencyBlocked }
public sealed record DagRunNodeStateV1(
    int Ordinal, DagNodeStateV1 State, InstanceId? ChildInstanceId,
    DagRunValueV1? Input, DagRunValueV1? Output,
    string? FailureCode, DateTimeOffset? ReadyAt,
    DateTimeOffset? StartedAt, DateTimeOffset? CompletedAt);
public sealed record DagRunSnapshotV1(
    Guid RunId, StreamVersion StreamVersion, DagRunStartKeyV1 StartKey,
    DagRunGraphV1 Graph, DagRunValueV1 RunInput,
    DagRunStateV1 State, IReadOnlyList<DagRunNodeStateV1> Nodes,
    DateTimeOffset CreatedAt, DateTimeOffset UpdatedAt,
    DateTimeOffset? CompletedAt);
public enum DagRunRecordKindV1 { Created, InputAdmitted, MappingFailed, ChildJoined, CancellationRequested, Terminal, IntegrityFaulted, PoisonReconciled }
public sealed record DagRunRecordV1(
    int Version, DagRunRecordKindV1 Kind, Guid RunId, Guid OperationId,
    StreamVersion StreamVersion, DagRunSnapshotV1 Snapshot);
public sealed record DagRunCheckpointV1(
    int Version, Guid RunId, StreamVersion StreamVersion,
    string ContentType, byte[] Payload, string Sha256);
public sealed record DagRunFenceV1(Guid RunId, Guid OwnerId, long Token);
public sealed record DagRunAppendV1(
    Guid OperationId, string OperationFingerprint,
    DagRunFenceV1? Fence, IReadOnlyList<DagRunRecordV1> Records,
    DagRunCheckpointV1 Checkpoint);
public sealed record DagRunLeaseRequestV1(
    Guid RunId, Guid OwnerId, long? ExpectedFence, TimeSpan LeaseDuration);
public enum DagRunLeaseOutcomeV1 { Granted, HeldByOther, StaleFence, NotFound, Terminal }
public sealed record DagRunLeaseResultV1(
    DagRunLeaseOutcomeV1 Outcome, DagRunFenceV1? Fence,
    DateTimeOffset? ExpiresAt, StreamVersion? CurrentVersion);
public sealed record DagRunDiscoveryRequestV1(long AfterCreationSequence, int MaxCount);
public sealed record DagRunDiscoveryV1(
    long CreationSequence, Guid RunId, StreamVersion StreamVersion,
    DagRunBindingV1 Definition, DagRunStateV1 State);
public sealed record DagRunSubscriptionV1(
    Guid SubscriptionId, Guid RecipientHostId, Guid RunId,
    StreamVersion AfterVersion, TimeSpan LeaseDuration);
public enum DagRunSubscribeOutcomeV1 { Subscribed, AlreadyChanged, NotFound }
public sealed record DagRunSubscribeResultV1(
    DagRunSubscribeOutcomeV1 Outcome, StreamVersion? CurrentVersion);
```

IWorkflowEventStore adds these exact methods (all abstract; no default success or
NotSupported implementation):

```csharp
Task<Option<DagRunCheckpointV1>> LoadDagRunCheckpointAsync(
    WorkflowStreamId streamId, CancellationToken cancellationToken);
Task<Option<DagRunDiscoveryV1>> GetDagRunByStartKeyAsync(
    DagRunStartKeyV1 startKey, CancellationToken cancellationToken);
Task<IReadOnlyList<DagRunRecordV1>> LoadDagRunTailAsync(
    WorkflowStreamId streamId, StreamVersion afterVersion, CancellationToken cancellationToken);
Task<DagRunLeaseResultV1> AcquireDagRunLeaseAsync(
    DagRunLeaseRequestV1 request, CancellationToken cancellationToken);
Task<DagRunLeaseResultV1> RenewDagRunLeaseAsync(
    DagRunLeaseRequestV1 request, CancellationToken cancellationToken);
Task<DagRunLeaseResultV1> ReleaseDagRunLeaseAsync(
    DagRunFenceV1 fence, CancellationToken cancellationToken);
Task<IReadOnlyList<DagRunDiscoveryV1>> ListRecoverableDagRunsAsync(
    DagRunDiscoveryRequestV1 request, CancellationToken cancellationToken);
Task<DagRunSubscribeResultV1> SubscribeDagRunAsync(
    DagRunSubscriptionV1 subscription, CancellationToken cancellationToken);
Task UnsubscribeDagRunAsync(Guid subscriptionId, Guid recipientHostId,
    CancellationToken cancellationToken);
```

Load methods require DagRun kind. Lists page by monotonic creation sequence,
strictly greater than cursor, ascending, positive bounded MaxCount. They include
all nonterminal runs, including unowned/expired, parked, poisoned and unresolved
admissions. Startup scans every page from zero; continuous discovery scans wrap to
zero, so earlier expired ownership is not starved by newer runs. Recovery scans
are bounded discovery, NOT polling terminal state on a caller's behalf.

GetDagRunByStartKeyAsync performs an exact ordinal lookup in the DAG-caller
namespace only. Snapshot.StartKey binds the public StartIdempotencyKey value
(the existing nonblank, untrimmed, ordinal caller-key validation) to one runtime-created RunId, graph/definition and
exact input contract/bytes. Creation inserts that unique binding atomically with
the run. Competing creation under the same key fails DAG_START_KEY_CONFLICT with
NO effects; the coordinator reloads the winner and returns Existing for exact
compatibility or BindingConflict otherwise. Neither DAG caller nor child keys
enter ordinary workflow CallerStart keys. Cross-kind GUID collisions fail closed
in both workflow and DAG creation before effects; a lineage anchor is not a workflow.

Acquire uses the provider's transaction-time clock and locks the owning row.
Missing/terminal returns NotFound/Terminal. Active other ownership returns
HeldByOther without mutation. Acquisition after release/expiry increments a
monotonic positive token; a same-owner lost-response retry returns its existing
live token. Renew requires the same live owner/token (ExpectedFence required) and
extends expiry; expiry/stale identity returns StaleFence. Release accepts only
the current owner/token and returns Granted with null ExpiresAt; it never
terminalizes a run or releases node admission. Wrong owner cannot steal a lease.

For non-creation append, provider checks exact stream version, matching live
owner/run/token and unexpired lease in the same transaction. Creation requires
ExpectedVersion zero, null fence, absent aggregate and matching creation binding.
Exact OperationId + OperationFingerprint replay returns the original version
without effects, even after takeover; divergent reuse is an integrity conflict.
Version conflict, stale fence, invalid aggregate/frame and divergent operation
produce Result failure codes DAG_VERSION_CONFLICT, DAG_STALE_FENCE,
DAG_PROTOCOL_INVALID and DAG_INTEGRITY_CONFLICT respectively. No partial append,
admission, checkpoint, index, subscription-notification or outbox effect occurs.
OperationFingerprint is lowercase SHA-256 of the exact normalized protocol frame
excluding provider-assigned stream version, fence/lease and observation times;
it includes full immutable graph/binding/value bytes and intended transition.

## 4. Internal lifecycle and ready callback

These types are INTERNAL to Durable.Hosting, namespace
OrcaCore.Durable.Hosting.Dag. The six approved IDurableDagChildBridgeV1 calls
and their descriptor/result types remain unchanged. This new interface completes
the run side; no public application member or new friend follows from it.

```csharp
internal sealed record DurableDagRunCreateV1(
    Guid RunId, DagRunStartKeyV1 StartKey, DagRunGraphV1 Graph, object? Input, Type InputType);
internal enum DurableDagCreateOutcomeV1 { Created, Existing, BindingConflict }
internal sealed record DurableDagCreateResultV1(
    DurableDagCreateOutcomeV1 Outcome, DagRunSnapshotV1? Snapshot);
internal enum DurableDagMutationOutcomeV1 { Committed, Existing, CapacityDeferred, AdmissionStopped, StaleFence, VersionConflict, MappingInvalid, IntegrityConflict, NotFound }
internal sealed record DurableDagMutationResultV1(
    DurableDagMutationOutcomeV1 Outcome, StreamVersion? Version,
    DagRunSnapshotV1? Snapshot);
internal enum DurableDagCancelOutcomeV1 { Requested, AlreadyRequested, AlreadyTerminal, NotFound }
internal sealed record DurableDagCancelResultV1(
    DurableDagCancelOutcomeV1 Outcome, StreamVersion? Version);
internal sealed record DurableDagReadyNoticeV1(
    Guid RunId, StreamVersion Version, Guid? SubscriptionId);
internal interface IDurableDagReadySinkV1
{
    ValueTask HandleAsync(DurableDagReadyNoticeV1 notice, CancellationToken cancellationToken);
}
internal interface IDurableDagRunBridgeV1
{
    ValueTask<DurableDagCreateResultV1> CreateOrReopenAsync(
        DurableDagRunCreateV1 run, CancellationToken cancellationToken);
    ValueTask<DagRunSnapshotV1?> ReadAsync(Guid runId, CancellationToken cancellationToken);
    ValueTask<DurableDagMutationResultV1> AdmitMappedInputAsync(
        DurableDagNodeContractV1 node, DurableDagMappedInputV1 input,
        DagRunFenceV1 fence, StreamVersion expectedVersion, int maxConcurrentNodes,
        CancellationToken cancellationToken);
    ValueTask<DurableDagMutationResultV1> CommitMappingFailureAsync(
        Guid runId, int ordinal, DagRunFenceV1 fence, StreamVersion expectedVersion,
        CancellationToken cancellationToken);
    ValueTask<DurableDagMutationResultV1> ApplyChildTerminalAsync(
        Guid runId, int ordinal, CancellationToken cancellationToken);
    ValueTask<DurableDagCancelResultV1> RequestRunCancellationAsync(
        Guid runId, CancellationToken cancellationToken);
    ValueTask<DagRunSnapshotV1> WaitForChangeAsync(
        Guid runId, StreamVersion afterVersion, CancellationToken cancellationToken);
    ValueTask<DagRunSnapshotV1> WaitForTerminalAsync(
        Guid runId, CancellationToken cancellationToken);
    ValueTask<DurableDagMutationResultV1> RecordIntegrityFaultAsync(
        Guid runId, string code, DagRunFenceV1 fence, StreamVersion expectedVersion,
        CancellationToken cancellationToken);
    ValueTask<DurableDagMutationResultV1> ReconcilePoisonAsync(
        Guid runId, Guid operationId, DagRunFenceV1 fence, StreamVersion expectedVersion,
        CancellationToken cancellationToken);
}
```

Sink registration uses the existing DI extension's internal coordinator/registry
path, not a public callback. Durable.Hosting defines the interface and resolves
zero or one sink; Dag.Hosting implements it. The durable package never references
Dag or Dag.Hosting. No delegates, plan objects or untyped workflow objects cross
the package edge. The implementation review freezes an exact type/member
metadata allowlist including these contracts, the unchanged six-call child
protocol and existing legitimate registration references; no wildcard exemption.

CreateOrReopen validates the full immutable graph and exact installed bindings,
run input type/value and fixed-codec round trip before a creation append. Graph
ordinals are contiguous zero-based; IDs are unique ordinal strings; dependencies
are distinct same-run ordinals and acyclic. Initial nodes are Ready when indegree
zero, otherwise Pending. Same start key/run identity/exact immutable binding+input returns
Existing; a mismatch returns BindingConflict without mutation. Missing exact
definitions remain retryable registry unavailability, not mapping failure.

The current leased progression owner alone reads mapping inputs/evaluates and
admits. Every successful declared resultful dependency is eagerly decoded,
including unused outputs. Ordinary declared-type decode, unsupported input or
mapper failure is DAG_INPUT_MAPPING_INVALID, before mapper/input/start as
applicable. CommitMappingFailure records that exact code, computes transitive
DependencyBlocked closure in authored order, and leaves independent branches
eligible. Storage/protocol/checksum/unknown-version/lease failures are NOT
mapping failures. Mapper-thrown cancellation follows the existing mapping rule;
local call cancellation outside evaluation throws without a durable cancellation
request. OOM, stack-overflow and access-violation remain process exclusions.

AdmitMappedInput validates expected version/fence/exact binding/readiness and the
CURRENT owner's validated host limit. The integer is obtained from the owning
host's options, never an application or remote caller. There is no creation-time
persisted ceiling. Started nonterminal children AND start-authorized intents
count per run, including waits/delays/lease queues. Lower takeover limits stop
new admissions, not cancel/forget prior ones. Existing node input returns Existing
and its persisted input/version; never re-encode or remap it. At a full ceiling CapacityDeferred
leaves Ready, commits neither input nor start intent, discards speculative
mapping and waits for capacity/ownership notification. AdmissionStopped is only
the cancellation/terminal barrier. Stale fences and version conflicts reload;
no mapper result grants independent admission authority.

An admitted append atomically records input presence/bytes/fingerprint, child
coordinate binding, StartAuthorized state, checkpoint/index and child-start
outbox. Present-null is exactly four UTF-8 bytes `null`, IsPresent true and
orcacore-json-v1; ordinary uppercase PayloadFingerprint hashes committed bytes
alone. Child start is a SECOND stream transaction, never cross-stream atomic.
After lost response retry verifies the full binding and reattaches that child.

The approved child-ID recipe is unchanged: lowercase SHA-256 of UTF-8
`orcacore-dag-child-v1\n<lowercase D run GUID>\n<invariant decimal ordinal>\n`.
Use its first 32 hex characters as Guid N text, replacing the last character by
`1` only for the all-zero result. Workflow definition/version/fingerprint,
declared types and input bytes are SEPARATE exact collision checks, never hash
inputs. No caller start-key reservation. ParentInstanceId and RootInstanceId are
the exact run GUID converted to the lineage anchor; that anchor is not a
workflow aggregate or child and ordinary workflow lookup must reject it.

ApplyChildTerminal reads the authoritative child terminal/output facts and full
binding, acquires the current run owner, appends idempotent node terminalization,
releases admission once, blocks descendants or readies dependants, and determines
run terminal state in that SAME run transaction. Notifications alone never free
capacity. Child Failed/TimedOut/Terminated becomes CHILD_FAILED/CHILD_TIMED_OUT/
CHILD_TERMINATED; unsolicited Cancelled becomes CHILD_CANCELLED. Absent materialized
child is not proof of absence after start authorization.

Cancellation acquires/retries the same fenced mutation lane. Its serialized
winner stops admission, cancels Pending/Ready, marks admitted nonterminal children
CancellationRequested and writes cancel intents; DependencyBlocked remains.
Already authorized children must materialize/reattach and receive cancellation,
not disappear from accounting. Running business work is governed by the child's
mutation lane. Success before cancellation returns AlreadyTerminal; cancellation
first eventually produces Cancelled only after all admitted children terminalize.
Without cancellation, all-success is Succeeded; any non-success is Failed after
all independent progress is exhausted. IntegrityFault is observable and admits
nothing, but is NOT proof that existing physical work stopped or a licence to
release uncertain slots/resources. Reconciliation requires authoritative child
facts and restores retryable protocol work, never arbitrary business state edits.

The six-call child protocol is subordinate to this fenced run lane, not a second
unfenced admission API. Its CommitMappedInputAsync implementation dispatches to
the current owning run coordinator; only that coordinator obtains the live fence,
version and trusted host limit and calls AdmitMappedInputAsync. It must not
substitute AdmissionStopped for CapacityDeferred. Consequently the former internal
DurableDagInputCommitResultV1 union gains exactly one internal variant
`internal sealed record CapacityDeferred : DurableDagInputCommitResultV1;`; its other
variants and all six method signatures remain unchanged. This explicit extension
supersedes only that closed outcome enumeration in the immutable child contract;
it is not a public API or codec/friend change. Retryable lease/version contention
is retried by the run coordinator rather than misreported as NotFound or failure.

## 5. Protocol and checkpoint framing

Aggregate discriminator `dag-run-v1`, record envelope version 1, codec
`orcacore-dag-record-v1`; no caller-selected codec. Eight exact kind names:
`dag-run-created-v1`, `dag-node-input-admitted-v1`, `dag-node-mapping-failed-v1`,
`dag-child-terminal-joined-v1`, `dag-run-cancellation-requested-v1`,
`dag-run-terminal-v1`, `dag-run-integrity-faulted-v1`, `dag-run-poison-reconciled-v1`.
Each frame has ONLY aggregateKind, version, kind, runId, operationId,
streamVersion, snapshot; each snapshot has ONLY runId, streamVersion, startKey, graph, runInput, state,
nodes, createdAt, updatedAt, completedAt. Nested value/node/graph fields exactly
mirror section 3's camel-case property names. Unknown/duplicate/extra/missing
fields, null required fields, unknown kinds/version/enum, noncanonical GUID or
timestamps, unequal run/operation/version, graph mutation and nonmonotonic
positions fail closed. Encode UTF-8 without BOM, fixed declared property order;
preserve embedded bytes exactly as base64, UTC round-trip timestamps and invariant
numeric ordinals/versions. Providers store exact frame bytes and lowercase SHA-256;
they never deserialize application values. No general polymorphic serializer.

The complete checkpoint content type is `application/vnd.orcacore.dag-run+json;v=1`.
Checkpoint version 1 payload is the exact snapshot frame with aggregateKind,
version, runId, streamVersion, snapshot, no workflow business state. Its lowercase
SHA-256 is over the exact payload bytes. Every accepted run mutation writes the
checkpoint at the SAME final stream version, so load checkpoint + validated tail
requires no genesis replay. Each run event contains a full immutable snapshot;
replay verifies old graph/input unchanged and permitted transition, not merely
replaces arbitrary state. Retention never deletes nonterminal run state, unresolved
admission/intents, join dedup or required operation receipts.

## 6. One outbox, four DAG kinds and durable notifications

The reserved four DAG kinds are dag-child-start-v1, dag-child-join-v1,
dag-child-cancel-v1 and dag-run-ready-v1. Continue-only and workflow-event-only
pumps remain unchanged. EVERY durable host installs the three-child-kind runtime
pump even without a dispatcher/DAG role. ONLY a host with AddOrcaCoreDag and a
ready sink claims ready records. IMessageDispatcher is the INTERNAL DISPATCH
PORT from PR-015, not a compatibility transport; it is still not the DAG runtime
pump. Both that port's pump and the external application event pump exclude all
reserved DAG kinds and reject wrong-lane results before invoking/acknowledging.
Wrong lane releases; retry/poison never fabricates successful progression or frees
an uncertain admission. All claims also check their trusted kind/owner after read.

Exact selectors: continuation Including("continue"); public events
Including("workflow-event"); DAG child Including(the three child kinds);
DAG ready Including("dag-run-ready-v1") AND RecipientHostId is null or this host;
IMessageDispatcher Excluding("continue", "workflow-event", all four DAG kinds),
intersected with any custom selector so a caller cannot widen it. No DAG role
means NO ready claim call, not claim/release churn. Disjoint partitions share the
existing logical IWorkflowOutboxStore, never another transport queue.

OutboxWrite adds `public Guid? DagRunOwner { get; init; }` and
`public Guid? RecipientHostId { get; init; }`; OutboxClaimRequest adds
`public Guid? RecipientHostId { get; init; }`. Workflow records have null DAG
owner/recipient. Every DAG record has a validated trusted owner coordinate outside
its opaque payload, so malformed payload can still record a visible owning fault.
Non-null recipient filter claims only null-or-equal recipient rows; null filter
never claims addressed rows. Only ready records may be addressed. Existing outbox
lease tokens/state compare-and-swap still govern acknowledgements.

Progression ready intents are unaddressed, durable hints with run/version. Observe
notices use the same ready kind, addressed to a random runtime boot HostId and
SubscriptionId. Subscribe atomically reads version and installs a lease-bounded
registration; AlreadyChanged needs no sleep. The run append atomically creates
one addressed notice per live subscriber whose AfterVersion was passed, plus
normal unaddressed readiness/capacity hints when progress is possible. Unique
(subscription, run, version) dedup prevents duplicate ownership. Sink delivers an
addressed notice to the local notification hub only; it never runs a mapper for
that notice. Expired subscriptions/undeliverable observer hints are maintenance
data, not lost run truth/admission. Subscriptions renew while the local wait lives;
local cancellation unregisters without cancelling run/children. No broker feature,
external dispatcher or database-specific notification API is assumed.

WaitForChange/Terminal subscribe THEN authoritatively recheck, attach the local
signal BEFORE claiming can deliver it, and recheck on every notice. This closes
registration/commit/delivery races. A missed wake is recoverable from the durable
subscription notice and startup scan; the bridge never polls terminal snapshots.
Run-owner heartbeat/renewal is operational lease maintenance, not terminal polling.
Startup discovery and bounded retry timers do not re-invoke already committed
mapping. Transport acknowledgement occurs only after the owning transition/hub
wake has completed; lost ack replays committed state. A corrupt trusted owner
quarantines the row visibly, never Dispatched success.

## 7. First-create schemas and transactions

Proposed additive FIRST-CREATE shape in both providers; no ALTER/upgrade DDL.
Existing workflow tables and key encoding remain unchanged. New complete tables:

| Table | Required columns / constraints |
|---|---|
| dag_runs | run_id PK; start_key_digest unique, start_key_text exact nonblank; creation_sequence unique generated; version bigint nonnegative; definition_id, definition_version positive, definition_fingerprint; state checked closed enum; graph_payload, input_payload, checkpoint_payload, checkpoint_sha256; created_at, updated_at, completed_at; owner_id nullable, owner_fence bigint nonnegative, lease_expires_at nullable; owner/expiry both null or both present, fence retained across release |
| dag_run_events | (run_id, stream_version) PK/FK; operation_id; record_kind/version; payload/checksum; created_at; exact kind check and positive versions |
| dag_run_operations | (run_id, operation_id) PK/FK; fingerprint; result_version; immutable receipt; exact replay/conflict serialization |
| dag_run_nodes | (run_id, ordinal) PK/FK; unique(run_id,node_id); binding_payload, dependencies_payload; state; child_id nullable (unique only when present); input_present, input_bytes, input_contract, input_codec, input_fingerprint; output_present, output_bytes, output_contract, output_codec, output_fingerprint; failure_code; ready_at, started_at, completed_at; nonnegative ordinal; parked/start-authorized remain admission-counted |
| dag_run_subscriptions | subscription_id PK; recipient_host_id; run_id FK; after_version; lease_expires_at; unique recipient/subscription; expiry index |

New columns in the first-create shared outbox definition: nullable dag_run_owner,
nullable recipient_host_id, plus indexes (kind,state,retry_at,lease_expires_at,
recipient_host_id) and (dag_run_owner,kind,state). New DAG tables index
(state,creation_sequence), (state,lease_expires_at,creation_sequence),
(run_id,state,ordinal) and subscription (run_id,after_version,lease_expires_at).
PostgreSQL uses uuid, bigint generated always as identity, bytea, text COLLATE C,
timestamptz. SQL Server uses uniqueidentifier, bigint IDENTITY, varbinary(max),
nvarchar fields with existing exact BIN2 collation, datetimeoffset(7). Exact
The new tables use the SAME snake_case names in both providers; migration
implementation freezes canonical script digests in 2.1.
All fingerprints/protocol/contract keys use exact ordinal comparison; provider
default collation must never change equality. An index projection is NOT
authoritative apart from the transaction that wrote it.

Exact new-column types (all NOT NULL unless marked nullable): IDs are uuid /
uniqueidentifier; versions, fences, sequence and definition_version are bigint;
ordinals and record_version are integer / int; state and record_kind are text
COLLATE C / nvarchar(64) BIN2 with closed-enumeration CHECKs. definition_id and
node_id are text COLLATE C / nvarchar(450) BIN2; implementation must reject an
unrepresentable ID before effects, consistently on all providers. Payloads and
bytes are bytea / varbinary(max). Contract/codec/failure strings are text COLLATE C /
nvarchar(max) BIN2; fingerprints/checksums/digests are char(64) with exact binary
comparison and checked hex casing. Presence is boolean / bit; times are timestamptz /
datetimeoffset(7). completed_at, ready_at, started_at, failure_code, child_id,
owner_id and lease_expires_at are nullable. Absent input/output requires all its
bytes/contract/codec/fingerprint fields null; present-null requires four actual
bytes and all metadata present. Input/output presence itself is never nullable.
Graph/binding/dependencies payloads are the exact validated version-1 frame fragments.
The events table's record_version is 1; payload/checksum are required; operations
store operation_fingerprint, result_version and receipt_payload with immutable
unique (run_id, operation_id). Every DAG FK names dag_runs(run_id), never workflow
instances; live rows are not cascade-deleted. SQL Server uses filtered unique
child_id WHERE child_id IS NOT NULL; PostgreSQL has the equivalent partial index.

start_key_text is text / nvarchar(max); start_key_digest is unique char(64)
lowercase SHA-256 over strict UTF-8 caller text without trimming/normalization.
Reject invalid UTF-8/NUL before creation. Lookup ALWAYS compares the stored exact
key as well; a different key with the same digest is DAG_INTEGRITY_CONFLICT, never
Existing. This avoids imposing a silently provider-specific SQL index-length cap.
definition_id/node_id have a shared maximum of 450 UTF-16 code units for these
advanced DAG ports only; invalid/oversized contracts fail DAG_PROTOCOL_INVALID
before creation, never truncate. This is an explicitly proposed persistence-boundary
validation, not a change to the generic authoring strong-value factory.

Lock run/owner row before CAS; one append transaction validates fence/version,
records operation receipt/events/checkpoint/nodes/indexes/outbox and all subscriber
notifications. Acquire/renew/release lock that same row. SQL Server uses serializable
or UPDLOCK/HOLDLOCK ownership; PostgreSQL row FOR UPDATE with unique insert/conflict
serialization. No runtime-ready admission is decided by an unfenced outside count.
Run/child transactions remain separate. First-create migrations include all new
tables/columns/indexes and journal content digests. Existing databases are not
silently upgraded; compatibility ALTER TABLE remains prohibited by the approved
v1 greenfield policy. InMemory gives the same atomic semantics in one store lock,
but never claims process-restart durability.

## 8. Certification and source exits

All rows apply to InMemory, PostgreSQL and SQL Server; restart/competing-host
claims are exercised with REAL production storage, not mocks:

| Row | Required positive / negative evidence |
|---|---|
| DG-P01 | Workflow stream text/21 codec entries unchanged; distinct DAG kind; workflow-only lookup and mixed append reject |
| DG-P02 | Create/reopen exact binding; changed graph/input/definition conflict; no caller start-key collision |
| DG-P03 | Full checkpoint+tail; unknown frame/version/field/checksum fail; nonterminal/poison retained |
| DG-P04 | Every append crash point commits all/none including subscriptions/outbox; operation replay stable/divergent conflict |
| DG-P05 | Competing owners, lease expiry/takeover/renew; stale fence/version append leaves zero effects |
| DG-P06 | All child/start-intent admissions survive takeover; lower current host ceiling; CapacityDeferred vs AdmissionStopped |
| DG-P07 | Four kind partitions, wrong-lane provider result, internal sink Success, no dispatcher, role-free no-ready claim |
| DG-P08 | Eager unused dependency decode -> DAG_INPUT_MAPPING_INVALID; null bytes/presence; mapper/local cancellation distinction |
| DG-P09 | Crash before/after start intent, child append, join, ack; exact deterministic recipe and separate binding conflicts |
| DG-P10 | Atomic failure/blocking closure, independent branches, once-only slot release and stable authored ordering |
| DG-P11 | Cancellation/start winner, reordering, admitted-child materialization, quarantine/physical-stop distinction |
| DG-P12 | Subscribe/recheck race, addressed host partition, lost ack, host death, restart discovery, zero terminal polling |
| DG-P13 | Exact API/baseline additions, shipped Hosting behavior and full type/member/reflection/UnsafeAccessor/expression bans |

Reshape 8.4–8.10 retain existing AC/DR-AC/JS exits. No waiver or intentional red
changes here. A contract checkbox never supplies compiled acceptance credit.

## 9. Review and retention rules

Proposal, design, tasks and this artifact are whole-file source-pinned after
contract approval preparation; future reviewed edits update named constants in
the SAME target, never routine metadata refresh. Process provenance remains
byte-exact in the permanent superseded catalog. All older contract/request/
manifest/verdict bytes are unchanged. Atomic 1.3/1.4 supersession affects only
the four hash-matched predecessors and preserves history. Withdrawal requires
one reviewed target removing all four registry rows AND the change directory,
with immutable disposition/evidence preserved; archival resolves exactly one
active or dated archived owner, failing duplicates. No silent directory deletion.

The [exact API catalog](task-1-1-exact-api-additions-2026-10-04.md) gives before/after
declarations and compiler-extracted baseline additions. Only an isolated signature
proof is built: main source/baselines stay untouched. The separate frozen request
records validation and activation rehearsal; this artifact invents neither.

## 10. Exact document disposition

 - `docs/specs/03-domain-model-and-glossary.md`: §3.3: distinct typed dag-run-v1 aggregate; never a fake workflow InstanceId. Runtime.Protocol owns records/codec and Provider.Abstractions owns operational ports; the current nine friends/graph do not change.
 - `docs/specs/04-requirements-core-runtime.md`: CR-040/043: existing workflow serialization stays unchanged. The distinct DAG aggregate serializes through a current provider fence and expected version; child/run commits are separate, not cross-stream atomic.
 - `docs/specs/05-requirements-events-waits-timers.md`: EV-060: public workflow-event dispatch stays unchanged. Four reserved DAG kinds belong to internal runtime pumps, never public event delivery or application transports.
 - `docs/specs/06-requirements-durable-execution.md`: DU-010/011/031/032/033/070: immutable run graph/input, admission/outcome/cancellation facts, checkpoint/index and related notices append atomically through IWorkflowEventStore.AppendAsync(ProviderCommitBatch). One outbox owns all kinds; host-local dictionaries never acknowledge durable authority.
 - `docs/specs/08-requirements-composition.md`: CP-022/023/024/025/028: eager ordinary declared-type decode is DAG_INPUT_MAPPING_INVALID, including unused outputs. Only run GUID/ordinal hash child identity; workflow binding checks separately. The current fenced owner reapplies its host-owned per-run limit, retaining StartAuthorized/parked admissions. CapacityDeferred is distinct from AdmissionStopped; no creation-time stored ceiling. Persisted admission facts do not themselves grant a host ownership lease.
 - `docs/specs/10-provider-model-and-extensibility.md`: PR-010/012/015/020/021/024: explicit typed DAG branch and run lease/discovery/subscription operations on the real append port; IMessageDispatcher remains the internal dispatch port and excludes continue, workflow-event and all four DAG kinds. Every durable host installs the child pump; real storage certifies whole-run atomicity and complete first-create schemas, never compatibility ALTER or cross-stream atomic writes.
 - `docs/specs/12-acceptance-criteria.md`: AC-608–618: retain all Section-8 compiled exits. DG-P01–13 adds real-storage crash/fence/claim/null/notification tests, not acceptance credit or a waiver/expected-red change.
 - `docs/specs/16-requirements-durable-driver.md`: DR-037/041 and DR-AC-029: continue-only, workflow-event-only, every-host three-child-kind and DAG-role-only ready partitions. Ready claims have null-or-exact boot-host recipient; role-free hosts never claim ready. Internal IMessageDispatcher excludes all six reserved kinds; wrong-lane release/poison never frees uncertain admission. Startup discovery recovers runs; terminal waits subscribe/recheck, not poll.
 - `docs/specs/17-selected-mode-capability-matrix.md`: §17.2.6: the six child calls become subordinate to the fenced run coordinator; its closed input-commit result adds CapacityDeferred. Complete creation/caller-key binding, graph/input persistence, recovery/admission/failure closure, child joins, cancellation/poison and notifications are defined. Public application signatures and OutputOf/null/mapper policy stay unchanged; exact advanced API additions are catalogued, with no new friend or codec access in Dag/Hosting.
 - `docs/implementation/00-stack-decisions.md`: Dated proposed decision: full typed DAG stream/provider lifecycle precedes bridge source. The current nine friends/package graph stay unchanged. The earlier six-call child contract remains immutable history, never re-described as complete.
 - `docs/implementation/01-solution-architecture.md`: Runtime.Protocol owns DAG records/codec; Provider.Abstractions owns explicit run ports; Durable.Hosting owns fenced lifecycle/codec/ready-sink interface; Dag.Hosting implements mapping/sink. No reverse package edge or tenth friend.
 - `docs/implementation/developer-facing-interface-refactor-phased-plan-2026-07-14.md`: Section-8: process b55056c -> e99f338 -> dab498f is landed. Contract 1.1/1.2 review precedes atomic 1.3/1.4 approval, then provider/source slices. Reshape 8.4/8.5 stay open.
 - `docs/implementation/README.md`: Section-10 successor prepares a complete contract, not a bridge implementation. Process approval is landed; contract, canonical sync and source remain separate gates.
 - `docs/project-technical-overview.md`: Propose first-class run persistence/fenced admission with explicitly catalogued advanced ports; existing workflow behavior, application signatures and nine friends stay unchanged.
 - `docs/durable-driver-lane-host.md`: Propose a role-independent child runtime pump and DAG-role-only readiness. Internal IMessageDispatcher remains disjoint; role-free hosts never claim ready; terminal waits subscribe/recheck.
 - `CLAUDE.md`: Current nine friends/codec boundaries stay unchanged. The complete DAG run contract is proposed only: approval and atomic 1.3/1.4 checkpoint precede source. Activation edits only its named checkbox/state, never documentation.

Explicit exclusions: doc 11 NF-002 adds no nonfunctional or package obligation;
doc 17 C# companion adds no application-authoring signature. Its immutable
companion stays unchanged; advanced API is the separate exact additive catalog.
The existing fixed-codec, notification-wait and authoring requirements stay
unchanged. These changes fit ONLY the four registered owners. Internal
StartAuthorized projects as public Running; IntegrityFault stays a nonterminal
admission-stopped runtime fault until authoritative reconciliation, not a new
public enum value or false terminal result. Task 8.6 freezes that projection.
