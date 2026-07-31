## Purpose

Define the behavior of the primary state-driven OrcaCore runtime in ephemeral and non-event-stream execution modes.
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
When the runtime is used without durable persistence, it SHALL support short-lived orchestration and waits in memory while explicitly rejecting durable-only features such as `WaitLong` and restart-safe rehydration.

#### Scenario: Host restarts in ephemeral mode
- **WHEN** an in-memory workflow instance is waiting and the process restarts
- **THEN** the prior instance state is not recoverable and durable-only wait features remain unavailable

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

### Requirement: Yield is a fiber scheduling operation
`Yield` SHALL commit the selected fiber's current progress, end that fiber's quantum, and allow another runnable sibling to advance before the yielding fiber is selected again.

#### Scenario: First branch repeatedly yields
- **WHEN** the first authored branch yields over multiple attempts while another branch remains runnable
- **THEN** the scheduler advances the sibling within the configured fairness bound

### Requirement: Runtime modes share execution semantics
Ephemeral and durable execution SHALL consume the same compiled-plan model and SHALL implement the same fiber, scope, join, merge, cancellation, and terminal semantics for capabilities supported by both modes. Persistence residency and durable-only compatibility handling SHALL be the only mode-specific differences for those shared behaviors. Durable mode MAY enter `Parked` for definition, plan-fingerprint, envelope-version, poison, or unsupported-capability diagnostics; ephemeral mode SHALL instead report a typed definition-start or execution failure and SHALL NOT enter `Parked`.

#### Scenario: Equivalent workflow runs in both modes
- **WHEN** an equivalent supported definition receives the same branch results and external events in ephemeral and durable modes
- **THEN** both modes produce the same observable business outcome, merge ordering, and residual cleanup

#### Scenario: Unsupported or incompatible execution is reported by mode
- **WHEN** a definition is unsupported by the selected mode or persisted durable execution is incompatible with its registered plan
- **THEN** ephemeral execution reports a typed failure while durable execution records the applicable `Parked` reason and diagnostics
