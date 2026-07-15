## ADDED Requirements

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
