## Purpose

Define the intended scope and behavioral contract of the separate OrcaCore event-driven prototype runtime.

## Requirements

### Requirement: Prototype remains a separate runtime slice
The event-driven prototype SHALL exist as a distinct runtime and test slice with its own engine, definitions, persistence model, and project boundary.

#### Scenario: Contributor explores event-driven execution
- **WHEN** a contributor works on the append-only runtime path
- **THEN** they do so through the dedicated prototype project and test suite rather than through hidden modes inside the primary state-driven runtime

### Requirement: Prototype uses append-only facts with derived state
The prototype SHALL record workflow facts in an append-only per-instance stream and SHALL derive resumable state through checkpoints, projections, and inbox tracking.

#### Scenario: Instance resumes after restart
- **WHEN** a prototype workflow is restarted after durable facts were appended
- **THEN** the engine restores behavior from checkpointed and projected state derived from the append-only stream

### Requirement: Current implemented slice is intentionally narrow
The prototype SHALL currently cover straight-line execution, `Wait`, buffering, deduplication, and instance-targeted or correlation-targeted resume without claiming full parity with the state-driven runtime.

#### Scenario: Consumer evaluates prototype capability
- **WHEN** a consumer asks whether the event-driven prototype is feature-complete
- **THEN** the system documents it as a narrow implemented slice rather than a full replacement for the primary runtime

### Requirement: Unsupported control-flow and infrastructure features stay explicit
The prototype SHALL not imply support for `If`, `While`, `Parallel`, `WaitLong`, timers, outbox, or non-trivial provider abstraction until those behaviors are explicitly designed and implemented.

#### Scenario: Contributor plans a new prototype feature
- **WHEN** a feature outside the current implemented slice is proposed
- **THEN** the work is treated as an explicit new capability increment instead of assumed baseline behavior
