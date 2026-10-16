# Deep Dive: MassTransit Saga State Machines and Stateless

Reviewed on March 14, 2026.

## Why these two matter

These two references address different but complementary parts of OrcaCore.

- MassTransit saga state machines are a strong reference for long-running, event-driven coordination with durable state, correlation, and messaging concerns.
- Stateless is a strong reference for compact, explicit runtime state modeling inside a process.

They should not be treated as equivalent alternatives. MassTransit is closer to distributed coordination. Stateless is closer to a local transition engine.

## 1. MassTransit saga state machines

### What the model actually is

MassTransit treats a saga as a long-lived coordinator. Its documentation describes sagas as being initiated by an event, orchestrating events, and maintaining state over time instead of relying on immediate consistency or distributed locking.

This is highly relevant to OrcaCore because your engine also needs to survive long waits, consume correlated events, and maintain persisted coordination state.

Primary sources:

- Saga overview: https://masstransit.io/documentation/patterns/saga
- Saga state machine: https://masstransit.io/documentation/patterns/saga/state-machine
- Transactional outbox: https://masstransit.io/documentation/patterns/transactional-outbox

### Key concepts worth borrowing

#### A. Instance-per-coordination model

MassTransit separates the state machine definition from the persisted instance.

- The state machine is a reusable class derived from `MassTransitStateMachine<T>`.
- Each running saga has an instance that stores `CorrelationId` and current state.
- A repository persists those instances.

Why this matters:

OrcaCore should also separate:

- workflow definition
- workflow instance state
- runtime orchestration logic

This is already directionally present in the project, but MassTransit confirms that the instance should be the durable owner of correlation and current progression state.

#### B. Correlation is not optional, it is the entry point

MassTransit requires events to be correlated to an instance, either by `CorrelatedBy<Guid>` or by an explicit correlation expression such as `CorrelateById`.

Why this matters:

For OrcaCore, event waiting cannot be modeled as "just subscribe and resume later." It needs explicit matching rules for:

- which workflow instance resumes
- which branch or wait token resumes
- whether the event creates a new instance, resumes an existing one, or is ignored

This reinforces a major requirement gap already identified in the project.

#### C. Out-of-order message handling must be designed, not assumed away

MassTransit explicitly warns that brokers typically do not guarantee message order. Its examples show how a state machine should accept, ignore, or safely process events that arrive in an unexpected order.

Why this matters:

OrcaCore needs explicit semantics for:

- event accepted in current state
- event ignored in current state
- event buffered for future use or rejected
- event used only to enrich state without changing control flow

A durable workflow engine that depends on message order is brittle by design.

#### D. Concurrency controls belong in the runtime boundary

MassTransit guidance shows saga definitions configuring endpoint concurrency and partitioning by correlation ID.

Why this matters:

OrcaCore will need a comparable concept even if it is not broker-centric. At minimum:

- only one logical executor should mutate a given workflow instance at a time
- concurrent resume attempts for the same instance must resolve safely
- persistence should use optimistic concurrency or equivalent locking

This is not just an infrastructure detail. It is core runtime semantics.

#### E. Transactional outbox is one of the strongest answers to the consistency problem

MassTransit documents the transactional outbox because database updates and broker publication cannot safely rely on distributed transactions. It distinguishes:

- Bus Outbox for database plus outbound message consistency
- Consumer Outbox for inbox plus outbox behavior and duplicate protection

Why this matters:

OrcaCore currently has a consistency gap. If a workflow instance progresses and publishes an event, the engine must define how both actions become durable together.

MassTransit strongly suggests that OrcaCore should research and probably adopt an outbox/inbox style consistency boundary rather than attempt two-phase commit or vague best-effort publication.

### What MassTransit does better than your current concept

- Explicit correlation model
- Explicit instance persistence model
- Serious treatment of ordering and concurrency
- Practical answer to the state-plus-message atomicity problem
- Real operational stance on long-running coordination

### Where MassTransit is not the target architecture

- It is broker-first, while OrcaCore should remain usable even in more local or mixed hosting scenarios.
- Saga state machines are event-centric, while your engine also wants reusable workflow steps and structured control flow.
- It does not by itself give you a natural `While`, `Parallel`, `WhenAll`, or `WhenFirst` workflow abstraction.

### What OrcaCore should take from it

Adopt:

- durable instance-per-execution state
- mandatory correlation model
- explicit allowed/ignored/out-of-order event semantics
- optimistic concurrency or per-instance serialized execution
- transactional outbox and inbox research as first-class requirements

Do not adopt blindly:

- a purely broker-shaped programming model
- saga state machine syntax as the only authoring model

## 2. Stateless

### What the library actually is

Stateless is a minimal .NET state machine library. It is not a workflow runtime and does not provide durability, messaging, bookmarks, or distributed coordination. Its value is in modeling state transitions clearly and compactly.

Primary source:

- Repository and README: https://github.com/dotnet-state-machine/stateless

### Key concepts worth borrowing

#### A. State and trigger rules are explicit

Stateless makes valid transitions explicit with a compact API. Unhandled triggers throw by default unless they are explicitly ignored.

Why this matters:

OrcaCore needs the same discipline in its runtime internals. State progression should not be implicit in scattered conditionals. Internal runtime states such as `Runnable`, `Waiting`, `Completed`, `Faulted`, `Canceled`, and `Resuming` should have explicit allowed transitions.

#### B. Guard clauses are valuable, but must be side-effect free

Stateless supports guarded transitions and says guards should be mutually exclusive and side-effect free.

Why this matters:

This is a very strong rule for OrcaCore branch and synchronization semantics.

Examples:

- deciding whether an `If` condition leads to branch A or branch B
- deciding whether `WhenFirst` has a winning branch yet
- deciding whether a timeout path should fire

These decisions should be deterministic and free of hidden side effects.

#### C. External state storage is a useful design pattern

Stateless can read and write state via delegates, so the library can be embedded into systems where state is stored elsewhere.

Why this matters:

This is directly relevant to OrcaCore architecture. Even if OrcaCore does not use Stateless internally, the pattern is important:

- runtime logic can operate over externally stored state
- transition rules do not need to own persistence details
- orchestration logic and persistence concerns can stay separated

#### D. Hierarchical states are useful for runtime modeling

Stateless supports substates and `IsInState`, allowing one state to refine another without losing the parent meaning.

Why this matters:

OrcaCore likely has state hierarchies whether you name them or not. For example:

- `Active`
- `Active/Running`
- `Active/Waiting`
- `Active/WaitingShort`
- `Active/WaitingLong`
- `Terminal/Completed`
- `Terminal/Faulted`

This can simplify runtime reasoning, status reporting, and guard logic.

#### E. Introspection and graph export are useful for debugging and docs

Stateless supports introspection and export to DOT and Mermaid.

Why this matters:

OrcaCore should probably expose its workflow definitions and maybe runtime state transitions in an inspectable graph form. That helps with:

- debugging workflow definitions
- documenting control flow
- understanding allowed state transitions inside the engine itself

#### F. Async support is useful, but the concurrency model is a warning sign

Stateless supports async entry/exit actions and async trigger firing, but it explicitly states that the state machine remains single-threaded and may not be used concurrently by multiple threads.

Why this matters:

This is a helpful warning for OrcaCore:

- async does not automatically imply safe concurrency
- one instance should have serialized state mutation
- the engine should make concurrency boundaries explicit

### What Stateless does better than your current concept

- Gives a very clear mental model for explicit runtime state transitions
- Encourages compact, readable transition definitions
- Makes ignored versus invalid triggers explicit
- Reinforces side-effect-free guards and inspectable transition rules

### Where Stateless is not enough

- No persistence model
- No event broker integration
- No durable wait model
- No inbox/outbox or deduplication story
- No branch fan-out/fan-in orchestration model
- No workflow instance history or resumability semantics by itself

### What OrcaCore should take from it

Adopt:

- explicit state and trigger model for the runtime itself
- side-effect-free guards for control-flow decisions
- hierarchical runtime state modeling where it reduces complexity
- introspection and graph export ideas

Do not adopt blindly:

- using a local state machine abstraction as if it solved durability
- assuming async transition handlers imply safe concurrent execution

## 3. Combined guidance for OrcaCore

The strongest architecture signal from these two references is this:

- MassTransit helps define how a durable workflow instance interacts with events, correlation, persistence, concurrency, and message consistency.
- Stateless helps define how the runtime itself should model legal transitions and state evolution in a compact, testable way.

That suggests OrcaCore should combine both perspectives.

### Recommended split

#### Use a state-machine mindset for runtime internals

Model the engine's own lifecycle explicitly, for example:

- `Created`
- `Runnable`
- `Executing`
- `Waiting`
- `Resuming`
- `Completed`
- `Faulted`
- `Canceled`

Also consider branch-level states separately from whole-workflow states.

#### Use a saga mindset for external event coordination

Model wait/resume behavior with durable correlated records, for example:

- workflow instance ID
- branch ID or token
- wait subscription ID
- accepted event types
- correlation rules
- timeout / expiration metadata
- deduplication metadata

### Strong requirement changes implied by this research

OrcaCore requirements should explicitly include:

1. A formal runtime state model with legal transitions.
2. Explicit ignored/invalid/out-of-order event behavior.
3. A workflow-instance concurrency policy.
4. A correlation model for all resumable waits.
5. A durable consistency strategy for state changes and outbound event publication.
6. Runtime introspection sufficient to inspect state, waits, and last transitions.

## 4. Questions this deep dive answers

### Should OrcaCore use Stateless directly?

Probably not as the core engine foundation.

Reason:

It is useful as a modeling reference and could be used in isolated internal components, but it does not cover the hard parts of workflow durability, event resume, provider abstraction, or long-running coordination.

### Should OrcaCore look more like a saga engine?

Partially, yes.

Reason:

Long-running event-driven coordination is saga-like. But OrcaCore also needs structured reusable workflow steps, so it should not collapse entirely into a pure message-saga abstraction.

### Is the current idea missing critical semantics?

Yes.

The biggest missing semantics reinforced by this research are:

- correlation
- out-of-order event handling
- per-instance concurrency control
- durability boundaries for state and published messages
- explicit runtime state transitions

## 5. Recommended next research work

1. Write a dedicated event and correlation model document.
2. Write a dedicated runtime state model document.
3. Compare checkpoint-based persistence versus history/replay for waits and resumptions.
4. Define what `Wait` and `WaitLong` mean in exact runtime and storage terms.

## Sources

- MassTransit saga overview: https://masstransit.io/documentation/patterns/saga
- MassTransit saga state machine: https://masstransit.io/documentation/patterns/saga/state-machine
- MassTransit transactional outbox: https://masstransit.io/documentation/patterns/transactional-outbox
- Stateless repository and README: https://github.com/dotnet-state-machine/stateless
