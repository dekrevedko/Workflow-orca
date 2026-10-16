# Proposed durable DAG run contract: RC-2 evaluation and accounting remediation

Date: 2026-10-05. Status: proposed RC-2 remediation; NOT contract approval, canonical sync or source authority.
The earlier Task 1.1 contract, signatures, API catalog, request, manifest and REJECT
are preserved byte-exact, as is the rejected RC-1 packet. This successor retains the resolved FIFO protocol and fixes RC-2 plus all six round-96 notes.
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
public enum DagNodeStateV1 { Pending, Ready, StartAuthorized, CancellationRequested, Succeeded, Failed, Cancelled, DependencyBlocked }
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
public sealed record DagRunNodeChangeV1(
    int Ordinal, DagNodeStateV1 State, InstanceId? ChildInstanceId,
    DagRunValueV1? InputWrite, DagRunValueV1? OutputWrite,
    string? FailureCode, DateTimeOffset? ReadyAt,
    DateTimeOffset? StartedAt, DateTimeOffset? CompletedAt);
public sealed record DagRunRecordV1(
    int Version, DagRunRecordKindV1 Kind, Guid RunId, Guid OperationId,
    StreamVersion StreamVersion, DagRunStateV1 State,
    DagRunSnapshotV1? CreationSnapshot,
    IReadOnlyList<DagRunNodeChangeV1> ChangedNodes,
    DateTimeOffset UpdatedAt, DateTimeOffset? CompletedAt);
public sealed record DagRunCheckpointV1(
    int Version, Guid RunId, StreamVersion StreamVersion,
    string ContentType, byte[] Payload, string Sha256);
public sealed record DagRunFenceV1(Guid RunId, Guid OwnerId, long Token, Guid RequestId);
public sealed record DagRunAppendV1(
    Guid OperationId, string OperationFingerprint,
    DagRunFenceV1? Fence, IReadOnlyList<DagRunRecordV1> Records,
    DagRunCheckpointV1 Checkpoint);
public enum DagRunLeasePurposeV1 { Admission, ChildJoin, RunCancellation, IntegrityReconciliation }
public sealed record DagRunLeaseRequestV1(
    Guid RunId, Guid OwnerId, Guid RequestId, DagRunLeasePurposeV1 Purpose,
    long? ExpectedFence, TimeSpan LeaseDuration);
public enum DagRunLeaseOutcomeV1 { Granted, Queued, OwnerBusy, StaleFence, NotFound, Terminal }
public sealed record DagRunLeaseResultV1(
    DagRunLeaseOutcomeV1 Outcome, DagRunFenceV1? Fence,
    DateTimeOffset? ExpiresAt, DateTimeOffset? HardDeadline,
    long? QueueSequence, StreamVersion? CurrentVersion);
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
Task CancelDagRunLeaseRequestAsync(
    Guid runId, Guid ownerId, Guid requestId, CancellationToken cancellationToken);
Task<IReadOnlyList<DagRunDiscoveryV1>> ListRecoverableDagRunsAsync(
    DagRunDiscoveryRequestV1 request, CancellationToken cancellationToken);
Task<DagRunSubscribeResultV1> SubscribeDagRunAsync(
    DagRunSubscriptionV1 subscription, CancellationToken cancellationToken);
Task UnsubscribeDagRunAsync(Guid subscriptionId, Guid recipientHostId,
    CancellationToken cancellationToken);
Task<int> PruneExpiredDagRunObserversAsync(
    int maxCount, CancellationToken cancellationToken);
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

### 3.1 FIFO, bounded run-turn lease protocol (RC-1)

Provider.Abstractions exposes the operational port; ONLY Durable.Hosting calls it.
Dag.Hosting MUST NOT reference or call IWorkflowEventStore for any lease/storage
operation. Its bridge-level BeginTurn/RenewTurn/EndTurn/AbandonTurnRequest below
are the sole path to a fence. One provider-owned transaction-clock sample, captured AFTER ownership locks, decides all DAG lease/subscription/cleanup times; section 3.2 defines production and controlled-test implementations.

Acquire enqueues one durable waiter identified by (run, boot OwnerId, RequestId,
Purpose), with a monotonically increasing QueueSequence. Retry of that exact
request preserves its sequence and refreshes its 30-second waiting heartbeat;
reuse with a different owner/purpose fails DAG_INTEGRITY_CONFLICT. Missing/terminal
returns NotFound/Terminal without a waiter. A waiting host retries acquisition
with a bounded 100–1000 ms operational backoff, not terminal-state polling. Hosts
refresh waiting heartbeats at least every 5 seconds. CancelDagRunLeaseRequest
removes only that owner/request's ungranted waiter; a granted request must Release.

The provider grants ONLY the lowest live QueueSequence after cleaning expired
waiters and release/expiry of the previous turn. No same-owner fast path may
bypass a queued predecessor. One owner can have at most ONE live request per run;
a second DISTINCT request from that owner while Waiting/Granted returns OwnerBusy, no new waiter or effects. Exact same-request retry is never OwnerBusy. New admission work cannot reserve an unbounded provider queue or reuse a completed request. The shared local broker protocol below defines ordering before this port.
Lost acquire responses return the SAME live fence/request and deadlines. Released
or expired request IDs remain consumed until run retention; they cannot resurrect
an old queue position or acquire a new token. A retry returns StaleFence. A fresh
turn uses a fresh RequestId at the queue tail. Grant increments the positive token
and records granted_at plus an ABSOLUTE HardDeadline = granted_at + 15 seconds.

Every grant has LeaseDuration > 0 and <= 5 seconds, ExpiresAt <= HardDeadline.
Renew requires ExpectedFence and exact run/owner/token/request, still-live expiry
and now < HardDeadline; it sets expiry to min(now + LeaseDuration, HardDeadline).
It NEVER resets granted_at/HardDeadline. Queued uses null fence/deadlines and its
stable sequence; Granted returns the full fence, version, expiry, hard deadline
and sequence. Expired/stale renewal returns StaleFence and has no effects.
Release is idempotent for the just-ended exact request, validates no newer owner
can be affected, clears owner/expiry/deadline, retains the monotonically increasing
fence, consumes the request and wakes the next waiter. It NEVER releases node
admission or terminalizes a run. Old-owner release cannot revoke a newer turn.

Scope is ONE mutation turn, not run lifetime: at most one business AppendAsync
(which may atomically contain several records), then release in finally. Release
before capacity waits, child waits, subscriber waits, outbox ack and any idle state.
Heartbeat is only for short provider I/O, normally every 2 seconds; NEVER for eager
decode, application mapping, codec normalization or waiting for physical work.
A watchdog releases a locally stalled I/O turn at its hard deadline; every late
append is fenced out. It never abandons/restarts a mapper because no mapper owns
this turn. Lease expiry is not a mapping failure. A completed evaluation is retained
outside turns and may be committed under a fresh turn after authoritative recheck.

For healthy provider/pump scheduling, a request with K live predecessors receives
its turn after at most (K+1)*15 seconds plus the sum of bounded acquisition/service
delays; new arrivals never increase K. Each dead waiting predecessor expires
within 30 seconds of its last heartbeat, so the more general bound uses 30 seconds
per dead predecessor. No finite wall-clock guarantee is made during provider outage
or process starvation. Certification uses controlled provider time, not sleeps.
Authoritative child joins from ANY durable host and cancellation requests from a DAG-role host enter this SAME FIFO; admission renewals,
even on a host with different options, cannot overtake them or extend a turn past
15 seconds. A caller cancellation of the API removes its waiting request (or releases
its granted turn) and does not itself request run cancellation. Once a cancellation
turn commits its barrier, later admission turns return AdmissionStopped.

The grant's request row also records append_committed, initially false. In the
SAME append transaction the provider validates its purpose against the operation
(Admission commits prepared input/mapping failure; ChildJoin joins; RunCancellation requests cancellation;
IntegrityReconciliation faults/reconciles, each with any derived terminal record)
and sets append_committed=true. A second different operation under that turn fails
DAG_TURN_ALREADY_COMMITTED with no effects; exact OperationId replay still returns
its immutable receipt. FIFO fairness therefore cannot be undermined by an owner
performing an unbounded series of appends before release. Queue grant/release
notifications are operational wakeups, not aggregate business events.

For non-creation append, provider checks exact stream version, matching live
owner/run/token/request and unexpired lease AND hard deadline in the same transaction. Creation requires
ExpectedVersion zero, null fence, absent aggregate and matching creation binding.
Exact OperationId + OperationFingerprint replay returns the original version
without effects, even after takeover; divergent reuse is an integrity conflict.
Version conflict, stale fence, invalid aggregate/frame and divergent operation
produce Result failure codes DAG_VERSION_CONFLICT, DAG_STALE_FENCE,
DAG_PROTOCOL_INVALID and DAG_INTEGRITY_CONFLICT respectively. A second operation
in a completed turn returns DAG_TURN_ALREADY_COMMITTED; an incompatible lease
purpose is DAG_PROTOCOL_INVALID. No partial append,
admission, checkpoint, index, subscription-notification or outbox effect occurs.
OperationFingerprint is lowercase SHA-256 of the exact normalized protocol frame
excluding provider-assigned stream version, fence/lease and observation times;
it includes full immutable graph/binding/value bytes and intended transition.

### 3.2 Transaction clock and same-host broker (P3-2/P3-3)

Production PostgreSQL reads clock_timestamp() and SQL Server reads SYSUTCDATETIME()
inside the locked DAG transaction; InMemory uses its store TimeProvider. All expiry
comparisons/writes in one transaction use that one captured UTC sample. The clock
is monotonic per stored run: clamp a backward sample to the last operational sample;
persist that sample with ownership maintenance, not as a business event. Existing
workflow time ports/record bytes stay unchanged. No request supplies a trusted time.

Each provider owns an INTERNAL instance-scoped DagTransactionClock override, with
the exact signature Func<DateTimeOffset>? TestUtcNow; null selects the production
clock. It is reachable ONLY from its existing owning unit-test friend, never DI
options/public API/configuration, Dag.Hosting, or application code. Runtime reads
one sample through the same locked transaction path in both modes. Certification
factories in the owning provider tests install one thread-safe manually advanced
UTC source shared by all store instances/hosts for that isolated database; shared
certification receives advance/read actions from that owning factory. Production
schemas and actual SQL append/lease/claim/cleanup paths still execute. No global
override, extra friend or mock store is introduced. DG-P05/14–19/21 advance this
source only after explicit barriers confirm the observed transition; drain pump
work to a signal rather than sleeping. Production-clock smoke tests verify DB
UTC selection and bounded skew; virtual-time fairness is not a wall-clock claim.

ONE Durable.Hosting boot-local broker per run serializes Admission, ChildJoin,
RunCancellation and IntegrityReconciliation requests in arrival FIFO. It assigns
a monotonically increasing LocalSequence at first request registration, coalesces
exact RequestId retries, removes cancelled ungranted callers and submits ONLY its
head to the provider FIFO. Same host Dag.Hosting and role-free child pumps share
this broker, not separate locks. Completion/expiry/release dequeues the head before
submitting the next. No cancellation/admission priority may jump an older join.
Queued returns null provider QueueSequence before submission plus its LocalSequence;
Granted returns both sequences. OwnerBusy from an unexpected provider-active request
is visible and retried without claiming a new sequence; it cannot silently reuse
another operation's fence. Local queue capacity is 1024 per run/boot host including
the head; excess requests return LocalQueueFull without allocation, retry with
bounded backoff, and never masquerade as AdmissionStopped/MappingInvalid. Workers
submit one work occurrence at a time and coalesce duplicate ready hints. Provider
FIFO fairness begins at successful queue admission, not before a rejected full queue.

With L earlier local requests and K earlier live provider requests encountered by
each of the L+1 submissions, the healthy-service bound sums each corresponding
(K+1)*15s grant bound plus at most 30s per dead provider waiter and bounded broker/
acquisition/service delays. Later local/provider arrivals never enlarge those
predecessor sets. No finite bound is promised during outage/process starvation or
before retry after LocalQueueFull. Arbitrary mapping is outside both queues and
therefore contributes no mapper-duration term. DG-P21 certifies mixed local/remote
purposes, OwnerBusy, retry coalescing, queue-full/cancel and predecessor order.

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
internal enum DurableDagTurnPurposeV1 { Admission, ChildJoin, RunCancellation, IntegrityReconciliation }
internal sealed record DurableDagTurnRequestV1(
    Guid RunId, Guid RequestId, DurableDagTurnPurposeV1 Purpose);
internal sealed record DurableDagTurnV1(
    DagRunFenceV1 Fence, StreamVersion Version,
    DateTimeOffset ExpiresAt, DateTimeOffset HardDeadline);
internal enum DurableDagTurnOutcomeV1 { Granted, Queued, OwnerBusy, LocalQueueFull, StaleFence, NotFound, Terminal }
internal sealed record DurableDagTurnResultV1(
    DurableDagTurnOutcomeV1 Outcome, DurableDagTurnV1? Turn, long? QueueSequence, long? LocalSequence);
internal sealed record DurableDagPreparedMappingV1(
    DurableDagNodeContractV1 Node, string ImmutableInputsFingerprint,
    DurableDagMappedInputV1? SourceInput, DagRunValueV1? NormalizedInput,
    bool IsMappingInvalid);
internal sealed record DurableDagReadyNoticeV1(
    Guid RunId, StreamVersion Version, Guid? SubscriptionId);
internal interface IDurableDagReadySinkV1
{
    ValueTask HandleAsync(DurableDagReadyNoticeV1 notice, CancellationToken cancellationToken);
}
internal interface IDurableDagRunBridgeV1
{
    ValueTask<DurableDagTurnResultV1> BeginTurnAsync(
        DurableDagTurnRequestV1 request, CancellationToken cancellationToken);
    ValueTask<DurableDagTurnResultV1> RenewTurnAsync(
        DurableDagTurnV1 turn, CancellationToken cancellationToken);
    ValueTask EndTurnAsync(DurableDagTurnV1 turn, CancellationToken cancellationToken);
    ValueTask AbandonTurnRequestAsync(
        DurableDagTurnRequestV1 request, CancellationToken cancellationToken);
    IDurableDagChildBridgeV1 OpenChildBridgeForEvaluation();
    ValueTask<DurableDagPreparedMappingV1> PrepareMappingAsync(
        DurableDagNodeContractV1 node, DurableDagMappedInputV1? input,
        bool mappingInvalid, CancellationToken cancellationToken);
    IDurableDagChildBridgeV1 OpenChildBridgeForTurn(
        DurableDagTurnV1 turn, DurableDagPreparedMappingV1 prepared,
        int maxConcurrentNodes);
    ValueTask<DurableDagCreateResultV1> CreateOrReopenAsync(
        DurableDagRunCreateV1 run, CancellationToken cancellationToken);
    ValueTask<DagRunSnapshotV1?> ReadAsync(Guid runId, CancellationToken cancellationToken);
    ValueTask<DurableDagMutationResultV1> AdmitPreparedMappingAsync(
        DurableDagPreparedMappingV1 prepared,
        DagRunFenceV1 fence, StreamVersion expectedVersion, int maxConcurrentNodes,
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

Coordinator ownership is explicit. Dag.Hosting owns the local admission/mapping
coordinator, which obtains a turn ONLY through IDurableDagRunBridgeV1. Durable.Hosting
owns the run lease broker, the exact provider calls, all mutations and the
role-independent join/cancellation coordinator. The provider FIFO is the single
serialization arbiter across both packages/ALL hosts. No durable service calls
into a remote Dag.Hosting coordinator; ready sink callbacks are hints only.
Each local admission turn uses that Dag.Hosting instance's validated DagHostOptions.
Evaluation owns NO fence and submits NO local/provider turn request. The coordinator
uses OpenChildBridgeForEvaluation().ReadMappingInputsAsync to read one authoritative
committed immutable run-input/dependency snapshot and eagerly decode every successful
declared resultful output, including unused ones. That adapter permits read-only
mapping input loading ONLY; all other six-call methods throw InvalidOperationException.
The callback's exact plan/node checks remain the reviewed runtime-view contract.
Ordinary declared-type decode, unsupported input or mapper failure is
DAG_INPUT_MAPPING_INVALID; storage/protocol/checksum/lease failures retain runtime
classifications. Mapper-thrown cancellation remains mapping-invalid; local call
cancellation outside evaluation cancels observation, not the run. OOM/stack-overflow/
access-violation remain process exclusions, no new exception/failure budget is added.

RC-2: eager decode, mapper invocation and fixed-codec normalization ALL run before
BeginTurnAsync. Ready implies successful dependency outputs and immutable run input,
so unrelated run transitions cannot invalidate those bytes. At most ONE evaluation
per (run,node ordinal) is physically in flight per boot host, coalescing ready hints.
There are at most that host's validated MaxConcurrentNodes physical evaluation
workers globally; a token-ignoring evaluation retains its slot until it returns,
even after run/caller cancellation. No timeout launches a replacement. Host death
loses speculative work; another host may evaluate independently, without an
exactly-once mapper-invocation promise. Eligible ties use authored ordinal. A slow
but terminating mapper has NO runtime duration budget and can commit normally.
An actually nonterminating application mapper has no completion guarantee, but
cannot hold a fence, multiply its same-host workers or block durable cancellation.

PrepareMappingAsync runs on Durable.Hosting outside a turn: it validates exact node/
plan binding and immutable input/output bytes, normalizes the returned declared input
through the existing fixed codec and returns a defensively copied prepared receipt.
It is MappingInvalid if requested after the evaluator/decode failure or normalization
fails; storage/protocol errors throw their runtime classifications. It stores an
ImmutableInputsFingerprint over length-prefixed exact graph/node binding, run-input
bytes and ordered direct dependency terminal-output bytes/presence (not mutable run
version/status). These preparation receipts are internal host-local caches, not
durable authority. Dag.Hosting may read but MUST NOT construct or modify them; the
metadata allowlist excludes constructors/setters. The broker validates receipt
provenance and rechecks exact immutable digest BEFORE queueing and at serialized
append; copied bytes/digest prevent alias mutation. Actual persisted input/fingerprint
is the normalized receipt bytes, never freshly encoded inside the turn.

After preparation, acquire a short Admission turn, recheck current run/graph/node
binding, dependency facts, readiness, existing input, cancellation/terminal barrier,
capacity and version, then AdmitPreparedMappingAsync appends successful input OR
DAG_INPUT_MAPPING_INVALID with transitive DependencyBlocked closure. The provider
receives exact bytes/digest, no delegate or CLR business value. Independent branches
remain eligible. A stale turn/version conflict renews or reacquires under the same
FIFO and REUSES the prepared receipt if immutable binding is equal; no rerun because
of elapsed lease time/unrelated version changes. CapacityDeferred retains the
completed receipt and waits outside any turn; committed input discards it/reuses
durable bytes. Cancellation/terminal/nonready/binding mismatch discards it without
admitting; authoritative integrity conflict is not a license to remap changed inputs.
Retain at most one completed receipt per ready node/host, evict on authoritative
resolution; host death loses it and can cause a precommit reevaluation.

AdmitPreparedMapping validates expected version/fence/exact binding/readiness and the
CURRENT owner's validated host limit. The integer is obtained from the local granted Dag.Hosting coordinator's options, never an application or remote caller. A newly granted admission turn
is a capacity-owner takeover; different hosts may therefore apply different
ceilings in SUCCESSIVE turns, but never concurrently. No long-lived capacity
owner/creation-time ceiling is asserted by Decision 5. There is no creation-time
persisted ceiling. Started nonterminal children AND start-authorized intents
count per run, including waits/delays/lease queues. Lower takeover limits stop
new admissions, not cancel/forget prior ones. Existing node input returns Existing
and its persisted input/version; never re-encode or remap it. At a full ceiling CapacityDeferred
leaves Ready, commits neither input nor start intent, retains the completed speculative
receipt and waits outside any turn for capacity/ownership notification. AdmissionStopped is only
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
binding, obtains a ChildJoin FIFO turn through Durable.Hosting, appends idempotent node terminalization,
releases admission once, blocks descendants or readies dependants, and determines
run terminal state in that SAME run transaction. Notifications alone never free
capacity. Child Failed/TimedOut/Terminated becomes CHILD_FAILED/CHILD_TIMED_OUT/
CHILD_TERMINATED; unsolicited Cancelled becomes CHILD_CANCELLED. Absent materialized
child is not proof of absence after start authorization.

The public trigger is DagRunHandle.RequestCancellationAsync on a DAG-role host (or another DAG-role host reopening that run). It obtains a RunCancellation FIFO turn through Durable.Hosting. A role-free host has no public DAG handle and is NOT claimed to initiate cancellation; every durable host still consumes committed child-cancel work and progresses joins. Its serialized
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

The six-call child protocol is subordinate, not a second admission lane.
Evaluation uses its read-only adapter BEFORE BeginTurn. The commit adapter is
created by OpenChildBridgeForTurn(turn, prepared, maxConcurrentNodes): the single
limit source is that explicit positive argument from local validated DagHostOptions,
captured immutably along with receipt/fence/request/version/deadline. No hidden
registration limit or remote options callback exists. The direct AdmitPreparedMapping
call uses the SAME value and implementation. Existing six child signatures remain
unchanged; CommitMappedInputAsync verifies the exact original SourceInput reference,
node coordinates and bound prepared bytes, performs no decode/mapper/normalization,
and delegates the append. The commit adapter rejects ReadMappingInputsAsync so
evaluation cannot accidentally occur while fenced. Child start/observe/cancel calls
use their existing protocol after releasing the run turn, not a held lease.
A stale adapter never silently reacquires/borrows a newer turn. Reacquisition is an
explicit fresh OpenChildBridgeForTurn with the same valid receipt and fresh fence.

### 4.1 Exact metadata seam / no provider bypass

The source review freezes the concrete member-reference keys for ALL declarations
in this section, the unchanged child block plus CapacityDeferred, and the existing
registration seam. This section's five turn methods, read-only evaluation adapter, preparation method/receipt and five turn contract types
MUST be in the closed Hosting metadata allowlist, not an inferred wildcard.
Dag.Hosting may construct/read ONLY those exact internal contracts and call the
listed bridge members. Calls to IWorkflowEventStore (including its public lease
methods), provider implementations, reflection, UnsafeAccessor or expression
compilation are forbidden from Dag.Hosting; a public provider port does not
create a second legal bridge. Permanent source controls must prove Begin/Renew/
End/Abandon and the captured-turn child adapter cannot be bypassed. Durable.Hosting
itself needs no Dag package reference to acquire/join/cancel. Its ready sink never
carries an admission request or a host's options; those are local coordinator state.

## 5. Protocol and checkpoint framing

Aggregate discriminator `dag-run-v1`, record envelope version 1, codec
`orcacore-dag-record-v1`; no caller-selected codec. Eight exact kind names (no persisted Running transition):
`dag-run-created-v1`, `dag-node-input-admitted-v1`, `dag-node-mapping-failed-v1`,
`dag-child-terminal-joined-v1`, `dag-run-cancellation-requested-v1`,
`dag-run-terminal-v1`, `dag-run-integrity-faulted-v1`, `dag-run-poison-reconciled-v1`.
Each frame has ONLY aggregateKind, version, kind, runId, operationId,
streamVersion, state, creationSnapshot, changedNodes, updatedAt, completedAt.
Created alone carries a required CreationSnapshot (the section-3 full snapshot),
empty ChangedNodes, version 1 and the exact immutable graph/input. Other kinds
have null CreationSnapshot and an ordinal-sorted unique ChangedNodes list. A
node change has ONLY ordinal, state, childInstanceId, inputWrite, outputWrite,
failureCode, readyAt, startedAt, completedAt. Null inputWrite/outputWrite means
RETAIN old bytes/presence; a non-null write must be IsPresent=true and is allowed
only at its first input admission or successful child join. Present-null bytes
are a non-null DagRunValueV1 containing four bytes null, never an absent write.
State transitions are exact: Created sets roots Ready and others Pending; InputAdmitted
moves Ready -> StartAuthorized and sets StartedAt to that logical admission commit
time (NOT physical child startup); MappingFailed moves Ready -> Failed and reachable
Pending/Ready descendants -> DependencyBlocked; ChildJoined moves StartAuthorized/
CancellationRequested -> Succeeded/Failed/Cancelled and eligible Pending dependants
-> Ready; CancellationRequested moves Pending/Ready -> Cancelled and StartAuthorized
-> CancellationRequested; Terminal changes only run status/timestamp; IntegrityFaulted
stops run admission without releasing uncertain nodes; PoisonReconciled restores
only validated protocol retry state. No operation writes persisted node Running:
StartAuthorized projects as public DagNodeStatus.Running until authoritative join,
even if the child has not yet materialized. Public node/run enum stays unchanged.
Illegal state regressions and second different input/output writes fail closed.
Repeated byte-identical facts are operation replay, not new writes. Child identity,
input/output bytes and non-null timestamps are immutable once set. The runtime
reducer validates each allowed kind/state transition, reconstructs untouched
nodes, and verifies the exact checkpoint result. Terminal/IntegrityFaulted may
have no node changes; PoisonReconciled cannot mutate graph/input/bindings.

Unknown/duplicate/extra/missing fields, null required fields, unknown kind/version/
enum, noncanonical GUID/timestamps, unequal run/operation/version and graph mutation
fail closed. UTF-8 without BOM, fixed property order, exact base64 bytes, UTC
round-trip timestamps and invariant numbers; providers store exact bytes/hash and
never deserialize business values. No polymorphic fallback is introduced.

Checkpoint content type stays application/vnd.orcacore.dag-run+json;v=1; its version-1
payload has aggregateKind, version, runId, streamVersion, snapshot and lowercase
SHA-256 of exact bytes. Every accepted mutation OVERWRITES ONE current checkpoint
at its final version; events do NOT retain full snapshots after creation. Snapshot
load plus validated tail needs no genesis replay. A checkpoint frame still contains
the full graph/input/node state; decoding validates it against bound identity/version.
Retention never deletes nonterminal truth, unresolved admissions or required receipts.

P3-3 bound: retained logical space is O(N+E+V+T+C), where N/E are nodes/dependencies,
V is committed value bytes, T is constant-size transition headers/receipts and C
is the total number of changed-node entries. Each value is recorded at its first
write only, not copied per transition. A blocking/cancellation closure can change
O(N) nodes once; it does not copy the graph into all events. Current checkpoint and
node index add O(N+E+V) space. Repeated current-checkpoint writes still cost
O((N+E+V)*T) WRITE AMPLIFICATION (and provider transaction-log/backup volume until
normal DB retention); no cheap-I/O claim or arbitrary DAG-size limit is made.
DG-P18 tests event retained size scales with changed nodes, verifies absence of
creationSnapshot on non-created frames, and reproduces checkpoint/tail equivalence.

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

OutboxWrite adds `public Guid? DagRunOwner { get; init; }`,
`public Guid? RecipientHostId { get; init; }` and
`public Guid? DagRunSubscriptionId { get; init; }`; OutboxClaimRequest adds
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
that notice. Expired subscriptions/undeliverable observer hints are operational maintenance
data, not lost run truth/admission. Their trusted DagRunSubscriptionId is outside
the opaque payload and links the owning subscription. Subscriptions renew while the local wait lives;
local cancellation unregisters without cancelling run/children. No broker feature,
external dispatcher or database-specific notification API is assumed.

WaitForChange/Terminal subscribe THEN authoritatively recheck, attach the local
signal BEFORE claiming can deliver it, and recheck on every notice. This closes
registration/commit/delivery races. A missed wake is recoverable from the durable
subscription notice and startup scan; the bridge never polls terminal snapshots.
Run-turn/waiter heartbeat is operational ownership maintenance, not terminal polling; no turn survives idle/child/capacity waits.
Startup discovery and bounded retry timers do not re-invoke already committed
mapping. Transport acknowledgement occurs only after the owning transition/hub
wake has completed; lost ack replays committed state. A corrupt trusted owner
quarantines the row visibly, never Dispatched success.

### 6.1 Dead boot-host observer cleanup (P3-2)

Subscription lease duration is >0 and <=30 seconds, refreshed every <=10 seconds
only while that boot host's wait is alive. New append notices require a still-live
subscription; unsubscribed/expired entries cannot create notices. EVERY durable
host runs PruneExpiredDagRunObserversAsync with positive bounded MaxCount at least
every 10 seconds. Under healthy scheduling it removes an expired subscription and
its unacknowledged addressed notices within 10 seconds, or after the current
outbox claim lease expires if later. It uses the provider clock and one transaction.
Active claims are not stolen/deleted; a claimant revalidates subscription liveness
before hub delivery and discards an expired observer through the same maintenance
path, never as successful run progression. A claim release/expiry makes cleanup
eligible. Unsubscribe has the same cleanup semantics; retries are idempotent.

Only addressed observer hints linked by exact run/subscription/boot host may be
purged. Unaddressed progression, child records, poison and uncertain admissions
are NEVER eligible. Quarantined observer corruption retains its diagnostic until
normal maintenance disposition. Observer rows are classified separately as
operational notices and excluded from BUSINESS pending/poison messaging pressure
and stuck-work counts; section 6.2 names exact operational projection additions and gauges. Existing workflow behavior is unchanged, but non-continue-is-external queries MUST be corrected for reserved DAG kinds. No dead
boot ID redirects a notice to another host: replacement waits resubscribe/recheck.
DG-P17 covers host death before/after notice creation/claim, inactive-only cleanup,
active-claim retention and the pressure classification with real storage.

### 6.2 Exact pressure accounting (P3-1)

Keep IWorkflowOperationalStore.RefreshStuckStateAsync(WorkflowOperatorStatisticsRequest)
and GetOperatorStatisticsAsync unchanged. BEFORE WorkflowOperationalPressure has its
14 existing long init properties. AFTER adds EXACTLY these fifteen long init properties:
InternalDagPendingCount, InternalDagRetryableCount, InternalDagClaimedCount,
InternalDagPoisonedCount, DagObserverPendingCount, DagObserverRetryableCount,
DagObserverClaimedCount, DagObserverPoisonedCount, ActiveDagObserverCount,
ExpiredDagObserverCount, ActiveDagRunCount, StuckDagRunCount, DagRunStreamEventCount,
DagRunCheckpointCount, DagRunCheckpointLag. The API catalog prints their exact lines.

Classification is disjoint: continue remains Continuation*; the three child kinds
and UNADDRESSED ready hints are InternalDag*; ADDRESSED ready hints are DagObserver*;
all other nonreserved legacy internal-dispatch/application kinds remain ExternalOutbox*.
No reserved DAG kind is external. Pending/Retryable/Claimed/Poisoned keep existing
outbox-state meanings. Addressed observer corruption contributes only observer poison,
not child/business pressure. Total business outbox gauges sum continuation+internal
DAG+external, excluding observers. Global StreamEventCount/CheckpointCount/CheckpointLag
include BOTH aggregate kinds, computed from transaction-maintained stream heads/
checkpoint versions, never aggregating event history on each scrape. DagRun* fields
are the DAG subset (event sum, checkpoint count, maximum head-minus-checkpoint lag).
ActiveDagRunCount includes Running/CancellationRequested/IntegrityFault; StuckDagRunCount
includes Ready work beyond the observation threshold without committed progress, not
ordinary waiting/parked children solely by age. A slow mapper can produce an operator
stuck diagnostic but never a failure/time budget. RefreshStuckStateAsync updates DAG
observations using the same existing runtime ObservedAt/StuckThreshold contract;
GetOperatorStatisticsAsync remains side-effect-free.

Durable diagnostics scrapes that authoritative snapshot with existing interval and
cardinality rules. Exact new BCL ObservableGauge<long> names: orca.dag.internal.pending.count
and orca.dag.observer.pending.count (provider.name,state Pending/Retryable/Claimed/Poisoned);
orca.dag.observer.subscription.count (provider.name,state Active/Expired);
orca.dag.runs.count (provider.name,state Active/Stuck); orca.dag.stream.events.count,
orca.dag.checkpoints.count, orca.dag.checkpoint.lag (provider.name only).
Existing orca.continuation.pending.count and orca.outbox.external.pending.count keep
their respective classifications; no run/instance IDs or error text become attributes.
Expired/active observers derive from provider transaction time at snapshot capture;
claim cleanup races retain exactly one classified row, never vanish business truth.
This applies the unchanged DR-050/DU-052/canonical operational-projection rule to
the four proposed new kinds; it adds no fifth owner or redefines existing workflow
stuck semantics. DG-P20 certifies all four states/kinds, observer exclusion, DAG stream
pressure/stuck refresh and equal gauges on all providers including real storage.

## 7. First-create schemas and transactions

Proposed additive FIRST-CREATE shape in both providers; no ALTER/upgrade DDL.
Existing workflow tables and key encoding remain unchanged. New complete tables:

| Table | Required columns / constraints |
|---|---|
| dag_runs | run_id PK; start_key_digest unique, start_key_text exact nonblank; creation_sequence unique generated; version bigint nonnegative; definition_id, definition_version positive, definition_fingerprint; state checked closed enum; last_operational_at UTC; is_stuck false default, last_progress_at UTC; graph_payload, input_payload, checkpoint_payload, checkpoint_sha256; created_at, updated_at, completed_at; owner_id nullable, owner_fence bigint nonnegative, lease_request_id, lease_granted_at, lease_expires_at, lease_hard_deadline nullable; active lease fields all null or all present, expiry <= hard deadline, hard deadline = granted_at + 15s, fence retained across release |
| dag_run_events | (run_id, stream_version) PK/FK; operation_id; record_kind/version; payload/checksum; created_at; exact kind check and positive versions |
| dag_run_operations | (run_id, operation_id) PK/FK; fingerprint; result_version; immutable receipt; exact replay/conflict serialization |
| dag_run_nodes | (run_id, ordinal) PK/FK; unique(run_id,node_id); binding_payload, dependencies_payload; state; child_id nullable (unique only when present); input_present, input_bytes, input_contract, input_codec, input_fingerprint; output_present, output_bytes, output_contract, output_codec, output_fingerprint; failure_code; ready_at, started_at, completed_at; nonnegative ordinal; parked/start-authorized remain admission-counted |
| dag_run_lease_requests | (run_id,request_id) PK/FK; queue_sequence unique generated; owner_id; purpose closed enum; state Waiting/Granted/Consumed; wait_expires_at; granted_at/hard_deadline nullable; granted_token bigint nullable before grant, append_committed boolean/bit default false; request and granted token retained as consumed through run retention; filtered unique(run_id,owner_id) for live Waiting/Granted; queue index(run_id,state,queue_sequence,wait_expires_at) |
| dag_run_subscriptions | subscription_id PK; recipient_host_id; run_id FK; after_version; lease_expires_at; unique recipient/subscription; expiry index |

New columns in the first-create shared outbox definition: nullable dag_run_owner,
nullable recipient_host_id, nullable dag_run_subscription_id (only addressed ready observers, FK to subscription while live), plus indexes (kind,state,retry_at,lease_expires_at,
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
owner_id, lease_request_id, lease_granted_at, lease_expires_at and lease_hard_deadline are nullable. Queue purpose/state use checked text / nvarchar(64) BIN2; queue_sequence bigint identity, request/owner GUID, waiting expiry non-null, grant times nullable before grant. A consumed receipt retains request/token/sequence until run retention; expired waiters become Consumed, not resurrectable deletions. Absent input/output requires all its
bytes/contract/codec/fingerprint fields null; present-null requires four actual
bytes and all metadata present. Input/output presence itself is never nullable.
Graph/binding/dependencies payloads are the exact validated version-1 frame fragments.
The events table's record_version is 1; payload/checksum are required; operations
store operation_fingerprint, result_version and receipt_payload with immutable
unique (run_id, operation_id). Every DAG FK names dag_runs(run_id), never workflow
instances; live rows are not cascade-deleted. SQL Server uses filtered unique
child_id WHERE child_id IS NOT NULL; PostgreSQL has the equivalent partial index.

receipt_payload has ONLY operationId, operationFingerprint, resultVersion, outcome,
ordinal (nullable), childInstanceId (nullable), mappedInputFingerprint (nullable).
It contains no full graph/snapshot/value buffer. Exact replay returns this receipt
and, if requested by an internal result, the CURRENT authoritative snapshot,
not a per-transition retained full snapshot. This bounds receipt storage too.

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
notifications. Acquire/renew/release/cancel-waiter lock that same row AND its lowest live FIFO request. Expired observer cleanup serializes subscriptions/outbox claims with the same live-row predicates. SQL Server uses serializable
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
| DG-P05 | FIFO request identity, lost response, cancelled/dead waiters, no bypass; 5s expiry/15s absolute deadline; consumed request cannot resurrect; stale fence/version append leaves zero effects |
| DG-P06 | All child/start-intent admissions survive takeover; lower current host ceiling; CapacityDeferred vs AdmissionStopped |
| DG-P07 | Four kind partitions, wrong-lane provider result, internal sink Success, no dispatcher, role-free no-ready claim |
| DG-P08 | Eager unused dependency decode -> DAG_INPUT_MAPPING_INVALID; null bytes/presence; mapper/local cancellation distinction |
| DG-P09 | Crash before/after start intent, child append, join, ack; exact deterministic recipe and separate binding conflicts |
| DG-P10 | Atomic failure/blocking closure, independent branches, once-only slot release and stable authored ordering |
| DG-P11 | Cancellation/start winner, reordering, admitted-child materialization, quarantine/physical-stop distinction |
| DG-P12 | Subscribe/recheck race, addressed host partition, lost ack, host death, restart discovery, zero terminal polling |
| DG-P13 | Exact API/baseline additions, shipped Hosting behavior, closed turn-method allowlist; forbidden direct provider calls and full type/member/reflection/UnsafeAccessor/expression bans |
| DG-P14 | A short I/O turn renews continuously; B child join gets its FIFO turn after A release/absolute expiry; an out-of-turn slow mapper holds NO lease and cannot delay that join |
| DG-P15 | DAG host B invokes public DagRunHandle.RequestCancellationAsync while A has slow speculative mapping or a short turn; cancellation commits, A cannot admit late input; a role-free host consumes the child cancellation/join, preserving slots until terminal |
| DG-P16 | Idle/CapacityDeferred/child wait releases turn; lower/higher limits on successive admitted turns, captured child bridge cannot borrow a newer fence; local call cancellation abandons waiter |
| DG-P17 | Dead observer expiration/claim race/unsubscribe purge, no orphan pending pressure; progression/child/poison rows retained |
| DG-P18 | Compact event changed-node size, value writes once, current full checkpoint overwrite, exact state-transition table including StartAuthorized public Running projection, checkpoint+tail reduction and tamper rejection |
| DG-P19 | Mapper gated across >15s controlled provider time with no run lease; joins/cancellation progress, terminating result commits exactly once after unrelated version conflict and CapacityDeferred without reevaluation; repeated hints launch one worker/node/host, global worker ceiling and cancelled-worker retention hold |
| DG-P20 | Exact internal DAG/observer/external pressure and all states; DAG event/checkpoint/stuck projections and matching BCL gauges; provider-clock observer expiration, no business loss |
| DG-P21 | Same-host mixed-purpose local FIFO and remote FIFO, OwnerBusy/LocalQueueFull/no effects, retry coalescing, queued cancellation and explicit captured host limit; full before/after baseline parity |

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

The [exact API catalog](task-1-1-exact-api-additions-rc-2-remediation-2026-10-05.md) gives before/after
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
 - `docs/specs/12-acceptance-criteria.md`: AC-608–618: retain all Section-8 compiled exits. DG-P01–21 adds real-storage crash/fence/claim/null/notification, FIFO liveness, observer maintenance and compact-event tests, not acceptance credit or a waiver/expected-red change.
 - `docs/specs/16-requirements-durable-driver.md`: DR-037/041 and DR-AC-029: continue-only, workflow-event-only, every-host three-child-kind and DAG-role-only ready partitions. Ready claims have null-or-exact boot-host recipient; role-free hosts never claim ready. Internal IMessageDispatcher excludes all six reserved kinds; wrong-lane release/poison never frees uncertain admission. Startup discovery recovers runs; terminal waits subscribe/recheck, not poll.
 - `docs/specs/17-selected-mode-capability-matrix.md`: §17.2.6: the six child calls become subordinate to the fenced run coordinator; its closed input-commit result adds CapacityDeferred. Complete creation/caller-key binding, graph/input persistence, recovery/admission/failure closure, child joins, cancellation/poison and notifications are defined. Public application signatures and OutputOf/null/mapper policy stay unchanged; exact advanced API additions are catalogued, with no new friend or codec access in Dag/Hosting.
 - `docs/implementation/00-stack-decisions.md`: Dated proposed decision: full typed DAG stream/provider lifecycle precedes bridge source. The current nine friends/package graph stay unchanged. The earlier six-call child contract remains immutable history, never re-described as complete.
 - `docs/implementation/01-solution-architecture.md`: Runtime.Protocol owns DAG records/codec; Provider.Abstractions owns explicit run ports; Durable.Hosting owns lease broker/codec/mutation and role-free join/cancel coordination; Dag.Hosting owns admission/mapping coordination and sink, using only bridge turns. No reverse package edge or tenth friend.
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
