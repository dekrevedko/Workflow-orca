## Purpose

Define the durable execution semantics for the state-driven OrcaCore runtime, including persistence, rehydration, and long-running workflow behavior.
## Requirements
### Requirement: Durable mode persists runtime-owned state for rehydration
The durable runtime SHALL persist workflow business payloads, fiber-local payloads, fibers, scopes, scheduling state, waits, owned obligations, and pending merge results through a store contract so every committed execution position can be rehydrated after host restart.

#### Scenario: Host restarts while an instance is suspended
- **WHEN** a durable workflow instance is resumed after process restart
- **THEN** the runtime reconstructs the instance from the complete committed envelope instead of relying on prior in-memory objects or recomputing ownership from cursor paths

### Requirement: Wait and WaitLong have distinct residency behavior
The durable runtime SHALL model both resident waits and cold waits, with `Wait` remaining available for hot instances and `WaitLong` explicitly representing cold durable residency.

#### Scenario: Long-running wait is authored
- **WHEN** a workflow uses `WaitLong`
- **THEN** the runtime checkpoints the instance for durable resume rather than requiring the instance to remain hot in memory

### Requirement: Durable instances remain bound to definition version
The durable runtime SHALL bind each instance to its definition identifier, authored definition version, compiler format version, and executable-plan fingerprint. Resume SHALL fail with explicit diagnostics when any required binding is unavailable or incompatible.

#### Scenario: New definition version is deployed
- **WHEN** an older durable instance resumes after a newer version has been registered
- **THEN** the runtime resolves the instance's bound compiled plan or fails explicitly instead of silently selecting the newer plan

#### Scenario: Graph changes under the same definition version
- **WHEN** registration or resume observes a plan fingerprint different from the fingerprint bound to the instance
- **THEN** the operation fails with a version-binding diagnostic before executing an instruction

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
When resource governance is enabled for the durable host, limits SHALL apply to configured execution paths (such as step bodies or advancement workers) in a manner consistent with the `runtime-resource-governance` capability, without weakening crash safety or optimistic concurrency guarantees at the store boundary.

#### Scenario: Rehydrated instance competes for a shared pool
- **WHEN** a durable instance resumes from storage and executes a step bound to a named pool
- **THEN** pool acquisition and release behavior matches non-durable hosts except where explicitly documented for infrastructure loops such as outbox dispatch

### Requirement: Durable envelope records the complete structured execution state
The durable execution envelope SHALL be versioned and SHALL persist parent and child fiber records, recursive scope records, scheduler position, fiber-local payloads, branch results awaiting merge, owned-obligation identities, definition identity, `ContinueAsNew` generation, compiler format version, and executable-plan fingerprint. Fiber records SHALL include loop iteration and next scope-entry sequence. Scope records SHALL include `ScopePlanId` and the committed scope-entry sequence used to mint runtime identity.

#### Scenario: Host restarts with nested scopes
- **WHEN** a host restarts while parent and child fibers are blocked at different nested scopes
- **THEN** the runtime reconstructs exact fiber positions, scope ancestry, ownership, results, and next-fiber scheduling position from committed state

#### Scenario: Envelope format is unsupported
- **WHEN** persisted execution state uses an unknown or retired envelope format
- **THEN** the runtime parks or rejects the instance with an explicit diagnostic instead of guessing how to interpret the payload

#### Scenario: Scope inside a loop is rehydrated
- **WHEN** a checkpoint contains a loop fiber that has entered the same `ScopePlanId` multiple times
- **THEN** the envelope distinguishes every runtime scope through persisted loop and scope-entry progress and restores the exact current owner identities

### Requirement: Scope transitions and effects commit atomically
Any transition that creates, blocks, completes, cancels, joins, or merges fibers and scopes SHALL atomically commit the updated execution envelope with all corresponding workflow facts, obligation changes, outbox records, and continuation signals.

#### Scenario: Branch completion makes a scope joinable
- **WHEN** the final required branch result is committed
- **THEN** the result, joinable scope state, residual cleanup facts, and any required continuation signal are committed as one transition

#### Scenario: Commit loses an optimistic concurrency race
- **WHEN** two hosts attempt to commit advancement for the same instance
- **THEN** at most one transition commits and the loser reloads the complete envelope before any retry

### Requirement: Development refactor does not retain cursor execution
The structured-fiber runtime SHALL replace the cursor split/join executor as one execution path. It SHALL NOT select between cursor and fiber interpreters based on persisted instance shape.

#### Scenario: Development store contains a cursor envelope
- **WHEN** a store created before the structured-fiber refactor contains an old cursor envelope
- **THEN** the runtime rejects it with a format diagnostic and development operators reset or recreate that data

### Requirement: ContinueAsNew requires a quiescent root scope
`ContinueAsNew` SHALL be valid only from the root fiber when it is the sole nonterminal fiber and no active descendant scope or outstanding owned obligation exists. A violation SHALL reject rollover and fail the workflow with stable runtime diagnostic `SFE-RUN-001` (`ContinueAsNewRequiresQuiescentRoot`) without changing generation, replacement state, or ownership. A valid rollover SHALL increment generation before deriving the new root fiber and scope identities.

#### Scenario: Root requests rollover with an active scope
- **WHEN** root `ContinueAsNew` is evaluated while any descendant scope, child fiber, wait, timer, child group, job, resource ticket, or pending cleanup remains active
- **THEN** rollover is rejected, the instance transitions to `Failed` with `SFE-RUN-001`, and the existing generation, replacement state, and ownership remain unchanged

#### Scenario: Quiescent root rolls over
- **WHEN** the sole runnable root fiber executes `ContinueAsNew` with no outstanding owned work
- **THEN** one atomic transition increments generation, installs the replacement root state, and creates the deterministic new-generation root identity
