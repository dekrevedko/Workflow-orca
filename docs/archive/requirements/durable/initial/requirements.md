# Durable Initial Requirements

This document defines the first durable execution slice for OrcaCore.

Durable execution is a separate execution-mode track from workflow semantics.

Why:

- durable mode changes guarantees, not only storage
- it introduces restart recovery, retention, versioning, and consistency boundaries

## Scope

This initial durable slice should build on the regular workflow baseline and add:

- persistence-backed workflow state
- durable suspension and resume
- rehydration after restart
- durable wait ownership
- durable inspection of runtime metadata
- version binding for durable instances

## Requirements

### DR-I-001: Persistence provider abstraction

The engine must support a persistence boundary that can durably store:

- business state
- runtime state
- active waits
- pending events as applicable
- consumed event IDs or equivalent dedup state
- instance version/concurrency token

### DR-I-002: Rehydration

A durable instance must be re-creatable from durable state without relying on prior in-memory activation.

### DR-I-003: Durable wait ownership

When a workflow enters a durable wait, enough information must be stored to resume it later after restart.

### DR-I-004: Version-bound durable instances

Each durable instance must be bound to a compatible definition version.

### DR-I-005: Durable inspection

Runtime metadata must remain queryable without requiring deserialization of opaque business state only.

### DR-I-006: Serialized execution remains guaranteed

Durable mode must preserve the same per-instance serialized committed outcome contract as ephemeral mode.

This may require optimistic concurrency, lease semantics, or both.

### DR-I-007: Durable-only API separation

Durable-only features should be absent from ephemeral-facing APIs where practical and clearly available on durable-facing surfaces.

### DR-I-008: Durable wait primitives

Durable mode must introduce durable waiting primitives, including `WaitLong` and durable timer or wake-up semantics.

## Non-functional requirements

### DR-I-009: Crash safety

If a crash occurs mid-transition, recovery must restore the last committed durable state only.

### DR-I-010: Restart-safe deduplication

Duplicate events after restart must not produce duplicate committed outcomes.

### DR-I-011: Retention baseline

Completed and failed durable instances must remain inspectable according to documented retention policy.
