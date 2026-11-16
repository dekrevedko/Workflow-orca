# Ephemeral start reservations during shutdown

This proposal updates the shutdown contract to match the per-key reservations
merged in PR #23. It is based on
[`bd4cce4`](https://github.com/dekrevedko/Workflow-orca/commit/bd4cce4dd0a0f86384de23f05303b9dfcd09e50f).

The host owns starts during fingerprinting and any same-key wait. Closing
admission can proceed while a reservation owner is executing. When the reservation
clears, the waiter rechecks admission before replay or another start. The
initialization and acceptance elections keep their existing Closed and Stopping
boundaries.

## Proposed OpenSpec delta

The destination is `management-and-querying`. `add-ephemeral-host-shutdown`
introduced the requirement and `expose-ephemeral-failures-and-settlement`
supplied its current canonical text. The current block, normalized to LF and
excluding trailing blank lines, has SHA-256
`496fe273722960b7365d8ad578ccbd00e11b52ebf442784238f072b20a59b03d`.

```markdown
## MODIFIED Requirements

### Requirement: Ephemeral host shutdown closes admission atomically
One host-scoped Open-to-Closed boundary SHALL irreversibly close new business
admission without cancellation or producer sealing. ApplicationStopping, read from
its token state independently of callback ordering, SHALL establish only Closed.
Raw abortive disposal SHALL likewise only close, not initiate cancellation/join.
Only an admitted public StopAsync or the engine's own hosted StopAsync SHALL move
Closed to Stopping, closing first if still Open, and start the one cooperative-
cancellation/sealing/join operation. Close SHALL establish admission closure
independently of in-flight start reservations and execution lanes. It SHALL NOT
enumerate or cancel workflow work or run serialization or user code under the
visibility gate.

A start SHALL acquire host ownership before input fingerprinting and retain it
while holding or waiting for an in-flight start reservation, including pre-Init
time. In-flight start reservations SHALL be scoped to the exact
StartIdempotencyKey. Different keys SHALL proceed independently of each other's
reservations. A caller waiting for the same key SHALL recheck admission before
replaying, reporting a conflict, or reserving the key for another attempt.
Queued starts SHALL recheck admission before initialization and run no Init
if Closed won. A start whose initialization admission won before Closed
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

#### Scenario: A noncooperative first step holds a start reservation
- **WHEN** close races a pre-handle start running its first physical step and holding its key's reservation
- **THEN** admission closes independently of the reservation, and the initialized start remains owned and can return Accepted while Closed after its first step finishes
- **AND** only a later Stopping phase cancels remaining work or aborts a still-unaccepted start

#### Scenario: A same-key caller waits when admission closes
- **WHEN** a start is waiting for the reservation of an already initialized start using the same key, and admission closes before the reservation clears
- **THEN** the initialized owner may return Accepted during Closed
- **AND** the waiting caller rechecks admission when the reservation clears and fails as host-stopping, subject to ordinary cancellation and error precedence, without running Init or returning a replay or conflict

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
```

## Numbered requirements and acceptance criteria

In `docs/specs/09-requirements-management-operations.md`, replace the first
paragraph of MG-066 with the following text.

```markdown
The host SHALL progress irreversibly Open -> Closed -> Stopping -> Completed.
ApplicationStopping SHALL establish only Closed from token state even if callback
notification is blocked. New/replayed/conflicting starts and compatible registrations
SHALL fail as host-stopping before serialization/mutation; preserve ordinary token/
argument and metadata-only static HostIncompatible precedence. A start SHALL own
host work from before input fingerprinting through any wait for its exact
StartIdempotencyKey reservation. Different keys SHALL proceed independently of
each other's reservations. A caller waiting for the same key SHALL recheck
admission before replay, conflict, or another attempt and SHALL reject at
Closed, subject to ordinary cancellation and error precedence. Queued pre-Init
starts SHALL reject at Closed; starts whose initialization admission already won SHALL
proceed normally and MAY publish Accepted during Closed. Accepted work SHALL retain
normal queries, new waits, commands, ingress, timers, retries/grants and retention.
Only public or engine-hosted StopAsync SHALL enter Stopping, closing first if needed.
Acceptance/abort SHALL elect at Stopping; only still-unaccepted losers SHALL be
caller-independently fenced with original-error/cancellation and terminal precedence.
Close SHALL establish admission closure independently of in-flight start
reservations and execution lanes and run no user code under its gate.
```

In `docs/specs/12-acceptance-criteria.md`, replace AC-546 with the following.

```markdown
- **AC-546** A caller waiting for an in-flight reservation rechecks admission
  when the same key becomes available and receives host-stopping if Closed won,
  without running Init or returning a replay or conflict. A queued pre-Init start
  rejects at Closed. Initialization admitted before Closed finishes its first step
  and returns Accepted during drain. Race acceptance with
  Stopping: only the still-unaccepted loser aborts, preserving original errors,
  immutable status and physical ownership in both policy modes. Include a Running
  pre-handle body and no close-caused host-stopping error after its effects. [MG-066]
```

The merged tests provide coverage for a queued same-key caller at Closed,
an initialized start draining through Closed, and independent starts while
another key owns its reservation. See
`HostShutdownTests.QueuedPreInitStart_IsRefusedAtClosed_WhileInitializedStartDrains`,
`HostShutdownTests.InitializedPendingStart_IsAcceptedDuringClosed_WithoutCancellingItsBody`
and `ConcurrentStartTests.UnrelatedStart_CompletesWhileAnotherStartIsStillRunningItsFirstStep`.

## Synchronization

The repository's post-gate path requires successor registration and independent
review before canonical synchronization. The current guard expects exactly the
two historical owners for this heading. The registration must account for both
owners and preserve their checkpoint evidence. Synchronize this exact requirement
and the numbered replacements together, then verify the approved shutdown
checkpoint, successor provenance and affected acceptance evidence.
