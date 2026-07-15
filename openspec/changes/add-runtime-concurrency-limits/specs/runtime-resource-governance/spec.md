## MODIFIED Requirements

### Requirement: Saturation behavior is configurable
The runtime SHALL support asynchronous waiting for cross-instance host admission and SHOULD support an optional fail-fast overload mode when no slot is immediately available. A per-step execution throttle SHALL be host-local and held only around one step body. A named cross-instance transient pool SHALL be host-local shared capacity and SHALL NOT claim restart durability. An in-instance fiber that waits for transient-pool capacity SHALL record an owned blocked obligation, end its quantum, and release the instance mutation turn; ephemeral mode records that obligation in memory and durable mode commits enough blocked state to re-evaluate admission after restart without persisting pool ownership. The runtime SHALL NOT await capacity while retaining an executing fiber quantum or instance mutation turn.

#### Scenario: Cancellation while waiting for a slot
- **WHEN** a step or advancement waits for a pool or global slot
- **AND** the governing cancellation token is signaled
- **THEN** the wait ends without acquiring the slot and propagates cancellation appropriately

#### Scenario: Local fiber waits for saturated capacity
- **WHEN** a selected local fiber cannot acquire a required named resource
- **THEN** it records a mode-appropriate owned blocked obligation, releases the instance turn, and allows another runnable sibling fiber to advance

#### Scenario: Durable host restarts while transient capacity is held
- **WHEN** a durable host restarts while an instance held or awaited a named transient-pool slot
- **THEN** host-local capacity is reset and the fiber re-evaluates admission without claiming that the previous slot survived restart

### Requirement: Transient governance is distinct from durable leasing
Per-step execution throttles and named cross-instance transient pools SHALL NOT be named, documented, or serialized as durable resource leases. Durable resource leases SHALL use a separate persisted cross-host/fiber/scope contract.

#### Scenario: Developer compares pool capabilities
- **WHEN** public authoring and operator documentation describe a transient pool and a durable lease
- **THEN** they distinguish host-local reset semantics from persisted queueing, deterministic scope release, expiry, and recovery
