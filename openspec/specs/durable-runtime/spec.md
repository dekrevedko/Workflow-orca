## Purpose

Define the durable execution semantics for the state-driven OrcaCore runtime, including persistence, rehydration, and long-running workflow behavior.
## Requirements

### Requirement: Durable mode persists runtime-owned state for rehydration
The durable runtime SHALL persist workflow business payloads, fiber-local payloads, fibers, scopes, scheduling state, waits, owned obligations, and pending merge results through a store contract so every committed execution position can be rehydrated after host restart.

#### Scenario: Host restarts while an instance is suspended
- **WHEN** a durable workflow instance is resumed after process restart
- **THEN** the runtime reconstructs the instance from the complete committed envelope instead of relying on prior in-memory objects or recomputing ownership from cursor paths

### Requirement: Durable instances remain bound to definition version
The durable runtime SHALL bind each instance to its definition identifier, positive authored version, workflow mode, compiler format version, fixed codec format `orcacore-json-v1`, typed input/output contract, and immutable structural fingerprint. Resume SHALL fail with typed diagnostics when the exact binding is unavailable or incompatible. The fingerprint SHALL cover inspectable authored structure and codec format only and SHALL exclude the other distinct binding values plus compiler acceptance/fairness limits; changing selector/projector/merge/output code, step construction/configuration, DAG mapping, or external-request construction SHALL require a new version and SHALL NOT use an author fingerprint contributor. A host SHALL retain every compiler format referenced by a nonterminal durable instance until the instance terminalizes or is explicitly migrated. Before a released package or durable-data compatibility contract exists, a format bump MAY be a hard cutover when no supported persisted instance exists.

#### Scenario: New definition version is deployed
- **WHEN** an older durable instance resumes after a newer version has been registered
- **THEN** the runtime resolves the exact bound definition or fails explicitly instead of selecting the newer version

#### Scenario: Graph changes under the same definition version
- **WHEN** registration or resume observes a fingerprint different from the fingerprint bound to the identity/version
- **THEN** the operation fails with a typed fingerprint conflict before executing an instruction

#### Scenario: Bound compiler format remains in use
- **WHEN** a nonterminal instance references an older supported compiler format after a newer format is deployed
- **THEN** the host retains or explicitly migrates that format binding rather than retiring it solely because the current compiler changed

### Requirement: Durable mutation is crash-safe
The durable runtime SHALL expose only the last committed durable state after a crash and SHALL not leak partially applied transitions across restart boundaries.

#### Scenario: Host crashes during advancement
- **WHEN** execution fails after computation but before durable commit completes
- **THEN** rehydration restores the last committed state only

### Requirement: Serialized execution survives eviction and reactivation
The durable runtime SHALL preserve one-logical-mutator semantics even when instances are evicted from memory and later reactivated from durable state.

#### Scenario: Evicted instance is resumed under load
- **WHEN** an idle durable instance is evicted and then receives new work concurrently
- **THEN** reactivation still results in a single valid serialized mutation stream for that instance

### Requirement: Optional resource governance applies after rehydration
When resource governance is enabled, the durable runtime SHALL restore per-instance admission state and every persisted durable lease obligation before admitting work. Host-local execution throttles SHALL apply only while a business-step attempt runs. Durable lease queueing, held, marked, ambiguous, and quarantine state SHALL preserve exact cross-host lifetime and SHALL NOT be reconstructed from elapsed time alone.

#### Scenario: Rehydrated instance competes for capacity
- **WHEN** a durable instance resumes with runnable steps and a pending or held lease scope
- **THEN** host-local step admission and persisted resource admission each apply according to their distinct lifetime without double-granting or losing the obligation

### Requirement: Durable envelope records the complete structured execution state
The versioned durable envelope SHALL persist parent/child fibers, the scope hierarchy for accepted root fan-out and lexical resource scopes, scheduling/path-token state, `ForEach` admitted-item and next-admission state, fixed-codec-detached private payloads, branch/item results awaiting merge, normalized bounded `ForEach` snapshots and item identities, workflow and active step deadlines, `StepOperationId` occurrence coordinates, durable retry-policy `AttemptNumber`, in-flight dispatch marker, owned lease obligations, definition identity/version/structural fingerprint, continue-as-new generation, and compiler/envelope/codec format versions. It SHALL NOT persist a branch or live-fiber admission resource.

#### Scenario: Host restarts with nested scopes
- **WHEN** a host restarts while parent, branch, and item fibers are runnable or parked at different scopes
- **THEN** the runtime reconstructs exact positions, private state, results, deadlines, ownership, and deterministic scheduling without re-running selectors already committed

#### Scenario: Envelope format is unsupported
- **WHEN** persisted execution state uses an unknown or retired envelope format
- **THEN** the runtime parks or rejects the instance with an explicit diagnostic instead of guessing

#### Scenario: Scope inside a loop is rehydrated
- **WHEN** a checkpoint records another occurrence of the same authored scope in a loop
- **THEN** occurrence coordinates distinguish its step operations, item identities, and obligations from prior iterations

### Requirement: Scope transitions and effects commit atomically
Any transition that creates, blocks, completes, cancels, joins, merges, times out, acquires, quarantines, or releases fibers/scopes SHALL atomically commit the updated envelope with corresponding workflow facts, typed output, obligation changes, outbox records, and continuation signals.

#### Scenario: Branch completion makes a scope joinable
- **WHEN** the final required branch or item result commits
- **THEN** the result, joinable scope state, cleanup facts, and continuation signal commit as one transition

#### Scenario: Commit loses an optimistic concurrency race
- **WHEN** two hosts attempt to commit advancement for the same instance
- **THEN** at most one transition commits and the loser reloads the complete envelope before retrying with the same logical occurrence identities

### Requirement: Development refactor does not retain cursor execution
The structured-fiber runtime SHALL replace the cursor split/join executor as one execution path. It SHALL NOT select between cursor and fiber interpreters based on persisted instance shape.

#### Scenario: Development store contains a cursor envelope
- **WHEN** a store created before the structured-fiber refactor contains an old cursor envelope
- **THEN** the runtime rejects it with a format diagnostic and development operators reset or recreate that data

### Requirement: ContinueAsNew requires a quiescent root scope
`ContinueAsNew` SHALL be valid only from the durable root builder after every lexical resource scope has exited, when the root fiber is the sole nonterminal fiber and no active descendant scope or outstanding owned obligation exists. Runtime violation SHALL fail with `SFE-RUN-001` without changing generation, replacement state, or ownership. A valid rollover SHALL atomically increment generation, install replacement state, derive new occurrence identities, and inherit the original absolute workflow deadline without resetting it.

#### Scenario: Root requests rollover with an active scope
- **WHEN** a stale or hand-built plan evaluates continue-as-new while a descendant scope, wait, timer, child, resource ticket, quarantine, or pending cleanup remains active
- **THEN** rollover is rejected, the instance fails with `SFE-RUN-001`, and existing generation and ownership remain unchanged

#### Scenario: Quiescent root rolls over
- **WHEN** the sole runnable root executes continue-as-new with no outstanding scope or obligation
- **THEN** one atomic transition increments generation, installs replacement state, and creates distinct new-generation operation identities

### Requirement: Wait residency is runtime-owned
Durable `Wait(eventContract, correlation[, timeout])` SHALL persist descriptor/version matching state, pending inbox ownership, and continuation position and MAY evict any parked instance for later rehydration. Hot/cold residency SHALL be a host decision and SHALL NOT require another authored node, broker redelivery, or change workflow semantics. Accepting a matching event SHALL commit a continuation for a cold instance, and a definition-owning pump SHALL restore the exact bound definition/version/fingerprint before applying it.

#### Scenario: Long-running wait is parked
- **WHEN** a durable workflow remains at `Wait` across host eviction or restart
- **THEN** the runtime restores the same matching registration and continuation position without a public `WaitLong` distinction

#### Scenario: Event matches a cold wait
- **WHEN** durable ingress accepts an event matching a persisted wait whose instance is not resident
- **THEN** a definition-owning pump rehydrates the exact instance and applies the accepted event once

#### Scenario: Wait event races its timeout
- **WHEN** an event and the optional structural wait timeout race
- **THEN** one committed winner cancels the losing event/timer obligation and timeout fails the current root, branch, or item with `WorkflowWaitTimeoutException`

### Requirement: Durable structured fan-out reuses committed inputs and results
Durable root `Parallel` and bounded root `ForEach` SHALL reconstruct scope progress exclusively from committed authored branch definitions, the committed item snapshot, stable branch/item identities, terminal results, and merge state. A `ForEach` selector SHALL commit one finite fixed-codec-detached snapshot before any item admission and SHALL NOT run again after that commit. Every fixed `Parallel` branch fiber SHALL exist at scope start and runnable branches SHALL be selected for tokens in authored order; items SHALL be admitted by index under the lower host/node limit. Host-local execution-path tokens SHALL NOT be persisted as capacity claims; after restart the runtime SHALL restore every unfinished fixed branch and re-admit unfinished items in deterministic order without a live-fiber capacity claim. A merge SHALL commit at most once from the complete persisted ordered inputs.

#### Scenario: Host fails after item snapshot commit
- **WHEN** a durable host restarts before all selected items are terminal
- **THEN** it reuses the committed detached snapshot, does not invoke the selector again, and re-admits unfinished item identities in index order

#### Scenario: Host fails after final result before merge
- **WHEN** every required terminal result committed but the merge transition did not
- **THEN** replay evaluates one merge from the persisted ordered inputs without rerunning completed branch or item bodies

#### Scenario: Host fails after merge commit
- **WHEN** the merge transition committed before the host stopped
- **THEN** replay observes the replacement parent state and does not evaluate the merge again

#### Scenario: Empty item snapshot commits
- **WHEN** the selected finite item snapshot is empty
- **THEN** no item fiber is admitted and the selected merge commits once with an empty ordered collection

### Requirement: Durable runtime is the complete application facade
The durable runtime SHALL implement the shared ordinary facade of application-configuration definition staging, typed exact-reference handle lookup, explicit low-level registration, idempotent typed start/reopen, self-routing durable event acceptance/continuation, durable outbound event dispatch, detached snapshot/root-state/output queries, cancellation request, and termination without requiring application callers to construct `DurableCommandProcessor`, raw commands, provider timestamps, or serialized provider payloads. It SHALL NOT add bulk selection/list/count/statistics, pause/resume, management retry, archive, purge, or history operations to the v1 application surface.

#### Scenario: Normal durable workflow runs
- **WHEN** an application interacts with a supported durable definition entirely through typed application facades
- **THEN** each accepted transition commits and progresses locally or records an at-least-once continuation handoff

#### Scenario: Raw protocol access is required
- **WHEN** a certified custom host needs command-level integration
- **THEN** it opts into the runtime-protocol package explicitly and the normal application facade remains unchanged

### Requirement: Definition registration is explicit, host scoped, and typed
The common `IWorkflowDefinitionRegistry` SHALL expose all four ephemeral/durable resultless/resultful registration overloads, four exact typed `GetRequiredHandle(reference)` overloads, and the corresponding typed handles. Every definition family SHALL expose a state-opaque typed reference. `AddOrcaCoreDurableEngine` SHALL return a durable composition builder whose resultless/resultful `AddWorkflow` overloads stage already-built durable definitions. Before readiness or any continuation, timer, inbox, outbox, or DAG progression, the host SHALL preflight the complete staged batch and install it atomically; an exact duplicate identity/version/fingerprint is idempotent, while any incompatibility/conflict leaves the registry unchanged and starts no loop. A durable host SHALL return `HostIncompatible.EngineModeMismatch` for an ephemeral definition before any other compatibility check or mutation. For a durable definition, registration SHALL validate every statically inspectable lease request against the configured durable pool catalog and require `IWorkflowEventDispatcher` when `Publish` is present before fingerprint conflict or mutation; missing pools SHALL return `HostIncompatible.MissingDurableResourcePools` with all copied distinct ordinal-sorted names and missing dispatch SHALL return `HostIncompatible.MissingWorkflowEventDispatcher`. Selector-produced pool names SHALL be validated at runtime before queue/provider mutation. Start and exact-reference lookup SHALL NOT register as a side effect. An absent or stale exact reference SHALL throw `WorkflowDefinitionNotRegisteredException`. Provider-global `StartIdempotencyKey` SHALL bind definition identity/version/fingerprint and deterministic input bytes.

#### Scenario: Registered resultful definition is started
- **WHEN** a host registers a resultful durable definition and calls `StartOrGetAsync` with typed input
- **THEN** the returned handle preserves `TOutput` without exposing private workflow state or phantom generic arguments

#### Scenario: Start key is reused incompatibly
- **WHEN** one start key is reused with another identity/version/fingerprint or different deterministic input bytes
- **THEN** the facade returns `StartIdempotencyConflict`, creates no instance, and does not return the incompatible existing instance

#### Scenario: Ephemeral definition is passed to durable registration
- **WHEN** application code passes an ephemeral definition to the common registry owned by a durable host
- **THEN** registration returns `HostIncompatible.EngineModeMismatch` before pool validation, fingerprint comparison, or mutation

#### Scenario: Static lease request names an unavailable pool
- **WHEN** durable registration inspects one or more static requests whose pools are not configured on the host
- **THEN** it returns `HostIncompatible.MissingDurableResourcePools` before registry or provider mutation

#### Scenario: Published event has no dispatcher
- **WHEN** a durable definition contains `Publish` and the definition-owning host has no application `IWorkflowEventDispatcher`
- **THEN** registration/startup returns `HostIncompatible.MissingWorkflowEventDispatcher`, mutates no registry, and starts no progression loop

#### Scenario: Replacement host starts
- **WHEN** a host replacement stages the same definition set and begins processing cold instances
- **THEN** it atomically installs the exact definitions before claiming any continuation, timer, inbox, outbox, or DAG work

### Requirement: Accepted application events guarantee continuation
Every `Accepted` durable application event SHALL atomically persist its complete fixed-codec normalized envelope and self-routing intent before return. `WorkflowEventAcceptanceResult` SHALL be the closed `Accepted`/`Duplicate`/`Rejected(WorkflowEventAcceptanceRejection)` union, with exact rejection variants `EventConflict`, `DirectInstanceNotFound`, `DirectInstanceTerminal`, `StartConflict(StartIdempotencyConflict)`, and `FanoutLimitExceeded`. The same globally unique `EventId` with identical normalized bytes SHALL return `Duplicate`; changed normalized bytes SHALL return `Rejected(EventConflict)` and never overwrite the first envelope. Identity comparison SHALL precede current target-state validation. `Accepted` and identical `Duplicate` mean OrcaCore durably owns the event and are safe for broker acknowledgement; rejection or exceptional completion SHALL NOT claim ownership, and fanout rejection SHALL leave no partial target set. A definition-owning host MAY claim and drive matching targets inline; a definition-less ingress host SHALL persist the same route/inbox state and leave execution to a definition-owning pump. `NoActiveWait` SHALL NOT be a durable ingress result, and no accepted unmatched event SHALL be silently expired or deleted.

#### Scenario: Definition-owning host receives an event
- **WHEN** a local definition-owning host accepts an event that immediately matches one or more targets
- **THEN** the accepted envelope, fixed target/claim state, and continuation handoffs are committed once without exposing local driver status as another public result shape

#### Scenario: Definition-less host receives an event
- **WHEN** a callback host without the definition accepts a normalized event
- **THEN** it commits accepted route/inbox ownership and any continuation handoff, and a definition-owning pump later progresses each target exactly once logically

#### Scenario: Broker redelivers after acknowledgement uncertainty
- **WHEN** the caller repeats an accepted envelope after losing the acceptance response
- **THEN** identical normalized bytes return `Duplicate`, changed bytes return `Rejected(EventConflict)`, and no target consumes the identity twice

### Requirement: Event routing outcomes are typed
`IWorkflowEventIngress` SHALL expose payloadless and typed `AcceptAsync` overloads over one immutable envelope whose closed route is direct instance, definition/correlation, definition fanout, or exact-definition start-or-deliver. Matching SHALL be exact ordinal and case-sensitive on event contract name/version and correlation. Correlation routing SHALL resolve at most one active wait by `(DefinitionId, event contract, CorrelationId)`; registering a second active wait for that key, including in one instance, SHALL fail with `AmbiguousWaitRegistrationException` before parking. Public input SHALL never contain wait sequence, fiber, scope, provider generation, checkpoint, or raw command identity.

#### Scenario: Correlation pair would become ambiguous
- **WHEN** a workflow attempts to register an active wait whose definition/contract/correlation key is already active
- **THEN** registration fails deterministically before parking, so delivery never chooses arbitrarily

#### Scenario: Live instance has no matching wait
- **WHEN** an event targets a live or cold nonterminal instance with no matching active wait
- **THEN** acceptance stores it in that instance's inbox and a later matching wait claims it without broker redelivery

#### Scenario: Event identity is redelivered
- **WHEN** the same target receives the same `EventId` with identical or different normalized envelope bytes
- **THEN** identical bytes return `Duplicate`, different bytes return `Rejected(EventConflict)`, and neither can satisfy another occurrence

#### Scenario: Correlation pair is reused later
- **WHEN** one wait consumes an event and a later loop occurrence registers the same event/correlation pair
- **THEN** the next pending or later accepted event in durable acceptance order may satisfy the new wait and occurrence-specific matching belongs in `CorrelationId`

#### Scenario: Definition fanout is accepted
- **WHEN** a fanout route is accepted for one definition identity
- **THEN** the provider atomically snapshots all current nonterminal persisted instances for that `DefinitionId` across versions, independent of the accepting host's registered catalog or resident instances, persists one independently deduplicated delivery per target, excludes later instances, and treats an empty snapshot as accepted

#### Scenario: Start-or-deliver is accepted
- **WHEN** a route carries exact definition identity/version, start idempotency key, typed workflow input, and a distinct event payload
- **THEN** acceptance atomically creates or reuses a durable pending start intent bound to identity/version/normalized input and retains the event; a definition-owning host then atomically creates or reattaches the compatible instance, target inbox delivery, and continuation without inferring input from event payload

#### Scenario: Callback-only ingress accepts start-or-deliver
- **WHEN** a definition-less ingress host accepts a start-or-deliver route whose durable start binding is not already incompatible
- **THEN** it commits the pending start intent and event without loading a definition, and a later definition-owning pump materializes them without broker redelivery

#### Scenario: Accepted start intent cannot be resolved
- **WHEN** the definition-owning pump cannot resolve the accepted intent's exact definition/version or discovers another semantic incompatibility unavailable to callback ingress
- **THEN** it records observable poison against the durably owned intent/event rather than losing it or changing the earlier acceptance result

### Requirement: Workflow and step deadlines survive restart
`CompleteWithin` SHALL persist one positive finite absolute deadline measured from instance start and include admission queueing, retries, delays, event waits, lease queueing, and every continue-as-new generation. When it wins, one commit SHALL terminalize as `TimedOut` with `WorkflowDeadlineExceededException`, prevent admission, cancel wait/timer obligations, signal active attempts, suppress merges, and perform definite cleanup or quarantine. `WithStepTimeout` SHALL persist/fence one business-step attempt deadline. Before first dispatch the runtime SHALL commit the `StepOperationId`, positive retry-policy `AttemptNumber`, optional absolute attempt deadline, and in-flight marker. Every physical dispatch SHALL mutate a fixed-codec-detached copy of the last committed state; only a winning successful transition commits it, while failed/timed-out/late copies are discarded. Host-loss, replay, expected-version conflict, and lost-response redispatch SHALL reuse the same operation ID, attempt ordinal, and deadline and SHALL NOT consume retry budget. Only a committed eligible failure/timeout transition SHALL increment the ordinal and create the next deadline. `maxAttempts = 1` SHALL permit redispatch of ordinal one but no policy retry. If recovery finds that an in-flight ordinal's absolute deadline already elapsed, it SHALL commit timeout without redispatch and either schedule the next ordinal when budget remains or terminalize. Outside a durable lease scope, a token-ignoring timed-out body MAY overlap a policy retry while retaining its physical throttle/transient capacity. Inside a durable lease scope, a later policy retry SHALL NOT start in the same process until the prior body has actually returned; after host loss, recovery MAY redispatch the same ordinal because the prior process body no longer exists, while the lease ambiguity and every durable ticket remain capacity-reserving. Runtime orchestration SHALL NOT delegate these semantics to Polly.

#### Scenario: Host restarts during a step attempt
- **WHEN** a timed step is in flight across host loss
- **THEN** recovery reuses the persisted operation ID, attempt ordinal, and deadline, fences a late prior result, and consumes no policy retry unless a timeout/failure transition commits

#### Scenario: In-flight deadline elapsed before recovery
- **WHEN** replay loads an in-flight attempt whose absolute deadline already passed
- **THEN** it commits timeout without redispatch and either schedules the next ordinal within budget or terminalizes when no policy retry remains

#### Scenario: Workflow deadline passes while waiting
- **WHEN** a durable instance remains parked at an event or resource wait beyond its workflow deadline
- **THEN** the runtime commits the workflow timeout decision without treating external protected work as proven stopped

#### Scenario: Retry eligibility is evaluated
- **WHEN** an authored failure, normalized exception, or `StepAttemptTimeoutException` occurs and attempt budget remains
- **THEN** the runtime retries after the fixed delay, while cancellation, workflow deadline, termination, definition conflict, and runtime invariant failures never retry

#### Scenario: Late body overlaps retry
- **WHEN** a token-ignoring timed-out body remains physically active after a retry starts
- **THEN** outside a durable lease scope it has no commit authority, mutates only its discarded copy, and retains physical throttle/transient capacity until return

#### Scenario: Leased timed-out body is still running
- **WHEN** a timed-out attempt inside an active durable lease scope ignores cancellation and remains physically active in the current process
- **THEN** the obligation becomes `AmbiguousHeld`, retains the same `StepOperationId`, protection token, tickets, and reserved capacity, and the next retry does not start until the prior body returns

#### Scenario: Leased attempt is recovered after host loss
- **WHEN** a host is lost with an in-flight leased attempt
- **THEN** recovery may redispatch the same ordinal with the same `StepOperationId`, deadline, and protection token because the prior process body is gone, while the obligation remains `AmbiguousHeld` until exact scope disposition

### Requirement: Step operation identity survives replay
The durable runtime SHALL provide one runtime-created `StepOperationId` for each logical business-step visit and persist or deterministically derive it from committed occurrence coordinates. It SHALL remain stable across policy retries, timeouts, crash redispatch, expected-version conflicts, and competing drivers and SHALL change across loop re-entry, branch/item occurrence, or continue-as-new generation. `AttemptNumber` SHALL identify the committed retry-policy ordinal and SHALL remain unchanged across physical redispatch of that ordinal.

#### Scenario: Create-or-observe call is ambiguous
- **WHEN** a durable step is redispatched after process loss during an external API call
- **THEN** the step receives the same operation ID and attempt ordinal so its application adapter can observe or deduplicate the same logical request without consuming retry budget

### Requirement: Durable leases are lexical occurrence-owned obligations
A scoped durable acquisition SHALL use one persistent runtime-generated obligation with the normative lifecycle `Queued -> PendingCommit -> Held -> ReviewMarked -> AmbiguousHeld -> Quarantined -> Released`, plus `CancelledBeforeGrant` and `LeaseLost` terminal side paths. The obligation SHALL retain exact instance/generation/fiber/scope occurrence, protection token, tickets, pool/units, and provider-generation identity. `LeaseProtectionToken` SHALL be minted and committed with the queued obligation before capacity admission and delivered to author code only after grant. `Queued` and `CancelledBeforeGrant` reserve zero; `PendingCommit`, `Held`, `ReviewMarked`, `AmbiguousHeld`, and `Quarantined` reserve every exact ticket unit. Its entire non-empty duplicate-free `ResourceLeaseRequest` SHALL grant atomically. A dynamic request delegate SHALL be deterministic and side-effect-free, MAY be re-invoked before commit, and SHALL produce exactly one normalized committed request before provider mutation; replay SHALL reuse that request. Static and dynamic normalized requests SHALL validate all pool names before queue/operation/provider mutation and SHALL throw `ResourcePoolNotConfiguredException` with code `WF-RESOURCE-POOL-NOT-CONFIGURED` carrying every copied, distinct, ordinal-sorted missing name. If capacity is unavailable, only the requesting fiber SHALL park.

Grant and cancellation SHALL be serialized at durable commit boundaries. Cancellation that wins while still `Queued` SHALL commit `CancelledBeforeGrant` with zero tickets and zero reserved units. Once `GovernanceReservationCommitted` wins, cancellation SHALL NOT erase or forget tickets. Cancellation before `WorkflowActivationCommitted` SHALL perform one exact compensating release because author code was never admitted; cancellation after activation SHALL use normal proven lexical release or ambiguity-preserving quarantine. Delayed or duplicated commands after a winner SHALL be idempotent stale no-ops.

Normal lexical body completion and causally proven pre-effect/cleaned-up failure SHALL release exact tickets before parent resume. A retryable step timeout, ambiguous submit, or recovered in-flight attempt while the lexical owner remains recoverable SHALL move `Held` or `ReviewMarked` to `AmbiguousHeld`, not detach the lease or make it confirmable. Every retry SHALL retain the same `StepOperationId`, `LeaseProtectionToken`, exact tickets, and reserved capacity. A successful retry SHALL NOT by itself erase the earlier ambiguity. If the scope later attempts to exit while ambiguity remains, or retry exhaustion, cancellation, workflow deadline, forced termination, or owner abandonment wins, one atomic transition SHALL detach the obligation as `Quarantined` before branch/item failure, merge, parent continuation, or terminal progression. For a `WhenAllOutcomes` branch/item, quarantine transfer and failed outcome SHALL commit before parent merge/progression; the merge SHALL NOT release or reuse the ticket. Only trusted causal stop confirmation or an end-to-end protected-resource fence MAY move `Quarantined` to `Released`. Elapsed time, terminal workflow state, metadata deletion, a successful retry alone, or holder liveness guess SHALL NOT release it.

Each exact ticket SHALL use its pool-owned review timestamp, so tickets in one atomic mixed-pool request MAY become due independently. A due ticket SHALL be marked/audited without splitting ownership or granting from its units. Reconciliation SHALL release only from causal proof that the acquisition never committed, already released, or belongs to a terminal owner with cleanup and protected-work stop/fence proven. A missing expected ticket for an active owner SHALL produce `LeaseLost` and SHALL NOT silently reacquire.

Downward resize MAY create `max(0, reserved - configured)` debt, SHALL revoke nothing, and SHALL block new grants until debt is zero and the whole next request fits.

#### Scenario: Owning branch completes normally
- **WHEN** a leased branch body reaches its lexical end with no ambiguous protected work
- **THEN** exact obligation/ticket release commits before parent join or resume

#### Scenario: Cancellation wins before reservation
- **WHEN** cancellation commits while a lease obligation is still `Queued`
- **THEN** it becomes `CancelledBeforeGrant` with no ticket, no reserved capacity, and every delayed grant command is a stale no-op

#### Scenario: Reservation wins before cancellation
- **WHEN** governance reservation commits before cancellation
- **THEN** cancellation preserves the exact tickets and either compensates before workflow activation or follows proven release/quarantine after activation

#### Scenario: Retry succeeds after a leased timeout
- **WHEN** a later attempt with the same operation ID succeeds after the lease entered `AmbiguousHeld`
- **THEN** the successful step transition does not silently restore `Held` or release capacity, and an ambiguous lexical exit transfers the exact obligation to `Quarantined` before progression

#### Scenario: Leased retries are exhausted
- **WHEN** the final eligible attempt fails while the obligation is `AmbiguousHeld`
- **THEN** quarantine transfer commits with the exact token/tickets before scope failure, merge, parent continuation, or terminal progression becomes visible

#### Scenario: Descendant attempts acquisition under an ancestor
- **WHEN** a stale plan attempts descendant acquisition while an ancestor obligation is pending or held
- **THEN** runtime defense commits deterministic terminal instance failure with `SFE-RUN-002` before queue or pool mutation, suppresses merges, and does not park, retry after restart, or report an input-delivery `Poisoned` outcome

#### Scenario: Review deadline passes for a live owner
- **WHEN** a pool review timestamp passes while committed owner state remains active or reconstructable
- **THEN** the obligation is marked/audited and capacity remains reserved without holder renewal

#### Scenario: Mixed-pool tickets reach different review deadlines
- **WHEN** one atomic request holds tickets from pools with different review policies
- **THEN** each exact ticket is marked/reconciled at its own pool-owned deadline while the whole obligation and every unresolved unit remain capacity-reserving

#### Scenario: Reconciliation proves a release gap
- **WHEN** exact owner/provider evidence proves the acquisition never committed, already released, or belongs to a terminal cleaned-up owner whose protected work is stopped/fenced
- **THEN** compare-and-act records one recovered release against the matching obligation, tickets, and provider generations

#### Scenario: Expected provider ticket is missing
- **WHEN** committed owner state remains active but an expected exact ticket/provider generation is absent
- **THEN** the owner receives `LeaseLost` and cannot continue or silently reacquire

#### Scenario: Forced termination has not stopped protected work
- **WHEN** an instance is terminal but causal evidence does not prove protected work stopped or fenced
- **THEN** its exact obligation remains quarantined and no waiter receives those units

#### Scenario: Trusted stop confirmation arrives
- **WHEN** a trusted reconciler calls `IDurableResourceLeaseRecovery.ConfirmProtectedWorkStoppedAsync` for the exact `LeaseProtectionToken` using an idempotent `StopConfirmationId`
- **THEN** runtime compare-and-act releases only that matching quarantined obligation once

#### Scenario: Release immediately grants a waiter
- **WHEN** exact release transfers freed units directly to an eligible queued request
- **THEN** ticket and unit conservation remain exact even though global available capacity does not return to an earlier snapshot

#### Scenario: Pool shrinks below reservations
- **WHEN** configured capacity falls below current reserved units
- **THEN** no reservation is revoked, debt is visible, and no new grant occurs until release or upward resize clears debt and the next request fits

#### Scenario: Stop confirmation is serialized with release
- **WHEN** confirmation is evaluated against confirmation-ID binding, prior accepted confirmation, live lexical ownership, quarantine eligibility, and missing/normally released state
- **THEN** precedence is `ConfirmationConflict`, `AlreadyConfirmed`, `NotConfirmable`, `Released`, then `TokenNotFound`, and capacity releases at most once

### Requirement: Durable resource governance is one serialized provider aggregate
V1 SHALL maintain one resource-governance aggregate per configured `ResourceGovernancePartitionId`. The aggregate SHALL own every immutable pool creation definition, FIFO queued atomic multi-pool request, reservation/ticket, review mark, idempotent resize operation, confirmation binding, and tombstone in that partition. `DurableResourcePoolDefinition.Capacity` SHALL be immutable creation capacity, while `DurableResourcePoolSnapshot.ConfiguredCapacity` SHALL be current capacity after replayed resizes. Startup SHALL atomically create definitions; later hosts SHALL present identical creation name/capacity/review values or fail even when current capacity differs, and startup SHALL NOT reset replayed current capacity or resize debt. `ReviewAfter` SHALL mark for reconciliation only. `GetAsync` and `ResizeAsync` SHALL throw `ResourcePoolNotConfiguredException` for an unknown pool before mutation. `ResizeAsync` SHALL reject capacity less than one with `ArgumentOutOfRangeException` before operation-ID binding or aggregate mutation; otherwise it SHALL be idempotent by `ResourcePoolOperationId`, return the closed `DurableResourcePoolResizeResult.Applied` or `.Conflict` outcome, and expose no force-release path. Exact replay of one operation ID/pool/capacity tuple SHALL return the originally recorded `Applied`; reuse with another pool or capacity SHALL return `Conflict` containing recorded and attempted mutation facts and SHALL perform no mutation.

Provider load validation SHALL be split across exact seams. `ResourceGovernanceRecord.FromPersisted` SHALL validate one positive sequence, supported protocol format, checksum, and a defensive payload-byte copy. `ResourceGovernanceStream.Create(long version, IReadOnlyList<ResourceGovernanceRecord> records)` SHALL reject a negative version, null collection, null entry, gap, duplicate, reordering, or version mismatch; defensively copy the collection; require version zero exactly when empty; and require the complete v1 loaded sequence `1..Version`. `AppendAsync` SHALL reject/certify an empty or nonconsecutive batch, defensively copy it, require sequence `expectedVersion + 1` through `expectedVersion + count`, and commit the whole ordered batch or return expected-version `Conflict` without a partial append.

The runtime SHALL expose exactly four internal post-commit certification barriers, never application or provider-storage API: `WorkflowPendingObligationCommitted`, `GovernanceReservationCommitted`, `WorkflowActivationCommitted`, and `GovernanceOwnershipConfirmed`. Their enum/fact/gate types SHALL remain internal in `OrcaCore.Engine.Durable.ResourceGovernance`; `OrcaCore.ProviderCertification` SHALL consume them through the sole cross-package test friend edge in the exact repository friend graph. Each barrier fact SHALL be immutable and carry partition, lease obligation, instance, generation, fiber occurrence, scope-entry occurrence, protection token, workflow and governance stream versions, plus the exact ticket ID, pool, units, and provider generation for every ticket then known. The barrier SHALL signal and block after the named durable commit and before the next protocol command so the deterministic certification fixture can stop the host at that boundary. Re-entry MAY report the same occurrence/barrier again but SHALL preserve equal facts and SHALL NOT create a new obligation or ticket.

#### Scenario: Atomic request crosses pools
- **WHEN** one request needs multiple pools in the configured partition
- **THEN** one expected-version governance transition grants every requirement or none and preserves fixed FIFO committed-request order

#### Scenario: Workflow/governance handoff crashes
- **WHEN** certification stops a host at any one of the four named post-commit barriers
- **THEN** recovery completes or compensates the exact occurrence while all uncertain tickets remain reserved and workflow/governance ownership agrees on obligation, instance, generation, fiber, scope entry, token, ticket, pool, units, and provider generation

#### Scenario: Provider persists governance
- **WHEN** a durable provider implements `IDurableResourceGovernanceStore`
- **THEN** `LoadAsync` creates one defensively copied full stream through `ResourceGovernanceStream.Create`, and expected-version ordered-batch `AppendAsync` preserves per-record checksum/format validation, exact sequence continuity, all-or-conflict append, and no partial batch

#### Scenario: Persisted governance stream is malformed
- **WHEN** a provider returns a null/mutable record collection, version mismatch, sequence gap, duplicate, reordering, unsupported format, bad checksum, or payload later mutated by its source buffer
- **THEN** construction fails before aggregate replay, and previously accepted stream/record values remain unchanged by caller mutation

#### Scenario: Resize operation ID is reused with changed intent
- **WHEN** one `ResourcePoolOperationId` was recorded for a pool/capacity and is submitted with another pool or capacity
- **THEN** `ResizeAsync` returns `DurableResourcePoolResizeResult.Conflict` with recorded and attempted facts, changes no pool state, and grants no waiter

#### Scenario: Host restarts after a persisted resize
- **WHEN** a pool was created at capacity N, resized to current capacity M with possible debt, and a later host supplies the original creation definition N
- **THEN** startup succeeds and retains M and debt; supplying M or any other value as creation capacity fails even when it equals current capacity

#### Scenario: Pool management input is invalid
- **WHEN** `GetAsync`/`ResizeAsync` names an unconfigured pool or `ResizeAsync` receives zero or negative capacity
- **THEN** the typed unknown-pool or range exception occurs before operation-ID binding, aggregate mutation, queue change, or provider append

#### Scenario: Provider only has per-workflow streams
- **WHEN** a provider cannot serialize the configured cross-workflow pool partition
- **THEN** startup/certification rejects it as incapable of v1 durable leasing

### Requirement: Durable DAG progression is runtime owned
The durable DAG runtime SHALL reconstruct node readiness from committed DAG state, evaluate each opaque `MapInput` only after all declared direct dependencies succeed, commit each fixed-codec node input once from immutable run input plus successful direct dependency outputs, start or reattach one durable child workflow instance per node occurrence, and continue after child outcomes without caller-owned ready/completed sets. `Build` SHALL reject only inspectable structural faults, including a missing or duplicate `MapInput`; it SHALL NOT claim to discover an undeclared/non-direct `OutputOf` call hidden in delegate code. Mapper access to an undeclared, non-direct, resultless, foreign-plan, or otherwise invalid output SHALL fail deterministically as `DAG_INPUT_MAPPING_INVALID` during mapping evaluation before node-input commit or child start. The node becomes failed, its dependants become dependency-blocked as applicable, and independent nodes remain valid and continue. The runtime SHALL support independent nodes whose mapper uses only immutable run input, resultless dependency-only nodes, and resultful output-bearing nodes. Every node/snapshot SHALL use zero-based authored order. `MaxConcurrentNodes` SHALL count every started nonterminal child node, including a child parked in a workflow wait, until that child becomes terminal. Child failed/timed-out/terminated SHALL map to stable `CHILD_FAILED`/`CHILD_TIMED_OUT`/`CHILD_TERMINATED`; child cancellation without a DAG-run cancellation request SHALL map to `CHILD_CANCELLED` failure. Without a run cancellation request, the run SHALL fail on any non-success child and succeed only when all nodes succeed.

#### Scenario: Host restarts during a DAG run
- **WHEN** a DAG resumes after one node child was started before host failure
- **THEN** runtime reconstruction reattaches the deterministic child identity and does not duplicate the node occurrence

#### Scenario: Required dependency fails
- **WHEN** one node's required dependency reaches failure
- **THEN** its input mapping is not invoked, it and its dependant chain become dependency-blocked as applicable, and independent ready/running nodes continue until no node can progress

#### Scenario: Opaque mapper reads an undeclared output
- **WHEN** `MapInput` calls `OutputOf` for an undeclared or non-direct dependency
- **THEN** mapping evaluation fails with `DAG_INPUT_MAPPING_INVALID` before input commit or child start, the node fails deterministically, its dependants block, and independent nodes continue

#### Scenario: Independent node maps only run input
- **WHEN** a node declares no dependencies and its one mapper uses only immutable DAG run input
- **THEN** `Build` accepts the structurally complete node and runtime mapping may commit its input and start it normally

#### Scenario: Started child is parked
- **WHEN** a started child workflow is nonterminal but parked in a wait
- **THEN** it continues to consume one `MaxConcurrentNodes` admission until terminal and another eligible node remains unadmitted when the cap is full

#### Scenario: DAG run terminalizes after mixed outcomes
- **WHEN** no node can progress after successes, failures, cancellations, and dependency blocking
- **THEN** the terminal result lists every node once in stable node order with its final succeeded, failed, cancelled, or dependency-blocked status

#### Scenario: DAG run cancellation is requested
- **WHEN** a caller requests cancellation through `DagRunHandle`
- **THEN** new admission stops, pending/ready nodes become cancelled, running nodes become cancellation-requested and propagate to children, dependency-blocked nodes remain blocked, and the run becomes cancelled only after running children are terminal

#### Scenario: DAG hosting starts a child
- **WHEN** `OrcaCore.Dag.Hosting` progresses a ready node
- **THEN** it uses the named versioned internal child-start/join bridge exposed only by `OrcaCore.Durable.Hosting` through the single friend declaration, with no public child member

### Requirement: Pending inbox matching is atomic and ordered
Direct-target inboxes and route-level correlation inboxes SHALL retain accepted unmatched envelopes in durable acceptance order. Wait registration, event acceptance, claim, timer competition, and event consumption SHALL use the same serialized mutation boundary needed to prevent both loss and double consumption. A successful consuming workflow transition SHALL atomically mark the inbox event applied; a failed/conflicted transition or host crash before that commit SHALL leave it re-matchable. Wait cancellation, timeout, scope cleanup, or instance eviction SHALL NOT silently delete an unmatched accepted event. Terminal or semantically unresolvable records SHALL become observable poison/dead-letter state.

#### Scenario: Event and wait registration race
- **WHEN** one host accepts an event while another commits the matching wait
- **THEN** exactly one atomic order wins, leaving either one claimed event and runnable owner or one pending event plus registered wait, never a lost or doubly consumed envelope

#### Scenario: Host fails while applying a claimed event
- **WHEN** a host crashes after claim but before the consuming workflow transition commits
- **THEN** recovery can reclaim the same event and apply it once logically using the unchanged event identity

### Requirement: Durable publish uses the transactional outbox
Every durable authored `Publish` SHALL create a complete `WorkflowOutboundEvent` with stable event contract name/version, replay-stable `EventId`, correlation, optional inbound causation `EventId`, origin instance/definition/version, deterministic UTC occurrence time, and fixed-codec payload. Workflow progression and the external outbox record SHALL commit atomically. The external outbox pump SHALL invoke only application `IWorkflowEventDispatcher.DispatchAsync(WorkflowOutboundEvent, CancellationToken)`, SHALL never forward internal continuation records or provider `OutboxWrite`, and SHALL preserve the same event identity across at-least-once retries. `WorkflowEventDispatchResult.Succeeded` SHALL mark dispatched, `RetryableFailure(WorkflowEventDispatchFailure)` or exception SHALL retain retryability, cancellation SHALL release the claim, and `PermanentFailure(WorkflowEventDispatchFailure)` SHALL record observable poison state carrying the immutable stable-code/optional-detail failure.

#### Scenario: Host crashes around publish commit
- **WHEN** a host fails before or after the atomic workflow/outbox commit
- **THEN** recovery observes either neither transition nor record, or both, and never a committed workflow transition with a missing outbound event

#### Scenario: Broker send succeeds before dispatch acknowledgement
- **WHEN** the process fails after the application dispatcher sends but before OrcaCore marks the record dispatched
- **THEN** a later attempt may resend the same outbound `EventId`, preserving the at-least-once contract for downstream deduplication
