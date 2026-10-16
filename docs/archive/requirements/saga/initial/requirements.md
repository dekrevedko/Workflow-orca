# Saga Initial Requirements

This document defines the first supported saga scope.

Saga is a separate semantic kind from regular workflow.

Why:

- saga changes the meaning of failure, cancellation, and completion
- compensation semantics should not leak into regular workflow by default

## Scope

Semantic kind:

- saga workflow

Execution mode:

- initial support may begin in ephemeral mode for semantics exploration
- production-value saga behavior is expected to depend on durable mode later

## Initial saga capabilities

The first supported saga slice should include:

- compensatable forward steps
- compensation handlers
- compensation scope
- compensation order rules
- saga-specific terminal states
- explicit saga failure policy

## Requirements

### SG-I-001: Separate saga definition kind

Saga definitions must be a separate semantic definition kind from regular workflows.

### SG-I-002: Forward and compensating actions

A saga must support forward actions and compensating actions as related but distinct concepts.

### SG-I-003: Compensation scope

A saga must support a compensation scope that tracks successfully completed forward actions eligible for rollback.

### SG-I-004: Reverse compensation order

Default compensation order should be reverse successful-completion order unless explicitly overridden.

### SG-I-005: Saga-specific terminal outcomes

Saga lifecycle must distinguish at least:

- completed successfully
- failed without compensation
- compensated
- compensation failed

### SG-I-006: Cancellation and timeout semantics

Saga requirements must explicitly define how cancellation and timeout affect forward actions and compensation.

### SG-I-007: Idempotent compensation expectation

Compensating actions must be designed and documented as idempotent at the contract level.

Why:

- retries and partial failures otherwise become unsafe

## Traceability

This is a future slice. No implementation plan exists yet.
