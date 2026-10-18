# OrcaCore Ephemeral Engine Developer Guide

This guide describes the selected v1 application surface. The exact authority is the
[selected-mode capability matrix](specs/17-selected-mode-capability-matrix.md) together with the
[public authoring declaration companion](specs/17-public-authoring-contract.cs). If an example or
implementation detail conflicts with either source, the matrix and declaration companion win.

## Packages and host role

An ephemeral application references `OrcaCore` for authoring and application contracts and
`OrcaCore.Engine.Ephemeral` for the engine host role. Register that role with
`AddOrcaCoreEphemeralEngine(EphemeralEngineHostOptions)`.

`EphemeralEngineHostOptions` is programmatic configuration. It contains:

- `StructuredExecution`, including the positive per-instance execution-path ceiling;
- `TransientPools`, the copied host-local catalog used by named-step transient throttles;
- nullable `TerminalRetention`, the optional immutable host-wide terminal policy.

`EphemeralTerminalRetentionPolicy` is implemented in `OrcaCore.Hosting`, owned by this engine
package. For example, set `TerminalRetention = EphemeralTerminalRetentionPolicy.Create(128,
TimeSpan.FromMinutes(10), TimeSpan.FromSeconds(1))`. The source, exact API baseline and compile
fixtures include these members together. Fresh external package-consumer qualification is
still a separate gate; this guide does not claim a released package version contains them.

The engine role is mutually exclusive with the durable engine role in one service collection.
There is no catch-all registration method, configuration-binder overload, hosted-service toggle,
or serializer replacement hook.

## Authoring model

Start a definition through `Workflow.Ephemeral<TState>(DefinitionId, DefinitionVersion)`. The
staged builder requires one root initialization and one root terminal. `Build()` throws on invalid
structure; `TryBuild()` returns the same ordered diagnostics without mutating the authoring session.

The v1 ephemeral mode supports:

- named steps and ephemeral lambda steps;
- ordinary `Wait` and `Delay` boundaries;
- nested `If` and linear nested sequences/bodies;
- root `While`, root fixed `Parallel`, and root bounded `ForEach`;
- workflow deadlines plus eligible step retry, timeout, and transient-concurrency decorators;
- named-step transient-pool throttling and typed `End` output.

Fixed branch and item bodies receive isolated state. A successful join changes the parent only
through its explicit merge. `Parallel` results preserve authored branch order. `ForEach` admits
items deterministically by item index and applies the lower of the host execution-path ceiling and
the node-local admission limit.

Authoring handles are session-, epoch-, and scope-bound. After a root terminal freezes the
definition, or after a join advances its authoring epoch, stale or escaped handles are rejected
without changing the graph.

## Step context and state

Each attempt runs on a codec-detached copy of the last committed root state. Only the winning
attempt commits its replacement. `StepContext<TState>` exposes the attempt-local state and permits
explicit replacement through `ReplaceState`.

When a wait resumes, `StepContext.ResumedEvent` is a detached `EventEnvelope` only for the first resumed
step. Its metadata is read-only and its payload is obtained through `GetPayload<TPayload>()`. Later
steps see no resumed event. `StepContext.ForEachItem` exposes the stable authored item index inside
an item body and is `null` outside that scope.

Application state, inputs, event payloads, and typed outputs use the fixed certified
`orcacore-json-v1` codec. Unsupported graphs fail before commit. The codec has no application or DI
replacement seam.

## Registration, start, and inspection

Register a built definition through `IWorkflowDefinitionRegistry`. Registration returns a closed
result: registered, host-incompatible, or fingerprint conflict. Use `GetHandleOrThrow()` only as a
cast-free success projection after inspecting or intentionally accepting that result.

Typed definition handles start or reopen instances without exposing an engine implementation.
Start idempotency binds the key to the definition fingerprint and deterministic input fingerprint.
An instance handle provides:

- detached snapshot and typed root-state queries;
- nonblocking typed output inspection and notification-driven `WaitForOutputAsync`;
- cooperative cancellation request;
- immediate fenced termination.

Local cancellation of `WaitForOutputAsync` cancels only the caller's wait, not the workflow.

## Retained-instance lifetime

The null/omitted policy keeps accepted instances and their keys for the host lifetime and adds
no retention timer. An opted-in `EphemeralTerminalRetentionPolicy.Create(maxRetainedInstances,
maxAge, sweepInterval)` sets one host-wide settled-terminal count and maximum age, across all
definitions. Count must be at least one; age and interval must be positive, with interval no
greater than age or one day. Parameters fail in signature order. All positive intervals within
those limits are valid, even one tick; tiny intervals can keep sweeps busy and consume CPU, so
choose an operationally sensible cadence. Registration owns a copied policy value and includes
enabled state and all values in role agreement.

Only physically settled immutable terminals without admitted-operation pins are eligible.
Active/waiting/delayed/unsettled work remains protected. Age uses monotonic settlement time,
does not refresh on reads/replays, and is enforced by idle sweeps. Count pressure can evict sooner.
Retirement removes runtime payloads, facade/instance/start bindings, router deduplication and
obsolete timer/notification records together; it retains definitions and creates no unbounded
replacement tombstones. Protected/pinned work and caller-held copies can exceed the retained
count, and payload size/process memory are not capped by it. Retirement never frees a physical
slot early or proves application-owned external effects stopped.

A handle is identity, not a lifetime lease. A query/wait admitted before retirement captures a
detached result under a transient pin; local wait cancellation releases only its own pin.
Operations admitted after retirement throw `WorkflowInstanceNotFoundException` for the original
ID. Keeping a handle does not keep payloads alive; already detached results remain usable.

There is a real gap between `StartOrGetAsync` returning `Accepted` and a direct or
`start.WaitForOutputAsync(token)` call being admitted. Other completions can retire that already
completed job by count during the gap. **Not-found after `Accepted` does not mean the run never
started.** Its step may have produced an effect, and replaying the now-expired key starts a fresh
ID with `WasExisting = false` and may repeat that effect. Size `MaxRetainedInstances` above the
completions possible during the caller's start-to-observe gap; preserve independent effect
deduplication or choose durable coordination when longer guarantees are required. A larger
retained count reduces exposure but does not establish effect-safe retry.

The contract also corrects unreachable provisional-start orphans with retention off. Start-token
cancellation after runtime creation but before acceptance, or an exception after `Init`, fences
the unpublished unit to `Terminated` through its lane (preserving any prior terminal status),
returns the original error, publishes no key/handle, and eagerly releases owned data after
physical settlement. Acceptance winning the race returns `Accepted`; later cancellation of the
start token is not workflow cancellation. Internal abort telemetry is separate from a caller's
termination command. This runtime correction is also pending the source/acceptance gates.

## Waits and external event ingress

Ephemeral `Wait` is an in-process suspension owned by the ephemeral engine. The application surface
does not expose a durable acknowledgement or replay contract for sending events into that
process-local runtime.

External self-routing ingress is a durable-host capability in `OrcaCore.Durable.Hosting`.
`IWorkflowEventIngress` accepts `WorkflowInboundEvent` through exactly four routes: direct instance,
correlation, definition fanout, and start-or-deliver. Durable acceptance persists ownership before a
matching wait is required; `Accepted` and `Duplicate` are the only outcomes that permit a broker
source to acknowledge. This durable boundary does not turn the ephemeral engine into a persistent
mailbox.

## Deliberately unavailable in v1

The future-capability registry is
[§13.4, “Future-capability registry”](specs/13-phasing-and-open-questions.md#134-future-capability-registry).
It records deferred capabilities—including `WhenFirst`, Saga, public child/external-job authoring,
nested structural fan-out and broad lifecycle/management operations—with
rationale and re-entry criteria. They are not callable v1 APIs. `WaitLong` and authored `Yield` are
removed rather than deferred and have no alias or placeholder.

## Verification

The consumer-facing contract is enforced by the developer-surface guards and fresh-package compile
fixtures. Runtime behavior is covered by the active acceptance, ephemeral-engine, and behavior-
scenario suites. Run commands from the repository root; the current gate request records the exact
configuration and expected counts for the target under review.

The superseded pre-v1 guide is preserved, unchanged, at
[`archive/plans/ephemeral-engine-developer-guide.md`](archive/plans/ephemeral-engine-developer-guide.md).
