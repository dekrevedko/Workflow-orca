# Task 8.4 — proposed durable DAG child bridge contract

Date: 2026-10-04. Base: `c85fd0f301f110db691a026bcdd0c0e35c6b26c1`.
Status: proposed implementation contract; independent approval required before source.

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

## 3. Proposed exact internal V1 seam

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

These six operations are the complete proposed callable bridge, not a general
command processor or service locator. Dependencies must name the same run, distinct
nonnegative authored ordinals, exact installed workflow bindings and the node's
persisted direct dependency list; reject a forged/stale contract before mutation.
The owning runtime must resolve the registered typed implementation without passing
untyped workflow objects or reflecting into Dag. No hidden public factory is added.

## 4. Identity and lineage — explicit proposal for review

Use the persisted runtime-created DagRunId GUID as the DAG aggregate's lineage
anchor InstanceId by exact GUID conversion, not as a newly started workflow. The
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
set its last hex character to `1`. The provider-global start key is
`orcacore-dag-child-v1:<full digest>`. Use the full coordinate and exact binding/input
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
provider framing in MappedInputFingerprint. Exact binding/type/codec information is
stored separately. Start receives those bytes without invoking the mapper again or
re-encoding a live object. Losing physical mapping attempts have no commit authority.

One expected-version DAG-owner commit contains the node input bytes/fingerprint,
deterministic child binding, admission decision and its internal child-start outbox
intent. Validate readiness and the host ceiling at that serialized decision. A child
can neither start nor consume an admission without that committed intent. Replays
with an existing committed input return it, ignoring a newly evaluated mapper result;
never overwrite it. A malformed/stale binding is an integrity conflict. MappingInvalid
commits no input/start intent; the separate DAG progression transition records node
failure and DependencyBlocked closure, leaving independent nodes eligible.

ProviderCommitBatch is a single-stream boundary. Starting the child is a second
atomic child-stream commit with its complete definition/input binding, stable start
key, ParentInstanceId/RootInstanceId, start/checkpoint/projection and continuation.
These two commits are connected by a retryable outbox intent, NOT an asserted
atomic multi-stream write. A lost start response reattaches the same identity after
checking full binding; a collision never returns another workflow's child. The
same parent intent remains reclaimable until its result is durably recorded.

## 7. Unified dispatch, join and cancellation

Use the existing logical provider outbox and its internal dispatcher for V1
`dag-child-start-v1`, `dag-child-join-v1` and `dag-child-cancel-v1` kind selectors.
They are runtime continuations, not workflow-authored Publish events. Continue
claims remain restricted to `continue`; the public event pump remains restricted to
`workflow-event`; internal claims exclude both. No DAG-private queue/store, application
dispatcher callback, provider SDK dependency or new persistence port is proposed.
V1 payloads contain persisted coordinates/bindings only, not CLR assembly-qualified
type names or opaque delegates. Unsupported versions fail visibly rather than coerce.

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
2. Implement 8.4's exact internal seam, deterministic identity/lineage and typed
   engine codec/start adapters in a source target, without expanding either DAG friend.
   Review the exact metadata reference allowlist as part of that target.
3. Implement 8.5's persisted DAG-owner progression, atomic mapped-input/outbox
   transition, kind-partitioned dispatch, join/recovery and competing-host behavior.
   Shared atomic infrastructure may land with 8.4 if the source request names it;
   no completion claim is justified by an in-memory bridge mock.
4. Keep 8.6 public registry/run/query/wait/cancellation wiring and 8.7 admission
   behavior as their reviewed slices. All required compiled exits remain owned by 8.10.

The runtime-view closeout's two later guard notes are carried forward, not silently
claimed fixed here: forbid UnsafeAccessor and System.Linq.Expressions in Hosting
and the linked harness; add DagRuntimeView.cs to permanent executable evidence paths.
The 8.4 source target must address both before claiming shipped-assembly bridge proof.
The old doc 08 CP-022 and doc 17 §17.2.6 sentences saying null handling is not implemented are stale
relative to the approved runtime-view source; reconcile that current-status sentence
and its pins in the next reviewed source/document target, never an activation commit.

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
