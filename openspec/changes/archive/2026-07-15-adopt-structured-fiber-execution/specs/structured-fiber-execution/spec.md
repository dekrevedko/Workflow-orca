## ADDED Requirements

### Requirement: Definitions compile to stable executable plans
The system SHALL compile every accepted workflow definition into an immutable executable plan before registration. The plan SHALL contain stable instruction, scope, branch, and merge identities plus a deterministic plan fingerprint.

#### Scenario: Definition is registered
- **WHEN** a workflow definition passes authoring validation
- **THEN** registration publishes one immutable compiled plan and its fingerprint atomically

#### Scenario: Same version has different executable structure
- **WHEN** a definition identifier and version are registered with a plan fingerprint different from the previously registered fingerprint
- **THEN** registration fails explicitly instead of replacing the executable plan used by existing instances

### Requirement: A fiber advances one linear instruction position
The runtime SHALL represent each active execution path as a fiber with one current instruction position, one lifecycle state, one owning scope, and fiber-local execution data. The interpreter SHALL advance only the selected fiber during a fiber quantum.

#### Scenario: Selected fiber completes a step
- **WHEN** the scheduler selects a runnable fiber and its current step completes
- **THEN** only that fiber advances to its next instruction before the scheduler chooses another runnable fiber

#### Scenario: Selected fiber yields
- **WHEN** the selected fiber returns `Yield`
- **THEN** its progress is committed at the same instruction, its quantum ends, and it is placed after already-runnable sibling fibers

### Requirement: Compiled plans contain explicit structural continuations
The compiler SHALL emit stable structural continuation instructions for authored control flow. Conditional paths SHALL converge at `IfJoin`, loops SHALL use explicit loop-back and loop-exit targets, and branch scopes SHALL use `ScopeJoin` and `ScopeExit` positions distinct from semantic `BranchReturn` and workflow `End`.

#### Scenario: Conditional block is compiled
- **WHEN** an authored `If` contains then and else bodies followed by another workflow node
- **THEN** both paths target one stable `IfJoin` instruction before the following node

#### Scenario: Branch scope is compiled
- **WHEN** an authored `WhenAll` or `WhenFirst` is compiled
- **THEN** the plan contains explicit scope join and scope exit positions used for join evaluation, cleanup, merge, and parent continuation

#### Scenario: Scope reaches a join position
- **WHEN** a final child result or cleanup transition makes a scope joinable or exitable
- **THEN** `ScopeReducer` evaluates the corresponding structural position inside the commit transition without scheduling a fiber quantum for `ScopeJoin` or `ScopeExit`

### Requirement: Structured scopes preserve the parent fiber
Every local branch construct SHALL compile to a recursive single-entry/single-exit execution scope. Starting a scope SHALL suspend and preserve the parent fiber, create child fibers with deterministic identities, and resume the same parent fiber only after the scope reaches its join outcome.

#### Scenario: Parent reaches a parallel scope
- **WHEN** a parent fiber executes a scope-start instruction
- **THEN** the parent is marked blocked on that scope and child fibers begin at their authored branch entries

#### Scenario: Nested branch starts another scope
- **WHEN** a child fiber reaches a nested branch construct
- **THEN** the runtime applies the same scope lifecycle recursively without inferring ownership from instruction-path prefixes

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
A `WhenAll` scope SHALL declare exactly one merge operation. Merge SHALL receive successful branch results in stable authored branch order, SHALL execute without asynchronous work or external side effects, and SHALL update the preserved parent state as one logical committed transition.

#### Scenario: Branches finish in different orders
- **WHEN** equivalent successful branch results are committed in different completion orders
- **THEN** merge receives the same canonical ordered inputs and produces the same parent state

#### Scenario: Host crashes around merge
- **WHEN** a host crashes before or after the merge commit
- **THEN** replay either executes the uncommitted merge from persisted branch results or observes the committed merged state without rerunning completed branches

#### Scenario: Merge fails
- **WHEN** merge throws or produces an unserializable parent state
- **THEN** the scope fails with an explicit diagnostic and completed branch bodies are not rerun implicitly

### Requirement: Join policies define one scope outcome
`WhenAll` SHALL merge only after every branch succeeds and SHALL fail the scope if a branch fails. `WhenFirst` SHALL select the first committed terminal branch, using authored branch order to break same-commit ties, and SHALL merge only a successful winner result.

#### Scenario: A WhenAll branch fails
- **WHEN** any `WhenAll` branch commits failure before the scope joins
- **THEN** the scope fails, merge does not execute, and remaining branch work is cancelled through the scope ownership rules

#### Scenario: A WhenFirst winner succeeds
- **WHEN** a `WhenFirst` branch is selected as the winner and returns a successful result
- **THEN** only that result is passed to the winner merge and the parent resumes once

#### Scenario: A WhenFirst winner fails
- **WHEN** the selected `WhenFirst` branch terminates with failure
- **THEN** the scope fails without executing winner merge and remaining branch work is cancelled

### Requirement: Scope ownership covers every residual obligation
Every wait, timer, pending resume, child group, external job, resource ticket, retry record, and cancellation record created by a fiber SHALL persist its owning `FiberId` and `ScopeId`. A scope SHALL NOT complete or disappear until every non-detached owned obligation is completed or durably released.

#### Scenario: WhenFirst selects a winner
- **WHEN** a `WhenFirst` scope selects its terminal winner
- **THEN** cancellation or release facts for every losing descendant obligation are committed before or atomically with scope completion

#### Scenario: Owned work cannot be cancelled durably
- **WHEN** a branch shape can create residual work for which the runtime has no durable cancellation or detachment contract
- **THEN** definition compilation rejects that composition before registration
