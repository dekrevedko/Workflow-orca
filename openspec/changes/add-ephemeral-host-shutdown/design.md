## Context

Issue #2 contract candidate on master `fa88986aaac219c998b15e96748efdf2f245de51`,
after the two-parent PR #7 merge. No product implementation is included. Exact
declarations and public/hosted validation precedence are in
`artifacts/task-2-1-public-api-2026-10-19.md`. The contract checkpoint and approval
are 3c8d33c and 4bd7c3e; this transition applies their required F2 wording correction
and still awaits canonical review. No source behavior or shipped API is claimed.

The current readiness hosted service has a no-op StopAsync; engine disposal stops
timers but does not close facade admission or join work. A start holds `startGate`
while awaiting its execution boundary and can be running application code without
a returned handle. `EphemeralInstanceLifetime.WhenSettled` is replaceable when work
is acquired again. The instance lanes, timer dispatcher and retention sweeps do
not provide a stable host-wide join. Caller output-wait cancellation is local.

## Goals / Non-Goals

**Goals:** irreversible Closed/drain then Stopping/join phases; cancellation only in Stopping; stable physical
and callback settlement; explicit Completed/Incomplete observations; retained
capacity and dependency ownership while stuck; independent lifetimes per root.

**Non-Goals:** process/native-thread killing, forced GC, durable changes, exactly-once
external effects, a new termination command, interception of arbitrary provider
disposal, a hidden service provider, or an unbounded implicit Dispose wait.

## Decisions

### 1. One minimal public service; fixed cooperative-cancel policy

The existing role registers one reserved owned singleton
`IEphemeralWorkflowHostLifetime`; it is not IDisposable and does not own the root
provider. Its sole StopAsync takes an observer timeout and token. There is no new
drain option or API: Closed permits draining accepted work, and Stopping always
requests cooperative cancellation of remaining work. Result classes are runtime-constructed, immutable and
closed; no per-instance payload/identity list or replacement history is retained.
Two exceptions distinguish host-stopping admission from incomplete hosted stop.
Exact defaults, signatures, validation, enum values and codes are exhaustively
listed in the API artifact and the surface delta.

### 2. Close first, outside the facade semaphore and every instance lane

Per the 2026-10-19 F1 owner decision, state progresses Open -> Closed -> Stopping
-> Completed. ApplicationStopping produces only Closed. Every gate reads the token
state directly, so another blocked stopping callback cannot leave new-start or
registration admission open. The bounded engine callback only closes; it neither
queues cancellation nor starts a join. Accepted-instance operation/attempt gates
distinguish Closed from Stopping and continue ordinary work in Closed. Raw abortive
disposal likewise only closes, without acquiring shutdown authority or promising
a drain; destroying the provider remains unsupported premature teardown.

Close is atomic under the existing visibility boundary, without a facade semaphore,
instance lane, enumeration, serialization, cancellation or user callback under the
gate. Only an admitted public StopAsync or the engine's own hosted StopAsync enters
Stopping, closing first if still Open. Stopping acquires one shared cancellation/
producer-sealing/join authority before releasing the gate; all work is queued outside.
No new configuration, timeout, close method or result is introduced.

Start ownership precedes input serialization and startGate queuing. A queued
start losing its initialization-admission recheck to Closed runs no Init. Per F2
in the contract approval verdict, a start that passed initialization admission
before Closed proceeds normally and may publish Accepted during Closed, even if
its first step/Init body begins or returns later outside the gate. Closing does not
abort it or manufacture a host-stopping error after its effects. The drain owner
can await pending initialized starts and then their handles normally.

Acceptance and entry to Stopping share the final visibility election. Acceptance
winning Stopping publishes Accepted once; remaining workflow work is then cancelled
separately. Only a start still unaccepted when Stopping wins is independently fenced
to Terminated through its lane, preserving immutable prior status and authoritative
original error/caller cancellation, otherwise reporting host-stopping. Physical
capacity is retained until actual settlement in both retention modes. Initialized
provisional work can continue its normal admission path during Closed; neither
abort nor cancellation is caused by that close. All pre-handle work remains owned.

Closed refuses all new/replayed/conflicting starts and compatible registrations
with the already reviewed validation/static-compatibility precedence. It changes
no accepted-instance query, new output wait, command, ingress, delay, timer, retry,
resource grant, yield or retention behavior. They can admit work and commit normally
during drain, with existing retention/not-found races and ordinary local cancellation.
Closed itself neither cancels tokens nor seals those producers. Registered-handle
identity lookup remains passive. Closure does not expire keys or make replay safe.

At Stopping, preserve original-ID absence/retirement and identity/state-type mismatch
precedence using metadata only, then reject new live instance-handle acquisition and snapshot/state/output queries,
output waits, cancellation/termination and ingress as host-stopping. Queries may
decode; waits allocate pins; commands/ingress dispatch user/lane/commit work, so all
are sealed for a finite stable-zero proof. Already-admitted operations, including
those admitted in Closed, finish normally and stay joined. The exact exhaustive
operation table and passive post-Completed exceptions are in the API artifact.

### 3. Stable host join, not a snapshot of instance tasks

Own each admitted registration/start, external command/query/wait and lane turn; each physical
root/branch/item attempt and capacity-release transfer; each retry/resource/yield
continuation; cancellation dispatch and entered handlers; every workflow/step-
timeout/stuck-step timer callback and dispatcher; each retention sweep and cleanup;
and final commit/notification work. Count queued/pre-Init starts and entered timer
callbacks that have not found an instance yet. Successor ownership is acquired
before predecessor release; never create a zero-count gap during a handoff.

Stopping seals business producers, cancels scheduled work and joins callbacks
already admitted, then completes required cancellation/abort commits and cleanup.
Closed still permits accepted-instance attempts, retries and resource grants.
No new business attempt/Init is admitted once Stopping begins. An invocation whose
own initialization/attempt admission won before the applicable boundary (Closed for
new initialization; Stopping for already-initialized or accepted work) can begin or return later and remains owned; do not promise impossible wall-clock
ordering of user code outside the gate. The internal terminal-fence/cleanup lane
remains available until drained.
Disposing a timer alone is insufficient; join entered callbacks, underlying timer
disposal where supported, dispatch and handoffs. A late rejected trampoline may
only check the sealed gate and return; it cannot touch workflow/dependency state,
notify, rearm a timer, acquire new owned work or commit after Completed.

Completed is a one-time stable-zero proof after every producer is sealed. It is
independent of registry size, logical terminal states, retained payloads and
individual WhenSettled snapshots. Keep the coordinator authority until final
cleanup and observer notification are safely published. Afterwards the result and
observer continuations carry detached data only. After Completed, the operation
table permits only passive immutable identity/error checks, registered-handle
identity lookup and cached detached shutdown results. Live queries (including
terminal output), waits, commands and ingress fail as stopping: no DI access,
user code, owned work, timer, notification/diagnostic dispatch or commit can start.
Already detached values are caller-owned. Retained metadata is frozen until ordinary
root disposal; retention cannot rearm after Completed. Disposal releases joined
storage without restarting engine work. This metadata is not outstanding work.
Stop observers and the coordinator's root reference are not quiescence obligations:
otherwise the operation would wait for itself. They own only local observation
resources; cancellation/sealing jobs and all dependency-using handoffs are counted.

### 4. Cancellation callbacks and faults have owners

Cancellation signalling is queued and owned, not executed on the public StopAsync,
ApplicationStopping or system-timer thread. Retain and join the **first** cancellation
dispatch/handler task; repeated CancelAsync on an already-cancelled source can
complete before its first handlers return. Do not use that second task as evidence.
Linked-token propagation, token-registration disposal and cancellation initiated by
other runtime paths must be covered by the same accounting. A permanently blocked
handler keeps its ownership and yields an incomplete observation; it cannot block
the observer's deadline by being called inline.

Contain throwing application handlers at their owned asynchronous/timer boundary,
continue independently cancellable instances, finish required owning-lane fences
and observe exceptions. A returned throwing callback is settled work; it does not
by itself forbid Completed when every obligation actually joined. Unexpected loss
of an accounting/producer-seal proof retains a fault obligation and returns
ShutdownFaulted; no later observer can convert that unproven root to Completed.
No attempt to force-stop a body or fabricate capacity release is permitted.

Use the existing `orca.lifecycle.events` instrument only, fixed origin
`host-shutdown`, durable=false and fixed event names `shutdown-incomplete`,
`shutdown-fault`, `shutdown-callback-failed`, `shutdown-abortive-disposal` as applicable.
Diagnostics are best effort, contained, without payloads, keys, instance IDs or
exception text in metric labels. There is no new instrument or logging dependency.

### 5. One shared operation, independent bounded observers

The exact API artifact defines timeout range, zero/infinite behavior, pre-cancelled
no-effect public calls, monotonic budgets and decision precedence. Admitted observer
cancellation returns Incomplete(ObservationCancelled), not an exception. Local
cancellation/timeout does not cancel other observers or the shared stop, reopen
admission, drop handler ownership, expire a key or release physical slots.
Later observations join the same operation and can return Completed after actually
stuck work returns. No polling is required: use notifications and bounded observation
timers; dispose local registrations/timers on every outcome. Store no history of
results or caller tokens and retain no completed observer list.

### 6. Explicit application ownership is the disposal boundary

The application owns the root/scopes/external dependencies. One application-owned
session/supervisor serializes disposal and keeps them alive on Incomplete. After
Completed, it awaits other hosted-service stops, then disposes. There is no
unconditional using/finally around an incomplete join and no reliance on GC roots.
Successful join proves all engine-owned dependency users ended; arbitrary external
users and premature concurrent root disposal remain outside this contract.

Generic Host ApplicationStopping is directly checked at every gate but establishes
only Closed; accepted work is allowed to progress despite that token. Initialize
the coordinator before subscribing. The engine hosted StopAsync enters Stopping
even with an already-cancelled supplied token (that token only bounds observation),
returns normally only on Completed and otherwise throws the typed incomplete result.
HostOptions owns the host budget; observer-local cancellation never cancels the join.

The API artifact defines three concrete drain recipes. For ordered Generic Host
draining, register the application IHostedService drainer after the engine and keep
ServicesStopConcurrently=false. Reverse stop order lets that service await its own
pending starts/accepted handles while Closed, including new waits and required event
delivery, before the engine starts cancellation. Use its StopAsync token, not the
already-cancelled ApplicationStopping token. Drain expiry ends only the local wait
and returns control to the engine; all phases share HostOptions.ShutdownTimeout.
Keep services/dependencies needed for drain alive and stopped after the drainer.
With concurrent stops the engine can enter Stopping during drain, so that ordering
cannot guarantee a drain window. Explicit IHost ownership and catch/reobserve still
protect Incomplete; unrelated premature service disposal is never made safe here.

An explicit Generic Host owner can instead stop submitters and await its pending
starts/handles before calling this StopAsync, with no concurrent engine/IHost stop;
ApplicationStopping alone may establish Closed without cancellation. A plain-DI
owner closes its own submission gate, joins submitters/pending starts and awaits
accepted handles, keeping ingress/dependencies alive, then calls StopAsync to close
and cancel/join anything left. There is no plain-DI close-only engine API: engine
admission remains Open during this app-controlled drain, so all submitters must be
controlled. In every recipe a bounded drain expiry proceeds to StopAsync; Incomplete
retains the actual host/provider/scopes, and only Completed permits final disposal.

Ordinary Run/RunAsync now surfaces incomplete hosted stop as an exception (possibly
aggregated), replacing the old clean no-op stop, but still disposes in finally. It
cannot preserve dependencies for incomplete/noncooperative work. Explicit retained
IHost.StopAsync with catch/reobserve is supported; unconditional using/finally is
not. Raw provider disposal starts neither cancellation nor join; it only closes
admission and records a bounded best-effort diagnostic before unsupported abortive
teardown, with no safe capacity/completion claim or interception of external disposal.

### 7. Normative and implementation transition

Six ADDED blocks (surface 2, management 4) introduce this host lifecycle without
reopening deferred workflow destructive commands or altering durable semantics.
The routing artifact enumerates numbered owners, guides, acceptance cases and
every individually classified occurrence. Future canonical sync copies these exact
blocks and adds the permanent proposed-to-source-pending registry records only
after real approval/checkpoint evidence exists. Current registry/API/compile/package
baselines are unchanged. Source task 3.3 changes executable baselines with source.

## Risks / Trade-offs

- An incomplete observer cannot preserve a provider somebody else disposes ->
  explicit serialized application ownership, framework-only counterexamples and
  later public-package dependency-sentinel qualification; no imaginary root lease.
- Noncooperative work/callbacks can outlive every finite budget -> keep truthful
  ownership and services, return Incomplete, provide no forced kill or hard real-time claim.
- Stop races with accepted start/retirement -> one visibility boundary and original
  ID error precedence; stopped admission is never a safe-effect retry instruction.
- Incorrect handoff/producer sealing can create false zero -> ownership-preserving
  transfers, fake-clock/barrier tests and a fail-closed accounting-fault obligation.
- Host stop is now meaningful and RunAsync can throw on incomplete work -> document
  the changed exception/disposal behavior, ordered drain recipes and abortive disposal;
  normal running defaults, fingerprints and retention settings remain identical.

## Migration Plan

1. Independently review the complete uncommitted contract/provenance target with
   strict OpenSpec, Release -warnaserror and Infrastructure green; recheck drift and
   checkpoint only after approval, followed by a verdict-only child.
2. Review/checkpoint one canonical/numbered/registry transition and binding before
   source. No product implementation is authorized by a green pending record.
3. Prepare one coherent test-first green source/consumer target. Do not commit an
   unfiltered expected-red baseline. Change all exact executable baselines with
   implementing source; run actual CI commands and proportionate stress/coverage.
4. After independent implementation approval/checkpoint and publication authority,
   push/open the PR, preserve merge ancestry and leave merge/issue closure to the owner.
5. In a later separately reviewed maintenance target, archive the completed issue #1
   retention change and refreeze every active-path/provenance guard affected by moving
   it. Do not archive it or borrow its approval in this contract remediation.

## Open Questions

None for this contract candidate. Independent review must approve or reject these
concrete choices before canonical synchronization or any source implementation.
The retained initial assessment and contract artifacts describe their dated stages,
not source authority. The F2 correction is reviewed with this canonical transition.

## Framework grounding

Pinned Microsoft 10.0.9 sources checked for this design:
[Host](https://github.com/dotnet/runtime/blob/v10.0.9/src/libraries/Microsoft.Extensions.Hosting/src/Internal/Host.cs),
[RunAsync](https://github.com/dotnet/runtime/blob/v10.0.9/src/libraries/Microsoft.Extensions.Hosting.Abstractions/src/HostingAbstractionsHostExtensions.cs),
[DI disposal](https://github.com/dotnet/runtime/blob/v10.0.9/src/libraries/Microsoft.Extensions.DependencyInjection/src/ServiceLookup/ServiceProviderEngineScope.cs),
[HostOptions](https://github.com/dotnet/runtime/blob/v10.0.9/src/libraries/Microsoft.Extensions.Hosting/src/HostOptions.cs),
and [CancelAsync](https://github.com/dotnet/runtime/blob/v10.0.9/src/libraries/System.Private.CoreLib/src/System/Threading/CancellationTokenSource.cs).
Host.cs StopAsync reverses registration order, supplies one shutdown-budget token
and awaits services sequentially unless ServicesStopConcurrently is enabled; the
ordered drainer recipe relies on those exact behaviors. This is framework grounding,
not implemented engine drain evidence. The five earlier framework probes remain
unchanged; future task 3.4 must prove the actual recipe with candidate packages.
Host stop and provider disposal are separate. RunAsync disposes in finally; DI walks
captured disposables in reverse order. CancelAsync schedules existing callbacks,
but an already-cancelled source can return a completed task while prior callbacks
remain active. The framework-only probe artifact checks these ownership limitations;
it contains no OrcaCore shutdown implementation or acceptance credit.
