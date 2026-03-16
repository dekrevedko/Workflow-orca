# Regular Workflow Advanced Requirements

This document defines regular workflow capabilities beyond the initial slice.

These requirements are not required for the first implementation.

## Purpose

Expand regular workflow semantics without introducing saga compensation semantics.

Why:

- many advanced workflow features are useful but do not require saga semantics
- keeping them separate avoids turning regular workflow into saga-by-default

## Advanced regular capabilities

The advanced regular workflow track should define and later support:

- `WhenFirst`
- time-based delay or timer primitive
- explicit cancellation semantics
- child workflows or sub-workflows
- scoped retry policy
- step timeout policy
- operator-driven pause/resume/cancel semantics where supported
- richer management queries and statistics
- lifecycle event publication
- stuck-step and stuck-instance detection

## Requirements

### RR-A-001: First-completion semantics

`WhenFirst` must define:

- which branch wins
- what happens to losing branches
- whether losing branches are cancelled, ignored, or allowed to finish

### RR-A-002: Time-based waiting

Regular workflows should support a time-based primitive distinct from event-based `Wait`.

Why:

- time is a first-class orchestration dependency
- mixing timer behavior into `Wait` would blur semantics

### RR-A-003: Child workflow support

Regular workflows should support calling child workflows with explicit completion, failure, and cancellation semantics.

### RR-A-004: Timeout policy

Steps and selected scopes should support timeout policies with configurable outcomes such as:

- retry
- fail workflow
- cancel workflow
- wait for operator action

### RR-A-005: Retry policy

The engine should support declarative retry behavior for transient step failures.

### RR-A-006: Lifecycle publication

The runtime should support publishing lifecycle events for step and instance transitions.

### RR-A-007: Operational queries

The runtime should support richer queries and grouped statistics such as:

- active count by definition
- waiting count by definition
- failed count by definition
- active waits by event name
- stuck and timed-out work lists

## Traceability

These capabilities are future work beyond the current implementation plan.
