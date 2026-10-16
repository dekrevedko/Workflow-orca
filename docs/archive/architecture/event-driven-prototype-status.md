# Event-Driven Prototype Status

Last updated April 10, 2026 (keep in sync with `src/OrcaCore.EventDrivenPrototype` and `tests/OrcaCore.EventDrivenPrototype.Tests`).

## Purpose

This document tracks the first implemented slice of the separate event-driven prototype.

Prototype code lives in:

- [src/OrcaCore.EventDrivenPrototype](../../src/OrcaCore.EventDrivenPrototype)
- [tests/OrcaCore.EventDrivenPrototype.Tests](../../tests/OrcaCore.EventDrivenPrototype.Tests)

For repository-wide layout and types, see [Code map](../project-technical-overview.md#code-map) and the [documentation map](../README.md).

## Implemented Slice

The current prototype is a narrow but end-to-end regular workflow slice.

Implemented:

- separate project and test project
- separate event-driven engine
- versioned workflow definition registration
- append-only per-instance event stream
- materialized checkpoint state
- summary projection
- active-wait projection
- inbox state for restart-safe dedup
- start workflow
- execute straight-line steps
- `Wait`
- instance-targeted event delivery
- correlation-targeted event delivery
- pending-event buffering
- buffered-event later consumption
- duplicate-event suppression through inbox/consumed ids
- restart-safe resume by reloading from persisted checkpoint

## Verified Tests

The prototype test suite currently covers:

- straight-line completion with committed workflow events
- matching event resumes after engine restart
- buffered event is consumed when matching wait appears
- duplicate event ignored after restart using inbox state
- correlation-targeted routing through active-wait projection

## Current Limitations

Not implemented yet:

- `If`
- `While`
- `Parallel`
- `WhenAll`
- `WaitLong`
- timers
- outbox
- saga semantics
- provider abstraction beyond the in-memory prototype store
- true checkpoint-plus-tail replay scenarios with non-empty stream tail after checkpoint
- management query surface beyond direct summary/state/waits lookup

## Honest Assessment

What this slice proves:

- the event-driven design is practical enough to implement as a separate runtime
- workflow facts, projections, and checkpoints can coexist cleanly in a small model
- restart-safe resume, buffering, dedup, and correlation routing fit the design naturally

What it does not prove yet:

- that the model remains clean under complex control flow
- that the durable event-driven path is better for `WaitLong`, timers, or saga
- that the provider contract is good enough for a real durable backend

## Next Recommended Slice

The next highest-value additions are:

1. `If`
2. `While`
3. `Parallel` + `WhenAll`
4. explicit provider contract extraction from the in-memory store
5. `WaitLong`
6. durable timer command path
