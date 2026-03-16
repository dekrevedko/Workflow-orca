# Design Synthesis

Reviewed on March 15, 2026.

## Purpose

This document consolidates the current research into one non-contradictory baseline for OrcaCore.

It answers five questions:

1. What features should the engine support?
2. What design practices look strong based on research?
3. What design practices look risky or weak?
4. What tradeoffs are real and unavoidable?
5. What important questions are still open?

This document is the current best high-level product and architecture baseline.

## 1. Core product definition

OrcaCore should be an embeddable .NET 10 workflow engine for application-local orchestration.

It should use two orthogonal axes:

- definition semantics:
  - regular workflow
  - saga workflow
- execution mode:
  - ephemeral mode
  - durable mode

Why:

- saga changes business semantics around compensation and failure
- durability changes runtime guarantees
- separating these axes reduces contradictions and keeps feature support explicit

The engine should not require a dedicated external workflow platform, but it should still support long-running workflows when durable mode is enabled.

## 2. Stable feature list

The list below reflects the current non-contradictory feature baseline.

### Workflow authoring

- Code-first workflow definitions.
- Separate definition semantics for regular workflow and saga workflow.
- Reusable infrastructure steps.
- User-defined business steps.
- Async-first execution contracts.
- Likely future support for serialized definitions, but not required in phase 1.

### Infrastructure/control-flow steps

Baseline shared primitives:

- `Init`
- `End`
- `If`
- `While`
- `Parallel`
- `WhenAll`
- `WhenFirst`
- `Wait`
- `WaitLong`

Likely next additions:

- `Delay` or `Timer`
- `ChildWorkflow`
- `Publish`
- `Cancel`

Saga-specific primitives:

- `CompensationScope`
- `Compensate`
- `Try`
- `Catch`
- `Finally`

### Workflow instance model

- Stable `InstanceId`.
- Definition identity and version bound to the instance.
- Runtime state separate from business state.
- Serialized execution guarantee per workflow instance.
- Queryable runtime metadata.
- Serializable business state.

### Runtime modes

#### Ephemeral mode

- In-memory execution only.
- No durable rehydration.
- Restart loses instances.
- `WaitLong` and other durable-only features are unavailable.
- Short waits and short-running workflows still work.
- API should hide durable-only features where possible.

#### Durable mode

- Persistence-backed workflow instances.
- Suspension and rehydration.
- Durable waits and durable timers.
- Post-restart resume.
- Retention, history/checkpoint inspection, and durable operational queries.

### Event and wait behavior

- External event waits are first-class.
- Correlated resume of specific workflow instances.
- Wait for any / wait for all patterns.
- Durable timers or equivalent wake-up primitives in durable mode.
- Explicit handling of unresolved waits/timers at completion.
- Timer/event race semantics must be deterministic.

### Parallelism and synchronization

- Branches may execute concurrently.
- Parent workflow instance state commits remain serialized.
- `WhenAll` and `WhenFirst` are explicit runtime primitives.
- Losing-branch cancellation or ignore policy must be explicit.

### Persistence and providers

- Persistence provider is optional.
- Event provider is pluggable.
- Multiple database/message technologies should be supportable through adapters.
- Runtime metadata must stay queryable in durable mode.
- Business payload may be serialized separately.
- Provider contracts must support per-instance concurrency guarantees.

### Lifecycle and operations

- Workflow and step lifecycle events.
- Timeout policy for steps.
- Stuck detection for steps and instances.
- Active-instance eviction for idle in-memory instances.
- Terminal instance retention and cleanup policy in durable mode.
- Operational statistics: counts by definition/version/status and similar visibility.
- Separate management command surface from workflow steps.
- Fluent LINQ-like management API with scope, filter, and terminal operation semantics.

### Versioning and deployment

- Durable instances are bound to definition version.
- Deployment/versioning rules must prevent silent corruption of long-running instances.
- Incompatible definition changes must fail explicitly or remain isolated by version.

### Future-but-likely features

These are not final commitments, but they are now strongly indicated by the research:

- child workflows / sub-workflows
- continue-as-new or equivalent history rollover
- query/signal-style management surface
- pause/resume/terminate/purge management APIs

## 3. Best-practice design baseline

These are the strongest design practices supported by the research so far.

### 3.1 Separate orchestration logic from side effects

This is one of the strongest lessons from Temporal and Durable Functions.

The runtime should own orchestration state changes. Steps should express intent. Side-effecting work should happen in explicit execution paths, not as hidden orchestration mutation.

### 3.2 Keep one logical mutator per workflow instance

This is one of the strongest lessons from Orleans.

For a given `InstanceId`, committed state transitions should appear in a single serial order, even if events and branch completions arrive concurrently.

### 3.3 Allow branch execution concurrency but serialize parent-state merge

This is the best balance discovered so far.

Parallel branches may execute concurrently. Their outcomes are merged back into the parent workflow instance through one serialized commit path.

### 3.4 Separate runtime state from business state

Do not treat the entire workflow instance as one opaque application object.

- runtime state is engine-owned and queryable
- business state is workflow-owned and serializable

### 3.5 Make durability explicit, not implied

The engine should not pretend all workflows are durable.

Ephemeral mode and durable mode must have a clear feature matrix and clear diagnostics when durable-only features are requested without persistence.

### 3.6 Prefer API-level separation for durable-only capabilities where possible

Why:

- avoids many invalid operations earlier
- reduces runtime guard logic
- makes the supported feature surface clearer to users

### 3.7 Make management API fluent and composable

Why:

- separates selection from action
- avoids method-name explosion
- creates one consistent mental model across engine-wide, definition-scoped, and instance-scoped management
- aligns naturally with LINQ-like semantics

### 3.8 Make wait and timer semantics first-class

Waiting is not a side feature. It is one of the core runtime primitives.

`Wait`, `WaitLong`, external events, durable timers, and timer/event races all need explicit semantics.

### 3.9 Bind durable instances to definition versions

Long-running workflows must be version-aware by design.

Versioning is not an afterthought. It is a core durability constraint.

### 3.10 Treat active memory as a cache, not the source of truth

This is strongly supported by Orleans and Durable Functions style resource management.

Loaded in-memory instances are disposable activations. Durable state is authoritative in durable mode.

### 3.11 Make operational visibility first-class

Users need:

- instance inspection
- active wait inspection
- history/checkpoint visibility in durable mode
- lifecycle events
- counts and statistics
- stuck/timeout visibility

### 3.12 Keep steps, policies, management commands, and semantic kinds separate

Why:

- steps describe workflow structure and business action
- decorators/policies describe behavior modifiers
- management commands describe runtime operations
- workflow vs saga describes semantic kind
- ephemeral vs durable describes guarantee level

### 3.13 Fail fast for unsupported semantics

If a feature depends on durability, persistence, or provider capability, the engine should reject it clearly rather than silently downgrade behavior.

## 4. Bad-practice / anti-pattern baseline

These are the strongest things to avoid.

### 4.1 Opaque JSON blob as the only persisted shape

This hides runtime metadata, harms inspection, and creates operator pain.

### 4.2 Concurrent mutation of one workflow instance

Allowing multiple workers or threads to mutate the same instance state concurrently is a direct path to branch/wait race bugs.

### 4.3 Treating waits as ad hoc subscriptions without first-class runtime records

Waits need identity, correlation, cancellation, and inspection. They should not be improvised at the event-provider edge.

### 4.4 Depending on shutdown/deactivation hooks for critical persistence

Critical persistence or event publication must happen before safe boundaries, not during best-effort cleanup.

### 4.5 Pretending in-memory mode is durable

Ephemeral mode is valid, but it must not claim restart safety or durable inspection.

### 4.6 Allowing workflow completion while runtime-owned waits/timers are unresolved without policy

This creates undefined semantics and hidden leaks.

### 4.7 Making feature interactions implicit

Branching, joins, wait races, cancellation, retries, and timeouts must be explicit. Hidden interactions are where competitors have failed.

### 4.8 Tying product semantics to one provider's implementation details

Provider capabilities matter, but the product contract should remain engine-owned and provider-neutral where possible.

### 4.9 Letting history/checkpoint growth stay invisible

If durable mode uses replay or history-heavy recovery, the system must surface growth and pressure before it becomes an outage.

### 4.10 Treating saga as merely a regular workflow plus a flag

Saga has distinct compensation and failure semantics. Modeling it as a boolean option on normal workflow weakens clarity.

### 4.11 Encoding management semantics into endless special-case method names

Examples like `PauseAllRunningFailedFoo` are a sign the management API is not composable enough.

## 5. Important tradeoffs

These are real design tensions, not documentation gaps.

### 5.1 Replay/history model vs checkpoint/snapshot model

Replay/history model:

- stronger audit trail
- stronger deterministic recovery model
- potential history growth and replay cost

Checkpoint/snapshot model:

- simpler restore path
- potentially lower replay cost
- weaker event-history fidelity unless augmented

### 5.2 Deterministic orchestration discipline vs authoring freedom

Stricter orchestration discipline:

- safer retries and replay
- clearer side-effect boundaries
- more authoring constraints

Freer step model:

- easier user code
- more room for hidden nondeterminism and retry hazards

### 5.3 Simplicity of one state object vs separation of runtime and business state

One state object:

- simpler API surface
- weaker queryability and operability

Separated runtime/business state:

- more explicit architecture
- better operations and correctness
- more model complexity

### 5.4 Single-node convenience vs future multi-node correctness

Single-node assumptions:

- simpler implementation
- easier active-instance ownership

Future multi-node support:

- stronger scale story
- harder instance ownership, lease, and duplicate-activation problems

### 5.5 Rich feature surface vs semantic clarity

Adding many step types and capabilities early can create contradictions if the interaction semantics are not fully specified first.

### 5.6 Durable mode power vs ephemeral mode simplicity

Ephemeral mode is useful and lightweight.
Durable mode is more powerful but imposes harder runtime contracts.
The engine must keep the mode distinction explicit rather than trying to blur them.

### 5.7 API-level separation vs shared surface with runtime checks

API-level separation:

- clearer support matrix
- fewer invalid operations
- more types/interfaces to design

Shared surface with runtime checks:

- simpler top-level API
- more runtime validation
- greater risk of ambiguous or late failures

## 6. Non-contradictory current positions

These are decisions that are now strong enough to treat as current baseline positions.

- Persistence is optional.
- Durable features require durable mode.
- `WaitLong` is durable-only.
- Workflow instance state changes are serialized per instance.
- Parallel branches may execute concurrently, but parent-instance state commits are serialized.
- Runtime state and business state are separate concerns.
- Active in-memory instances are evictable.
- Terminal instances should leave active memory quickly after terminal handling.
- Lifecycle events and operational queries are required product features.
- Version-bound durable instances are required.
- Orchestration logic and side effects must be separated.
- Workflow and saga are separate semantic definition kinds.
- Ephemeral and durable are separate execution modes.
- Durable-only capabilities should be separated at the API level where practical.
- Ephemeral saga is allowed but limited.
- Management API should be fluent, composable, and LINQ-like.

## 7. Open questions

These questions remain open and still need explicit design decisions.

### Runtime model

- Should durable mode use replay/history, checkpoint/snapshot, or hybrid recovery?
- What is the smallest useful history model that still supports debugging and confidence?

### Wait semantics

- Should `Wait` and `WaitLong` be separate public concepts forever, or one concept with policy-driven behavior?
- What exactly counts as short wait versus long wait?
- What is the exact policy for timer/event races?

### Definition model

- Graph-based only, fluent builder only, or both?
- When should serialized definitions be introduced?
- What exact base abstractions should regular workflow and saga share?

### Business state model

- Strongly typed, loosely typed, or hybrid?
- How much schema/version responsibility belongs to the engine versus the application?

### Side effects and messaging

- Can business steps publish directly, or must they only request publication through runtime-owned results?
- What are the official delivery guarantees by mode and provider capability?

### Parallel semantics

- Exact `WhenFirst` losing-branch policy?
- Exact cancellation and compensation interaction with branches?
- Exact rules for branch completion ordering visibility?

### Lifecycle and operations

- Which lifecycle events are durable versus best-effort?
- Should active-instance eviction use plain idle timeout, LRU-like pressure eviction, or hybrid policy?
- Which statistics are required in phase 1 versus later?

### Versioning and deployment

- How should incompatible definition changes be handled in durable mode?
- Should child workflows and continue-as-new be phase-1 or later features?

### Public API shape

- How much durable/ephemeral separation should be enforced at compile time versus runtime?
- Should decorators be attributes, fluent builders, metadata objects, or hybrid?

## 8. Recommended next design sequence

To reduce contradictions, the next design work should happen in this order:

1. Define `Parallel`, `WhenAll`, and `WhenFirst` semantics.
2. Define event envelope, correlation, and deduplication.
3. Define durable mode recovery model: replay, checkpoint, or hybrid.
4. Define lifecycle event durability and timeout semantics.
5. Define versioning and deployment rules for durable instances.
6. Define feature matrix for workflow vs saga and ephemeral vs durable.
7. Define management command surface and step decorators.

## 9. Final conclusion

The research now points to a coherent architecture direction:

OrcaCore should be an embeddable workflow engine with optional durability, explicit runtime modes, separate workflow and saga semantics, per-instance serialized execution, first-class waits/events/timers, queryable runtime metadata, strong operational visibility, and version-aware long-running instances.

The biggest risk is not missing features. The biggest risk is ambiguous semantics where features interact.

So the engine should prefer semantic clarity over early surface-area growth.
