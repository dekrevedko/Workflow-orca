## ADDED Requirements

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
run no Init if Closed won. Created but unaccepted starts losing close SHALL remain
owned and unpublished; already-admitted Init/attempt work MAY finish but no new
provisional business attempt SHALL be admitted. Caller-independent owning-lane abort
fencing SHALL preserve prior immutable terminal status and authoritative original
error/caller cancellation, otherwise returning host-stopping, and release capacity
only after physical settlement in both retention modes. Fencing that would signal
cancellation SHALL wait for Stopping or actual settlement; Closed alone SHALL NOT
dispatch cancellation. Acceptance winning close SHALL publish Accepted once and
allow ordinary accepted-instance draining; only Stopping SHALL cancel any remaining
active work. The acceptance/abort election SHALL share close's visibility gate.

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
- **THEN** admission closes without that semaphore, the provisional work remains owned and uncancelled by Closed, and only a later Stopping phase requests its cancellation

#### Scenario: A queued start has not initialized
- **WHEN** a start admitted before close reaches initialization after Closed
- **THEN** no Init/business step runs and the queued start releases its admission with the authoritative original cancellation/error or host-stopping result

#### Scenario: Acceptance races close
- **WHEN** final accepted publication and Closed contend at the same boundary
- **THEN** the start either becomes accepted once with drain rights or stays unpublished/owned for abort settlement without cancellation merely from Closed

#### Scenario: A retired handle is queried on a stopping host
- **WHEN** the old handle's original ID has retired before a query in Stopping or Completed
- **THEN** its typed not-found still names that ID; an otherwise valid retained handle reports host-stopping before any decoding, and no replacement ID is addressed

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
(Closed for provisional initialization, Stopping for accepted attempts) SHALL remain
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

### Requirement: Incomplete ephemeral host shutdown preserves truthful ownership
DeadlineElapsed, ObservationCancelled and ShutdownFaulted SHALL be explicit incomplete
reasons in the closed result. Its PendingWorkCount SHALL be a detached nonnegative
count of outstanding host quiescence obligations, not unique physical bodies,
threads, retained instances or bytes; Incomplete SHALL retain at least one actual
or faulted proof obligation. An accounting/sealing fault SHALL retain such an
obligation and fail closed rather than manufacture zero. Results SHALL retain no
runtime payloads, callbacks, caller tokens or unbounded observation history.

Cancellation signalling SHALL run as owned asynchronous work, without executing
application handlers inline on a stop/host/system-timer caller or under a lifetime
lock. Join SHALL retain the first cancellation/handler task, including linked-token
propagation and registration disposal; a repeated CancelAsync returning early SHALL
NOT prove earlier handlers finished. A blocked handler SHALL keep ownership and
allow independent finite observers to report Incomplete. Throwing callbacks SHALL
be contained and observed, allow other instances to proceed and permit Completed
only once all required terminal fences and actual remaining work settle. Logical
fencing, a deadline or an incomplete result SHALL never release still-owned capacity
or prove external physical stop.

The existing orca.lifecycle.events instrument SHALL report bounded best-effort
host-shutdown diagnostics with origin host-shutdown, durable=false and fixed event
names shutdown-incomplete, shutdown-fault, shutdown-callback-failed or
shutdown-abortive-disposal. It SHALL add no instrument, identity/payload/key/exception
metric label or dependency, and diagnostic failures SHALL be contained.

#### Scenario: One cancellation callback blocks indefinitely
- **WHEN** a callback does not return although its source reports cancellation requested
- **THEN** the callback remains an owned obligation, a second CancelAsync completion is not a join proof, and a finite observer can return Incomplete without releasing services or slots

#### Scenario: A callback throws and its work actually settles
- **WHEN** an owned callback throws while other instances still need cancellation
- **THEN** the fault is contained/diagnosed, other instances continue, and Completed remains possible only after every obligation and required fence finishes

#### Scenario: Accounting loses a producer-seal proof
- **WHEN** an unexpected shutdown coordination fault prevents proving stable quiescence
- **THEN** every observation returns Incomplete(ShutdownFaulted) with the retained proof obligation; apparent empty runtime data cannot turn it into Completed

### Requirement: Repeated ephemeral host shutdown observes one irreversible stop
All stop observers SHALL join one host-scoped irreversible operation. Validation and
pre-admission cancellation SHALL follow the surface contract. After admission, local
cancellation or monotonic timeout SHALL only detach that observer and its timer/
registration; it SHALL NOT reopen admission, cancel another observer or discard shared
cleanup authority. At an observation boundary, accounting fault SHALL win; otherwise
stable completion SHALL win; otherwise observer cancellation SHALL win over elapsed
budget. A later observer SHALL be able to see Completed when previously stuck work
actually returns, unless a retained coordination fault still prevents proof.
Separate root providers SHALL have independent open/stop state. Repeated observations
SHALL not accumulate stored results, completed observer lists, token registrations
or history/tombstone records.

#### Scenario: One of two observers cancels
- **WHEN** two callers observe a shared stop and one cancels after admission
- **THEN** only that caller receives Incomplete(ObservationCancelled); the other and the shared join continue with admission closed

#### Scenario: A deadline and physical completion race
- **WHEN** stable settlement and a deadline or local cancellation contend at the observation boundary
- **THEN** Completed wins if the stable-zero proof already holds; otherwise the exact incomplete reason follows the documented cancellation-before-deadline precedence

#### Scenario: A fresh host is created after another host stops
- **WHEN** the application builds a distinct root provider with the same definitions and options
- **THEN** its lifetime is independent and open; an earlier host's stop cannot cancel or close it
