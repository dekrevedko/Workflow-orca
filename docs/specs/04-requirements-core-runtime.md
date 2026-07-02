# 4. Core Runtime & Authoring Requirements (CR)

Scope: authoring model, execution model, state model, lifecycle, and the serialized-execution
guarantee. These requirements apply to **both** engines unless marked otherwise.

## 4.1 Authoring

### CR-001 Code-first fluent builder
The library SHALL expose a fluent builder as the primary public authoring path for workflow
definitions. Builders exist per axis combination as needed (regular vs saga; ephemeral vs
durable) so that durable-only and saga-only constructs are absent from surfaces where they
are invalid (CR-020).

### CR-002 Immutable, validated definitions
`Build()` SHALL produce an immutable definition graph. Runtime execution SHALL NOT depend on
mutable definition state. Build-time validation SHALL verify structural integrity (matching
`Parallel`/join, no orphan branches, presence of `Init`/`End`, legal policy combinations,
no durable-only primitives in ephemeral definitions) and SHOULD report **all** validation
errors together (accumulated validation), not only the first.

### CR-003 Composite definition structure
Definitions SHALL form a tree: infrastructure steps and business steps share a common step
abstraction; container steps (`If`, `While`, `Parallel`, scopes) contain child steps. The
interpreter walks the tree uniformly. Nesting depth MAY be limited in early phases, but limits
SHALL be explicit build-time errors, never undefined runtime behavior.

### CR-004 Definition identity and versioning
Every definition SHALL carry `DefinitionId` and `DefinitionVersion`. Registration of a
definition version SHALL be explicit; instances bind to the version at start (see DU-040).

### CR-005 `Init` owns input-to-state construction
The `Init` step SHALL be the single owner of converting start input into initial business
state. No parallel initialization path (e.g., a factory function on the definition root)
SHALL exist.

### CR-006 Policies are decorators, not steps
Retry, timeout, cancellation, compensation binding, idempotency, visibility/telemetry tags,
and resource-pool hints SHALL be modeled as declarative decorators attachable to steps,
scopes, or definitions. The first supported policy set SHOULD be: retry, timeout,
cancellation, compensation (sagas), idempotency. Retry policies SHALL be structured
(`MaxAttempts`, backoff, optional retry predicate, explicit terminal condition), bounded, and
MUST NOT produce duplicate committed outcomes.

### CR-007 Serialized definition format is deferred
No YAML/JSON/DSL definition format SHALL be introduced until code-first semantics are stable.
If introduced later, DSL semantics SHALL be verified for parity with code-first behavior by
the same acceptance suite.

### CR-008 Named End outcomes
`End` MAY declare a named outcome (e.g. `Approved`, `TimedOut`, `FailedValidation`). The
outcome name SHALL be recorded in runtime metadata (queryable and filterable through the
management surface) and SHALL be carried on the completion lifecycle event and any configured
end-of-life publication (EV-060). Named outcomes do not introduce new statuses: the lifecycle
status remains the terminal state; the outcome is metadata describing *which* end was
reached.

## 4.2 Execution model

### CR-010 Interpreter-owned orchestration
The runtime SHALL interpret the definition graph and own all orchestration transitions
(status, execution position, waits, branch state). Steps SHALL be passive: they execute and
return results; they never drive their own sequencing.

### CR-011 Step results carry control intent only
Step outcomes SHALL be expressed through an immutable result object (closed set, exhaustively
handled by the runtime): at minimum `Completed`, `Failed(error)`,
`WaitForEvent(eventName, correlationId)`, `Yield`. Results SHALL NOT carry business-state
mutations. Steps mutate business state directly through the execution context.

### CR-012 Orchestration ownership rule
Steps MUST NOT call back into the runtime, spawn work that outlives the step, use exceptions
as orchestration signals, or otherwise make orchestration decisions outside the result
channel. This SHALL be enforced by API shape (the step context exposes no runtime surface)
and documented as the authoring contract. The runtime decision path SHALL be deterministic
given the same inputs — the prerequisite for durable replay.

### CR-013 Async-first execution
All step execution and all potentially work-performing public operations SHALL be async and
SHALL accept a `CancellationToken`.

### CR-014 Failure semantics
A step that returns `Failed` or throws an unhandled exception SHALL move the instance to
`Failed` (or trigger saga compensation per SG rules). Later steps SHALL NOT execute. Error
details (type, message, step identity, timestamp) SHALL be captured in runtime state and be
inspectable through the management surface.

### CR-015 Execution position as a stack
The runtime SHALL track position in the definition graph as a call-stack of frames (step +
enclosing containers), not a flat index, so that nested structures (branch inside `Parallel`
inside `If`) and rehydration are represented exactly.

### CR-016 Synchronous completion bridge
The product SHALL define how a caller awaits completion of a short-running workflow (e.g., a
start API returning a handle that can be awaited or polled to a terminal snapshot), and how
that differs from long-running durable execution. Awaiting SHALL NOT expose live internal
state.

### CR-017 `Yield` semantics (cooperative checkpoint)
`Yield` SHALL mean: commit the business-state progress made so far, release the instance's
execution lane, and reschedule continuation of the **same step**. The instance status remains
`Running`. In durable mode each yield is a commit boundary — yielded progress survives a
crash, and the continuation resumes from committed state. The rescheduled continuation obeys
non-reentrancy (CR-042) and resource governance (MG-060). Intended uses:

- **chunked long-running work** — each chunk commits, so a crash never redoes committed
  chunks;
- **loop and fanout bodies** (`While`, `ForEach`) — a yield per iteration/item gives each a
  commit boundary and lets the engine interleave other items fairly under `maxConcurrency`;
- **`Parallel` branches under governance** — a long step yields so one branch does not
  monopolize a governed execution lane (MG-060/061) while siblings starve.

Steps using `Yield` MUST make their work resumable from committed state (no reliance on
locals across yields).

## 4.3 State model

### CR-020 Runtime state / business state separation
Every instance SHALL carry engine-owned runtime state and workflow-owned typed business state
as distinct categories (see glossary). Runtime metadata SHALL be queryable without
deserializing business state. Business state SHALL be serializable/deserializable across
persistence boundaries when persistence exists.

### CR-021 No live-instance leakage
Public APIs SHALL NOT expose mutable internal instance objects. `Start` returns a
metadata-only immutable snapshot; business state is readable only via a typed accessor that
returns a copy and fails with clear diagnostics on type mismatch.

### CR-022 Instance identity
Instances SHALL carry: `InstanceId` (globally unique, stable), `DefinitionId`,
`DefinitionVersion`, and a monotonic version/epoch for optimistic concurrency. Optional:
`StartIdempotencyKey`, `ParentInstanceId`, `RootInstanceId`. `InstanceId` identifies the
logical execution, never the current activation.

## 4.4 Lifecycle

### CR-030 Explicit transition table
Instance (and branch) lifecycle SHALL be defined by an explicit transition table with named
triggers. Illegal triggers SHALL be rejected with clear errors — not silently ignored.
Terminal states SHALL reject all triggers. Shared statuses (both modes): `Running`,
`Waiting`, `Completed`, `Failed`, `Cancelled`, `Terminated`; durable mode adds `Paused`
(see MG-013 for pause/resume semantics).

### CR-031 Terminal behavior is precise
The behaviors of the terminal paths are:

- **Cancel (graceful, both modes).** Cooperative stop: the runtime signals in-flight steps
  via `CancellationToken` and allows them to observe cancellation; active waits and timers
  move to `Cancelled` (EV-044); cancellation policies apply (and for sagas, cancellation
  never implies compensation — SG-011); the instance then commits `Cancelled`.
- **Terminate (forced, both modes).** Immediate operator stop: no cooperative wait for
  in-flight work beyond the current commit boundary; runtime-owned waits and timers are
  forcibly resolved; no policies or compensation run; the instance commits `Terminated`.
- **Complete / Fail.** Per CR-014 and CR-032; the terminal state and its lifecycle event are
  consistent (MG-021 — no state without its event, no event without the state).
- **Host stop.** Correctness never depends on shutdown handling: all committed transitions
  were persisted at safe boundaries (DU-021); on restart, instances resume from committed
  state.

For each path, which lifecycle events fire and in what order SHALL be documented and tested.

### CR-032 Completion with unresolved runtime-owned work
An instance SHALL NOT reach `Completed` while runtime-owned waits or timers are unresolved,
unless an explicit policy cancels or ignores them. The chosen policy outcome SHALL be
observable.

## 4.5 Serialized execution guarantee

### CR-040 One logical mutator per instance
For a given `InstanceId`, committed state transitions SHALL appear in a single serial order.
Concurrent commands/events/branch completions racing on one instance SHALL resolve into an
outcome equivalent to some valid sequential ordering. This guarantee holds in both modes and
across all features.

### CR-041 Enforcement layering
- Ephemeral / single host: per-instance serialization primitive (lock or mailbox).
- Durable: optimistic concurrency on the persisted version/epoch as the minimum
  cross-provider correctness boundary (expected-version append in the event-driven core).
- Future multi-node: optional per-instance execution lease layered on top.
The enforcement mechanism is an implementation choice; the observable guarantee is not.

### CR-042 Non-reentrancy by default
While an instance is executing, new commands/events for the same instance SHALL be queued,
retried, or rejected per explicit policy — never interleaved. Reentrancy, if ever offered,
is a separate advanced feature.

### CR-043 Atomic transitions
Each accepted mutation SHALL commit atomically. Failed attempts MUST NOT partially advance
observable state. Concurrency conflicts SHALL produce retry, no-op, or rejection per explicit
policy. After a crash, recovery restores the last committed state only.

### CR-044 Parallelism composes with serialization
Parallel branches MAY execute concurrently (real concurrency for I/O), but all branch-state
mutations and join checks SHALL commit through the per-instance serialized path
(see CP requirements).
