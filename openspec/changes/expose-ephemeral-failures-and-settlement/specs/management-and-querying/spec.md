## MODIFIED Requirements

### Requirement: Management API is scope-oriented and fluent
The ordinary management interface SHALL use the exact typed definition and instance handles approved by the matrix. The common `IWorkflowDefinitionRegistry` SHALL expose all resultless/resultful ephemeral/durable registration overloads and return the matching typed handle through its closed `Registered` result or resolve an already registered exact typed reference through `GetRequiredHandle`; engine-mode mismatch or missing statically inspectable host capabilities SHALL return `HostIncompatible` before mutation. Exact-reference lookup SHALL never register and SHALL throw `WorkflowDefinitionNotRegisteredException` when absent or stale. Each definition handle SHALL remain the sole owner of typed `StartOrGetAsync` and `GetInstanceAsync`; each instance handle SHALL expose only detached snapshot/state queries, cancellation request, and termination, with typed output query added only by `WorkflowInstanceHandle<TOutput>`. State inspection SHALL remain separate from committed workflow output inspection.

The instance-handle allowlist SHALL additionally include the passive
WaitForSettlementAsync(CancellationToken) observation on already-issued ephemeral
handles. It SHALL grant no instance lookup, mutation, replay, payload retention or
provider authority. It SHALL not add a definition/global settlement method or
start-result settlement helper. Durable inherited calls SHALL reject as unsupported
without a provider request.

#### Scenario: Operator targets one workflow instance
- **WHEN** an operator selects a known instance in either execution mode
- **THEN** `GetSnapshotAsync`, `GetStateAsync<TState>`, optional typed `GetOutputAsync`, `RequestCancellationAsync`, and `TerminateAsync` provide the exact typed results and errors

#### Scenario: Application resolves a configured workflow
- **WHEN** runtime code supplies an exact typed reference installed by the host's staged definition batch
- **THEN** `GetRequiredHandle` returns the typed definition handle without registration, a cast, keyed DI, or another workflow client abstraction

#### Scenario: Operator looks for bulk querying
- **WHEN** an operator inspects ordinary v1 handles
- **THEN** engine-wide/definition selections, `Where`, list/count/statistics, and bulk mutation members are absent

#### Scenario: Settlement authority belongs to an issued handle
- **WHEN** a caller retains an accepted ephemeral handle after its start key later expires
- **THEN** settlement observation addresses only that original receipt; no new start, key lookup or replacement-instance access occurs

### Requirement: Typed committed root state and output are queryable
Both engines SHALL provide fixed-codec-detached root business-state inspection. Resultful definitions SHALL additionally expose `WorkflowOutputResult<TOutput>` as pending, available, or unavailable; resultless definitions SHALL have no synthetic output. `GetStateAsync<TState>` SHALL return the last committed root state and fail with `WorkflowStateTypeMismatchException` for another type. `WorkflowInstanceHandle<TOutput>` SHALL expose `ValueTask<TOutput> WaitForOutputAsync(CancellationToken cancellationToken = default)`, and the start-result helper SHALL expose the callable shape `ValueTask<TOutput> WaitForOutputAsync<TOutput>(this WorkflowStartResult<WorkflowInstanceHandle<TOutput>> start, CancellationToken cancellationToken = default)` after projecting the handle through `GetHandleOrThrow()`. Neither mode SHALL return live state, in-flight attempt copies, branch/item-private payloads, or checkpoint/provider DTOs as snapshot/state/output values. The explicitly permitted ephemeral wrapper InnerException is a separate application-exception reference, not a value-inspection API.

Workflow output waiting SHALL be notification-driven and race-free: register the terminal/output notification before an authoritative state recheck, complete from the committed output if terminality raced registration, and never poll. A terminal result without output SHALL throw typed `WorkflowOutputUnavailableException` carrying terminal status and optional `WorkflowFailure`. Caller cancellation SHALL cancel only the local wait and SHALL NOT request workflow cancellation or mutate runtime state.

Only an ephemeral output-unavailable wrapper for one elected business-step
failure MAY carry the original live exception graph in InnerException. That
application-provided graph can itself retain mutable application objects; it is
not a detached query result or a safe disposal/settlement proof. The engine SHALL
not inject live engine state, provider DTOs or DI services into that graph.
WorkflowFailure, including nullable ExceptionType text, SHALL remain detached and
authoritative. Snapshots, state/output values and typed branch/item failure data
SHALL never contain the live exception slot. Durable output-unavailable wrappers
SHALL not expose an original live exception.

#### Scenario: Durable business state is inspected
- **WHEN** a caller requests the registered root state type for a retained instance
- **THEN** management returns a detached value representing the last committed root business state

#### Scenario: Resultful workflow completed
- **WHEN** a caller queries a successful resultful instance through its typed handle
- **THEN** it receives the atomically committed `TOutput` and fixed outcome metadata separately

#### Scenario: Output commits while a caller starts waiting
- **WHEN** successful output commits before, during, or immediately after `WaitForOutputAsync` registers its notification
- **THEN** subscribe-then-recheck observes the committed output exactly once without polling or a missed wakeup

#### Scenario: Output wait is cancelled by its caller
- **WHEN** the caller cancellation token wins while output is still pending
- **THEN** only that local wait is cancelled, while the workflow receives no cancellation request and remains eligible to complete

#### Scenario: Workflow terminates without output
- **WHEN** a resultful workflow reaches a terminal status that has no successful output
- **THEN** `WaitForOutputAsync` throws `WorkflowOutputUnavailableException` with that terminal status and optional committed failure

#### Scenario: Structured branches contain private state
- **WHEN** active branch or item fibers hold payloads of the same or another CLR type
- **THEN** root-state and output queries never select those private payloads

#### Scenario: A snapshot is taken beside an original-exception output wait
- **WHEN** a failed ephemeral instance is inspected and its eligible output wait throws
- **THEN** the snapshot/failure values remain detached and only that wrapper's InnerException holds the original application exception; inspecting it does not prove settlement

### Requirement: Retired ephemeral handles have deterministic outcomes
Handles SHALL carry identity without extending retained payload lifetime. Operation admission and retirement SHALL share a linearization boundary. A snapshot/state/output query admitted before retirement SHALL pin a coherent unit until it captures a detached result. A pre-existing `WaitForOutputAsync` SHALL subscribe and authoritatively recheck under that protocol, retain an operation pin until detached successful output or terminal failure is captured, and settle without polling or a missed wakeup even if count/age pressure races notification. Completion/error/local caller cancellation SHALL release that pin exactly once; caller cancellation SHALL NOT cancel the workflow. Obtaining a handle alone SHALL NOT grant a pin to later calls. After retirement, `GetInstanceAsync` and every ordinary runtime query/command on an existing handle SHALL throw existing `WorkflowInstanceNotFoundException`, code `WF-INSTANCE-NOT-FOUND`, carrying that handle's original ID; no replacement instance SHALL be targeted. Previously detached values and completed wait results SHALL remain caller-owned and valid. For a retained instance, existing definition/type mismatch behavior SHALL remain unchanged.

WaitForSettlementAsync on an already-issued ephemeral handle SHALL be the one
passive exception to the post-retirement operation rule. Retirement already
requires terminal physical settlement; the original handle's receipt SHALL return
successful completion without runtime lookup, payload resurrection, a new pin or a
global tombstone registry. Obtaining a replacement handle by an old retired ID
SHALL still fail. The settlement observer SHALL not delay count/age retirement.
An admitted output waiter SHALL atomically capture the elected original exception
alongside its detached failure before releasing its pin; a later retirement SHALL
clear the engine's reference but cannot revoke caller-owned exception objects.

#### Scenario: A stale handle is reused after its key starts a new instance
- **WHEN** a unit has retired and a later start reuses its former key
- **THEN** ordinary runtime queries/commands through the old handle fail with not-found for the old ID, its passive settlement receipt remains completed, and the new handle addresses only the new runtime-minted ID

#### Scenario: Output notification races retirement
- **WHEN** count/age pressure selects a newly terminal instance with already admitted output waiters
- **THEN** each waiter captures its detached output or typed terminal failure before pin release and no waiter hangs or becomes spuriously not-found

#### Scenario: Caller cancels one output wait
- **WHEN** local cancellation wins for one admitted waiter
- **THEN** only its subscription/pin is released and the workflow and other waiters remain unchanged

#### Scenario: Read and retirement interleave
- **WHEN** a query races the retirement visibility boundary
- **THEN** it either completes from a pinned detached value or returns typed not-found, with no partially removed state or live payload reference

#### Scenario: Count pressure retires a result between start return and observation
- **WHEN** `StartOrGetAsync` has returned `Accepted` for a completed unit and enough other units settle to retire it by count before the caller's direct or start-result-helper `WaitForOutputAsync` is admitted
- **THEN** the wait throws `WorkflowInstanceNotFoundException` for the original ID even though that job ran and may have produced output or side effects; replay of its now-unbound key creates a fresh ID with `WasExisting = false` and may repeat side effects, so not-found after acceptance SHALL NOT be interpreted as proof the run never started or as permission for an effect-safe retry

#### Scenario: Count retirement races a settlement waiter
- **WHEN** an accepted instance physically settles and count pressure retires it before or during the passive wait
- **THEN** the original receipt completes once and the waiter creates no retention pin; ordinary stale-handle runtime operations still report the original not-found ID

#### Scenario: A caller keeps a delivered exception past retirement
- **WHEN** retirement removes the instance and its live exception slot after an output waiter captured the original
- **THEN** engine references are released, while the caller's already delivered exception and detached failure remain caller-owned

### Requirement: Ephemeral host shutdown closes admission atomically
One host-scoped Open-to-Closed boundary SHALL irreversibly close new business
admission without cancellation or producer sealing. ApplicationStopping, read from
its token state independently of callback ordering, SHALL establish only Closed.
Raw abortive disposal SHALL likewise only close, not initiate cancellation/join.
Only an admitted public StopAsync or the engine's own hosted StopAsync SHALL move
Closed to Stopping, closing first if still Open, and start the one cooperative-
cancellation/sealing/join operation. Close SHALL NOT acquire the facade start
semaphore, wait for a lane, enumerate/cancel users or run serialization/user code
under the visibility gate.

A start SHALL acquire host ownership before input fingerprinting and startGate
queuing, including pre-Init time. Queued starts SHALL recheck initialization and
run no Init if Closed won. A start whose initialization admission won before Closed
SHALL proceed normally through Init/attempts and MAY publish Accepted while Closed;
closing SHALL NOT abort it or convert successful pending-start completion to a
host-stopping error. The acceptance/abort election SHALL instead share the Stopping
visibility gate. Acceptance winning Stopping SHALL publish Accepted once; Stopping
then cancels remaining active workflow work separately. Only starts still unaccepted
when Stopping wins SHALL be unpublished and caller-independently fenced through
their owning lane, preserving immutable terminal status and authoritative original
error/caller cancellation, otherwise returning host-stopping. They SHALL release
physical capacity only after actual settlement in both retention modes. Closed
alone SHALL neither abort these initialized starts nor dispatch cancellation. An
initialization/attempt admission winning its applicable gate MAY begin/return later
outside the lock and SHALL remain owned.

After ordinary argument/token checks, new/replayed/conflicting StartOrGetAsync in
Closed or later SHALL fail as host-stopping before input serialization. Registration
SHALL preserve metadata-only mode/static-capability HostIncompatible precedence;
otherwise compatible registration, including idempotent replay, SHALL fail as
stopping before mutation or fingerprint-conflict/success. Existing registered-
definition handle lookup on an already-resolved registry SHALL remain a passive
identity lookup, with no registration, user callback, DI use or owned work.

In Closed before Stopping, accepted instances SHALL retain ordinary live instance-
handle acquisition (GetInstanceAsync), snapshot,
state and output queries, new output waits/pins, cancellation/termination commands,
event ingress, delays/timers, retries/resource grants/yield continuations and
retention behavior. They MAY execute new accepted-instance attempts, notify and
commit normally. Closed itself SHALL NOT cancel anything or seal these producers.
Existing retention/not-found races and explicit per-instance commands/token
cancellation SHALL retain their normal semantics; no unbounded drain is guaranteed.

From Stopping, original-ID absence/retirement and identity/state-type mismatch
checks SHALL preserve precedence using metadata only. Otherwise the following new
live operations SHALL fail as host-stopping before work, pins or mutation:

- GetInstanceAsync/live instance-handle acquisition: acquiring a runtime handle/pin can create work.
- Snapshot query: mapping/inspection can invoke runtime or codec work.
- State query: extraction can use codecs/dependencies.
- Output query, including terminal output: decoding can run user/dependency work.
- Output wait: it creates a pin, waiter or decoding work.
- Cancellation/termination: it can dispatch callbacks, enter a lane or commit.
- Event ingress: it can invoke handlers, resume work or commit.

Already-admitted operations, including those admitted during Closed, SHALL finish
with their normal result/error and remain joined. No new accepted-instance attempt
SHALL enter after Stopping; only already-admitted work and required fences/cleanup
SHALL continue. Shutdown SHALL NOT expire a key, replace an ID, prove an accepted
start never ran or grant effect-safe replay. Open/Closed accepted-instance retention
behavior and detached caller values SHALL remain unchanged. These six additions
SHALL add the host lifecycle precondition without rewriting ordinary open-host rules.

A settlement wait on an already-issued ephemeral handle SHALL remain a passive,
observer-local operation during Closed and Stopping. It SHALL not enter a runtime
lane, use DI, produce work, acquire a payload pin or count as a host join obligation.
It SHALL not cancel the workflow or stop coordinator, nor wait on itself as an
obligation. The ordinary new-live-operation rejection rules still apply to output
waits and queries; this settlement exception SHALL not reopen those operations.

#### Scenario: ApplicationStopping precedes every hosted stop
- **WHEN** the stopping token is cancelled but no engine hosted/public StopAsync has run
- **THEN** only starts/replays/compatible registrations are closed, no shutdown cancellation is queued, and accepted work continues with ordinary operations and producers

#### Scenario: Accepted work finishes naturally while Closed
- **WHEN** an accepted physical attempt finishes between admission closure and Stopping
- **THEN** it can commit and deliver its normal result without a host-requested cancellation

#### Scenario: An event reaches a waiting accepted instance while Closed
- **WHEN** ingress supplies a valid event after ApplicationStopping but before engine stop
- **THEN** normal dispatch can satisfy the wait, resume the accepted workflow and commit completion

#### Scenario: A retry or delay resumes while Closed
- **WHEN** an accepted instance's retry backoff or delay timer becomes ready in Closed
- **THEN** its continuation can admit the next accepted-instance attempt and complete normally; the host stopping token alone does not cancel or seal it

#### Scenario: A new output waiter arrives during drain
- **WHEN** a caller starts an output wait on a retained accepted instance while Closed
- **THEN** the normal waiter/pin is admitted, observes normal completion or local cancellation and remains owned if Stopping later begins

#### Scenario: A noncooperative first step holds the start semaphore
- **WHEN** close races a pre-handle start running its first physical step
- **THEN** admission closes without that semaphore, the initialized start remains owned and can return Accepted while Closed after its first step finishes; only a later Stopping phase cancels remaining work or aborts a still-unaccepted start

#### Scenario: A queued start has not initialized
- **WHEN** a start admitted before close reaches initialization after Closed
- **THEN** no Init/business step runs and the queued start releases its admission with the authoritative original cancellation/error or host-stopping result

#### Scenario: An initialized pending start completes during Closed
- **WHEN** initialization admission won before close and its first step finishes during Closed
- **THEN** the pending start proceeds normally and returns Accepted once; awaiting pending starts during drain does not produce host-stopping merely because close occurred

#### Scenario: Acceptance races Stopping
- **WHEN** accepted publication and entry to Stopping contend at the same visibility boundary
- **THEN** acceptance either wins once and the workflow is separately cancelled if still active, or Stopping wins while unaccepted and owning-lane abort preserves authoritative original errors/cancellation and physical ownership

#### Scenario: A retired handle is queried on a stopping host
- **WHEN** the old handle's original ID has retired before a query in Stopping or Completed
- **THEN** its typed not-found still names that ID; an otherwise valid retained handle reports host-stopping before any decoding, and no replacement ID is addressed

#### Scenario: A new settlement observer enters during Stopping
- **WHEN** an accepted instance's physical work is still pending and its existing handle receives a settlement call after Stopping
- **THEN** only the local observer waits for that receipt; it introduces no host obligation and cannot prevent StopAsync from reporting Completed once real work settles

### Requirement: Completed ephemeral host shutdown proves host-wide quiescence
Completed SHALL require stable zero host-owned quiescence obligations after every
producer is sealed: admitted registrations/queued starts, commands/queries/waits and execution
turns; physical root/branch/item attempts and capacity release; retry/resource/yield
continuations; first cancellation dispatches and their handlers; entered workflow,
step-timeout and stuck-step timer callbacks and dispatch; retention sweeps and all
commit/notification/cleanup handoffs. A successor SHALL acquire ownership before
its predecessor releases it. Pre-existing output waiters SHALL keep their pins until
their normal detached terminal result or local cancellation. Runtime cancellation
and provisional abort SHALL preserve existing immutable workflow terminal states.

Stopping SHALL request cooperative cancellation, cancel scheduled producers and
prevent new Init/business-attempt admission from queued retries or resource grants, while
allowing required owning-lane terminal fences and cleanup to finish. Disposed timers,
an empty instance registry, logical terminal status and a snapshot of replaceable
WhenSettled tasks SHALL NOT prove completion. An entered callback/dispatcher/sweep
SHALL be joined; any late rejected trampoline SHALL only check the sealed gate and
return without engine/dependency access, rearming, notification, ownership or commit.
No owned user callback, dependency use or late commit SHALL occur after Completed.
An invocation whose own initialization/attempt admission won the applicable gate
(Closed for new initialization, Stopping for already-initialized or accepted work) SHALL remain
owned even if its user body begins or returns later; user code SHALL NOT
run under the visibility lock to manufacture wall-clock ordering. Stop observers
and the coordinator's root reference SHALL NOT count as join obligations or wait
for themselves; their resources SHALL be observer-local and all dependency-using
cancellation/sealing/handoff jobs SHALL be accounted independently.

After Completed and before owner disposal, no engine path SHALL create owned work, touch DI dependencies,
invoke user code, arm timers, dispatch notifications/diagnostics or commit. All
new live instance operations listed above SHALL still fail as host-stopping, even
for retained terminal data. Only pure argument/token and immutable identity/error
checks, passive registered-definition handle lookup on an already-resolved registry,
reading previously detached caller-owned values and returning the cached detached
Completed result SHALL remain available. Repeated stop's Completed fast path SHALL
allocate no observation timer/registration, restart no producer and emit no diagnostic.
Retained metadata SHALL remain frozen until owner disposal; no retention sweep SHALL
resume. Disposal MAY release already-joined internal storage and application-owned
dependencies but SHALL NOT start engine work. Caller use of detached data is outside
engine-owned work; it SHALL NOT imply a live query or service-resolution permission.

Returning an already-issued instance handle's completed settlement receipt SHALL
also remain available after Completed and after correct owner disposal. It SHALL
perform only immutable/local token/proof checks and return success without DI,
codec, provider, user code, engine ownership, timers, diagnostics or commits. A
pre-cancelled token SHALL still produce local cancellation. Disposing an owner
without a completed join SHALL not itself manufacture settlement proof. The
receipt concerns only that instance's owned work, not other instances, arbitrary
client queries, detached child processes or disposal of the root provider.

#### Scenario: A cancellation request is only logically terminal
- **WHEN** an instance has reached Cancelled or TimedOut but its physical step still runs
- **THEN** shutdown remains incomplete and holds physical ownership until that body and its capacity-release continuation finish

#### Scenario: Timer disposal races entered work
- **WHEN** shutdown disposes a timer whose callback/dispatch or retention sweep already entered
- **THEN** join waits for that callback and its final handoffs; a later rejected timer trampoline cannot create work after Completed

#### Scenario: Final notification hands work to another owner
- **WHEN** a terminal commit wakes an admitted output waiter or schedules cleanup
- **THEN** ownership transfers before prior release and Completed cannot appear in a transient zero-count gap

#### Scenario: A caller reads after Completed
- **WHEN** a caller requests a new live instance-handle/snapshot/state/output/wait/command/ingress operation after Completed
- **THEN** metadata-only not-found/mismatch precedence applies, otherwise host-stopping occurs without DI/codec/user work, pins, timers or commits; previously detached values and passive registered-handle identity remain readable

#### Scenario: Stop is observed again after Completed
- **WHEN** a valid noncancelled caller repeats StopAsync before owner disposal
- **THEN** it receives the detached cached Completed result without new owned work, timers, token registrations, diagnostics, callbacks or commits

#### Scenario: An issued handle is used after Completed and disposal
- **WHEN** a caller invokes settlement on an original accepted handle after successful host join and subsequent owner disposal
- **THEN** the completed receipt returns without dependency access, new work or an ObjectDisposed failure; ordinary live output/query operations remain unavailable

## ADDED Requirements

### Requirement: Ephemeral physical settlement is independently observable
A settlement receipt SHALL complete successfully exactly when the accepted
instance is terminal and every owned physical attempt, cancellation callback,
pending commit and continuation has definitively finished, including final
ownership handoffs. This SHALL be a monotonic proof under the same visibility
protocol used by terminal retention; transient zero-count or nonterminal idle
states SHALL not complete it. Existing internal WhenSettled task epochs SHALL
not be exposed unconditionally because a waiting nonterminal can have no owned
work. Once true, no new owned instance work SHALL enter except rejected stale
trampolines that cannot use dependencies or commit.

Observers SHALL subscribe before an authoritative recheck and complete without
polling. They SHALL hold neither a physical-work/host lease nor a payload pin.
Caller cancellation SHALL release only observer-local registrations; it SHALL not
cancel work, mutate the receipt or affect other observers. Retirement is an
already-settled success for existing receipts; it SHALL not require retaining
payloads, original exceptions, services or unbounded global ID tombstones.
Settlement SHALL not wait for unrelated query pins or retirement cleanup success,
and SHALL not depend on their diagnostics. It is not proof of whole-host quiescence.

An unaccepted provisional start SHALL publish neither a handle nor a receipt;
its physical abort remains host-owned until settled. Applications that have no
accepted handle SHALL retain step-owned/external resources using their own
physical-work owner or a successful host-wide StopAsync, not infer settlement
from a thrown/cancelled start. Fire-and-forget application work or child processes
not joined by the step are outside engine physical-settlement proof.

#### Scenario: Nonterminal waiting has zero owned work
- **WHEN** an accepted instance is waiting on an event/delay with a transient zero physical count
- **THEN** settlement remains pending until actual terminality and final physical handoffs are both proven

#### Scenario: Timeout finishes before the physical attempt
- **WHEN** a noncooperative root/branch/item body outlives its timeout
- **THEN** output unavailability may be observed first but settlement waits for that body, callbacks and capacity/commit continuations

#### Scenario: One settlement observer cancels
- **WHEN** one observer cancels while a second observer and workflow remain active
- **THEN** only that observer cancels and releases its local registrations; neither the workflow nor the shared receipt is cancelled

#### Scenario: Terminal notification races subscription
- **WHEN** final settlement occurs before, during or immediately after observation registration
- **THEN** subscribe/recheck produces one successful completion without polling or a missed notification

#### Scenario: A provisional start aborts without returning a handle
- **WHEN** initialization succeeded but start cancellation, a later exception or Stopping prevents accepted publication while physical work remains
- **THEN** no public settlement handle is fabricated; the existing abort fence and host join retain all work until physical settlement in both retention modes
