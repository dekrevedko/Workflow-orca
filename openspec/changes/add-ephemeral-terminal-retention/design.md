## Context

This is the unapproved R1 remediation of the rejected contract for [issue #1](https://github.com/dekrevedko/Workflow-orca/issues/1). The target deliberately remains based on `370b239402c8974a45fc0a54f539a823ab470cf4`, the original synced default-branch commit examined by the reviewer; it does not claim to be current `master`. Its branch is `issue/1-ephemeral-retention`. The original request, assessment, route and rejecting verdict remain immutable evidence; the R1 request/route/provenance supersede their current-state claims. The issue's older `3c2cd96` snapshot is background only.

Current source evidence:

- `src/OrcaCore.Engine.Ephemeral/Facade/EphemeralWorkflowFacade.cs` owns separate `starts` and `instances` dictionaries. `StartCoreAsync` retains exact definition/input fingerprints indefinitely; public handles capture `InstanceBinding` and delegate queries back to the engine. Starts are serialized through `startGate`; dictionary access uses `gate`.
- `EphemeralWorkflowEngine.EvictInstance` and `EvictTerminalInstances` inspect terminal status and remove only the runtime registry entry. They do not retire the facade dictionaries. Reusing these helpers alone would leave a key that reopens a handle to missing runtime data.
- `Execution/InMemoryInstanceRegistry.cs` retains the full runtime instance, including state/output graphs. The `IWorkflowInstance` seam currently exposes no complete physical-settlement proof.
- `Execution/InMemoryExecutionStateAdapter.cs` can fence an attempt at timeout while its CLR task remains incomplete. `GovernanceLease.RetainUntil` preserves physical capacity until that task returns. Retention needs per-instance ownership tracking across all invocation paths, including unthrottled attempts; terminal status and a zero governance-slot count are insufficient evidence.
- `WaitForOutputAsync` subscribes to `InstanceCommitted` before an authoritative output recheck. Retirement must preserve that no-missed-wakeup contract.
- `EphemeralEngineHostOptions` currently has only structured execution and transient pools. Hosting validates/copies those values and fingerprints them for duplicate-registration agreement.

The normative source map routes the affected capabilities to documents 09/15 and 10/17. Both canonical capability specs, the numbered requirements, and the exact declaration companion remain unchanged by this contract target. Its provenance fixture, current provenance artifact, directory inventory and exact guard-source pins are refrozen together so the contract checkpoint can have a green Infrastructure lane while semantic approval remains false.

## Goals / Non-Goals

**Goals:** Provide explicit count/age retention for settled terminal ephemeral instances and their facade/start records; keep default host-lifetime retention; define stale handles, replay, payload ownership, and concurrent operations precisely; provide testable acceptance criteria and a package-only measurement plan.

**Non-Goals:** Active-instance eviction, durable storage/retention, public purge/archive/history/evict operations, a new engine/management facade, new friend access, serializer hooks, forced GC in product behavior, host-wide admission limits, cooperative host shutdown (#2), AOT support (#3), or proof of arbitrary external side-effect cleanup.

## Decisions

### 1. One opt-in immutable host policy

The following is the complete proposed public surface addition. All names are in namespace `OrcaCore.Hosting`, assembly/package `OrcaCore.Engine.Ephemeral`:

```csharp
// One additional member on the existing class; all existing members stay exact.
public sealed class EphemeralEngineHostOptions
{
    public EphemeralTerminalRetentionPolicy? TerminalRetention { get; init; }
}

public sealed class EphemeralTerminalRetentionPolicy
{
    private EphemeralTerminalRetentionPolicy(
        int maxRetainedInstances, TimeSpan maxAge, TimeSpan sweepInterval);
    public int MaxRetainedInstances { get; }
    public TimeSpan MaxAge { get; }
    public TimeSpan SweepInterval { get; }
    public static EphemeralTerminalRetentionPolicy Create(
        int maxRetainedInstances, TimeSpan maxAge, TimeSpan sweepInterval);
}
```

`TerminalRetention = null` (including omission) preserves existing host-lifetime retention and creates no retention timer. The factory rejects `maxRetainedInstances < 1`, nonpositive `maxAge`, and `sweepInterval <= 0`, greater than `maxAge`, or greater than `TimeSpan.FromDays(1)`, with `ArgumentOutOfRangeException` naming the invalid parameter. The one-day sweep ceiling ensures a portable bounded timer interval without constraining the maximum retained age. Validation checks parameters in signature order. No zero/immediate-eviction or infinite sentinel is supported; omission disables the policy.

The null default governs accepted instances; unaccepted aborted starts still receive mandatory cleanup under decision 4. Validation has no minimum interval beyond positive time: one tick is valid. Such tiny values can keep sweeps continuously busy and consume CPU; guides must recommend choosing an interval suitable for the host without clamping an accepted configuration. This documents the accepted surface without introducing a new validation floor.

Registration validates immediately, freezes owned values, and includes all policy values and the disabled/enabled distinction in the existing host-options fingerprint. Equal values are equivalent even across different policy objects; any changed value conflicts before service mutation. No new registration overload or mutable options monitor is proposed. Automatic policy is preferable to a public purge method because it keeps lifetime guarantees host-owned and avoids introducing bulk/destructive management.

### 2. Settlement is an internal prerequisite

A published instance is eligible only when its status is `Completed`, `Failed`, `TimedOut`, `Cancelled`, or `Terminated`, all runtime-owned root/branch/item attempts have physically returned, timer/wait/continuation callbacks and commit lanes for it have drained or been definitively detached, and no accepted operation currently holds a transient read/wait pin. Ownership must be registered before dispatch and removed in `finally`; failure, timeout, cancellation, retry, and synchronous completion must not lose ownership facts. A callback still able to commit or republish the instance prevents settlement.

An accepted instance that is active, waiting, delayed, cancellation-requested, or logically terminal with unsettled owned work is protected indefinitely. Unaccepted provisional work follows the abort fence and settlement cleanup in decision 4. Retention never releases a physical throttle/pool slot, cancels an accepted workflow, forces a task to stop, or asserts that application-spawned external work stopped. Application work outside the runtime-owned attempt task remains an integration trust boundary. No global memory bound is promised for unbounded active/stuck work, registered definitions, caller-held copies, or unbounded individual payload size.

### 3. Count and age select the same retirement unit

One unit includes the runtime instance and its retained state/output/failure bytes, facade instance binding, exact start binding, retention bookkeeping, obsolete instance-owned routing/timer/notification entries, and every per-instance entry in `EphemeralWorkflowEventRouter.accepted`, its `(InstanceId, EventId)` deduplication map. Definition registrations remain installed. No permanent retired-ID set or start-key tombstone may grow with completed-job count. Tests must include event-acceptance fingerprints/payload references, not just registry counts.

Record `settledAt` once, using the host `TimeProvider` monotonic timestamp, when runtime settlement and facade publication are both complete. Later reads/replays do not refresh it. Assign a strictly increasing host-local settlement ordinal for total ordering, including simultaneous timestamps. Temporary pins suspend eligibility without changing either value.

On each publication/settlement or final pin release, and on a non-overlapping periodic sweep, first retire unpinned units whose elapsed age is at least `MaxAge`; then retire oldest remaining eligible units by settlement ordinal until their host-wide count is at most `MaxRetainedInstances`. Count is shared by all definitions in the host. Enforce the count before the triggering lifetime operation completes. With a running, schedulable host and no pins, age retirement occurs on the first sweep at/after the deadline, no later than one sweep interval plus actual scheduler delay; wall-clock jumps do not extend or shorten elapsed age. This is a maximum age, not a guaranteed minimum retention window: count pressure can evict sooner. Lookup/replay uses the currently retained set; elapsed age alone does not expire a binding before an actual retirement linearizes.

Protected and transiently pinned records are additional to the eligible-terminal count. When all work and admitted read/wait operations settle, every owned per-instance collection must plateau at the configured count, and idle sweeps must release age-expired units without another start. Each retirement completes ownership release before reporting success; no secondary queue or diagnostic history may conceal an unbounded backlog of retired payloads.

### 4. Public visibility and ownership change together

Introduce an internal lifetime coordinator in the ephemeral package. Admission of a start replay, facade query/mutation/wait, and retirement share one linearization boundary for unit visibility. A read/wait pin admitted first makes the unit ineligible for retirement until the result is detached and its pin released. Retirement admitted first removes the complete unit/key visibility and rejects subsequent pins; because the selected unit is already unpinned, it has no admitted readers to drain. Runtime cleanup cannot save the removed instance back into the registry. Do not hold a lifetime lock while invoking user steps, awaiting task settlement, or performing fixed-codec copies.

The current separate dictionaries are implementation details, not independently removable stores. Pinning/retiring protocol must be reviewed before implementation; callers must never observe an existing start binding with absent instance data or an absent binding while old work can still commit. The old internal eviction helpers must route through the same safety protocol or remain unreachable from automatic retention; do not export them.

Synchronous completion before `StartCoreAsync` publishes the binding must produce one coherent unit before retention can see it. Each provisional start has one acceptance-or-abort election. Caller cancellation observed after runtime creation but before atomic facade acceptance counts as abort, as does an exception escaping after `Init` before acceptance. Before surfacing the original cancellation/exception, fence a nonterminal provisional unit through its owning mutation lane to `Terminated`, using cleanup authority independent of the cancelled caller token; preserve an already committed immutable terminal status. It is never facade/key-bound or application-handle-reachable, stays outside the accepted-terminal budget, and is released eagerly after physical settlement with either enabled or disabled retention. Do not await physical return under the lifetime lock or treat the fence as physical stop. If acceptance wins, return `Accepted`; subsequent start-token cancellation cannot abort that accepted workflow. No unreachable `Running` provisional unit may remain after abort handling. There is no successful-start/idempotency promise for an aborted start; its exception does not prove absence of side effects or permit an effect-safe retry.

### 5. Existing handles are identities, not lifetime leases

| Operation | Admitted before retirement | Begins after retirement |
|---|---|---|
| `GetInstanceAsync` | Returns a handle; possession alone does not retain data | `WorkflowInstanceNotFoundException` |
| Snapshot/state/output query | Completes from a pinned, coherent, codec-detached value | `WorkflowInstanceNotFoundException` |
| Output wait | Pins until it captures detached success or terminal failure; no polling; cancellation releases only its local pin | `WorkflowInstanceNotFoundException` |
| Cancellation/termination | Existing terminal outcome through the owning lane; pin releases afterward | `WorkflowInstanceNotFoundException` |

Not-found retains code `WF-INSTANCE-NOT-FOUND` and the requested original `InstanceId`; no new error type or code is proposed. A retained instance belonging to another definition continues to raise the existing definition-mismatch error; a retired ID is absent for every definition. A stale handle can never target a replacement instance. Caller-owned detached snapshots/output and already completed wait results remain valid after eviction. Keeping a handle object alone must not root payload/runtime instances.

Wait pins are acquired with subscribe-then-authoritative-recheck admission. Terminal notification processing captures the detached result before releasing the pin, so eviction cannot strand a pre-existing waiter or turn its terminal result into not-found. Each waiter releases exactly once on completion, error, or local cancellation. A cancelled token checked before admission creates no pin; other completion/cancellation races retain the existing local-wait semantics. New calls through the same handle after eviction fail normally. A returned `StartOrGetAsync` handle is not an implicit output-wait subscription; callers opting into bounded retention must begin a wait while the instance remains retained.

Count pressure can retire a completed unit after `StartOrGetAsync` returns but before `start.WaitForOutputAsync` or a direct wait is admitted. That wait then throws not-found even though the job ran; retrying its expired key starts a fresh run and may repeat side effects. The management delta states this consequence explicitly, task 4.3 requires the barrier-controlled test, and task 2.1 requires guide advice: not-found after `Accepted` is not evidence that a run never started, and choose `MaxRetainedInstances` above the completions possible during the caller's start-to-observe gap. A larger bound reduces exposure but is not an effect-safety guarantee.

### 6. The key's guarantee ends at atomic retirement

While a unit is retained or protected, exact definition/version/fingerprint and fixed-codec input reuse returns the same ID with `WasExisting = true`; incompatible reuse returns the existing typed conflict. Reads and replays are not LRU refreshes. Once retirement removes that unit and its key together, the key is unbound: a subsequent valid start through any registered definition may create a fresh runtime-minted ID with `WasExisting = false`. Concurrent replay/start/retire has one of these ordered outcomes, never a partial removal or two new instances for the same current key.

Retirement ends only OrcaCore's host-local start deduplication window. It never authorizes repeating a physical side effect; applications requiring longer deduplication retain their own effect IDs/store or choose durable workflows. A retained/protected old unit cannot lose its key merely because an age/count target was exceeded.

### 7. Contract reconciliation and source gates

Use the five uniquely named `ADDED` requirements in the two existing capabilities. The rejecting verdict accepted their ownership and confirmed that automatic host policy does not modify deferred destructive command rules. No existing canonical block is rewritten and no separate process-amendment target or successor exception is needed. Include the complete provenance prerequisite in this contract target, with semantic approval false and all five pending operations assigned to task 2.2.

This is a new ADDED-only change entering its own open contract/sync gates; it has no `ApprovedPending` or `Complete` row before approval and cannot cite the old selected-v1 approval as approval for retention. Under `CLAUDE.md`'s late-decision obligation it will need permanent records for the two capabilities at task 2.2, after this change's actual 1.3 approval and 1.4 checkpoint evidence exist. Those records must bind all five exact identities, numbered targets, acceptance tasks, source tasks and final evidence. The current `post-gate-amendment-path.json` remains unchanged; it does not accept a fabricated approval or a proposed duplicate-owner waiver. The proposed R1 route explicitly distinguishes current provenance refreeze from that future permanent-record transition.

| Target | Required disposition before source work |
|---|---|
| `developer-facing-surface` | Add exact policy/host-ownership requirement; preserve hosting entries, codec, packages/friends |
| `management-and-querying` | Add four retention requirements; preserve ordinary handle signatures and deferred destructive commands |
| `workflow-contracts`, `state-driven-runtime`, `runtime-resource-governance` | Check notification/typed-output, immutable terminality and physical-slot rules; no intended amendment |
| `repository-foundation`, Package topology and ownership are exact | Ownership reference to doc 17's namespace/assembly table applies; add the policy to the ephemeral row at 2.1/2.2, no capability-block change |
| docs 03/04 | Clarify retained-instance lifetime and settlement terminology without adding a status |
| docs 03 terminal-state paragraph; doc 09 MG-050/053 | Qualify quick memory eviction/durable inspectability as durable-activation behavior; accepted ephemeral instances default to host-lifetime retention, with opt-in settled-terminal retirement |
| docs 09 MG-003/010/011/020/050–053 | Distinguish ephemeral terminal retirement from durable activation eviction and deferred public commands; retain best-effort internal `evicted` observation after retirement, without public archive/delete commands or events |
| docs 10 and 17 matrix §17.4 + namespace/assembly table | Record package ownership, complete existing host-options shape plus policy addition, defaults/validation, idempotency/handle lifetime, and retained-count versus execution/DAG capacity taxonomy |
| doc 17 declaration companion | Explicitly exclude: its current scope contains builders/authoring, no host-option declarations. Keep it byte-exact; do not add an isolated policy type without the surrounding hosting contract |
| docs 11 NF-031, 12, 15 | Qualify any active-instance-cap wording as durable active-memory residency, never an ephemeral host-wide admission ceiling; retained-terminal count is separate. Add deterministic acceptance cases and low-cardinality retention/protection observations |
| docs 13 future registry | Record the narrowly approved automatic policy while retaining public archive/purge/history re-entry criteria |
| docs 05/06/08/14/16 and other canonical capabilities | Reverse-sweep timer, durable retention, resource, DAG and external-effect references; no intended behavior amendment |
| affected guides/technical overview + normative source map | Explain opt-in use, early count eviction, stale handles, host-local deduplication limits; reflect source classification |
| provenance fixture/current artifact/directory inventory/source pins | Refreeze together inside this contract target; preserve all earlier provenance artifacts and superseded pins; Infrastructure must pass before re-review |
| permanent post-gate registry | Task 2.2 installs two exact records after real contract approval/checkpoint evidence exists; no present registry change or fake approval |
| executable public API/compile/package fixtures | Change atomically with the implementing source in 4.2, then validate/review in 4.5; leave them unchanged in 2.2 |

Canonical specs and public signatures stay untouched until independent contract approval and the reviewed canonical transition. An OpenSpec status of artifact-complete or a green Infrastructure lane is not permission to implement. The refrozen provenance fixture states the five pending additions and semantic ineligibility explicitly; a green structural/process guard certifies that the pending state is honestly recorded, not that canonical synchronization or source implementation happened.

## Risks / Trade-offs

- **Bounded keys permit later repeated starts** → explicit opt-in, stable defaults, exact retirement boundary, documented effect-idempotency responsibility.
- **Terminal tasks may still own physical work** → track per-instance root/branch/item tasks and callbacks regardless of governance configuration; fail closed on missing settlement proof.
- **Pins and stuck work exceed the eligible-terminal count** → expose the distinction in internal diagnostics and consumer evidence; never claim a byte/RSS ceiling or safe capacity reuse.
- **Read/notification/retirement deadlock** → deterministic barriers, one visibility protocol, no user code or codec work under the lifetime lock.
- **GC/process memory fluctuates** → measure retained metadata and payload reachability independently from allocation rate, managed heap, process memory, and host working set.

## Migration Plan

1. Complete process/provenance routing and independently review/checkpoint the draft contract and exact signatures; preserve the present selected defaults.
2. Review the canonical/numbered-document/signature-text transition as its own target, then synchronize both normative trees and record the permanent registry state atomically. Exclude the authoring-only declaration companion explicitly; leave executable API/compile/package fixtures for implementing source slice 4.2.
3. Implement the approved source in test-first, independently reviewed/checkpointed slices. First test failures must identify missing behavior, not incidental build errors.
4. Publish fresh packages to an isolated local feed and run an ordinary external consumer. Existing hosts omit `TerminalRetention`; resident hosts explicitly opt in.
5. Disabling the policy for a new host restores host-lifetime retention for that host. Neither rollback nor changing configuration can resurrect data already retired by an earlier host.

## Open Questions

The proposed names, positive count floor, one-day sweep ceiling, count-before-age-window trade-off, and not-found race rules are concrete review decisions. Independent review must accept them or replace them before source work. No approval, measurement result, new public implementation, or closure of issue #1 is asserted by this draft.
