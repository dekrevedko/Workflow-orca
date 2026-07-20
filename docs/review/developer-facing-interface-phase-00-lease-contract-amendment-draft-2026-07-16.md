# Durable resource contract amendment record (2026-07-16)

Status: **planning amendment applied; Phase 0 guard retargeting remains pending**.

This record resolves the two independent lease reviews and the two primitive-obsession
reviews. The approved text is now carried by the canonical requirements, the selected-mode
matrix, and the two coordinated OpenSpec changes. It does not change production source or
tests.

## 1. Outcome

The previous draft is superseded. In particular, OrcaCore does **not** expose an
author-supplied lease duration, expiry backstop, holder ID, or renewal callback. The baseline
does not periodically renew resource tickets.

The safe recovery model is:

1. deterministic exact-owner release is the normal path;
2. a pool-owned deadline marks and audits a ticket for owner-state review while retaining its
   capacity;
3. exact durable owner reconciliation keeps active or ambiguous ownership held and recovers
   only a causally proven released/never-committed obligation, or a terminal owner whose lease
   cleanup and protected-work stop/fence are also proven;
4. automatic elapsed-time reclaim of a logically active holder would require a separately
   approved renewal protocol **and** end-to-end fencing enforced by the protected resource.

Renewal alone is not safe and is not needed for the chosen state-based ownership model.

## 2. Decision record

| ID | Decision | Resolution |
|---|---|---|
| D1 | Structural node name | `AcquireResources`, matching the portable member it replaces; no third `AcquireLease` name |
| D2 | Descriptor | Factory-only immutable `ResourceLeaseRequirement` over `ResourcePoolName` and validated positive `int Units`; no positional record, mutable initializer, or defaultable `LeaseUnits` parameter |
| D3 | Static shape | `params ResourceLeaseRequirement[]`; copied and validated immediately |
| D4 | Dynamic shape | Selector returns `IReadOnlyList<ResourceLeaseRequirement>`; normalized selection commits before any provider mutation |
| D5 | Author TTL / renewal | Neither exists in the baseline; review/expiry policy belongs to each configured pool |
| D6 | Runtime identity | One runtime-generated protocol `LeaseObligationId` per instance generation, authored node, fiber occurrence, and scope-entry occurrence; it is not caller-supplied and `AuthoredLocation`/holder text alone is insufficient |
| D7 | Availability | Durable root and durable branch builders; absent from every ephemeral builder |
| D8 | Multiplicity | At most one pending or held durable acquisition along an active inclusive fiber ancestry; independent siblings may acquire |
| D9 | Multi-resource request | Non-empty, unique by `ResourcePoolName`, positive units, all-or-nothing; concurrently needed resources belong in one request |
| D10 | Selector semantics | Deterministic and side-effect-free; may retry only before selection commit; committed normalized selection is replayed |
| D11 | Release | Exact occurrence/ticket release on normal exit, cancellation, and failure before owner removal/parent resume; forced termination retains a fenced capacity-reserving quarantine until protected work is proven stopped |
| D12 | Capacity assertion | Exact state-based reservation/unit conservation and grant-time capacity admission; downward resize may create no-new-grants over-capacity debt; exact numeric restoration is only an isolated no-waiter/no-resize assertion |
| D13 | Continue-as-new | A pending or held occurrence is non-quiescent; granted-lease rollover is rejected without clearing lease state |
| D14 | External jobs | Exact `RunExternalJob` overloads remain reserved; its future resource bracket participates in the same ancestry rule |

## 3. Approved public lease shape

```csharp
public sealed record ResourcePoolName
{
    public ResourcePoolName(string value);
    public string Value { get; }
}

public sealed record ResourceLeaseRequirement
{
    private ResourceLeaseRequirement(ResourcePoolName pool, int units);
    public ResourcePoolName Pool { get; }
    public int Units { get; }

    public static ResourceLeaseRequirement Require(
        ResourcePoolName pool,
        int units = 1);
}

DurableWorkflowBuilder<TState> AcquireResources(
    params ResourceLeaseRequirement[] requirements);

DurableBranchBuilder<TBranchState, TResult> AcquireResources(
    params ResourceLeaseRequirement[] requirements);

DurableWorkflowBuilder<TState> AcquireResources(
    Func<TState, IReadOnlyList<ResourceLeaseRequirement>> requirements);

DurableBranchBuilder<TBranchState, TResult> AcquireResources(
    Func<TBranchState, IReadOnlyList<ResourceLeaseRequirement>> requirements);
```

There is deliberately no duration, `expiryBackstop`, holder key, public descriptor
constructor, renewal token, or raw-string pool overload.

## 4. Authoring and validation semantics

### 4.1 Local invariants

- `ResourcePoolName` follows exact ordinal case-sensitive scalar semantics, rejects null,
  empty, whitespace, and surrounding whitespace, and has no implicit string conversion.
- `ResourceLeaseRequirement.Require` rejects null/default pool and non-positive units.
- A static request is copied and rejects a null/empty array, any null element, and duplicate
  pools at the fluent call; configured-pool existence is checked at registration/startup.
- A dynamic selector is non-null. Its result is normalized under the same null-list, empty,
  null-element, and duplicate-pool rules and rejected before any pool effect when invalid.

### 4.2 Path-sensitive ancestry validation

The rule is implementable in the delivered graph/compiler. It is defined over runtime fiber
lifetimes rather than lexical node counts:

- `If` arms are analyzed independently and their possible lease states merge, so mutually
  exclusive acquisitions can be valid;
- a second same-fiber acquisition is invalid while the first may remain pending/held;
- a direct same-fiber acquisition in a repeatable `While` body is invalid because a later
  iteration can reacquire before fiber exit;
- a fresh structured child fiber in a loop may acquire when it terminates/releases before the
  next iteration;
- descendants inherit the ancestor-held prohibition;
- siblings are analyzed independently and may acquire concurrently;
- a parent may acquire after a child join only because terminal child release commits before
  parent resume.

The runtime repeats the inclusive-ancestry check before queue or pool mutation for hand-built,
legacy, or stale plans. It must inspect both persistent pending lease state and held active
tickets. Until `StepResult.AcquireResources` is removed, compiler validation alone cannot see
every dynamic acquisition.

### 4.3 Selector and provider commit boundary

The selector is not promised to execute physically exactly once. Before its normalized value
commits, a crash may cause deterministic reevaluation. After commit, replay and retries reuse
that value. No provider reservation or ticket mutation occurs before the selection commit.

An unavailable atomic request remains pending until it is granted or its exact owning path is
canceled, fails, or terminates. The baseline adds no acquisition-timeout overload; an enclosing
authored timeout follows normal owner-terminal cleanup.

Provider grants use an exact pending-commit reservation tied to the lease obligation. Guarded
work cannot start until the workflow acquisition fact/checkpoint commits and the reservation
is confirmed. Recovery confirms or cancels only from causal owner-stream evidence; ambiguity
stays held.

## 5. Recovery and reconciliation

Each ticket carries exact ticket ID, `LeaseObligationId`, owner occurrence, and provider
ownership generation. Confirm, cancel, release, and reconciliation compare-and-act on those
values so stale cleanup cannot release a successor occurrence.

When a pool review deadline is reached:

- record one durable/queryable expiry mark and retain held capacity;
- active/reconstructable exact owner: keep held and optionally schedule another review;
- queued or pending-commit owner: preserve/confirm or cancel only from causal evidence;
- committed release or never-committed acquisition: release the exact whole obligation once,
  audit recovered release, then make units eligible for waiters;
- terminal owner: release only when cleanup plus protected-work stop or end-to-end fencing is
  causally proven; bare terminal state remains capacity-reserving quarantine;
- unavailable/ambiguous owner: hold for operator;
- active owner with missing ticket/fence: raise typed `LeaseLost`; never continue or silently
  reacquire.

A host crash alone does not end logical ownership. Capacity becomes available through resumed
deterministic release, proven recovery, or an operator action that first prevents exact-owner
resume and confirms protected work stopped/end-to-end fenced—not simply because time passed or
provider metadata was deleted.

## 6. Race and capacity invariants

The serialized pool lane and one request identity make grant versus cancellation/failure/
terminal cleanup choose one durable winner. A canceled request cannot leave a ghost grant; a
concurrent grant is committed to the still-active exact owner, released once before normal
owner removal, or retained once in forced-stop quarantine.

Every release preserves acquisition identity and units. Queued/released/cancelled requests
reserve zero; pending-commit, held, expiry-marked, ambiguous, and forced-stop quarantine states
reserve their exact units. Each grant must fit configured capacity. A downward resize below
existing reservations records over-capacity debt, revokes nothing, and blocks new grants until
`max(0, reserved - configured)` returns to zero through exact release or upward resize and the
next whole request fits. Freed units may transfer immediately to an eligible waiter, so
`AvailableCapacity` is not required to equal a historical global snapshot. Tests use:

- isolated pool, no waiters, no resize: exact numeric pre-acquisition restoration;
- contended pool: exact ticket/unit release, conservation, and next-waiter eligibility.

## 7. Strong matching-value amendment

The useful primitive-obsession findings are also adopted:

| Concept | Approved type/role |
|---|---|
| Event matching | `EventName` across `Wait`, `WaitLong`, `StepResult.WaitForEvent`, routing, envelopes, and projections |
| Successful named completion | `WorkflowOutcomeName`; `End()` alone means unnamed |
| Authored branch declaration | `AuthoredBranchId`, distinct from internal composite branch/runtime identities |
| Durable pool | `ResourcePoolName` |
| Host-local transient pool | distinct `TransientPoolName`; method `WithTransientPool` |
| Durable start dedup | `StartIdempotencyKey`, distinct from `InstanceId`; provider-global baseline namespace |
| External-job author key | `ExternalJobKey` |
| External-job occurrence | runtime-generated `ExternalJobId` |
| Completion/failure dedup | existing `EventId reportId` |
| Application payloads | typed request/result values; protocol/provider seam owns serialized bytes and content type |

All strong matching types reject invalid/default values at every seam, preserve exact ordinal
case-sensitive equality, serialize as scalar strings, and have no implicit string conversion
or parallel raw-string overload. Provider certification must defeat case-insensitive default
collations. `FailureReason` remains free-form diagnostic text unless the approved external-job
policy introduces a separate `ExternalJobFailureCode` matching contract.

## 8. Phase 0 guard follow-through

The existing guard source still targets the superseded
`AcquireLease(id selector, ResourceLease selector, duration)` invention and must not be
reviewed as the final contract. The next guard-only remediation must:

1. retarget reflection/compile fixtures to the exact `AcquireResources` overloads and strong
   descriptor/name types;
2. flip durable nested lease authoring to positive while keeping transient pool negative;
3. add `If`/loop/ancestor/descendant/sibling/post-child-release compile cases and matching
   runtime defense;
4. prove invalid dynamic selection causes no provider mutation and committed selection is
   replayed;
5. compare exact obligation/fiber/scope/ticket/provider-generation identity on release;
6. add granted-lease `ContinueAsNew` rejection with provider capacity accounting;
7. replace "expiry lets a replacement acquire" with mark-retains-capacity, active-owner,
   ambiguous-owner, proven-orphan/release-gap, missing-fence, and operator-action cases;
8. separate isolated exact-capacity restoration from contended unit conservation;
9. retain the qualified `StepResult.AcquireResources` absence scan until task 5.4 removes it;
10. refresh expected-red counts from actual execution rather than predicting them.

Tasks 3.1-3.10 retain their prior review disposition. Task 3.11 remains rejected until these
guard changes are implemented and independently re-reviewed. Phase 1/task 4.0 must not start.

## 9. Reserved and out of scope

- the complete `RunExternalJob` generic overload/failure-policy/resource-bracket family;
- any automatic timed reclaim or resource-enforced fencing protocol;
- rate-based pools and non-FIFO priority policy;
- production source, provider migration, or guard implementation in this planning-only pass.
