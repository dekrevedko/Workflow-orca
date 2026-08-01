# Durable Functions Patterns for OrcaCore

Reviewed on March 14, 2026.

## Scope

This document extracts ideas from Azure Durable Functions and the Durable Task model that are useful for OrcaCore.

The goal is not to copy Azure Functions hosting or force OrcaCore into a full replay-only architecture. The goal is to identify the strongest runtime semantics worth borrowing.

Primary sources:

- Durable orchestrations: https://learn.microsoft.com/en-us/azure/azure-functions/durable/durable-functions-orchestrations
- Orchestrator code constraints: https://learn.microsoft.com/en-us/azure/azure-functions/durable/durable-functions-code-constraints
- External events: https://learn.microsoft.com/en-us/azure/azure-functions/durable/durable-functions-external-events
- Timers: https://learn.microsoft.com/en-us/azure/azure-functions/durable/durable-functions-timers
- Fan-out/fan-in: https://learn.microsoft.com/en-us/azure/azure-functions/durable/durable-functions-cloud-backup
- Human interaction and timeouts: https://learn.microsoft.com/en-us/azure/azure-functions/durable/durable-functions-phone-verification
- Versioning: https://learn.microsoft.com/en-us/azure/azure-functions/durable/durable-functions-versioning
- Orchestration versioning: https://learn.microsoft.com/en-us/azure/azure-functions/durable/durable-orchestration-versioning
- Zero-downtime deployment: https://learn.microsoft.com/en-us/azure/azure-functions/durable/durable-functions-zero-downtime-deployment
- Azure Storage provider internals: https://learn.microsoft.com/en-us/azure/azure-functions/durable/durable-functions-azure-storage-provider

## 1. Event-sourced orchestration history is a serious runtime model

Durable Functions documents that orchestrations use event sourcing and replay to rebuild local state. The runtime records orchestration actions in append-only history and replays them to reconstruct orchestration state.

What to borrow:

- orchestration progress should be recoverable from durable runtime records
- local orchestration state reconstruction is a first-class runtime concern
- history can provide auditability and debugging value

What not to adopt blindly:

- event-sourced replay should not be assumed to be the only acceptable OrcaCore persistence model
- full replay may be too restrictive or too expensive for all usage modes

Best takeaway for OrcaCore:

Keep replay-based history as a serious candidate for durable mode, but do not commit to it yet as the only architecture.

## 2. The orchestration-side-effect boundary is one of the strongest ideas to adopt

Durable Functions explicitly warns that orchestrators replay, so they must avoid direct I/O, nondeterministic APIs, uncontrolled async work, current-time APIs, random values, static mutable state, and direct network calls.

What to borrow:

- keep orchestration decisions separate from side-effecting execution
- define a strict boundary between workflow control logic and external effects
- require side effects to happen in dedicated step/activity-like execution paths

Why this matters for OrcaCore:

Even if OrcaCore does not fully adopt replay semantics, this boundary is still extremely valuable. It makes retries, deduplication, diagnostics, and future replay/checkpoint strategies much easier.

Best takeaway for OrcaCore:

Step authors should not mutate engine internals directly. Workflow control logic should request effects, and dedicated effect execution should perform them.

## 3. Durable timers should be a first-class primitive

Durable Functions treats timers as a core orchestration primitive, not as a generic sleep. The docs also note that timers can wake orchestration work later, even if the host scaled down.

What to borrow:

- timers should be modeled explicitly in the runtime
- timer-based wake-up should be durable in durable mode
- timeout logic should be composed with timers, not ad hoc polling or sleeping

Why this matters for OrcaCore:

This strongly supports having clear semantics for time-based waits and for timeout-driven branch decisions.

Best takeaway for OrcaCore:

`WaitLong` should probably build on the same durable scheduling concept as durable timers.

## 4. External events are an excellent workflow primitive

Durable Functions exposes a direct "wait for external event" pattern and documents its use for human interaction and asynchronous callbacks.

What to borrow:

- waiting for a named event with payload should be first-class
- timeouts should compose naturally with waiting for events
- human-in-the-loop scenarios should be modeled explicitly, not as a workaround

Why this matters for OrcaCore:

Your current direction already aligns with this. Durable Functions validates that this is one of the highest-value primitives in a workflow engine.

Best takeaway for OrcaCore:

Support event waits and timer races as standard patterns, not custom user code.

## 5. Fan-out/fan-in model supports your current parallel design

Durable Functions provides a clear fan-out/fan-in story: activities or sub-orchestrations can run concurrently, then results are awaited and aggregated.

What to borrow:

- parallel execution should be explicit
- join semantics should be deliberate and testable
- branch execution concurrency and parent-state serialization can coexist cleanly

Why this matters for OrcaCore:

This is directly compatible with your current model: branches may execute concurrently while parent-instance state commits stay serialized.

Best takeaway for OrcaCore:

Document `Parallel`, `WhenAll`, and `WhenFirst` as orchestration patterns with explicit merge semantics.

## 6. Sub-orchestration pattern is valuable for history and complexity control

Durable Functions commonly uses sub-orchestrations to break large workflows into smaller parts.

The Azure Storage provider documentation also warns that orchestration history is fully loaded and replayed, which can create memory pressure. It explicitly recommends reducing history length and size, for example by splitting large orchestrations into sub-orchestrations and reducing payload sizes.

What to borrow:

- large workflows should be decomposable
- runtime design must consider history/checkpoint growth
- orchestration payload size and history size are product concerns, not just implementation details

Best takeaway for OrcaCore:

Child workflows or sub-workflows should remain in scope as a likely future feature, and history size must be tracked operationally.

## 7. Version-bound instances are essential

Durable Functions versioning docs make a very important point: long-running orchestrations can fail, get stuck, or throw nondeterminism errors if code changes are incompatible with the previously recorded history. Microsoft recommends version-aware strategies and documents orchestration versioning where each instance is permanently associated with a version.

What to borrow:

- each workflow instance should be bound to a definition version
- runtime behavior must be explicit when code changes while instances are still active
- zero-downtime deployment requires version-aware runtime behavior

Why this matters for OrcaCore:

This should no longer be treated as a secondary concern. It is one of the core design constraints for any durable workflow engine.

Best takeaway for OrcaCore:

Definition version must be part of the instance identity model and part of management/inspection APIs.

## 8. Outstanding timers and unfinished work affect completion semantics

Durable Functions timers documentation warns that outstanding timers must be canceled if they will not be awaited, because orchestration completion waits for outstanding durable tasks to complete or cancel.

What to borrow:

- unfinished waits and timers must have explicit ownership and cancellation semantics
- workflow completion should check for unresolved runtime-owned work

Why this matters for OrcaCore:

This directly informs branch cancellation, `WhenFirst`, timeout races, and terminal-state correctness.

Best takeaway for OrcaCore:

A workflow should not silently complete while runtime-owned waits or timers remain unresolved unless policy explicitly allows ignoring them.

## 9. History size, memory pressure, and concurrency throttling are real operational concerns

The Azure Storage provider docs explain that full orchestration history may be loaded into memory and that this can create memory pressure. The guidance includes reducing history size and limiting concurrency.

What to borrow:

- runtime history growth must be observable
- concurrency limits and active-instance limits matter operationally
- large payloads and long histories should be considered anti-pattern signals

Best takeaway for OrcaCore:

Operational statistics should eventually include history size, active instance pressure, and oldest-running-instance indicators.

## 10. Durable mode versus ephemeral mode should stay explicit

Durable Functions is built around durable orchestration. OrcaCore is not required to be. That is a useful difference.

What to borrow:

- make durable guarantees explicit
- avoid pretending in-memory execution has durable semantics

Best takeaway for OrcaCore:

Keep the feature matrix explicit: some features are ephemeral-safe, some are durable-only.

## Strongest Durable Functions-derived recommendations

OrcaCore should adopt these ideas strongly:

1. A clear orchestration-side-effect boundary.
2. First-class external events and durable timers.
3. Version-bound workflow instances.
4. Explicit handling of unresolved waits/timers at completion.
5. Visibility into history/checkpoint growth and runtime pressure.
6. Parallel branch execution with explicit fan-in semantics.

## What not to import as-is

- Azure Functions hosting assumptions.
- Replay-only authoring as the only supported model.
- Task-hub/storage-provider specifics as product semantics.
- Platform-specific operational knobs without a provider-neutral contract.

## Acceptance criteria to add

### AT-041: Instance is bound to definition version

Given a workflow instance started from definition version N
When newer versions of the definition are deployed
Then the instance remains bound to version N according to documented versioning rules

### AT-042: Incompatible definition change does not silently corrupt durable instance

Given a durable workflow instance paused under an older definition version
When an incompatible newer version is deployed
Then the engine either continues safely under version rules or fails with explicit versioning diagnostics
And it never silently resumes with corrupted semantics

### AT-043: Timer and event race resolves deterministically

Given a workflow waiting for an external event and a timeout timer simultaneously
When either the event or timer wins the race
Then the losing timer or wait is canceled or ignored according to explicit policy
And terminal behavior is deterministic

### AT-044: Workflow does not complete with unresolved runtime-owned waits unless policy allows it

Given a workflow with outstanding runtime-owned wait or timer records
When the workflow reaches an apparent terminal path
Then completion is rejected or unresolved work is canceled according to explicit policy

### AT-045: History/checkpoint pressure is observable

Given long-running workflows with growing history or checkpoint volume
When operational statistics are queried
Then the engine exposes enough information to detect history growth and runtime pressure

## Final conclusion

Durable Functions contributes some of the strongest runtime ideas available for OrcaCore, especially around:

- external-event waits
- durable timers
- fan-out/fan-in structure
- orchestration versus side-effect boundaries
- version-aware long-running instances
- operational awareness of history growth

The most important thing to borrow is not Azure hosting. It is the rigor of orchestration semantics.
