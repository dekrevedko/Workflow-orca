## ADDED Requirements

### Requirement: Quantum rotation is runtime-owned
The runtime SHALL commit a selected fiber's current progress at its scheduler-owned quantum
boundary and SHALL allow another runnable sibling to advance before selecting the same fiber
again when the fairness bound requires rotation. No authored `Yield`, `StepResult.Yield`, engine
result bridge, alias, or tombstone SHALL expose this scheduling decision.

#### Scenario: First branch exhausts repeated runtime quanta
- **WHEN** the first runnable branch repeatedly reaches the internal quantum bound while another branch remains runnable
- **THEN** the scheduler advances the sibling within the configured fairness bound without an author-returned yield result

## REMOVED Requirements

### Requirement: Yield is a fiber scheduling operation
**Reason**: Authored `Yield` and `StepResult.Yield` are removed from v1 rather than deferred. Fairness remains a runtime-owned quantum rotation rule with no author-returned result, alias, reflection bridge, or tombstone. Replaced by "Quantum rotation is runtime-owned".

**Migration**: No released consumer migration exists. Remove authored yield nodes and let the runtime rotate runnable fibers at its internal quantum boundary.

## MODIFIED Requirements

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
