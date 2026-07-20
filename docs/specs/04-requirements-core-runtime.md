# 4. Core Runtime & Authoring Requirements (CR)

Scope: authoring model, execution model, state model, lifecycle, and the serialized-execution
guarantee. These requirements apply to **both** engines unless marked otherwise.

## 4.1 Authoring

### CR-001 Code-first fluent builder
The library SHALL expose a fluent builder as the primary public authoring path for workflow
definitions. Separate ephemeral and durable staged builders expose only the capabilities valid
for the selected mode; saga has no v1 builder or placeholder surface.
Authors SHALL select ephemeral or durable mode before mode-specific methods are available,
and the shared compiler SHALL reject unsupported manually constructed graph nodes. The
normative capability and signature baseline is
[document 17](17-selected-mode-capability-matrix.md).

### CR-002 Immutable, validated definitions
`Build()` SHALL invoke the shared `DefinitionCompiler` and produce an immutable definition
backed by its compiled plan. `TryBuild()` SHALL invoke the same compiler and return
`Validation<TDefinition>` with every discoverable graph-wide diagnostic without publishing a
definition. Runtime execution SHALL NOT depend on mutable definition state. Compilation SHALL
verify structural integrity, selected-mode capability support, result/merge compatibility,
serializer availability, configured limits, and complete successful-path termination.
`Build()` SHALL throw one definition exception containing the same accumulated diagnostics.

### CR-003 Composite definition structure
Definitions SHALL form a tree: infrastructure steps and business steps share a common step
abstraction; container steps (`If`, root `While`, root `Parallel`, root `ForEach`, and lease
scopes) contain child steps. Of the conditional/loop/fanout operators, only `If` may nest in v1.
`While`, `Parallel`, and `ForEach` SHALL be absent from nested, branch, item, and leased builders
and rejected by the compiler on
hand-built nested graphs. The interpreter walks the tree uniformly; every nesting/depth
limit is an explicit build-time error, never undefined runtime behavior.

### CR-004 Definition identity and versioning
Every definition SHALL carry validated immutable reference values `DefinitionId` and
`DefinitionVersion`; `DefinitionId.New()` SHALL never return `Guid.Empty`, `Parse` SHALL reject
its canonical text, `TryParse` SHALL return false/null for it, and the version SHALL reject
non-positive values. Registration of a definition
version SHALL be explicit; instances bind to the version at start (see DU-040).
The compiled format and deterministic **structural** plan fingerprint are immutable for that
identity/version. It covers node/member kinds and order, strong authored values, referenced
step/workflow types, static request values, and codec format. Registering another structure
under the same identity/version SHALL return a typed fingerprint conflict. Selector, projector,
merge/output bodies, step configuration, DAG mapping logic, and external-request construction
are opaque to v1 fingerprinting; changing any of them SHALL use a new `DefinitionVersion`.

### CR-005 `Init` owns input-to-state construction
The staged init builder SHALL expose only `Init<TInput>`, which is the single owner of converting
typed start input into private initial business state. `Init` transitions to the typed workflow
body builder. No parallel initialization path on the definition root SHALL exist.

### CR-006 Policies are decorators, not steps
`WithRetry(int maxAttempts, TimeSpan? fixedDelay = null)`, `WithStepTimeout(TimeSpan)`, and
ephemeral-only transient-pool guarding SHALL be declarative decorators applied to the immediately
preceding business step. Their relative order does not change semantics; each may appear at most once
for that step, and duplicate/misplaced decorators fail at the fluent call. `maxAttempts` is
positive and includes the initial policy attempt; `fixedDelay` is optional, fixed between attempts,
and non-negative. V1 has no retry predicate, backoff family, terminal-condition callback,
definition-wide retry, or failed-instance retry. `CompleteWithin` and persisted
`AcquireResources` are structural nodes rather than step decorators.

`StepResult.Failed`, a normalized unhandled business-step exception, and
`StepAttemptTimeoutException` consume an attempt and retry when another attempt remains.
Cancellation/termination/workflow deadline, authoring or codec validation, lease loss, runtime
invariant failure, and expected-version commit conflict SHALL NOT consume an authored retry.

### CR-007 Serialized definition format is deferred
No YAML/JSON/DSL definition format SHALL be introduced until code-first semantics are stable.
If introduced later, DSL semantics SHALL be verified for parity with code-first behavior by
the same acceptance suite.

### CR-008 Typed `End` and fixed outcome metadata
Every ordinarily completing workflow SHALL have exactly one root `End`. `End()` builds a
resultless typed workflow; `End<TOutput>(selector)` projects the last committed root-state snapshot
to an immutable typed output. Their named overloads MAY carry one fixed authored
`WorkflowOutcomeName`; unnamed completion uses the parameterless or selector-only overload, never
an explicit null. The output and optional fixed outcome SHALL commit atomically with root
completion and be queryable separately from in-progress state. A perpetual durable workflow SHALL
instead select exactly one unconditional root `ContinueAsNew` generation terminal and SHALL have
no `End`; `ContinueAsNew` does not satisfy an ordinary-completion `End` requirement. There is no
dynamic outcome-name selector in v1; dynamic business classification belongs in `TOutput`.
Null/default/empty outcome values are rejected rather than treated as unnamed. Outcome metadata
does not add statuses or structural exits: status remains `Completed`, and failure, cancellation,
termination, deadline, or runtime-invariant paths do not execute `End`.

### CR-009 Shared compiler diagnostics
The mode-first workflow builders and the separate `OrcaCore.Dag` front-end SHALL share one
compiler/validation contract.
Compiler diagnostics SHALL have stable machine-readable codes and structured authored-node,
instruction, scope, and branch locations where applicable. Codes and locations are contract;
human-readable messages MAY improve. Diagnostics SHALL be deterministic and ordered by
authored graph location and then code. The reserved code families and approved public
signatures are defined in [document 17](17-selected-mode-capability-matrix.md).
Document 17's complete workflow/DAG build and runtime-defense catalog is exhaustive for Phase 0:
no undocumented or duplicate-meaning emitted code is permitted. `AuthoredLocation.Value` SHALL
use its invariant `workflow:$`/`dag:$` slash-token grammar, zero-based eight-digit ordinals, and
canonical primary/related ordering without localized/free-form text. Every public runtime
failure SHALL derive from `OrcaCoreException`, expose one nonblank stable `Code`, and follow the
authoritative exception/failure mapping; normalized arbitrary author/integration exceptions use
the documented generic code rather than CLR type/message identity.

## 4.2 Execution model

### CR-010 Interpreter-owned orchestration
The runtime SHALL interpret the definition graph and own all orchestration transitions
(status, execution position, waits, branch state). Steps SHALL be passive: they execute and
return results; they never drive their own sequencing.

### CR-011 Step results carry control intent only
Step outcomes SHALL be expressed through an immutable result object (closed set, exhaustively
handled by the runtime): at minimum `Completed`, `Failed(error)`,
and `WaitForEvent(EventName, correlationId)`. The dynamic wait result exists only for a
business step that must select its event or correlation after it runs; structural `Wait` is
  preferred. Results SHALL NOT carry business-state mutations. Every attempt receives a
  fixed-codec-detached copy of the last committed root/branch/item state. Mutable state may be
  changed through `StepContext<TState>.State`; immutable/value state is replaced with
  `ReplaceState`. Only the winning successful attempt may commit its copy. Failed, timed-out,
  fenced, and token-ignoring late attempts are discarded. Author-controlled `Yield` is absent.

### CR-012 Orchestration ownership rule
Steps MUST NOT call back into the runtime, spawn work that outlives the step, use exceptions
as orchestration signals, or otherwise make orchestration decisions outside the result
channel. This SHALL be enforced by API shape (the step context exposes no runtime surface)
and documented as the authoring contract. The runtime decision path SHALL be deterministic
given the same inputs — the prerequisite for durable replay. The execution context exposes
typed state and `ReplaceState`, resumed event, deterministic time, item/lease context where
applicable, and `StepExecutionContext` through its `Execution` property; it exposes no
orchestration or persistence service.

### CR-013 Async-first execution
All step execution and all potentially work-performing public operations SHALL be async and
SHALL accept a `CancellationToken`.

### CR-013a Ephemeral-only lambda steps
Ephemeral builders MAY author synchronous or asynchronous lambda step bodies for concise local
workflows. Durable builders SHALL require named step types created through host dependency
injection; captured delegates have no stable durable code/version identity and are absent from
the durable surface.

### CR-014 Failure semantics
A root-sequence step that returns `Failed` or throws after policy exhaustion SHALL move the
instance to `Failed`; later root steps do not execute. Inside a root `Parallel` branch or root
`ForEach` item, the
failure terminates that branch/item and is retained until the all-terminal join: `WhenAll`
fails the scope after every sibling finishes, while `WhenAllOutcomes` supplies it as typed data
to the merge. Neither join automatically cancels a sibling. Error details (type, message,
operation/attempt identity, timestamp) SHALL be captured in runtime state and be inspectable
through the management surface.

### CR-015 Execution position as structured fibers and scopes
The runtime SHALL track execution through a compiled plan containing stable instruction,
scope-plan, branch, and merge identities. Each active fiber SHALL carry one linear
instruction position and fiber-local progress; recursive execution-scope records SHALL carry
parent preservation, child membership, join/merge state, scheduler position, loop and scope
entry progress, and owned obligations. A frame-stack or cursor path SHALL NOT be the
canonical position or ownership model. The complete fiber/scope state SHALL represent nested
structures and rehydration exactly.

### CR-016 Synchronous completion bridge
Resultful `WorkflowInstanceHandle<TOutput>` SHALL expose notification-driven
`WaitForOutputAsync(CancellationToken)`, and the typed `WorkflowStartResult` helper of the same
name SHALL project through `GetHandleOrThrow()` so callers need neither casts nor union switches
on the success path. `DagRunHandle` SHALL expose notification-driven
`WaitForTerminalAsync(CancellationToken)`. Each wait registers its notification before one
authoritative recheck, completes without polling when terminality races registration, and never
exposes live internal state. Caller cancellation cancels only that local wait and SHALL NOT
request workflow, DAG-run, or child cancellation. A terminal workflow without output throws
typed `WorkflowOutputUnavailableException` carrying status and optional `WorkflowFailure`.

### CR-017 Runtime-owned execution quantum
Fiber turns, checkpoint cadence, and fair rescheduling are runtime decisions. Reaching the
configured instruction/time admission budget SHALL commit any already-completed transitions,
leave a continuation when work remains runnable, release the instance lane, and later resume
from the persisted position. No `Yield` result, builder member, alias, or placeholder is
author-visible in v1. Long-running application work must be split into bounded idempotent steps
or executed externally; an in-flight business step is not forcibly preempted.

### CR-018 Workflow and step time bounds
`CompleteWithin` SHALL set one absolute workflow deadline measured from instance start and
include admission, retries/fixed delays, delays, event waits, lease queueing, every
`ContinueAsNew` generation, and cleanup/quarantine decisions. Durable mode persists the same
absolute deadline across restart and rollover. When it wins, the runtime commits terminal
`TimedOut` with `WorkflowDeadlineExceededException`, stops new admission, cancels runtime-owned
wait/timer obligations, signals attempt tokens, suppresses branch/item merges, and performs
definite cleanup or quarantine without waiting forever for token-ignoring bodies.

`WithStepTimeout` SHALL establish one absolute deadline before dispatch for a retry-policy attempt
coordinate, including any uncertain host-loss redispatch under that coordinate. Durable mode SHALL
persist that deadline; ephemeral mode SHALL retain it only for the in-memory run. Reaching the
deadline before a winning result wins the timeout race, fences and discards that attempt copy,
and reports `StepAttemptTimeoutException`; a remaining retry
keeps `StepOperationId`, commits the next `AttemptNumber`, and receives a new deadline. A timed-out
body may
continue physically if it ignores its token, retaining its physical step-throttle/transient slot
until return, but it has no commit authority. Timeout uses runtime timers and `TimeProvider`, is
not defined by Polly, and never proves protected external work stopped.

Inside a durable lease scope, a timeout/ambiguous submit/recovered in-flight attempt moves the
live lexical obligation to capacity-reserving `AmbiguousHeld` while retaining the same
`StepOperationId`, `LeaseProtectionToken`, exact tickets, and units. A later retry SHALL NOT start
in the same process until a token-ignoring prior body returns; host-loss recovery MAY redispatch
the same attempt coordinate and identities because the old process body is gone. Retry success alone SHALL NOT erase
ambiguity. Ambiguous exit, exhaustion, cancellation, workflow deadline, forced termination, or
abandonment transfers to `Quarantined` before progression, and only trusted stop/fence proof may
release it.

### CR-019 Stable step-operation identity
Every business-step context SHALL expose `InstanceId`, runtime-created
`StepOperationId`, and positive `AttemptNumber`. One operation ID identifies one logical step
visit and remains stable across retries, timeout reconciliation, replay, process replacement,
expected-version conflicts, and competing durable drivers. A loop re-entry, another branch,
another `ForEach` item, or a new continue-as-new generation receives a new ID. `AttemptNumber` is
a retry-policy attempt coordinate, starts at one, and counts against `maxAttempts`; it is not a
count of physical CLR dispatches. Durable mode SHALL commit the operation ID, attempt coordinate,
optional absolute attempt deadline, and in-flight dispatch marker before dispatch. Uncertain
host-loss replay reuses that same coordinate and deadline and MAY physically redispatch without consuming another attempt;
only a committed retry transition increments the coordinate. If the persisted attempt deadline
has expired during recovery, the runtime records that same attempt as timed out before dispatch
and either commits the next retry coordinate or exhausts the existing budget. This remains true
when `maxAttempts == 1`: uncertain redispatch reuses attempt one and never creates attempt two.
Ephemeral mode uses the same public shape without claiming crash survival. `AttemptNumber` remains
public diagnostics
and SHALL NOT be used as an external idempotency key;
provider/companion certification SHALL verify external create-or-observe identity uses
`StepOperationId` and SHALL record this arbitrary-adapter behavior as a trust boundary rather
than claim core can inspect it.

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
`StartIdempotencyKey`, `ParentInstanceId`, `RootInstanceId`. `StartIdempotencyKey` is a
caller-selected start-deduplication identity and SHALL NOT be accepted where an `InstanceId`
is required. `InstanceId`, `WaitId`, and `DagRunId` parsers SHALL reject canonical `Guid.Empty`
text through throw/false-null semantics. `InstanceId` identifies the logical execution, never
the current activation.

## 4.4 Lifecycle

### CR-030 Explicit transition table
Instance (and branch) lifecycle SHALL be defined by an explicit transition table with named
triggers. Illegal triggers SHALL be rejected with clear errors — not silently ignored.
The public `WorkflowInstanceStatus` set in both modes SHALL be `Pending`, `Running`, `Waiting`,
`CancellationRequested`, `Completed`, `Failed`, `TimedOut`, `Cancelled`, and `Terminated`.
`Completed`, `Failed`, `TimedOut`, `Cancelled`, and `Terminated` are immutable terminal states;
v1 never reopens them. `Paused` and `Parked` are not first-release public statuses.

### CR-031 Terminal behavior is precise
The behaviors of the terminal paths are:

- **Cancellation request (graceful, both modes).** `RequestCancellationAsync` commits
  `CancellationRequested` when work remains, signals in-flight steps via `CancellationToken`,
  cancels active runtime waits/timers, and eventually commits `Cancelled` once definite cleanup
  or required `AmbiguousHeld -> Quarantined` lease transfer is recorded before progression.
  Repeated requests are idempotent.
- **Terminate (forced, both modes).** Immediate operator stop: no cooperative wait for
  in-flight work beyond the current commit boundary; runtime-owned waits and timers are
  forcibly resolved; no policies or compensation run; the instance commits `Terminated`.
  Durable resource ownership for protected external work is fenced from workflow resume and
  transferred to capacity-reserving terminal cleanup/quarantine until confirmed stop or an
  end-to-end protected-resource fence proves release safe (MG-064). `Terminated` alone is not
  proof that external work stopped.
- **Complete / Fail.** Per CR-014 and CR-032; the terminal state and its lifecycle event are
  consistent (MG-021 — no state without its event, no event without the state).
- **Workflow deadline.** Per CR-018, the deadline winner commits `TimedOut` with
  `WorkflowDeadlineExceededException`; it is distinct from cooperative cancellation and forced
  termination, commits any required `AmbiguousHeld -> Quarantined` transfer before terminal
  progression, and never reopens.
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
Each accepted mutation SHALL commit atomically. Every attempt operates on a codec-detached copy;
failed, timed-out, fenced, or late attempts MUST NOT partially advance observable state. Only
one successful attempt owns the winning state/position commit. Concurrency conflicts SHALL
produce retry, no-op, or rejection per explicit policy. After a crash, recovery restores the
last committed state plus the persisted operation/attempt coordinate and optional attempt
deadline; an uncertain dispatch is replayed under CR-019 without inventing another retry attempt.

### CR-044 Parallelism composes with serialization
Root `Parallel`, `WhenAll`/`WhenAllOutcomes`, and bounded root `ForEach` branches/items SHALL
execute as
deterministically scheduled cooperative fibers: at most one **unfenced** attempt owns commit
authority for an instance. A timed-out token-ignoring body may still execute physically against
its discarded copy while a retry or sibling progresses, but it cannot commit. Branches SHALL use isolated input/private state and communicate
through typed results and explicit merge, while all fiber, scope, merge, and ownership
transitions commit through the per-instance serialized path. True concurrent work SHALL use
separate workflow instances, DAG child instances, or external systems governed by explicit
cross-instance resource limits (see CP and MG requirements).
