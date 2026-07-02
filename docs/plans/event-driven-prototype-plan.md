# Event-Driven Prototype Plan

Reviewed on April 13, 2026.

## Purpose

This document defines the phased prototype and research track for the event-driven OrcaCore runtime.

It is still a prototype plan, not a replacement approval.

Its job is now narrower and more explicit:

- de-risk the architecture that the parity plan depends on
- provide honest evidence about whether the event-driven design can satisfy shared durable semantics
- surface cost, complexity, and operational tradeoffs before more replacement work is committed

Replacement readiness itself is governed by:

- [event-driven-durable-parity-plan.md](event-driven-durable-parity-plan.md)

## Relationship To The Parity Plan

The parity plan is authoritative for:

- replacement scope
- replacement gates
- backend-switch approval
- final definition of done

The prototype plan must not contradict the parity plan once it touches parity-critical behavior.

That means the prototype track should now de-risk these same architectural decisions early:

- shared `WorkflowDefinition<TState>` / `IWorkflowNode` authoring model
- one atomic `WorkflowCommit`-style durable commit boundary, or an equivalent explicit atomic model
- synchronous routing-critical and management-critical projections
- authoritative correlation routing updated in the same durable mutation boundary
- explicit durable start contract
- explicit delete model for the append-only backend
- explicit compatibility rules for runtime events, checkpoints, and projection rebuilds

Optional prototype exploration may continue beyond those topics, but it must not re-open them casually.

## Prototype Goals

The prototype should prove or disprove these claims:

1. The event-driven runtime can preserve shared regular durable semantics using the same authored definition model as the current durable engine.
2. An atomic commit boundary around stream, checkpoint, inbox, outbox, and parity-critical projections is practical and easier to reason about than split snapshot-first mutation.
3. Checkpoint plus stream-tail recovery, restart-safe deduplication, and projection rebuild are workable without hidden hot-memory truth.
4. Cold-instance routing, rehydration, and query behavior remain coherent when authoritative state is durable instead of resident in memory.
5. The event-driven design is practical for an embedded .NET workflow engine and does not require turning OrcaCore into a separate workflow platform product.

## Prototype Non-Goals

The prototype is not initially trying to deliver:

- replacement approval or backend switching
- multiple durable providers
- production-grade multi-host ownership
- live migration from the snapshot backend
- polished public API beyond what is required to exercise shared definitions and parity-critical operations
- full archive and retention lifecycle beyond what is needed to validate delete and purge semantics
- requiring saga support in order to validate regular durable parity

## Prototype Success Criteria

The prototype is successful if it can demonstrate all of the following:

- shared `WorkflowDefinition<TState>` / `IWorkflowNode` authoring can drive the event-driven runtime
- deterministic per-instance serialized mutation with explicit expected-version enforcement
- one explicit durable start contract for the tested slice
- one explicit atomic durable commit boundary for parity-critical mutation paths
- authoritative active-wait and routing state that is durable, synchronous, and not dependent on eventual async projection catch-up
- checkpoint plus stream-tail recovery
- restart-safe inbox deduplication and deterministic outbox identity for the tested slice
- an explicit deleted-state model that prevents deleted-instance resurrection
- written compatibility rules for persisted runtime artifacts used by the prototype

Prototype success does not mean the engine is approved as a replacement.

It means the parity path is credible enough to justify continued implementation under the parity plan.

## Phase 0: Contract And Architecture Freeze

Before new prototype feature work:

- crosswalk the prototype scope against the parity plan
- freeze the parity-critical decisions the prototype will rely on:
  - definition-model adoption
  - durable start contract
  - atomic durable commit boundary
  - projection consistency model
  - correlation index ownership
  - delete model
  - runtime artifact compatibility approach
- classify which prototype scenarios are:
  - shared parity gates later
  - prototype-only research checks
  - explicitly deferred

Deliverables:

- prototype-to-parity crosswalk
- architecture decision checklist for the prototype track

Exit criteria:

- no unresolved contradictions remain between this plan and the parity plan for any parity-critical topic

## Phase 1: Honest Baseline And Prototype Hardening

Fix the prototype defects that can create false confidence before broader feature work continues.

Required hardening baseline:

- reject unmatched events for terminal instances
- enforce expected stream version on commit
- route all post-start mutation through the serialized instance lane
- drain buffered events until stable rather than consuming only one match

Also:

- capture the current implemented slice honestly
- identify the first shared durable tests that should be expected to fail
- adopt the shared definition model plan before new control-flow investment continues

Deliverables:

- corrected prototype baseline
- blocker regression tests
- red/green backlog against the first shared parity slice

Exit criteria:

- the prototype no longer gives misleading parity signals because of known correctness defects

## Phase 2: Write Model And Durable Contract Specification

Design the event-driven write side before major implementation proceeds.

Specify:

- command catalog
- workflow event catalog
- aggregate state shape
- execution-position and checkpoint shape
- stream version and optimistic concurrency contract
- command idempotency rules
- durable start contract
- delete and tombstone model
- runtime artifact compatibility rules
- durable commit boundary

Minimum command set for the parity-relevant slice:

- `StartWorkflowCommand`
- `DeliverInstanceEventCommand`
- `DeliverCorrelationEventCommand`
- `DeliverDefinitionEventCommand`
- `DeleteInstanceCommand`

Optional later commands:

- `FireTimerCommand`
- `PurgeArtifactsCommand`
- saga-specific commands

Minimum event set for the parity-relevant slice:

- `WorkflowStarted`
- `DefinitionVersionBound`
- `StepSucceeded`
- `StepFailed`
- `WaitRegistered`
- `EventBuffered`
- `BufferedEventConsumed`
- `WaitMatched`
- `BranchCompleted`
- `JoinSatisfied`
- `WorkflowCompleted`
- `WorkflowFailed`
- deleted-state or tombstone event if delete is modeled through stream facts

Deliverables:

- command and event catalog note
- start-contract note
- stream and checkpoint schema note
- delete-model note
- runtime artifact compatibility note

Exit criteria:

- there is no ambiguity about which durable facts are committed for each accepted mutation
- the durable start and delete contracts are explicit
- the commit boundary is frozen before provider work expands

## Phase 3: Projection And Routing Specification

Define the read-side model required for the prototype.

Minimum projections for the parity-relevant slice:

- `InstanceSummaryProjection`
- `ActiveWaitProjection`
- `PendingEventProjection`
- outbox summary or dispatch projection for the tested outbox slice
- history projection only if needed for the selected operator tests

Decide explicitly:

- which projections are updated inline in the same durable mutation boundary
- whether any projection work is scheduled durably rather than applied inline
- rebuild rules from stream and checkpoint
- routing dependence on projections
- startup rebuild of hot caches from durable authoritative state
- how deleted instances remain visibly deleted rather than silently absent

Deliverables:

- projection catalog
- query and routing contract note

Exit criteria:

- every required management or routing use case maps to one authoritative durable read model
- routing-critical behavior does not depend on eventual async projection lag

## Phase 4: Reference Provider And Harness

Implement one narrow reference provider and the supporting test harness.

Provider direction:

- one reference provider only
- simplest valid in-memory or local durable implementation is acceptable
- internal modules may be split, but the correctness contract exposed to the runtime remains one atomic durable boundary

Prototype harness tasks:

- add backend fixture support for the prototype slice
- add failure-injection hooks around the durable commit boundary
- add crash and restart simulation support for the tested slice
- make parity drift visible rather than implicit

Do not optimize for a provider matrix yet.

Deliverables:

- reference provider
- failure-injection harness
- prototype backend fixture

Exit criteria:

- the reference path can commit parity-critical durable state atomically
- partial mutation leaks can be tested directly

## Phase 5: Regular Workflow Prototype Slice

Implement a narrow but honest regular durable slice using the shared definition model.

Scope:

- straight-line execution
- `Wait`
- instance-targeted events
- correlation-targeted routing
- out-of-order buffering
- duplicate-event deduplication
- `If`
- `While`
- `Parallel`
- `WhenAll`
- definition fanout

Required semantic focus:

- branch-scoped waits
- deterministic join-once behavior
- ordering invariance
- resumed payload handoff after waits and loop waits
- read-your-writes routing immediately after wait registration

Do not add yet:

- saga
- multi-host ownership
- broad timer framework

Goal:

- prove that shared regular durable semantics are achievable on the event-driven substrate without inventing a separate programming model

Exit criteria:

- the selected regular shared acceptance slice passes on the prototype

## Phase 6: Durable Slice

Add the durable behavior required to prove the main event-driven claims.

Scope:

- event stream persistence
- checkpointing
- checkpoint plus stream-tail recovery
- `WaitLong`
- cold-instance eviction and resume
- authoritative active-wait routing state
- inbox semantics for restart-safe dedup
- deterministic outbox identity for the tested slice
- projection-backed query slice
- runtime artifact compatibility tests for the supported prototype window
- delete semantics that do not allow resurrection after late delivery or rebuild

Goal:

- prove the main durable claims of the event-driven design without yet declaring replacement readiness

Minimum acceptance focus:

- durable wait survives restart
- crash restores last committed state only
- durable query can read runtime metadata from durable projections
- concurrent durable resume attempts serialize
- commit-boundary failure does not leak partial visible mutation
- deleted instances stay deleted under late delivery and rebuild

Exit criteria:

- the prototype passes a narrow durable acceptance slice that is honest about crash safety, recovery, and delete behavior

## Phase 7: Optional Timer Slice

Timers are useful research, but they are not on the critical path to regular durable parity.

Optional scope:

- timer scheduling
- timer fire command path
- deterministic timer versus event race rules

Goal:

- validate timer-ready infrastructure without broad timeout decorator work

Exit criteria:

- one durable timer scenario works end to end

## Phase 8: Optional Narrow Saga Slice

Saga is useful research, but it is not required to validate replacement of the current regular durable engine.

Optional scope:

- separate saga definition kind
- forward action completion recording
- compensation stack
- reverse compensation order
- compensation failure visibility

Do not add every saga feature yet.

Goal:

- validate whether saga becomes materially cleaner on the event-driven substrate after the regular durable path is already credible

Exit criteria:

- one narrow saga acceptance slice passes:
  - success without compensation
  - failure triggers compensation
  - deterministic compensation order

## Phase 9: Comparative Evaluation And Handoff

Run a formal comparison between:

- current durable engine
- event-driven prototype

Compare:

- semantic fidelity
- implementation complexity
- test complexity
- operational visibility
- provider complexity
- durability clarity
- restart and crash semantics
- optional timer and saga fit, if those slices were attempted

Deliverables:

- comparison scorecard
- recommendation memo
- explicit handoff note to the parity plan, or a stop note if the prototype fails to justify further work

Exit criteria:

- the next decision is based on evidence rather than architecture preference alone

## Recommended Acceptance Slice For The Prototype

Use a smaller evaluation suite first, but align it with parity-critical risk.

### Early contract slice

- terminal instance rejects late unmatched events
- duplicate-start policy is explicit for the prototype start surface
- commit-boundary failure does not leak partial visible mutation
- correlation-targeted routing is read-your-writes safe
- deleted instances are not recreated by late delivery

### Regular workflow slice

- straight-line completion
- wait enters waiting state
- matching event resumes exactly once
- out-of-order event buffered and later consumed
- duplicate event deduplicated
- correlation-targeted routing resumes exactly one instance
- parallel branches join exactly once
- wait inside loop resumes correctly without replaying prior committed progress
- concurrent resume attempts serialize

### Durable slice

- durable wait survives restart
- rehydration restores committed state only
- checkpoint plus stream-tail recovery works
- durable instance remains version-bound
- durable inspection remains queryable
- compatible persisted runtime artifacts can be read or rebuilt under the documented prototype rules

### Optional research slices

- timer and event race resolves deterministically
- saga failure triggers deterministic compensation

## Risks

### Risk 1: Prototype becomes a second full product

Mitigation:

- keep the prototype explicitly subordinate to the parity plan
- reuse the shared definition model rather than growing a separate long-term programming model

### Risk 2: Split provider contracts hide atomicity bugs

Mitigation:

- freeze one atomic `WorkflowCommit`-style boundary before provider work grows
- do not treat provider call ordering as a product guarantee

### Risk 3: Runtime artifact schema drifts without an upgrade story

Mitigation:

- define event, checkpoint, and rebuild compatibility rules early
- add compatibility tests before the prototype claims durable credibility

### Risk 4: Delete behavior remains implicit

Mitigation:

- freeze the deleted-state model before delete and purge work
- test late delivery and rebuild against deleted instances explicitly

### Risk 5: Prototype validates only an in-memory happy path

Mitigation:

- add failure injection and restart simulation early
- do not confuse reference-provider success with replacement approval

### Risk 6: Optional timer or saga research delays regular durable proof

Mitigation:

- keep timer and saga slices off the critical path
- require regular durable evidence first

## Honest Cost Assessment

Expected cost is still medium-high.

Most expensive parts:

- execution-model redesign
- atomic durable commit and projection consistency
- runtime artifact compatibility design
- durable test harness and failure injection

Least expensive parts:

- reusing the shared authored definition model
- reusing many acceptance scenarios
- using the current durable engine as the semantic benchmark

## Recommendation

The right next move is not to treat the prototype as an independent architecture track.

The right next move is:

1. harden the existing prototype so it gives honest signals
2. freeze the parity-critical architecture decisions
3. adapt the prototype to the shared definition model
4. add the failure-injection and restart harness around one atomic durable boundary
5. implement the narrow regular and durable proof slices
6. only then decide whether optional timer or saga research is worth additional investment

That keeps the prototype useful without letting it drift away from the replacement path it is supposed to inform.
