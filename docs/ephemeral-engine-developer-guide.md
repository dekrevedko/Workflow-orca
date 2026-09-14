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
- `TransientPools`, the copied host-local catalog used by named-step transient throttles.

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

## Event delivery

Create a payloadless or typed `WorkflowEvent` and deliver it through `IWorkflowEventClient` using
one of exactly two routes:

- `DeliverToInstanceAsync` for one `InstanceId`;
- `DeliverByCorrelationAsync` for one `(DefinitionId, EventName, CorrelationId)` route.

Delivery is a non-buffering signal stream. If no matching active wait exists, the result is
`NoActiveWait` and the `EventId` is not consumed, so the same envelope can be redelivered later.
Duplicate and conflict classification is per target instance. A competing active correlation pair
is rejected at wait registration rather than resolved by a delivery-time tie breaker.

## Deliberately unavailable in v1

The future-capability registry is
[§13.4, “Future-capability registry”](specs/13-phasing-and-open-questions.md#134-future-capability-registry).
It records deferred capabilities—including `WhenFirst`, Saga, public child/external-job authoring,
nested fan-out, definition-targeted event fanout, and broad lifecycle/management operations—with
rationale and re-entry criteria. They are not callable v1 APIs. `WaitLong` and authored `Yield` are
removed rather than deferred and have no alias or placeholder.

## Verification

The consumer-facing contract is enforced by the developer-surface guards and fresh-package compile
fixtures. Runtime behavior is covered by the active acceptance, ephemeral-engine, and behavior-
scenario suites. Run commands from the repository root; the current gate request records the exact
configuration and expected counts for the target under review.

The superseded pre-v1 guide is preserved, unchanged, at
[`archive/plans/ephemeral-engine-developer-guide.md`](archive/plans/ephemeral-engine-developer-guide.md).
