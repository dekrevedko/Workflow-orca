# Quick Engine vs Durable Event-Driven Engine Feature Matrix

Reviewed on March 29, 2026.

## Purpose

This document defines a strict product boundary between two possible OrcaCore runtime families:

- Quick engine
- Durable event-driven engine

The purpose is to avoid semantic drift and product confusion if both approaches exist during research or transition.

This is intentionally strict.

If a capability is unclear, the default rule is:

- keep it out of the quick engine
- put it only in the durable event-driven engine

## Short Position

Quick engine is for:

- one host
- mostly ephemeral execution
- short-running workflows
- regular workflows only
- low operational complexity

Durable event-driven engine is for:

- durable workflows
- long-running workflows
- `WaitLong`
- timers
- durable inspection and history
- saga semantics
- future multi-host evolution

## Decision Rules

### Rule 1: Quick engine is not a degraded durable engine

It is a separate runtime with a smaller promise:

- simpler
- faster to adopt
- fewer guarantees

### Rule 2: Durable event-driven engine owns long-running coordination

If a feature implies:

- recovery after restart
- durable subscriptions
- compensation tracking
- timers beyond process lifetime
- strong operational visibility

it belongs to the durable event-driven engine.

### Rule 3: Do not promise instance portability between engines

Definition portability may be a goal.

Instance-state portability is not a default promise.

## Feature Matrix

### Authoring and definitions

| Capability | Quick engine | Durable event-driven engine | Notes |
|---|---|---|---|
| Code-first builder | Yes | Yes | Shared authoring model preferred |
| Regular workflow definitions | Yes | Yes | Must preserve semantic parity where shared |
| Saga workflow definitions | No | Yes | Keep saga out of quick engine |
| Definition versioning | Optional/lightweight | Required | Durable engine binds instances to versions |
| Serialized definitions later | Optional | Optional | Not a phase-1 requirement for either |

### Runtime modes

| Capability | Quick engine | Durable event-driven engine | Notes |
|---|---|---|---|
| Ephemeral execution | Yes | Optional for test/dev only | Durable engine may support hot execution but is not positioned as the lightweight runtime |
| Durable execution | No | Yes | Hard boundary |
| Restart recovery | No | Yes | Hard boundary |
| In-memory eviction with rehydration | No | Yes | Hard boundary |

### Core control flow

| Capability | Quick engine | Durable event-driven engine | Notes |
|---|---|---|---|
| `Init` / `End` | Yes | Yes | Shared |
| Business step | Yes | Yes | Shared |
| `If` | Yes | Yes | Shared |
| `While` | Yes | Yes | Shared |
| `Parallel` | Yes | Yes | Same product semantics preferred |
| `WhenAll` | Yes | Yes | Shared |
| `WhenFirst` | Maybe later | Maybe later | If added, define semantics once and share |

### Waits, events, and timers

| Capability | Quick engine | Durable event-driven engine | Notes |
|---|---|---|---|
| `Wait` | Yes | Yes | Shared concept, different backing model |
| `WaitLong` | No | Yes | Hard boundary |
| Instance-targeted events | Yes | Yes | Shared API shape preferred |
| Correlation-targeted routing | Yes | Yes | Shared semantics preferred |
| Definition fanout | Yes | Yes | Shared semantics preferred |
| Durable timers / reminders | No | Yes | Hard boundary |
| Timeout policies | No | Yes | Belongs with durable orchestration |
| Retry policies | No | Yes | Prefer durable engine only at first |

### State and durability

| Capability | Quick engine | Durable event-driven engine | Notes |
|---|---|---|---|
| Typed business state | Yes | Yes | Shared |
| Separate runtime state | Yes | Yes | Shared concept |
| Persisted runtime state | No | Yes | Hard boundary |
| Event stream | No | Yes | Durable engine internal model |
| Checkpoints | No | Yes | Durable engine internal model |
| Inbox/outbox | No | Yes | Durable engine only |
| History projection | No | Yes | Durable engine only |
| Continue-as-new later | No | Yes | Durable advanced feature |

### Query and management

| Capability | Quick engine | Durable event-driven engine | Notes |
|---|---|---|---|
| `All()` / `Where(...)` | Yes | Yes | Shared public shape preferred |
| `ListAsync()` / `CountAsync()` | Yes | Yes | Shared public shape preferred |
| `GetAsync()` | Yes | Yes | Shared |
| `GetStateAsync<T>()` | Yes | Yes | Shared |
| `GetActiveWaitsAsync()` | Yes | Yes | Shared |
| Query by persisted metadata | No | Yes | Durable engine only |
| Durable history inspection | No | Yes | Durable engine only |
| Delete instance | Optional | Yes | If exposed in quick engine, it is only in-memory removal |
| Purge artifacts | No | Yes | Durable engine only |
| Archive later | No | Yes | Durable engine only |

### Lifecycle and operations

| Capability | Quick engine | Durable event-driven engine | Notes |
|---|---|---|---|
| Running/waiting/completed/failed lifecycle | Yes | Yes | Shared semantics preferred |
| Durable retention policy | No | Yes | Hard boundary |
| Outbox replay | No | Yes | Hard boundary |
| Poison message handling | No | Yes | Hard boundary |
| Operational backlog/pressure visibility | Minimal | Yes | Durable engine only |
| Stuck detection later | Minimal | Yes | Prefer durable engine first |

### Concurrency and hosting

| Capability | Quick engine | Durable event-driven engine | Notes |
|---|---|---|---|
| One logical mutator per instance | Yes | Yes | Shared semantic guarantee |
| Single-host supported | Yes | Yes | Both |
| Multi-host roadmap | No | Yes | Durable engine only |
| Lease/partition ownership later | No | Yes | Durable advanced path |

### Saga-specific semantics

| Capability | Quick engine | Durable event-driven engine | Notes |
|---|---|---|---|
| Compensation scope | No | Yes | Hard boundary |
| Compensation ordering | No | Yes | Hard boundary |
| Compensation failure tracking | No | Yes | Hard boundary |
| Saga terminal states | No | Yes | Hard boundary |

## What Must Be Shared

Even if two engines exist, these must stay shared or intentionally aligned:

- public workflow authoring model for regular workflows
- event envelope shape
- correlation semantics
- duplicate-event semantics
- lifecycle state meanings
- `Parallel` / `WhenAll` product semantics
- management query vocabulary
- error vocabulary where practical

If these drift, users will experience the system as two different products, not one platform with two runtimes.

## What May Differ

These may differ without harming product coherence if documented clearly:

- persistence model
- provider model
- routing implementation
- durability guarantees
- operational metadata richness
- recovery model
- history visibility
- retention and archival support

## API Guidance

### Quick engine API

Should feel intentionally small:

- regular workflow builder
- in-memory runtime
- management scopes
- no durable-only affordances
- no saga affordances

### Durable event-driven engine API

May be broader:

- regular + saga definition support
- durable wait/timer APIs
- richer management and operational APIs
- durable retention/history features

### Compile-time separation

Prefer compile-time separation where practical:

- quick builder should not expose `WaitLong`
- quick engine should not expose saga-only APIs
- durable engine should expose durable-only operations explicitly

## Recommendation

If both engines exist, the safest product split is:

- Quick engine:
  - regular workflows only
  - ephemeral only
  - no `WaitLong`
  - no timers beyond process lifetime
  - no sagas
  - minimal operations

- Durable event-driven engine:
  - regular workflows
  - saga workflows
  - durable waits
  - `WaitLong`
  - durable timers
  - history/retention/outbox/inbox
  - future multi-host

This is the narrowest split that is still easy to explain.

## Anti-Goals

Do not allow this matrix to drift into:

- quick engine with partial durable emulation
- quick engine with experimental saga support
- both engines exposing nearly the same features
- durable engine being forced to mimic all quick-engine shortcuts

That would create two overlapping products and make long-term maintenance significantly worse.
