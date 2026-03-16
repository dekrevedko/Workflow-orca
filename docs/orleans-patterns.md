# Orleans Patterns for OrcaCore

Reviewed on March 14, 2026.

## Scope

This document extracts ideas from Orleans that are useful for OrcaCore.

It is not proposing that OrcaCore should become an Orleans-style virtual actor runtime. The goal is narrower: identify Orleans patterns that improve workflow durability, lifecycle handling, concurrency boundaries, persistence, and event/wake-up behavior.

Primary sources:

- Orleans overview: https://learn.microsoft.com/en-us/dotnet/orleans/overview
- Orleans benefits: https://learn.microsoft.com/en-us/dotnet/orleans/benefits
- Grain persistence: https://learn.microsoft.com/en-us/dotnet/orleans/grains/grain-persistence/
- Serialization in Orleans: https://learn.microsoft.com/en-us/dotnet/orleans/host/configuration-guide/serialization
- Request scheduling: https://learn.microsoft.com/en-us/dotnet/orleans/grains/request-scheduling
- Timers and reminders: https://learn.microsoft.com/en-us/dotnet/orleans/grains/timers-and-reminders
- Grain lifecycle overview: https://learn.microsoft.com/en-us/dotnet/orleans/grains/grain-lifecycle
- Activation collection: https://learn.microsoft.com/en-us/dotnet/orleans/host/configuration-guide/activation-collection
- Grain directory: https://learn.microsoft.com/en-us/dotnet/orleans/host/grain-directory
- Orleans streaming APIs: https://learn.microsoft.com/en-us/dotnet/orleans/streaming/streams-programming-apis
- Orleans transactions: https://learn.microsoft.com/en-us/dotnet/orleans/grains/transactions

## What Orleans is good at for our problem

### 1. Stable logical identity with transient in-memory activation

Orleans separates logical grain identity from the current in-memory activation. A grain can activate and deactivate many times, but callers keep using the same logical identity.

Why this matters for OrcaCore:

A workflow instance should also have a stable logical identity independent of whether it is currently loaded in memory. That directly supports:

- long-running workflows
- host restarts
- lazy rehydration of waiting instances
- moving execution across nodes in the future

What to adopt:

- treat workflow instance ID as a stable logical identity
- treat in-memory runtime objects as disposable activations, not the identity itself

### 2. Single-threaded mutation per activation is a strong default

Orleans grains use a single-threaded execution model by default. That reduces concurrency bugs because state is not modified concurrently by multiple requests unless reentrancy/interleaving is explicitly enabled.

Why this matters for OrcaCore:

This is one of the strongest ideas Orleans offers. A workflow instance should probably have exactly one active mutator at a time by default.

What to adopt:

- default to serialized mutation per workflow instance
- require explicit rules for any interleaving or concurrent branch advancement
- design around optimistic concurrency or per-instance execution leases to preserve this invariant across hosts

What not to copy blindly:

- Orleans reentrancy options are powerful, but if applied too early they could destabilize workflow semantics. OrcaCore should be conservative here.

### 3. Durable reminders are a useful model for long waits and wake-up

Orleans distinguishes transient timers from durable reminders.

- Timers are activation-local and stop when the activation disappears.
- Reminders persist and can reactivate a grain later, but missed ticks are not replayed individually if the cluster was down.

Why this matters for OrcaCore:

This maps well to your `Wait` versus `WaitLong` distinction.

Suggested interpretation:

- `Wait`: short-lived, activation-local, lower-overhead waiting where exact wake-up survival is not the main goal
- `WaitLong`: durable persisted wake-up/subscription capable of rehydrating a workflow instance later

Important caution from Orleans:

A reminder stores the reminder definition, not every missed occurrence. That is a strong signal that OrcaCore should define whether waits represent:

- exact missed signal replay
- next eligible wake-up only
- durable subscription without guaranteed replay of every missed interval

This is a design decision that must be explicit.

### 4. Persistence model supports multiple state objects and pluggable providers

Orleans supports multiple named persistent state objects per grain and lets storage providers control how state is stored. It also supports configurable grain-state serialization.

Why this matters for OrcaCore:

This aligns well with the workflow-instance model already emerging in your docs.

What to adopt:

- support a clear split between runtime metadata and business state
- allow one workflow instance to persist multiple logical records if needed, for example:
  - runtime metadata
  - business state
  - wait subscriptions
  - history/checkpoints
- keep serialization configurable behind provider boundaries

Important caution:

Orleans documentation explicitly says its persistence model is designed for simplicity and is not intended to cover all data access patterns. OrcaCore should not assume a single persistence abstraction will solve reporting, auditing, history, and runtime queries equally well.

### 5. Ordered lifecycle hooks are useful, but shutdown hooks are not guaranteed

Orleans has an observable grain lifecycle and supports activation/deactivation hooks. But the docs explicitly warn not to rely on deactivation hooks for critical persistence because they are not guaranteed in all failure situations.

Why this matters for OrcaCore:

This is directly applicable. OrcaCore should not rely on host shutdown or instance-deactivation hooks to perform critical persistence or event publication.

What to adopt:

- persist state at business-safe transitions, not at shutdown cleanup
- treat activation/deactivation hooks as optimization or best-effort cleanup points only
- define explicit runtime checkpoints before suspension and after important transitions

### 6. Streaming pub/sub shows how durable subscription metadata can be runtime-owned

Orleans streaming uses a runtime pub/sub component to track subscriptions and can persist those subscriptions via storage.

Why this matters for OrcaCore:

This is a good conceptual model for wait subscriptions.

What to adopt:

- model active waits as first-class runtime subscription records
- make subscription persistence part of the engine, not just the broker adapter
- allow the event provider to deliver events while the engine remains the source of truth for which waits are active

### 7. Transactions are available, but should not become the core design dependency

Orleans supports transactions, but they are opt-in and platform-specific.

Why this matters for OrcaCore:

This suggests two things:

- transactional support can exist as an optional capability
- the core engine should not depend on distributed ACID transactions to be correct

What to adopt:

- design core guarantees around provider-agnostic consistency mechanisms first
- treat stronger transactional guarantees as optional provider capabilities

### 8. Activation collection is a warning about cache-like execution state

Orleans deactivates idle activations automatically. Memory presence is not identity or durability.

Why this matters for OrcaCore:

A loaded workflow instance should be treated as a cache of durable state, not the durable state itself.

What to adopt:

- assume instances may be evicted from memory any time after safe checkpoints
- make rehydration cheap and deterministic
- do not tie correctness to an always-live in-memory executor

### 9. Grain directory semantics are a caution about duplicates under instability

The Orleans grain directory page notes that the default distributed directory can allow occasional duplicate activations during cluster instability, while stronger consistency options also exist.

Why this matters for OrcaCore:

This is a useful warning for future distributed execution. If OrcaCore ever runs across multiple nodes, identity routing and exclusive execution are hard problems.

What to adopt:

- define whether multi-node execution is in scope for each phase
- if distributed hosting is added later, require a strong per-instance execution ownership model
- build acceptance tests for duplicate activation / duplicate executor defense

## Strongest Orleans-derived recommendations

These are the Orleans ideas most worth carrying into OrcaCore.

### A. Workflow instance identity must be stable and independent from memory

A workflow instance should be rehydratable at any time from durable state.

### B. One workflow instance should have one logical mutator by default

This should be the default concurrency rule unless a future feature intentionally loosens it.

### C. `Wait` and `WaitLong` should be distinguished by durability and wake-up semantics

Orleans timers versus reminders strongly supports this split.

### D. Wait subscriptions should be runtime-owned persisted records

This is analogous to Orleans pub/sub subscription tracking.

### E. Persistence should separate runtime metadata from business state

Orleans supports multiple state objects. OrcaCore should do the same conceptually even if the storage model differs.

### F. Critical state changes must not depend on deactivation hooks

This is non-negotiable for a durable workflow engine.

## What not to import from Orleans as-is

### 1. Full virtual actor model

OrcaCore is a workflow engine, not a general-purpose actor platform.

### 2. Broad reentrancy/interleaving controls as an early feature

Those are powerful but risky before workflow semantics are fully specified.

### 3. Assuming reminders solve event durability

Reminders solve durable wake-up, not full event history or exact replay semantics.

### 4. Treating storage-provider convenience as sufficient observability

OrcaCore still needs queryable runtime metadata, explicit history, and acceptance-tested provider behavior.

## Concrete design implications for OrcaCore

The research suggests these design decisions should move up in priority:

1. Define workflow instance identity and rehydration rules.
2. Define per-instance serialized execution as the default concurrency model.
3. Define exact semantics of `Wait` versus `WaitLong`.
4. Define persisted wait/subscription records as engine-owned runtime data.
5. Define what lifecycle hooks are allowed to do and what they must never be relied on for.
6. Define provider capabilities around serialization and persistence shape.
7. Add acceptance tests for rehydration, duplicate executor prevention, and durable wake-up.

## Acceptance tests implied by Orleans research

These should be added to the future test backlog.

### OR-001: Instance rehydration after deactivation

Given a suspended workflow instance persisted to storage
When its in-memory executor is discarded and the instance is later resumed
Then the workflow resumes correctly from durable state without relying on prior memory state

### OR-002: One logical mutator per instance

Given concurrent attempts to advance the same workflow instance
When both attempts race
Then only one mutates the instance at a time according to the documented concurrency policy

### OR-003: Durable wake-up versus transient wait

Given one `Wait` and one `WaitLong`
When the host restarts before wake-up
Then the documented difference in survivability between the two is observed exactly

### OR-004: No critical persistence in shutdown hook

Given a workflow transition requiring durability
When the host fails before deactivation hooks can run
Then correctness is preserved because the transition was already persisted at a safe boundary

### OR-005: Active wait subscriptions are engine-owned

Given active waits registered against external events
When the event provider is restarted or the workflow host is rehydrated
Then the runtime still knows which waits are active and can correlate incoming events correctly

## Final conclusion

Orleans is not a workflow blueprint, but it is a very good blueprint for several runtime qualities OrcaCore needs:

- identity separated from memory
- single-writer semantics by default
- durable wake-up distinctions
- multiple persisted state objects
- provider-backed serialization
- lifecycle discipline

The most valuable Orleans contribution to OrcaCore is not actors. It is the operational discipline around identity, activation, persistence, and wake-up.
