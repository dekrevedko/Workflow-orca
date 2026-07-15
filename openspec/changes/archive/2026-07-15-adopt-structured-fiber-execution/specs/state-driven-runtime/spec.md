## ADDED Requirements

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

## MODIFIED Requirements

### Requirement: Interpreter executes control flow deterministically
The runtime SHALL interpret each selected fiber through one linear instruction position and SHALL represent branching through explicit recursive scopes. Equivalent branch inputs and results SHALL produce the same post-join state regardless of branch completion interleaving because merge order is derived from stable authored branch order rather than scheduler timing.

#### Scenario: Equivalent branch results complete in different orders
- **WHEN** the same logical branch outcomes arrive with different interleavings
- **THEN** the runtime supplies them to merge in canonical authored order and produces the same post-join state and continuation

#### Scenario: Nested composition is interpreted
- **WHEN** a child fiber reaches another branch construct
- **THEN** the interpreter starts a nested scope using the same scope lifecycle instead of introducing a shape-specific join algorithm
