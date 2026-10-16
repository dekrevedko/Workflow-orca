## Purpose

Define the behavior of the primary state-driven OrcaCore runtime, including ephemeral execution
where ordinary `Wait` residency is runtime and hosting policy rather than a separate authored node.
## Requirements

### Requirement: One logical mutator advances each instance at a time
The state-driven runtime SHALL serialize execution per workflow instance so only one logical mutator can advance an instance state transition at a time.

#### Scenario: Concurrent work targets the same instance
- **WHEN** two operations attempt to advance the same workflow instance concurrently
- **THEN** the runtime commits their effects in a valid serialized order rather than allowing overlapping mutation

### Requirement: Interpreter executes control flow deterministically
The runtime SHALL interpret each selected fiber through one linear instruction position. It SHALL
represent supported nested `If` through explicit conditional continuations and supported root
`Parallel`/`ForEach` through explicit single-entry/single-exit scopes. Equivalent fixed child
inputs and results SHALL produce the same post-join state regardless of child completion
interleaving because merge order is derived from stable authored branch or item order rather than
scheduler timing. This requirement does not authorize fan-out inside a child body.

#### Scenario: Equivalent branch results complete in different orders
- **WHEN** the same logical branch outcomes arrive with different interleavings
- **THEN** the runtime supplies them to merge in canonical authored order and produces the same post-join state and continuation

#### Scenario: Supported nested conditional is interpreted
- **WHEN** a root, branch, item, loop, conditional, or leased body reaches a nested `If`
- **THEN** the interpreter follows its explicit conditional continuation and rejoins the same linear fiber without creating a nested fan-out scope

#### Scenario: Child body reaches another linear instruction
- **WHEN** a root-fan-out child completes a supported nested conditional, wait, delay, resource scope, or business step
- **THEN** the same child fiber advances to its next linear instruction under the shared interpreter rather than invoking a shape-specific child runtime

### Requirement: Lifecycle transitions are explicit and terminal states are final
The runtime SHALL model workflow lifecycle transitions explicitly and SHALL reject further advancement once an instance reaches a terminal outcome such as completed, failed, or terminated.

#### Scenario: Event arrives for a completed instance
- **WHEN** a completed workflow later receives a resume or event-delivery attempt
- **THEN** the runtime rejects or ignores the request according to explicit terminal-state policy instead of resuming work

### Requirement: Ephemeral mode has explicit limitations
When the runtime is used without durable persistence, it SHALL support short-lived orchestration and in-memory waits while providing no restart-safe rehydration. Wait residency SHALL remain a runtime and hosting policy rather than an authored node distinction, so ephemeral `Wait` SHALL NOT survive process loss and SHALL NOT be distinguished by a separate authored member. The ephemeral builder SHALL NOT expose durable-only capabilities such as root `ContinueAsNew` or scoped `AcquireResources`.

#### Scenario: Host restarts in ephemeral mode
- **WHEN** an in-memory workflow instance is waiting and the process restarts
- **THEN** the prior instance state is not recoverable and no cold wait residency is claimed

#### Scenario: Ephemeral author looks for durable-only capabilities
- **WHEN** an ephemeral author looks for root `ContinueAsNew` or scoped `AcquireResources`
- **THEN** the members are absent from the statically selected ephemeral builder

### Requirement: Optional resource governance composes with instance serialization
When host-level concurrency or named resource pools are enabled (see `runtime-resource-governance`), those limits SHALL apply to operational capacity and shared external resources without replacing the requirement that at most one logical mutator advances a given instance at a time.

#### Scenario: Pool limit and instance lock coexist
- **WHEN** two instances both hold slots from the same named pool
- **THEN** each instance still advances under serialized-per-instance semantics so state transitions for one instance do not interleave with another mutator for that same instance

### Requirement: Failure handling is part of the runtime contract
The runtime SHALL capture workflow failures as explicit state transitions with inspectable error details instead of leaving step exceptions as unstructured host failures.

#### Scenario: Step throws or returns failure
- **WHEN** a workflow step fails during execution
- **THEN** the instance transitions into a failed state with runtime-visible failure information

### Requirement: Instance status derives from aggregate fiber runnability
The runtime SHALL derive instance residency status from all nonterminal fibers. An instance SHALL remain `Running` while at least one fiber is runnable and SHALL be `Waiting` only when every nonterminal fiber is blocked on a committed obligation.

#### Scenario: One branch waits while a sibling is runnable
- **WHEN** one child fiber registers a wait and another child fiber remains runnable
- **THEN** the waiting fiber is blocked but the workflow instance remains `Running`

#### Scenario: Every active fiber is blocked
- **WHEN** all nonterminal fibers are blocked on waits, timers, children, jobs, resources, or scope joins
- **THEN** the workflow instance transitions to `Waiting`

### Requirement: Runtime modes share execution semantics
Ephemeral and durable execution SHALL consume the same compiled-plan model and SHALL implement the same fiber, scope, join, merge, cancellation, and terminal semantics for capabilities supported by both modes. Persistence residency and durable-only compatibility handling SHALL be the only mode-specific differences for those shared behaviors. Durable mode MAY enter `Parked` for definition, plan-fingerprint, envelope-version, poison, or unsupported-capability diagnostics; ephemeral mode SHALL instead report a typed definition-start or execution failure and SHALL NOT enter `Parked`.

#### Scenario: Equivalent workflow runs in both modes
- **WHEN** an equivalent supported definition receives the same branch results and external events in ephemeral and durable modes
- **THEN** both modes produce the same observable business outcome, merge ordering, and residual cleanup

#### Scenario: Unsupported or incompatible execution is reported by mode
- **WHEN** a definition is unsupported by the selected mode or persisted durable execution is incompatible with its registered plan
- **THEN** ephemeral execution reports a typed failure while durable execution records the applicable `Parked` reason and diagnostics

### Requirement: Quantum rotation is runtime-owned
The runtime SHALL commit a selected fiber's current progress at its scheduler-owned quantum
boundary and SHALL allow another runnable sibling to advance before selecting the same fiber
again when the fairness bound requires rotation. No authored `Yield`, `StepResult.Yield`, engine
result bridge, alias, or tombstone SHALL expose this scheduling decision.

#### Scenario: First branch exhausts repeated runtime quanta
- **WHEN** the first runnable branch repeatedly reaches the internal quantum bound while another branch remains runnable
- **THEN** the scheduler advances the sibling within the configured fairness bound without an author-returned yield result
