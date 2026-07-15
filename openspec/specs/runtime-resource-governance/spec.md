## Purpose

Define optional limits on concurrent orchestration work and shared external resources so hosts can enforce operational budgets (for example maximum concurrent database operations) while preserving OrcaCore’s correctness-oriented execution model.
## Requirements
### Requirement: Hosts may cap concurrent workflow advancement

The runtime SHALL allow configuration of an upper bound on how many workflow instances may actively advance execution at the same time, when this capability is enabled.

#### Scenario: Instance slots are saturated

- **WHEN** the number of instances currently advancing reaches the configured maximum
- **AND** another instance is ready to advance
- **THEN** the runtime defers that advancement according to the configured policy until a slot is available, host execution is cancelled, or an overload failure policy triggers

#### Scenario: Shutdown releases waiters

- **WHEN** the host begins orderly shutdown
- **THEN** advancement waiters SHALL observe cancellation and SHALL NOT retain slots indefinitely

### Requirement: Hosts may cap concurrent step body execution

The runtime SHALL allow configuration of an upper bound on concurrent execution of workflow step bodies (or an explicitly documented subset of steps), independent of the instance advancement limit.

#### Scenario: Global step concurrency protects shared services

- **WHEN** many instances reach executable steps simultaneously
- **THEN** the runtime SHALL ensure at most the configured number of step bodies run concurrently, excluding waits that do not execute step bodies unless explicitly included by configuration

### Requirement: Named resource pools enforce shared limits

The runtime SHALL support named pools, each with its own concurrency budget, so unrelated workflows can share a limit on a common external resource.

#### Scenario: Shared database pool

- **WHEN** steps from multiple workflow instances are configured to use pool key `db-updates` with limit 4
- **THEN** at most four such steps MAY hold that pool concurrently across all instances on that host

#### Scenario: Pools compose with instance serialization

- **WHEN** a step acquires a named pool slot
- **THEN** per-instance serialized mutation rules remain in effect such that instance state is not concurrently mutated by multiple logical mutators

### Requirement: Governance scope is explicit for infrastructure paths

The runtime SHALL document which execution paths participate in each limit category (for example workflow step bodies versus durable outbox dispatch workers) so operators can predict whether internal bookkeeping competes with business steps for the same pool.

#### Scenario: Dispatch path classification

- **WHEN** operators configure resource pools
- **THEN** documentation or runtime metadata SHALL indicate whether outbox dispatch, inbox replay, or similar loops share pools with business steps by default

### Requirement: Saturation behavior is configurable

The runtime SHALL support asynchronous waiting for cross-instance host admission and SHOULD support an optional fail-fast overload mode when no slot is immediately available. An in-instance fiber that waits for named-resource capacity SHALL record an owned blocked obligation, end its quantum, and release the instance mutation turn; ephemeral mode records that obligation in memory and durable mode commits it. The runtime SHALL NOT await resource capacity while retaining an executing fiber quantum or instance mutation turn.

#### Scenario: Cancellation while waiting for a slot

- **WHEN** a step or advancement waits for a pool or global slot
- **AND** the governing cancellation token is signaled
- **THEN** the wait ends without acquiring the slot and propagates cancellation appropriately

#### Scenario: Local fiber waits for saturated capacity

- **WHEN** a selected local fiber cannot acquire a required named resource
- **THEN** it records a mode-appropriate owned blocked obligation, releases the instance turn, and allows another runnable sibling fiber to advance

### Requirement: Observability hooks exist for operators

When resource governance is enabled, the runtime SHALL expose introspection for configured limits and current contention (for example depth of waiters per pool), via metrics, logs, or debug APIs suitable for production.

#### Scenario: Operator inspects pool pressure

- **WHEN** pools are saturated during a load test
- **THEN** operators can observe that saturation through the documented introspection mechanism

### Requirement: Resource ownership follows fibers and scopes
Every acquired or pending named-resource ticket SHALL record the owning workflow instance, fiber, and scope in execution state. Durable mode SHALL persist that ownership. Completion, cancellation, failure, merge, and workflow termination SHALL release tickets through the mode's committed or in-memory transition before removing the owner.

#### Scenario: Losing branch owns a resource ticket
- **WHEN** a `WhenFirst` scope selects another branch as winner
- **THEN** the losing ticket is released or its pending acquisition is cancelled before the losing fiber is removed

### Requirement: Local fiber scheduling is distinct from host concurrency
Resource-governance limits SHALL apply across workflow instances and to explicitly classified external work. Cooperative local fibers SHALL NOT be counted as concurrently executing local step bodies because only one local fiber step body executes per instance at a time.

#### Scenario: One instance has many runnable local fibers
- **WHEN** a workflow instance has multiple runnable local branches
- **THEN** the fiber scheduler advances them cooperatively while host-level limits continue to govern concurrent work across instances

#### Scenario: Branch starts external work
- **WHEN** a local fiber dispatches an external job or child workflow
- **THEN** that work can execute concurrently under its configured resource-governance policy without allowing concurrent mutation of the parent instance

### Requirement: Resource waits do not retain an executing fiber quantum
A fiber that cannot acquire a required resource SHALL record a blocked resource obligation, end its quantum, and release the instance mutation turn. Ephemeral mode SHALL record the obligation in memory; durable mode SHALL commit it. The runtime SHALL NOT await resource capacity while retaining the instance turn. The fiber SHALL become runnable only after the mode's grant or cancellation transition.

#### Scenario: Named pool is saturated
- **WHEN** the selected fiber cannot acquire its required named resource
- **THEN** it blocks without repeatedly occupying the local scheduler and another runnable fiber can advance
