# Task 8.4 — remediated child protocol and mandatory progression gate

Date: 2026-10-04. Base: `c85fd0f301f110db691a026bcdd0c0e35c6b26c1`.
Status: proposed child-protocol contract; BR-1/BR-2 remediation. Independent review required.

The rejected 2026-10-04 artifact remains byte-exact and is historical, not authority.
This revision deliberately takes the reviewer's narrower BR-2 option: the six
operations below are the child-protocol slice, NOT the complete durable DAG seam.
Neither this review nor its checkpoint authorizes bridge source. The separately
reviewed **Task 8.4/8.5 run-persistence and progression contract** in section 10 must
land first, including any required canonical amendment. Its exact additional
signatures and metadata allowlist must be reviewed before implementation; they are
not implicit exceptions to this child-protocol list.

## 1. Scope and authority

This target prepares the named/versioned internal protocol already required by reshape
Task 8.4. It does not implement or complete Tasks 8.3–8.10. No product source,
canonical requirement, public API, package reference, friend attribute, codec allowlist,
acceptance waiver or ExpectedRed disposition changes. Archival is separate.

The runtime-view amendment is complete: source checkpoint
`4eb2e8d3a68e0ae7d873bd4f54c53735beefb132`, evidence
`2fdfa59a747a5d1d1667abd63a8def88168a172b`, source activation
`1d8941b7c4e4a96752b33684075d152494057c49`; closeout checkpoint
`3270678409593c9af0a7c1810f3c4be329b133fd`, evidence
`496f30ccd91fbe63b8fd099c546d36864c440a4d`, activation/base above.
The closeout APPROVE is immutable at
`docs/review/developer-facing-interface-section-08-task-8-3-runtime-view-closeout-independent-review-verdict-2026-10-04.md`,
9,888 B / SHA-256 `25962bbf9e461fe8fb5aaddac6b846b4820442b3e45525f52751773611ae94cc`.

The two normative trees supply these obligations:

| Canonical requirement | Numbered authority | Contract clause |
| --- | --- | --- |
| durable-runtime / Durable DAG progression is runtime owned | doc 08 CP-023–028; doc 16 DR-041; doc 17 §17.2.6 | Readiness, one child, recovery, status/cancellation handoff |
| workflow-contracts / Durable values use one fixed detached codec | doc 17 §17.2.6; doc 06 DU-043 | Declared-type eager decoding; detached committed input/output |
| repository-foundation / Dependency direction remains one-way | doc 17 §17.5; doc 10 PR-005 | Existing Durable.Hosting → Dag.Hosting friend only |
| developer-facing-surface / Implementation package boundaries use exact internal friends | doc 17 §§17.2.6, 17.5 | No public child or reflection bridge |
| durable-persistence-and-outbox / Workflow store persists first-class durable records | doc 06 DU-031/033; doc 16 DR-037 | One provider commit/outbox, disjoint dispatch kinds |
| quality-and-verification / Typed DAG execution is acceptance tested | doc 12 AC-607/608/610/612/613/614; doc 16 DR-AC-008 | Implementation exit evidence in §9 |

The provenance fixture currently records 180 rows / 47,347 B /
`40d4d8c0b9144ea087d7b36d33df35d4736942ae615f50126f7ba136b20da8c1`,
zero pending canonical operations, and semanticApprovalEligible true. This is
synchronization evidence, not approval of the choices proposed here.

## 2. Existing seams and ownership

`OrcaCore.Durable.Hosting` already grants `InternalsVisibleTo("OrcaCore.Dag.Hosting")`.
This target proposes no tenth product friend. Its direct references remain OrcaCore,
Engine.Durable and Provider.Abstractions; no reverse Dag reference is permitted.
`DagRunId`, `DagNodeId` and `DagNodeRef` belong to Dag, so none appears in the
Durable.Hosting bridge signatures. Dag.Hosting performs the checked conversion from
the exact plan's references to the internal run/ordinal coordinate below.

`DagInputMappingAdapter<TRunInput>` is the shipped Dag.Hosting adapter. It owns
mapping invocation and the exact-reference-to-plan checks. The durable bridge owns
reading committed dependency outputs, fixed-codec materialization and input
normalization, bytes, fingerprints, commands, lineage and persistence. Dag and
Dag.Hosting receive no Core codec access and no new OrcaCore member allowlist entry.
The bridge reaches existing Core codec behavior through Engine.Durable's owning
internal adapter; Durable.Hosting must not reference Core directly or use reflection.

`CoreWorkflowValueCodec` already provides declared-type Serialize/Deserialize with
`orcacore-json-v1`. `ProviderCommitBatch` already commits one stream's events,
checkpoint, projections, start binding and outbox records atomically. The ordinary
start command already carries ParentInstanceId and RootInstanceId. No DAG bridge is
implemented by those facts alone: the run/node aggregate, deterministic start path,
join dispatch and integration evidence remain future work.

## 3. Proposed internal V1 child-protocol slice (not a complete run API)

All declarations below are implementation-only in assembly OrcaCore.Durable.Hosting,
namespace `OrcaCore.Durable.Hosting.Dag`. They are a proposed signature contract, not
source declarations or a public compatibility promise. Record collections and buffers
must be validated and copied; a record signature does not grant mutable persistence
authority. No delegate, byte payload, private workflow state, provider record or engine
type crosses this seam. Nullable output presence is separate from output value.

```csharp
internal readonly record struct DurableDagNodeIdentityV1(Guid RunId, int AuthoredOrdinal);

internal sealed record DurableDagWorkflowBindingV1(
    DefinitionId DefinitionId, DefinitionVersion DefinitionVersion,
    DefinitionFingerprint DefinitionFingerprint, Type InputType, Type? OutputType);

internal sealed record DurableDagDependencyV1(
    DurableDagNodeIdentityV1 Identity, DurableDagWorkflowBindingV1 Workflow);

internal sealed record DurableDagNodeContractV1(
    DurableDagNodeIdentityV1 Identity, DurableDagWorkflowBindingV1 Workflow,
    IReadOnlyList<DurableDagDependencyV1> DirectDependencies);

internal sealed record DurableDagDecodedOutputV1(
    DurableDagNodeIdentityV1 Identity, Type DeclaredType, object? Value);

internal abstract record DurableDagMappingInputsV1
{
    private protected DurableDagMappingInputsV1() { }
    internal sealed record Available(
        object? RunInput, Type RunInputType,
        IReadOnlyList<DurableDagDecodedOutputV1> Outputs) : DurableDagMappingInputsV1;
    internal sealed record MappingInvalid : DurableDagMappingInputsV1;
}

internal sealed record DurableDagMappedInputV1(object? Value, Type DeclaredType);

internal sealed record DurableDagInputReceiptV1(
    DurableDagNodeIdentityV1 Identity, InstanceId ChildInstanceId,
    PayloadFingerprint MappedInputFingerprint);

internal abstract record DurableDagInputCommitResultV1
{
    private protected DurableDagInputCommitResultV1() { }
    internal sealed record Committed(
        DurableDagInputReceiptV1 Receipt, bool WasExisting) : DurableDagInputCommitResultV1;
    internal sealed record MappingInvalid : DurableDagInputCommitResultV1;
    internal sealed record AdmissionStopped : DurableDagInputCommitResultV1;
}

internal abstract record DurableDagChildStartResultV1
{
    private protected DurableDagChildStartResultV1() { }
    internal sealed record Started(
        InstanceId ChildInstanceId, bool WasExisting) : DurableDagChildStartResultV1;
    internal sealed record CancelledBeforeStart : DurableDagChildStartResultV1;
}

internal sealed record DurableDagChildSnapshotV1(
    InstanceId ChildInstanceId, InstanceId RootInstanceId, InstanceId ParentInstanceId,
    WorkflowInstanceStatus Status, bool HasOutput, WorkflowFailure? Failure);

internal interface IDurableDagChildBridgeV1
{
    ValueTask<DurableDagMappingInputsV1> ReadMappingInputsAsync(
        DurableDagNodeContractV1 node, CancellationToken cancellationToken);
    ValueTask<DurableDagInputCommitResultV1> CommitMappedInputAsync(
        DurableDagNodeContractV1 node, DurableDagMappedInputV1 input,
        CancellationToken cancellationToken);
    ValueTask<DurableDagChildStartResultV1> StartOrReattachAsync(
        DurableDagNodeIdentityV1 node, CancellationToken cancellationToken);
    ValueTask<DurableDagChildSnapshotV1?> GetChildAsync(
        DurableDagNodeIdentityV1 node, CancellationToken cancellationToken);
    ValueTask<DurableDagChildSnapshotV1> WaitForChildTerminalAsync(
        DurableDagNodeIdentityV1 node, CancellationToken cancellationToken);
    ValueTask<WorkflowCancellationRequestStatus> RequestChildCancellationAsync(
        DurableDagNodeIdentityV1 node, CancellationToken cancellationToken);
}
```

These six operations define only the proposed child protocol. They require an
already-created authoritative run and persisted graph supplied by the separate
section 10 contract. They cannot create or finish a run, persist mapping failure,
perform dependency closure or wake a mapper. Their future caller is the run
coordinator, not an application caller or a general command processor. Source and
the final bridge metadata allowlist are gated on section 10; no claim of a complete
run protocol or runnable DAG is made here. Dependencies must name the same run, distinct
nonnegative authored ordinals, exact installed workflow bindings and the node's
persisted direct dependency list; reject a forged/stale contract before mutation.
The owning runtime must resolve the registered typed implementation without passing
untyped workflow objects or reflecting into Dag. No hidden public factory is added.

## 4. Identity and lineage — explicit proposal for review

Use the persisted runtime-created DagRunId GUID as the DAG aggregate's lineage
anchor InstanceId by exact GUID conversion, not as a newly started workflow. Its persistence is a section 10 proposal, not an admitted workflow record. The
anchor is a distinguished DAG-run record, not an extra authored node, child workflow,
synthetic output, or MaxConcurrentNodes admission. Its DAG identity/version/fingerprint
and typed immutable run-input binding are validated by the DAG runtime. An ordinary
workflow lookup must not reopen this DAG aggregate as a workflow. Collision with an
existing different aggregate kind fails closed; it cannot overwrite that stream.

Every direct node child has ParentInstanceId = RootInstanceId = that anchor. Edges
express dependency, not lineage: depending on another node never makes its child the
parent. Continue-as-new preserves child identity and lineage. AC-614 navigation is
internal/provider-owned keyed linkage from the anchor to all node children; it exposes
no child private state and adds no ordinary public enumeration API.

Identity derivation V1 is deterministic and independent of host, retries, time and
completion order. Its UTF-8 input is exactly
`orcacore-dag-child-v1\n<run GUID in lower-case D format>\n<ordinal in invariant decimal>\n`.
SHA-256 of that record gives the full lowercase identity digest. Child InstanceId
uses the first 32 digest hex characters, interpreted as GUID `N` text; if all zero,
set its last hex character to `1`. There is NO caller-visible start idempotency key for a DAG child. The internal
start path uses the deterministic child InstanceId and a persisted full-coordinate
binding; it must not write or look up a DAG key in the caller's global string-key
namespace. A caller cannot pre-claim a runtime DAG key. Collision with an existing
ordinary workflow's InstanceId fails visibly as integrity conflict; it never
reattaches that unrelated workflow or silently abandons the admitted node. If the
runtime requires a start binding, its domain discriminator is runtime-owned and
separate from CallerStart; that representation is reviewed in section 10 before
source, not added to the current provider contract by this artifact. Use the full coordinate and exact binding/input
bytes as conflict checks, not truncated hash equality as a proof of compatibility.
Authored identity/version/fingerprint and declared input/output types bind separately;
changing them under an existing coordinate is an integrity conflict, not another child.

This anchor and byte recipe are implementation choices requiring this review. If
they conflict with either normative tree, stop for an explicit amendment; this artifact
does not silently grant them normative authority.

## 5. Declared-type decode, mapping and null/failure policy

ReadMappingInputsAsync reads authoritative successful direct dependency facts and
the committed run-input bytes, never live state or incomplete output. All direct
dependencies must have succeeded; a resultless dependency contributes readiness but
no output entry. Materialize every resultful output eagerly by its exact declared
type, including ones the opaque mapper never reads. Available.Outputs contains
exactly those entries, no extras, foreign references, duplicates or missing values.
Dag.Hosting rebuilds the dictionary using the exact references from its plan and calls
the shipped adapter. Each read creates detached values and a read-only copied map;
caller mutation cannot alter committed bytes or a later read. The bridge does not
invoke the mapper and receives no mapper delegate.

A successful null output is valid for reference/nullable-value declared types;
HasOutput remains true and the value is null. Resultless output, missing output,
wrong declared/runtime type and nonnullable-value null are not equivalent. This is
the approved doc 17 §17.2.6 rule, not a new null policy. Ordinary declared-type value
decode/materialization failure returns MappingInvalid before mapper invocation,
input commit or child start, even for an otherwise unused output. Unsupported mapped
input/type or ordinary normalization failure likewise returns MappingInvalid before
commit/start. No partially decoded dictionary is delivered to the mapper.

The adapter's ordinary mapper exceptions, including OperationCanceledException,
remain DAG_INPUT_MAPPING_INVALID. OutOfMemoryException, StackOverflowException and
AccessViolationException remain process/infrastructure exclusions; only the first
can be tested as an in-process exclusion. Runtime call cancellation, storage faults,
unknown protocol/codec version, checksum/identity corruption, unavailable exact
definition and optimistic-concurrency exhaustion must not masquerade as mapping
failures. Reload/retry expected-version conflicts with the same coordinate; genuine
protocol/runtime failures retain their existing exceptional classification. Local
call cancellation never fabricates a durable run/child cancellation request.

## 6. Commit before start; no cross-stream atomicity claim

After mapping succeeds, the durable side serializes the typed mapped input using
the existing fixed codec, round-trips to validate/detach it, copies the exact bytes
to be persisted, and computes the ordinary PayloadFingerprint from those committed
bytes alone. It does not include type names, definition IDs, delegates, clocks or
provider framing in MappedInputFingerprint. A present mapped null is encoded
explicitly by orcacore-json-v1 as the four UTF-8 bytes 6e 75 6c 6c ("null"), with
HasInput = true, its declared type and codec format recorded. It is never represented
by an absent payload, including when the ordinary non-DAG start path omits null.
The ordinary uppercase SHA-256 PayloadFingerprint hashes exactly those four bytes;
nonnullable-value null remains invalid. Reattach must compare presence, type, format,
bytes and fingerprint, not absence with null. Exact binding/type/codec information is
stored separately. Start receives those bytes without invoking the mapper again or
re-encoding a live object. Losing physical mapping attempts have no commit authority.

One expected-version DAG-owner commit contains the node input bytes/fingerprint,
deterministic child binding, admission decision and its internal child-start outbox
intent. Validate readiness and MaxConcurrentNodes at that serialized decision. The ceiling
is PER DAG RUN: admitted, nonterminal node children of that run only, including
parked children and committed start intents awaiting materialization. It is not a
process-wide ceiling across runs, path-token ceiling, named-step throttle or lease
count. The run's serialized admission decision and recovery counts are specified
in section 10; no slot may be released merely because a child has not materialized. A child
can neither start nor consume an admission without that committed intent. Replays
with an existing committed input return it, ignoring a newly evaluated mapper result;
never overwrite it. A malformed/stale binding is an integrity conflict. MappingInvalid
commits no input/start intent; the separate DAG progression transition records node
failure and DependencyBlocked closure, leaving independent nodes eligible.

ProviderCommitBatch is a single-stream boundary. Starting the child is a second
atomic child-stream commit with its complete definition/input binding, runtime-only
coordinate binding (not a caller start key), ParentInstanceId/RootInstanceId,
start/checkpoint/projection and continuation.
These two commits are connected by a retryable outbox intent, NOT an asserted
atomic multi-stream write. A lost start response reattaches the same identity after
checking full binding; a collision never returns another workflow's child. The
same parent intent remains reclaimable until its result is durably recorded.

## 7. One outbox; dedicated DAG runtime pump, never external dispatch

Reserve dag-child-start-v1, dag-child-join-v1 and dag-child-cancel-v1 for a
dedicated runtime-owned Include-only consumer over the EXISTING logical provider
outbox. It is registered whenever the durable engine role runs, independently of
IMessageDispatcher and independently of whether this host registers AddOrcaCoreDag.
The durable side can finish/reattach a previously admitted child, commit joins and
propagate cancellation without invoking a mapper. It must not depend on a reverse
Durable.Hosting -> Dag.Hosting package reference or an external transport.

All external IMessageDispatcher pumps, including Engine.Durable's default selector,
Durable.Hosting's hosted selector, explicit caller selectors and post-claim record
validation, MUST exclude/reject all three DAG kinds unconditionally. The continue
lane includes only continue; the public event lane includes only workflow-event;
the DAG runtime lane includes only the three DAG kinds. The external partition
excludes ALL five reserved runtime/application-event kinds. A provider returning
a DAG record to a wrong lane must have it released without dispatch or Dispatched
acknowledgement, even if a dispatcher would return Success. An InMemory sink is
not a DAG handler. No external dispatcher is required for DAG progress on
PostgreSQL, SQL Server or InMemory, and a non-DAG durable host cannot swallow intents.
No second outbox, private queue, application callback or new persistence port is
authorized by this contract.

After an operation's owning durable state transition commits, acknowledge its
outbox lease; if acknowledgement is lost, replay reads the same persisted outcome.
Transient provider/lease failures release or retry the intent with existing bounded
backoff and at-least-once rules, never acknowledge Success without a committed
result. Expected-version conflicts reload the owning aggregate. A poison disposition
is not success and never silently frees capacity:

| Kind | Permanent failure disposition required in section 10 |
| --- | --- |
| dag-child-start-v1 | Persist a visible node/runtime protocol failure and blocked-dependent closure in the run before poison/ack. If child materialization is ambiguous, retain admission, reconcile its deterministic identity and do not claim absent or safely stopped. |
| dag-child-join-v1 | Re-read authoritative child terminal facts; commit the idempotent join, or persist an explicit visible run-integrity fault before poison. Never discard the sole terminal fact or release admission on notification alone. |
| dag-child-cancel-v1 | Persist a visible cancellation-delivery failure; retain CancellationRequested and capacity until authoritative child terminality/reconciliation. Never report Cancelled or safe resource release on failed delivery. |

Unreadable payloads must retain the outbox row's trusted owning-run coordinate for
fault recording. If that trusted coordinate is also corrupt, quarantine the row and
publish a provider/runtime integrity diagnostic; retained admissions remain visible
as stuck, not success. The progression contract must define recovery from that
fault before source. Unknown versions fail visibly rather than coerce. V1 payloads
contain persisted coordinates/bindings, not CLR assembly-qualified type names,
delegates or application transport envelopes.

DR-037, PR-015 and their DR-AC-029 provider-certification obligation currently name
only continue/workflow-event partitioning. The section 10 normative disposition must
extend those numbered clauses to the DAG partition and record whether the canonical
internal-continuation requirement needs a MODIFIED delta. Where it does, an approved
post-gate amendment plus canonical sync precedes source. This artifact does not
silently reinterpret the existing external-dispatch partition as a DAG runtime pump.

Child successful output, optional outcome and terminal fact commit atomically in
the existing child workflow path. A join notification is derived in that commit's
outbox. The DAG owner consumes terminal child facts idempotently, under expected
version, and never trusts the notification alone or a live object as terminal/output
authority. A crash or duplicate/out-of-order notification cannot lose an outcome,
duplicate child creation, rerun committed input mapping or release admission twice.
GetChildAsync returns null only before child materialization; it throws on an
incompatible binding. HasOutput is false for resultless success and non-success.
WaitForChildTerminalAsync requires a materialized child and uses subscribe-then-
authoritative-recheck, never polling; its token cancels only that local wait.

Run cancellation is serialized on the DAG aggregate before child cancellation
intents are published. Pending/Ready become Cancelled; admitted nonterminal children
become CancellationRequested and keep admission until terminal. DependencyBlocked
remains explanatory. The serialized winner is cancellation versus the parent's
committed start authorization, not a claimed atomic race between two streams.
CancelledBeforeStart applies only when cancellation beat that authorization; there
can then be no committed start intent. Once authorization/input/start intent won,
complete or reattach the same child even if its physical materialization is later,
then propagate committed cancellation. This finishes an already admitted operation,
not new admission after cancellation. Never infer absence or release its slot from
a cross-stream read or lost acknowledgement. Start/cancel delivery may be reordered:
the internal handler must ensure the authorized child exists before applying its
persisted cancellation, without executing business code after that cancellation
has won the child's mutation lane.

The admitted start intent must remain capacity-accounted until child terminality;
otherwise competing delayed dispatches could oversubscribe MaxConcurrentNodes.
This logical authorization boundary and its representation in run/node state must
be reviewed with 8.5/8.7; the artifact does not claim the corresponding runtime
state machine is already implemented.

RequestChildCancellationAsync is allowed only for a committed run cancellation;
it uses the owning engine mutation lane and returns its existing closed status.
Failure does not cancel unrelated siblings. Child Failed/TimedOut/Terminated maps
to CHILD_FAILED/CHILD_TIMED_OUT/CHILD_TERMINATED; unsolicited Cancelled maps to
CHILD_CANCELLED. Protected work retains the existing lease/quarantine rules:
terminal child status is never proof of physical stop or safe resource release.

## 8. Implementation sequence and review boundaries

1. Independently review/checkpoint this proposed contract. Approval is not evidence
   that the bridge exists and does not close 8.4, 8.5 or their acceptance waivers.
2. Prepare, independently review and checkpoint the section 10 run-persistence and
   progression contract FIRST. Resolve its normative dispositions and any required
   amendment/sync; review the complete typed callback/run API and metadata allowlist.
   Until that gate lands, 8.4/8.5 bridge source remains blocked.
3. Implement 8.4's reviewed child protocol together with the now-reviewed owning run
   preconditions, deterministic identity/lineage and typed
   engine codec/start adapters in a source target, without expanding either DAG friend.
   Review the exact metadata reference allowlist as part of that target.
4. Implement 8.5's reviewed persisted DAG-owner progression, atomic mapped-input/outbox
   transition, kind-partitioned dispatch, join/recovery and competing-host behavior.
   Shared atomic infrastructure may land with 8.4 if the source request names it;
   no completion claim is justified by an in-memory bridge mock.
5. Keep 8.6 public registry/run/query/wait/cancellation wiring and 8.7 admission
   behavior as their reviewed slices. All required compiled exits remain owned by 8.10.

The runtime-view closeout's two later guard notes are carried forward, not silently
claimed fixed here: forbid UnsafeAccessor and System.Linq.Expressions in Hosting
and the linked harness; add DagRuntimeView.cs to permanent executable evidence paths.
The 8.4 source target must address both before claiming shipped-assembly bridge proof.
This reviewed remediation reconciles CLAUDE.md's closeout status and the stale
successful-null implementation sentences in CP-022 and doc 17. The evaluator is
implemented/source-approved; the durable decode/commit bridge is not. No dated
decision or immutable packet is rewritten. Task 7.3 rows/digest and exact block
pins change in this same freeze, never in an activation.

## 9. Required implementation evidence (not results of this target)

| Boundary | Required negative/positive cases | Owner |
| --- | --- | --- |
| Package/metadata | No extra friend/edge/export, forbidden non-public type/member, reflection/UnsafeAccessor/expression bridge; exact V1 seam | 8.4 |
| Declared values | Resultless dependency, reference/nullable null, bad/null value type, unused-output decode failure, eager exact map, copied inputs, local cancellation versus mapper cancellation | 8.4/8.5 |
| Identity/lineage | Same coordinate under restart/lost response/two hosts, different run/ordinal, binding/input collision, exact anchor/root/parent, continue-as-new preservation, no private state | 8.4/8.5; AC-614 |
| Commit and dispatch | Crash before/after input+intent, child commit and parent acknowledgement; shared outbox coexistence with continue/workflow-event, selector isolation and at-least-once dedup | 8.5; AC-613 |
| Mapping/reattach | Opaque mapper reevaluates only precommit, committed bytes/fingerprint never change, no second child, shipped Hosting adapter invoked, structural fingerprint unaffected | 8.4/8.5 |
| Terminal/cancel | Ordered dependency closure, independent progress, unsolicited child cancellation, cancel before/after start, missed-notification race, parked slot retained, quarantine preserved | 8.5–8.7 |
| Real providers | InMemory dev behavior plus PostgreSQL/SQL Server restart and competing-host commits, no test-only provider shortcut | 8.5/8.10 |

Current 243-green Infrastructure and fourteen intentional ExpectedRed tests are
baseline evidence only. This planning target cannot turn any required DAG scenario
green, supply AC-613/614 compiled evidence or justify closing the Section 8 exit.

## 10. Mandatory Task 8.4/8.5 run-persistence and progression contract

This is a named, separate CONTRACT review/checkpoint before ANY bridge source,
not an allowlist extension improvised in the 8.5 source review. It must resolve the
following complete progression matrix together; this child protocol depends on it.
The operation names below identify its required responsibilities, not pre-approved
signatures or extra members of IDurableDagChildBridgeV1.

| Future owner / operation | Direction, authoritative state and required commit |
| --- | --- |
| IDurableDagRunBridgeV1.CreateOrReopenRunAsync | Dag.Hosting -> Durable.Hosting: exact DAG definition/version/structural fingerprint, run GUID, typed run input, copied ordinal workflow bindings and every direct dependency. Commit immutable run-input bytes, graph binding, initial Pending/Ready states, effective per-run ceiling and initial ready intent together. Replay returns the same full binding; conflicting structure/input fails visibly. |
| ReadRunAsync / AcquireRecoveryAsync | Dag.Hosting -> durable side: detached committed run/node snapshot plus expected version and fenced progression claim; validate exact installed plan before mapping. Startup scan through an explicit existing-port disposition must discover unfinished runs, not guess run IDs. Readiness is recomputed from authoritative persisted dependencies. |
| IDurableDagReadySinkV1.OnRunReadyAsync | Interface owned by Durable.Hosting, implementation registered by AddOrcaCoreDag in Dag.Hosting: opaque run GUID/version wake, never a delegate or reverse package edge. Durable transition publishes a durable ready intent; include-only runtime pump hands it to the locally registered typed sink. No sink means release/retry, NOT Success. This requires a reviewed FOURTH DAG kind dag-run-ready-v1 and matching all-host external exclusions; it is not smuggled into the three-kind child list above. Recovery scans + subscribe-then-authoritative-recheck prevent lost notifications; no periodic terminal polling. |
| CommitMappingFailureAsync | Dag.Hosting -> durable side: validated exact node/version plus DAG_INPUT_MAPPING_INVALID. Atomic node-failure fact and authored-order transitive DependencyBlocked closure; independent branches stay eligible. If all work is terminal, the same run commit terminalizes the run and publishes terminal notification. Concurrent success/cancel observes the serialized winner. |
| CommitMappedInputAsync | The six-operation child slice: authoritative run ready/admission decision + immutable typed input bytes/fingerprint + deterministic child binding + start intent, all one owning-run expected-version commit. Mapping never happens after committed input. |
| ApplyChildTerminalAsync | Dedicated durable DAG pump -> run owner: authoritative child status/output-presence/bytes/failure, idempotent node terminalization, once-only admission release, blocked closure or new readiness, run terminal decision and notification in one run commit. Duplicate/out-of-order joins cannot resurrect work. |
| RequestRunCancellationAsync | Dag.Hosting -> durable side: atomically stop new admission, cancel Pending/Ready nodes and commit cancellation intents for admitted children; serialize against input/start authorization. In-flight mapping receives no authority to commit after cancellation won. Children retain slots until terminal. |
| WaitForRunChangeAsync / WaitForRunTerminalAsync | Dag.Hosting -> durable side: subscribe, authoritative recheck, then await version change/terminal committed state; local token cancels only wait. The typed sink and startup recovery path drive mappers; Durable.Hosting never evaluates them. |
| FaultRunAsync / ReconcilePoisonAsync | Dedicated durable pump -> run owner: visible protocol/storage-integrity disposition and operator-recoverable stuck/ambiguous admissions; fault and poison are never successful execution or proof of physical stop. |

The ready wake is an additional run-progression responsibility, not one of the six
child calls. Its fourth kind and callback signatures must be reflected in the final
metadata allowlist and certification matrix by that independent contract review.
Each committed readiness transition must be discoverable after process death;
notifications are hints, durable state is authority. Multi-host claims need fencing,
expected versions, retry and an explicit bounded recovery scan, not a volatile queue.

### Persistence proposal and stop rule

Propose a distinct event stream dag-run-<lowercase D run GUID>, not workflow-<id>:
aggregateKind = dag-run-v1; protocol-owned exact versioned event/checkpoint records
for creation/graph binding, mapped input+admission, mapping failure+blocked closure,
join, cancellation, terminal and integrity fault. Reuse IWorkflowEventStore,
IWorkflowCommitStore and IWorkflowOutboxStore through ProviderCommitBatch; no
DAG-private store or provider-specific SDK is proposed. Use the owning run stream
for each run commit and the existing workflow stream for each child commit. The
logical lineage anchor is an InstanceId conversion only; normal workflow query/start
must reject the DAG kind, and projection/query/recovery discovery must discriminate
it explicitly. Existing WorkflowEventCodec must never accept an unknown DAG record
by fallback/coercion. Exact codec tables, copied checkpoint schema, checksum/version
and projection/recovery fields belong to that contract's reviewed schema disposition.

These new aggregate records are OUTSIDE the Task 8.0 map's internal-continuation-only
interpretation. Therefore an approved canonical amendment to Workflow store persists
first-class durable records and the mapped numbered schema/record clauses is REQUIRED
before this representation is implemented. Name both trees, update the mapped
quality/provider certification exits, then sync through a reviewed delta. No canonical
file changes here, no claim that the current workflow codec already supports DAGs.
If the separate review chooses a different representation, amend this proposal and
its source pin explicitly; it cannot be an undocumented source-review exception.

The contract must also resolve startup discovery using existing port semantics or
an approved provider-boundary amendment, exact ready-sink signatures, durable DAG
definition re-registration and host-without-DAG-role behavior. It must not assume
IWorkflowOperationalStore enumerates full graph/run inputs when it does not.

### Required provider-certification extension

Extend DR-AC-029-style selector certification to InMemory, PostgreSQL and SQL Server:
simultaneous continue, workflow-event, all DAG child kinds and later ready kind;
transport sink Success cannot consume a reserved record; custom selectors cannot
override exclusion; no dispatcher registered; durable host without DAG role; missing
ready sink; wrong-lane provider result; lease loss/reclaim; poison for each kind;
crash/restart between input intent, child start, parent join and acknowledgement.
Every intent either remains retryable/visible or has its owning result committed.
An admission cannot disappear or hang silently on transport acknowledgement.

## 11. Review remediation and durable provenance

- BR-1: dedicated runtime-owned pump, all-host exclusions, failure/poison matrix and
  explicit numbered/canonical disposition; no source routing fix is claimed yet.
- BR-2: narrowed six-call child protocol plus mandatory separately reviewed run API,
  persistence representation, notification/recovery direction and amendment gate.
- P3-1: no DAG entry in caller start-key namespace; deterministic coordinate binding.
- P3-2: present null has explicit fixed-codec bytes/presence/fingerprint.
- P3-3: ceiling is per run, including parked children and committed start intents.
- P3-4: this entire artifact and the rejected predecessor are SHA-256 pinned in
  OpenSpecCorpusGuards source, on the canonical gate independently of activeFreeze.
  Task 8.4 links this artifact without closing the implementation task.
- Status: source-approved null evaluator and completed runtime-view amendment are
  named accurately, without claiming the unimplemented durable bridge is complete.

The original request, manifest, artifact and REJECT remain unchanged. A rejected
freeze binds their original packet/verdict bytes; the immutable-history catalog
admits the verdict. No approval of this proposal closes 8.4/8.5, changes ExpectedRed
or bypasses the new persistence/progression/amendment gate.
