# structured-fiber-execution Specification

## Purpose
TBD - created by archiving change adopt-structured-fiber-execution. Update Purpose after archive.
## Requirements

### Requirement: Definitions compile to stable executable plans
The system SHALL compile every accepted workflow definition into an immutable executable plan before registration. The plan SHALL contain stable instruction, scope, branch, and merge identities plus a deterministic structural fingerprint covering only inspectable authored structure and fixed codec format. Compiler format, workflow mode, definition identity/version, and compiler options SHALL remain separate bindings and SHALL NOT contribute to that fingerprint.

#### Scenario: Same version has different executable structure
- **WHEN** a definition identifier and version are registered with a structural fingerprint different from the previously registered fingerprint
- **THEN** registration fails explicitly instead of replacing the executable plan used by existing instances

### Requirement: A fiber advances one linear instruction position
The runtime SHALL represent each active execution path as a fiber with one current instruction position, one lifecycle state, one owning scope, and fiber-local execution data. The interpreter SHALL advance only the selected fiber during a fiber quantum. Reaching the runtime-owned internal-instruction budget MAY end and requeue the quantum without exposing an authored `Yield`.

#### Scenario: Internal instruction budget ends a quantum
- **WHEN** the selected fiber reaches the runtime-owned internal-instruction budget without reaching another quantum-ending operation
- **THEN** its committed position is preserved, its quantum ends, and it is placed after already-runnable sibling fibers without exposing an authored `Yield`

### Requirement: Compiled plans contain explicit structural continuations
The compiler SHALL emit stable structural continuation instructions for authored control flow. Conditional paths SHALL converge at `IfJoin`, loops SHALL use explicit loop-back and loop-exit targets, and accepted root fan-out scopes SHALL use `ScopeJoin` and `ScopeExit` positions distinct from semantic branch/item `Return` and workflow `End`.

#### Scenario: Fan-out scope is compiled
- **WHEN** an authored root `Parallel` or bounded root `ForEach` with `WhenAll` or `WhenAllOutcomes` is compiled
- **THEN** the plan contains explicit scope join and scope exit positions used for terminal-result collection, cleanup, merge, and parent continuation

### Requirement: Structured scopes preserve the parent fiber
Every accepted root fan-out construct SHALL compile to a single-entry/single-exit execution scope. Starting a scope SHALL suspend and preserve the parent fiber, release its execution-path token, and resume the same parent fiber only after the scope reaches its join outcome and the parent reacquires a token. Starting fixed root `Parallel` SHALL create every authored branch fiber with deterministic identity before scheduling any branch; there SHALL be no separate branch or live-fiber admission resource. Starting root `ForEach` SHALL commit its finite item metadata and create item fibers only through its separate bounded admitted-item rule.

#### Scenario: Hand-built nested fan-out reaches compilation
- **WHEN** a stale or manually constructed graph places `Parallel`, `ForEach`, or `While` inside a nested, branch, item, or leased body
- **THEN** compiler defense rejects the graph before registration instead of creating another fan-out scope

#### Scenario: Fixed Parallel starts above the path ceiling
- **WHEN** a fixed root `Parallel` contains more branches than `MaxConcurrentExecutionPathsPerInstance`
- **THEN** every branch fiber exists at scope start and runnable branches wait only for path tokens in authored order

### Requirement: Runtime scope and fiber identities derive from committed state
The runtime SHALL derive each root fiber, runtime scope, authored child fiber, and dynamic item fiber identity from stable plan identity and committed execution state. A root fiber SHALL include instance identity and `ContinueAsNew` generation. A scope SHALL include parent fiber identity, `ScopePlanId`, and the parent's persisted scope-entry sequence. A child fiber SHALL include scope identity and authored branch identity or stable item index.

#### Scenario: Loop re-enters a scope across restart
- **WHEN** a host crashes after a scope inside a loop is created and another host reloads or receives a duplicate continuation claim
- **THEN** replay observes or derives the same `ScopeId` and child `FiberId` values for that loop iteration and the next iteration uses a different committed scope-entry sequence

#### Scenario: ContinueAsNew starts another generation
- **WHEN** a root `ContinueAsNew` transition commits and the same compiled instructions execute in the new generation
- **THEN** the new root, scope, and child identities differ from prior-generation identities while remaining deterministic across replay

### Requirement: Local fibers use cooperative scheduling
The runtime SHALL execute local workflow fibers cooperatively and SHALL NOT run local business-step bodies concurrently within one workflow instance. The scheduler SHALL use stable authored branch order with a persisted next-fiber position so every continuously runnable fiber receives a turn within a bounded number of sibling quanta. Each quantum SHALL execute at most one user step and at most the positive configured `MaxInternalInstructionsPerQuantum`, whose default is 1024.

#### Scenario: Multiple siblings remain runnable
- **WHEN** two or more child fibers remain runnable across multiple quanta
- **THEN** scheduling rotates through them without allowing an earlier fiber identity to starve a later sibling

#### Scenario: Host restarts between sibling turns
- **WHEN** a durable host restarts after committing one sibling quantum
- **THEN** rehydration preserves the next-fiber position and does not restart scheduling from the first branch

#### Scenario: Internal-instruction budget is reached
- **WHEN** a fiber executes `MaxInternalInstructionsPerQuantum` structural instructions without another quantum-ending operation
- **THEN** the runtime ends the quantum successfully, persists or records current progress, increments a diagnostic counter, and requeues the fiber behind already-runnable siblings

#### Scenario: Loop cannot end a quantum
- **WHEN** compilation finds a reachable loop cycle containing no user step, suspension, `Yield`, scope transition, branch return, or terminal instruction
- **THEN** compilation rejects the definition instead of admitting a structural-only hot loop

### Requirement: Branches cannot mutate parent business state directly
Each local branch SHALL execute against authored serializable branch input and fiber-local business data. A branch SHALL return one serializable value of the scope's declared result type and SHALL NOT receive mutable access to the suspended parent business state.

#### Scenario: Scope creates child fibers
- **WHEN** a parent starts a scope
- **THEN** each child receives its authored input snapshot or projection and mutations remain local to that child

#### Scenario: Scope declares heterogeneous data
- **WHEN** branches need to return different logical result shapes
- **THEN** the author represents them through one declared union, record, or other common serializable result type

### Requirement: Ephemeral ForEach uses dynamic isolated item fibers
Ephemeral `ForEach` SHALL compile to a dynamic execution scope whose stable ordered work descriptors create item fibers with isolated input and private state. Each item SHALL produce an ordered `ForEachItemOutcome<TResult>`. Durable compilation SHALL reject `ForEach` until durable dynamic local fanout is separately specified.

#### Scenario: ForEach materializes partitioned work
- **WHEN** an ephemeral `ForEach` selector and partitioner produce work descriptors
- **THEN** descriptors receive stable item indexes and admitted item fibers derive identity from the runtime scope and item index

#### Scenario: ForEach reaches its admission limit
- **WHEN** the number of admitted nonterminal item fibers reaches `maxConcurrency`
- **THEN** later descriptors remain pending until an admitted item becomes terminal, while local step bodies still execute cooperatively one at a time

#### Scenario: ForEach WhenAll succeeds
- **WHEN** every item fiber succeeds
- **THEN** outcomes are ordered by item index and an authored merge, when present, executes once over that ordered list before the parent resumes

#### Scenario: ForEach WhenAny selects an item
- **WHEN** the first terminal item is committed under `WhenAny`
- **THEN** same-commit ties use item index, all remaining item fibers and pending descriptors are cancelled, and no let-remaining work survives parent continuation

#### Scenario: ForEach WhenAny first terminal item fails
- **WHEN** the first committed terminal item under `WhenAny` is failed
- **THEN** that item is the winner, the scope fails without merge, and all remaining admitted fibers and pending descriptors are cancelled

#### Scenario: ForEach WhenAny winner succeeds
- **WHEN** the first committed terminal item under `WhenAny` succeeds and a winner merge is declared
- **THEN** the merge receives exactly that `ForEachItemOutcome<TResult>` as a single-element item-index-ordered list

#### Scenario: ForEach allows partial failures
- **WHEN** `ContinueWithPartialFailures` reaches terminal outcomes for all admitted and pending work
- **THEN** an authored merge may inspect the complete ordered success/failure outcome list, while a resultless overload leaves parent state unchanged and exposes status through management

### Requirement: Merge is explicit, deterministic, and side-effect free
Every `WhenAll` or `WhenAllOutcomes` scope SHALL declare exactly one merge operation. `WhenAll` merge SHALL receive successful branch or item results in stable authored/index order and run only when all children succeed. `WhenAllOutcomes` merge SHALL receive one immutable success-or-failure outcome for every child in the same stable order after all children become terminal. Either merge SHALL execute without asynchronous work or external side effects and SHALL replace the preserved parent state as one logical committed transition.

#### Scenario: Host crashes around merge
- **WHEN** a host crashes before or after the merge commit
- **THEN** replay either executes the uncommitted merge from persisted ordered inputs or observes the committed merged state without rerunning completed child bodies

### Requirement: Join policies define one scope outcome
`WhenAll` SHALL wait for every branch or item to become terminal, SHALL merge once only when all succeed, and otherwise SHALL fail after all finish without automatically cancelling siblings. One failure SHALL be preserved directly; multiple failures SHALL become `SFE-JOIN-FAILED` with causes ordered by authored branch or item index. `WhenAllOutcomes` SHALL wait for every child, merge the complete ordered success/failure outcomes once, and complete the scope successfully. An ancestor cancellation, termination, or workflow deadline SHALL suppress either merge instead of creating a merge-visible cancellation outcome.

#### Scenario: A WhenAll branch fails
- **WHEN** any `WhenAll` branch commits failure before the scope joins
- **THEN** remaining branch work is allowed to reach terminal, merge does not execute, and the scope fails with the ordered failure result

#### Scenario: A WhenAllOutcomes branch fails
- **WHEN** fixed branches reach a mixture of success and failure in any completion order
- **THEN** the merge executes once with authored-order success/failure outcomes and the parent resumes from its replacement state

#### Scenario: Ancestor terminal transition wins
- **WHEN** workflow cancellation, termination, or workflow deadline commits while the scope is active
- **THEN** child work is signalled or fenced, merge is suppressed, and no branch or item cancellation outcome is fabricated

### Requirement: Scope ownership covers every residual obligation
Every wait, timer, pending resume, child scope, resource ticket, retry record, and cancellation record created by a fiber SHALL persist its owning `FiberId` and `ScopeId`. A scope SHALL NOT complete or disappear until every non-detached owned obligation is completed, durably released, or transferred to its separately governed terminal ownership state.

#### Scenario: Scope reaches its join
- **WHEN** every child is terminal and the selected join can be evaluated
- **THEN** cleanup and ownership transitions commit before or atomically with the join result and parent continuation

### Requirement: Local fibers use bounded execution-path scheduling
The runtime SHALL schedule runnable local workflow fibers under the positive host-owned `MaxConcurrentExecutionPathsPerInstance` ceiling. A runnable root, branch, or item fiber SHALL own one logical execution-path token; parking on a wait, delay, resource request, or join SHALL release it, and progression SHALL reacquire it. The scheduler SHALL use stable authored branch order or item index with persisted next-selection and, for `ForEach`, next-admission positions so every continuously runnable admitted fiber receives a turn within a bounded number of sibling quanta. Each quantum SHALL execute at most one user step and at most the positive configured `MaxInternalInstructionsPerQuantum`, whose default is 1024. No implementation-only live-fiber ceiling SHALL reject, delay, or terminally fail an otherwise accepted workflow.

#### Scenario: Parent fans out under a ceiling of one
- **WHEN** the parent reaches root `Parallel` or root `ForEach` while the host path ceiling is one
- **THEN** the parent releases its token before child scheduling and reacquires only for merge or continuation, so path-token capacity alone cannot create a parent-held-token deadlock

### Requirement: Bounded root ForEach uses dynamic isolated item fibers
Root-only `ForEach` in both modes SHALL compile to a dynamic execution scope from one finite fixed-codec-detached item snapshot. `ForEachOptions.MaxItems` SHALL be positive, optional `MaxConcurrency` SHALL be positive when present, and the selected snapshot SHALL be rejected before partial admission when it exceeds the authored item or applicable encoded-value bound. Stable zero-based item indexes SHALL determine fiber identity, admission order, result order, and replay identity. Each item SHALL execute with isolated private state projected only from its detached item and index. At most the lower of the host path ceiling and optional node-local limit SHALL be admitted as nonterminal item scopes. Parking SHALL release the item's path token but retain its admitted-item slot until terminal. Eventual admission of every item SHALL be conditional on admitted items not depending on pending items.

#### Scenario: Durable host restarts after snapshot commit
- **WHEN** a durable host reloads a scope with terminal and unfinished item records
- **THEN** it reuses the committed snapshot and identities, does not run the selector again, and re-admits unfinished items in index order without persisting host-local tokens

#### Scenario: ForEach snapshot is empty
- **WHEN** the detached selected list contains no items
- **THEN** no item is admitted and the selected merge executes once with an empty ordered collection

#### Scenario: Admitted items await a pending item
- **WHEN** every admitted item parks awaiting an effect that only a later pending item can produce
- **THEN** the pending item remains unadmitted while the admitted-item limit is full, and v1 does not claim global progress for that authored dependency
