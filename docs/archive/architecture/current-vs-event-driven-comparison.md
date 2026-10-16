# Current Architecture vs Event-Driven Architecture

Reviewed on March 29, 2026.

## Purpose

This document compares:

- the current OrcaCore architecture
- the proposed event-driven durable architecture

It also evaluates a third option:

- keep the current engine for quick workflows
- introduce a second event-driven engine for durable, long-running, or saga-heavy workflows

The goal is to answer honestly:

- which design is stronger for which problem
- what the migration cost really is
- whether running two engine approaches is advisable

## Short Conclusion

If OrcaCore is meant to stay mostly:

- single-host
- app-local
- short-running
- regular-workflow-focused

then the current architecture is a strong practical fit.

If OrcaCore is meant to become:

- durable-first
- saga-capable
- heavily event-driven
- multi-host later
- operationally inspectable

then the proposed event-driven architecture is the better long-term foundation.

Keeping both approaches is possible, but only under a strict product split.

Without a strict split, two engines will create:

- duplicated semantics
- confusing feature boundaries
- higher test burden
- much harder documentation and support

So the honest recommendation is:

- do not keep two equal-status engines long term
- only keep both if one is explicitly a lightweight fast-path runtime with a narrower promise

## 1. Current Architecture

### Summary

The current design is:

- code-first workflow builder
- interpreter/runtime-driven execution
- snapshot/checkpoint-centric durable persistence
- explicit runtime metadata persistence
- inbox/outbox/history side structures
- hot/cold instance lifecycle with `Wait` and `WaitLong`

The durable model is now quite mature for a single-host engine:

- persisted runtime state
- rehydration
- durable waits
- cold `WaitLong`
- inbox/outbox
- query surface
- retention hooks
- structured management APIs

### What it is good at

- straightforward authoring model
- easy mental model for regular workflows
- fast path for in-memory execution
- direct step-by-step interpreter behavior
- lower infrastructure burden than full event sourcing
- easier to get green in single-host TDD

### What it is weaker at

- durable truth is a materialized state model, not an append-only fact model
- history/audit is secondary, not primary
- saga semantics fit less naturally
- multi-node ownership becomes harder later
- versioned runtime facts and replay-style debugging are weaker
- correctness under failure requires more careful staging and edge-case handling

## 2. Proposed Event-Driven Architecture

### Summary

The proposed design is:

- command-driven on the write side
- append-only workflow events as durable truth
- checkpoint plus stream-tail recovery
- projection-driven query/routing
- inbox/outbox as first-class durable components
- same substrate for regular workflow, durable workflow, and saga

### What it is good at

- clean durable fact model
- stronger crash and audit semantics
- natural saga fit
- natural routing from projections
- easier path to multi-host correctness later
- easier history pressure visibility
- cleaner continue-as-new / compaction story

### What it is weaker at

- more infrastructure
- more complex provider model
- more internal moving parts
- more design effort around event schema and projection rebuilding
- stronger determinism discipline required
- slower path to a minimal “just run a workflow in memory” engine if built first

## 3. Side-by-Side Comparison

### Execution Model

Current:

- interpreter mutates in-memory runtime/business state
- durable mode persists materialized runtime and business state
- correctness comes from serialized execution, durable CAS, and carefully staged side effects

Event-driven:

- command loads aggregate
- aggregate emits workflow events
- events are appended as truth
- state is reconstructed from checkpoint plus stream

Assessment:

- current is simpler to execute
- event-driven is stronger as a durable write model

### Recovery

Current:

- rehydrate from persisted materialized state
- inbox/outbox/history help with operations and consistency

Event-driven:

- rebuild from checkpoint plus committed events
- event log is the authoritative recovery trail

Assessment:

- current is cheaper to restore immediately
- event-driven is stronger for debugging, audit, and long-lived correctness

### Querying

Current:

- query from persisted metadata/state snapshots

Event-driven:

- query from projections purpose-built for status, waits, history, compensation, pressure, and routing

Assessment:

- current already supports useful query
- event-driven makes query a first-class design rather than a persistence side effect

### Waits and Routing

Current:

- durable waits are persisted records
- routing uses hot index plus durable lookup support
- `WaitLong` is modeled through hot/cold residency policy

Event-driven:

- waits are subscription facts in the event stream
- routing is projection-first
- `WaitLong` is just `WaitMode = Cold` with eviction semantics

Assessment:

- current works
- event-driven is conceptually cleaner and more scalable

### Saga Fit

Current:

- saga can be added, but it is an additional semantic layer over an interpreter/snapshot runtime

Event-driven:

- saga is a natural extension of commands, events, and compensation facts

Assessment:

- event-driven is materially better here

### Multi-Host Readiness

Current:

- can evolve there, but the model is still primarily single-host practical today

Event-driven:

- better fit for stream-version concurrency, leases, partitions, and durable ownership transfer

Assessment:

- event-driven is stronger

## 4. Strengths and Weaknesses

### Current Architecture Strengths

- lower complexity
- easier onboarding for contributors
- fast local execution path
- very good fit for “embedded app workflow engine”
- already implemented
- already acceptance-tested in the current direction

### Current Architecture Weaknesses

- long-term durable model is more complex to harden incrementally
- correctness relies on many explicit safeguards rather than one dominant fact model
- saga and event-heavy orchestration are less natural
- history/inspection/pressure are less central to the design

### Event-Driven Architecture Strengths

- clean durable truth boundary
- better audit and explainability
- better saga model
- better outbox/inbox story
- stronger eventual multi-host trajectory
- better long-lived instance control

### Event-Driven Architecture Weaknesses

- higher conceptual and implementation cost
- harder first provider
- higher test-matrix cost
- projections introduce another consistency model to manage
- may feel too heavy for short-lived, local workflows

## 5. Migration Cost

The migration cost is not small.

This is not a refactor of a few components. It is a write-model redesign.

### Low-cost reusable assets

These can likely survive:

- builder/DSL authoring surface
- workflow definition graph or compiled plan
- many business steps
- acceptance criteria and much of the test intent
- `Wait` / `WaitLong` product semantics
- management surface shape
- some payload and event envelope abstractions

### Medium-cost assets

These would need reshaping:

- query APIs
- management scopes
- durable payload registry
- timer interfaces
- retention APIs
- durable management operations

### High-cost rewrite areas

- durable persistence contract
- state mapper
- durable engine core
- rehydration model
- routing core
- inbox/outbox implementation boundaries
- durable tests that assert current persistence shapes

### Honest migration assessment

If you choose the event-driven direction, expect:

- a new durable core
- a new provider contract
- a partial rewrite of durable tests
- a transition period with duplicated concepts

This is a strategic redesign, not cheap cleanup.

## 6. Recommended Phased Adoption Path

If the event-driven design is chosen, the safest path is not “replace everything at once.”

### Phase 1: Freeze product semantics

Freeze these as engine-agnostic contracts:

- workflow vs saga semantic split
- ephemeral vs durable mode split
- wait semantics
- event envelope semantics
- management/query shape
- version-binding rules
- one-logical-mutator guarantee

Goal:

- keep public product semantics stable while internals can change

### Phase 2: Define event-driven write model

Produce explicit specs for:

- commands
- workflow events
- checkpoints
- projections
- transaction boundaries
- timer/wakeup model

Goal:

- remove ambiguity before implementation starts

### Phase 3: Build an experimental durable provider

Do not replace the current durable engine immediately.

Instead:

- build one experimental event-driven durable provider/engine path
- validate:
  - recovery
  - query projections
  - inbox/outbox
  - `WaitLong`
  - compensation model

Goal:

- prove the architecture before migration commitment

### Phase 4: Run shared acceptance suites

Use the same acceptance criteria against:

- current engine
- experimental event-driven engine

Goal:

- compare semantics, not implementation opinions

### Phase 5: Choose long-term durable core

After evidence:

- either adopt event-driven durable core as the future path
- or keep current durable core and stop the experiment

Goal:

- make the decision from executable evidence

### Phase 6: Migrate public durable surface only if chosen

If event-driven wins:

- preserve as much public API shape as possible
- migrate durable internals behind the facade
- deprecate snapshot-first durable internals

## 7. Should OrcaCore Keep Two Engines?

This is the critical product question.

The proposed split is:

- current engine for quick workflows:
  - one host
  - mostly ephemeral
  - no sagas
  - no `WaitLong`
- event-driven engine for:
  - durable workflows
  - long-running workflows
  - saga workflows
  - timer-heavy orchestration

### Is it advisable?

Short answer:

- yes, as a transition strategy
- maybe, as a permanent product strategy
- no, if both engines are exposed as equally general-purpose runtimes

### When it is advisable

It is advisable if the split is explicit and narrow:

- “quick engine” is intentionally limited
- “durable engine” is the strategic advanced engine
- users are not promised feature parity
- docs clearly explain the boundary

In that case, the quick engine becomes:

- a lightweight embedded runtime
- a dev/test/local/simple-flow runtime
- potentially the default for apps that do not need durability

### When it is not advisable

It is not advisable if both engines are allowed to grow toward the same feature set.

Then you will end up with:

- two durability stories
- two failure models
- two routing cores
- two testing matrices
- two sets of docs
- feature drift
- endless “why is feature X here but not there?” questions

That becomes expensive very quickly.

## 8. Maintenance Cost of Two Approaches

This is the biggest risk.

### Costs you will definitely pay

- duplicated runtime semantics tests
- duplicated query/management behavior validation
- duplicated bug-fix analysis for waits, routing, dedup, and parallel semantics
- duplicated docs and examples
- duplicated support burden

### Costs you may also pay

- incompatible extension points
- different plugin/provider models
- different step behavior expectations
- difficult migration stories between engines
- user confusion about which engine to choose

### Hidden cost

The hidden cost is semantic drift.

Even if both engines expose the same public APIs, over time they may differ in:

- timing
- history visibility
- dedup behavior
- error types
- inspection shape
- ordering guarantees

That is usually worse than obvious API differences.

## 9. Possible Conflicts and Usability Issues

### Conflict 1: Same workflow definition, different execution truths

If one definition can run on both engines, users will expect the same behavior.

That is risky if:

- event buffering differs
- branch timing differs
- history visibility differs
- failure/retry semantics differ

Recommendation:

- only allow the same public workflow definition model if semantic parity is acceptance-tested

### Conflict 2: Feature discoverability

If users see one builder surface and one engine selection point, they may not understand why:

- `WaitLong` works only in one engine
- saga works only in one engine
- retention/query/history differ

Recommendation:

- separate engine families at the API level when semantics differ materially

### Conflict 3: Migration path

Users will ask:

- can I start on quick engine and move later?
- is the instance portable?
- are definitions portable?
- are management APIs portable?

Recommendation:

- promise definition portability first
- do not promise instance-state portability unless you explicitly build it

### Conflict 4: Extension model

Providers, decorators, and lifecycle hooks may need different contracts across engines.

Recommendation:

- keep shared extension contracts only where semantics truly match
- do not force fake commonality

## 10. Best Product Shapes If Two Engines Are Kept

If you keep two approaches, the cleanest options are:

### Option A: One product, two runtime modes, but one is intentionally limited

- `EphemeralWorkflowRuntime`
- `DurableWorkflowRuntime`

Rules:

- ephemeral runtime is not a “less reliable version of the same thing”
- it is explicitly a lightweight runtime with smaller guarantees

This is acceptable if durable remains the only long-term advanced path.

### Option B: One core engine plus one experimental durable backend

- keep current engine as product runtime
- build event-driven durable core as experimental or research track

Rules:

- do not market both as equivalent product choices yet

This is the safest research approach.

### Option C: Split by product identity

- `OrcaCore.Light`
- `OrcaCore.Durable`

This is the clearest product story but the heaviest packaging story.

## 11. Recommendation

My honest recommendation is:

1. Keep the current architecture as the practical engine for now.
2. Treat the event-driven architecture as the candidate long-term durable/saga core.
3. Do not commit yet to a permanent “two equal engines” product strategy.
4. If you explore both, do it as:
   - stable current engine
   - experimental event-driven durable engine
5. Make the long-term decision only after both pass the same acceptance suite.

Why:

- current design already solves the short-term product problem well
- event-driven design is strategically stronger for durable+saga
- the risk of permanent dual-engine drift is high

## 12. Final Answer To The Two-Engine Idea

Is it advisable?

- yes as a transition and research strategy
- conditionally yes as a product strategy if the lightweight engine stays intentionally narrow
- no if both engines are allowed to become broad, overlapping workflow runtimes

How hard is it to maintain?

- moderately hard if the scope split is strict
- very hard if both engines aim at similar breadth

Possible conflicts:

- semantic drift
- support burden
- migration ambiguity
- duplicated test matrix
- duplicated extension models

Usability impact:

- acceptable if the split is explicit and easy to explain
- poor if users must guess which engine they need from subtle guarantees

So the clean product line is:

- current engine for simple, local, quick workflows
- event-driven engine only if it becomes the durable/saga strategic engine
- avoid a future where both engines compete for the same use cases
