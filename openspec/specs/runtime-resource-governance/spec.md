## Purpose

Define optional host-local limits on concurrent orchestration work and shared external resources
while preserving OrcaCore's serialized per-instance commit authority. These transient controls are
separate from persisted cross-host durable resource leasing.

## Requirements

### Requirement: Hosts may cap concurrent step body execution
The runtime SHALL apply each configured `StepExecutionThrottle.For<TStep>(N)` as one host-local
concurrency limit keyed solely by the exact named step type `TStep` authored through `Then<TStep>()`
across all workflow instances. An ephemeral lambda has no inferred exact-type throttle target. A
granted slot SHALL bracket only the actual guarded body and SHALL remain counted until that body
returns. V1 SHALL NOT expose assignable/base-type matching, an untyped global, per-definition, or
step-category throttle scope.

#### Scenario: Exact step type protects a shared service
- **WHEN** many workflow instances execute one step type whose exact-type throttle is 4
- **THEN** at most four bodies of that exact step type execute concurrently on that host

#### Scenario: Different step types do not share a throttle implicitly
- **WHEN** two distinct step types execute and only one has a configured throttle
- **THEN** the configured limit applies only to the exact configured type

### Requirement: Named resource pools enforce shared limits
The ephemeral runtime SHALL support named host-local transient pools, each identified by
`TransientPoolName` and carrying its own concurrency budget, so unrelated workflow instances
on one host can share a limit on a common external resource. A transient grant is not persisted
as cross-host ownership and resets on host restart. Persisted cross-host capacity is a distinct
durable lease over `ResourcePoolName`; it is not an alias or a stronger configuration of this
requirement.

Ephemeral definition registration SHALL validate every authored `TransientPoolName` against the
host's copied configured pool catalog before registry mutation or fingerprint-conflict evaluation.
If any are absent, the common registry SHALL return
`HostIncompatible.MissingTransientPools` carrying a defensively copied, distinct, ordinal-sorted
list of every missing name. Saturation remains a runtime wait and is not a compatibility failure.

#### Scenario: Shared database pool
- **WHEN** steps from multiple workflow instances use one `TransientPoolName` for database
  updates with limit 4
- **THEN** at most four such steps hold that transient pool concurrently across all instances
  on that host

#### Scenario: Pools compose with instance serialization
- **WHEN** a step acquires a named transient-pool slot
- **THEN** per-instance serialized mutation rules remain in effect such that instance state is
  not concurrently mutated by multiple logical mutators

#### Scenario: One pool decorator per step
- **GIVEN** multiple transient pools are configured on an ephemeral host
- **WHEN** an ephemeral business step selects a pool through `WithTransientPool(TransientPoolName)`
- **THEN** that step carries exactly that one pool requirement and cannot stack a second named pool

#### Scenario: Pool decorator binds the preceding step
- **WHEN** an author writes `.Then<A>().WithTransientPool(pool).Then<B>()`
- **THEN** only step `A` carries that pool requirement and step `B` is unpooled unless separately decorated

#### Scenario: Definition names unconfigured transient pools
- **WHEN** ephemeral registration inspects a definition that names one or more pools absent from the host catalog
- **THEN** it returns the complete sorted `MissingTransientPools` failure before registry mutation, fingerprint conflict, or execution

### Requirement: Governance scope is explicit for infrastructure paths

The runtime SHALL document which execution paths participate in each limit category so operators
can predict whether durable outbox, inbox, timer, continuation, or resource-governance pumps compete
with business steps.

#### Scenario: Dispatch path classification

- **WHEN** operators configure governance limits
- **THEN** documentation or runtime metadata identifies whether each infrastructure loop shares capacity with business steps or uses explicitly separate capacity

### Requirement: Saturation parks the exact owner
When an approved per-instance path, exact-step-type throttle, or ephemeral named-pool limit is
saturated, the runtime SHALL asynchronously park the exact requesting owner until grant or
governing cancellation. Saturation SHALL NOT fail the workflow, and v1 SHALL NOT expose a
fail-fast switch, capacity-wait timeout, or provider-defined admission policy. Driver segment
budgets remain separate fairness mechanics and SHALL NOT end a capacity wait. An in-instance fiber
SHALL record its exact owned blocked obligation, end its quantum, and release the instance
mutation turn before the host awaits a grant. Ephemeral mode records that obligation in memory;
durable mode commits enough blocked path/step state before releasing the turn to re-evaluate
admission after restart without persisting slot ownership. A granted physical step-throttle or
transient-pool slot SHALL remain counted until the guarded body actually returns, even when
timeout/cancellation has fenced its logical result and released its execution-path token.

#### Scenario: Cancellation while waiting for a slot
- **WHEN** a path, exact-step-type throttle, or named-pool admission is waiting
- **AND** the governing cancellation token is signaled
- **THEN** the wait ends without acquiring the slot and propagates cancellation appropriately

#### Scenario: Local fiber waits for saturated capacity
- **WHEN** a selected local fiber cannot acquire required host-local capacity
- **THEN** it records or commits a mode-appropriate exact-owner blocked obligation, releases the instance turn before awaiting the grant, and allows another runnable sibling fiber to advance

#### Scenario: Timed-out guarded body ignores cancellation
- **WHEN** a timed-out step body continues running after its logical attempt has been fenced
- **THEN** its detached state can no longer commit and its logical execution-path token is released, but every granted physical step-throttle or transient-pool slot remains counted until the body returns

#### Scenario: Durable host restarts while host-local capacity is held
- **WHEN** a durable host restarts while an instance held or awaited a path/step admission slot
- **THEN** host-local capacity is reset and the fiber re-evaluates admission without claiming that the previous slot survived restart

### Requirement: Observability hooks exist for operators

When governance is enabled, the runtime SHALL expose stable metrics or debug counters for configured
limits, active slots, wait depth, cancellation, and `TransientPoolName` without mixing transient
capacity with durable lease tickets. Saturation has no rejection metric because it parks; invalid
host configuration fails startup separately.

#### Scenario: Operator inspects transient-pool pressure

- **WHEN** transient pools are saturated during a load test
- **THEN** operators can observe the configured limit, active count, and wait depth through the documented introspection mechanism

### Requirement: Resource ownership follows fibers and scopes
Every pending host-local admission SHALL record the owning workflow instance and exact fiber
occurrence in mode-appropriate execution state. Ephemeral named-pool admission is recorded in
memory. Durable path/step admission may commit the blocked/re-admission obligation but SHALL
NOT persist a host-local slot as a restart-surviving ticket. Pending admission is cancelled
before logical fiber removal.
A granted slot, however, remains held until its actual guarded step body returns or stops;
forced workflow termination cannot release it while that body still runs. The host owns this
short-lived cleanup even after logical fiber removal; process exit resets the slot only because
in-process guarded work has also stopped. Durable `ResourcePoolName` lease ownership follows its
separate persistent `LeaseObligationId` contract and is outside this transient-governance change.

#### Scenario: Losing branch owns a host-local admission
- **WHEN** a `Parallel` branch is cancelled or fails while waiting for host-local admission
- **THEN** its pending admission is cancelled before the fiber is removed, except that a
  still-running guarded body retains its granted slot until it actually stops

#### Scenario: Forced termination occurs while a guarded body still runs
- **WHEN** an operator force-terminates a workflow while one step body holds a transient slot
- **THEN** pending admissions cancel immediately, but the granted slot remains counted until the
  body returns/stops, so another body cannot exceed the configured host-local limit

### Requirement: Local fiber scheduling is distinct from host concurrency

Host governance SHALL NOT imply simultaneous commit authority for local paths. At most one unfenced
attempt owns commit authority for a workflow instance, every attempt mutates a detached state copy,
and parking one local path releases the instance turn so a runnable sibling can progress.

#### Scenario: One instance has many runnable local paths

- **WHEN** a workflow instance has multiple runnable branches or items
- **THEN** the scheduler advances them cooperatively while host limits continue to govern physical work across instances

### Requirement: Resource waits do not retain an executing fiber quantum

A fiber that cannot acquire required host-local capacity SHALL record or commit its blocked
obligation and release the instance mutation turn before waiting. It SHALL become runnable only
after the mode's grant or cancellation transition.

#### Scenario: Transient pool is saturated

- **WHEN** the selected fiber cannot acquire its required transient pool
- **THEN** it blocks without repeatedly occupying the local scheduler and another runnable fiber can advance

### Requirement: Execution-path concurrency is host-owned
The host SHALL own `MaxConcurrentExecutionPathsPerInstance` as the upper bound on countable
structured execution-path tokens for one workflow instance. A runnable root, branch, or item
SHALL own one token. It SHALL release the token when it parks on a wait, delay, resource request,
or join and SHALL reacquire a token before progressing. A parent SHALL release its token before
scheduling fixed branches or admitting items and SHALL reacquire one only for merge/continuation.
Workflow authoring SHALL NOT expose a global option that overrides or duplicates that host limit.
Every fixed root-`Parallel` branch fiber SHALL exist when its scope starts; runnable branches SHALL
receive tokens fairly in authored order, and no separate branch or live-fiber admission resource
SHALL affect workflow acceptance, scheduling, or outcome. Waiting, delayed, and terminal paths
SHALL NOT consume a token solely because their state exists. V1 SHALL NOT expose an independent
host-wide workflow-instance or advancement ceiling.

Shared path/step settings SHALL be carried by `StructuredExecutionHostOptions` under both
role-specific engine option wrappers. Ephemeral transient pools SHALL appear only on
`EphemeralEngineHostOptions`; durable resource-pool settings SHALL appear only on
`DurableEngineHostOptions`. Engine registration SHALL occur through the matching
`AddOrcaCoreEphemeralEngine(...)` or `AddOrcaCoreDurableEngine(...)` role and SHALL include its
hosted loops; no catch-all registration or second hosted-service toggle is part of this capability.
The common registry SHALL return `EngineModeMismatch` before pool validation when a definition's
mode does not match the selected engine role.

A root `ForEach` node MAY declare a positive node-local `maxConcurrency`. Its effective limit SHALL
be the lower of the node-local value and `MaxConcurrentExecutionPathsPerInstance`; when the
node-local value is omitted, the host ceiling applies. This node-local limit SHALL count admitted
nonterminal item scopes, including items parked in a wait, delay, or resource admission, until each item
becomes terminal; it is distinct from the runnable path-token count. Bounded durable `ForEach`
 SHALL commit its finite item set and node-local limit, but SHALL re-admit unfinished item paths
after restart without treating a host slot as persisted ownership. `DagHostOptions.MaxConcurrentNodes`
SHALL remain a separate host-owned limit and count every started nonterminal child instance,
including a child parked in a wait, delay, or durable lease queue, until its node becomes terminal.
Releasing an in-instance path token SHALL NOT free DAG admission.

Within one structured root fan-out scope, path tokens and admitted root-`ForEach` item slots are the
only two quantities owned by the structured-fiber scheduler. This scope statement does not merge or replace exact-step
throttles, named transient pools, durable resource leases, or the independent DAG-node ceiling.
Eventual admission of every `ForEach` item is conditional on admitted items not depending on
pending items.

#### Scenario: Host tightens a ForEach node
- **GIVEN** a `ForEach` node declares `maxConcurrency` 100
- **AND** the host configures `MaxConcurrentExecutionPathsPerInstance` 10
- **WHEN** the node runs
- **THEN** no more than 10 item paths are admitted for that instance

#### Scenario: Node tightens the host ceiling
- **GIVEN** a `ForEach` node declares `maxConcurrency` 4
- **AND** the host configures `MaxConcurrentExecutionPathsPerInstance` 10
- **WHEN** the node runs
- **THEN** no more than 4 item paths are admitted for that node

#### Scenario: Root Parallel token scheduling is deterministic
- **WHEN** a root fixed `Parallel` node has more ready branches than the host path ceiling
- **THEN** every branch fiber exists and runnable branches receive path tokens fairly in authored order without a separate admission state

#### Scenario: Fan-out progresses with a path ceiling of one
- **GIVEN** the host configures `MaxConcurrentExecutionPathsPerInstance` to 1
- **WHEN** a root parent reaches a `Parallel` or `ForEach` join
- **THEN** the parent releases its token before child scheduling, one runnable child at a time can receive it, and the parent reacquires a token only for merge/continuation without a deadlock caused solely by retaining path-token capacity

#### Scenario: Durable host configures structured execution
- **WHEN** a host registers `AddOrcaCoreDurableEngine(DurableEngineHostOptions)`
- **THEN** shared path/step limits come from `DurableEngineHostOptions.StructuredExecution`, durable resource pools come from its durable pool options, and no ephemeral transient-pool collection is accepted

#### Scenario: Parked ForEach item still consumes the node limit
- **GIVEN** a `ForEach` node has reached its effective node-local concurrency limit
- **AND** one admitted item is parked in a wait
- **WHEN** another item is ready
- **THEN** the ready item remains unadmitted until an admitted item becomes terminal even though the parked item does not own a runnable path token

#### Scenario: Admitted ForEach items depend on pending work
- **WHEN** every admitted item is parked awaiting an effect that only a pending item would produce
- **THEN** later admission may remain blocked because path-token release does not release the admitted-item slot, and the runtime makes no global-progress promise for that authored dependency

#### Scenario: Durable ForEach restarts under current host admission
- **WHEN** a durable host restarts with unfinished committed items
- **THEN** it re-admits them in item-index order under the lower host/node limit without treating the former host slot as persisted ownership

#### Scenario: Parked DAG child still consumes DAG admission
- **GIVEN** a DAG has reached `DagHostOptions.MaxConcurrentNodes`
- **AND** one started child is parked in a wait, delay, or durable lease queue
- **WHEN** another DAG node becomes ready
- **THEN** the ready node remains unadmitted until a started child becomes terminal, even though the parked child may own no workflow execution-path token

### Requirement: Transient governance is distinct from durable leasing
Per-step execution throttles and named cross-instance transient pools SHALL NOT be named, documented, or serialized as durable resource leases. Named transient authoring/configuration SHALL use `TransientPoolName`. Durable resource leases SHALL use distinct `ResourcePoolName` plus a separate persisted exact lexical fiber/scope-occurrence contract with atomic request, `AmbiguousHeld` retry retention, quarantine transfer, deterministic release, and review-mark/owner-state reconciliation. Their authoring placements SHALL be the durable root, root-nested `If`/`While` bodies, and independent durable root-`Parallel` branch/root-`ForEach` item bodies only when no live ancestor lease exists; dedicated leased builders SHALL expose no fan-out, nested acquisition, or `ContinueAsNew`. A review deadline SHALL NOT imply reclaim, and this transient-governance capability SHALL NOT implement leasing, introduce holder renewal, or expose force release.

#### Scenario: Developer compares pool capabilities
- **WHEN** public authoring and operator documentation describe a transient pool and a durable lease
- **THEN** both names and parameter types distinguish host-local reset semantics from persisted queueing, deterministic exact-owner release, mark-and-reconcile recovery, and audited operator action

### Requirement: Builder discoverability is mode-guaranteed
A static mode-first builder SHALL expose `WithTransientPool(TransientPoolName)` only on the ephemeral first-release builder. Durable root and nested builders SHALL keep the member absent and compiler-rejected. Host registration, DI composition, or implementation evidence SHALL NOT add methods after mode selection; durable named-pool authoring requires a future explicit matrix amendment.

#### Scenario: Durable host profile enables transient governance
- **WHEN** one durable host profile configures transient-pool infrastructure during the first release
- **THEN** the selected durable root and nested builders still do not expose transient-pool authoring and no definition metadata is silently ignored
