## Purpose

Define optional host-local limits on concurrent orchestration work and shared external resources
while preserving OrcaCore's serialized per-instance commit authority. These transient controls are
separate from persisted cross-host durable resource leasing.

## Requirements

### Requirement: Hosts may cap concurrent step body execution

The runtime SHALL apply each configured `StepExecutionThrottle.For<TStep>(N)` as one host-local
concurrency limit keyed solely by the exact step type `TStep` across all workflow instances. A
granted physical throttle slot SHALL bracket only the actual guarded body and SHALL remain counted
until that body returns. V1 SHALL NOT expose an untyped global, per-definition, or step-category
throttle scope.

#### Scenario: Exact step type protects a shared service

- **WHEN** many workflow instances execute one step type whose exact-type throttle is 4
- **THEN** at most four bodies of that exact step type execute concurrently on that host

#### Scenario: Different step types do not share a throttle implicitly

- **WHEN** two distinct step types execute and only one has a configured throttle
- **THEN** the configured limit applies only to the exact configured type

#### Scenario: Fenced body ignores cancellation

- **WHEN** a timed-out or cancelled body continues to run after its logical result is fenced
- **THEN** its detached state cannot commit and its logical path token is released, but its physical throttle slot remains counted until the body returns

### Requirement: Named resource pools enforce shared limits

The ephemeral runtime SHALL support named host-local transient pools identified by
`TransientPoolName`, each with its own concurrency budget, so unrelated workflow instances on one
host can share a limit on a common external resource. A transient grant is not persisted as
cross-host ownership and resets on host restart. Persisted cross-host capacity is a distinct
durable lease over `ResourcePoolName`; it is not an alias or stronger configuration of this
requirement.

#### Scenario: Shared database pool

- **WHEN** steps from multiple workflow instances use one `TransientPoolName` for database updates with limit 4
- **THEN** at most four such steps hold that transient pool concurrently across all instances on that host

#### Scenario: Pools compose with instance serialization

- **WHEN** a step acquires a named transient-pool slot
- **THEN** per-instance serialized commit authority remains in effect and no two logical attempts can commit conflicting state for the same instance

#### Scenario: One pool decorator per step

- **GIVEN** multiple transient pools are configured on an ephemeral host
- **WHEN** an ephemeral business step selects a pool through `WithTransientPool(TransientPoolName)`
- **THEN** that step carries exactly that one pool requirement and cannot stack a second named pool

#### Scenario: Pool decorator binds the preceding step

- **WHEN** an author writes `.Then<A>().WithTransientPool(pool).Then<B>()`
- **THEN** only step `A` carries that pool requirement and step `B` is unpooled unless separately decorated

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
governing cancellation. Saturation SHALL NOT fail the workflow, and v1 SHALL NOT expose a fail-fast
switch, capacity-wait timeout, or provider-defined admission policy. Driver segment budgets remain
separate fairness mechanics and SHALL NOT end a capacity wait. An in-instance fiber SHALL record
its exact owned blocked obligation, end its quantum, and release the instance mutation turn before
the host awaits a grant. Ephemeral mode records that obligation in memory; durable mode commits
enough blocked path/step state before releasing the turn to re-evaluate admission after restart
without persisting slot ownership. The runtime SHALL NOT await capacity while retaining an
executing fiber quantum or instance mutation turn.

#### Scenario: Cancellation while waiting for a slot

- **WHEN** a path, exact-step-type throttle, or named-pool admission is waiting
- **AND** the governing cancellation token is signalled
- **THEN** the wait ends without acquiring the slot and propagates cancellation appropriately

#### Scenario: Local fiber waits for saturated capacity

- **WHEN** a selected local fiber cannot acquire required host-local capacity
- **THEN** it records or commits a mode-appropriate exact-owner blocked obligation, releases the instance turn before awaiting the grant, and allows another runnable sibling fiber to advance

#### Scenario: Durable host restarts while host-local capacity is held

- **WHEN** a durable host restarts while an instance held or awaited a path or step-admission slot
- **THEN** host-local capacity resets and the fiber re-evaluates admission without claiming the previous slot survived restart

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
occurrence in mode-appropriate execution state. Durable path or step admission MAY commit a blocked
re-admission obligation but SHALL NOT persist a host-local slot as a restart-surviving ticket.
Pending admission is cancelled before logical fiber removal. A granted slot remains held until its
actual guarded body returns, even after forced logical workflow termination. Durable
`ResourcePoolName` lease ownership follows its separate persistent `LeaseObligationId` contract.

#### Scenario: Losing branch owns a pending admission

- **WHEN** a `Parallel` branch is cancelled or fails while waiting for host-local admission
- **THEN** its pending admission is cancelled before the fiber is removed

#### Scenario: Forced termination occurs while a guarded body still runs

- **WHEN** an operator terminates a workflow while one body holds a transient slot
- **THEN** pending admissions cancel immediately, but the granted slot remains counted until the body returns so another body cannot exceed the host-local limit

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
structured execution-path tokens for one workflow instance. A runnable root, branch, or item SHALL
own one token; it SHALL release the token on a wait, delay, resource request, or join and reacquire
one before progressing. A fan-out parent SHALL release its token before child admission and SHALL
reacquire one only for merge or continuation. Workflow authoring SHALL NOT expose an author-global
path cap. Root fixed-`Parallel` branches SHALL queue by authored ordinal.
V1 SHALL NOT expose an independent host-wide workflow-instance or advancement ceiling.

Shared path/step settings SHALL be carried by `StructuredExecutionHostOptions` under both
role-specific engine option wrappers. Ephemeral transient pools SHALL appear only on
`EphemeralEngineHostOptions`; durable resource-pool settings SHALL appear only on
`DurableEngineHostOptions`. Engine registration SHALL occur through the matching
`AddOrcaCoreEphemeralEngine(...)` or `AddOrcaCoreDurableEngine(...)` role and SHALL include its
hosted loops; no catch-all registration or second hosted-service toggle is part of this capability.

A root `ForEach` node MAY declare a positive node-local `maxConcurrency`; the effective limit SHALL be
the lower of that value and the host ceiling. The node-local limit SHALL count admitted nonterminal
item scopes, including parked items, until terminal completion; it is distinct from runnable path
tokens. Durable `ForEach` SHALL commit its finite item snapshot and node-local limit, then re-admit
unfinished items after restart without persisting a host token.

#### Scenario: Host tightens a ForEach node

- **GIVEN** a `ForEach` node declares `maxConcurrency` 100
- **AND** the host configures `MaxConcurrentExecutionPathsPerInstance` 10
- **WHEN** the node runs
- **THEN** no more than 10 item scopes are admitted for that instance

#### Scenario: Node tightens the host ceiling

- **GIVEN** a `ForEach` node declares `maxConcurrency` 4
- **AND** the host configures `MaxConcurrentExecutionPathsPerInstance` 10
- **WHEN** the node runs
- **THEN** no more than 4 item scopes are admitted for that node

#### Scenario: Root Parallel admission is deterministic

- **WHEN** a root fixed `Parallel` node has more ready branches than the host path ceiling
- **THEN** the runtime admits branches in authored order and later branches remain runnable but unadmitted until capacity becomes available

#### Scenario: Fan-out progresses with a path ceiling of one

- **GIVEN** the host configures `MaxConcurrentExecutionPathsPerInstance` to 1
- **WHEN** a root parent reaches a `Parallel` or `ForEach` join
- **THEN** the parent releases its token before child admission, children run one at a time, and the parent reacquires a token only for merge or continuation without deadlock

#### Scenario: Durable host configures structured execution

- **WHEN** a host registers `AddOrcaCoreDurableEngine(DurableEngineHostOptions)`
- **THEN** shared path/step limits come from `DurableEngineHostOptions.StructuredExecution`, durable resource pools come from its durable pool options, and no ephemeral transient-pool collection is accepted

#### Scenario: Parked ForEach item still consumes the node limit

- **GIVEN** a `ForEach` node has reached its effective node-local concurrency limit
- **AND** one admitted item is parked in a wait
- **WHEN** another item is ready
- **THEN** the ready item remains unadmitted until an admitted item becomes terminal even though the parked item does not own a runnable path token

### Requirement: Transient governance is distinct from durable leasing

Per-step execution throttles and named cross-instance transient pools SHALL NOT be named,
documented, or serialized as durable resource leases. Transient authoring/configuration SHALL use
`TransientPoolName`. Durable resource leases SHALL use `ResourcePoolName` plus a separate persisted
exact-occurrence contract with atomic request, deterministic release, and review-mark/owner-state
reconciliation. A review deadline SHALL NOT imply reclaim. Durable leasing has no holder renewal or
force-release operation; trusted stop/fence confirmation is its only quarantine-release seam.

#### Scenario: Developer compares governance capabilities

- **WHEN** public authoring and operator documentation describe a transient pool and a durable lease
- **THEN** their names, types, persistence, recovery, and release rules clearly distinguish host-local reset semantics from durable exact-owner reservation

### Requirement: Builder discoverability is mode-guaranteed

A static mode-first builder SHALL expose `WithTransientPool(TransientPoolName)` only on the
ephemeral first-release builder. Durable root and nested builders SHALL keep the member absent and
compiler-rejected. Host registration or dependency-injection composition SHALL NOT add methods
after mode selection; durable named transient-pool authoring requires a future explicit matrix
amendment.

#### Scenario: Durable host profile enables transient governance

- **WHEN** one durable host profile configures transient-governance infrastructure
- **THEN** durable root and nested builders still do not expose transient-pool authoring and no definition metadata is silently ignored
